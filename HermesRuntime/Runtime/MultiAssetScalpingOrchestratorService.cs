using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MultiAssetScalpingAssetResult(
    string Asset,
    string HistoricalDataStatus,
    string QuoteStatus,
    string ResearchStatus,
    int CandidatesTotal,
    int RobustCandidates,
    int FinalCandidates,
    int CertifiedCandidates,
    int FailedCandidates,
    int SetupCount,
    string BestSetup,
    string SignalAgentSpecStatus,
    string NextAction,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Timeframes,
    bool M1Available,
    bool M5Available,
    bool M15Available);

public sealed record MultiAssetScalpingResearchReport(
    string ReportVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<string> AssetsRequested,
    IReadOnlyList<string> AssetsProcessed,
    IReadOnlyList<string> AssetsSkipped,
    IReadOnlyList<MultiAssetScalpingAssetResult> PerAssetResults,
    IReadOnlyList<string> SafetyFlags,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NextRecommendedActions,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed record MultiAssetResearchAssetStatus(
    string Asset,
    string HistoricalDataStatus,
    string QuoteStatus,
    string ResearchStatus,
    int CandidatesTotal,
    int RobustCandidates,
    int FinalCandidates,
    int CertifiedCandidates,
    int FailedCandidates,
    int SetupCount,
    string BestSetup,
    string SignalAgentSpecStatus,
    string NextAction,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Timeframes,
    bool M1Available,
    bool M5Available,
    bool M15Available);

public sealed record MultiAssetResearchStatusSnapshot(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> AssetsReady,
    IReadOnlyList<string> AssetsSetupReady,
    IReadOnlyList<string> AssetsDataReadyOnly,
    IReadOnlyList<string> AssetsMissingData,
    IReadOnlyList<MultiAssetResearchAssetStatus> PerAssetResults,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NextRecommendedActions,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed class MultiAssetScalpingOrchestratorService
{
    private static readonly string[] ReadyAssets = ["GER40", "XAUUSD", "EURUSD"];
    private static readonly string[] FutureAssets = ["GBPUSD", "USDJPY", "NAS100", "US500"];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public MultiAssetScalpingOrchestratorService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string ReportPath => Path.Combine(Root, "multi_asset_research_report.json");
    public string MarkdownPath => Path.Combine(Root, "multi_asset_research_report.md");

    public MultiAssetScalpingResearchReport Run(string[] assets, int maxVariants)
    {
        var requested = assets
            .Select(NormalizeAsset)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requested.Count == 0)
        {
            requested = ReadyAssets.ToList();
        }

        var startedAt = DateTimeOffset.UtcNow;
        var processed = new List<string>();
        var skipped = new List<string>();
        var warnings = new List<string>();
        var results = new List<MultiAssetScalpingAssetResult>();
        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var robustness = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot);
        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        var readinessService = new ScalpingAssetReadinessService(_storagePaths, _runtimeRoot);
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);

        foreach (var asset in requested)
        {
            var dataAvailable = marketData.HasUsableScalpingData(asset, out var dataGaps, out var candleCount);
            var timeframes = AvailableTimeframes(asset, marketData);
            var readiness = readinessService.Evaluate(asset);
            var historicalStatus = readiness.HistoricalDataStatus;
            var quoteStatus = readiness.QuoteStatus;
            if (!dataAvailable)
            {
                skipped.Add(asset);
                results.Add(new MultiAssetScalpingAssetResult(
                    Asset: asset,
                    HistoricalDataStatus: historicalStatus,
                    QuoteStatus: quoteStatus,
                    ResearchStatus: "data_missing",
                    CandidatesTotal: 0,
                    RobustCandidates: 0,
                    FinalCandidates: 0,
                    CertifiedCandidates: 0,
                    FailedCandidates: 0,
                    SetupCount: 0,
                    BestSetup: "-",
                    SignalAgentSpecStatus: "not_ready",
                    NextAction: "import_or_normalize_market_data",
                    Warnings: [.. dataGaps, "data_missing"],
                    Timeframes: timeframes,
                    M1Available: timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase),
                    M5Available: timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase),
                    M15Available: timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)));
                continue;
            }

            processed.Add(asset);
            var researchReport = research.LoadReport() ?? research.LoadAssetReport(asset);
            var robustReports = robustness.LoadReports().Where(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();
            var finalReports = robustReports.Where(item => item.Status == ScalpingExpansionStatus.final_candidate).ToList();
            var certReports = certification.LoadReports()
                .Where(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase) && item.Status == ScalpingCertificationStatus.certified_candidate)
                .ToList();

            results.Add(new MultiAssetScalpingAssetResult(
                Asset: asset,
                HistoricalDataStatus: historicalStatus,
                QuoteStatus: quoteStatus,
                ResearchStatus: readiness.ResearchStatus,
                CandidatesTotal: readiness.CandidatesTotal,
                RobustCandidates: readiness.RobustCandidates,
                FinalCandidates: readiness.FinalCandidates,
                CertifiedCandidates: readiness.CertifiedCandidates,
                FailedCandidates: researchReport?.RejectedCandidates ?? 0,
                SetupCount: readiness.SetupCount,
                BestSetup: readiness.BestSetup,
                SignalAgentSpecStatus: readiness.SignalAgentSpecStatus,
                NextAction: readiness.AssetStatus switch
                {
                    "bot_ready" => "maintain_signal_agent_exports",
                    "setup_ready" => "maintain_signal_agent_exports",
                    "signal_ready" => "export_signal_agent_spec",
                    "certified_candidates_available" => "export_signal_agent_spec",
                    "final_candidates_available" => "run_scalping_certification",
                    "robust_candidates_available" => "run_scalping_robustness_expansion",
                    "candidates_found" => "run_scalping_robustness_expansion",
                    "research_started" => "run_scalping_research",
                    "data_ready_only" => "run_scalping_research",
                    _ => "import_or_normalize_market_data"
                },
                Warnings: readiness.Warnings.ToList(),
                Timeframes: readiness.Timeframes,
                M1Available: readiness.M1Available,
                M5Available: readiness.M5Available,
                M15Available: readiness.M15Available));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var allAssets = ReadyAssets.Concat(requested).Concat(FutureAssets).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var report = new MultiAssetScalpingResearchReport(
            ReportVersion: "multi_asset_scalping_research_v1",
            StartedAtUtc: startedAt,
            CompletedAtUtc: completedAt,
            AssetsRequested: requested,
            AssetsProcessed: processed,
            AssetsSkipped: skipped,
            PerAssetResults: results.OrderBy(item => item.Asset, StringComparer.OrdinalIgnoreCase).ToList(),
            SafetyFlags: ["no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "research_only=true"],
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NextRecommendedActions: BuildNextActions(results, allAssets),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public MultiAssetScalpingResearchReport? LoadReport()
    {
        return File.Exists(ReportPath)
            ? JsonSerializer.Deserialize<MultiAssetScalpingResearchReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public MultiAssetResearchStatusSnapshot BuildStatus()
    {
        var report = LoadReport();
        var readinessService = new ScalpingAssetReadinessService(_storagePaths, _runtimeRoot);
        var requested = report?.AssetsRequested?.Count > 0 ? report.AssetsRequested.ToList() : ReadyAssets.ToList();
        var assets = ReadyAssets
            .Concat(requested)
            .Concat(FutureAssets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var evaluated = assets.Select(readinessService.Evaluate).ToList();
        var perAssetResults = evaluated
            .Select(item => new MultiAssetResearchAssetStatus(
                Asset: item.Asset,
                HistoricalDataStatus: item.HistoricalDataStatus,
                QuoteStatus: item.QuoteStatus,
                ResearchStatus: item.ResearchStatus,
                CandidatesTotal: item.CandidatesTotal,
                RobustCandidates: item.RobustCandidates,
                FinalCandidates: item.FinalCandidates,
                CertifiedCandidates: item.CertifiedCandidates,
                FailedCandidates: item.CandidatesTotal >= item.CertifiedCandidates ? item.CandidatesTotal - item.CertifiedCandidates : 0,
                SetupCount: item.SetupCount,
                BestSetup: item.BestSetup,
                SignalAgentSpecStatus: item.SignalAgentSpecStatus,
                NextAction: item.AssetStatus switch
                {
                    "bot_ready" => "maintain_signal_agent_exports",
                    "setup_ready" => "maintain_signal_agent_exports",
                    "signal_ready" => "export_signal_agent_spec",
                    "certified_candidates_available" => "export_signal_agent_spec",
                    "final_candidates_available" => "run_scalping_certification",
                    "robust_candidates_available" => "run_scalping_robustness_expansion",
                    "candidates_found" => "run_scalping_robustness_expansion",
                    "research_started" => "run_scalping_research",
                    "data_ready_only" => "run_scalping_research",
                    _ => "import_or_normalize_market_data"
                },
                Warnings: item.Warnings,
                Timeframes: item.Timeframes,
                M1Available: item.M1Available,
                M5Available: item.M5Available,
                M15Available: item.M15Available))
            .ToList();

        var assetsReady = evaluated
            .Where(item => item.AssetStatus is "signal_ready" or "setup_ready" or "bot_ready")
            .Select(item => item.Asset)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsSetupReady = evaluated
            .Where(item => item.AssetStatus is "setup_ready" or "bot_ready")
            .Select(item => item.Asset)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsDataReadyOnly = evaluated
            .Where(item => item.AssetStatus == "data_ready_only")
            .Select(item => item.Asset)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsMissingData = evaluated
            .Where(item => item.AssetStatus == "missing_data")
            .Select(item => item.Asset)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MultiAssetResearchStatusSnapshot(
            ReportVersion: "multi_asset_scalping_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            AssetsReady: assetsReady,
            AssetsSetupReady: assetsSetupReady,
            AssetsDataReadyOnly: assetsDataReadyOnly,
            AssetsMissingData: assetsMissingData,
            PerAssetResults: perAssetResults.OrderBy(item => item.Asset, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: perAssetResults.SelectMany(item => item.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NextRecommendedActions: perAssetResults.Select(item => $"{item.Asset}:{item.NextAction}").Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);
    }

    private static string NormalizeAsset(string asset)
        => asset.Trim().ToUpperInvariant();

    private static IReadOnlyList<string> AvailableTimeframes(string asset, MarketDataAvailabilityService marketData)
    {
        var quality = marketData.BuildQuality(asset);
        return quality.TimeframesAvailable
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string QuoteStatus(string asset, ScalpingMultiAssetRoadmapService roadmapService)
        => roadmapService.FindAsset(asset)?.QuoteMappingStatus ?? "quote_mapping_pending";

    private static string SanitizeWarning(string message)
        => string.IsNullOrWhiteSpace(message)
            ? "unknown_error"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();

    private static IReadOnlyList<string> BuildNextActions(IReadOnlyList<MultiAssetScalpingAssetResult> results, IReadOnlyList<string> allAssets)
    {
        var actions = new List<string>();
        foreach (var result in results.OrderBy(item => item.Asset, StringComparer.OrdinalIgnoreCase))
        {
            if (result.NextAction != "maintain_signal_agent_exports")
            {
                actions.Add($"{result.Asset}:{result.NextAction}");
            }
        }

        foreach (var missing in allAssets.Where(asset => results.All(item => !item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase))))
        {
            actions.Add($"{missing}:prepare_data_or_validate_mapping");
        }

        return actions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "scalping_multi_asset_research");
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
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_multi_asset_research");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string BuildMarkdown(MultiAssetScalpingResearchReport report)
        => $"""
# Multi Asset Scalping Research

- started_at_utc: {report.StartedAtUtc:O}
- completed_at_utc: {(report.CompletedAtUtc?.ToString("O") ?? "-")}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false
- research_only: true

## Per Asset
{string.Join(Environment.NewLine + Environment.NewLine, report.PerAssetResults.Select(item => $"""
### {item.Asset}
- historical_data_status: {item.HistoricalDataStatus}
- quote_status: {item.QuoteStatus}
- research_status: {item.ResearchStatus}
- candidates_total: {item.CandidatesTotal}
- robust_candidates: {item.RobustCandidates}
- final_candidates: {item.FinalCandidates}
- certified_candidates: {item.CertifiedCandidates}
- failed_candidates: {item.FailedCandidates}
- setup_count: {item.SetupCount}
- best_setup: {item.BestSetup}
- signal_agent_spec_status: {item.SignalAgentSpecStatus}
- next_action: {item.NextAction}
- warnings: {string.Join(", ", item.Warnings)}
"""))}
""";
}
