using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScalpingAssetReadinessSnapshot(
    string Asset,
    string HistoricalDataStatus,
    string QuoteStatus,
    string AssetStatus,
    string SignalReadyStatus,
    string SetupReadyStatus,
    string BotReadyStatus,
    string ResearchStatus,
    string SignalAgentSpecStatus,
    int CandidatesTotal,
    int RobustCandidates,
    int FinalCandidates,
    int CertifiedCandidates,
    int SetupCount,
    string BestSetup,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Timeframes,
    bool M1Available,
    bool M5Available,
    bool M15Available,
    bool DataAvailable,
    bool HasSignalSpecs,
    bool HasSetupRegistry,
    bool HasBotReadySetup);

public sealed class ScalpingAssetReadinessService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingAssetReadinessService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public ScalpingAssetReadinessSnapshot Evaluate(string asset)
    {
        var normalized = NormalizeAsset(asset);
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var quality = marketData.BuildQuality(normalized);
        var timeframes = quality.TimeframesAvailable;
        var dataAvailable = quality.CandleCount > 0 && quality.DataGaps.Count == 0;

        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var researchReport = research.LoadAssetReport(normalized);
        var robustness = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot);
        var robustReports = robustness.LoadReports().Where(item => item.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        var finalReports = robustReports.Where(item => item.Status == ScalpingExpansionStatus.final_candidate).ToList();
        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        var certReports = certification.LoadReports().Where(item => item.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase) && item.Status == ScalpingCertificationStatus.certified_candidate).ToList();
        var inventoryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var registry = inventoryService.LoadRegistry();
        var setupCount = registry?.SetupCountsByAsset.TryGetValue(normalized, out var count) == true ? count : 0;
        var bestSetup = registry?.BestSetupByAsset.TryGetValue(normalized, out var best) == true ? best : "-";
        var signalSpecDirectories = SignalSpecDirectories();
        var hasSignalSpecs = certReports.Count > 0 && certReports.Any(item => signalSpecDirectories.Any(directory => File.Exists(Path.Combine(directory, item.CandidateId, "signal_agent_spec.json"))));
        var signalSpecStatus = certReports.Count == 0
            ? "not_ready"
            : hasSignalSpecs ? "ready" : "signal_agent_spec_pending";

        var setupReadinessStatuses = registry?.Assets
            .Where(entry => entry.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.ReadinessStatus)
            .ToList() ?? [];
        var hasBotReadySetup = setupReadinessStatuses.Any(status => status.Equals("bot_ready", StringComparison.OrdinalIgnoreCase));
        var hasSetupReady = setupReadinessStatuses.Any(status => status.Equals("setup_ready", StringComparison.OrdinalIgnoreCase) || status.Equals("bot_ready", StringComparison.OrdinalIgnoreCase));

        var warnings = new List<string>();
        if (registry is null) warnings.Add("setup_registry_missing");
        if (!dataAvailable) warnings.Add("data_missing");
        if (!timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase)) warnings.Add("m1_data_missing");
        if (!timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase)) warnings.Add("m5_data_missing");
        if (!timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase)) warnings.Add("m15_data_missing");
        if (setupReadinessStatuses.Any(status => status.Contains("low_trade_count", StringComparison.OrdinalIgnoreCase))) warnings.Add("low_trade_count");
        if (setupReadinessStatuses.Any(status => status.Contains("low_signal_frequency", StringComparison.OrdinalIgnoreCase))) warnings.Add("low_signal_frequency");
        if (researchReport?.Candidates.Any(candidate => candidate.Backtest.AverageHoldingDurationMinutes is > 360) == true) warnings.Add("overnight_risk");

        var candidateCount = researchReport?.Candidates.Count ?? 0;
        var robustCount = researchReport?.Candidates.Count(candidate => candidate.ValidationStatus == ScalpingValidationStatus.robust_candidate) ?? 0;
        var finalCount = finalReports.Count;
        var certifiedCount = certReports.Count;
        var historicalDataStatus = dataAvailable ? "historical_data_ready" : "data_missing";
        var currentMarket = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).LoadStatus();
        var quoteStatus = currentMarket?.AssetsAvailable.Contains(normalized, StringComparer.OrdinalIgnoreCase) == true
            ? "quote_available"
            : "quote_mapping_pending";
        var researchStatus = !dataAvailable
            ? "missing_data"
            : candidateCount == 0
                ? "data_ready_only"
                : robustCount == 0
                    ? "research_started"
                    : finalCount == 0
                        ? "candidates_found"
                        : certifiedCount == 0
                            ? "final_candidates_available"
                            : hasSignalSpecs
                                ? "signal_ready"
                                : "certified_candidates_available";

        var assetStatus =
            !dataAvailable ? "missing_data" :
            hasBotReadySetup ? "bot_ready" :
            hasSetupReady ? "setup_ready" :
            hasSignalSpecs ? "signal_ready" :
            certifiedCount > 0 ? "certified_candidates_available" :
            finalCount > 0 ? "final_candidates_available" :
            robustCount > 0 ? "robust_candidates_available" :
            candidateCount > 0 ? "candidates_found" : "data_ready_only";

        return new ScalpingAssetReadinessSnapshot(
            Asset: normalized,
            HistoricalDataStatus: historicalDataStatus,
            QuoteStatus: quoteStatus,
            AssetStatus: assetStatus,
            SignalReadyStatus: hasSignalSpecs ? "signal_ready" : "needs_more_validation",
            SetupReadyStatus: hasSetupReady ? "setup_ready" : "needs_more_validation",
            BotReadyStatus: hasBotReadySetup ? "bot_ready" : "needs_more_validation",
            ResearchStatus: researchStatus,
            SignalAgentSpecStatus: signalSpecStatus,
            CandidatesTotal: candidateCount,
            RobustCandidates: robustCount,
            FinalCandidates: finalCount,
            CertifiedCandidates: certifiedCount,
            SetupCount: setupCount,
            BestSetup: bestSetup,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Timeframes: timeframes,
            M1Available: timeframes.Contains("M1", StringComparer.OrdinalIgnoreCase),
            M5Available: timeframes.Contains("M5", StringComparer.OrdinalIgnoreCase),
            M15Available: timeframes.Contains("M15", StringComparer.OrdinalIgnoreCase),
            DataAvailable: dataAvailable,
            HasSignalSpecs: hasSignalSpecs,
            HasSetupRegistry: setupCount > 0,
            HasBotReadySetup: hasBotReadySetup);
    }

    public IReadOnlyList<string> BuildAssetsReady(IEnumerable<string> assets)
        => assets.Select(Evaluate).Where(item => item.AssetStatus is "signal_ready" or "setup_ready" or "bot_ready").Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> BuildAssetsSetupReady(IEnumerable<string> assets)
        => assets.Select(Evaluate).Where(item => item.AssetStatus is "setup_ready" or "bot_ready").Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> BuildAssetsDataReadyOnly(IEnumerable<string> assets)
        => assets.Select(Evaluate).Where(item => item.AssetStatus == "data_ready_only").Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> BuildAssetsMissingData(IEnumerable<string> assets)
        => assets.Select(Evaluate).Where(item => item.AssetStatus == "missing_data").Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

    private static string NormalizeAsset(string asset)
        => asset.Trim().Equals("GOLD", StringComparison.OrdinalIgnoreCase) ? "XAUUSD" : asset.Trim().ToUpperInvariant();

    private IEnumerable<string> SignalSpecDirectories()
    {
        yield return Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs");
        yield return Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_agent_specs");
    }
}
