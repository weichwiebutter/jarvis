using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScalpingAssetRoadmapEntry(
    string Asset,
    IReadOnlyList<string> Aliases,
    int Priority,
    string MarketType,
    bool DataAvailable,
    string DataGap,
    string ResearchStatus,
    int CertifiedCandidates,
    string NextAction,
    IReadOnlyList<string> RiskNotes);

public sealed record ScalpingMultiAssetRoadmap(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Mode,
    IReadOnlyList<ScalpingAssetRoadmapEntry> Assets,
    IReadOnlyList<string> NextAssets,
    IReadOnlyList<string> AssetsWithData,
    IReadOnlyList<string> AssetsNeedingData,
    string RoadmapHealth,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingMultiAssetRoadmapService
{
    private static readonly (string Asset, string[] Aliases, int Priority, string MarketType, string[] RiskNotes)[] DefaultAssets =
    [
        ("XAUUSD", ["Gold", "XAU/USD"], 1, "metal_cfd", ["high_volatility", "spread_and_news_sensitivity", "certified_candidate_exists_but_not_portfolio_ready"]),
        ("GER40", ["DE40", "Germany40", "Germany 40", "DAX"], 2, "index_cfd", ["cash_index_session_gaps", "high_open_volatility", "requires_asset_specific_backtest"]),
        ("DE40", ["GER40", "Germany40", "Germany 40", "DAX"], 3, "index_cfd_alias", ["alias_requires_data_mapping", "do_not_transfer_xauusd_strategy"]),
        ("Germany40", ["GER40", "DE40", "Germany 40", "DAX"], 4, "index_cfd_alias", ["alias_requires_data_mapping", "do_not_transfer_xauusd_strategy"]),
        ("EURUSD", ["EUR/USD", "Euro Dollar"], 5, "forex_major", ["lower_spread_than_gold", "session_specific_liquidity", "requires_independent_certification"]),
        ("GBPUSD", ["GBP/USD", "Cable"], 6, "forex_major_optional", ["higher_news_sensitivity", "requires_independent_certification"]),
        ("USDJPY", ["USD/JPY", "Dollar Yen"], 7, "forex_major_optional", ["asia_session_relevance", "requires_independent_certification"]),
        ("NAS100", ["US100", "NASDAQ100", "Nasdaq 100"], 8, "index_cfd_optional", ["high_volatility_index", "us_session_dependency", "requires_independent_certification"])
    ];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingMultiAssetRoadmapService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio");
    public string RoadmapPath => Path.Combine(Root, "multi_asset_roadmap.json");
    public string RoadmapMarkdownPath => Path.Combine(Root, "multi_asset_roadmap.md");

    public ScalpingMultiAssetRoadmap Update()
    {
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var certifications = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReports();
        var entries = DefaultAssets.Select(asset => BuildEntry(asset, marketData, certifications)).ToList();
        var nextAssets = entries
            .Where(entry => entry.NextAction is "run_scalping_research" or "import_market_data")
            .OrderBy(entry => entry.Priority)
            .Take(4)
            .Select(entry => entry.Asset)
            .ToList();
        var assetsWithData = entries.Where(entry => entry.DataAvailable).Select(entry => entry.Asset).ToList();
        var assetsNeedingData = entries.Where(entry => !entry.DataAvailable).Select(entry => entry.Asset).ToList();
        var health = entries.Any(entry => entry.Asset == "XAUUSD" && entry.CertifiedCandidates > 0)
            ? assetsNeedingData.Count > 0 ? "building" : "ready_for_multi_asset_research"
            : "needs_primary_certification";
        var roadmap = new ScalpingMultiAssetRoadmap(
            ReportVersion: "scalping_multi_asset_roadmap_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Mode: "planned_research_only",
            Assets: entries,
            NextAssets: nextAssets,
            AssetsWithData: assetsWithData,
            AssetsNeedingData: assetsNeedingData,
            RoadmapHealth: health,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(Root);
        File.WriteAllText(RoadmapPath, JsonSerializer.Serialize(roadmap, JsonDefaults.WriteOptions));
        File.WriteAllText(RoadmapMarkdownPath, BuildMarkdown(roadmap));
        return roadmap;
    }

    public ScalpingMultiAssetRoadmap? Load()
    {
        return File.Exists(RoadmapPath)
            ? JsonSerializer.Deserialize<ScalpingMultiAssetRoadmap>(File.ReadAllText(RoadmapPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public ScalpingAssetRoadmapEntry? FindAsset(string asset)
    {
        var normalized = asset.Trim();
        var roadmap = Load() ?? Update();
        return roadmap.Assets.FirstOrDefault(entry =>
            entry.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || entry.Aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private static ScalpingAssetRoadmapEntry BuildEntry((string Asset, string[] Aliases, int Priority, string MarketType, string[] RiskNotes) asset, MarketDataAvailabilityService marketData, IReadOnlyList<ScalpingCertificationReport> certifications)
    {
        var quality = marketData.BuildQuality(asset.Asset);
        var certified = certifications.Count(report => report.Asset.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase)
            || asset.Aliases.Any(alias => report.Asset.Equals(alias, StringComparison.OrdinalIgnoreCase)));
        var dataAvailable = quality.DataGaps.Count == 0;
        var dataGap = dataAvailable ? "-" : string.Join(",", quality.DataGaps);
        var researchStatus = certified > 0 ? "certified_candidate_available" : dataAvailable ? "data_ready_research_pending" : "needs_market_data";
        var nextAction = certified > 0
            ? "search_additional_diverse_candidates"
            : dataAvailable ? "run_scalping_research" : "import_market_data";
        var riskNotes = asset.RiskNotes
            .Concat(["no_strategy_transfer_without_asset_specific_validation", "requires_backtest_oos_walkforward_robustness_certification_human_review"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ScalpingAssetRoadmapEntry(
            Asset: asset.Asset,
            Aliases: asset.Aliases,
            Priority: asset.Priority,
            MarketType: asset.MarketType,
            DataAvailable: dataAvailable,
            DataGap: dataGap,
            ResearchStatus: researchStatus,
            CertifiedCandidates: certified,
            NextAction: nextAction,
            RiskNotes: riskNotes);
    }

    private static string BuildMarkdown(ScalpingMultiAssetRoadmap roadmap) => $"""
# Scalping Multi-Asset Roadmap

- mode: {roadmap.Mode}
- health: {roadmap.RoadmapHealth}
- next_assets: {string.Join(", ", roadmap.NextAssets)}
- assets_with_data: {string.Join(", ", roadmap.AssetsWithData)}
- assets_needing_data: {string.Join(", ", roadmap.AssetsNeedingData)}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Asset Roadmap
{string.Join(Environment.NewLine, roadmap.Assets.Select(entry => $"- {entry.Asset}: priority={entry.Priority}, type={entry.MarketType}, data_available={entry.DataAvailable.ToString().ToLowerInvariant()}, certified={entry.CertifiedCandidates}, status={entry.ResearchStatus}, next_action={entry.NextAction}, data_gap={entry.DataGap}"))}

## Rules
- every asset requires its own data availability check
- every asset requires its own backtest, OOS, walkforward, robustness expansion, certification and human review
- no XAUUSD strategy may be transferred to another asset without independent validation
- roadmap is research/reporting only and does not execute trades
""";
}
