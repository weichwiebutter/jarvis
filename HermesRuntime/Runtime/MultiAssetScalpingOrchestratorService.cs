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
        var inventoryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var roadmapService = new ScalpingMultiAssetRoadmapService(_storagePaths, _runtimeRoot);
        var signalSpecDirectory = research.SignalSpecDirectory;

        foreach (var asset in requested)
        {
            var dataAvailable = marketData.HasUsableScalpingData(asset, out var dataGaps, out var candleCount);
            var timeframes = AvailableTimeframes(asset, marketData);
            var historicalStatus = dataAvailable ? "historical_data_ready" : "data_missing";
            var quoteStatus = QuoteStatus(asset, roadmapService);
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
            var researchReport = research.RunResearch(asset, maxVariants);
            var robustReports = robustness.ExpandAllRobust();
            var assetRobust = robustReports.Where(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();
            var finalReports = robustness.LoadReports().Where(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase) && item.Status == ScalpingExpansionStatus.final_candidate).ToList();
            var certReports = new List<ScalpingCertificationReport>();
            foreach (var candidateId in finalReports.Select(item => item.CandidateId).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    certReports.Add(certification.LoadReport(candidateId) ?? certification.Certify(candidateId));
                }
                catch (Exception ex)
                {
                    warnings.Add($"{asset}:{candidateId}:certification_failed:{SanitizeWarning(ex.Message)}");
                }
            }

            foreach (var cert in certReports)
            {
                try
                {
                    research.ExportSignalAgentSpec(cert.CandidateId);
                }
                catch
                {
                    warnings.Add($"{asset}:{cert.CandidateId}:signal_spec_export_failed");
                }
            }

            var registry = inventoryService.BuildRegistry();
            var setupCount = registry.SetupCountsByAsset.GetValueOrDefault(asset, 0);
            var bestSetup = registry.BestSetupByAsset.GetValueOrDefault(asset, "-");
            var signalSpecStatus = certReports.Count > 0
                ? certReports.Count(cert => File.Exists(Path.Combine(signalSpecDirectory, cert.CandidateId, "signal_agent_spec.json"))) > 0
                    ? "ready"
                    : "signal_agent_spec_pending"
                : "not_ready";
            var researchStatus = certReports.Count > 0
                ? "certified_candidates_available"
                : finalReports.Count > 0
                    ? "certification_pending"
                    : assetRobust.Count > 0 || researchReport.Candidates.Count > 0
                        ? "candidates_found"
                        : "research_started";
            var nextAction = signalSpecStatus == "ready"
                ? "maintain_signal_agent_exports"
                : researchStatus == "certified_candidates_available"
                    ? "export_signal_agent_spec"
                    : researchStatus == "certification_pending"
                        ? "run_scalping_certification"
                        : researchStatus == "candidates_found"
                            ? "run_scalping_robustness_expansion"
                            : "run_scalping_research";
            var assetWarnings = new List<string>(dataGaps);
            if (!timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase)) assetWarnings.Add("m1_data_missing");
            if (!timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase)) assetWarnings.Add("m5_data_missing");
            if (!timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)) assetWarnings.Add("m15_data_missing");
            if (researchReport.Candidates.Any(candidate => candidate.Backtest.TradeCount < 100)) assetWarnings.Add("low_trade_count");
            if (researchReport.Candidates.Any(candidate => (candidate.Backtest.SignalDensityPerMonth ?? 0) < 4)) assetWarnings.Add("low_signal_frequency");
            if (researchReport.Candidates.Any(candidate => candidate.Backtest.AverageHoldingDurationMinutes is > 360)) assetWarnings.Add("overnight_risk");

            results.Add(new MultiAssetScalpingAssetResult(
                Asset: asset,
                HistoricalDataStatus: historicalStatus,
                QuoteStatus: quoteStatus,
                ResearchStatus: researchStatus,
                CandidatesTotal: researchReport.CandidatesTotal,
                RobustCandidates: researchReport.RobustCandidates,
                FinalCandidates: finalReports.Count,
                CertifiedCandidates: certReports.Count,
                FailedCandidates: researchReport.RejectedCandidates,
                SetupCount: setupCount,
                BestSetup: bestSetup,
                SignalAgentSpecStatus: signalSpecStatus,
                NextAction: nextAction,
                Warnings: assetWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Timeframes: timeframes,
                M1Available: timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase),
                M5Available: timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase),
                M15Available: timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var allAssets = requested.Concat(FutureAssets).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var roadmapService = new ScalpingMultiAssetRoadmapService(_storagePaths, _runtimeRoot);
        var inventoryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var registry = inventoryService.LoadRegistry() ?? inventoryService.BuildRegistry();
        var requested = report?.AssetsRequested?.Count > 0 ? report.AssetsRequested.ToList() : ReadyAssets.ToList();
        var assets = ReadyAssets
            .Concat(requested)
            .Concat(FutureAssets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var perAssetResults = new List<MultiAssetResearchAssetStatus>();

        foreach (var asset in assets)
        {
            var quality = marketData.BuildQuality(asset);
            var timeframes = quality.TimeframesAvailable;
            var historicalStatus = quality.CandleCount > 0 ? "historical_data_ready" : "data_missing";
            var quoteStatus = QuoteStatus(asset, roadmapService);
            var certCount = registry.SetupCountsByAsset.TryGetValue(asset, out var setupCount) ? setupCount : 0;
            var bestSetup = registry.BestSetupByAsset.TryGetValue(asset, out var best) ? best : "-";
            var matchingReport = report?.PerAssetResults.FirstOrDefault(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase));
            var researchStatus = matchingReport?.ResearchStatus
                ?? (certCount > 0 ? "certified_candidates_available"
                    : quality.CandleCount > 0 ? "historical_data_ready"
                    : "data_missing");
            var signalStatus = matchingReport?.SignalAgentSpecStatus
                ?? (certCount > 0 ? "ready" : "not_ready");
            var setupReady = certCount > 0 && signalStatus == "ready";
            var nextAction = matchingReport?.NextAction
                ?? (quality.CandleCount == 0 ? "import_or_normalize_market_data"
                    : certCount > 0 ? "maintain_signal_agent_exports"
                    : "run_scalping_research");
            var warnings = new List<string>();
            if (quality.CandleCount == 0) warnings.Add("data_missing");
            if (!timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase)) warnings.Add("m1_data_missing");
            if (!timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase)) warnings.Add("m5_data_missing");
            if (!timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)) warnings.Add("m15_data_missing");
            if (matchingReport is null && quality.CandleCount > 0 && certCount == 0) warnings.Add("research_not_started");

            perAssetResults.Add(new MultiAssetResearchAssetStatus(
                Asset: asset,
                HistoricalDataStatus: historicalStatus,
                QuoteStatus: quoteStatus,
                ResearchStatus: researchStatus,
                CandidatesTotal: matchingReport?.CandidatesTotal ?? 0,
                RobustCandidates: matchingReport?.RobustCandidates ?? 0,
                FinalCandidates: matchingReport?.FinalCandidates ?? 0,
                CertifiedCandidates: matchingReport?.CertifiedCandidates ?? 0,
                FailedCandidates: matchingReport?.FailedCandidates ?? 0,
                SetupCount: setupCount,
                BestSetup: bestSetup,
                SignalAgentSpecStatus: signalStatus,
                NextAction: nextAction,
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Timeframes: timeframes,
                M1Available: timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase),
                M5Available: timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase),
                M15Available: timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)));
        }

        var assetsReady = perAssetResults
            .Where(item => item.HistoricalDataStatus == "historical_data_ready" && item.CertifiedCandidates > 0)
            .Select(item => item.Asset)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsSetupReady = perAssetResults
            .Where(item => item.SetupCount > 0 && item.SignalAgentSpecStatus == "ready")
            .Select(item => item.Asset)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsDataReadyOnly = perAssetResults
            .Where(item => item.HistoricalDataStatus == "historical_data_ready" && item.CertifiedCandidates == 0)
            .Select(item => item.Asset)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assetsMissingData = perAssetResults
            .Where(item => item.HistoricalDataStatus == "data_missing")
            .Select(item => item.Asset)
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
