using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RuntimeHealthSummaryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string MainStatus,
    string LastStep,
    string NextStep,
    string LastResult,
    bool FrankRequired,
    int OpenReviews,
    int OpenOosPlans,
    int OpenForwardPlans,
    string LastWarning,
    string SafetyStatus,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath);

public sealed class RuntimeHealthSummaryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public RuntimeHealthSummaryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "runtime_health_summary");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "runtime_health_summary.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "runtime_health_summary.md");

    public RuntimeHealthSummaryReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RuntimeHealthSummaryReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public RuntimeHealthSummaryReport Run()
    {
        Directory.CreateDirectory(Root);

        var masterStatus = LoadMasterStatusSnapshot() ?? BuildFallbackMasterStatus();
        var loopReport = new AutonomousResearchLoopOrchestratorService(_storagePaths, _runtimeRoot).Load();
        var actionPlan = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_action_plan", "review_action_plan.json"));
        var domainAware = LoadJson(Path.Combine(_storagePaths.Root, "reports", "domain_aware_review_prioritization", "domain_aware_review_prioritization.json"));
        var oosPlanning = new AutonomousOosPlanningService(_storagePaths).Load();
        var forwardSync = new AutonomousForwardObservationCompletionSyncService(_storagePaths, _runtimeRoot).Load();
        var warnings = new List<string>();

        var mainStatus = DetermineMainStatus(masterStatus, loopReport, actionPlan, domainAware);
        var frankRequired = mainStatus == "frank_noetig";
        var lastStep = loopReport?.LastAutonomousAction ?? loopReport?.StepType ?? "-";
        var nextStep = loopReport?.NextScheduledStep ?? loopReport?.NextPlannedStep ?? "Research-Loop warten";
        var lastResult = loopReport?.StepResult ?? "-";
        var openReviews = masterStatus.PendingReviews;
        var openOosPlans = oosPlanning?.Plans.Count(plan => !plan.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var openForwardPlans = forwardSync?.OpenPlans ?? 0;
        var lastWarning = TranslateLastWarning(masterStatus, loopReport, warnings);
        var safetyStatus = BuildSafetyStatus(masterStatus, loopReport);
        var operatorSummary = BuildOperatorSummary(mainStatus, lastStep, nextStep, lastResult, frankRequired, openReviews);

        var report = new RuntimeHealthSummaryReport(
            ReportVersion: "runtime_health_summary_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MainStatus: mainStatus,
            LastStep: lastStep,
            NextStep: nextStep,
            LastResult: lastResult,
            FrankRequired: frankRequired,
            OpenReviews: openReviews,
            OpenOosPlans: openOosPlans,
            OpenForwardPlans: openForwardPlans,
            LastWarning: lastWarning,
            SafetyStatus: safetyStatus,
            SourceReports: BuildSourceReports(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: operatorSummary,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        AppendHistory(report);
        return report;
    }

    private static string DetermineMainStatus(MasterStatusSnapshot masterStatus, AutonomousResearchLoopOrchestratorReport? loopReport, JsonElement? actionPlan, JsonElement? domainAware)
    {
        var hasRuntimeError = masterStatus.OverallStatus.Equals("critical", StringComparison.OrdinalIgnoreCase)
            || !masterStatus.SupervisorRunning
            || masterStatus.SafetyFlags.BrokerOrdersEnabled
            || masterStatus.SafetyFlags.LiveTradingEnabled;
        if (hasRuntimeError)
        {
            return "fehler";
        }

        var frankDecisionRequired = ReadInt(actionPlan, "frank_decision_required", "FrankDecisionRequired") > 0
            || ReadBool(domainAware, "frank_red_required", "FrankRedRequired");
        var unresolvedHighPriority = masterStatus.PendingReviews > 0
            && masterStatus.TopReviewPriorities.Any(item => item.StartsWith("high:", StringComparison.OrdinalIgnoreCase))
            && ReadInt(actionPlan, "hermes_can_continue", "HermesCanContinue") <= 0;
        if (frankDecisionRequired || unresolvedHighPriority)
        {
            return "frank_noetig";
        }

        if (masterStatus.PendingReviews > 0)
        {
            var loopStatus = loopReport?.Status ?? string.Empty;
            var waitingStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "waiting_for_window",
                "waiting_for_allowed_time_window",
                "no_signal",
                "waiting_for_signal",
                "waiting_for_market_data",
                "waiting_for_allowed_window",
                "idle_no_safe_action"
            };

            return waitingStatuses.Contains(loopStatus) || waitingStatuses.Contains(loopReport?.StepStatus ?? string.Empty)
                ? "wartet"
                : "arbeitet";
        }

        var waitingStatusesFallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "waiting_for_window",
            "waiting_for_allowed_time_window",
            "no_signal",
            "waiting_for_signal",
            "waiting_for_market_data",
            "waiting_for_allowed_window",
            "idle_no_safe_action"
        };

        if (waitingStatusesFallback.Contains(loopReport?.Status ?? string.Empty)
            || waitingStatusesFallback.Contains(loopReport?.StepStatus ?? string.Empty))
        {
            return "wartet";
        }

        return "arbeitet";
    }

    private static string TranslateLastWarning(MasterStatusSnapshot masterStatus, AutonomousResearchLoopOrchestratorReport? loopReport, List<string> warnings)
    {
        var rawWarning = masterStatus.Warnings.FirstOrDefault() ?? loopReport?.Warnings.FirstOrDefault() ?? "-";
        if (rawWarning == "-")
        {
            return "-";
        }

        var translated = rawWarning switch
        {
            var item when item.StartsWith("high:validation_gap:hypotheses_without_validation_queue", StringComparison.OrdinalIgnoreCase) =>
                "Hermes muss interne Validierungen nachziehen. Frank muss aktuell nichts tun.",
            var item when item.StartsWith("cleanup_candidates:", StringComparison.OrdinalIgnoreCase) =>
                "Speicher aufräumen wäre sinnvoll, ist aber nicht trading-kritisch.",
            var item when item.StartsWith("pending_reviews:", StringComparison.OrdinalIgnoreCase) =>
                "Offene Reviews warten auf Prüfung.",
            var item when item.StartsWith("outside_allowed_window", StringComparison.OrdinalIgnoreCase) =>
                "Hermes wartet auf ein erlaubtes Arbeits- oder Lernfenster.",
            var item when item.StartsWith("no_signal", StringComparison.OrdinalIgnoreCase) =>
                "Noch kein passendes Signal gesehen. Hermes beobachtet später weiter.",
            _ => rawWarning
        };

        if (!string.Equals(translated, rawWarning, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(rawWarning);
        }

        return translated;
    }

    private static string BuildSafetyStatus(MasterStatusSnapshot masterStatus, AutonomousResearchLoopOrchestratorReport? loopReport)
        => $"no_auto_trading={masterStatus.NoAutoTrading.ToString().ToLowerInvariant()}, human_review_required={masterStatus.HumanReviewRequired.ToString().ToLowerInvariant()}, broker_orders_enabled={masterStatus.BrokerOrdersEnabled.ToString().ToLowerInvariant()}, live_trading_enabled={masterStatus.LiveTradingEnabled.ToString().ToLowerInvariant()}, research_only=true, in_work_window={(loopReport?.InWorkWindow ?? false).ToString().ToLowerInvariant()}, in_learning_window={(loopReport?.InLearningWindow ?? false).ToString().ToLowerInvariant()}";

    private static string BuildOperatorSummary(string mainStatus, string lastStep, string nextStep, string lastResult, bool frankRequired, int openReviews)
        => openReviews > 0 && !frankRequired
            ? $"{openReviews} Reviews offen, aber Hermes kann die Top-Trading-Reviews autonom weiterbearbeiten. Frank muss aktuell nicht entscheiden."
            : $"Hauptstatus={mainStatus}. Letzter Schritt={lastStep}. Nächster Schritt={nextStep}. Ergebnis={lastResult}. Frank nötig={(frankRequired ? "ja" : "nein")}.";

    private static IReadOnlyList<string> BuildSourceReports() =>
    [
        "/mnt/d/HermesData/reports/master-status/master_status.json",
        "/mnt/d/HermesData/reports/autonomous_research_loop/autonomous_research_loop.json",
        "/mnt/d/HermesData/reports/autonomous_oos_planning/autonomous_oos_planning.json",
        "/mnt/d/HermesData/reports/autonomous_forward_observation_sync/autonomous_forward_observation_sync.json"
    ];

    private void WriteArtifacts(RuntimeHealthSummaryReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private void AppendHistory(RuntimeHealthSummaryReport report)
    {
        var historyRoot = Path.Combine(_storagePaths.Root, "reports", "runtime_health_history");
        Directory.CreateDirectory(historyRoot);
        var path = Path.Combine(historyRoot, "runtime_health_history.jsonl");
        var entry = new RuntimeHealthHistoryEntry(
            TimestampUtc: report.UpdatedAtUtc,
            MainStatus: report.MainStatus,
            LastStep: report.LastStep,
            NextStep: report.NextStep,
            LastResult: report.LastResult,
            FrankRequired: report.FrankRequired,
            OpenReviews: report.OpenReviews,
            OpenOosPlans: report.OpenOosPlans,
            OpenForwardPlans: report.OpenForwardPlans,
            SafetyStatus: report.SafetyStatus);
        File.AppendAllText(path, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private MasterStatusSnapshot? LoadMasterStatusSnapshot()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static JsonElement? LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static int ReadInt(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    return number;
                }
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static bool ReadBool(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var value))
            {
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetBoolean();
                }
                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return false;
    }

    private static MasterStatusSnapshot BuildFallbackMasterStatus()
        => new(
            SnapshotVersion: "master_status_snapshot_v1",
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            DataRoot: "-",
            OverallStatus: "warning",
            CurrentFocus: "runtime health summary fallback",
            ActiveDomains: [],
            CognitiveStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            ResearchQueueStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            AutonomousLoopStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            NightlyStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            SchedulerStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            SupervisorStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            ResourceStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            StorageStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            TradingDomainStatus: new MasterStatusSection("unknown", null, new Dictionary<string, object?>(), []),
            SafetyFlags: new MasterStatusSafetyFlags(true, true, false, false, true, true),
            Warnings: [],
            SectionTimingsMs: new Dictionary<string, long>(),
            SlowSections: [],
            TopBlockers: [],
            NextRecommendedActions: [],
            QueuedTasks: 0,
            LastNightlyRun: null,
            LastAutonomousLoop: null,
            LastMetaReview: null,
            LearningStrategy: "unknown",
            SupervisorRunning: true,
            SchedulerEnabled: 0,
            ResourceAction: "unknown",
            StorageCleanup: 0,
            AutoCleanupPolicyEnabled: false,
            AutoCleanupAllowed: false,
            AutoCleanupLastRun: null,
            AutoCleanupLastResult: "unknown",
            CleanupCandidates: 0,
            EstimatedFreeBytes: 0,
            ProtectedPathsCount: 0,
            SafetyMode: "monitor_only",
            RobustStrategies: 0,
            DemoBotCandidates: 0,
            TrustedKnowledge: 0,
            WeakKnowledge: 0,
            DeprecatedKnowledge: 0,
            AverageQualityScore: 0,
            AverageTrustScore: 0,
            KnowledgeHealth: "unknown",
            KnowledgeTrend: "stable",
            EvidenceCoverage: 0,
            ContradictionCount: 0,
            HumanReviewedItems: 0,
            ValidationCoverage: 0,
            TrustDistribution: new Dictionary<string, int>(),
            PendingReviews: 0,
            ApprovedReviews: 0,
            RejectedReviews: 0,
            NeedsMoreEvidenceReviews: 0,
            DeferredReviews: 0,
            ReviewCoverage: 0,
            TopReviewPriorities: [],
            ValidationPlansOpen: 0,
            ValidationTasksPending: 0,
            TrustedCandidateCount: 0,
            KnowledgeItemsNeedingOos: 0,
            KnowledgeItemsNeedingSourceCheck: 0,
            InvalidValidationTasks: 0,
            ValidationTasksCleaned: 0,
            ValidationRoutingHealth: "unknown",
            DomainValidationHealth: "unknown",
            DocumentationValidationPending: 0,
            SoftwareValidationPending: 0,
            ProcessValidationPending: 0,
            ResearchValidationPending: 0,
            ScalpingAsset: "-",
            ScalpingCandidatesTotal: 0,
            ScalpingRobustCandidates: 0,
            ScalpingRejectedCandidates: 0,
            ScalpingNeedsMoreData: 0,
            BestScalpingCandidate: null,
            SignalAgentSpecsReady: 0,
            CTraderBotSpecsReady: 0,
            LatestSignalAgentSpec: null,
            SignalAgentExportHealth: "unknown",
            CertifiedCandidateSignalReady: false,
            LatestCTraderBotSpec: null,
            CTraderBotExportHealth: "unknown",
            CertifiedCandidateBotReady: false,
            CandidatePortfolioMode: "planned",
            ScalpingPortfolioStatus: "unknown",
            ScalpingPortfolioMembers: 0,
            ScalpingEnsembleCandidates: 0,
            ScalpingSignalDensityScore: 0,
            ScalpingPortfolioDiversityScore: 0,
            ScalpingNextCandidateSearchAction: "unknown",
            ScalpingMultiAssetMode: "research_only",
            ScalpingNextAssets: [],
            ScalpingAssetsWithData: [],
            ScalpingAssetsNeedingData: [],
            ScalpingMultiAssetRoadmapHealth: "unknown",
            MultiAssetResearchStatus: "unknown",
            MultiAssetAssetsReady: [],
            MultiAssetAssetsSetupReady: [],
            MultiAssetAssetsDataReadyOnly: [],
            MultiAssetAssetsMissingData: [],
            DataReadyAssets: [],
            SignalReadyAssets: [],
            SetupReadyAssets: [],
            BotReadyAssets: [],
            AssetsNeedingValidation: [],
            AssetsMissingData: [],
            Ger40QuoteMappingStatus: "unknown",
            Ger40HistoricalDataStatus: "unknown",
            Ger40ResearchStatus: "unknown",
            Ger40SignalAgentSpecStatus: "unknown",
            CertifiedCandidateInventoryStatus: "unknown",
            SetupRegistryStatus: "unknown",
            SetupRegistryAssets: [],
            XauusdSetupCount: 0,
            EurusdSetupCount: 0,
            Ger40SetupCount: 0,
            BestXauusdSetup: null,
            BestEurusdSetup: null,
            BestGer40Setup: null,
            TotalSetupReadyAssets: 0,
            TotalSignalSpecsReady: 0,
            EurusdCertifiedCandidates: 0,
            EnsembleCandidateStatus: "unknown",
            EnsembleCandidateMembers: 0,
            EnsembleCandidateHealth: "unknown",
            ScalpingEnsembleOptimizerHealth: "unknown",
            ScalpingOptimizedEnsembleStatus: "unknown",
            ScalpingOptimizedEnsembleMembers: 0,
            ScalpingOptimizedEnsembleMode: "balanced",
            ScalpingOptimizedEnsembleDrawdown: 0,
            ScalpingOptimizedEnsembleSignalDensity: 0,
            ScalpingOptimizedEnsembleReadiness: "unknown",
            ScalpingEnsemblePackageReady: false,
            LatestScalpingEnsemblePackage: null,
            ScalpingEnsembleExportHealth: "unknown",
            ScalpingEnsembleHumanReviewReady: false,
            ScalpingEnsembleReviewStatus: "unknown",
            ScalpingEnsembleApprovedForDemoSignalUse: false,
            ScalpingEnsembleApprovedForForwardTestPreparation: false,
            ScalpingEnsembleReviewHealth: "unknown",
            LatestScalpingEnsembleReview: null,
            DemoSignalFeedStatus: "unknown",
            DemoSignalsAvailable: false,
            LatestDemoSignalCount: 0,
            DemoSignalFeedHealth: "unknown",
            DemoSignalFeedMode: "unknown",
            ForwardTestStatus: "unknown",
            ForwardTestMode: "unknown",
            ForwardTestAssets: [],
            ForwardTestSignalsObserved: 0,
            ForwardTestObservationsTotal: 0,
            ForwardTestTriggeredCount: 0,
            ForwardTestInvalidatedCount: 0,
            ForwardTestSimulatedObservationCount: 0,
            ForwardTestLatestObservationUtc: null,
            ForwardTestUsingCurrentMarketSnapshot: false,
            ForwardTestHealth: "unknown",
            ForwardTestRequiresHumanReview: true,
            CurrentMarketSnapshotStatus: "unknown",
            CurrentMarketAssetsAvailable: [],
            CurrentMarketSnapshotHealth: "unknown",
            CurrentMarketLatestUpdateUtc: null,
            MarketDataAssetsAvailable: [],
            MarketDataGer40Available: false,
            MarketDataXauusdAvailable: false,
            MarketDataEurusdAvailable: false,
            MarketDataQualityHealth: "unknown",
            ScalpingDataGap: "-",
            ScalpingRobustnessExpanded: 0,
            ScalpingFinalCandidates: 0,
            ScalpingRejectedAfterExpansion: 0,
            BestFinalScalpingCandidate: null,
            ScalpingMonteCarloHealth: "unknown",
            ScalpingParameterSensitivityHealth: "unknown",
            ScalpingRegimeValidationHealth: "unknown",
            ScalpingSensitivityExplainabilityHealth: "unknown",
            ScalpingCandidatesWithStableCorridor: 0,
            ScalpingCandidatesBlockedBySensitivity: 0,
            BestScalpingParameterCorridorCandidate: null,
            ScalpingCertificationHealth: "unknown",
            ScalpingCertifiedCandidates: 0,
            ScalpingCertificationFailed: 0,
            BestCertifiedScalpingCandidate: null,
            ScalpingHumanReviewPackagesReady: 0,
            DomainValidationWarnings: [],
            ActiveGoals: [],
            TopGoal: "unknown",
            BlockedGoals: [],
            GoalProgressSummary: new Dictionary<string, double>(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

    private static string BuildMarkdown(RuntimeHealthSummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Runtime Health Summary");
        sb.AppendLine();
        sb.AppendLine($"- Hauptstatus: {report.MainStatus}");
        sb.AppendLine($"- Letzter Schritt: {report.LastStep}");
        sb.AppendLine($"- Nächster Schritt: {report.NextStep}");
        sb.AppendLine($"- Letztes Ergebnis: {report.LastResult}");
        sb.AppendLine($"- Frank nötig: {(report.FrankRequired ? "ja" : "nein")}");
        sb.AppendLine($"- Offene Reviews: {report.OpenReviews}");
        sb.AppendLine($"- Offene OOS-Pläne: {report.OpenOosPlans}");
        sb.AppendLine($"- Offene Forward-Pläne: {report.OpenForwardPlans}");
        sb.AppendLine($"- Letzte Warnung: {report.LastWarning}");
        sb.AppendLine($"- Safety Status: {report.SafetyStatus}");
        return sb.ToString();
    }
}
