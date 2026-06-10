using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScalpingAssetRoadmapEntry(
    string Asset,
    IReadOnlyList<string> Aliases,
    int Priority,
    string MarketType,
    bool DataAvailable,
    string DataGap,
    string QuoteMappingStatus,
    string HistoricalDataStatus,
    string ResearchStatus,
    string SignalAgentSpecStatus,
    string ReadinessStatus,
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
        ("GER40", ["DE40", "Germany40", "Germany 40", "DAX"], 1, "index_cfd", ["priority_data_integration_target", "cash_index_session_gaps", "high_open_volatility", "requires_asset_specific_backtest"]),
        ("DE40", ["GER40", "Germany40", "Germany 40", "DAX"], 2, "index_cfd_alias", ["priority_alias_validation_target", "alias_requires_data_mapping", "do_not_transfer_xauusd_strategy"]),
        ("Germany40", ["GER40", "DE40", "Germany 40", "DAX"], 3, "index_cfd_alias", ["alias_requires_data_mapping", "do_not_transfer_xauusd_strategy"]),
        ("XAUUSD", ["Gold", "XAU/USD"], 4, "metal_cfd", ["baseline_certified_research_track", "high_volatility", "spread_and_news_sensitivity", "certified_candidate_exists_but_not_portfolio_ready"]),
        ("EURUSD", ["EUR/USD", "Euro Dollar"], 5, "forex_major", ["lower_spread_than_gold", "session_specific_liquidity", "requires_independent_certification"]),
        ("GBPUSD", ["GBP/USD", "Cable"], 6, "forex_major_optional", ["higher_news_sensitivity", "requires_independent_certification"]),
        ("USDJPY", ["USD/JPY", "Dollar Yen"], 7, "forex_major_optional", ["asia_session_relevance", "requires_independent_certification"]),
        ("NAS100", ["US100", "NASDAQ100", "Nasdaq 100"], 8, "index_cfd_optional", ["high_volatility_index", "us_session_dependency", "requires_independent_certification"])
    ];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public ScalpingMultiAssetRoadmapService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string RoadmapPath => Path.Combine(Root, "multi_asset_roadmap.json");
    public string RoadmapMarkdownPath => Path.Combine(Root, "multi_asset_roadmap.md");

    public ScalpingMultiAssetRoadmap Update()
    {
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var marketDataAvailability = marketData.Scan();
        var currentMarket = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot);
        var currentMarketStatus = currentMarket.LoadOrCreateStatus();
        var certifications = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReports();
        var config = new CTraderOpenApiConfigLoader().Load(_runtimeRoot).Config;
        var configuredMapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var knownMapper = new CTraderSymbolMapper([]);
        var entries = DefaultAssets.Select(asset => BuildEntry(asset, marketData, marketDataAvailability, currentMarket, currentMarketStatus, configuredMapper, knownMapper, certifications)).ToList();
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
            Mode: "trading_intelligence_research_only",
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

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio");
        try
        {
            Directory.CreateDirectory(preferred);
            var probePath = Path.Combine(preferred, ".write_probe");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return preferred;
        }
        catch (IOException)
        {
            return ResolveFallbackRoot();
        }
        catch (UnauthorizedAccessException)
        {
            return ResolveFallbackRoot();
        }
    }

    private string ResolveFallbackRoot()
    {
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_portfolio");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static ScalpingAssetRoadmapEntry BuildEntry(
        (string Asset, string[] Aliases, int Priority, string MarketType, string[] RiskNotes) asset,
        MarketDataAvailabilityService marketData,
        MarketDataAvailability marketDataAvailability,
        CurrentMarketSnapshotService currentMarket,
        CurrentMarketStatusSnapshot currentMarketStatus,
        CTraderSymbolMapper configuredMapper,
        CTraderSymbolMapper knownMapper,
        IReadOnlyList<ScalpingCertificationReport> certifications)
    {
        var quality = marketData.BuildQuality(asset.Asset, marketDataAvailability);
        var research = new ScalpingResearchService(marketData.StoragePaths, marketData.RuntimeRoot).LoadAssetReport(asset.Asset);
        var expansions = new ScalpingRobustnessExpansionService(marketData.StoragePaths, marketData.RuntimeRoot).LoadReports()
            .Where(report => report.Asset.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase)
                || asset.Aliases.Any(alias => report.Asset.Equals(alias, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var certified = certifications.Count(report => report.Asset.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase)
            || asset.Aliases.Any(alias => report.Asset.Equals(alias, StringComparison.OrdinalIgnoreCase)));
        var dataAvailable = quality.DataGaps.Count == 0;
        var quote = currentMarket.FindSnapshot(asset.Asset)
            ?? asset.Aliases.Select(currentMarket.FindSnapshot).FirstOrDefault(snapshot => snapshot is not null);
        var quoteAvailable = quote?.Status == "available";
        var mappingKnown = knownMapper.TryMap(asset.Asset, out _)
            || asset.Aliases.Any(alias => knownMapper.TryMap(alias, out _));
        var mappingConfigured = configuredMapper.TryMap(asset.Asset, out _)
            || asset.Aliases.Any(alias => configuredMapper.TryMap(alias, out _));
        var quoteMappingStatus = !mappingConfigured
            ? "mapping_missing"
            : quoteAvailable ? "quote_available" : "quote_mapping_pending";
        var historicalDataStatus = dataAvailable ? "historical_data_ready" : "historical_data_missing";
        var gapParts = new List<string>();
        if (!mappingConfigured) gapParts.Add(mappingKnown ? "mapping_not_enabled_in_config" : "mapping_missing");
        if (!quoteAvailable) gapParts.Add(currentMarketStatus.AssetsAvailable.Contains("GER40", StringComparer.OrdinalIgnoreCase) && asset.Aliases.Any(alias => alias.Equals("GER40", StringComparison.OrdinalIgnoreCase))
            ? "alias_quote_only"
            : "quote_unavailable");
        gapParts.AddRange(quality.DataGaps);
        var dataGap = gapParts.Count == 0 ? "-" : string.Join(",", gapParts.Distinct(StringComparer.OrdinalIgnoreCase));
        var finalCandidates = expansions.Count(report => report.Status == ScalpingExpansionStatus.final_candidate);
        var robustCandidates = research?.Candidates.Count(candidate => candidate.ValidationStatus == ScalpingValidationStatus.robust_candidate) ?? 0;
        var totalCandidates = research?.Candidates.Count ?? 0;
        var signalAgentSpecCount = certifications.Count(report =>
            report.Asset.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase)
            || asset.Aliases.Any(alias => report.Asset.Equals(alias, StringComparison.OrdinalIgnoreCase)))
            > 0
            ? certifications.Count(report =>
                (report.Asset.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase)
                 || asset.Aliases.Any(alias => report.Asset.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                && File.Exists(Path.Combine(new ScalpingResearchService(marketData.StoragePaths, marketData.RuntimeRoot).SignalSpecDirectory, report.CandidateId, "signal_agent_spec.json")))
            : 0;
        var signalAgentSpecStatus = certified > 0
            ? signalAgentSpecCount > 0 ? "ready" : "signal_agent_spec_pending"
            : "not_ready";
        var researchStatus = certified > 0
            ? "certified_candidates_available"
            : finalCandidates > 0 ? "certification_pending"
            : robustCandidates > 0 || totalCandidates > 0 ? "candidates_found"
            : research is not null ? "research_started"
            : dataAvailable ? "historical_data_ready"
            : quoteMappingStatus == "quote_mapping_pending" ? "quote_mapping_pending"
            : "historical_data_missing";
        var nextAction = certified > 0
            ? "export_signal_agent_spec"
            : researchStatus == "certification_pending" ? "run_scalping_certification"
            : researchStatus == "candidates_found" ? "run_scalping_robustness_expansion"
            : researchStatus == "research_started" ? "review_candidate_quality_and_continue_search"
            : dataAvailable ? "run_scalping_research"
            : quoteMappingStatus == "quote_mapping_pending" ? "validate_ctrader_symbol_mapping"
            : "import_market_data";
        var riskNotes = asset.RiskNotes
            .Concat(["no_strategy_transfer_without_asset_specific_validation", "requires_backtest_oos_walkforward_robustness_certification_human_review"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var readiness = new ScalpingAssetReadinessService(marketData.StoragePaths, marketData.RuntimeRoot).Evaluate(asset.Asset);
        return new ScalpingAssetRoadmapEntry(
            Asset: asset.Asset,
            Aliases: asset.Aliases,
            Priority: asset.Priority,
            MarketType: asset.MarketType,
            DataAvailable: dataAvailable,
            DataGap: dataGap,
            QuoteMappingStatus: quoteMappingStatus,
            HistoricalDataStatus: historicalDataStatus,
            ResearchStatus: readiness.ResearchStatus,
            SignalAgentSpecStatus: readiness.SignalAgentSpecStatus,
            ReadinessStatus: readiness.AssetStatus,
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

## Strategic Focus
- lane_1: scalping_bot_research_engine
- lane_2: signal_agent_export_engine
- priority_1: integrate_ger40_de40_data
- priority_2: expand_read_only_signal_watch_and_forward_test_tracking
- priority_3: continue_multi_asset_certification_before_any_bot_spec_execution_work

## Asset Roadmap
{string.Join(Environment.NewLine, roadmap.Assets.Select(entry => $"- {entry.Asset}: priority={entry.Priority}, type={entry.MarketType}, data_available={entry.DataAvailable.ToString().ToLowerInvariant()}, certified={entry.CertifiedCandidates}, status={entry.ResearchStatus}, next_action={entry.NextAction}, data_gap={entry.DataGap}"))}

## Rules
- every asset requires its own data availability check
- every asset requires its own backtest, OOS, walkforward, robustness expansion, certification and human review
- no XAUUSD strategy may be transferred to another asset without independent validation
- roadmap is research/reporting only and does not execute trades
""";
}
