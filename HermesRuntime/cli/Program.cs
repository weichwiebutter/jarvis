using Hermes.Runtime;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;

var app = new HermesCli(args);
return app.Run();

internal sealed class HermesCli
{
    private const string CliVersion = "0.1.0";

    private readonly string[] _args;
    private readonly string _runtimeRoot;
    private readonly string _dataRoot;

    public HermesCli(string[] args)
    {
        _args = args;
        _runtimeRoot = ResolveRuntimeRoot(args);
        _dataRoot = ResolveDataRoot(_runtimeRoot);
    }

    public int Run()
    {
        var command = FirstCommand(_args);

        return command switch
        {
            "" or "help" or "--help" or "-h" => ShowHelp(),
            "write-master-status" => WriteMasterStatus(),
            "master-status-refresh" => RefreshMasterStatus(),
            "master-status" => ShowMasterStatus(),
            "runtime-health-summary" => ShowRuntimeHealthSummary(),
            "runtime-health-history" => ShowRuntimeHealthHistory(),
            "runtime-stability-audit" => ShowRuntimeStabilityAudit(),
            "health" => ShowHealth(),
            "setup-watch" => ShowSetupWatch(),
            "events" => ShowEvents(),
            "jobs" => ShowJobs(),
            "storage" => ShowStorage(),
            "ctrader-health" => ShowCTraderHealth(),
            "ctrader-symbols" => ShowCTraderSymbols(),
            "ctrader-auth-url" => ShowCTraderAuthUrl(),
            "ctrader-auth-code" => ExchangeCTraderAuthCode(),
            "ctrader-auth-status" => ShowCTraderAuthStatus(),
            "download-history" or "import-ctrader-history" => DownloadCTraderHistory(),
            "import-csv" => ImportCsv(),
            "market-data-status" => ShowMarketDataStatus(),
            "scan-market-data" => ScanMarketData(),
            "market-data-quality" => ShowMarketDataQuality(),
            "normalize-market-data" => NormalizeMarketData(),
            "explain-market-data-gap" => ExplainMarketDataGap(),
            "generate-features" => GenerateFeatures(),
            "run-nightly-research" => RunNightlyResearch(),
            "run-nightly-beta3" => RunNightlyBeta3(),
            "nightly-status" => ShowNightlyStatus(),
            "nightly-stop-request" => RequestNightlyStop(),
            "scheduler-status" => ShowSchedulerStatus(),
            "workload-schedule-status" => ShowWorkloadScheduleStatus(),
            "scheduler-jobs" => ShowSchedulerJobs(),
            "time-control-status" => ShowTimeControlStatus(),
            "time-control-update" => UpdateTimeControl(),
            "startup-status" => ShowStartupStatus(),
            "readonly-bridge" or "bridge-start" => StartReadOnlyBridge(),
            "supervisor-start" => StartSupervisor(),
            "supervisor-status" => ShowSupervisorStatus(),
            "supervisor-stop-request" => RequestSupervisorStop(),
            "resource-status" => ShowResourceStatus(),
            "storage-status" => ShowStorageStatus(),
            "storage-cleanup-safety-audit" => ShowStorageCleanupSafetyAudit(),
            "cleanup-plan" => ShowCleanupPlan(),
            "cleanup-apply" => ApplyCleanup(),
            "research-status" => ShowResearchStatus(),
            "research-report" => ShowResearchReport(),
            "run-beta-learning" => RunBetaLearning(),
            "beta-status" => ShowBetaStatus(),
            "update-research-memory" => UpdateResearchMemory(),
            "research-memory" => ShowResearchMemory(),
            "run-long-research" => RunLongResearch(),
            "run-research-autopilot" => RunResearchAutopilot(),
            "run-strategy-research" => RunStrategyResearch(),
            "run-walkforward-validation" => RunWalkForwardValidation(),
            "realism-report" => ShowRealismReport(),
            "walkforward-summary" => ShowWalkForwardSummary(),
            "cost-sensitivity-report" => ShowCostSensitivityReport(),
            "monte-carlo-report" => ShowMonteCarloReport(),
            "cost-stress-report" => ShowCostStressReport(),
            "risk-of-ruin-report" => ShowRiskOfRuinReport(),
            "simulation-status" => ShowSimulationStatus(),
            "strategy-discovery-status" => ShowStrategyDiscoveryStatus(),
            "overfit-report" => ShowOverfitReport(),
            "robust-strategies" => ShowRobustStrategies(),
            "cognitive-status" => ShowCognitiveStatus(),
            "scan-knowledge-sources" => ScanKnowledgeSources(),
            "domain-status" => ShowDomainStatus(),
            "domain-insights" => ShowDomainInsights(),
            "software-domain-status" => ShowSingleDomainStatus("software"),
            "scan-software-domain" => ScanDomain("software"),
            "documentation-domain-status" => ShowSingleDomainStatus("documentation"),
            "scan-documentation-domain" => ScanDomain("documentation"),
            "process-domain-status" => ShowSingleDomainStatus("process"),
            "scan-process-domain" => ScanDomain("process"),
            "research-domain-status" => ShowSingleDomainStatus("research"),
            "scan-research-domain" => ScanDomain("research"),
            "knowledge-catalog" => ShowKnowledgeCatalog(),
            "knowledge-item" => ShowKnowledgeItem(),
            "knowledge-health" => ShowKnowledgeHealth(),
            "knowledge-reason-status" => ShowKnowledgeReasonStatus(),
            "knowledge-reason" => RunKnowledgeReason(),
            "trusted-knowledge-usage-audit-status" => ShowTrustedKnowledgeUsageAuditStatus(),
            "trusted-knowledge-usage-audit" => RunTrustedKnowledgeUsageAudit(),
            "trusted-knowledge-impact-status" => ShowTrustedKnowledgeImpactStatus(),
            "trusted-knowledge-impact" => RunTrustedKnowledgeImpact(),
            "autonomous-knowledge-advancement-status" => ShowAutonomousKnowledgeAdvancementStatus(),
            "autonomous-knowledge-advancement" => RunAutonomousKnowledgeAdvancement(),
            "knowledge-health-root-cause" => ShowKnowledgeHealthRootCause(),
            "knowledge-confidence-engine" => ShowKnowledgeConfidenceEngine(),
            "confidence-review-prioritization" => ShowConfidenceReviewPrioritization(),
            "domain-aware-review-prioritization" => ShowDomainAwareReviewPrioritization(),
            "review-action-plan" => ShowReviewActionPlan(),
            "promotion-status" => ShowPromotionStatus(),
            "knowledge-trust-promotion-status" => ShowKnowledgeTrustPromotionStatus(),
            "knowledge-trust-promote" => RunKnowledgeTrustPromotion(),
            "knowledge-state-consistency-status" => ShowKnowledgeStateConsistencyStatus(),
            "knowledge-state-consistency-check" => RunKnowledgeStateConsistencyCheck(),
            "knowledge-state-consistency-repair" => RunKnowledgeStateConsistencyRepair(),
            "next-trusted-candidates-status" => ShowNextTrustedCandidatesStatus(),
            "multi-source-evidence-status" => ShowMultiSourceEvidenceStatus(),
            "multi-source-evidence-plan" => ShowMultiSourceEvidencePlan(),
            "multi-source-evidence-apply" => RunMultiSourceEvidenceApply(),
            "canonical-evidence-status" => ShowCanonicalEvidenceStatus(),
            "canonical-evidence-run" => RunCanonicalEvidenceRun(),
            "knowledge-consolidation-analyzer" => ShowKnowledgeConsolidationAnalyzer(),
            "knowledge-consolidation-executor" => ShowKnowledgeConsolidationExecutor(),
            "strategy-mutation-analyzer" => ShowStrategyMutationAnalyzer(),
            "strategy-parameter-research-planner" => ShowStrategyParameterResearchPlanner(),
            "trading-research-synthesizer" => ShowTradingResearchSynthesizer(),
            "strategy-mutation-validation-planner" => ShowStrategyMutationValidationPlanner(),
            "strategy-validation-queue-export" => ShowStrategyValidationQueueExport(),
            "strategy-validation-readiness-analyzer" => ShowStrategyValidationReadinessAnalyzer(),
            "strategy-backtest-job-planner" => ShowStrategyBacktestJobPlanner(),
            "strategy-backtest-executor" => ShowStrategyBacktestExecutor(),
            "mutation-validation-executor" => ShowMutationValidationExecutor(),
            "autonomous-oos-planning" => ShowAutonomousOosPlanning(),
            "autonomous-oos-execution-gate" => ShowAutonomousOosExecutionGate(),
            "autonomous-forward-validation-planning" => ShowAutonomousForwardValidationPlanning(),
            "autonomous-forward-observation-gate" => ShowAutonomousForwardObservationGate(),
            "autonomous-forward-observation-sync" => ShowAutonomousForwardObservationSync(),
            "autonomous-research-loop-step" => ShowAutonomousResearchLoopStep(),
            "autonomous-research-loop-status" => ShowAutonomousResearchLoopStatus(),
            "mutation-attribution-analysis" => ShowMutationAttributionAnalysis(),
            "attribution-hypothesis-feedback" => ShowAttributionHypothesisFeedback(),
            "strategy-backtest-quality-audit" => ShowStrategyBacktestQualityAudit(),
            "strategy-backtest-evidence-gate" => ShowStrategyBacktestEvidenceGate(),
            "strategy-backtest-signal-density-analyzer" => ShowStrategyBacktestSignalDensityAnalyzer(),
            "strategy-backtest-failure-learning" => ShowStrategyBacktestFailureLearning(),
            "failure-guided-mutation-planner" => ShowFailureGuidedMutationPlanner(),
            "mutation-candidate-export" => ShowMutationCandidateExport(),
            "mutation-validation-job-planner" => ShowMutationValidationJobPlanner(),
            "strategy-dataset-gate-audit" => ShowStrategyDatasetGateAudit(),
            "trusted-candidates" => ShowTrustedCandidates(),
            "trusted-review-gate" => ShowTrustedReviewGate(),
            "generate-trusted-review-candidates" => GenerateTrustedReviewCandidates(),
            "trust-improvement-plan" => ShowTrustImprovementPlan(),
            "generate-trust-improvement-plan" => GenerateTrustImprovementPlan(),
            "review-promotion-candidates" => ReviewPromotionCandidates(),
            "explain-promotion" => ExplainPromotion(),
            "contradictions" => ShowContradictions(),
            "contradiction-status" => ShowContradictionStatus(),
            "review-knowledge" => ReviewKnowledge(),
            "review-status" => ShowReviewStatus(),
            "review-queue" => ShowReviewQueue(),
            "review-prioritization-audit" => ShowReviewPrioritizationAudit(),
            "review-queue-hygiene-audit" => ShowReviewQueueHygieneAudit(),
            "review-decision-assistant" => ShowReviewDecisionAssistant(),
            "review-status-consistency-audit" => ShowReviewStatusConsistencyAudit(),
            "evidence-auto-loop" => RunEvidenceAutoLoop(),
            "run-evidence-tasks" => RunEvidenceTasks(),
            "review-item" => ShowReviewItem(),
            "approve-review" => DecideReview("approved"),
            "reject-review" => DecideReview("rejected"),
            "request-more-evidence" => DecideReview("needs_more_evidence"),
            "defer-review" => DecideReview("deferred"),
            "review-summary" => ShowReviewSummary(),
            "consolidate-memory" => ConsolidateMemory(),
            "validation-plans" => ShowValidationPlans(),
            "generate-validation-plans" => GenerateValidationPlans(),
            "validate-knowledge" => ValidateKnowledge(),
            "execute-validation-tasks" => ExecuteValidationTasks(),
            "validate-domain-knowledge" => ValidateDomainKnowledge(),
            "domain-validation-status" => ShowDomainValidationStatus(),
            "validation-execution-log" => ShowValidationExecutionLog(),
            "validation-routing-status" => ShowValidationRoutingStatus(),
            "cleanup-invalid-validation-tasks" => CleanupInvalidValidationTasks(),
            "explain-validation-routing" => ExplainValidationRouting(),
            "knowledge-validation-status" => ShowKnowledgeValidationStatus(),
            "knowledge-validation-audit" => ShowKnowledgeValidationAudit(),
            "validation-evidence-status" => ShowValidationEvidenceStatus(),
            "validation-evidence" => RunValidationEvidence(),
            "validation-state-sync-status" => ShowValidationStateSyncStatus(),
            "validation-state-sync" => RunValidationStateSync(),
            "validation-backlog-analyzer" => ShowValidationBacklogAnalyzer(),
            "validation-backlog-executor" => ShowValidationBacklogExecutor(),
            "validation-backlog-executor-status" => ShowValidationBacklogExecutorStatus(),
            "validation-backlog-executor-enable" => SetValidationBacklogExecutorEnabled(true),
            "validation-backlog-executor-disable" => SetValidationBacklogExecutorEnabled(false),
            "validation-queue-refill" => RunValidationQueueRefill(),
            "run-evidence-validation" => RunEvidenceValidation(),
            "generate-improvement-queue" => GenerateImprovementQueue(),
            "improvement-queue-summary" => ShowImprovementQueueSummary(),
            "improvement-work-areas" => ShowImprovementWorkAreas(),
            "improvement-queue" => ShowImprovementQueue(),
            "work-area-policy" => ShowWorkAreaPolicy(),
            "execute-work-areas" => ExecuteWorkAreas(),
            "nightly-work-area-status" => ShowNightlyWorkAreaStatus(),
            "run-nightly-work-areas" => RunNightlyWorkAreas(),
            "evidence-auto-loop-status" => ShowEvidenceAutoLoopStatus(),
            "evidence-auto-loop-enable" => SetEvidenceAutoLoopEnabled(true),
            "evidence-auto-loop-disable" => SetEvidenceAutoLoopEnabled(false),
            "evidence-task-execution" => ShowEvidenceTaskExecution(),
            "evidence-impact-analysis" => ShowEvidenceImpactAnalysis(),
            "review-evidence-refresh" => ShowReviewEvidenceRefresh(),
            "execute-improvement-queue" => ExecuteImprovementQueue(),
            "improvement-execution-status" => ShowImprovementExecutionStatus(),
            "explain-validation" => ExplainValidation(),
            "research-queue" => ShowResearchQueue(),
            "enqueue-research" => EnqueueResearch(),
            "process-research-queue" => ProcessResearchQueue(),
            "web-research-source-collector-status" => ShowWebResearchSourceCollectorStatus(),
            "web-research-source-collector-export" => RunWebResearchSourceCollectorExport(),
            "trusted-source-catalog-status" => ShowTrustedSourceCatalogStatus(),
            "publisher-group-status" => ShowPublisherGroupStatus(),
            "publisher-group-refresh" => RunPublisherGroupRefresh(),
            "web-search-connector-status" => ShowWebSearchConnectorStatus(),
            "web-research-import-status" => ShowWebResearchImportStatus(),
            "web-research-import" => RunWebResearchImport(),
            "knowledge-evidence-match-status" => ShowKnowledgeEvidenceMatchStatus(),
            "knowledge-evidence-match" => RunKnowledgeEvidenceMatch(),
            "independent-source-resolver-status" => ShowIndependentSourceResolverStatus(),
            "independent-source-resolver" => RunIndependentSourceResolver(),
            "auto-source-review-status" => ShowAutoSourceReviewStatus(),
            "auto-source-review" => RunAutoSourceReview(),
            "automated-web-research-status" => ShowAutomatedWebResearchStatus(),
            "automated-web-research-fetch" => RunAutomatedWebResearchFetch(),
            "research-query-builder-status" => ShowResearchQueryBuilderStatus(),
            "direct-domain-research-status" => ShowDirectDomainResearchStatus(),
            "direct-domain-research-fetch" => RunDirectDomainResearchFetch(),
            "known-article-seed-status" => ShowKnownArticleSeedStatus(),
            "known-article-seed-fetch" => RunKnownArticleSeedFetch(),
            "seed-to-policy-trace-status" => ShowSeedToPolicyTraceStatus(),
            "multi-source-acquisition-status" => ShowMultiSourceAcquisitionStatus(),
            "multi-source-acquisition" => RunMultiSourceAcquisition(),
            "browser-research-status" => ShowBrowserResearchStatus(),
            "browser-research-fetch" => RunBrowserResearchFetch(),
            "generate-hypotheses" => GenerateHypotheses(),
            "cognitive-insights" => ShowCognitiveInsights(),
            "planning-status" => ShowPlanningStatus(),
            "detect-needs" => DetectNeeds(),
            "plan-next-tasks" => PlanNextTasks(),
            "run-planning-cycle" => RunPlanningCycle(),
            "execute-planned-tasks" => ExecutePlannedTasks(),
            "planned-task-status" => ShowPlannedTaskStatus(),
            "planned-task-executor-status" => ShowPlannedTaskExecutorStatus(),
            "planned-task-scheduler-link-status" => ShowPlannedTaskSchedulerLinkStatus(),
            "task-execution-log" => ShowTaskExecutionLog(),
            "evaluate-task-outcomes" => EvaluateTaskOutcomes(),
            "outcome-feedback-status" => ShowOutcomeFeedbackStatus(),
            "planner-feedback" => ShowPlannerFeedback(),
            "goal-feedback" => ShowGoalFeedback(),
            "goals" => ShowGoals(),
            "goal-status" => ShowGoalStatus(),
            "goal-progress" => ShowGoalProgress(),
            "explain-goal" => ExplainGoal(),
            "run-autonomous-loop" => RunAutonomousLoop(),
            "autonomous-loop-status" => ShowAutonomousLoopStatus(),
            "autonomous-loop-log" => ShowAutonomousLoopLog(),
            "explain-last-loop" => ExplainLastLoop(),
            "meta-review" => ShowMetaReview(),
            "domain-health" => ShowDomainHealth(),
            "learning-strategy" => ShowLearningStrategy(),
            "governance-status" => ShowGovernanceStatus(),
            "explain-plan" => ExplainPlan(),
            "explain-task" => ExplainTask(),
            "bot-candidates" => ShowBotCandidates(),
            "bot-candidate-report" => ShowBotCandidateReport(),
            "candidate-rejection-analysis" => ShowCandidateRejectionAnalysis(),
            "scalping-status" => ShowScalpingStatus(),
            "run-scalping-research" => RunScalpingResearch(),
            "run-multi-asset-scalping-research" => RunMultiAssetScalpingResearch(),
            "multi-asset-research-status" => ShowMultiAssetResearchStatus(),
            "scalping-candidates" => ShowScalpingCandidates(),
            "scalping-candidate" => ShowScalpingCandidate(),
            "scalping-validation-report" => ShowScalpingValidationReport(),
            "run-scalping-robustness-expansion" => RunScalpingRobustnessExpansion(),
            "scalping-robustness-report" => ShowScalpingRobustnessReport(),
            "scalping-sensitivity-report" => ShowScalpingSensitivityReport(),
            "explain-scalping-blocker" => ExplainScalpingBlocker(),
            "scalping-parameter-corridor" => ShowScalpingParameterCorridor(),
            "scalping-final-candidates" => ShowScalpingFinalCandidates(),
            "run-scalping-certification" => RunScalpingCertification(),
            "scalping-certification-report" => ShowScalpingCertificationReport(),
            "certification-report" => ShowCertificationReport(),
            "scalping-certified-candidates" => ShowScalpingCertifiedCandidates(),
            "candidate-audit-report" => ShowCandidateAuditReport(),
            "candidate-details" => ShowCandidateDetails(),
            "certified-candidate-inventory" => ShowCertifiedCandidateInventory(),
            "setup-registry" => ShowSetupRegistry(),
            "explain-setup-selection" => ExplainSetupSelection(),
            "scalping-human-review-package" => ShowScalpingHumanReviewPackage(),
            "export-scalping-bot-spec" => ExportScalpingBotSpec(),
            "scalping-bot-spec" => ShowScalpingBotSpec(),
            "scalping-bot-specs" => ShowScalpingBotSpecs(),
            "export-signal-agent-spec" => ExportSignalAgentSpec(),
            "signal-agent-spec" => ShowSignalAgentSpec(),
            "signal-agent-specs" => ShowSignalAgentSpecs(),
            "scalping-portfolio-status" => ShowScalpingPortfolioStatus(),
            "build-scalping-portfolio" => BuildScalpingPortfolio(),
            "scalping-ensemble-plan" => ShowScalpingEnsemblePlan(),
            "scalping-portfolio-candidates" => ShowScalpingPortfolioCandidates(),
            "ensemble-portfolio-status" => ShowEnsemblePortfolioStatus(),
            "explain-ensemble-selection" => ExplainEnsembleSelection(),
            "search-more-scalping-candidates" => SearchMoreScalpingCandidates(),
            "scalping-multi-asset-roadmap" => ShowScalpingMultiAssetRoadmap(),
            "update-scalping-multi-asset-roadmap" => UpdateScalpingMultiAssetRoadmap(),
            "scalping-asset-status" => ShowScalpingAssetStatus(),
            "optimize-scalping-ensemble" => OptimizeScalpingEnsemble(),
            "scalping-ensemble-optimized" => ShowScalpingEnsembleOptimized(),
            "scalping-ensemble-member" => ShowScalpingEnsembleMember(),
            "export-scalping-ensemble-package" => ExportScalpingEnsemblePackage(),
            "scalping-ensemble-package" => ShowScalpingEnsemblePackage(),
            "validate-ensemble-signal-package" => ValidateEnsembleSignalPackage(),
            "system-b-handoff-bundle" => ShowSystemBHandoffBundle(),
            "cloud-embedded-release-package" => ShowCloudEmbeddedReleasePackage(),
            "hermes-paperbot-replay" => ShowHermesPaperBotReplay(),
            "scalping-ensemble-human-review-package" => ShowScalpingEnsembleHumanReviewPackage(),
            "scalping-ensemble-review-status" => ShowScalpingEnsembleReviewStatus(),
            "approve-scalping-ensemble" => ApproveScalpingEnsemble(),
            "reject-scalping-ensemble" => RejectScalpingEnsemble(),
            "defer-scalping-ensemble" => DeferScalpingEnsemble(),
            "request-more-scalping-evidence" => RequestMoreScalpingEvidence(),
            "demo-signal-feed-status" => ShowDemoSignalFeedStatus(),
            "generate-demo-signals" => GenerateDemoSignals(),
            "latest-demo-signals" => ShowLatestDemoSignals(),
            "demo-signal-feed-log" => ShowDemoSignalFeedLog(),
            "signal-watch-status" => ShowSignalWatchStatus(),
            "signal-watch-log" => ShowSignalWatchLog(),
            "export-missing-signal-agent-specs" => ExportMissingSignalAgentSpecs(),
            "validate-ensemble-signal-specs" => ValidateEnsembleSignalSpecs(),
            "create-forward-test-plan" => CreateForwardTestPlan(),
            "forward-test-status" => ShowForwardTestStatus(),
            "forward-test-log" => ShowForwardTestLog(),
            "record-forward-test-observation" => RecordForwardTestObservation(),
            "run-forward-test-observation" => RunForwardTestObservation(),
            "latest-forward-test-observations" => ShowLatestForwardTestObservations(),
            "forward-test-summary" => ShowForwardTestSummary(),
            "current-market-status" => ShowCurrentMarketStatus(),
            "update-current-market-snapshot" => UpdateCurrentMarketSnapshot(),
            "current-market-snapshot" => ShowCurrentMarketSnapshot(),
            "explain-current-market-gap" => ExplainCurrentMarketGap(),
            "update-ctrader-readonly-quotes" => UpdateCTraderReadonlyQuotes(),
            "ctrader-readonly-quotes" => ShowCTraderReadonlyQuotes(),
            "quote-snapshot-status" => ShowQuoteSnapshotStatus(),
            "near-miss-strategies" => ShowNearMissStrategies(),
            "improvement-experiments" => ShowImprovementExperiments(),
            "run-quality-improvement-experiments" => RunQualityImprovementExperiments(),
            "quality-improvement-report" => ShowQualityImprovementReport(),
            "cost-resilience-report" => ShowCostResilienceReport(),
            "oos-stability-report" => ShowOosStabilityReport(),
            "risk-sensitivity-report" => ShowRiskSensitivityReport(),
            "strategy-research-status" => ShowStrategyResearchStatus(),
            "top-strategies" => ShowTopStrategies(),
            "knowledge-sources" => ShowKnowledgeSources(),
            "research-insights" => ShowResearchInsights(),
            "strategy-clusters" => ShowStrategyClusters(),
            "regime-summary" => ShowRegimeSummary(),
            "strategy-regime-performance" => ShowStrategyRegimePerformance(),
            "regime-distribution" => ShowRegimeDistribution(),
            "pattern-catalog" => ShowPatternCatalog(),
            "pattern-performance" => ShowPatternPerformance(),
            "features" => ShowFeatures(),
            "signals" => ShowSignals(),
            "backtests" => ShowBacktests(),
            "outcomes" => ShowOutcomes(),
            "market-data" => ShowMarketData(),
            "version" => ShowVersion(),
            _ => UnknownCommand(command)
        };
    }

    private int ShowHelp()
    {
        WriteHeader("Hermes CLI Foundation");
        Console.WriteLine("Lokale Sicherheits-CLI fuer HermesRuntime. Status-Kommandos sind read-only; generate-features und run-nightly-research schreiben nur lokale Analyseartefakte.");
        Console.WriteLine();
        Console.WriteLine("Kommandos:");
        Console.WriteLine("  hermes write-master-status Master Status Snapshot schreiben");
        Console.WriteLine("  hermes master-status-refresh [--knowledge-only] [--max-seconds N] Master Status Snapshot bewusst refreshen");
        Console.WriteLine("  hermes master-status      kompakten Gesamtstatus aus bestehenden Reports anzeigen");
        Console.WriteLine("  hermes runtime-health-summary kompakten Betreiberstatus anzeigen");
        Console.WriteLine("  hermes runtime-health-history Betriebs-Historie schreiben und anzeigen");
        Console.WriteLine("  hermes runtime-stability-audit Stabilitaets-Audit anzeigen");
        Console.WriteLine("  hermes health             RuntimeHealth anzeigen");
        Console.WriteLine("  hermes setup-watch        Setup-Watch-Kandidaten anzeigen");
        Console.WriteLine("  hermes events recent      letzte Runtime-Events anzeigen");
        Console.WriteLine("  hermes jobs               Queue/Jobjournale anzeigen");
        Console.WriteLine("  hermes storage            lokale Storage-Uebersicht anzeigen");
        Console.WriteLine("  hermes ctrader-health     cTrader Open API Health anzeigen");
        Console.WriteLine("  hermes ctrader-symbols    cTrader Symbol-Mapping anzeigen");
        Console.WriteLine("  hermes ctrader-auth-url   cTrader OAuth URL anzeigen");
        Console.WriteLine("  hermes ctrader-auth-code  OAuth Redirect-Code gegen Token tauschen");
        Console.WriteLine("  hermes ctrader-auth-status lokalen cTrader Token-Status anzeigen");
        Console.WriteLine("  hermes download-history   historische cTrader-Candles read-only laden oder Stub-Fallback nutzen");
        Console.WriteLine("  hermes import-csv         cTrader Candle-CSV lokal importieren");
        Console.WriteLine("  hermes market-data-status Market Data Availability anzeigen");
        Console.WriteLine("  hermes scan-market-data   lokale CSV-Datenquellen scannen");
        Console.WriteLine("  hermes market-data-quality --asset XAUUSD Datenqualitaet anzeigen");
        Console.WriteLine("  hermes normalize-market-data --asset XAUUSD CSVs normalisieren");
        Console.WriteLine("  hermes explain-market-data-gap --asset XAUUSD Data Gap erklaeren");
        Console.WriteLine("  hermes generate-features  FeatureVectors aus lokalen Candle-Daten erzeugen");
        Console.WriteLine("  hermes run-nightly-research lokale Research-Pipeline ausfuehren");
        Console.WriteLine("  hermes run-nightly-beta3 Nightly Beta 3 Research-Orchestrierung starten");
        Console.WriteLine("  hermes nightly-status    Nightly Beta 3 Status anzeigen");
        Console.WriteLine("  hermes nightly-stop-request sicheren Stop-Request fuer Nightly Beta 3 setzen");
        Console.WriteLine("  hermes scheduler-status  internen Hermes Scheduler Status anzeigen");
        Console.WriteLine("  hermes workload-schedule-status Hermes Research Workload Schedule anzeigen");
        Console.WriteLine("  hermes scheduler-jobs    geplante Hermes Jobs anzeigen");
        Console.WriteLine("  hermes time-control-status zentrale Arbeitszeit-/Window-Konfiguration anzeigen");
        Console.WriteLine("  hermes time-control-update zentrale Arbeitszeit-/Window-Konfiguration aktualisieren");
        Console.WriteLine("  hermes startup-status    Bridge-/Scheduler-Startstatus und Start-Hilfe anzeigen");
        Console.WriteLine("  hermes readonly-bridge   localhost Read-only Bridge fuer Jarvis Control Center starten");
        Console.WriteLine("  hermes supervisor-start  langlebigen Hermes Supervisor starten");
        Console.WriteLine("  hermes supervisor-status Supervisor Heartbeat/State anzeigen");
        Console.WriteLine("  hermes supervisor-stop-request sicheren Supervisor Stop Request setzen");
        Console.WriteLine("  hermes resource-status   CPU/RAM/Disk ResourceGuard anzeigen");
        Console.WriteLine("  hermes storage-status    Storage-/Retention-Status anzeigen");
        Console.WriteLine("  hermes storage-cleanup-safety-audit Cleanup-Kandidaten sicher analysieren");
        Console.WriteLine("  hermes cleanup-plan      sicheren Storage Cleanup-Plan erzeugen");
        Console.WriteLine("  hermes cleanup-apply --safe sicheren Cleanup-Plan anwenden");
        Console.WriteLine("  hermes research-status    letzten Nightly-Research-Report anzeigen");
        Console.WriteLine("  hermes research-report    letzten ResearchSummaryReport anzeigen");
        Console.WriteLine("  hermes run-beta-learning  Trading Learning Beta 1 lokal ausfuehren");
        Console.WriteLine("  hermes beta-status        letzten Trading Learning Beta Report anzeigen");
        Console.WriteLine("  hermes update-research-memory Research Memory Index aktualisieren");
        Console.WriteLine("  hermes research-memory    Research Memory Index anzeigen");
        Console.WriteLine("  hermes run-long-research  checkpointed Long-Run Research starten");
        Console.WriteLine("  hermes run-research-autopilot kombinierte Data-/Pattern-/Strategy-Research-Pipeline starten");
        Console.WriteLine("  hermes run-strategy-research adaptive Strategy-Research-Varianten bewerten");
        Console.WriteLine("  hermes run-walkforward-validation Walk-Forward-/Overfit-Validation ausfuehren");
        Console.WriteLine("  hermes realism-report    Realism-/Kosten-/Overfit-Qualitaetsreport anzeigen");
        Console.WriteLine("  hermes walkforward-summary Walk-Forward Summary anzeigen");
        Console.WriteLine("  hermes cost-sensitivity-report Brokerkosten-Sensitivitaetsreport anzeigen");
        Console.WriteLine("  hermes monte-carlo-report Monte-Carlo-Qualitaetsreport erzeugen/anzeigen");
        Console.WriteLine("  hermes cost-stress-report Spread-/Slippage-Stress-Test anzeigen");
        Console.WriteLine("  hermes risk-of-ruin-report konservativen Risk-of-Ruin-Report anzeigen");
        Console.WriteLine("  hermes simulation-status Realistic Simulation Status anzeigen");
        Console.WriteLine("  hermes strategy-discovery-status Trusted Strategy Discovery anzeigen");
        Console.WriteLine("  hermes overfit-report     Overfit-/Risk-Report anzeigen");
        Console.WriteLine("  hermes robust-strategies  robuste Strategy-Kandidaten anzeigen");
        Console.WriteLine("  hermes cognitive-status   Cognitive Core Status anzeigen");
        Console.WriteLine("  hermes scan-knowledge-sources Knowledge Sources read-only scannen");
        Console.WriteLine("  hermes domain-status      aktive Cognitive Domains anzeigen");
        Console.WriteLine("  hermes domain-insights    Multi-Domain Insights anzeigen");
        Console.WriteLine("  hermes scan-software-domain Software-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-documentation-domain Dokumentations-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-process-domain Prozess-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-research-domain Research-Domaene metadata-only scannen");
        Console.WriteLine("  hermes knowledge-catalog  allgemeinen Cognitive Knowledge Catalog anzeigen");
        Console.WriteLine("  hermes knowledge-item --id <ID> einzelnes Knowledge Item anzeigen");
        Console.WriteLine("  hermes knowledge-health   Knowledge Trust/Quality Scores erzeugen und anzeigen");
        Console.WriteLine("  hermes knowledge-reason --topic \"bullish engulfing\" Trusted Knowledge Reasoning anzeigen");
        Console.WriteLine("  hermes knowledge-reason-status Trusted Knowledge Reasoning Status anzeigen");
        Console.WriteLine("  hermes trusted-knowledge-usage-audit Trusted Knowledge Usage Audit erzeugen");
        Console.WriteLine("  hermes trusted-knowledge-usage-audit-status Trusted Knowledge Usage Audit anzeigen");
        Console.WriteLine("  hermes trusted-knowledge-impact Trusted Knowledge Impact Report erzeugen");
        Console.WriteLine("  hermes trusted-knowledge-impact-status Trusted Knowledge Impact Report anzeigen");
        Console.WriteLine("  hermes autonomous-knowledge-advancement [--execute] [--max-items N] Autonomous Knowledge Advancement ausfuehren");
        Console.WriteLine("  hermes autonomous-knowledge-advancement-status Autonomous Knowledge Advancement Status anzeigen");
        Console.WriteLine("  hermes knowledge-health-root-cause Knowledge Trust Root Cause Analyse anzeigen");
        Console.WriteLine("  hermes knowledge-confidence-engine Knowledge Confidence Score anzeigen");
        Console.WriteLine("  hermes confidence-review-prioritization Confidence-basierte Review Priorisierung anzeigen");
        Console.WriteLine("  hermes domain-aware-review-prioritization Domain-aware Review Priorisierung anzeigen");
        Console.WriteLine("  hermes review-action-plan Review Action Plan anzeigen");
        Console.WriteLine("  hermes trusted-source-catalog-status Trusted Source Catalog anzeigen");
        Console.WriteLine("  hermes promotion-status   Knowledge Promotion Status anzeigen");
        Console.WriteLine("  hermes knowledge-trust-promotion-status Trust-Promotion Pipeline Status anzeigen");
        Console.WriteLine("  hermes knowledge-trust-promote [--dry-run|--apply] [--max-seconds N] [--skip-refresh] Trusted Knowledge promoten");
        Console.WriteLine("  hermes knowledge-state-consistency-status Knowledge State Consistency Status anzeigen");
        Console.WriteLine("  hermes knowledge-state-consistency-check Knowledge State Consistency pruefen");
        Console.WriteLine("  hermes knowledge-state-consistency-repair [--dry-run|--apply] Knowledge State Consistency reparieren");
        Console.WriteLine("  hermes next-trusted-candidates-status Nächste Trusted-Kandidaten und Aktionsplan anzeigen");
        Console.WriteLine("  hermes multi-source-evidence-status Multi-Source Evidence Status anzeigen");
        Console.WriteLine("  hermes multi-source-evidence-plan Multi-Source Evidence Plan anzeigen");
        Console.WriteLine("  hermes multi-source-evidence-apply [--dry-run|--apply] Multi-Source Evidence anwenden");
        Console.WriteLine("  hermes canonical-evidence-status Canonical Evidence Acquisition Status anzeigen");
        Console.WriteLine("  hermes canonical-evidence-run [--max-items N] [--max-fetch-seconds N] [--dry-run|--apply] Canonical Evidence Acquisition ausfuehren");
        Console.WriteLine("  hermes knowledge-consolidation-analyzer Knowledge Consolidation Analyzer anzeigen");
        Console.WriteLine("  hermes knowledge-consolidation-executor Knowledge Consolidation Kandidaten erzeugen");
        Console.WriteLine("  hermes strategy-mutation-analyzer Strategy Mutation Kandidaten anzeigen");
        Console.WriteLine("  hermes strategy-parameter-research-planner Strategy Parameter Research Planner anzeigen");
        Console.WriteLine("  hermes trading-research-synthesizer Trading Research Synthesizer anzeigen");
        Console.WriteLine("  hermes strategy-mutation-validation-planner Strategy Mutation Validierung planen");
        Console.WriteLine("  hermes strategy-validation-queue-export Strategy Validation Queue exportieren");
        Console.WriteLine("  hermes strategy-validation-readiness-analyzer Strategy Validation Readiness analysieren");
        Console.WriteLine("  hermes strategy-backtest-job-planner Strategy Backtest Job Planner anzeigen");
        Console.WriteLine("  hermes strategy-backtest-executor Strategy Backtest Executor anzeigen");
        Console.WriteLine("  hermes mutation-validation-executor Mutation Validation Executor anzeigen");
        Console.WriteLine("  hermes autonomous-oos-planning OOS-Validierungsplaene aus Hypothesen erzeugen");
        Console.WriteLine("  hermes autonomous-oos-execution-gate genau einen OOS-Plan sicher ausfuehren");
        Console.WriteLine("  hermes autonomous-forward-validation-planning Forward-Validierungsplaene erzeugen");
        Console.WriteLine("  hermes autonomous-forward-observation-gate Forward-Beobachtung read-only ausfuehren");
        Console.WriteLine("  hermes autonomous-forward-observation-sync Forward-Observation Status synchronisieren");
        Console.WriteLine("  hermes autonomous-research-loop-step einen autonomen Research-Schritt ausfuehren");
        Console.WriteLine("  hermes autonomous-research-loop-status autonomen Research-Loop Status anzeigen");
        Console.WriteLine("  hermes mutation-attribution-analysis Mutation Attribution Analysis anzeigen");
        Console.WriteLine("  hermes attribution-hypothesis-feedback Attribution in Research Hypothesis ueberfuehren");
        Console.WriteLine("  hermes strategy-backtest-quality-audit Strategy Backtest Quality Audit anzeigen");
        Console.WriteLine("  hermes strategy-backtest-evidence-gate Strategy Backtest Evidence Gate anzeigen");
        Console.WriteLine("  hermes strategy-backtest-signal-density-analyzer Strategy Backtest Signal Density Analyzer anzeigen");
        Console.WriteLine("  hermes strategy-backtest-failure-learning Strategy Backtest Failure Learning anzeigen");
        Console.WriteLine("  hermes failure-guided-mutation-planner Failure Guided Mutation Planner anzeigen");
        Console.WriteLine("  hermes mutation-candidate-export Mutation Candidate Queue exportieren");
        Console.WriteLine("  hermes mutation-validation-job-planner Mutation Validation Job Planner anzeigen");
        Console.WriteLine("  hermes strategy-dataset-gate-audit Strategy Dataset Gate Audit anzeigen");
        Console.WriteLine("  hermes trusted-candidates Trusted Knowledge Kandidaten anzeigen");
        Console.WriteLine("  hermes trusted-review-gate Trusted Knowledge Review Gate anzeigen");
        Console.WriteLine("  hermes generate-trusted-review-candidates Trusted Review Kandidaten erzeugen");
        Console.WriteLine("  hermes trust-improvement-plan Knowledge Trust Improvement Plan anzeigen");
        Console.WriteLine("  hermes generate-trust-improvement-plan Knowledge Trust Improvement Plan erzeugen");
        Console.WriteLine("  hermes review-promotion-candidates Promotion Kandidaten reviewen");
        Console.WriteLine("  hermes explain-promotion --id <ID> Promotion Entscheidung erklaeren");
        Console.WriteLine("  hermes contradictions     Knowledge Contradiction Report erzeugen/anzeigen");
        Console.WriteLine("  hermes contradiction-status Widerspruchsstatus kompakt anzeigen");
        Console.WriteLine("  hermes review-knowledge --id <ID> [--result approved|rejected|needs_review] Human Review Evidence speichern");
        Console.WriteLine("  hermes review-status      Human Review Evidence Status anzeigen");
        Console.WriteLine("  hermes review-queue       offene Human Review Queue anzeigen");
        Console.WriteLine("  hermes review-prioritization-audit Reviews priorisieren und gruppieren");
        Console.WriteLine("  hermes review-queue-hygiene-audit Review Queue Hygiene auditieren");
        Console.WriteLine("  hermes review-decision-assistant Review-Entscheidungshilfe anzeigen");
        Console.WriteLine("  hermes review-status-consistency-audit Review-/Master-Status Konsistenz anzeigen");
        Console.WriteLine("  hermes evidence-auto-loop Sicheren Evidenz-Auto-Loop planen");
        Console.WriteLine("  hermes evidence-auto-loop-status Evidenz-Auto-Loop Status anzeigen");
        Console.WriteLine("  hermes evidence-auto-loop-enable Evidenz-Auto-Loop aktivieren");
        Console.WriteLine("  hermes evidence-auto-loop-disable Evidenz-Auto-Loop deaktivieren");
        Console.WriteLine("  hermes run-evidence-tasks Evidence-/Validierungsaufgaben sicher ausfuehren");
        Console.WriteLine("  hermes evidence-task-execution Evidence Task Execution Report anzeigen");
        Console.WriteLine("  hermes evidence-impact-analysis Evidence Impact Analysis anzeigen");
        Console.WriteLine("  hermes review-evidence-refresh Review Evidence Refresh anzeigen");
        Console.WriteLine("  hermes review-item --id <REVIEW_ID> einzelnes Review Item anzeigen");
        Console.WriteLine("  hermes approve-review --id <REVIEW_ID> --note \"...\" Review approven");
        Console.WriteLine("  hermes reject-review --id <REVIEW_ID> --note \"...\" Review ablehnen");
        Console.WriteLine("  hermes request-more-evidence --id <REVIEW_ID> --note \"...\" mehr Evidenz anfordern");
        Console.WriteLine("  hermes defer-review --id <REVIEW_ID> --note \"...\" Review zurueckstellen");
        Console.WriteLine("  hermes review-summary     Human Review Workflow Summary anzeigen");
        Console.WriteLine("  hermes consolidate-memory Cognitive Memory markieren/konsolidieren, ohne Wissen zu loeschen");
        Console.WriteLine("  hermes generate-validation-plans --max-items 50 Plaene fuer weak Knowledge erzeugen");
        Console.WriteLine("  hermes validation-plans   Knowledge Validation Plans anzeigen");
        Console.WriteLine("  hermes validate-knowledge --max-items 20 Validation Tasks in Research Queue einreihen");
        Console.WriteLine("  hermes execute-validation-tasks --max-items 20 Validation Tasks kontrolliert ausfuehren");
        Console.WriteLine("  hermes validate-domain-knowledge --domain documentation --max-items 20 Domain-spezifische Knowledge Validation ausfuehren");
        Console.WriteLine("  hermes domain-validation-status Domain-spezifischen Validation Status anzeigen");
        Console.WriteLine("  hermes validation-execution-log Validation Execution Log anzeigen");
        Console.WriteLine("  hermes validation-routing-status Domain Validation Router anzeigen");
        Console.WriteLine("  hermes cleanup-invalid-validation-tasks unpassende Validation Tasks bereinigen");
        Console.WriteLine("  hermes explain-validation-routing --domain documentation Routing-Profil erklaeren");
        Console.WriteLine("  hermes knowledge-validation-status Validation Fortschritt anzeigen");
        Console.WriteLine("  hermes knowledge-validation-audit Knowledge Validation Audit anzeigen");
        Console.WriteLine("  hermes validation-backlog-analyzer Validierungsstau und Auto-Plan anzeigen");
        Console.WriteLine("  hermes validation-backlog-executor Validierungsstau sicher abarbeiten");
        Console.WriteLine("  hermes validation-backlog-executor-status Validierungsstau Executor Status anzeigen");
        Console.WriteLine("  hermes validation-backlog-executor-enable Validierungsstau Executor aktivieren");
        Console.WriteLine("  hermes validation-backlog-executor-disable Validierungsstau Executor deaktivieren");
        Console.WriteLine("  hermes validation-queue-refill Validation Queue aus offenen Plaenen auffuellen");
        Console.WriteLine("  hermes run-evidence-validation sichere Evidenz-/Validierungs-Aufgaben ausfuehren");
        Console.WriteLine("  hermes validation-evidence-status Validation Evidence Pipeline Status anzeigen");
        Console.WriteLine("  hermes validation-evidence [--dry-run|--apply] Validation Evidence Pipeline ausfuehren");
        Console.WriteLine("  hermes validation-state-sync-status Validation State Synchronizer Status anzeigen");
        Console.WriteLine("  hermes validation-state-sync [--dry-run|--apply] Validation State Synchronizer ausfuehren");
        Console.WriteLine("  hermes generate-improvement-queue Verbesserungs-Warteschlange aus Audit/Warnungen erzeugen");
        Console.WriteLine("  hermes improvement-queue-summary Verbesserungs-Warteschlange kompakt anzeigen");
        Console.WriteLine("  hermes improvement-work-areas Verbesserungs-Arbeitsbereiche anzeigen");
        Console.WriteLine("  hermes work-area-policy Work-Area-Ausfuehrungsregeln anzeigen");
        Console.WriteLine("  hermes execute-work-areas erlaubte Work Areas ausfuehren");
        Console.WriteLine("  hermes nightly-work-area-status Nightly Work Area Status anzeigen");
        Console.WriteLine("  hermes run-nightly-work-areas Re-Validierung im Nightly-Fenster ausfuehren");
        Console.WriteLine("  hermes improvement-queue Verbesserungs-Warteschlange anzeigen");
        Console.WriteLine("  hermes execute-improvement-queue sichere Verbesserungsaufgaben ausfuehren");
        Console.WriteLine("  hermes improvement-execution-status Verbesserungs-Ausfuehrungsstatus anzeigen");
        Console.WriteLine("  hermes explain-validation --id <KNOWLEDGE_ITEM_ID> Validierungsplan erklaeren");
        Console.WriteLine("  hermes research-queue     Cognitive Research Queue anzeigen");
        Console.WriteLine("  hermes enqueue-research --domain trading --type validation Research-Item einreihen");
        Console.WriteLine("  hermes process-research-queue --max-items 50 Research Queue verarbeiten");
        Console.WriteLine("  hermes web-research-source-collector-status Web Research Source Collector Status anzeigen");
        Console.WriteLine("  hermes web-research-source-collector-export [--apply] Web Research Requests exportieren");
        Console.WriteLine("  hermes web-search-connector-status Web Search Connector Status anzeigen");
        Console.WriteLine("  hermes publisher-group-status Publisher Group Resolver Status anzeigen");
        Console.WriteLine("  hermes publisher-group-refresh Publisher Group Report aktualisieren");
        Console.WriteLine("  hermes web-research-import-status Web Research Import Status anzeigen");
        Console.WriteLine("  hermes web-research-import [--dry-run|--apply] Web Research Quellen importieren");
        Console.WriteLine("  hermes knowledge-evidence-match-status Knowledge Evidence Semantic Matcher Status anzeigen");
        Console.WriteLine("  hermes knowledge-evidence-match [--dry-run|--apply] Knowledge Evidence Semantic Matcher ausfuehren");
        Console.WriteLine("  hermes independent-source-resolver-status Independent Source Resolver Status anzeigen");
        Console.WriteLine("  hermes independent-source-resolver [--dry-run|--apply] Independent Source Resolver ausfuehren");
        Console.WriteLine("  hermes automated-web-research-status Web Research Fetcher Status anzeigen");
        Console.WriteLine("  hermes automated-web-research-fetch --max-items 10 [--dry-run|--apply] Web Research automatisch abrufen");
        Console.WriteLine("  hermes research-query-builder-status Research Query Builder Status anzeigen");
        Console.WriteLine("  hermes direct-domain-research-status Direct Domain Research Status anzeigen");
        Console.WriteLine("  hermes direct-domain-research-fetch --max-items 5 [--max-fetch-seconds N] [--dry-run|--apply] Direct Domain Research ausfuehren");
        Console.WriteLine("  hermes known-article-seed-status Known Article Seed Catalog Status anzeigen");
        Console.WriteLine("  hermes known-article-seed-fetch --max-items 10 [--max-fetch-seconds N] [--dry-run|--apply] Known Article Seeds abrufen");
        Console.WriteLine("  hermes seed-to-policy-trace-status Seed-to-Policy Trace Diagnostics anzeigen");
        Console.WriteLine("  hermes multi-source-acquisition-status Multi Source Acquisition Status anzeigen");
        Console.WriteLine("  hermes multi-source-acquisition --max-items 10 [--dry-run|--apply] Multi Source Acquisition ausfuehren");
        Console.WriteLine("  hermes browser-research-status Browser Research Agent Status anzeigen");
        Console.WriteLine("  hermes browser-research-fetch --max-items 5 [--dry-run|--apply] Browser Research ausfuehren");
        Console.WriteLine("  hermes generate-hypotheses --domain trading Cross-Knowledge-Hypothesen erzeugen");
        Console.WriteLine("  hermes cognitive-insights Cognitive Insights anzeigen");
        Console.WriteLine("  hermes planning-status  Autonomous Planning Status anzeigen");
        Console.WriteLine("  hermes detect-needs     aktuelle Bedarfe erkennen");
        Console.WriteLine("  hermes plan-next-tasks --max-items 20 Aufgaben aus Needs/Goals planen");
        Console.WriteLine("  hermes run-planning-cycle --max-items 20 Planning Cycle ausfuehren und Research Queue aktualisieren");
        Console.WriteLine("  hermes execute-planned-tasks --max-items 10 geplante Aufgaben kontrolliert ausfuehren");
        Console.WriteLine("  hermes planned-task-status Planned Task Execution Status anzeigen");
        Console.WriteLine("  hermes planned-task-executor-status Planned Task Executor Diagnose anzeigen");
        Console.WriteLine("  hermes planned-task-scheduler-link-status Planned Task Scheduler Link Diagnose anzeigen");
        Console.WriteLine("  hermes task-execution-log Planned Task Execution Log anzeigen");
        Console.WriteLine("  hermes evaluate-task-outcomes --max-items 50 ausgefuehrte Planned Tasks bewerten");
        Console.WriteLine("  hermes outcome-feedback-status Outcome Feedback Status anzeigen");
        Console.WriteLine("  hermes planner-feedback Planner Feedback anzeigen");
        Console.WriteLine("  hermes goal-feedback Goal Feedback anzeigen");
        Console.WriteLine("  hermes goals persistente Hermes Goals anzeigen");
        Console.WriteLine("  hermes goal-status --id <GOAL_ID> einzelnes Goal anzeigen");
        Console.WriteLine("  hermes goal-progress Goal Progress Report anzeigen");
        Console.WriteLine("  hermes explain-goal --id <GOAL_ID> Zielbezug, Blocker und naechste Aktionen erklaeren");
        Console.WriteLine("  hermes run-autonomous-loop --max-iterations 5 vollstaendigen Need->Insight Lernloop ausfuehren");
        Console.WriteLine("  hermes autonomous-loop-status Autonomous Learning Loop Status anzeigen");
        Console.WriteLine("  hermes autonomous-loop-log Autonomous Learning Loop JSONL-Auszug anzeigen");
        Console.WriteLine("  hermes explain-last-loop letzte autonome Loop-Iteration erklaeren");
        Console.WriteLine("  hermes meta-review Meta-Learning Review erzeugen/anzeigen");
        Console.WriteLine("  hermes domain-health Domain Health Scores anzeigen");
        Console.WriteLine("  hermes learning-strategy aktuelle Lernstrategie anzeigen");
        Console.WriteLine("  hermes governance-status Governance Entscheidungen anzeigen");
        Console.WriteLine("  hermes explain-plan     letzte Planning-Entscheidung erklaeren");
        Console.WriteLine("  hermes explain-task --id <TASK_ID> einzelne geplante Aufgabe erklaeren");
        Console.WriteLine("  hermes bot-candidates     strenge Demo-Bot-Kandidatenbewertung anzeigen");
        Console.WriteLine("  hermes bot-candidate-report Bot-Candidate-Report mit Ablehnungsgruenden anzeigen");
        Console.WriteLine("  hermes candidate-rejection-analysis Ablehnungsdiagnose fuer Bot-Kandidaten anzeigen");
        Console.WriteLine("  hermes scalping-status   fokussierten Scalping-Research-Status anzeigen");
        Console.WriteLine("  hermes run-scalping-research --asset XAUUSD --max-variants 50 gezielte Scalping-Varianten testen");
        Console.WriteLine("  hermes run-multi-asset-scalping-research --assets XAUUSD,EURUSD,GER40 --max-variants 100 Multi-Asset Scalping-Pipeline ausfuehren");
        Console.WriteLine("  hermes multi-asset-research-status Multi-Asset Scalping-Status anzeigen");
        Console.WriteLine("  hermes scalping-candidates Scalping-Kandidaten anzeigen");
        Console.WriteLine("  hermes scalping-candidate --id <ID> einzelnen Scalping-Kandidaten anzeigen");
        Console.WriteLine("  hermes scalping-validation-report strenge Scalping-Gates anzeigen");
        Console.WriteLine("  hermes run-scalping-robustness-expansion --id <ID>|--all-robust robuste Scalping-Kandidaten erweitern");
        Console.WriteLine("  hermes scalping-robustness-report --id <ID> Robustness Expansion anzeigen");
        Console.WriteLine("  hermes scalping-sensitivity-report --id <ID> Parameter-Sensitivity Details anzeigen");
        Console.WriteLine("  hermes explain-scalping-blocker --id <ID> Scalping-Blocker erklaeren");
        Console.WriteLine("  hermes scalping-parameter-corridor --id <ID> stabilen Parameterkorridor anzeigen");
        Console.WriteLine("  hermes scalping-final-candidates finale Scalping-Kandidaten anzeigen");
        Console.WriteLine("  hermes run-scalping-certification --id <ID>|--all-final finale Scalping-Kandidaten zertifizieren");
        Console.WriteLine("  hermes scalping-certification-report --id <ID> Certification Report anzeigen");
        Console.WriteLine("  hermes scalping-certified-candidates zertifizierte Scalping-Kandidaten anzeigen");
        Console.WriteLine("  hermes candidate-audit-report vollständigen Kandidaten-Audit anzeigen");
        Console.WriteLine("  hermes candidate-details --id <ID> Kandidaten-Details anzeigen");
        Console.WriteLine("  hermes scalping-human-review-package --id <ID> Human Review Package anzeigen");
        Console.WriteLine("  hermes export-scalping-bot-spec --id <ID> cTrader-Spezifikationsreport exportieren");
        Console.WriteLine("  hermes scalping-bot-spec --id <ID> cTrader-Spezifikation anzeigen");
        Console.WriteLine("  hermes scalping-bot-specs exportierte cTrader-Spezifikationen anzeigen");
        Console.WriteLine("  hermes export-signal-agent-spec --id <ID> Signal-Agent-Spezifikationsreport exportieren");
        Console.WriteLine("  hermes signal-agent-spec --id <ID> Signal-Agent-Spezifikation anzeigen");
        Console.WriteLine("  hermes signal-agent-specs exportierte Signal-Agent-Spezifikationen anzeigen");
        Console.WriteLine("  hermes build-scalping-portfolio Scalping-Portfolio-Report erzeugen");
        Console.WriteLine("  hermes scalping-portfolio-status Scalping-Portfolio-Status anzeigen");
        Console.WriteLine("  hermes scalping-ensemble-plan Scalping-Ensemble-Plan anzeigen");
        Console.WriteLine("  hermes scalping-portfolio-candidates Portfolio-Kandidaten anzeigen");
        Console.WriteLine("  hermes ensemble-portfolio-status Ensemble-Portfolio-Status anzeigen");
        Console.WriteLine("  hermes explain-ensemble-selection --asset GER40 Ensemble-Auswahl begruenden");
        Console.WriteLine("  hermes search-more-scalping-candidates --asset XAUUSD --max-variants 100 weitere Kandidaten suchen");
        Console.WriteLine("  hermes update-scalping-multi-asset-roadmap Multi-Asset-Roadmap aktualisieren");
        Console.WriteLine("  hermes scalping-multi-asset-roadmap Multi-Asset-Roadmap anzeigen");
        Console.WriteLine("  hermes scalping-asset-status --asset GER40 Asset-Roadmap-Status anzeigen");
        Console.WriteLine("  hermes optimize-scalping-ensemble --mode balanced optimiertes Scalping-Ensemble erzeugen");
        Console.WriteLine("  hermes scalping-ensemble-optimized optimiertes Scalping-Ensemble anzeigen");
        Console.WriteLine("  hermes scalping-ensemble-member --id <ID> optimiertes Ensemble-Mitglied anzeigen");
        Console.WriteLine("  hermes export-scalping-ensemble-package Ensemble Export Package erzeugen");
        Console.WriteLine("  hermes scalping-ensemble-package Ensemble Export Package anzeigen");
        Console.WriteLine("  hermes validate-ensemble-signal-package Ensemble Signal-Agent Package validieren");
        Console.WriteLine("  hermes system-b-handoff-bundle System-B Uebergabepaket erzeugen");
        Console.WriteLine("  hermes cloud-embedded-release-package Cloud-kompatibles Embedded Release Package erzeugen");
        Console.WriteLine("  hermes hermes-paperbot-replay [--asset XAUUSD --timeframe M5] lokales HermesPaperBot Replay-Report-Paket erzeugen");
        Console.WriteLine("  hermes scalping-ensemble-human-review-package Ensemble Human Review Package anzeigen");
        Console.WriteLine("  hermes scalping-ensemble-review-status Ensemble Review Status anzeigen");
        Console.WriteLine("  hermes approve-scalping-ensemble --mode demo_signal_use|forward_test_preparation Ensemble freigeben");
        Console.WriteLine("  hermes reject-scalping-ensemble --reason \"<TEXT>\" Ensemble ablehnen");
        Console.WriteLine("  hermes defer-scalping-ensemble --reason \"<TEXT>\" Ensemble zurueckstellen");
        Console.WriteLine("  hermes request-more-scalping-evidence --reason \"<TEXT>\" mehr Evidenz anfordern");
        Console.WriteLine("  hermes demo-signal-feed-status Demo Signal Feed Status anzeigen");
        Console.WriteLine("  hermes generate-demo-signals read-only Demo-Signale erzeugen");
        Console.WriteLine("  hermes latest-demo-signals letzte Demo-Signale anzeigen");
        Console.WriteLine("  hermes demo-signal-feed-log Demo Signal Feed Log anzeigen");
        Console.WriteLine("  hermes signal-watch-status read-only Signal-Watch-Lifecycle anzeigen");
        Console.WriteLine("  hermes signal-watch-log Signal-Watch-Log anzeigen");
        Console.WriteLine("  hermes export-missing-signal-agent-specs fehlende Ensemble-Signal-Agent-Specs exportieren");
        Console.WriteLine("  hermes validate-ensemble-signal-specs Ensemble-Signal-Agent-Specs validieren");
        Console.WriteLine("  hermes create-forward-test-plan read-only Forward-Test-Plan erzeugen");
        Console.WriteLine("  hermes forward-test-status Forward-Test-Status anzeigen");
        Console.WriteLine("  hermes forward-test-log Forward-Test-Log anzeigen");
        Console.WriteLine("  hermes record-forward-test-observation --signal-id <ID> --result <RESULT> --note \"<TEXT>\" Beobachtung protokollieren");
        Console.WriteLine("  hermes run-forward-test-observation Demo-Signale read-only beobachten");
        Console.WriteLine("  hermes latest-forward-test-observations letzte Forward-Test-Beobachtungen anzeigen");
        Console.WriteLine("  hermes forward-test-summary Forward-Test-Zusammenfassung anzeigen");
        Console.WriteLine("  hermes current-market-status Current Market Snapshot Status anzeigen");
        Console.WriteLine("  hermes update-current-market-snapshot read-only Current Market Snapshot aktualisieren");
        Console.WriteLine("  hermes current-market-snapshot read-only Current Market Snapshot anzeigen");
        Console.WriteLine("  hermes explain-current-market-gap --asset XAUUSD Snapshot-Luecke erklaeren");
        Console.WriteLine("  hermes update-ctrader-readonly-quotes cTrader read-only Quotes aktualisieren");
        Console.WriteLine("  hermes ctrader-readonly-quotes cTrader read-only Quotes anzeigen");
        Console.WriteLine("  hermes quote-snapshot-status cTrader Quote Snapshot Status anzeigen");
        Console.WriteLine("  hermes near-miss-strategies beinahe geeignete verworfene Strategien anzeigen");
        Console.WriteLine("  hermes improvement-experiments naechste Research-Experimente anzeigen");
        Console.WriteLine("  hermes run-quality-improvement-experiments gezielte OOS-/Cost-/Risk-Experimente erzeugen");
        Console.WriteLine("  hermes quality-improvement-report Quality-Improvement-Experimentbericht anzeigen");
        Console.WriteLine("  hermes cost-resilience-report Cost-Resilience-Experimente anzeigen");
        Console.WriteLine("  hermes oos-stability-report OOS-/Walk-Forward-Experimente anzeigen");
        Console.WriteLine("  hermes risk-sensitivity-report Risk-Sensitivity-Experimente anzeigen");
        Console.WriteLine("  hermes strategy-research-status Strategy-Research-Memory anzeigen");
        Console.WriteLine("  hermes top-strategies     beste Strategy-Research-Varianten anzeigen");
        Console.WriteLine("  hermes knowledge-sources  kuratierte Strategy-Discovery-Quellen anzeigen");
        Console.WriteLine("  hermes research-insights  Strategy-Research-Insights anzeigen");
        Console.WriteLine("  hermes strategy-clusters  Strategy-Cluster anzeigen");
        Console.WriteLine("  hermes regime-summary     Market-Regime-Zusammenfassung erzeugen/anzeigen");
        Console.WriteLine("  hermes strategy-regime-performance Strategy-Performance nach Regime anzeigen");
        Console.WriteLine("  hermes regime-distribution Regime-Verteilung nach Symbol/Timeframe anzeigen");
        Console.WriteLine("  hermes pattern-catalog    Strategy/Pattern Knowledge Base anzeigen");
        Console.WriteLine("  hermes pattern-performance Pattern-Fitness aggregiert anzeigen");
        Console.WriteLine("  hermes features           letzte Feature-JSONL-Zeilen anzeigen");
        Console.WriteLine("  hermes signals            letzte Signal-JSONL-Zeilen anzeigen");
        Console.WriteLine("  hermes backtests          Demo-Backtest-Reports anzeigen");
        Console.WriteLine("  hermes outcomes           Signal-Outcome-Reports anzeigen");
        Console.WriteLine("  hermes market-data        historische Candle-JSONL-Dateien anzeigen");
        Console.WriteLine("  hermes version            CLI-/Runtime-Version anzeigen");
        Console.WriteLine();
        Console.WriteLine("Start ohne Installation:");
        Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- health");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int WriteMasterStatus()
    {
        WriteHeader("Hermes Master Status Snapshot");
        var writer = BuildMasterStatusWriter(BuildStoragePaths());
        var snapshot = writer.WriteSnapshot();

        PrintMasterStatusSnapshot(snapshot, writer.SnapshotPath);
        Console.WriteLine();
        WriteSafety();
        return snapshot.OverallStatus.Equals("critical", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int ShowMasterStatus()
    {
        WriteHeader("Hermes Master Status");
        var maxSeconds = ReadIntOption(_args, "--max-seconds", 60, 1, 3600);
        var writer = BuildMasterStatusWriter(BuildStoragePaths());
        var snapshot = writer.LoadSnapshot();
        if (snapshot is null)
        {
            WriteWarning("Master-Status-Snapshot fehlt. Verwende keinen blockierenden Refresh im CLI-Pfad.");
            WriteWarning("WARN stale/refresh_timeout");
            WriteSafety();
            return 1;
        }
        else if (!IsMasterStatusSnapshotFresh(writer.SnapshotPath))
        {
            WriteWarning($"Master-Status-Snapshot ist veraltet. Verwende Snapshot statt Refresh (max-seconds={maxSeconds}).");
            WriteWarning("WARN stale/refresh_timeout");
        }

        PrintMasterStatusSnapshot(snapshot, writer.SnapshotPath);
        Console.WriteLine();
        WriteSafety();
        return snapshot.OverallStatus.Equals("critical", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int RefreshMasterStatus()
    {
        WriteHeader("Hermes Master Status Refresh");
        var knowledgeOnly = HasArg("--knowledge-only");
        var maxSeconds = ReadIntOption(_args, "--max-seconds", 120, 1, 3600);
        var storagePaths = BuildStoragePaths();
        var writer = BuildMasterStatusWriter(storagePaths);
        var timeout = TimeSpan.FromSeconds(maxSeconds);
        var stage = knowledgeOnly ? "run_knowledge_quality" : "write_snapshot";
        var refreshRoot = Path.Combine(storagePaths.Root, "reports", "master-status-refresh");
        Directory.CreateDirectory(refreshRoot);
        var refreshReportPath = Path.Combine(refreshRoot, "master_status_refresh_report.json");
        var refreshMarkdownPath = Path.Combine(refreshRoot, "master_status_refresh_report.md");
        var startedAt = DateTimeOffset.UtcNow;

        var runTask = Task.Run(() =>
        {
            if (knowledgeOnly)
            {
                var quality = new KnowledgeQualityEngine(storagePaths).Run();
                stage = "write_knowledge_snapshot";
                return writer.WriteKnowledgeOnlySnapshot(quality);
            }

            stage = "write_snapshot";
            return writer.WriteSnapshot();
        });

        if (!runTask.Wait(timeout))
        {
            var timeoutReport = new Dictionary<string, object?>
            {
                ["status"] = knowledgeOnly ? "blocked_knowledge_refresh_timeout" : "blocked_master_status_refresh_timeout",
                ["last_successful_stage"] = stage,
                ["affected_items"] = Array.Empty<string>(),
                ["recommended_next_action"] = knowledgeOnly
                    ? "retry_with_smaller_batch_or_review_knowledge_quality_refresh"
                    : "retry_master_status_refresh_with_smaller_batch_or_inspect_slow_sections",
                ["timeout_seconds"] = maxSeconds,
                ["started_at_utc"] = startedAt.ToString("O"),
                ["updated_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["snapshot_path"] = writer.SnapshotPath,
                ["report_path"] = refreshReportPath,
                ["knowledge_only"] = knowledgeOnly
            };

            File.WriteAllText(refreshReportPath, JsonSerializer.Serialize(timeoutReport, JsonDefaults.WriteOptions));
            File.WriteAllText(refreshMarkdownPath, BuildMasterStatusRefreshTimeoutMarkdown(timeoutReport));

            WriteWarning($"Master-Status-Refresh timed out after {maxSeconds} seconds.");
            WriteWarning("WARN stale/refresh_timeout");
            WriteField("Report", DisplayPath(refreshReportPath));
            WriteField("Markdown", DisplayPath(refreshMarkdownPath));
            WriteField("Last Successful Stage", "write_snapshot");
            WriteSafety();
            return 1;
        }

        var snapshot = runTask.Result;
        WriteField("Master Status", DisplayPath(writer.SnapshotPath));
        WriteField("Knowledge Only", knowledgeOnly ? "ja" : "nein");
        WriteField("Last Successful Stage", stage);
        Console.WriteLine();
        PrintMasterStatusSnapshot(snapshot, writer.SnapshotPath);
        Console.WriteLine();
        WriteSafety();
        return snapshot.OverallStatus.Equals("critical", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int ShowRuntimeHealthSummary()
    {
        WriteHeader("Hermes Runtime Health Summary");
        var service = new RuntimeHealthSummaryService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Hauptstatus", report.MainStatus);
        WriteField("Letzter Schritt", report.LastStep);
        WriteField("Nächster Schritt", report.NextStep);
        WriteField("Letztes Ergebnis", report.LastResult);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Offene Reviews", report.OpenReviews.ToString());
        WriteField("Offene OOS-Pläne", report.OpenOosPlans.ToString());
        WriteField("Offene Forward-Pläne", report.OpenForwardPlans.ToString());
        WriteField("Letzte Warnung", report.LastWarning);
        WriteField("Safety Status", report.SafetyStatus);
        WriteField("Operator Summary", report.OperatorSummary);
        WriteSafety();
        return report.MainStatus.Equals("fehler", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int ShowRuntimeHealthHistory()
    {
        WriteHeader("Hermes Runtime Health History");
        var service = new RuntimeHealthHistoryService(BuildStoragePaths(), _runtimeRoot);
        var entry = service.AppendFromSummary();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Timestamp", entry.TimestampUtc.ToString("O"));
        WriteField("Hauptstatus", entry.MainStatus);
        WriteField("Letzter Schritt", entry.LastStep);
        WriteField("Nächster Schritt", entry.NextStep);
        WriteField("Letztes Ergebnis", entry.LastResult);
        WriteField("Frank nötig", entry.FrankRequired ? "ja" : "nein");
        WriteField("Offene Reviews", entry.OpenReviews.ToString());
        WriteField("Offene OOS-Pläne", entry.OpenOosPlans.ToString());
        WriteField("Offene Forward-Pläne", entry.OpenForwardPlans.ToString());
        WriteField("Safety Status", entry.SafetyStatus);
        WriteSafety();
        return 0;
    }

    private int ShowRuntimeStabilityAudit()
    {
        WriteHeader("Hermes Runtime Stability Audit");
        var service = new RuntimeStabilityAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Operator Summary", report.OperatorSummary);
        WriteField("24h", $"{report.Last24Hours.ArbeitetPercent:0.##}% / {report.Last24Hours.WartetPercent:0.##}% / {report.Last24Hours.FrankNoetigPercent:0.##}% / {report.Last24Hours.FehlerPercent:0.##}%");
        WriteField("7d", $"{report.Last7Days.ArbeitetPercent:0.##}% / {report.Last7Days.WartetPercent:0.##}% / {report.Last7Days.FrankNoetigPercent:0.##}% / {report.Last7Days.FehlerPercent:0.##}%");
        WriteField("Frank-Eskalationen", report.Last7Days.FrankEscalations.ToString());
        WriteField("Fehler", report.Last7Days.FehlerPercent.ToString("0.##") + "%");
        WriteSafety();
        return 0;
    }

    private bool IsMasterStatusSnapshotFresh(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return false;
        }

        var snapshot = JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(snapshotPath), JsonDefaults.SnapshotReadOptions);
        if (snapshot is null)
        {
            return false;
        }

        var humanReview = new HumanReviewWorkflow(BuildStoragePaths()).BuildSummary();
        var quality = new KnowledgeQualityEngine(BuildStoragePaths()).Run();
        return snapshot.PendingReviews == humanReview.PendingReviews
            && snapshot.NeedsMoreEvidenceReviews == humanReview.NeedsMoreEvidenceReviews
            && snapshot.ApprovedReviews == humanReview.ApprovedReviews
            && snapshot.RejectedReviews == humanReview.RejectedReviews
            && snapshot.DeferredReviews == humanReview.DeferredReviews
            && snapshot.TrustedKnowledge == quality.TrustedKnowledge
            && snapshot.WeakKnowledge == quality.WeakKnowledge
            && snapshot.DeprecatedKnowledge == quality.DeprecatedKnowledge
            && Math.Abs(snapshot.AverageQualityScore - quality.AverageQualityScore) < 0.0001
            && Math.Abs(snapshot.AverageTrustScore - quality.AverageTrustScore) < 0.0001
            && string.Equals(snapshot.KnowledgeHealth, quality.KnowledgeHealth, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(snapshot.EvidenceCoverage - quality.EvidenceCoverage) < 0.0001
            && snapshot.ContradictionCount == quality.ContradictionCount
            && snapshot.HumanReviewedItems == quality.HumanReviewedItems
            && Math.Abs(snapshot.ValidationCoverage - quality.ValidationCoverage) < 0.0001;
    }

    private MasterStatusWriter BuildMasterStatusWriter(StoragePaths storagePaths) =>
        new(new MasterStatusService(storagePaths, _runtimeRoot));

    private void TryWriteMasterStatusSnapshot(StoragePaths storagePaths, bool printPath = true)
    {
        try
        {
            var writer = BuildMasterStatusWriter(storagePaths);
            writer.WriteSnapshot();
            if (printPath)
            {
                WriteField("Master Status", DisplayPath(writer.SnapshotPath));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            if (printPath)
            {
                WriteWarning($"Master Status Snapshot konnte nicht geschrieben werden: {ex.Message}");
            }
        }
    }

    private static string BuildMasterStatusRefreshTimeoutMarkdown(IReadOnlyDictionary<string, object?> report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Master Status Refresh Timeout");
        sb.AppendLine();
        foreach (var entry in report)
        {
            sb.AppendLine($"- {entry.Key}: {entry.Value ?? "-"}");
        }

        return sb.ToString();
    }

    private void PrintMasterStatusSnapshot(MasterStatusSnapshot snapshot, string reportPath)
    {
        WriteField("overall_status", snapshot.OverallStatus);
        WriteField("current_focus", snapshot.CurrentFocus);
        var sectionTimings = snapshot.SectionTimingsMs ?? new Dictionary<string, long>();
        var slowSections = snapshot.SlowSections ?? [];
        WriteField("master_status_section_timings", sectionTimings.Count == 0 ? "-" : string.Join(", ", sectionTimings.OrderByDescending(item => item.Value).Select(item => $"{item.Key}={item.Value}ms")));
        WriteMessages("slow_sections", slowSections);
        WriteField("active_domains", string.Join(", ", snapshot.ActiveDomains));
        WriteField("queued_tasks", snapshot.QueuedTasks.ToString());
        WriteField("last_nightly_run", snapshot.LastNightlyRun ?? "-");
        WriteField("last_autonomous_loop", snapshot.LastAutonomousLoop ?? "-");
        WriteField("last_meta_review", snapshot.LastMetaReview ?? "-");
        WriteField("learning_strategy", snapshot.LearningStrategy);
        WriteField("supervisor_running", snapshot.SupervisorRunning.ToString().ToLowerInvariant());
        WriteField("scheduler_enabled", snapshot.SchedulerEnabled.ToString());
        WriteField("resource_action", snapshot.ResourceAction);
        WriteField("storage_cleanup", snapshot.StorageCleanup.ToString());
        WriteField("auto_cleanup_policy_enabled", snapshot.AutoCleanupPolicyEnabled.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_allowed", snapshot.AutoCleanupAllowed.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_last_run", snapshot.AutoCleanupLastRun ?? "-");
        WriteField("auto_cleanup_last_result", snapshot.AutoCleanupLastResult);
        WriteField("cleanup_candidates", snapshot.CleanupCandidates.ToString());
        WriteField("estimated_free_bytes", snapshot.EstimatedFreeBytes.ToString());
        WriteField("protected_paths_count", snapshot.ProtectedPathsCount.ToString());
        WriteField("safety_mode", snapshot.SafetyMode);
        WriteField("robust_strategies", snapshot.RobustStrategies.ToString());
        WriteField("demo_bot_candidates", snapshot.DemoBotCandidates.ToString());
        WriteField("trusted_knowledge", snapshot.TrustedKnowledge.ToString());
        WriteField("weak_knowledge", snapshot.WeakKnowledge.ToString());
        WriteField("deprecated_knowledge", snapshot.DeprecatedKnowledge.ToString());
        WriteField("average_quality_score", $"{snapshot.AverageQualityScore:0.####}");
        WriteField("average_trust_score", $"{snapshot.AverageTrustScore:0.####}");
        WriteField("knowledge_health", snapshot.KnowledgeHealth);
        WriteField("knowledge_trend", snapshot.KnowledgeTrend);
        WriteField("evidence_coverage", $"{snapshot.EvidenceCoverage:0.####}");
        WriteField("validation_coverage", $"{snapshot.ValidationCoverage:0.####}");
        WriteField("contradiction_count", snapshot.ContradictionCount.ToString());
        WriteField("human_reviewed_items", snapshot.HumanReviewedItems.ToString());
        WriteMessages(
            "trust_distribution",
            snapshot.TrustDistribution
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}: {item.Value}")
                .ToList());
        WriteField("pending_reviews", snapshot.PendingReviews.ToString());
        WriteField("approved_reviews", snapshot.ApprovedReviews.ToString());
        WriteField("rejected_reviews", snapshot.RejectedReviews.ToString());
        WriteField("needs_more_evidence", snapshot.NeedsMoreEvidenceReviews.ToString());
        WriteField("deferred_reviews", snapshot.DeferredReviews.ToString());
        WriteField("review_coverage", $"{snapshot.ReviewCoverage:0.####}");
        WriteMessages("top_review_priorities", snapshot.TopReviewPriorities.Take(8).ToList());
        WriteField("validation_plans_open", snapshot.ValidationPlansOpen.ToString());
        WriteField("validation_tasks_pending", snapshot.ValidationTasksPending.ToString());
        WriteField("trusted_candidate_count", snapshot.TrustedCandidateCount.ToString());
        WriteField("knowledge_items_needing_oos", snapshot.KnowledgeItemsNeedingOos.ToString());
        WriteField("knowledge_items_needing_source_check", snapshot.KnowledgeItemsNeedingSourceCheck.ToString());
        WriteField("invalid_validation_tasks", snapshot.InvalidValidationTasks.ToString());
        WriteField("validation_tasks_cleaned", snapshot.ValidationTasksCleaned.ToString());
        WriteField("validation_routing_health", snapshot.ValidationRoutingHealth);
        WriteField("domain_validation_health", snapshot.DomainValidationHealth);
        WriteField("documentation_validation_pending", snapshot.DocumentationValidationPending.ToString());
        WriteField("software_validation_pending", snapshot.SoftwareValidationPending.ToString());
        WriteField("process_validation_pending", snapshot.ProcessValidationPending.ToString());
        WriteField("research_validation_pending", snapshot.ResearchValidationPending.ToString());
        WriteField("scalping_asset", snapshot.ScalpingAsset);
        WriteField("scalping_candidates_total", snapshot.ScalpingCandidatesTotal.ToString());
        WriteField("scalping_robust_candidates", snapshot.ScalpingRobustCandidates.ToString());
        WriteField("scalping_rejected_candidates", snapshot.ScalpingRejectedCandidates.ToString());
        WriteField("scalping_needs_more_data", snapshot.ScalpingNeedsMoreData.ToString());
        WriteField("best_scalping_candidate", snapshot.BestScalpingCandidate ?? "-");
        WriteField("signal_agent_specs_ready", snapshot.SignalAgentSpecsReady.ToString());
        WriteField("latest_signal_agent_spec", snapshot.LatestSignalAgentSpec is null ? "-" : DisplayPath(snapshot.LatestSignalAgentSpec));
        WriteField("signal_agent_export_health", snapshot.SignalAgentExportHealth);
        WriteField("certified_candidate_signal_ready", snapshot.CertifiedCandidateSignalReady.ToString().ToLowerInvariant());
        WriteField("ctrader_bot_specs_ready", snapshot.CTraderBotSpecsReady.ToString());
        WriteField("latest_ctrader_bot_spec", snapshot.LatestCTraderBotSpec is null ? "-" : DisplayPath(snapshot.LatestCTraderBotSpec));
        WriteField("ctrader_bot_export_health", snapshot.CTraderBotExportHealth);
        WriteField("certified_candidate_bot_ready", snapshot.CertifiedCandidateBotReady.ToString().ToLowerInvariant());
        WriteField("candidate_portfolio_mode", snapshot.CandidatePortfolioMode);
        WriteField("scalping_portfolio_status", snapshot.ScalpingPortfolioStatus);
        WriteField("scalping_portfolio_members", snapshot.ScalpingPortfolioMembers.ToString());
        WriteField("scalping_ensemble_candidates", snapshot.ScalpingEnsembleCandidates.ToString());
        WriteField("scalping_signal_density_score", $"{snapshot.ScalpingSignalDensityScore:0.####}");
        WriteField("scalping_portfolio_diversity_score", $"{snapshot.ScalpingPortfolioDiversityScore:0.####}");
        WriteField("scalping_next_candidate_search_action", snapshot.ScalpingNextCandidateSearchAction);
        WriteField("scalping_multi_asset_mode", snapshot.ScalpingMultiAssetMode);
        WriteMessages("scalping_next_assets", snapshot.ScalpingNextAssets);
        WriteMessages("scalping_assets_with_data", snapshot.ScalpingAssetsWithData);
        WriteMessages("scalping_assets_needing_data", snapshot.ScalpingAssetsNeedingData);
        WriteField("scalping_multi_asset_roadmap_health", snapshot.ScalpingMultiAssetRoadmapHealth);
        WriteField("multi_asset_research_status", snapshot.MultiAssetResearchStatus);
        WriteMessages("multi_asset_assets_ready", snapshot.MultiAssetAssetsReady);
        WriteMessages("multi_asset_assets_setup_ready", snapshot.MultiAssetAssetsSetupReady);
        WriteMessages("multi_asset_assets_data_ready_only", snapshot.MultiAssetAssetsDataReadyOnly);
        WriteMessages("multi_asset_assets_missing_data", snapshot.MultiAssetAssetsMissingData);
        WriteMessages("data_ready_assets", snapshot.DataReadyAssets);
        WriteMessages("signal_ready_assets", snapshot.SignalReadyAssets);
        WriteMessages("setup_ready_assets", snapshot.SetupReadyAssets);
        WriteMessages("bot_ready_assets", snapshot.BotReadyAssets);
        WriteMessages("assets_needing_validation", snapshot.AssetsNeedingValidation);
        WriteMessages("assets_missing_data", snapshot.AssetsMissingData);
        WriteField("certified_candidate_inventory_status", snapshot.CertifiedCandidateInventoryStatus);
        WriteField("setup_registry_status", snapshot.SetupRegistryStatus);
        WriteMessages("setup_registry_assets", snapshot.SetupRegistryAssets);
        WriteField("xauusd_setup_count", snapshot.XauusdSetupCount.ToString());
        WriteField("eurusd_setup_count", snapshot.EurusdSetupCount.ToString());
        WriteField("ger40_setup_count", snapshot.Ger40SetupCount.ToString());
        WriteField("best_xauusd_setup", snapshot.BestXauusdSetup ?? "-");
        WriteField("best_eurusd_setup", snapshot.BestEurusdSetup ?? "-");
        WriteField("best_ger40_setup", snapshot.BestGer40Setup ?? "-");
        WriteField("total_setup_ready_assets", snapshot.TotalSetupReadyAssets.ToString());
        WriteField("total_signal_specs_ready", snapshot.TotalSignalSpecsReady.ToString());
        WriteField("eurusd_certified_candidates", snapshot.EurusdCertifiedCandidates.ToString());
        WriteField("ensemble_candidate_status", snapshot.EnsembleCandidateStatus);
        WriteField("ensemble_candidate_members", snapshot.EnsembleCandidateMembers.ToString());
        WriteField("ensemble_candidate_health", snapshot.EnsembleCandidateHealth);
        WriteField("scalping_ensemble_optimizer_health", snapshot.ScalpingEnsembleOptimizerHealth);
        WriteField("scalping_optimized_ensemble_status", snapshot.ScalpingOptimizedEnsembleStatus);
        WriteField("scalping_optimized_ensemble_members", snapshot.ScalpingOptimizedEnsembleMembers.ToString());
        WriteField("scalping_optimized_ensemble_mode", snapshot.ScalpingOptimizedEnsembleMode);
        WriteField("scalping_optimized_ensemble_drawdown", $"{snapshot.ScalpingOptimizedEnsembleDrawdown:0.####}");
        WriteField("scalping_optimized_ensemble_signal_density", $"{snapshot.ScalpingOptimizedEnsembleSignalDensity:0.####}");
        WriteField("scalping_optimized_ensemble_readiness", snapshot.ScalpingOptimizedEnsembleReadiness);
        WriteField("scalping_ensemble_package_ready", snapshot.ScalpingEnsemblePackageReady.ToString().ToLowerInvariant());
        WriteField("latest_scalping_ensemble_package", snapshot.LatestScalpingEnsemblePackage is null ? "-" : DisplayPath(snapshot.LatestScalpingEnsemblePackage));
        WriteField("scalping_ensemble_export_health", snapshot.ScalpingEnsembleExportHealth);
        WriteField("scalping_ensemble_human_review_ready", snapshot.ScalpingEnsembleHumanReviewReady.ToString().ToLowerInvariant());
        WriteField("scalping_ensemble_review_status", snapshot.ScalpingEnsembleReviewStatus);
        WriteField("scalping_ensemble_approved_for_demo_signal_use", snapshot.ScalpingEnsembleApprovedForDemoSignalUse.ToString().ToLowerInvariant());
        WriteField("scalping_ensemble_approved_for_forward_test_preparation", snapshot.ScalpingEnsembleApprovedForForwardTestPreparation.ToString().ToLowerInvariant());
        WriteField("scalping_ensemble_review_health", snapshot.ScalpingEnsembleReviewHealth);
        WriteField("latest_scalping_ensemble_review", snapshot.LatestScalpingEnsembleReview is null ? "-" : DisplayPath(snapshot.LatestScalpingEnsembleReview));
        WriteField("demo_signal_feed_status", snapshot.DemoSignalFeedStatus);
        WriteField("demo_signals_available", snapshot.DemoSignalsAvailable.ToString().ToLowerInvariant());
        WriteField("latest_demo_signal_count", snapshot.LatestDemoSignalCount.ToString());
        WriteField("demo_signal_feed_health", snapshot.DemoSignalFeedHealth);
        WriteField("demo_signal_feed_mode", snapshot.DemoSignalFeedMode);
        WriteField("forward_test_status", snapshot.ForwardTestStatus);
        WriteField("forward_test_mode", snapshot.ForwardTestMode);
        WriteField("forward_test_assets", snapshot.ForwardTestAssets.Count == 0 ? "-" : string.Join(", ", snapshot.ForwardTestAssets));
        WriteField("forward_test_signals_observed", snapshot.ForwardTestSignalsObserved.ToString());
        WriteField("forward_test_observations_total", snapshot.ForwardTestObservationsTotal.ToString());
        WriteField("forward_test_triggered_count", snapshot.ForwardTestTriggeredCount.ToString());
        WriteField("forward_test_invalidated_count", snapshot.ForwardTestInvalidatedCount.ToString());
        WriteField("forward_test_simulated_observation_count", snapshot.ForwardTestSimulatedObservationCount.ToString());
        WriteField("forward_test_latest_observation_utc", snapshot.ForwardTestLatestObservationUtc?.ToString("O") ?? "-");
        WriteField("forward_test_using_current_market_snapshot", snapshot.ForwardTestUsingCurrentMarketSnapshot.ToString().ToLowerInvariant());
        WriteField("forward_test_health", snapshot.ForwardTestHealth);
        WriteField("forward_test_requires_human_review", snapshot.ForwardTestRequiresHumanReview.ToString().ToLowerInvariant());
        WriteField("current_market_snapshot_status", snapshot.CurrentMarketSnapshotStatus);
        WriteField("current_market_assets_available", snapshot.CurrentMarketAssetsAvailable.Count == 0 ? "-" : string.Join(", ", snapshot.CurrentMarketAssetsAvailable));
        WriteField("current_market_snapshot_health", snapshot.CurrentMarketSnapshotHealth);
        WriteField("current_market_latest_update_utc", snapshot.CurrentMarketLatestUpdateUtc?.ToString("O") ?? "-");
        WriteField("market_data_assets_available", snapshot.MarketDataAssetsAvailable.Count == 0 ? "-" : string.Join(", ", snapshot.MarketDataAssetsAvailable));
        WriteField("market_data_ger40_available", snapshot.MarketDataGer40Available.ToString().ToLowerInvariant());
        WriteField("market_data_xauusd_available", snapshot.MarketDataXauusdAvailable.ToString().ToLowerInvariant());
        WriteField("market_data_eurusd_available", snapshot.MarketDataEurusdAvailable.ToString().ToLowerInvariant());
        WriteField("market_data_quality_health", snapshot.MarketDataQualityHealth);
        WriteField("ger40_quote_mapping_status", snapshot.Ger40QuoteMappingStatus);
        WriteField("ger40_historical_data_status", snapshot.Ger40HistoricalDataStatus);
        WriteField("ger40_research_status", snapshot.Ger40ResearchStatus);
        WriteField("ger40_signal_agent_spec_status", snapshot.Ger40SignalAgentSpecStatus);
        WriteField("scalping_data_gap", snapshot.ScalpingDataGap);
        WriteField("scalping_robustness_expanded", snapshot.ScalpingRobustnessExpanded.ToString());
        WriteField("scalping_final_candidates", snapshot.ScalpingFinalCandidates.ToString());
        WriteField("scalping_rejected_after_expansion", snapshot.ScalpingRejectedAfterExpansion.ToString());
        WriteField("best_final_scalping_candidate", snapshot.BestFinalScalpingCandidate ?? "-");
        WriteField("scalping_monte_carlo_health", snapshot.ScalpingMonteCarloHealth);
        WriteField("scalping_parameter_sensitivity_health", snapshot.ScalpingParameterSensitivityHealth);
        WriteField("scalping_regime_validation_health", snapshot.ScalpingRegimeValidationHealth);
        WriteField("scalping_sensitivity_explainability_health", snapshot.ScalpingSensitivityExplainabilityHealth);
        WriteField("scalping_candidates_with_stable_corridor", snapshot.ScalpingCandidatesWithStableCorridor.ToString());
        WriteField("scalping_candidates_blocked_by_sensitivity", snapshot.ScalpingCandidatesBlockedBySensitivity.ToString());
        WriteField("best_scalping_parameter_corridor_candidate", snapshot.BestScalpingParameterCorridorCandidate ?? "-");
        WriteField("scalping_certification_health", snapshot.ScalpingCertificationHealth);
        WriteField("scalping_certified_candidates", snapshot.ScalpingCertifiedCandidates.ToString());
        WriteField("scalping_certification_failed", snapshot.ScalpingCertificationFailed.ToString());
        WriteField("best_certified_scalping_candidate", snapshot.BestCertifiedScalpingCandidate ?? "-");
        WriteField("scalping_human_review_packages_ready", snapshot.ScalpingHumanReviewPackagesReady.ToString());
        WriteMessages("domain_validation_warnings", snapshot.DomainValidationWarnings.Take(8).ToList());
        WriteField("top_goal", string.IsNullOrWhiteSpace(snapshot.TopGoal) ? "-" : snapshot.TopGoal);
        WriteMessages("active_goals", snapshot.ActiveGoals);
        WriteMessages("blocked_goals", snapshot.BlockedGoals);
        WriteMessages(
            "goal_progress_summary",
            snapshot.GoalProgressSummary
                .OrderByDescending(item => item.Value)
                .Take(8)
                .Select(item => $"{item.Key}: {item.Value:0.####}")
                .ToList());
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", snapshot.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", snapshot.LiveTradingEnabled.ToString().ToLowerInvariant());
        WriteField("research_only", "true");
        WriteField("JSON Report", DisplayPath(reportPath));
        WriteMessages("Top Blockers", snapshot.TopBlockers.Take(8).ToList());
        WriteMessages("Next Recommended Actions", snapshot.NextRecommendedActions.Take(8).ToList());
        WriteMessages("Warnings", snapshot.Warnings.Take(8).ToList());
    }

    private int ShowLegacyMasterStatus()
    {
        WriteHeader("Hermes Master Status");
        var storagePaths = BuildReadOnlyStoragePaths();
        var reportDirectory = Path.Combine(storagePaths.Root, "reports", "master_status");
        var reportPath = Path.Combine(reportDirectory, "master_status.json");
        var scheduleConfigPath = Path.Combine(_runtimeRoot, "config", "schedules.json");
        var nightlyConfigPath = Path.Combine(_runtimeRoot, "config", "nightly.research.json");

        var schedulerStatus = new HermesInternalScheduler(storagePaths, scheduleConfigPath).GetStatus();
        var supervisor = new HermesSupervisor(storagePaths, scheduleConfigPath);
        var supervisorState = supervisor.LoadState();
        var supervisorHeartbeat = supervisor.LoadHeartbeat();
        var supervisorProcess = new SupervisorProcessManager(storagePaths)
            .GetStatus(supervisorState, supervisorHeartbeat?.SupervisorId == supervisorState.SupervisorId ? supervisorHeartbeat : null);
        var nightlyState = new NightlyResearchService(storagePaths, nightlyConfigPath).LoadState();

        var runtimeHealthPath = Path.Combine(storagePaths.Root, "reports", "runtime_health.json");
        var resourceStatusPath = new ResourceGuard(storagePaths).StatusPath;
        var storagePlanPath = new StorageHygieneService(storagePaths).CleanupPlanPath;
        var cognitiveRoot = Path.Combine(storagePaths.Root, "cognitive_core");
        var strategyRoot = Path.Combine(storagePaths.Root, "strategy_research");
        var simulationRoot = Path.Combine(storagePaths.Root, "reports", "simulation");
        var botCandidatePath = Path.Combine(storagePaths.Root, "bot_candidates", "latest_bot_candidate_report.json");

        var runtimeHealth = TryLoadJson(runtimeHealthPath, out var runtimeHealthJson) ? runtimeHealthJson : default;
        var resourceStatus = TryLoadJson(resourceStatusPath, out var resourceStatusJson) ? resourceStatusJson : default;
        var storagePlan = TryLoadJson(storagePlanPath, out var storagePlanJson) ? storagePlanJson : default;
        var cognitiveStatus = TryLoadJson(Path.Combine(cognitiveRoot, "cognitive_status.json"), out var cognitiveStatusJson) ? cognitiveStatusJson : default;
        var domainStatus = TryLoadJson(Path.Combine(cognitiveRoot, "domain_status.json"), out var domainStatusJson) ? domainStatusJson : default;
        var planningStatus = TryLoadJson(Path.Combine(cognitiveRoot, "planning_status.json"), out var planningStatusJson) ? planningStatusJson : default;
        var autonomousLoop = TryLoadJson(Path.Combine(cognitiveRoot, "autonomous_loop_summary.json"), out var autonomousLoopJson) ? autonomousLoopJson : default;
        var outcomeStatus = TryLoadJson(Path.Combine(cognitiveRoot, "outcome_feedback_status.json"), out var outcomeStatusJson) ? outcomeStatusJson : default;
        var metaReview = TryLoadJson(Path.Combine(cognitiveRoot, "meta_review.json"), out var metaReviewJson) ? metaReviewJson : default;
        var learningStrategy = TryLoadJson(Path.Combine(cognitiveRoot, "learning_strategy.json"), out var learningStrategyJson) ? learningStrategyJson : default;
        var researchInsights = TryLoadJson(Path.Combine(strategyRoot, "research_insights.json"), out var researchInsightsJson) ? researchInsightsJson : default;
        var robustStrategies = TryLoadJson(Path.Combine(strategyRoot, "robust_strategies.json"), out var robustStrategiesJson) ? robustStrategiesJson : default;
        var overfitReport = TryLoadJson(Path.Combine(simulationRoot, "overfit_report.json"), out var overfitReportJson) ? overfitReportJson : default;
        var botCandidateReport = TryLoadJson(botCandidatePath, out var botCandidateJson) ? botCandidateJson : default;

        var activeDomains = CombineStringLists(
            GetStringArray(domainStatus, "active_domains", "activeDomains"),
            GetStringArray(planningStatus, "active_domains", "activeDomains"),
            GetStringArray(cognitiveStatus, "active_domains", "activeDomains"),
            nightlyState.ActiveDomains ?? []);
        if (activeDomains.Count == 0)
        {
            activeDomains = ["trading"];
        }

        var topBlockers = CombineStringLists(
            GetStringArray(planningStatus, "top_needs", "topNeeds"),
            GetStringArray(planningStatus, "warnings"),
            GetStringArray(researchInsights, "top_blockers", "topBlockers"),
            GetStringArray(researchInsights, "why_no_candidates", "whyNoCandidates"),
            GetStringArray(metaReview, "recurring_needs", "recurringNeeds"),
            GetStringArray(domainStatus, "weak_domains", "weakDomains").Select(item => $"weak_domain:{item}"))
            .Take(10)
            .ToList();

        var nextRecommendedActions = CombineStringLists(
            GetStringArray(planningStatus, "top_tasks", "topTasks"),
            GetStringArray(learningStrategy, "priority_task_types", "priorityTaskTypes"),
            GetStringArray(researchInsights, "recommended_next_experiments", "recommendedNextExperiments"),
            GetStringArray(researchInsights, "next_validation_recommendations", "nextValidationRecommendations"))
            .Take(10)
            .ToList();

        var learningStrategyName = FirstNonEmpty(
            GetString(learningStrategy, "current_strategy", "currentStrategy"),
            GetString(metaReview, "learning_strategy", "learningStrategy"),
            "unknown");
        var domainFocus = GetStringArray(learningStrategy, "domain_focus", "domainFocus");
        var currentFocus = domainFocus.Count > 0
            ? $"{learningStrategyName}: {string.Join(", ", domainFocus)}"
            : $"{learningStrategyName}: {string.Join(", ", activeDomains)}";

        var queuedTasks = FirstPositive(
            GetInt(planningStatus, "queued_research_items", "queuedResearchItems"),
            nightlyState.QueuedResearchItems ?? 0,
            GetInt(cognitiveStatus, "queue_items", "queueItems"),
            GetArrayCount(cognitiveStatus, "queue", "items"));

        var cleanupCandidates = GetArrayCount(storagePlan, "candidates");
        var noAutoTrading = schedulerStatus.NoAutoTrading
            && supervisorState.NoAutoTrading
            && nightlyState.NoAutoTrading
            && SafetyFlagTrue([runtimeHealth, resourceStatus, storagePlan, cognitiveStatus, planningStatus, autonomousLoop, outcomeStatus, metaReview, learningStrategy, researchInsights, botCandidateReport], "no_auto_trading", "noAutoTrading");
        var humanReviewRequired = schedulerStatus.HumanReviewRequired
            && supervisorState.HumanReviewRequired
            && nightlyState.HumanReviewRequired
            && SafetyFlagTrue([runtimeHealth, resourceStatus, storagePlan, cognitiveStatus, planningStatus, autonomousLoop, outcomeStatus, metaReview, learningStrategy, researchInsights, botCandidateReport], "human_review_required", "humanReviewRequired");

        var criticalReasons = new List<string>();
        var warningReasons = new List<string>();
        var requiredReportStates = new (string Name, JsonElement Root)[]
        {
            ("runtime_health", runtimeHealth),
            ("resource_status", resourceStatus),
            ("planning_status", planningStatus),
            ("autonomous_loop_summary", autonomousLoop),
            ("meta_review", metaReview),
            ("learning_strategy", learningStrategy)
        };

        warningReasons.AddRange(requiredReportStates
            .Where(reportState => reportState.Root.ValueKind != JsonValueKind.Object)
            .Select(reportState => $"report_missing:{reportState.Name}"));

        if (!noAutoTrading)
        {
            criticalReasons.Add("no_auto_trading_not_confirmed");
        }

        if (!humanReviewRequired)
        {
            criticalReasons.Add("human_review_required_not_confirmed");
        }

        if (JsonBool(resourceStatus, false, "should_stop", "shouldStop"))
        {
            criticalReasons.Add("resource_guard_should_stop");
        }

        if (JsonBool(resourceStatus, false, "should_pause", "shouldPause"))
        {
            warningReasons.Add("resource_guard_should_pause");
        }

        if (supervisorProcess.StalePid)
        {
            warningReasons.Add("supervisor_stale_pid");
        }

        if (!string.IsNullOrWhiteSpace(supervisorState.LastError))
        {
            warningReasons.Add($"supervisor_error:{supervisorState.LastError}");
        }

        if (!string.IsNullOrWhiteSpace(nightlyState.LastError))
        {
            warningReasons.Add($"nightly_error:{nightlyState.LastError}");
        }

        if (schedulerStatus.Warnings.Count > 0)
        {
            warningReasons.AddRange(schedulerStatus.Warnings.Select(warning => $"scheduler:{warning}"));
        }

        warningReasons.AddRange(schedulerStatus.Jobs
            .Where(job => job.FailureCount > 0 || job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            .Select(job => $"scheduled_job_issue:{job.JobId}:{job.Status}"));

        if (cleanupCandidates > 0)
        {
            warningReasons.Add($"cleanup_candidates:{cleanupCandidates}");
        }

        if (topBlockers.Count > 0)
        {
            warningReasons.AddRange(topBlockers.Take(5));
        }

        var overallStatus = criticalReasons.Count > 0
            ? "critical"
            : warningReasons.Count > 0
                ? "warning"
                : "ok";

        var robustCount = GetArrayCount(robustStrategies, "robust_strategies", "robustStrategies");
        var overfitCount = FirstPositive(
            GetInt(overfitReport, "overfit_suspected_strategies", "overfitSuspectedStrategies"),
            GetArrayCount(researchInsights, "overfit_suspected_strategies", "overfitSuspectedStrategies"));
        var highRiskCount = FirstPositive(
            GetInt(overfitReport, "high_risk_strategies", "highRiskStrategies"),
            GetArrayCount(researchInsights, "high_risk_strategies", "highRiskStrategies"));
        var demoBotCandidates = FirstPositive(
            GetInt(botCandidateReport, "demo_bot_candidate_count", "demoBotCandidateCount"),
            GetInt(researchInsights, "bot_candidate_count", "botCandidateCount"));
        var rejectedCandidates = FirstPositive(
            GetInt(botCandidateReport, "rejected_candidate_count", "rejectedCandidateCount"),
            GetInt(researchInsights, "rejected_candidate_count", "rejectedCandidateCount"));

        var report = new
        {
            report_version = "master_status_v1",
            updated_at_utc = DateTimeOffset.UtcNow,
            data_root = storagePaths.Root,
            overall_status = overallStatus,
            current_focus = currentFocus,
            active_domains = activeDomains,
            queued_tasks = queuedTasks,
            last_nightly_run = nightlyState.LastStartUtc?.ToString("O") ?? nightlyState.StartedAtUtc?.ToString("O"),
            last_autonomous_loop = GetString(autonomousLoop, "updated_at_utc", "updatedAtUtc"),
            last_meta_review = GetString(metaReview, "updated_at_utc", "updatedAtUtc"),
            learning_strategy = learningStrategyName,
            top_blockers = topBlockers,
            next_recommended_actions = nextRecommendedActions,
            runtime_health = new
            {
                report_path = runtimeHealthPath,
                state = GetString(runtimeHealth, "runtime_state", "runtimeState") ?? "unknown",
                timestamp_utc = GetString(runtimeHealth, "timestamp_utc", "timestampUtc"),
                safe_mode = GetBoolText(runtimeHealth, "safe_mode", "safeMode"),
                last_error = GetString(runtimeHealth, "last_error", "lastError")
            },
            scheduler = new
            {
                config_path = schedulerStatus.ConfigPath,
                state_path = schedulerStatus.StatePath,
                enabled_jobs = schedulerStatus.Jobs.Count(job => job.Enabled),
                active_jobs = schedulerStatus.Jobs.Count(job => job.CurrentlyRunning),
                failed_jobs = schedulerStatus.Jobs.Count(job => job.FailureCount > 0 || job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
                next_job = schedulerStatus.Jobs.FirstOrDefault(job => job.NextRunUtc is not null)?.JobId,
                warnings = schedulerStatus.Warnings
            },
            supervisor = new
            {
                state_path = supervisor.StatePath,
                heartbeat_path = supervisor.HeartbeatPath,
                running = supervisorProcess.Running,
                pid = supervisorProcess.Pid,
                status = supervisorState.Status,
                heartbeat_age_seconds = supervisorProcess.HeartbeatAgeSeconds,
                current_job = supervisorState.CurrentJobId,
                next_action = supervisorState.NextAction,
                last_error = supervisorState.LastError
            },
            nightly_beta3 = new
            {
                status = nightlyState.Status,
                last_start_utc = nightlyState.LastStartUtc ?? nightlyState.StartedAtUtc,
                next_scheduled_start_utc = nightlyState.NextScheduledStartUtc,
                iterations = nightlyState.IterationsCompleted,
                work_performed = nightlyState.WorkPerformed,
                next_action = nightlyState.NextAction,
                cognitive_jobs_enabled = nightlyState.CognitiveJobsEnabled,
                queued_research_items = nightlyState.QueuedResearchItems,
                last_cognitive_error = nightlyState.LastCognitiveError
            },
            cognitive_core = new
            {
                status_path = Path.Combine(cognitiveRoot, "cognitive_status.json"),
                sources = GetInt(cognitiveStatus, "source_count", "sourceCount"),
                knowledge_items = GetInt(cognitiveStatus, "knowledge_item_count", "knowledgeItemCount"),
                queue_items = GetInt(cognitiveStatus, "queue_item_count", "queueItemCount"),
                insights = GetInt(cognitiveStatus, "insight_count", "insightCount"),
                active_domains = activeDomains
            },
            planning_engine = new
            {
                status_path = Path.Combine(cognitiveRoot, "planning_status.json"),
                needs_detected = GetInt(planningStatus, "needs_detected", "needsDetected"),
                planned_tasks = GetInt(planningStatus, "planned_tasks", "plannedTasks"),
                queued_research_items = queuedTasks,
                next_action = GetString(planningStatus, "next_action", "nextAction")
            },
            autonomous_loop = new
            {
                summary_path = Path.Combine(cognitiveRoot, "autonomous_loop_summary.json"),
                status = GetString(autonomousLoop, "status"),
                iterations = GetInt(autonomousLoop, "iterations_completed", "iterationsCompleted"),
                average_learning_value = GetDouble(autonomousLoop, "average_learning_value", "averageLearningValue"),
                next_action = GetString(autonomousLoop, "next_action", "nextAction")
            },
            outcome_feedback = new
            {
                status_path = Path.Combine(cognitiveRoot, "outcome_feedback_status.json"),
                total_outcomes = GetInt(outcomeStatus, "total_outcomes", "totalOutcomes"),
                last_outcome_utc = GetString(outcomeStatus, "last_outcome_utc", "lastOutcomeUtc"),
                latest_recommendations = GetStringArray(outcomeStatus, "latest_recommendations", "latestRecommendations").Take(10).ToList()
            },
            meta_review = new
            {
                report_path = Path.Combine(cognitiveRoot, "meta_review.json"),
                status = GetString(metaReview, "status") ?? "unknown",
                updated_at_utc = GetString(metaReview, "updated_at_utc", "updatedAtUtc"),
                observations = GetArrayCount(metaReview, "observations"),
                recurring_needs = GetStringArray(metaReview, "recurring_needs", "recurringNeeds").Take(10).ToList()
            },
            resource_status = new
            {
                report_path = resourceStatusPath,
                action = GetString(resourceStatus, "action") ?? "unknown",
                cpu_usage_percent = GetDouble(resourceStatus, "cpu_usage_percent", "cpuUsagePercent"),
                memory_usage_percent = GetDouble(resourceStatus, "memory_usage_percent", "memoryUsagePercent"),
                free_disk_percent = GetDouble(resourceStatus, "free_disk_percent", "freeDiskPercent"),
                should_pause = JsonBool(resourceStatus, false, "should_pause", "shouldPause"),
                should_stop = JsonBool(resourceStatus, false, "should_stop", "shouldStop"),
                warnings = GetStringArray(resourceStatus, "warnings")
            },
            storage_status = new
            {
                cleanup_plan_path = storagePlanPath,
                storage_root = GetString(storagePlan, "storage_root", "storageRoot") ?? storagePaths.Root,
                cleanup_candidates = cleanupCandidates,
                safe_to_apply = GetBoolText(storagePlan, "safe_to_apply", "safeToApply"),
                estimated_bytes_to_free = GetString(storagePlan, "estimated_bytes_to_free", "estimatedBytesToFree")
            },
            trading_domain = new
            {
                research_insights_path = Path.Combine(strategyRoot, "research_insights.json"),
                robust_strategies = robustCount,
                overfit_suspected = overfitCount,
                high_risk_strategies = highRiskCount,
                demo_bot_candidates = demoBotCandidates,
                rejected_candidates = rejectedCandidates,
                next_validation_recommendations = GetStringArray(researchInsights, "next_validation_recommendations", "nextValidationRecommendations").Take(8).ToList()
            },
            status_reasons = new
            {
                critical = criticalReasons,
                warnings = warningReasons.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList()
            },
            no_auto_trading = noAutoTrading,
            human_review_required = humanReviewRequired
        };

        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));

        WriteField("overall_status", overallStatus);
        WriteField("current_focus", currentFocus);
        WriteField("active_domains", string.Join(", ", activeDomains));
        WriteField("queued_tasks", queuedTasks.ToString());
        WriteField("last_nightly_run", report.last_nightly_run ?? "-");
        WriteField("last_autonomous_loop", report.last_autonomous_loop ?? "-");
        WriteField("last_meta_review", report.last_meta_review ?? "-");
        WriteField("learning_strategy", learningStrategyName);
        WriteField("supervisor_running", supervisorProcess.Running.ToString().ToLowerInvariant());
        WriteField("scheduler_enabled", schedulerStatus.Jobs.Count(job => job.Enabled).ToString());
        WriteField("resource_action", report.resource_status.action);
        WriteField("storage_cleanup", cleanupCandidates.ToString());
        WriteField("robust_strategies", robustCount.ToString());
        WriteField("demo_bot_candidates", demoBotCandidates.ToString());
        WriteField("no_auto_trading", noAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", humanReviewRequired.ToString().ToLowerInvariant());
        WriteField("JSON Report", DisplayPath(reportPath));
        WriteMessages("Top Blockers", topBlockers.Take(8).ToList());
        WriteMessages("Next Recommended Actions", nextRecommendedActions.Take(8).ToList());
        WriteMessages("Warnings", warningReasons.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList());

        Console.WriteLine();
        WriteSafety();
        return criticalReasons.Count > 0 ? 1 : 0;
    }

    private int StartReadOnlyBridge()
    {
        var url = ReadOption(_args, "--url") ?? "http://127.0.0.1:8787/";
        if (!url.EndsWith("/", StringComparison.Ordinal))
        {
            url += "/";
        }

        var storagePaths = BuildReadOnlyStoragePaths();
        var bridge = new HermesReadOnlyBridge(storagePaths, _runtimeRoot);
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            bridge.RunAsync(url, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (HttpListenerException ex)
        {
            WriteError($"Read-only Bridge konnte nicht starten: {ex.Message}");
            Console.WriteLine("Hinweis: Nutze einen freien localhost-Port, z. B. --url http://127.0.0.1:8788/");
            WriteSafety();
            return 1;
        }

        Console.WriteLine("Read-only Bridge wurde beendet.");
        WriteSafety();
        return 0;
    }

    private int ShowHealth()
    {
        WriteHeader("Hermes Runtime Health");
        var path = Path.Combine(_dataRoot, "reports", "runtime_health.json");
        if (!TryLoadJson(path, out var root))
        {
            WriteWarning($"RuntimeHealth nicht gefunden: {path}");
            WriteSafety();
            return 0;
        }

        WriteField("Runtime State", GetString(root, "runtime_state", "runtimeState"));
        WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
        WriteField("Safe Mode", GetBoolText(root, "safe_mode", "safeMode"));
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));
        WriteField("Free Disk", $"{GetDouble(root, "free_disk_gb", "freeDiskGb"):0.##} GB");
        WriteField("Pending Jobs", GetInt(root, "pending_jobs", "pendingJobs").ToString());
        WriteField("Running Jobs", GetInt(root, "running_jobs", "runningJobs").ToString());
        WriteField("Failed Jobs", GetInt(root, "failed_jobs", "failedJobs").ToString());
        WriteField("Quarantined Jobs", GetInt(root, "quarantined_jobs", "quarantinedJobs").ToString());
        WriteField("Active Setup Watches", GetInt(root, "active_setup_watches", "activeSetupWatches").ToString());
        WriteField("Last Snapshot", GetString(root, "last_snapshot_id", "lastSnapshotId"));
        WriteField("Last Error", GetString(root, "last_error", "lastError") ?? "-");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSetupWatch()
    {
        WriteHeader("Hermes Setup Watch");
        var path = Path.Combine(_dataRoot, "setup_watch", "setup_watch.json");
        if (!TryLoadJson(path, out var root) || root.ValueKind != JsonValueKind.Array)
        {
            WriteWarning($"Setup-Watch-Datei nicht gefunden oder nicht lesbar: {path}");
            WriteSafety();
            return 0;
        }

        var count = 0;
        foreach (var item in root.EnumerateArray())
        {
            count++;
            WriteSubHeader($"{GetString(item, "symbol") ?? "UNKNOWN"} - {GetString(item, "bias") ?? "unknown"}");
            WriteField("Status", GetString(item, "status"));
            WriteField("Confidence", $"{GetDouble(item, "confidence") * 100:0}%");
            WriteField("Entry Zone", GetString(item, "entry_zone", "entryZone"));
            WriteField("Stop-Loss", GetString(item, "suggested_stop_loss", "suggestedStopLoss"));
            WriteField("Target", GetString(item, "suggested_target", "suggestedTarget"));
            WriteField("Invalidation", GetString(item, "invalidation_level", "invalidationLevel"));
            WriteField("Trigger", GetString(item, "trigger_condition", "triggerCondition"));
            WriteField("Time Window", $"{GetInt(item, "time_window_minutes", "timeWindowMinutes")} min");
            WriteField("Notes", GetString(item, "notes"));
            Console.WriteLine();
        }

        if (count == 0)
        {
            Console.WriteLine("Keine Setup-Watches vorhanden.");
        }

        WriteSafety();
        return 0;
    }

    private int ShowEvents()
    {
        var subCommand = CommandAt(_args, 1);
        if (subCommand is not ("recent" or ""))
        {
            return UnknownCommand($"events {subCommand}");
        }

        WriteHeader("Hermes Recent Runtime Events");
        var limit = ReadLimit(_args, 12);
        var files = FindEventFiles().ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Runtime-Event-Dateien gefunden.");
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();

        var lines = File.ReadLines(file)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(limit)
            .ToList();

        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var timestamp = GetString(root, "timestamp_utc", "timestampUtc") ?? "-";
                var eventType = GetString(root, "event_type", "eventType") ?? "UnknownEvent";
                var severity = GetString(root, "severity") ?? "Info";
                var source = GetString(root, "source") ?? "-";
                var message = TryGetProperty(root, out var payload, "payload")
                    ? GetString(payload, "message") ?? "-"
                    : "-";

                WriteEvent(timestamp, severity, eventType, source, message);
            }
            catch (JsonException)
            {
                WriteWarning("Ungueltige JSONL-Zeile uebersprungen.");
            }
        }

        WriteSafety();
        return 0;
    }

    private int ShowJobs()
    {
        WriteHeader("Hermes Queue Jobs");
        var jobsRoot = Path.Combine(_dataRoot, "jobs");
        var states = new[] { "pending", "running", "completed", "failed", "quarantined" };

        foreach (var state in states)
        {
            var directory = Path.Combine(jobsRoot, state);
            var jobFiles = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.job.json").OrderBy(path => path).ToList()
                : [];

            WriteField(CultureTitle(state), jobFiles.Count.ToString());
        }

        Console.WriteLine();
        var latestJobs = Directory.Exists(jobsRoot)
            ? Directory.EnumerateFiles(jobsRoot, "*.job.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(8)
                .ToList()
            : [];

        if (latestJobs.Count == 0)
        {
            Console.WriteLine("Keine Job-Manifeste vorhanden.");
        }

        foreach (var jobFile in latestJobs)
        {
            if (!TryLoadJson(jobFile, out var root))
            {
                continue;
            }

            WriteSubHeader(GetString(root, "job_id", "jobId") ?? Path.GetFileName(jobFile));
            WriteField("Type", GetString(root, "job_type", "jobType"));
            WriteField("Status", GetString(root, "status"));
            WriteField("Priority", GetInt(root, "priority").ToString());
            WriteField("Requested By", GetString(root, "requested_by", "requestedBy"));
            WriteField("Path", DisplayPath(jobFile));
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowStorage()
    {
        WriteHeader("Hermes Storage");
        Console.WriteLine($"Runtime Root: {_runtimeRoot}");
        Console.WriteLine($"Data Root:    {_dataRoot}");
        Console.WriteLine();

        var healthPath = Path.Combine(_dataRoot, "reports", "runtime_health.json");
        if (TryLoadJson(healthPath, out var health))
        {
            WriteField("Free Disk", $"{GetDouble(health, "free_disk_gb", "freeDiskGb"):0.##} GB");
        }

        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (TryLoadJson(profilePath, out var profile))
        {
            WriteField("Minimum Free Disk", $"{GetInt(profile, "minimum_free_disk_mb", "minimumFreeDiskMb")} MB");
            WriteField("Profile", GetString(profile, "profile_name", "profileName"));
        }

        Console.WriteLine();
        foreach (var name in new[] { "cache", "events", "snapshots", "replays", "exports", "market_data", "jobs", "reports", "setup_watch", "archive" })
        {
            var directory = Path.Combine(_dataRoot, name);
            var size = Directory.Exists(directory) ? DirectorySize(directory) : 0;
            WriteField(name, FormatBytes(size));
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowFeatures()
    {
        WriteHeader("Hermes Feature Exports");
        var limit = ReadLimit(_args, 8);
        var files = FindExportFiles("features").ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Feature-Export-Dateien gefunden.");
            WriteSafety();
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();
        foreach (var line in ReadRecentJsonlLines(file, limit))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                WriteWarning("Ungueltige Feature-JSONL-Zeile uebersprungen.");
                continue;
            }

            WriteSubHeader($"{GetString(root, "symbol") ?? "UNKNOWN"} {GetString(root, "timeframe") ?? "-"}");
            WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
            if (TryGetProperty(root, out _, "simple_return", "simpleReturn"))
            {
                WriteField("Schema", "generated_from_market_data_v1");
                WriteField("Close", $"{GetDouble(root, "close"):0.#####}");
                WriteField("Simple Return", $"{GetDouble(root, "simple_return", "simpleReturn"):0.########}");
                WriteField("Candle Range", $"{GetDouble(root, "candle_range", "candleRange"):0.#####}");
                WriteField("Body Size", $"{GetDouble(root, "body_size", "bodySize"):0.#####}");
                WriteField("Direction", GetString(root, "direction"));
                WriteField("Mock Session", GetString(root, "mock_session", "mockSession"));
                WriteField("Mock Regime", GetString(root, "mock_regime", "mockRegime"));
                WriteField("Mock Signal Score", $"{GetDouble(root, "mock_signal_score", "mockSignalScore"):0.####}");
            }
            else
            {
                WriteField("Schema", "feature_export_demo_v1");
                WriteField("Session", GetString(root, "session"));
                WriteField("H4 Regime", GetString(root, "h4_regime", "h4Regime"));
                WriteField("H1 Bias", GetString(root, "h1_bias", "h1Bias"));
                WriteField("M15 Setup", GetString(root, "m15_setup", "m15Setup"));
                WriteField("M5 Trigger", GetString(root, "m5_trigger", "m5Trigger"));
                WriteField("ADX", $"{GetDouble(root, "adx"):0.##}");
                WriteField("ATR", $"{GetDouble(root, "atr"):0.#####}");
                WriteField("RSI", $"{GetDouble(root, "rsi"):0.##}");
                WriteField("Structure", GetString(root, "structure_state", "structureState"));
                WriteField("Pattern", GetString(root, "pattern_candidate", "patternCandidate"));
                WriteField("Signal Score", $"{GetDouble(root, "signal_score", "signalScore"):0.##}");
                WriteField("Spread", $"{GetDouble(root, "spread"):0.#####}");
            }

            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int GenerateFeatures()
    {
        WriteHeader("Hermes Feature Generation");
        var storagePaths = BuildStoragePaths();

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var service = new FeatureGenerationService(storagePaths, eventBus, CliVersion);
        var result = service.GenerateFromMarketData();
        eventStore.Flush();

        WriteField("Generation ID", result.Job.GenerationId);
        WriteField("Source", DisplayPath(result.Job.SourceRoot));
        WriteField("Output", DisplayPath(result.OutputPath));
        WriteField("Candle Rows", result.CandleCount.ToString());
        WriteField("Feature Rows", result.FeatureCount.ToString());
        WriteField("Symbols Processed", string.Join(", ", result.SymbolsProcessed));
        WriteField("Symbols", string.Join(", ", result.Job.Symbols));
        WriteField("Timeframes", string.Join(", ", result.Job.Timeframes));
        Console.WriteLine();

        if (result.FeatureCount == 0)
        {
            WriteWarning($"Keine Features erzeugt. Pruefe lokale Candle-Daten unter {DisplayPath(Path.Combine(storagePaths.Root, "market_data", "candles"))}.");
        }

        WriteSafety();
        return 0;
    }

    private int RunNightlyResearch()
    {
        WriteHeader("Hermes Nightly Research");
        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var schedule = new ResearchJobScheduleStub();
        var job = schedule.CreateDemoNightlyRun("hermes_cli");
        var coordinator = new ResearchPipelineCoordinator(storagePaths, eventBus, CliVersion);
        var report = coordinator.RunNightlyResearch(job);
        eventStore.Flush();

        WriteResearchReport(report);
        WriteSafety();
        return report.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private int ShowResearchStatus()
    {
        WriteHeader("Hermes Research Status");
        if (!TryLoadLatestResearchReport(out var latestPath, out var root))
        {
            WriteWarning("Kein Nightly-/Research-Report gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteResearchSummaryFields(root, detailed: false);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowResearchReport()
    {
        WriteHeader("Hermes Research Summary Report");
        if (!TryLoadLatestResearchReport(out var latestPath, out var root))
        {
            WriteWarning("Kein ResearchSummaryReport gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteResearchSummaryFields(root, detailed: true);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int RunBetaLearning()
    {
        WriteHeader("Hermes Trading Learning Beta 1");
        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var pipeline = new TradingLearningBetaPipeline(storagePaths, eventBus, CliVersion);
        var report = pipeline.Run();
        eventStore.Flush();

        WriteBetaReport(report);
        WriteSafety();
        return report.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private int ShowBetaStatus()
    {
        WriteHeader("Hermes Trading Learning Beta Status");
        if (!TryLoadLatestBetaReport(out var latestPath, out var root))
        {
            WriteWarning("Kein Trading Learning Beta Report gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteBetaReportFields(root, detailed: true);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int RunNightlyBeta3()
    {
        WriteHeader("Hermes Nightly Robust Research Beta 3");
        var storagePaths = BuildStoragePaths();
        var configPath = Path.Combine(_runtimeRoot, "config", "nightly.research.json");
        var nightly = new NightlyResearchService(storagePaths, configPath);
        var config = nightly.LoadConfig();
        var now = DateTimeOffset.Now;
        var maxRuntimeHours = ReadDoubleOption(_args, "--max-runtime-hours", config.MaxRuntimeHours, min: 0.01, max: 24);
        var sleepSeconds = ReadIntOption(
            _args,
            "--sleep-seconds",
            fallback: config.SleepSecondsBetweenIterations,
            min: 0,
            max: 3600);
        var maxIdleIterations = ReadIntOption(
            _args,
            "--max-idle-iterations",
            fallback: config.MaxIdleIterations,
            min: 1,
            max: 1000);
        var maxQualityCandidates = ReadIntOption(
            _args,
            "--max-quality-candidates",
            fallback: 64,
            min: 1,
            max: 500);

        WriteField("Config", DisplayPath(configPath));
        WriteField("Allowed Window", $"{config.StartHour:00}:00 -> {config.EndHour:00}:00");
        WriteField("Current Local Time", now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        WriteField("Max Quality Candidates", maxQualityCandidates.ToString());
        var schedulerStatus = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetStatus();
        var cognitiveJobsEnabled = AreCognitiveJobsEnabled(schedulerStatus);
        var cognitiveNightly = new CognitiveNightlyService(storagePaths);
        var cognitiveSummary = cognitiveNightly.LoadSummary();
        var autonomousLoop = new AutonomousLearningLoop(storagePaths, Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));

        if (!config.IsInAllowedWindow(now))
        {
            var existing = nightly.LoadState();
            var state = nightly.WriteState(WithCognitiveState(existing with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Status = "outside_nightly_window",
                NextAction = "wait_for_23_00_to_05_00_window",
                NextScheduledStartUtc = NightlyResearchService.CalculateNextScheduledStart(config, now).ToUniversalTime(),
                CurrentlyRunning = false,
                ProcessId = null,
                StopRequestedAtUtc = null
            }, cognitiveJobsEnabled, cognitiveSummary, cognitiveNightly.SummaryPath));
            WriteField("Status", state.Status);
            WriteField("Nightly State", DisplayPath(nightly.StatePath));
            TryWriteMasterStatusSnapshot(storagePaths);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var deadlineUtc = startedAtUtc.AddHours(maxRuntimeHours);
        var runId = $"nightly_beta3_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var memoryService = new ResearchMemoryIndexService(storagePaths);
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var targetToUtc = new DateTimeOffset(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var targetFromUtc = targetToUtc.AddYears(-1);
        var job = new LongRunResearchJob(
            JobId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            RequestedHours: maxRuntimeHours,
            RequestedBy: "hermes_nightly_beta3",
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var iterations = 0;
        var idleIterations = 0;
        var workPerformed = 0;
        var status = "running";
        var nextAction = "start_iteration";
        string? lastCheckpoint = null;
        string? lastError = null;
        DateTimeOffset? stopRequestedAtUtc = null;
        var cognitiveStepCompleted = false;

        nightly.ClearStopRequest();
        nightly.WriteState(WithCognitiveState(
            nightly.CreateRunState(runId, startedAtUtc, deadlineUtc, status, nextAction),
            cognitiveJobsEnabled,
            cognitiveSummary,
            cognitiveNightly.SummaryPath));

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        while (DateTimeOffset.UtcNow < deadlineUtc)
        {
            if (nightly.IsStopRequested())
            {
                stopRequestedAtUtc = nightly.StopRequestedAtUtc();
                status = "stopped_by_stop_request";
                nextAction = "safe_stop_requested";
                break;
            }

            if (!config.IsInAllowedWindow(DateTimeOffset.Now))
            {
                status = "stopped_outside_nightly_window";
                nextAction = "wait_for_next_window";
                break;
            }

            var resources = new ResourceGuard(storagePaths).Check();
            var storageHygiene = new StorageHygieneService(storagePaths);
            var cleanupPlan = storageHygiene.LoadPlan() ?? storageHygiene.BuildPlan();
            if (resources.ShouldStop)
            {
                status = "stopped_resource_guard";
                nextAction = "review_resource_status";
                break;
            }

            if (resources.ShouldPause)
            {
                status = "paused_resource_guard";
                nextAction = "sleep_then_recheck_resources";
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                continue;
            }

            try
            {
                var loopMinutes = Math.Max(0.01, Math.Min(10, (deadlineUtc - DateTimeOffset.UtcNow).TotalMinutes));
                var loopSummary = autonomousLoop.Run(maxIterations: 1, maxMinutes: loopMinutes);
                workPerformed += loopSummary.WorkPerformed;

                WriteSubHeader("Autonomous Learning Loop Step");
                WriteField("loop_status", loopSummary.Status);
                WriteField("loop_iterations", loopSummary.IterationsCompleted.ToString());
                WriteField("loop_work_performed", loopSummary.WorkPerformed.ToString());
                WriteField("loop_idle_iterations", loopSummary.IdleIterations.ToString());
                WriteField("loop_next_action", loopSummary.NextAction);
                WriteField("loop_summary", DisplayPath(autonomousLoop.SummaryPath));
                WriteField("loop_log", DisplayPath(autonomousLoop.LogPath));
                if (loopSummary.LastIteration is not null)
                {
                    WriteField("needs_detected", loopSummary.LastIteration.NeedsDetected.ToString());
                    WriteField("planned_tasks", loopSummary.LastIteration.TasksPlanned.ToString());
                    WriteField("executed_planned_tasks", loopSummary.LastIteration.TasksExecuted.ToString());
                    WriteField("evaluated_outcomes", loopSummary.LastIteration.OutcomesEvaluated.ToString());
                    WriteField("avg_learning", $"{loopSummary.LastIteration.AverageOutcomeLearningValue:0.####}");
                    WriteMessages("feedback_changes", loopSummary.LastIteration.FeedbackChanges.Take(8).ToList());
                }
                Console.WriteLine();

                if (!cognitiveStepCompleted)
                {
                    cognitiveSummary = cognitiveNightly.Run(maxQueueItems: 20);
                    cognitiveStepCompleted = true;
                    var cognitiveWorkUnits = cognitiveSummary.QueueItemsProcessed
                        + cognitiveSummary.HypothesesGenerated
                        + cognitiveSummary.InsightsGenerated;
                    workPerformed += cognitiveWorkUnits;

                    WriteSubHeader("Cognitive Nightly Step");
                    WriteField("sources_scanned", cognitiveSummary.SourcesScanned.ToString());
                    WriteField("knowledge_items", cognitiveSummary.KnowledgeItems.ToString());
                    WriteField("queue_items_processed", cognitiveSummary.QueueItemsProcessed.ToString());
                    WriteField("hypotheses_generated", cognitiveSummary.HypothesesGenerated.ToString());
                    WriteField("insights_generated", cognitiveSummary.InsightsGenerated.ToString());
                    WriteField("summary", DisplayPath(cognitiveNightly.SummaryPath));
                    WriteMessages("Warnings", cognitiveSummary.Warnings);
                    Console.WriteLine();
                }

                var iteration = RunResearchAutopilotIteration(
                    storagePaths,
                    memoryService,
                    configLoad,
                    authContext,
                    eventBus,
                    job,
                    iterations + 1,
                    targetFromUtc,
                    targetToUtc,
                    maxDownloads: Math.Min(9, config.AllowedSymbols.Count * config.AllowedTimeframes.Count),
                    maxRequests: 500,
                    inheritedWarnings: [],
                    runQualityGates: true,
                    maxQualityCandidates: maxQualityCandidates);
                eventStore.Flush();

                iterations++;
                if (iteration.WorkPerformed)
                {
                    idleIterations = 0;
                    workPerformed += iteration.WorkUnits;
                }
                else
                {
                    idleIterations++;
                }

                nextAction = iteration.NextAction;
                lastCheckpoint = memoryService.WriteCheckpoint(
                    job,
                    iterations,
                    iteration.Status,
                    $"nightly_beta3 work_performed={iteration.WorkUnits}; cognitive_jobs_enabled={cognitiveJobsEnabled.ToString().ToLowerInvariant()}; idle_iterations={idleIterations}; cleanup_candidates={cleanupPlan.Candidates.Count}",
                    iteration.Index,
                    betaRunId: iteration.BetaReport?.RunId);

                nightly.WriteState(WithCognitiveState(new NightlyResearchState(
                    StateVersion: "nightly_research_state_v1",
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    Status: "running",
                    RunId: runId,
                    StartedAtUtc: startedAtUtc,
                    DeadlineUtc: deadlineUtc,
                    IterationsCompleted: iterations,
                    IdleIterations: idleIterations,
                    WorkPerformed: workPerformed,
                    NextAction: nextAction,
                    LastCheckpointPath: lastCheckpoint,
                    LastAutopilotReportPath: null,
                    LastError: null,
                    NoAutoTrading: true,
                    HumanReviewRequired: true,
                    NextScheduledStartUtc: NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime(),
                    LastStartUtc: startedAtUtc,
                    LastStopUtc: null,
                    CurrentlyRunning: true,
                    RuntimeDurationMinutes: Math.Round((DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes, 2),
                    ProcessId: Environment.ProcessId,
                    StopRequestedAtUtc: null),
                    cognitiveJobsEnabled,
                    cognitiveSummary,
                    cognitiveNightly.SummaryPath));

                WriteSubHeader($"Nightly Iteration {iterations}");
                WriteField("work_performed", iteration.WorkUnits.ToString());
                WriteField("idle_iterations", idleIterations.ToString());
                WriteField("resource_action", resources.Action);
                WriteField("cleanup_candidates", cleanupPlan.Candidates.Count.ToString());
                WriteField("checkpoint", DisplayPath(lastCheckpoint));
                Console.WriteLine();

                if (idleIterations >= maxIdleIterations)
                {
                    status = "stopped_max_idle_iterations";
                    nextAction = "wait_for_new_data_or_new_strategy_space";
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
            {
                lastError = ex.Message;
                status = "stopped_critical_error";
                nextAction = "review_last_error";
                break;
            }

            var remaining = deadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                status = "completed_deadline_reached";
                nextAction = "nightly_window_complete";
                break;
            }

            if (SleepUntilNextIterationOrStop(nightly, TimeSpan.FromSeconds(Math.Min(sleepSeconds, Math.Max(0, remaining.TotalSeconds)))))
            {
                stopRequestedAtUtc = nightly.StopRequestedAtUtc();
                status = "stopped_by_stop_request";
                nextAction = "safe_stop_requested";
                break;
            }
        }

        if (status == "running")
        {
            status = "completed_deadline_reached";
            nextAction = "nightly_window_complete";
        }

        lastCheckpoint ??= memoryService.WriteCheckpoint(
            job,
            iteration: iterations + 1,
            status: status,
            message: $"nightly_beta3 final checkpoint; status={status}; work_performed={workPerformed}; idle_iterations={idleIterations}",
            memoryService.UpdateIndex(),
            betaRunId: null);

        var finalState = nightly.WriteState(WithCognitiveState(new NightlyResearchState(
            StateVersion: "nightly_research_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: iterations,
            IdleIterations: idleIterations,
            WorkPerformed: workPerformed,
            NextAction: nextAction,
            LastCheckpointPath: lastCheckpoint,
            LastAutopilotReportPath: null,
            LastError: lastError,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            NextScheduledStartUtc: NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime(),
            LastStartUtc: startedAtUtc,
            LastStopUtc: DateTimeOffset.UtcNow,
            CurrentlyRunning: false,
            RuntimeDurationMinutes: Math.Round((DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes, 2),
            ProcessId: null,
            StopRequestedAtUtc: stopRequestedAtUtc),
            cognitiveJobsEnabled,
            cognitiveSummary,
            cognitiveNightly.SummaryPath));

        nightly.ClearStopRequest();

        WriteField("Nightly State", DisplayPath(nightly.StatePath));
        WriteNightlyState(finalState);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private static bool SleepUntilNextIterationOrStop(NightlyResearchService nightly, TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow.Add(duration);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (nightly.IsStopRequested())
            {
                return true;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Min(5, Math.Max(0.1, remaining.TotalSeconds))));
        }

        return nightly.IsStopRequested();
    }

    private int ShowNightlyStatus()
    {
        WriteHeader("Hermes Nightly Beta 3 Status");
        var storagePaths = BuildStoragePaths();
        var service = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        var config = service.LoadConfig();
        var state = service.LoadState();
        var displayState = state with
        {
            NextScheduledStartUtc = NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime()
        };

        WriteField("State", DisplayPath(service.StatePath));
        WriteNightlyState(displayState);
        WriteCognitiveOperationalStatus(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RequestNightlyStop()
    {
        WriteHeader("Hermes Nightly Beta 3 Stop Request");
        var storagePaths = BuildStoragePaths();
        var service = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        var state = service.RequestStop();
        WriteField("Stop Request", DisplayPath(service.StopRequestPath));
        WriteField("State", DisplayPath(service.StatePath));
        WriteNightlyState(state);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSchedulerStatus()
    {
        WriteHeader("Hermes Internal Scheduler Status");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var status = scheduler.GetStatus();

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("State", DisplayPath(status.StatePath));
        WriteField("Check Interval", $"{status.CheckIntervalSeconds} s");
        WriteField("Enabled Jobs", status.Jobs.Count(job => job.Enabled).ToString());
        WriteField("Next Action", status.Jobs.Count == 0 ? "no_jobs_configured" : $"next_job={status.Jobs.FirstOrDefault(job => job.NextRunUtc is not null)?.JobId ?? "-"}");
        WriteMessages("Warnings", status.Warnings);
        WriteCognitiveOperationalStatus(storagePaths, status);
        foreach (var job in status.Jobs.Take(8))
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWorkloadScheduleStatus()
    {
        WriteHeader("Hermes Research Workload Schedule");
        var storagePaths = BuildStoragePaths();
        var service = new SchedulerWorkloadPlanService(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var report = service.Build();

        WriteField("Report JSON", DisplayPath(service.ReportPath));
        WriteField("Report Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Current Window", report.CurrentTimeWindow);
        WriteField("Day Jobs Enabled", report.DayJobsEnabled.ToString().ToLowerInvariant());
        WriteField("Night Heavy Jobs Enabled", report.NightHeavyJobsEnabled.ToString().ToLowerInvariant());
        WriteField("Learning Window Active", report.LearningWindowActive.ToString().ToLowerInvariant());
        WriteField("Human Review Window Active", report.HumanReviewWindowActive.ToString().ToLowerInvariant());
        WriteField("Research Insights Status", report.ResearchInsightsStatus);
        WriteField("Nightly Status", report.NightlyStatus);
        WriteField("Recommended Action", report.RecommendedAction);
        WriteMessages("Stale Running Jobs", report.StaleRunningJobs);
        foreach (var job in report.HeavyJobsNextRun.Take(10))
        {
            WriteField($"{job.JobId} ({job.JobType})", $"{job.Status}; next_run={(job.NextRunUtc?.ToString("O") ?? "-")}; heavy={job.Heavy.ToString().ToLowerInvariant()}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSchedulerJobs()
    {
        WriteHeader("Hermes Scheduled Jobs");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var status = scheduler.GetStatus();

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("State", DisplayPath(status.StatePath));
        foreach (var job in status.Jobs)
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTimeControlStatus()
    {
        WriteHeader("Hermes Zeitsteuerung");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var status = scheduler.GetTimeControlStatus();

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("Zeitzone", status.TimeZone);
        WriteField("Status", status.StatusLabel);
        WriteField("Arbeitsfenster", $"{status.WorkWindow.Start} - {status.WorkWindow.End} ({(status.WorkWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Nightly", $"{status.NightlyWindow.Start} - {status.NightlyWindow.End} ({(status.NightlyWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Lernfenster", $"{status.LearningWindow.Start} - {status.LearningWindow.End} ({(status.LearningWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Human-Review", $"{status.HumanReviewWindow.Start} - {status.HumanReviewWindow.End} ({(status.HumanReviewWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Lokale Zeit", status.CurrentLocal.ToString("O"));
        WriteField("UTC", status.CurrentUtc.ToString("O"));
        WriteField("Aktive Wochentage", string.Join(", ", status.ActiveWeekdays));
        WriteField("Inaktive Wochentage", string.Join(", ", status.InactiveWeekdays));
        WriteField("Im Arbeitsfenster", status.InWorkWindow.ToString().ToLowerInvariant());
        WriteMessages("Warnings", status.Warnings);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStartupStatus()
    {
        WriteHeader("Hermes Startup Orchestrator");

        var scheduler = new HermesInternalScheduler(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var schedulerStatus = scheduler.GetStatus();
        var timeControl = scheduler.GetTimeControlStatus();
        var bridgeUrl = "http://127.0.0.1:8787/bridge/health";
        var bridgeRunning = IsBridgeHealthy(bridgeUrl, out var bridgeVersion, out var bridgeWarnings);

        WriteField("Read-only Bridge", bridgeRunning ? "aktiv" : "nicht aktiv");
        WriteField("Bridge Version", bridgeVersion ?? "-");
        WriteField("Bridge Health", bridgeRunning ? bridgeUrl : "nicht erreichbar");
        WriteField("Scheduler Status", schedulerStatus.Warnings.Count == 0 ? "lesbar" : "mit Warnungen");
        WriteField("Scheduler Jobs", schedulerStatus.Jobs.Count.ToString());
        WriteField("Time Control", timeControl.InWorkWindow ? "Derzeit im Arbeitsfenster" : "Außerhalb des Arbeitsfensters");
        WriteField("Zeitzone", timeControl.TimeZone);
        WriteField("Arbeitsfenster", $"{timeControl.WorkWindow.Start} - {timeControl.WorkWindow.End}");
        WriteField("Nightly Fenster", $"{timeControl.NightlyWindow.Start} - {timeControl.NightlyWindow.End}");
        WriteField("Lernfenster", $"{timeControl.LearningWindow.Start} - {timeControl.LearningWindow.End}");
        WriteField("Human-Review Fenster", $"{timeControl.HumanReviewWindow.Start} - {timeControl.HumanReviewWindow.End}");
        WriteMessages("Bridge Hinweise", bridgeWarnings);

        Console.WriteLine();
        Console.WriteLine("Start-Hilfe:");
        Console.WriteLine(bridgeRunning
            ? "  Bridge läuft bereits: dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge"
            : "  Bridge starten: cd ~/jarvis/HermesRuntime && dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge");
        Console.WriteLine("  Scheduler prüfen: dotnet run --project ./cli/Hermes.Cli.csproj -- scheduler-status");
        Console.WriteLine("  Zeitsteuerung prüfen: dotnet run --project ./cli/Hermes.Cli.csproj -- time-control-status");
        Console.WriteLine("  UI starten: cd ~/jarvis/ui/jarvis-control-center && npm run dev");
        Console.WriteLine("  Komplettstart: cd ~/jarvis && ./start-hermes.sh");

        if (!bridgeRunning)
        {
            var started = TryStartBridgeProcess(out var startMessage);
            WriteField("Bridge Startversuch", started ? "gestartet" : "nicht gestartet");
            WriteField("Bridge Startinfo", startMessage);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private bool IsBridgeHealthy(string bridgeUrl, out string? bridgeVersion, out List<string> warnings)
    {
        bridgeVersion = null;
        warnings = [];

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = client.GetStringAsync(bridgeUrl).GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (TryGetProperty(root, out var data, "data"))
            {
                bridgeVersion = GetString(data, "bridge_version", "bridgeVersion");
                var status = GetString(data, "status");
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("available", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Bridge meldet Status '{status}'.");
                }
            }
            else
            {
                bridgeVersion = GetString(root, "bridge_version", "bridgeVersion");
                var status = GetString(root, "status");
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("available", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Bridge meldet Status '{status}'.");
                }
            }

            return true;
        }
        catch (Exception)
        {
            warnings.Add("Bridge ist nicht aktiv.");
            warnings.Add("Starte: cd ~/jarvis/HermesRuntime && dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge");
            return false;
        }
    }

    private bool TryStartBridgeProcess(out string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project ./cli/Hermes.Cli.csproj -- readonly-bridge --url http://127.0.0.1:8787/",
                WorkingDirectory = _runtimeRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                message = "Bridge-Prozess konnte nicht gestartet werden.";
                return false;
            }

            message = $"Bridge-Startprozess mit PID {process.Id} gestartet.";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private int UpdateTimeControl()
    {
        WriteHeader("Hermes Zeitsteuerung aktualisieren");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var update = new ScheduleTimeControlUpdate(
            TimeZone: ReadOption(_args, "--time-zone") ?? ReadOption(_args, "--timezone"),
            WorkWindow: BuildWindowUpdate("work"),
            NightlyWindow: BuildWindowUpdate("nightly"),
            LearningWindow: BuildWindowUpdate("learning"),
            HumanReviewWindow: BuildWindowUpdate("human-review"),
            ActiveWeekdays: ParseWeekdays(ReadOption(_args, "--active-weekdays")));

        var updated = scheduler.UpdateTimeControl(update);
        var status = updated.BuildTimeControlStatus(DateTimeOffset.UtcNow, Path.Combine(_runtimeRoot, "config", "schedules.json"));

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("Status", status.StatusLabel);
        WriteField("Zeitzone", status.TimeZone);
        WriteField("Arbeitsfenster", $"{status.WorkWindow.Start} - {status.WorkWindow.End} ({(status.WorkWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Nightly", $"{status.NightlyWindow.Start} - {status.NightlyWindow.End} ({(status.NightlyWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Lernfenster", $"{status.LearningWindow.Start} - {status.LearningWindow.End} ({(status.LearningWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Human-Review", $"{status.HumanReviewWindow.Start} - {status.HumanReviewWindow.End} ({(status.HumanReviewWindow.Enabled ? "aktiv" : "inaktiv")})");
        WriteField("Aktive Wochentage", string.Join(", ", status.ActiveWeekdays));
        WriteMessages("Warnings", status.Warnings);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int StartSupervisor()
    {
        WriteHeader("Hermes Supervisor");
        var storagePaths = BuildStoragePaths();
        var scheduleConfigPath = Path.Combine(_runtimeRoot, "config", "schedules.json");
        var scheduler = new HermesInternalScheduler(storagePaths, scheduleConfigPath);
        var schedulerStatus = scheduler.GetStatus();
        var supervisor = new HermesSupervisor(storagePaths, scheduleConfigPath);
        var processManager = new SupervisorProcessManager(storagePaths);
        var maxRuntimeMinutes = ReadIntOption(_args, "--max-runtime-minutes", fallback: 1440, min: 1, max: 10080);
        var checkIntervalSeconds = ReadIntOption(
            _args,
            "--check-interval-seconds",
            fallback: schedulerStatus.CheckIntervalSeconds,
            min: 5,
            max: 3600);
        var maxJobsPerLoop = ReadIntOption(_args, "--max-jobs-per-loop", fallback: 2, min: 1, max: 8);

        if (_args.Any(arg => arg.Equals("--background", StringComparison.OrdinalIgnoreCase)))
        {
            return StartSupervisorBackground(supervisor, processManager, maxRuntimeMinutes, checkIntervalSeconds, maxJobsPerLoop);
        }

        var processStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        if (processStatus.Running && processStatus.Pid != Environment.ProcessId)
        {
            WriteWarning("Hermes Supervisor laeuft bereits. Es wird kein zweiter Supervisor gestartet.");
            WriteSupervisorProcessStatus(processStatus);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteField("Config", DisplayPath(scheduleConfigPath));
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        WriteField("PID", DisplayPath(supervisor.PidPath));
        WriteField("Log", DisplayPath(supervisor.LogPath));
        WriteField("Max Runtime", $"{maxRuntimeMinutes} min");
        WriteField("Check Interval", $"{checkIntervalSeconds} s");
        Console.WriteLine();

        var result = supervisor.Run(
            new SupervisorRunOptions(maxRuntimeMinutes, checkIntervalSeconds, maxJobsPerLoop),
            ExecuteScheduledJob);

        WriteSupervisorState(result.State);
        Console.WriteLine();
        WriteField("Scheduler State", DisplayPath(result.SchedulerStatus.StatePath));
        foreach (var job in result.SchedulerStatus.Jobs.Take(8))
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int StartSupervisorBackground(
        HermesSupervisor supervisor,
        SupervisorProcessManager processManager,
        int maxRuntimeMinutes,
        int checkIntervalSeconds,
        int maxJobsPerLoop)
    {
        processManager.ClearStalePid();
        var processStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        if (processStatus.Running)
        {
            WriteField("Status", "already_running");
            WriteSupervisorProcessStatus(processStatus);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        processManager.RotateLogIfNeeded();
        processManager.AppendLogLine("Hermes Supervisor background launcher invoked.");

        var command = string.Join(
            " ",
            [
                "cd",
                ShellQuote(_runtimeRoot),
                "&&",
                "nohup",
                "setsid",
                "-f",
                "dotnet",
                "run",
                "--project",
                "./cli/Hermes.Cli.csproj",
                "--",
                "supervisor-start",
                "--max-runtime-minutes",
                maxRuntimeMinutes.ToString(CultureInfo.InvariantCulture),
                "--check-interval-seconds",
                checkIntervalSeconds.ToString(CultureInfo.InvariantCulture),
                "--max-jobs-per-loop",
                maxJobsPerLoop.ToString(CultureInfo.InvariantCulture),
                ">>",
                ShellQuote(processManager.LogPath),
                "2>&1",
                "<",
                "/dev/null"
            ]);
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _runtimeRoot
        };
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);

        SupervisorProcessStatus latestStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        for (var attempt = 0; attempt < 20 && !latestStatus.Running; attempt++)
        {
            Thread.Sleep(500);
            latestStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        }

        WriteField("Status", latestStatus.Running ? "background_started" : "background_starting");
        WriteSupervisorProcessStatus(latestStatus);
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSupervisorStatus()
    {
        WriteHeader("Hermes Supervisor Status");
        var storagePaths = BuildStoragePaths();
        var supervisor = new HermesSupervisor(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var processManager = new SupervisorProcessManager(storagePaths);
        var state = supervisor.LoadState();
        var heartbeat = supervisor.LoadHeartbeat();
        var activeHeartbeat = heartbeat?.SupervisorId == state.SupervisorId ? heartbeat : null;
        var processStatus = processManager.GetStatus(state, activeHeartbeat);

        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        WriteSupervisorProcessStatus(processStatus);
        WriteSupervisorState(state);
        if (activeHeartbeat is not null)
        {
            WriteSubHeader("Heartbeat");
            WriteField("Heartbeat UTC", activeHeartbeat.TimestampUtc.ToString("O"));
            WriteField("Status", activeHeartbeat.Status);
            WriteField("Current Job", activeHeartbeat.CurrentJobId ?? "-");
            WriteField("Resource Action", activeHeartbeat.ResourceAction);
            WriteField("Storage Action", activeHeartbeat.StorageAction);
            WriteField("Next Action", activeHeartbeat.NextAction);
        }

        var schedulerStatus = scheduler.GetStatus();
        var nextJob = schedulerStatus.Jobs.FirstOrDefault(job => job.NextRunUtc is not null);
        WriteSubHeader("Scheduler");
        WriteField("Config", DisplayPath(schedulerStatus.ConfigPath));
        WriteField("Jobs", schedulerStatus.Jobs.Count.ToString());
        WriteField("Next Scheduled Job", nextJob is null ? "-" : $"{nextJob.JobId} @ {nextJob.NextRunUtc?.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
        foreach (var job in schedulerStatus.Jobs.Take(5))
        {
            WriteSchedulerJob(job);
        }

        WriteCognitiveOperationalStatus(storagePaths, schedulerStatus);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RequestSupervisorStop()
    {
        WriteHeader("Hermes Supervisor Stop Request");
        var storagePaths = BuildStoragePaths();
        var supervisor = new HermesSupervisor(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var processManager = new SupervisorProcessManager(storagePaths);
        var state = supervisor.RequestStop();

        // If the supervisor is currently inside the existing Nightly Beta3 loop,
        // reuse its safe-stop flag instead of killing the process.
        var nightly = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        nightly.RequestStop();

        WriteField("Stop Request", DisplayPath(supervisor.StopRequestPath));
        WriteField("Nightly Stop Request", DisplayPath(nightly.StopRequestPath));
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteSupervisorProcessStatus(processManager.GetStatus(state, supervisor.LoadHeartbeat()));
        WriteSupervisorState(state);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private ScheduledJobExecutionResult ExecuteScheduledJob(ScheduledJobDefinition job, SupervisorJobContext context)
    {
        var storagePaths = BuildStoragePaths();
        var result = job.JobType.ToLowerInvariant() switch
        {
            "nightly_beta3_research" => ExecuteNightlyBeta3ScheduledJob(job, context),
            "storage_hygiene" => ExecuteStorageHygieneJob(storagePaths),
            "research_insights" => ExecuteResearchInsightsJob(storagePaths),
            "health_snapshot" => ExecuteHealthSnapshotJob(storagePaths),
            "strategy_discovery" => ExecuteStrategyDiscoveryJob(storagePaths),
            "walkforward_validation" => ExecuteWalkForwardValidationJob(storagePaths),
            "scan_knowledge_sources" => ExecuteScanKnowledgeSourcesJob(storagePaths),
            "scan_software_domain" => ExecuteDomainScanJob(storagePaths, "software"),
            "scan_documentation_domain" => ExecuteDomainScanJob(storagePaths, "documentation"),
            "scan_process_domain" => ExecuteDomainScanJob(storagePaths, "process"),
            "scan_research_domain" => ExecuteDomainScanJob(storagePaths, "research"),
            "process_research_queue" => ExecuteProcessResearchQueueJob(storagePaths, job),
            "generate_cognitive_insights" => ExecuteGenerateCognitiveInsightsJob(storagePaths, job),
            "generate_domain_insights" => ExecuteGenerateDomainInsightsJob(storagePaths),
            "trading_nightly_beta3" => ExecuteNightlyBeta3ScheduledJob(job, context),
            "run_planning_cycle" => ExecutePlanningCycleJob(storagePaths, job),
            "process_planned_tasks" => ExecuteProcessPlannedTasksJob(storagePaths, job),
            "evaluate_task_outcomes" => ExecuteEvaluateTaskOutcomesJob(storagePaths, job),
            "run_autonomous_loop" => ExecuteAutonomousLoopJob(storagePaths, job),
            "update_goal_progress" => ExecuteUpdateGoalProgressJob(storagePaths),
            "review_goals" => ExecuteReviewGoalsJob(storagePaths),
            "evaluate_knowledge_quality" => ExecuteKnowledgeQualityJob(storagePaths),
            "consolidate_memory" => ExecuteConsolidateMemoryJob(storagePaths),
            "execute_validation_tasks" => ExecuteValidationTasksJob(storagePaths, job),
            "validate_domain_knowledge" => ExecuteDomainKnowledgeValidationJob(storagePaths, job),
            "run_scalping_robustness_expansion" => ExecuteScalpingRobustnessExpansionJob(storagePaths),
            "run_nightly_work_areas" => ExecuteNightlyWorkAreasJob(storagePaths, job),
            "evidence_auto_loop" => ExecuteEvidenceAutoLoopJob(storagePaths, job),
            "market_data_refresh" => new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "market_data_refresh_disabled_until_explicit_config",
                ReportPath: null,
                Warnings: ["market_data_refresh is intentionally disabled by default; no broker/order action was attempted."]),
            _ => new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "unsupported_internal_job_type",
                ReportPath: null,
                Warnings: [$"Unsupported internal job type: {job.JobType}"])
        };

        if (ShouldRefreshMasterStatusAfterScheduledJob(job.JobType))
        {
            TryWriteMasterStatusSnapshot(storagePaths, printPath: false);
        }

        return result;
    }

    private ScheduledJobExecutionResult ExecuteScalpingRobustnessExpansionJob(StoragePaths storagePaths)
    {
        var service = new ScalpingRobustnessExpansionService(storagePaths, _runtimeRoot);
        var reports = service.ExpandAllRobust();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: reports.Count > 0,
            Action: $"scalping_robustness_expansion reports={reports.Count}; no_auto_trading=true; human_review_required=true",
            ReportPath: service.ExpansionDirectory,
            Warnings: reports.Count == 0 ? ["no_robust_scalping_candidates_to_expand"] : []);
    }

    private static bool ShouldRefreshMasterStatusAfterScheduledJob(string jobType) =>
        jobType.Equals("nightly_beta3_research", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("trading_nightly_beta3", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("run_autonomous_loop", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("run_planning_cycle", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("process_planned_tasks", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("evaluate_task_outcomes", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("update_goal_progress", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("review_goals", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("evaluate_knowledge_quality", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("consolidate_memory", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("validate_domain_knowledge", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("run_nightly_work_areas", StringComparison.OrdinalIgnoreCase)
        || jobType.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase);

    private ScheduledJobExecutionResult ExecuteNightlyBeta3ScheduledJob(ScheduledJobDefinition job, SupervisorJobContext context)
    {
        var remainingMinutes = Math.Max(1, (int)Math.Floor(context.RemainingRuntime.TotalMinutes));
        var maxRuntimeMinutes = Math.Clamp(job.MaxRuntimeMinutes ?? 360, 1, remainingMinutes);
        var maxQualityCandidates = job.Parameters is not null
            && job.Parameters.TryGetValue("max_quality_candidates", out var maxQualityCandidatesText)
            && int.TryParse(maxQualityCandidatesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxQualityCandidates)
            ? Math.Clamp(parsedMaxQualityCandidates, 1, 500)
            : 64;
        var args = new List<string>
        {
            "run-nightly-beta3",
            "--max-runtime-hours",
            (maxRuntimeMinutes / 60.0).ToString("0.####", CultureInfo.InvariantCulture),
            "--sleep-seconds",
            (job.SleepSeconds ?? 60).ToString(CultureInfo.InvariantCulture),
            "--max-idle-iterations",
            (job.MaxIdleIterations ?? 10).ToString(CultureInfo.InvariantCulture),
            "--max-quality-candidates",
            maxQualityCandidates.ToString(CultureInfo.InvariantCulture)
        };

        var exitCode = new HermesCli(args.ToArray()).Run();
        return new ScheduledJobExecutionResult(
            Status: exitCode == 0 ? "completed" : "failed",
            WorkPerformed: true,
            Action: "run-nightly-beta3",
            ReportPath: Path.Combine(_dataRoot, "reports", "nightly_beta3", "nightly_state.json"),
            Warnings: exitCode == 0 ? [] : [$"run-nightly-beta3 exited with code {exitCode}"]);
    }

    private static ScheduledJobExecutionResult ExecuteStorageHygieneJob(StoragePaths storagePaths)
    {
        var hygiene = new StorageHygieneService(storagePaths);
        var plan = hygiene.BuildPlan();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: plan.Candidates.Count > 0,
            Action: $"cleanup_plan candidates={plan.Candidates.Count}",
            ReportPath: hygiene.CleanupPlanPath,
            Warnings: []);
    }

    private ScheduledJobExecutionResult ExecuteEvidenceAutoLoopJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var config = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();

        if (!config.EvidenceAutoLoopEnabled || !job.Enabled)
        {
            return new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "evidence_auto_loop_disabled",
                ReportPath: null,
                Warnings: ["evidence_auto_loop_disabled"]);
        }

        var learningActive = timeControl.LearningWindow.ActiveNow;
        var nightlyActive = timeControl.NightlyWindow.ActiveNow;

        if (config.EvidenceAutoLoopRunOnlyInLearningWindow && !learningActive)
        {
            return new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "evidence_auto_loop_waiting_for_learning_window",
                ReportPath: null,
                Warnings: ["evidence_auto_loop_waiting_for_learning_window"]);
        }

        if (!learningActive && !nightlyActive)
        {
            return new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "evidence_auto_loop_waiting_for_window",
                ReportPath: null,
                Warnings: ["evidence_auto_loop_waiting_for_window"]);
        }

        var service = new EvidenceAutoLoopService(storagePaths);
        var report = service.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: report.PlannedTasks > 0,
            Action: $"evidence_auto_loop planned={report.PlannedTasks}; trading={report.TradingTasks}; documentation={report.DocumentationTasks}; no_auto_trading=true; human_review_required=true",
            ReportPath: service.ReportPath,
            Warnings: report.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteResearchInsightsJob(StoragePaths storagePaths)
    {
        var generator = new ResearchInsightsGenerator(storagePaths);
        var insights = generator.Generate();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"research_insights clusters={insights.Clusters.Count}",
            ReportPath: generator.InsightsPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteHealthSnapshotJob(StoragePaths storagePaths)
    {
        var guard = new ResourceGuard(storagePaths);
        var snapshot = guard.Check();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"health_snapshot resource_action={snapshot.Action}",
            ReportPath: guard.StatusPath,
            Warnings: snapshot.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteStrategyDiscoveryJob(StoragePaths storagePaths)
    {
        var discovery = new StrategyDiscoveryService(storagePaths);
        var report = discovery.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"strategy_discovery analyzed={report.StrategiesAnalyzed}",
            ReportPath: discovery.DiscoveryStatusPath,
            Warnings: report.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteWalkForwardValidationJob(StoragePaths storagePaths)
    {
        var simulations = new RealisticSimulationService(storagePaths).Run();
        var walkForward = new WalkForwardValidationService(storagePaths);
        var report = walkForward.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"walkforward strategies={report.StrategiesEvaluated}; simulations={simulations.Count}",
            ReportPath: walkForward.WalkForwardSummaryPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteScanKnowledgeSourcesJob(StoragePaths storagePaths)
    {
        var scout = new KnowledgeSourceScout(storagePaths);
        var sources = scout.Scan();
        var registry = new KnowledgeSourceRegistry(storagePaths);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"scan_knowledge_sources sources={sources.Count}",
            ReportPath: registry.SourcesPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteDomainScanJob(StoragePaths storagePaths, string domain)
    {
        var service = new DomainCognitiveService(storagePaths);
        var result = service.ScanDomain(domain);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: result.KnowledgeItems > 0,
            Action: $"scan_{domain}_domain sources={result.SourcesScanned}; knowledge_items={result.KnowledgeItems}",
            ReportPath: service.DomainStatusPath,
            Warnings: result.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteGenerateDomainInsightsJob(StoragePaths storagePaths)
    {
        var service = new DomainCognitiveService(storagePaths);
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: insights.Insights.Count > 0,
            Action: $"generate_domain_insights active_domains={status.ActiveDomains.Count}; insights={insights.Insights.Count}",
            ReportPath: service.DomainInsightsPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteProcessResearchQueueJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : 50;
        var service = new ResearchQueueService(storagePaths);
        var queue = service.Process(maxItems);
        var processed = queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: processed > 0,
            Action: $"process_research_queue processed_total={processed}",
            ReportPath: service.QueuePath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteGenerateCognitiveInsightsJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : 20;
        var service = new CognitiveNightlyService(storagePaths);
        var summary = service.Run(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: summary.HypothesesGenerated > 0 || summary.QueueItemsProcessed > 0,
            Action: $"generate_cognitive_insights hypotheses={summary.HypothesesGenerated}; queue_processed={summary.QueueItemsProcessed}",
            ReportPath: service.SummaryPath,
            Warnings: summary.Warnings);
    }

    private static ScheduledJobExecutionResult ExecutePlanningCycleJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.RunPlanningCycle(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: decision.PlannedTasks.Count > 0,
            Action: $"run_planning_cycle needs={decision.Needs.Count}; planned={decision.PlannedTasks.Count}",
            ReportPath: service.PlanningStatusPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteProcessPlannedTasksJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var execution = RunPlannedTaskExecution(storagePaths, maxItems);
        return new ScheduledJobExecutionResult(
            Status: execution.Failed > 0 ? "failed" : "completed",
            WorkPerformed: execution.Completed > 0 || execution.Skipped > 0 || execution.Failed > 0,
            Action: $"process_planned_tasks completed={execution.Completed}; skipped={execution.Skipped}; failed={execution.Failed}; pending_after={execution.PendingAfter}",
            ReportPath: execution.ExecutionStatePath,
            Warnings: execution.Results.SelectMany(result => result.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList());
    }

    private static ScheduledJobExecutionResult ExecuteEvaluateTaskOutcomesJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 50);
        var evaluator = new TaskOutcomeEvaluator(storagePaths);
        var outcomes = evaluator.Evaluate(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: outcomes.Count > 0,
            Action: $"evaluate_task_outcomes evaluated={outcomes.Count}",
            ReportPath: evaluator.PlannerFeedbackPath,
            Warnings: outcomes
                .Where(outcome => outcome.Recommendation is "escalate_to_review" or "retire_task_type")
                .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}")
                .Take(20)
                .ToList());
    }

    private ScheduledJobExecutionResult ExecuteAutonomousLoopJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxIterations = job.Parameters is not null
            && job.Parameters.TryGetValue("max_iterations", out var maxIterationsText)
            && int.TryParse(maxIterationsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIterations)
                ? Math.Clamp(parsedIterations, 1, 1000)
                : 1;
        var maxMinutes = job.Parameters is not null
            && job.Parameters.TryGetValue("max_minutes", out var maxMinutesText)
            && double.TryParse(maxMinutesText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMinutes)
                ? Math.Clamp(parsedMinutes, 0.01, 1440)
                : 10;
        var loop = new AutonomousLearningLoop(
            storagePaths,
            Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));
        var summary = loop.Run(maxIterations, maxMinutes);
        return new ScheduledJobExecutionResult(
            Status: summary.Status.StartsWith("stopped_", StringComparison.OrdinalIgnoreCase) ? "skipped" : "completed",
            WorkPerformed: summary.WorkPerformed > 0,
            Action: $"run_autonomous_loop iterations={summary.IterationsCompleted}; work={summary.WorkPerformed}; idle={summary.IdleIterations}",
            ReportPath: loop.SummaryPath,
            Warnings: summary.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteUpdateGoalProgressJob(StoragePaths storagePaths)
    {
        var tracker = new GoalProgressTracker(storagePaths);
        var state = tracker.Update();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: state.Goals.Count > 0,
            Action: $"update_goal_progress active={state.ActiveGoals}; blocked={state.BlockedGoals.Count}; top={state.TopGoalId}",
            ReportPath: tracker.GoalProgressPath,
            Warnings: state.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteReviewGoalsJob(StoragePaths storagePaths)
    {
        var tracker = new GoalProgressTracker(storagePaths);
        var state = tracker.Update();
        var blocked = state.BlockedGoals.Count;
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"review_goals active={state.ActiveGoals}; blocked={blocked}",
            ReportPath: tracker.GoalStatePath,
            Warnings: state.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteKnowledgeQualityJob(StoragePaths storagePaths)
    {
        var engine = new KnowledgeQualityEngine(storagePaths);
        var report = engine.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: report.TotalKnowledgeItems > 0,
            Action: $"knowledge_health trusted={report.TrustedKnowledge}; weak={report.WeakKnowledge}; deprecated={report.DeprecatedKnowledge}; health={report.KnowledgeHealth}",
            ReportPath: engine.QualityPath,
            Warnings: report.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteConsolidateMemoryJob(StoragePaths storagePaths)
    {
        var service = new MemoryConsolidationService(storagePaths);
        var report = service.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: report.TotalKnowledgeItems > 0,
            Action: $"consolidate_memory weak={report.WeakKnowledge}; deprecated={report.DeprecatedKnowledge}; duplicate_groups={report.DuplicateGroups}",
            ReportPath: service.ConsolidationPath,
            Warnings: report.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteValidationTasksJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var executor = new KnowledgeValidationExecutor(storagePaths);
        var results = executor.Execute(maxItems);
        var completed = results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        var needsMoreData = results.Count(result => result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase));
        var failed = results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
        return new ScheduledJobExecutionResult(
            Status: failed > 0 ? "failed" : "completed",
            WorkPerformed: results.Count > 0,
            Action: $"execute_validation_tasks completed={completed}; needs_more_data={needsMoreData}; failed={failed}",
            ReportPath: executor.ExecutionLogPath,
            Warnings: results.SelectMany(result => result.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList());
    }

    private static ScheduledJobExecutionResult ExecuteDomainKnowledgeValidationJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var domain = job.Parameters is not null
            && job.Parameters.TryGetValue("domain", out var configuredDomain)
            && !string.IsNullOrWhiteSpace(configuredDomain)
                ? configuredDomain
                : "documentation";
        var executor = new KnowledgeValidationExecutor(storagePaths);
        var results = executor.ExecuteDomain(domain, maxItems);
        var status = new DomainKnowledgeValidationService(storagePaths).BuildStatus();
        var completed = results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        var needsMoreData = results.Count(result => result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase));
        var failed = results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
        return new ScheduledJobExecutionResult(
            Status: failed > 0 ? "failed" : "completed",
            WorkPerformed: results.Count > 0,
            Action: $"validate_domain_knowledge domain={domain}; completed={completed}; needs_more_data={needsMoreData}; failed={failed}; health={status.DomainValidationHealth}",
            ReportPath: status.ExecutionLogPath,
            Warnings: results.SelectMany(result => result.Warnings).Concat(status.DomainValidationWarnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList());
    }

    private static int ReadMaxItems(ScheduledJobDefinition job, int fallback) =>
        job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : fallback;

    private int ShowResourceStatus()
    {
        WriteHeader("Hermes Resource Guard");
        var guard = new ResourceGuard(BuildStoragePaths());
        var snapshot = guard.Check();
        WriteField("Report", DisplayPath(guard.StatusPath));
        WriteResourceSnapshot(snapshot);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStorageStatus()
    {
        WriteHeader("Hermes Storage Status");
        var storagePaths = BuildStoragePaths();
        var guard = new ResourceGuard(storagePaths);
        var resource = guard.Check();
        var hygiene = new StorageHygieneService(storagePaths);
        var plan = hygiene.BuildPlan();
        var status = hygiene.LoadStatus() ?? hygiene.BuildStatus();
        WriteField("Storage Root", DisplayPath(storagePaths.Root));
        WriteField("Free Disk", $"{resource.FreeDiskMb / 1024.0:0.##} GB ({resource.FreeDiskPercent:0.##}%)");
        WriteField("Resource Action", resource.Action);
        WriteField("Cleanup Plan", DisplayPath(hygiene.CleanupPlanPath));
        WriteField("Storage Status", DisplayPath(hygiene.StatusPath));
        WriteStorageStatus(status);
        WriteCleanupPlan(plan, limit: 8);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStorageCleanupSafetyAudit()
    {
        WriteHeader("Hermes Storage Cleanup Safety Audit");
        var service = new StorageCleanupSafetyAuditService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Free Disk", $"{report.FreeDiskGb:0.##} GB ({report.FreeDiskPercent:0.##}%)");
        WriteField("Disk Usage", $"{report.DiskUsagePercent:0.##}%");
        WriteField("cleanup_candidates", report.CleanupCandidates.ToString());
        WriteField("estimated_free_bytes", report.EstimatedFreeBytes.ToString());
        WriteField("protected_paths_count", report.ProtectedPathsCount.ToString());
        WriteField("auto_cleanup_policy_enabled", report.AutoCleanupPolicyEnabled.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_allowed", report.AutoCleanupAllowed.ToString().ToLowerInvariant());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Cleanup-Gruppen");
        foreach (var group in report.Groups)
        {
            WriteField(group.Title, $"{group.FileCount} Dateien · {group.EstimatedBytes} bytes · Risiko {group.Risk}");
            WriteField("Automatisch sicher", group.AutomaticallySafe.ToString().ToLowerInvariant());
            WriteField("Manuell empfohlen", group.ManuallyRecommended.ToString().ToLowerInvariant());
            WriteMessages("Beispiele", group.ExamplePaths.Take(6).ToList());
        }
        WriteSubHeader("Geschützte Pfade");
        WriteMessages("Protected", report.ProtectedPaths.Take(13).ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCleanupPlan()
    {
        WriteHeader("Hermes Cleanup Plan");
        var hygiene = new StorageHygieneService(BuildStoragePaths());
        var plan = hygiene.BuildPlan();
        WriteField("Cleanup Plan", DisplayPath(hygiene.CleanupPlanPath));
        WriteCleanupPlan(plan, limit: 20);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ApplyCleanup()
    {
        WriteHeader("Hermes Safe Cleanup Apply");
        if (!_args.Any(arg => arg.Equals("--safe", StringComparison.OrdinalIgnoreCase)))
        {
            WriteError("cleanup-apply braucht explizit --safe. Es wurden keine Dateien geloescht.");
            WriteSafety();
            return 2;
        }

        var hygiene = new StorageHygieneService(BuildStoragePaths());
        var report = hygiene.ApplySafeCleanup();
        WriteField("Cleanup Report", DisplayPath(hygiene.CleanupReportPath));
        WriteField("Files Deleted", report.FilesDeleted.ToString());
        WriteField("Bytes Freed", report.BytesFreed.ToString());
        WriteField("Unsafe Candidates Skipped", report.UnsafeCandidatesSkipped.ToString());
        WriteField("Protected Candidates Skipped", report.ProtectedCandidatesSkipped.ToString());
        WriteField("Audit Log", DisplayPath(report.AuditLogPath));
        WriteField("Safe Mode", report.SafeMode.ToString().ToLowerInvariant());
        WriteMessages("Skipped", report.SkippedPaths.Take(12).ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int UpdateResearchMemory()
    {
        WriteHeader("Hermes Research Memory Update");
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var index = service.UpdateIndex();

        WriteField("Index", DisplayPath(service.IndexPath));
        WriteResearchMemoryIndex(index);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchMemory()
    {
        WriteHeader("Hermes Research Memory");
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var index = service.LoadIndex();
        if (index is null)
        {
            WriteWarning($"Kein Research Memory Index gefunden: {DisplayPath(service.IndexPath)}");
            WriteSafety();
            return 0;
        }

        WriteField("Index", DisplayPath(service.IndexPath));
        WriteResearchMemoryIndex(index);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunLongResearch()
    {
        WriteHeader("Hermes Long-Run Research");
        var hours = ReadHours(_args, 1);
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var job = new LongRunResearchJob(
            JobId: $"long_research_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: startedAtUtc.AddHours(hours),
            RequestedHours: hours,
            RequestedBy: "hermes_cli",
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var existingIndex = service.LoadIndex();
        var currentRanges = service.GetCurrentMarketDataRanges();
        var currentCandleCount = currentRanges.Sum(range => range.CandleCount);
        if (currentCandleCount == 0)
        {
            var index = service.UpdateIndex();
            var checkpoint = service.WriteCheckpoint(
                job,
                iteration: 0,
                status: "stopped_no_data",
                message: "No historical candle data found. Long-run research stopped before beta learning.",
                index,
                betaRunId: null);

            WriteField("Job ID", job.JobId);
            WriteField("Status", "stopped_no_data");
            WriteField("Checkpoint", DisplayPath(checkpoint));
            WriteField("Market Data Candles", "0");
            WriteResearchMemoryIndex(index);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        var currentFingerprint = service.BuildMarketDataFingerprint(currentRanges);
        if (existingIndex is not null
            && existingIndex.LearningReady
            && service.BuildMarketDataFingerprint(existingIndex.ProcessedRanges) == currentFingerprint)
        {
            var strategyStep = RunStrategyResearchAndInsights(storagePaths);
            var checkpoint = service.WriteCheckpoint(
                job,
                iteration: 0,
                status: strategyStep.TestedNow > 0 ? "strategy_research_checkpointed" : "stopped_no_new_data",
                message: $"Current market-data ranges already match the Research Memory Index. No duplicate beta run started. Strategy variants tested: {strategyStep.TestedNow}.",
                existingIndex,
                betaRunId: null);

            WriteField("Job ID", job.JobId);
            WriteField("Status", strategyStep.TestedNow > 0 ? "strategy_research_checkpointed" : "stopped_no_new_data");
            WriteField("Checkpoint", DisplayPath(checkpoint));
            WriteField("Market Data Candles", currentCandleCount.ToString());
            WriteField("Strategy Variants Tested", strategyStep.TestedNow.ToString());
            WriteField("Strategy Insights", DisplayPath(strategyStep.InsightsPath));
            WriteResearchMemoryIndex(existingIndex);
            WriteStrategyResearchMemory(strategyStep.Memory, limit: 3);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var pipeline = new TradingLearningBetaPipeline(storagePaths, eventBus, CliVersion);
        var betaReport = pipeline.Run();
        eventStore.Flush();

        var updatedIndex = service.UpdateIndex();
        var strategyResearch = RunStrategyResearchAndInsights(storagePaths);
        var finalStatus = betaReport.CandlesProcessed == 0
            ? "stopped_no_data"
            : "checkpointed_no_new_data";
        var finalMessage = betaReport.CandlesProcessed == 0
            ? "Beta learning produced no candle-based work; long-run research stopped."
            : $"Beta learning checkpoint written. Strategy variants tested: {strategyResearch.TestedNow}. No second iteration was started without new market-data ranges.";
        var finalCheckpoint = service.WriteCheckpoint(
            job,
            iteration: 1,
            status: finalStatus,
            message: finalMessage,
            updatedIndex,
            betaReport.RunId);

        WriteField("Job ID", job.JobId);
        WriteField("Requested Hours", $"{job.RequestedHours:0.##}");
        WriteField("Status", finalStatus);
        WriteField("Iterations", "1");
        WriteField("Checkpoint", DisplayPath(finalCheckpoint));
        WriteField("Beta Run", betaReport.RunId);
        WriteField("Beta Report", DisplayOptionalPath(betaReport.BetaReportPath));
        WriteField("Strategy Variants Tested", strategyResearch.TestedNow.ToString());
        WriteField("Strategy Insights", DisplayPath(strategyResearch.InsightsPath));
        WriteResearchMemoryIndex(updatedIndex);
        WriteStrategyResearchMemory(strategyResearch.Memory, limit: 3);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunResearchAutopilot()
    {
        WriteHeader("Hermes Research Autopilot Beta");
        var hours = ReadHours(_args, 1);
        var deadlineUtc = DateTimeOffset.UtcNow.AddHours(hours);
        var sleepSeconds = ReadIntOption(_args, "--sleep-seconds", fallback: 60, min: 0, max: 3600);
        var maxIdleIterations = ReadIntOption(_args, "--max-idle-iterations", fallback: 10, min: 1, max: 1000);
        var maxDownloads = ReadIntOption(
            _args,
            "--max-downloads",
            fallback: Math.Clamp((int)Math.Ceiling(hours), 1, 9),
            min: 0,
            max: 27);
        var maxRequests = ReadIntOption(_args, "--max-requests", fallback: 500, min: 1, max: 500);
        var targetToUtc = new DateTimeOffset(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var targetFromUtc = targetToUtc.AddYears(-1);
        var fromOption = ReadOption(_args, "--from");
        if (!string.IsNullOrWhiteSpace(fromOption) && TryParseCliDate(fromOption, out var fromOverride))
        {
            targetFromUtc = fromOverride;
        }

        var toOption = ReadOption(_args, "--to");
        if (!string.IsNullOrWhiteSpace(toOption) && TryParseCliDate(toOption, out var toOverride))
        {
            targetToUtc = toOverride;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var storagePaths = BuildStoragePaths();
        var catalog = new StrategyPatternCatalog(storagePaths);
        var patterns = catalog.LoadOrCreateCatalog();
        var memoryService = new ResearchMemoryIndexService(storagePaths);
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var job = new LongRunResearchJob(
            JobId: $"research_autopilot_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            RequestedHours: hours,
            RequestedBy: "hermes_research_autopilot",
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var warnings = new List<string>();
        var downloadResults = new List<AutopilotDownloadResult>();
        StrategyResearchStepResult? latestStrategyResearch = null;
        ResearchMemoryIndex? latestIndex = null;
        WalkForwardValidationReport? latestWalkForward = null;
        StrategyDiscoveryReport? latestDiscovery = null;
        var totalDownloadPlans = 0;
        var totalDownloadsAttempted = 0;
        var totalStrategyVariantsTested = 0;
        var latestSimulationReports = 0;
        var iterationsCompleted = 0;
        var idleIterations = 0;
        var totalWorkPerformed = 0;
        var status = "completed";
        var nextAction = "deadline_reached";

        using (var eventStore = new EventStore(storagePaths))
        {
            var eventBus = new EventBus();
            eventBus.Subscribe(eventStore.Append);

            while (DateTimeOffset.UtcNow < deadlineUtc)
            {
                var iteration = iterationsCompleted + 1;
                var iterationResult = RunResearchAutopilotIteration(
                    storagePaths,
                    memoryService,
                    configLoad,
                    authContext,
                    eventBus,
                    job,
                    iteration,
                    targetFromUtc,
                    targetToUtc,
                    maxDownloads,
                    maxRequests,
                    warnings);

                eventStore.Flush();

                iterationsCompleted++;
                totalDownloadPlans += iterationResult.DownloadPlans;
                totalDownloadsAttempted += iterationResult.DownloadsAttempted;
                totalStrategyVariantsTested += iterationResult.StrategyResearch.TestedNow;
                latestStrategyResearch = iterationResult.StrategyResearch;
                latestIndex = iterationResult.Index;
                latestWalkForward = iterationResult.WalkForward;
                latestDiscovery = iterationResult.Discovery;
                latestSimulationReports = iterationResult.SimulationReports;
                downloadResults.AddRange(iterationResult.Downloads);
                warnings.AddRange(iterationResult.Warnings);

                if (iterationResult.WorkPerformed)
                {
                    idleIterations = 0;
                    totalWorkPerformed += iterationResult.WorkUnits;
                }
                else
                {
                    idleIterations++;
                }

                nextAction = iterationResult.NextAction;
                var checkpoint = memoryService.WriteCheckpoint(
                    job,
                    iteration,
                    iterationResult.Status,
                    $"work_performed={iterationResult.WorkUnits}; idle_iterations={idleIterations}; next_action={nextAction}",
                    latestIndex,
                    betaRunId: iterationResult.BetaReport?.RunId);

                WriteSubHeader($"Iteration {iteration}");
                WriteField("elapsed_minutes", $"{(DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes:0.##}");
                WriteField("work_performed", iterationResult.WorkUnits.ToString());
                WriteField("idle_iterations", idleIterations.ToString());
                WriteField("next_action", nextAction);
                WriteField("Checkpoint", DisplayPath(checkpoint));
                Console.WriteLine();

                if (iterationResult.Status == "stopped_storage_critical")
                {
                    status = "stopped_storage_critical";
                    nextAction = "fix_storage_before_retry";
                    break;
                }

                if (idleIterations >= maxIdleIterations)
                {
                    status = "stopped_max_idle_iterations";
                    nextAction = "wait_for_new_data_or_expand_strategy_space";
                    break;
                }

                var remaining = deadlineUtc - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    status = "completed_deadline_reached";
                    nextAction = "deadline_reached";
                    break;
                }

                if (sleepSeconds > 0)
                {
                    var sleepFor = TimeSpan.FromSeconds(Math.Min(sleepSeconds, Math.Max(0, remaining.TotalSeconds)));
                    Thread.Sleep(sleepFor);
                }
            }
        }

        if (DateTimeOffset.UtcNow >= deadlineUtc && status == "completed")
        {
            status = "completed_deadline_reached";
            nextAction = "deadline_reached";
        }

        if (iterationsCompleted == 0)
        {
            latestIndex = memoryService.UpdateIndex();
            latestStrategyResearch = RunStrategyResearchAndInsights(storagePaths);
            latestWalkForward = new WalkForwardValidationService(storagePaths).Run();
            latestDiscovery = new StrategyDiscoveryService(storagePaths).Run();
            latestSimulationReports = new RealisticSimulationService(storagePaths).Run().Count;
            iterationsCompleted = 1;
            status = "completed_minimum_iteration";
            nextAction = "deadline_reached";
        }

        latestStrategyResearch ??= RunStrategyResearchAndInsights(storagePaths);
        latestIndex ??= memoryService.UpdateIndex();
        latestWalkForward ??= new WalkForwardValidationService(storagePaths).Run();
        latestDiscovery ??= new StrategyDiscoveryService(storagePaths).Run();

        var elapsedMinutes = (DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes;
        var report = WriteResearchAutopilotReport(
            storagePaths,
            job.JobId,
            startedAtUtc,
            hours,
            targetFromUtc,
            targetToUtc,
            totalDownloadPlans,
            totalDownloadsAttempted,
            downloadResults,
            totalStrategyVariantsTested,
            latestStrategyResearch.Memory.ResearchEntries?.Count ?? 0,
            catalog.CatalogPath,
            latestStrategyResearch.InsightsPath,
            status,
            elapsedMinutes,
            iterationsCompleted,
            totalWorkPerformed,
            idleIterations,
            nextAction,
            warnings);

        var finalCheckpoint = memoryService.WriteCheckpoint(
            job,
            iteration: iterationsCompleted + 1,
            status: report.Status,
            message: $"Research Autopilot stopped with {report.Status}. Iterations: {iterationsCompleted}. Work performed: {totalWorkPerformed}.",
            latestIndex,
            betaRunId: null);

        WriteField("Autopilot Report", DisplayPath(Path.Combine(storagePaths.Root, "strategy_research", "autopilot", $"{report.ReportId}.autopilot_report.json")));
        WriteField("Checkpoint", DisplayPath(finalCheckpoint));
        WriteField("requested_hours", $"{hours:0.##}");
        WriteField("elapsed_minutes", $"{elapsedMinutes:0.##}");
        WriteField("iterations_completed", iterationsCompleted.ToString());
        WriteField("work_performed", totalWorkPerformed.ToString());
        WriteField("idle_iterations", idleIterations.ToString());
        WriteField("next_action", nextAction);
        WriteField("Target Range", $"{targetFromUtc:yyyy-MM-dd} -> {targetToUtc:yyyy-MM-dd}");
        WriteField("Pattern Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Patterns", patterns.Count.ToString());
        WriteField("Download Plans", report.DownloadPlans.ToString());
        WriteField("Downloads Attempted", report.DownloadsAttempted.ToString());
        WriteField("Candles Downloaded", report.CandlesDownloaded.ToString());
        WriteField("Download Requests", report.DownloadRequests.ToString());
        WriteField("Strategy Variants Tested", report.StrategyVariantsTested.ToString());
        WriteField("Strategy Memory Entries", report.StrategyResearchEntries.ToString());
        WriteField("Simulation Reports", latestSimulationReports.ToString());
        WriteField("Walk-Forward Robust", latestWalkForward.RobustStrategies.ToString());
        WriteField("Overfit Suspects", latestWalkForward.OverfitSuspectedStrategies.ToString());
        WriteField("Discovery Strategies Analyzed", latestDiscovery.StrategiesAnalyzed.ToString());
        WriteField("Discovery Risk Flags", latestDiscovery.RiskFlagsDetected.ToString());
        WriteField("Insights", DisplayPath(latestStrategyResearch.InsightsPath));
        WriteField("Status", report.Status);
        WriteResearchMemoryIndex(latestIndex);
        WriteStrategyResearchMemory(latestStrategyResearch.Memory, limit: 3);
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private AutopilotIterationResult RunResearchAutopilotIteration(
        StoragePaths storagePaths,
        ResearchMemoryIndexService memoryService,
        CTraderOpenApiConfigLoadResult configLoad,
        (CTraderOAuthUrlResult OAuthUrl, CTraderAuthStatus AuthStatus, CTraderAuthTokenState TokenState) authContext,
        EventBus eventBus,
        LongRunResearchJob job,
        int iteration,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc,
        int maxDownloads,
        int maxRequests,
        IReadOnlyList<string> inheritedWarnings,
        bool runQualityGates = false,
        int maxQualityCandidates = 64)
    {
        var warnings = new List<string>();
        var disk = new DiskSpaceGuard().Check(storagePaths, minimumFreeDiskMb: 512);
        if (!disk.IsOk)
        {
            warnings.Add($"Storage critical: {disk.Warning}");
            var index = memoryService.UpdateIndex();
            var strategy = RunStrategyResearchAndInsights(storagePaths);
            var storageStopWalkForward = new WalkForwardValidationService(storagePaths).Run();
            var storageStopDiscovery = new StrategyDiscoveryService(storagePaths).Run();
            return new AutopilotIterationResult(
                Iteration: iteration,
                DownloadPlans: 0,
                DownloadsAttempted: 0,
                Downloads: [],
                BetaReport: null,
                Index: index,
                StrategyResearch: strategy,
                SimulationReports: 0,
                WalkForward: storageStopWalkForward,
                Discovery: storageStopDiscovery,
                Warnings: warnings,
                WorkPerformed: false,
                WorkUnits: 0,
                Status: "stopped_storage_critical",
                NextAction: "fix_storage_before_retry");
        }

        var beforeRanges = memoryService.GetCurrentMarketDataRanges();
        var beforeFingerprint = memoryService.BuildMarketDataFingerprint(beforeRanges);
        var plans = BuildAutopilotDownloadPlans(beforeRanges, targetFromUtc, targetToUtc);
        var selectedPlans = plans.Take(maxDownloads).ToList();
        var downloads = new List<AutopilotDownloadResult>();

        foreach (var plan in selectedPlans)
        {
            try
            {
                var download = DownloadHistoricalRangeForAutopilot(
                    storagePaths,
                    configLoad,
                    authContext,
                    eventBus,
                    plan.Request,
                    maxRequests);
                downloads.Add(download);
                warnings.AddRange(download.Warnings);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
            {
                warnings.Add($"{plan.Request.Symbol} {plan.Request.Timeframe} {plan.Reason}: {ex.Message}");
            }
        }

        var afterRanges = memoryService.GetCurrentMarketDataRanges();
        var afterFingerprint = memoryService.BuildMarketDataFingerprint(afterRanges);
        var candlesAvailable = afterRanges.Sum(range => range.CandleCount) > 0;
        var newCandles = downloads.Sum(download => download.ImportResult.CandleCount);
        var marketDataChanged = !string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal);
        var shouldRunBeta = candlesAvailable
            && (iteration == 1 || marketDataChanged || !HasFeatureExports(storagePaths));
        TradingLearningBetaReport? betaReport = null;
        if (shouldRunBeta)
        {
            using var betaEventStore = new EventStore(storagePaths);
            var betaBus = new EventBus();
            betaBus.Subscribe(betaEventStore.Append);
            var pipeline = new TradingLearningBetaPipeline(storagePaths, betaBus, CliVersion);
            betaReport = pipeline.Run();
            betaEventStore.Flush();
        }
        else if (!candlesAvailable)
        {
            warnings.Add("No candle data available after Autopilot data expansion; beta pipeline skipped.");
        }

        var updatedIndex = memoryService.UpdateIndex();
        var strategyResearch = RunStrategyResearchAndInsights(storagePaths);
        var simulationReports = new RealisticSimulationService(storagePaths).Run();
        var walkForward = new WalkForwardValidationService(storagePaths).Run();
        var discovery = new StrategyDiscoveryService(storagePaths).Run();
        if (runQualityGates)
        {
            new MonteCarloSimulationService(storagePaths).Run(maxCandidates: maxQualityCandidates);
            new CostStressTestService(storagePaths).Run(maxQualityCandidates);
            new RiskOfRuinService(storagePaths).Run(maxQualityCandidates);
            new BotCandidatePipelineService(storagePaths).Evaluate();
            new BotCandidateRejectionAnalyzer(storagePaths).Run();
            new ResearchQualityImprovementExperimentService(storagePaths)
                .Run(maxBatchSize: Math.Min(maxQualityCandidates, 64));
        }

        new ResearchInsightsGenerator(storagePaths).Generate();

        var workUnits = newCandles
            + strategyResearch.TestedNow
            + (betaReport is { CandlesProcessed: > 0 } ? 1 : 0);
        var nextAction = workUnits > 0
            ? "continue_until_deadline"
            : "sleep_then_mutate_or_stop_after_idle_limit";

        return new AutopilotIterationResult(
            Iteration: iteration,
            DownloadPlans: plans.Count,
            DownloadsAttempted: selectedPlans.Count,
            Downloads: downloads,
            BetaReport: betaReport,
            Index: updatedIndex,
            StrategyResearch: strategyResearch,
            SimulationReports: simulationReports.Count,
            WalkForward: walkForward,
            Discovery: discovery,
            Warnings: warnings
                .Concat(inheritedWarnings)
                .Distinct(StringComparer.Ordinal)
                .Take(80)
                .ToList(),
            WorkPerformed: workUnits > 0,
            WorkUnits: workUnits,
            Status: workUnits > 0 ? "iteration_completed" : "idle_iteration",
            NextAction: nextAction);
    }

    private int RunWalkForwardValidation()
    {
        WriteHeader("Hermes Walk-Forward Validation");
        var storagePaths = BuildStoragePaths();
        var simulations = new RealisticSimulationService(storagePaths).Run();
        var report = new WalkForwardValidationService(storagePaths).Run();
        new ResearchInsightsGenerator(storagePaths).Generate();

        WriteField("Simulation Reports", simulations.Count.ToString());
        WriteField("Walk-Forward Report", DisplayPath(Path.Combine(storagePaths.Root, "simulation", "walkforward_validation.json")));
        WriteField("Overfit Report", DisplayPath(Path.Combine(storagePaths.Root, "simulation", "overfit_report.json")));
        WriteWalkForwardSummary(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSimulationStatus()
    {
        WriteHeader("Hermes Realistic Simulation Status");
        var service = new RealisticSimulationService(BuildStoragePaths());
        var reports = service.LoadReports();
        if (reports.Count == 0)
        {
            WriteWarning("Keine Simulation-Reports gefunden. Nutze run-walkforward-validation.");
            WriteSafety();
            return 0;
        }

        WriteField("Simulation Root", DisplayPath(service.SimulationRoot));
        WriteField("Reports", reports.Count.ToString());
        WriteField("Latest UTC", reports.Max(report => report.CreatedAtUtc).ToString("O"));
        WriteField("Average Stability", $"{reports.Average(report => report.Metrics.StabilityScore):0.####}");
        WriteField("Average Profit Factor", $"{reports.Average(report => report.Metrics.ProfitFactor):0.####}");
        WriteField("Average Realism Penalty", $"{reports.Average(report => report.Metrics.RealismPenalty):0.####}");
        WriteField("Average Realism Score", $"{reports.Average(report => report.Metrics.RealismScore):0.####}");
        WriteField("Average Overfit Risk", $"{reports.Average(report => report.Metrics.OverfitRisk):0.####}");
        WriteField("Average Robustness", $"{reports.Average(report => report.Metrics.RobustnessConfidence):0.####}");
        WriteField("Average Cost Sensitivity", $"{reports.Average(report => report.Metrics.CostSensitivity):0.####}");
        WriteField("Too Good To Be True", reports.Count(report => report.Metrics.TooGoodToBeTrue).ToString());
        WriteField("no_auto_trading", "true");
        WriteField("human_review_required", "true");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRealismReport()
    {
        WriteHeader("Hermes Realism Report");
        var service = new RealisticSimulationService(BuildStoragePaths());
        service.Run();
        var report = service.LoadRealismReport();
        if (report is null)
        {
            report = service.LoadRealismReport();
        }

        if (report is null)
        {
            WriteWarning("Kein Realism Report erzeugbar.");
            WriteSafety();
            return 0;
        }

        WriteField("Realism Report", DisplayPath(service.RealismReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Realistic Strategies", report.RealisticStrategies.ToString());
        WriteField("Suspicious Strategies", report.SuspiciousStrategies.ToString());
        WriteField("Average Realism Penalty", $"{report.AverageRealismPenalty:0.####}");
        WriteField("Average Realism Score", $"{report.AverageRealismScore:0.####}");
        WriteField("Average Overfit Risk", $"{report.AverageOverfitRisk:0.####}");
        WriteField("Average Cost Sensitivity", $"{report.AverageCostSensitivity:0.####}");
        WriteField("Average Loss Distribution", $"{report.AverageLossDistributionQuality:0.####}");
        WriteField("too_good_to_be_true", report.TooGoodToBeTrueStrategies.ToString());
        WriteMessages("Most Realistic", report.MostRealisticStrategies.Take(10).ToList());
        WriteMessages("Suspicious", report.SuspiciousStrategiesList.Take(10).ToList());
        WriteMessages("Too Good To Be True", report.TooGoodToBeTrueStrategiesList?.Take(10).ToList() ?? []);
        WriteMessages("Cost Sensitive", report.CostSensitiveStrategies?.Take(10).ToList() ?? []);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostSensitivityReport()
    {
        WriteHeader("Hermes Cost Sensitivity Report");
        var service = new RealisticSimulationService(BuildStoragePaths());
        service.Run();
        var report = service.LoadCostSensitivityReport();
        if (report is null)
        {
            report = service.LoadCostSensitivityReport();
        }

        if (report is null)
        {
            WriteWarning("Kein Cost Sensitivity Report erzeugbar.");
            WriteSafety();
            return 0;
        }

        WriteField("Cost Report", DisplayPath(service.CostSensitivityReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Cost Sensitive", report.CostSensitiveStrategies.ToString());
        WriteField("Stress Cost Failures", report.StressCostFailures.ToString());
        WriteField("Average Cost Sensitivity", $"{report.AverageCostSensitivity:0.####}");
        foreach (var entry in report.Entries.Take(12))
        {
            WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternId ?? "-"} / {entry.StrategyVariantId}");
            WriteField("Status", entry.Status);
            WriteField("Trades", entry.TradeCount.ToString());
            WriteField("Normal Cost Score", $"{entry.NormalCostScore:0.####}");
            WriteField("High Cost Score", $"{entry.HighCostScore:0.####}");
            WriteField("Stress Cost Score", $"{entry.StressCostScore:0.####}");
            WriteField("Cost Sensitivity", $"{entry.CostSensitivity:0.####}");
            WriteField("Works Only Without Costs", entry.WorksOnlyWithoutCosts.ToString().ToLowerInvariant());
            WriteField("too_good_to_be_true", entry.TooGoodToBeTrue.ToString().ToLowerInvariant());
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMonteCarloReport()
    {
        WriteHeader("Hermes Monte-Carlo Report");
        var simulationRuns = ReadIntOption(_args, "--simulations", fallback: 100, min: 20, max: 2000);
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new MonteCarloSimulationService(BuildStoragePaths());
        var report = service.Run(simulationRuns, maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Simulations/Strategy", report.SimulationsPerStrategy.ToString());
        WriteField("Passed", report.Passed.ToString());
        WriteField("Failed", report.Failed.ToString());
        WriteField("Avg Positive Ratio", $"{report.AveragePositiveSimulationRatio:0.####}");
        WriteField("Avg Ruin Probability", $"{report.AverageRuinProbabilityEstimate:0.####}");
        foreach (var result in report.Results.Take(10))
        {
            WriteMonteCarloResult(result);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostStressReport()
    {
        WriteHeader("Hermes Cost Stress Report");
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new CostStressTestService(BuildStoragePaths());
        var report = service.Run(maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Survives Normal", report.SurvivesNormalCost.ToString());
        WriteField("Survives Spread x2", report.SurvivesSpreadX2.ToString());
        WriteField("Survives Spread x3", report.SurvivesSpreadX3.ToString());
        WriteField("Survives Stress", report.SurvivesStressCost.ToString());
        WriteField("Stress Failures", report.StressCostFailures.ToString());
        foreach (var result in report.Results.Take(10))
        {
            WriteCostStressResult(result);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRiskOfRuinReport()
    {
        WriteHeader("Hermes Risk-of-Ruin Report");
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new RiskOfRuinService(BuildStoragePaths());
        var report = service.Run(maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Passed", report.Passed.ToString());
        WriteField("Failed", report.Failed.ToString());
        WriteField("Avg Ruin Probability", $"{report.AverageRuinProbabilityEstimate:0.####}");
        WriteField("Avg Recommended Risk", $"{report.AverageRecommendedMaxRiskPerTrade:0.####}%");
        foreach (var entry in report.Entries.Take(10))
        {
            WriteRiskOfRuinEntry(entry);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWalkForwardSummary()
    {
        WriteHeader("Hermes Walk-Forward Summary");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Walk-Forward Report", DisplayPath(service.WalkForwardPath));
        WriteField("Walk-Forward Summary", DisplayPath(service.WalkForwardSummaryPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Take(8))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Train", $"{item.TrainScore:0.####}");
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("OOS Available", item.OosAvailable.ToString().ToLowerInvariant());
            WriteField("WalkForward Confidence", $"{item.WalkForwardConfidence:0.####}");
            WriteField("Degradation", $"{item.DegradationScore:0.####}");
            WriteField("Robustness Gap", $"{item.RobustnessGap:0.####}");
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Realism Penalty", $"{item.RealismPenalty:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
            WriteField("Overfit Risk", $"{item.OverfitRisk:0.####}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyDiscoveryStatus()
    {
        WriteHeader("Hermes Trusted Strategy Discovery");
        var service = new StrategyDiscoveryService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Trusted Sources", DisplayPath(service.TrustedSourcesPath));
        WriteField("Discovery Status", DisplayPath(service.DiscoveryStatusPath));
        WriteField("Sources Whitelisted", report.SourcesWhitelisted.ToString());
        WriteField("Local .cs Files Analyzed", report.LocalCsFilesAnalyzed.ToString());
        WriteField("Strategies Analyzed", report.StrategiesAnalyzed.ToString());
        WriteField("Risk Flags", report.RiskFlagsDetected.ToString());
        WriteField("Foreign Code Executed", (!report.NoForeignCodeExecuted).ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOverfitReport()
    {
        WriteHeader("Hermes Overfit Report");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Overfit Report", DisplayPath(service.OverfitReportPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Where(item => item.StrategyConfidence == "overfit_suspected").Take(12))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("OOS Available", item.OosAvailable.ToString().ToLowerInvariant());
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
            WriteMessages("Flags", item.OverfitFlags);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRobustStrategies()
    {
        WriteHeader("Hermes Robust Strategies");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Walk-Forward Report", DisplayPath(service.WalkForwardPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Where(item => item.Robust).Take(12))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Train", $"{item.TrainScore:0.####}");
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("WalkForward Confidence", $"{item.WalkForwardConfidence:0.####}");
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowBotCandidates()
    {
        WriteHeader("Hermes Bot Candidate Pipeline");
        var service = new BotCandidatePipelineService(BuildStoragePaths());
        var report = service.Evaluate();

        WriteField("Candidates", DisplayPath(service.BotCandidatesPath));
        WriteField("Rejected", DisplayPath(service.RejectedCandidatesPath));
        WriteField("Report", DisplayPath(service.LatestReportPath));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("bot_candidate_count", report.BotCandidateCount.ToString());
        WriteField("demo_bot_candidate", report.DemoBotCandidateCount.ToString());
        WriteField("Promising", report.PromisingCandidateCount.ToString());
        WriteField("Robust", report.RobustCandidateCount.ToString());
        WriteField("Rejected", report.RejectedCandidateCount.ToString());
        WriteField("Blocked Monte-Carlo", report.CandidatesBlockedByMonteCarlo.ToString());
        WriteField("Blocked Cost Stress", report.CandidatesBlockedByCostStress.ToString());
        WriteField("Blocked Risk", report.CandidatesBlockedByRisk.ToString());
        WriteMessages("Monte-Carlo Summary", report.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", report.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", report.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteMessages("Top Demo Bot Candidates", report.TopDemoBotCandidates);
        WriteMessages("Next Validation", report.NextValidationRecommendations);
        foreach (var candidate in report.Candidates.Take(10))
        {
            WriteBotCandidate(candidate);
        }

        WriteField("No Bot Created", report.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowBotCandidateReport()
    {
        WriteHeader("Hermes Bot Candidate Report");
        var service = new BotCandidatePipelineService(BuildStoragePaths());
        var report = service.Evaluate();

        WriteField("Report", DisplayPath(service.LatestReportPath));
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Bot Candidates", report.BotCandidateCount.ToString());
        WriteField("Demo Bot Candidates", report.DemoBotCandidateCount.ToString());
        WriteField("Rejected", report.RejectedCandidateCount.ToString());
        WriteField("Blocked Monte-Carlo", report.CandidatesBlockedByMonteCarlo.ToString());
        WriteField("Blocked Cost Stress", report.CandidatesBlockedByCostStress.ToString());
        WriteField("Blocked Risk", report.CandidatesBlockedByRisk.ToString());
        WriteMessages("Monte-Carlo Summary", report.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", report.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", report.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteMessages("Top Demo Bot Candidates", report.TopDemoBotCandidates);
        WriteMessages("Next Validation", report.NextValidationRecommendations);
        WriteMessages(
            "Top Rejection Reasons",
            report.RejectionReasonCounts
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(item => $"{item.Key}: {item.Value}")
                .ToList());

        foreach (var candidate in report.RejectedCandidates.Take(8))
        {
            WriteBotCandidate(candidate);
        }

        WriteField("No Bot Created", report.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCandidateRejectionAnalysis()
    {
        WriteHeader("Hermes Candidate Rejection Analysis");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.Run();

        WriteField("Analysis", DisplayPath(analyzer.AnalysisPath));
        WriteField("Near Miss", DisplayPath(analyzer.NearMissPath));
        WriteField("Experiments", DisplayPath(analyzer.ImprovementExperimentsPath));
        WriteField("Candidates Analyzed", report.CandidatesAnalyzed.ToString());
        WriteField("Rejected", report.RejectedCandidates.ToString());
        WriteField("Near Miss Count", report.NearMissCount.ToString());
        WriteMessages("Why No Candidates", report.WhyNoCandidates);
        WriteMessages(
            "Top Blockers",
            report.ReasonSummaries
                .Take(12)
                .Select(summary => $"{summary.Reason}: count={summary.Count}, share={summary.Share:P2}, category={summary.Category}, hint={summary.ImprovementHint}")
                .ToList());
        WriteMessages("Potential Clusters", report.PotentialClusters);
        WriteMessages("Unsuitable Clusters", report.UnsuitableClusters);
        WriteMessages(
            "Recommended Experiments",
            report.RecommendedImprovementExperiments
                .Take(8)
                .Select(FormatSuggestion)
                .ToList());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingStatus()
    {
        WriteHeader("Hermes Scalping Research Status");
        var service = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadReport() ?? service.RunResearch(ScalpingResearchService.DefaultAsset, 0);
        WriteScalpingSummary(service, report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunScalpingResearch()
    {
        WriteHeader("Hermes Scalping Research Loop");
        var asset = ReadOption(_args, "--asset") ?? ScalpingResearchService.DefaultAsset;
        var maxVariants = ReadIntOption(_args, "--max-variants", fallback: 50, min: 1, max: 500);
        var service = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var report = service.RunResearch(asset, maxVariants);
        WriteScalpingSummary(service, report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunMultiAssetScalpingResearch()
    {
        WriteHeader("Hermes Multi-Asset Scalping Research Loop");
        var service = new MultiAssetScalpingOrchestratorService(BuildStoragePaths(), _runtimeRoot);
        var assets = ReadAssetList(_args, "--assets");
        if (_args.Any(arg => arg.Equals("--all-ready-assets", StringComparison.OrdinalIgnoreCase)))
        {
            assets = ["GER40", "XAUUSD", "EURUSD"];
        }

        var maxVariants = ReadIntOption(_args, "--max-variants", fallback: 100, min: 1, max: 500);
        var report = service.Run(assets.ToArray(), maxVariants);
        WriteMultiAssetResearchReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMultiAssetResearchStatus()
    {
        WriteHeader("Hermes Multi-Asset Scalping Research Status");
        var service = new MultiAssetScalpingOrchestratorService(BuildStoragePaths(), _runtimeRoot);
        var status = service.BuildStatus();
        WriteMultiAssetResearchStatus(status, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingCandidates()
    {
        WriteHeader("Hermes Scalping Candidates");
        var service = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadOrCreateStatus();
        WriteScalpingSummary(service, report);
        foreach (var candidate in report.Candidates.OrderByDescending(item => item.ConfidenceScore).Take(15))
        {
            WriteScalpingCandidateSummary(candidate);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteMultiAssetResearchReport(MultiAssetScalpingResearchReport report, MultiAssetScalpingOrchestratorService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Assets Requested", report.AssetsRequested.Count == 0 ? "-" : string.Join(", ", report.AssetsRequested));
        WriteField("Assets Processed", report.AssetsProcessed.Count == 0 ? "-" : string.Join(", ", report.AssetsProcessed));
        WriteField("Assets Skipped", report.AssetsSkipped.Count == 0 ? "-" : string.Join(", ", report.AssetsSkipped));
        WriteField("Safety Flags", string.Join(", ", report.SafetyFlags));
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Next Recommended Actions", report.NextRecommendedActions);
        foreach (var item in report.PerAssetResults)
        {
            WriteMultiAssetAssetResult(item);
        }
    }

    private void WriteMultiAssetResearchStatus(MultiAssetResearchStatusSnapshot status, MultiAssetScalpingOrchestratorService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Updated UTC", status.UpdatedAtUtc.ToString("O"));
        WriteField("Assets Ready", status.AssetsReady.Count == 0 ? "-" : string.Join(", ", status.AssetsReady));
        WriteField("Assets Setup Ready", status.AssetsSetupReady.Count == 0 ? "-" : string.Join(", ", status.AssetsSetupReady));
        WriteField("Assets Data Ready Only", status.AssetsDataReadyOnly.Count == 0 ? "-" : string.Join(", ", status.AssetsDataReadyOnly));
        WriteField("Assets Missing Data", status.AssetsMissingData.Count == 0 ? "-" : string.Join(", ", status.AssetsMissingData));
        WriteMessages("Warnings", status.Warnings);
        WriteMessages("Next Recommended Actions", status.NextRecommendedActions);
        foreach (var item in status.PerAssetResults)
        {
            WriteMultiAssetAssetResult(item);
        }

        WriteField("no_auto_trading", status.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", status.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", status.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", status.LiveTradingEnabled.ToString().ToLowerInvariant());
        WriteField("research_only", status.ResearchOnly.ToString().ToLowerInvariant());
    }

    private static void WriteMultiAssetAssetResult(MultiAssetScalpingAssetResult item)
    {
        WriteSubHeader(item.Asset);
        WriteField("Historical Data Status", item.HistoricalDataStatus);
        WriteField("Quote Status", item.QuoteStatus);
        WriteField("Research Status", item.ResearchStatus);
        WriteField("Candidates Total", item.CandidatesTotal.ToString());
        WriteField("Robust Candidates", item.RobustCandidates.ToString());
        WriteField("Final Candidates", item.FinalCandidates.ToString());
        WriteField("Certified Candidates", item.CertifiedCandidates.ToString());
        WriteField("Failed Candidates", item.FailedCandidates.ToString());
        WriteField("Setup Count", item.SetupCount.ToString());
        WriteField("Best Setup", item.BestSetup);
        WriteField("Signal Agent Spec Status", item.SignalAgentSpecStatus);
        WriteField("Next Action", item.NextAction);
        WriteField("M1 Available", item.M1Available.ToString().ToLowerInvariant());
        WriteField("M5 Available", item.M5Available.ToString().ToLowerInvariant());
        WriteField("M15 Available", item.M15Available.ToString().ToLowerInvariant());
        WriteField("Timeframes", item.Timeframes.Count == 0 ? "-" : string.Join(", ", item.Timeframes));
        WriteMessages("Warnings", item.Warnings);
    }

    private static void WriteMultiAssetAssetResult(MultiAssetResearchAssetStatus item)
    {
        WriteSubHeader(item.Asset);
        WriteField("Historical Data Status", item.HistoricalDataStatus);
        WriteField("Quote Status", item.QuoteStatus);
        WriteField("Research Status", item.ResearchStatus);
        WriteField("Candidates Total", item.CandidatesTotal.ToString());
        WriteField("Robust Candidates", item.RobustCandidates.ToString());
        WriteField("Final Candidates", item.FinalCandidates.ToString());
        WriteField("Certified Candidates", item.CertifiedCandidates.ToString());
        WriteField("Failed Candidates", item.FailedCandidates.ToString());
        WriteField("Setup Count", item.SetupCount.ToString());
        WriteField("Best Setup", item.BestSetup);
        WriteField("Signal Agent Spec Status", item.SignalAgentSpecStatus);
        WriteField("Next Action", item.NextAction);
        WriteField("M1 Available", item.M1Available.ToString().ToLowerInvariant());
        WriteField("M5 Available", item.M5Available.ToString().ToLowerInvariant());
        WriteField("M15 Available", item.M15Available.ToString().ToLowerInvariant());
        WriteField("Timeframes", item.Timeframes.Count == 0 ? "-" : string.Join(", ", item.Timeframes));
        WriteMessages("Warnings", item.Warnings);
    }

    private int ShowScalpingCandidate()
    {
        WriteHeader("Hermes Scalping Candidate");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var candidate = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot).FindCandidate(id);
        if (candidate is null)
        {
            WriteError($"Scalping Candidate nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteScalpingCandidateDetails(candidate);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingValidationReport()
    {
        WriteHeader("Hermes Scalping Validation Report");
        var service = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadOrCreateStatus();
        WriteScalpingSummary(service, report);
        WriteMessages("Data Gaps", report.DataGaps);
        WriteMessages(
            "Top Rejection Reasons",
            report.Candidates
                .SelectMany(candidate => candidate.RejectionReasons)
                .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(group => $"{group.Key}: {group.Count()}")
                .ToList());
        foreach (var candidate in report.Candidates.OrderByDescending(item => item.ConfidenceScore).Take(8))
        {
            WriteScalpingCandidateSummary(candidate);
            WriteMessages("Gate Failures", candidate.Validation.GateFailures);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunScalpingRobustnessExpansion()
    {
        WriteHeader("Hermes Scalping Robustness Expansion");
        var service = new ScalpingRobustnessExpansionService(BuildStoragePaths(), _runtimeRoot);
        var simulations = ReadIntOption(_args, "--simulations", fallback: 1000, min: 1000, max: 10000);
        IReadOnlyList<ScalpingRobustnessExpansionReport> reports;
        if (_args.Any(arg => arg.Equals("--all-robust", StringComparison.OrdinalIgnoreCase)))
        {
            reports = service.ExpandAllRobust(simulations);
        }
        else
        {
            var id = ReadOption(_args, "--id");
            if (string.IsNullOrWhiteSpace(id))
            {
                WriteError("--id fehlt oder nutze --all-robust");
                WriteSafety();
                return 1;
            }

            reports = [service.Expand(id, simulations)];
        }

        WriteField("Reports", reports.Count.ToString());
        foreach (var report in reports)
        {
            WriteScalpingRobustnessReport(report);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingRobustnessReport()
    {
        WriteHeader("Hermes Scalping Robustness Report");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var report = new ScalpingRobustnessExpansionService(BuildStoragePaths(), _runtimeRoot).LoadReport(id);
        if (report is null)
        {
            WriteError($"Robustness Report nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteScalpingRobustnessReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingSensitivityReport()
    {
        WriteHeader("Hermes Scalping Sensitivity Report");
        var report = LoadRequiredScalpingRobustnessReport();
        if (report is null) return 1;
        WriteScalpingSensitivityDetails(report.ParameterSensitivity);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainScalpingBlocker()
    {
        WriteHeader("Hermes Scalping Blocker Explanation");
        var report = LoadRequiredScalpingRobustnessReport();
        if (report is null) return 1;
        WriteField("Candidate", report.CandidateId);
        WriteField("Status", report.Status.ToString());
        WriteField("Final Candidate", report.FinalCandidate.ToString().ToLowerInvariant());
        WriteMessages("Blockers", report.Blockers);
        WriteField("Primary Sensitivity Driver", report.ParameterSensitivity.StableCorridor.PrimaryConfidenceDropDriver);
        WriteField("Explainability", report.ParameterSensitivity.StableCorridor.ExplanationHealth);
        WriteMessages("Recommended Corridor", report.ParameterSensitivity.StableCorridor.RecommendedConservativeCorridor);
        foreach (var detail in report.ParameterSensitivity.Details.OrderBy(item => item.ConfidenceDelta).Take(3))
        {
            WriteSubHeader(detail.VariantLabel);
            WriteField("Parameter", detail.ParameterName);
            WriteField("Confidence Delta", $"{detail.ConfidenceDelta:0.####}");
            WriteField("Stability", detail.Stability);
            WriteField("Explanation", detail.Explanation);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingParameterCorridor()
    {
        WriteHeader("Hermes Scalping Parameter Corridor");
        var report = LoadRequiredScalpingRobustnessReport();
        if (report is null) return 1;
        var corridor = report.ParameterSensitivity.StableCorridor;
        WriteField("Candidate", report.CandidateId);
        WriteField("Stable Corridor Available", report.ParameterSensitivity.StableConservativeCorridorAvailable.ToString().ToLowerInvariant());
        WriteField("Primary Confidence Driver", corridor.PrimaryConfidenceDropDriver);
        WriteField("Explainability", corridor.ExplanationHealth);
        WriteMessages("Stable Ranges", corridor.StableParameterRanges);
        WriteMessages("Unstable Ranges", corridor.UnstableParameterRanges);
        WriteMessages("Recommended Conservative Corridor", corridor.RecommendedConservativeCorridor);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private ScalpingRobustnessExpansionReport? LoadRequiredScalpingRobustnessReport()
    {
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return null;
        }

        var service = new ScalpingRobustnessExpansionService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadReport(id) ?? service.Expand(id);
        return report;
    }

    private int ShowScalpingFinalCandidates()
    {
        WriteHeader("Hermes Scalping Final Candidates");
        var reports = new ScalpingRobustnessExpansionService(BuildStoragePaths(), _runtimeRoot).LoadReports();
        var finals = reports.Where(report => report.Status == ScalpingExpansionStatus.final_candidate).ToList();
        WriteField("Final Candidates", finals.Count.ToString());
        WriteField("Expanded", reports.Count(report => report.Status == ScalpingExpansionStatus.robustness_expanded).ToString());
        WriteField("Rejected After Expansion", reports.Count(report => report.Status == ScalpingExpansionStatus.rejected_after_expansion).ToString());
        foreach (var report in finals.Take(10))
        {
            WriteScalpingRobustnessReport(report);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunScalpingCertification()
    {
        WriteHeader("Hermes Scalping Certification");
        var service = new ScalpingCertificationService(BuildStoragePaths(), _runtimeRoot);
        IReadOnlyList<ScalpingCertificationReport> reports;
        if (_args.Any(arg => arg.Equals("--all-final", StringComparison.OrdinalIgnoreCase)))
        {
            reports = service.CertifyAllFinal();
        }
        else
        {
            var id = ReadOption(_args, "--id");
            if (string.IsNullOrWhiteSpace(id))
            {
                WriteError("--id fehlt oder nutze --all-final");
                WriteSafety();
                return 1;
            }

            reports = [service.Certify(id)];
        }

        WriteField("Reports", reports.Count.ToString());
        foreach (var report in reports)
        {
            WriteScalpingCertificationReport(report);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingCertificationReport()
    {
        WriteHeader("Hermes Scalping Certification Report");
        var report = LoadRequiredScalpingCertificationReport();
        if (report is null) return 1;
        WriteScalpingCertificationReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCertificationReport()
    {
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return ShowScalpingCertifiedCandidates();
        }

        return ShowScalpingCertificationReport();
    }

    private int ShowScalpingCertifiedCandidates()
    {
        WriteHeader("Hermes Scalping Certified Candidates");
        var reports = new ScalpingCertificationService(BuildStoragePaths(), _runtimeRoot).LoadReports();
        var certified = reports.Where(report => report.Status == ScalpingCertificationStatus.certified_candidate).ToList();
        WriteField("Certified Candidates", certified.Count.ToString());
        WriteField("Certification Failed", reports.Count(report => report.Status == ScalpingCertificationStatus.certification_failed).ToString());
        foreach (var report in certified.Take(10))
        {
            WriteScalpingCertificationReport(report);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCandidateAuditReport()
    {
        WriteHeader("Hermes Certified Candidate Audit Report");
        var asset = ReadOption(_args, "--asset");
        var service = new CertifiedCandidateAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.BuildReport(string.IsNullOrWhiteSpace(asset) ? "GER40" : asset);
        WriteField("Report Path", DisplayPath(service.ReportPath));
        WriteField("Markdown Path", DisplayPath(service.MarkdownPath));
        WriteField("Asset", report.Asset);
        WriteField("Audit Warnings", report.AuditWarnings.Count.ToString());
        foreach (var finding in report.Findings)
        {
            WriteSubHeader(finding.CandidateId);
            WriteField("Asset", finding.Asset);
            WriteField("Timeframe", finding.Timeframe);
            WriteField("Setup", finding.SetupType);
            WriteField("Direction", finding.Direction);
            WriteField("Certification Status", finding.CertificationStatus);
            WriteField("Indicators", finding.UsedIndicators);
            WriteField("Filters", finding.UsedFilters);
            WriteField("Session Filter", finding.SessionFilter);
            WriteField("Market Regime Filter", finding.MarketRegimeFilter);
            WriteField("Entry Rules", finding.EntryRules);
            WriteField("Exit Rules", finding.ExitRules);
            WriteField("Stop-Loss Logic", finding.StopLossLogic);
            WriteField("Take-Profit Logic", finding.TakeProfitLogic);
            WriteField("Invalidation Rules", finding.InvalidationRules);
            WriteField("Trades Total", finding.TradesTotal);
            WriteField("Trades Per Year", finding.TradesPerYear);
            WriteField("Trades Per Month", finding.TradesPerMonth);
            WriteField("Trades Per Week", finding.TradesPerWeek);
            WriteField("Average Holding Duration", finding.AverageHoldingDuration);
            WriteField("Win Rate", finding.WinRate);
            WriteField("Profit Factor", finding.ProfitFactor);
            WriteField("Expectancy", finding.Expectancy);
            WriteField("Sharpe", finding.Sharpe);
            WriteField("Sortino", finding.Sortino);
            WriteField("Max Drawdown", finding.MaxDrawdown);
            WriteField("Max Daily Drawdown", finding.MaxDailyDrawdown);
            WriteField("Risk Of Ruin", finding.RiskOfRuin);
            WriteField("Signal Density", finding.SignalDensity);
            WriteField("Walk Forward Status", finding.WalkForwardStatus);
            WriteField("OOS Status", finding.OosStatus);
            WriteField("Monte Carlo Status", finding.MonteCarloStatus);
            WriteField("Sensitivity Status", finding.SensitivityStatus);
            WriteField("Spread Stress Status", finding.SpreadStressStatus);
            WriteField("Slippage Stress Status", finding.SlippageStressStatus);
            WriteField("Certification Reason", finding.CertificationReason);
            WriteField("Metric Availability", finding.MetricAvailability);
            WriteMessages("Thresholds Met", finding.MinimumThresholdsMet);
            WriteMessages("Weaknesses", finding.Weaknesses);
            WriteMessages("Audit Warnings", finding.AuditWarnings);
            WriteField("Source Certification", DisplayPath(finding.SourceCertificationPath));
            WriteField("Source Expansion", DisplayPath(finding.SourceExpansionPath));
            WriteField("human_review_required", finding.HumanReviewRequired.ToString().ToLowerInvariant());
        }

        WriteMessages("Audit Warnings", report.AuditWarnings);
        WriteSafety();
        return 0;
    }

    private int ShowCandidateDetails()
    {
        WriteHeader("Hermes Candidate Details");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var research = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var candidate = research.FindCandidate(id);
        if (candidate is null)
        {
            WriteError($"Kandidat nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteField("Candidate", candidate.CandidateId);
        WriteField("Asset", candidate.Asset);
        WriteField("Timeframe", candidate.Timeframe);
        WriteField("Setup", candidate.SetupType);
        WriteField("Validation Status", candidate.ValidationStatus.ToString());
        WriteField("Confidence Score", candidate.ConfidenceScore.ToString("0.####"));
        WriteMessages("Entry Rules", candidate.EntryRules);
        WriteMessages("Exit Rules", candidate.ExitRules);
        WriteMessages("Stop Loss Rules", candidate.StopLossRules);
        WriteMessages("Take Profit Rules", candidate.TakeProfitRules);
        WriteField("Session Filter", candidate.SessionFilter);
        WriteField("Spread Filter", candidate.SpreadFilter);
        WriteField("News Filter", candidate.NewsFilterStub);
        WriteField("Risk Per Trade", candidate.RiskPerTrade.ToString("0.####"));
        WriteField("Max Daily Loss", candidate.MaxDailyLoss.ToString("0.####"));
        WriteField("Max Trades Per Day", candidate.MaxTradesPerDay.ToString());
        WriteField("Trades", candidate.Backtest.TradeCount.ToString());
        WriteField("Average Holding Duration", candidate.Backtest.AverageHoldingDurationMinutes?.ToString("0.##") ?? "not_captured");
        WriteField("Median Holding Duration", candidate.Backtest.MedianHoldingDurationMinutes?.ToString("0.##") ?? "not_captured");
        WriteField("Sharpe Ratio", candidate.Backtest.SharpeRatio?.ToString("0.####") ?? "not_captured");
        WriteField("Sortino Ratio", candidate.Backtest.SortinoRatio?.ToString("0.####") ?? "not_captured");
        WriteField("Signal Density / Month", candidate.Backtest.SignalDensityPerMonth?.ToString("0.##") ?? "not_captured");
        WriteField("Signal Density / Week", candidate.Backtest.SignalDensityPerWeek?.ToString("0.##") ?? "not_captured");
        WriteField("Average R", candidate.Backtest.AverageR?.ToString("0.####") ?? "not_captured");
        WriteField("Expectancy R", candidate.Backtest.ExpectancyR?.ToString("0.####") ?? "not_captured");
        WriteField("Max Consecutive Losses", candidate.Backtest.MaxConsecutiveLosses?.ToString() ?? "not_captured");
        WriteField("Max Consecutive Wins", candidate.Backtest.MaxConsecutiveWins?.ToString() ?? "not_captured");
        WriteField("OOS Net R", candidate.Backtest.OosNetR.ToString("0.####"));
        WriteField("Walk Forward Net R", candidate.Backtest.WalkForwardNetR.ToString("0.####"));
        WriteField("Profit Factor", candidate.Backtest.ProfitFactor.ToString("0.####"));
        WriteField("Win Rate", candidate.Backtest.WinRate.ToString("0.####"));
        WriteField("Max Drawdown R", candidate.Backtest.MaxDrawdownR.ToString("0.####"));
        WriteField("Cost Stress Net R", candidate.Backtest.CostStressNetR.ToString("0.####"));
        WriteField("Monte Carlo Median Drawdown R", candidate.RiskProfile.MonteCarloMedianDrawdownR.ToString("0.####"));
        WriteField("Monte Carlo P95 Drawdown R", candidate.RiskProfile.MonteCarloP95DrawdownR.ToString("0.####"));
        WriteField("Risk Of Ruin", candidate.RiskProfile.RiskOfRuinProbability.ToString("0.####"));
        WriteField("no_auto_trading", candidate.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", candidate.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", candidate.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", candidate.LiveTradingEnabled.ToString().ToLowerInvariant());
        WriteSafety();
        return 0;
    }

    private int ShowCertifiedCandidateInventory()
    {
        WriteHeader("Hermes Certified Candidate Inventory");
        var service = new CertifiedCandidateInventoryService(BuildStoragePaths(), _runtimeRoot);
        var inventory = service.LoadInventory() ?? service.BuildInventory();
        WriteField("Inventory Path", DisplayPath(service.InventoryPath));
        WriteField("Items", inventory.Items.Count.ToString());
        foreach (var group in inventory.Items.GroupBy(item => item.Asset, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteSubHeader(group.Key);
            foreach (var item in group.OrderByDescending(item => item.QualityScore ?? 0).ThenBy(item => item.CandidateId, StringComparer.OrdinalIgnoreCase))
            {
                WriteField("Candidate", item.CandidateId);
                WriteField("Timeframe", item.Timeframe);
                WriteField("Setup", item.SetupType);
                WriteField("Direction", item.Direction);
                WriteField("Certification Status", item.CertificationStatus);
                WriteField("Quality Score", item.QualityScore?.ToString("0.####") ?? "-");
                WriteField("Trust Score", item.TrustScore?.ToString("0.####") ?? "-");
                WriteField("Profit Factor", item.ProfitFactor?.ToString("0.####") ?? "-");
                WriteField("Win Rate", item.WinRate?.ToString("0.####") ?? "-");
                WriteField("Max Drawdown R", item.MaxDrawdownR.ToString("0.####"));
                WriteField("Max Daily Drawdown R", item.MaxDailyDrawdownR.ToString("0.####"));
                WriteField("Risk Of Ruin", item.RiskOfRuin.ToString("0.####"));
                WriteField("Signal Density", item.SignalDensity.ToString("0.####"));
                WriteField("Stability", item.StabilityStatus);
                WriteField("Source Report", DisplayPath(item.SourceReportPath));
                WriteField("Human Review Required", item.HumanReviewRequired.ToString().ToLowerInvariant());
            }
        }

        WriteSafety();
        return 0;
    }

    private int ShowSetupRegistry()
    {
        WriteHeader("Hermes Setup Registry");
        var asset = ReadOption(_args, "--asset");
        var service = new CertifiedCandidateInventoryService(BuildStoragePaths(), _runtimeRoot);
        var registry = service.LoadRegistry();
        if (registry is null)
        {
            WriteWarning("setup_registry_missing");
            WriteSafety();
            return 0;
        }
        WriteField("Registry Path", DisplayPath(service.SetupRegistryPath));
        WriteField("Assets", string.Join(", ", registry.SetupCountsByAsset.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        var entries = string.IsNullOrWhiteSpace(asset)
            ? registry.Assets
            : registry.Assets.Where(entry => entry.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var entry in entries)
        {
            WriteSubHeader(entry.SetupId);
            WriteField("Asset", entry.Asset);
            WriteField("Primary Timeframe", entry.PrimaryTimeframe);
            WriteField("Setup Type", entry.SetupType);
            WriteField("Allowed Directions", string.Join(", ", entry.AllowedDirections));
            WriteField("Primary Candidate", entry.PrimaryCandidate);
            WriteMessages("Backup Candidates", entry.BackupCandidates);
            WriteMessages("Market Regime Tags", entry.MarketRegimeTags);
            WriteMessages("Session Tags", entry.SessionTags);
            WriteField("Confidence Baseline", entry.ConfidenceBaseline.ToString("0.####"));
            WriteField("Average Quality Score", entry.AverageQualityScore.ToString("0.####"));
            WriteField("Average Profit Factor", entry.AverageProfitFactor.ToString("0.####"));
            WriteField("Average Win Rate", entry.AverageWinRate.ToString("0.####"));
            WriteField("Average Max Drawdown R", entry.AverageMaxDrawdownR.ToString("0.####"));
            WriteField("Average Risk Of Ruin", entry.AverageRiskOfRuin.ToString("0.####"));
            WriteField("Expected Signal Frequency", entry.ExpectedSignalFrequency);
            WriteField("Trade Count Range", entry.TradeCountRange);
            WriteField("Minimum Member Trade Count", entry.MinimumMemberTradeCount.ToString());
            WriteField("Maximum Member Trade Count", entry.MaximumMemberTradeCount.ToString());
            WriteField("Risk Profile", entry.RiskProfile);
            WriteField("Readiness Status", entry.ReadinessStatus);
            WriteField("human_review_required", entry.HumanReviewRequired.ToString().ToLowerInvariant());
            WriteField("no_auto_trading", entry.NoAutoTrading.ToString().ToLowerInvariant());
        }

        WriteSafety();
        return 0;
    }

    private int ExplainSetupSelection()
    {
        WriteHeader("Hermes Setup Selection Explanation");
        var asset = ReadOption(_args, "--asset");
        if (string.IsNullOrWhiteSpace(asset))
        {
            WriteError("--asset fehlt");
            WriteSafety();
            return 1;
        }

        var timeframe = ReadOption(_args, "--timeframe");
        var service = new CertifiedCandidateInventoryService(BuildStoragePaths(), _runtimeRoot);
        WriteField("Asset", asset.ToUpperInvariant());
        WriteField("Timeframe", string.IsNullOrWhiteSpace(timeframe) ? "-" : timeframe.ToUpperInvariant());
        WriteField("Explanation", service.ExplainSelection(asset, timeframe));
        WriteSafety();
        return 0;
    }

    private int ShowScalpingHumanReviewPackage()
    {
        WriteHeader("Hermes Scalping Human Review Package");
        var report = LoadRequiredScalpingCertificationReport();
        if (report is null) return 1;
        WriteField("Candidate", report.CandidateId);
        WriteField("Package", DisplayPath(report.HumanReviewPackagePath));
        if (File.Exists(report.HumanReviewPackagePath))
        {
            foreach (var line in File.ReadLines(report.HumanReviewPackagePath).Take(80))
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private ScalpingCertificationReport? LoadRequiredScalpingCertificationReport()
    {
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return null;
        }

        var service = new ScalpingCertificationService(BuildStoragePaths(), _runtimeRoot);
        return service.LoadReport(id) ?? service.Certify(id);
    }

    private int ExportScalpingBotSpec()
    {
        WriteHeader("Hermes Scalping cTrader Bot Spec Export");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        try
        {
            var result = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot).ExportCTraderBotSpec(id);
            WriteField("JSON", DisplayPath(result.JsonPath));
            WriteField("Markdown", DisplayPath(result.MarkdownPath));
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
    }

    private int ShowScalpingBotSpec()
    {
        WriteHeader("Hermes Scalping cTrader Bot Spec");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var path = Path.Combine(BuildStoragePaths().Root, "reports", "scalping_bot_specs", id, "ctrader_bot_spec.md");
        if (!File.Exists(path))
        {
            WriteError($"ctrader_bot_spec_missing:{id}");
            WriteSafety();
            return 1;
        }

        WriteField("Markdown", DisplayPath(path));
        foreach (var line in File.ReadLines(path).Take(140))
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingBotSpecs()
    {
        WriteHeader("Hermes Scalping cTrader Bot Specs");
        var root = Path.Combine(BuildStoragePaths().Root, "reports", "scalping_bot_specs");
        var specs = Directory.Exists(root)
            ? Directory.GetFiles(root, "ctrader_bot_spec.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).ToList()
            : [];
        WriteField("Specs Ready", specs.Count.ToString());
        foreach (var spec in specs.Take(20))
        {
            WriteField(Path.GetFileName(Path.GetDirectoryName(spec)) ?? "candidate", DisplayPath(spec));
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExportSignalAgentSpec()
    {
        WriteHeader("Hermes Signal Agent Spec Export");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        try
        {
            var result = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot).ExportSignalAgentSpec(id);
            WriteField("JSON", DisplayPath(result.JsonPath));
            WriteField("Markdown", DisplayPath(result.MarkdownPath));
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
    }

    private int ShowSignalAgentSpec()
    {
        WriteHeader("Hermes Signal Agent Spec");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var path = Path.Combine(BuildStoragePaths().Root, "reports", "signal_agent_specs", id, "signal_agent_spec.md");
        if (!File.Exists(path))
        {
            WriteError($"signal_agent_spec_missing:{id}");
            WriteSafety();
            return 1;
        }

        WriteField("Markdown", DisplayPath(path));
        foreach (var line in File.ReadLines(path).Take(120))
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSignalAgentSpecs()
    {
        WriteHeader("Hermes Signal Agent Specs");
        var service = new ScalpingResearchService(BuildStoragePaths(), _runtimeRoot);
        var roots = new[]
        {
            service.SignalSpecDirectory,
            Path.Combine(BuildStoragePaths().Root, "reports", "signal_agent_specs"),
            Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_agent_specs")
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(Directory.Exists)
        .ToList();
        var specs = roots
            .SelectMany(root => Directory.GetFiles(root, "signal_agent_spec.json", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        WriteField("Specs Ready", specs.Count.ToString());
        foreach (var spec in specs.Take(20))
        {
            WriteField(Path.GetFileName(Path.GetDirectoryName(spec)) ?? "candidate", DisplayPath(spec));
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int BuildScalpingPortfolio()
    {
        WriteHeader("Hermes Scalping Portfolio Build");
        var portfolio = new ScalpingPortfolioService(BuildStoragePaths(), _runtimeRoot).Build();
        WriteScalpingPortfolio(portfolio);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingPortfolioStatus()
    {
        WriteHeader("Hermes Scalping Portfolio Status");
        var service = new ScalpingPortfolioService(BuildStoragePaths(), _runtimeRoot);
        var portfolio = service.Load() ?? service.Build();
        WriteScalpingPortfolio(portfolio);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingEnsemblePlan()
    {
        WriteHeader("Hermes Scalping Ensemble Plan");
        var service = new ScalpingPortfolioService(BuildStoragePaths(), _runtimeRoot);
        var portfolio = service.Load() ?? service.Build();
        WriteField("Status", portfolio.EnsemblePlan.Status);
        WriteMessages("Candidate Selection Rules", portfolio.EnsemblePlan.CandidateSelectionRules);
        WriteMessages("Correlation Controls", portfolio.EnsemblePlan.CorrelationControls);
        WriteMessages("Readiness Gates", portfolio.EnsemblePlan.EnsembleReadinessGates);
        WriteMessages("Next Actions", portfolio.EnsemblePlan.NextActions);
        WriteMessages("Safety Rules", portfolio.EnsemblePlan.SafetyRules);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingPortfolioCandidates()
    {
        WriteHeader("Hermes Scalping Portfolio Candidates");
        var service = new ScalpingPortfolioService(BuildStoragePaths(), _runtimeRoot);
        var portfolio = service.Load() ?? service.Build();
        WriteField("Members", portfolio.Members.Count.ToString());
        foreach (var member in portfolio.Members.OrderByDescending(item => item.Status == ScalpingCertificationStatus.certified_candidate.ToString()).ThenByDescending(item => item.DiversityScore).Take(30))
        {
            WriteSubHeader(member.CandidateId);
            WriteField("Asset/Timeframe", $"{member.Asset}/{member.Timeframe}");
            WriteField("Setup", member.SetupType);
            WriteField("Status", member.Status);
            WriteField("Confidence", $"{member.Confidence:0.####}");
            WriteField("Profit Factor", $"{member.ProfitFactor:0.####}");
            WriteField("Recovery Factor", $"{member.RecoveryFactor:0.####}");
            WriteField("Max Drawdown", $"{member.MaxDrawdown:0.####}");
            WriteField("Diversity", $"{member.DiversityScore:0.####}");
            WriteField("Signal Density", $"{member.SignalDensityScore:0.####}");
            WriteField("Correlation Group", member.CorrelationGroup);
            WriteField("Ensemble Readiness", member.EnsembleReadiness);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowEnsemblePortfolioStatus()
    {
        WriteHeader("Hermes Ensemble Portfolio Status");
        var service = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Load();
        if (report is null)
        {
            WriteWarning("ensemble_portfolio_missing");
            WriteSafety();
            return 0;
        }
        WriteField("Portfolio Readiness", report.PortfolioReadiness);
        WriteField("Assets", string.Join(", ", report.Assets));
        WriteField("Setup Count Total", report.SetupCountTotal.ToString());
        WriteField("Certified Candidate Count Total", report.CertifiedCandidateCountTotal.ToString());
        WriteField("Signal Spec Count Total", report.SignalSpecCountTotal.ToString());
        WriteMessages("Warnings", report.Warnings);
        foreach (var entry in report.Entries)
        {
            WriteSubHeader(entry.Asset);
            WriteField("Setup Count", entry.SetupCount.ToString());
            WriteField("Certified Candidate Count", entry.CertifiedCandidateCount.ToString());
            WriteField("Signal Spec Count", entry.SignalSpecCount.ToString());
            WriteField("Primary Setup", entry.PrimarySetup);
            WriteMessages("Backup Setups", entry.BackupSetups);
            WriteField("Primary Candidate", entry.PrimaryCandidate);
            WriteMessages("Backup Candidates", entry.BackupCandidates);
            WriteField("Confidence Baseline", $"{entry.ConfidenceBaseline:0.####}");
            WriteField("Readiness", entry.Readiness);
            WriteField("Portfolio Readiness", entry.PortfolioReadiness);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainEnsembleSelection()
    {
        WriteHeader("Hermes Ensemble Selection Explanation");
        var asset = ReadOption(_args, "--asset");
        var service = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Load();
        if (report is null)
        {
            WriteWarning("ensemble_portfolio_missing");
            WriteSafety();
            return 0;
        }
        if (string.IsNullOrWhiteSpace(asset))
        {
            foreach (var entry in report.Entries.OrderByDescending(item => item.ConfidenceBaseline))
            {
                WriteSubHeader(entry.Asset);
                WriteField("Selected", entry.PrimarySetup);
                WriteMessages("Backups", entry.BackupSetups);
                WriteField("Reason", service.ExplainSelection(entry.Asset));
            }
        }
        else
        {
            var entry = report.Entries.FirstOrDefault(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                WriteError($"ensemble_asset_not_found:{asset}");
                WriteSafety();
                return 1;
            }

            WriteField("Asset", entry.Asset);
            WriteField("Selected", entry.PrimarySetup);
            WriteMessages("Backups", entry.BackupSetups);
            WriteField("Reason", service.ExplainSelection(entry.Asset));
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int SearchMoreScalpingCandidates()
    {
        WriteHeader("Hermes Search More Scalping Candidates");
        var asset = ReadOption(_args, "--asset") ?? ScalpingResearchService.DefaultAsset;
        var maxVariants = ReadIntOption(_args, "--max-variants", fallback: 100, min: 1, max: 500);
        var service = new ScalpingPortfolioService(BuildStoragePaths(), _runtimeRoot);
        var report = service.SearchMoreCandidates(asset, maxVariants);
        WriteField("Asset", report.Asset);
        WriteField("Variants Tested", report.VariantsTested.ToString());
        WriteField("Candidates", report.CandidatesTotal.ToString());
        WriteField("Robust Candidates", report.RobustCandidates.ToString());
        WriteField("Rejected", report.RejectedCandidates.ToString());
        WriteField("Needs More Data", report.NeedsMoreData.ToString());
        WriteField("Best Candidate", report.BestCandidateId ?? "-");
        WriteMessages("Target Setups", ["XAUUSD range_breakout", "XAUUSD ema_pullback", "XAUUSD liquidity_rejection", "XAUUSD micro_trend_continuation", "EURUSD optional when data available"]);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int UpdateScalpingMultiAssetRoadmap()
    {
        WriteHeader("Hermes Scalping Multi-Asset Roadmap Update");
        var roadmap = new ScalpingMultiAssetRoadmapService(BuildStoragePaths(), _runtimeRoot).Update();
        WriteScalpingMultiAssetRoadmap(roadmap);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingMultiAssetRoadmap()
    {
        WriteHeader("Hermes Scalping Multi-Asset Roadmap");
        var service = new ScalpingMultiAssetRoadmapService(BuildStoragePaths(), _runtimeRoot);
        var roadmap = service.Update();
        WriteScalpingMultiAssetRoadmap(roadmap);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingAssetStatus()
    {
        WriteHeader("Hermes Scalping Asset Status");
        var asset = ReadOption(_args, "--asset");
        if (string.IsNullOrWhiteSpace(asset))
        {
            WriteError("--asset fehlt");
            WriteSafety();
            return 1;
        }

        var readiness = new ScalpingAssetReadinessService(BuildStoragePaths(), _runtimeRoot).Evaluate(asset);
        WriteField("Asset", readiness.Asset);
        WriteField("Historical Data Status", readiness.HistoricalDataStatus);
        WriteField("Quote Status", readiness.QuoteStatus);
        WriteField("Research Status", readiness.ResearchStatus);
        WriteField("Signal Agent Spec Status", readiness.SignalAgentSpecStatus);
        WriteField("Readiness Status", readiness.AssetStatus);
        WriteField("Setup Ready Status", readiness.SetupReadyStatus);
        WriteField("Bot Ready Status", readiness.BotReadyStatus);
        WriteField("Setup Count", readiness.SetupCount.ToString());
        WriteField("Best Setup", readiness.BestSetup);
        WriteField("Candidates Total", readiness.CandidatesTotal.ToString());
        WriteField("Robust Candidates", readiness.RobustCandidates.ToString());
        WriteField("Final Candidates", readiness.FinalCandidates.ToString());
        WriteField("Certified Candidates", readiness.CertifiedCandidates.ToString());
        WriteMessages("Timeframes", readiness.Timeframes);
        WriteMessages("Warnings", readiness.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int OptimizeScalpingEnsemble()
    {
        WriteHeader("Hermes Scalping Ensemble Optimizer");
        var modeValue = ReadOption(_args, "--mode") ?? ScalpingEnsembleOptimizationMode.balanced.ToString();
        if (!Enum.TryParse<ScalpingEnsembleOptimizationMode>(modeValue, ignoreCase: true, out var mode))
        {
            WriteError($"invalid_optimizer_mode:{modeValue}");
            WriteSafety();
            return 1;
        }

        var report = new ScalpingEnsembleOptimizerService(BuildStoragePaths(), _runtimeRoot).Optimize(mode);
        WriteScalpingOptimizerReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingEnsembleOptimized()
    {
        WriteHeader("Hermes Scalping Optimized Ensemble");
        var report = new ScalpingEnsembleOptimizerService(BuildStoragePaths(), _runtimeRoot).LoadReport();
        if (report is null)
        {
            WriteError("scalping_ensemble_optimizer_report_missing");
            WriteSafety();
            return 1;
        }

        WriteScalpingOptimizerReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingEnsembleMember()
    {
        WriteHeader("Hermes Scalping Optimized Ensemble Member");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteError("--id fehlt");
            WriteSafety();
            return 1;
        }

        var member = new ScalpingEnsembleOptimizerService(BuildStoragePaths(), _runtimeRoot).FindMember(id);
        if (member is null)
        {
            WriteError($"optimized_ensemble_member_not_found:{id}");
            WriteSafety();
            return 1;
        }

        WriteScalpingOptimizedMember(member);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExportScalpingEnsemblePackage()
    {
        WriteHeader("Hermes Scalping Ensemble Export Package");
        try
        {
            var result = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot).Export();
            WriteField("Package ID", result.PackageId);
            WriteField("Status", result.Status);
            WriteField("JSON", DisplayPath(new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot).PackagePath));
            WriteField("Markdown", DisplayPath(new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot).PackageMarkdownPath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
    }

    private int ShowScalpingEnsemblePackage()
    {
        WriteHeader("Hermes Scalping Ensemble Package");
        var service = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot);
        var package = service.LoadPackage();
        if (package is null)
        {
            WriteError("scalping_ensemble_package_missing");
            WriteSafety();
            return 1;
        }

        WriteField("Package ID", package.PackageId);
        WriteField("Package Version", package.PackageVersion);
        WriteField("Generated At", package.GeneratedAtUtc.ToString("O"));
        WriteField("Source System", package.SourceSystem);
        WriteField("Status", package.Status);
        WriteField("Assets", package.Assets.Count.ToString());
        WriteMessages("Asset List", package.Assets.Select(asset => asset.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(asset => asset).ToList());
        WriteMessages("Safety Flags", package.SafetyFlags);
        WriteField("JSON", DisplayPath(service.PackagePath));
        WriteField("Markdown", DisplayPath(service.PackageMarkdownPath));
        WriteField("no_auto_trading", package.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", package.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ValidateEnsembleSignalPackage()
    {
        WriteHeader("Hermes Validate Ensemble Signal Package");
        var service = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot);
        var package = service.LoadPackage();
        if (package is null)
        {
            WriteError("ensemble_signal_agent_package_missing");
            WriteSafety();
            return 1;
        }

        var blockers = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(package.PackageId)) blockers.Add("package_id_missing");
        if (string.IsNullOrWhiteSpace(package.PackageVersion)) blockers.Add("package_version_missing");
        if (package.Assets.Count == 0) blockers.Add("assets_missing");
        if (!package.NoAutoTrading) blockers.Add("no_auto_trading_must_be_true");
        if (!package.HumanReviewRequired) blockers.Add("human_review_required_must_be_true");
        if (package.BrokerOrdersEnabled) blockers.Add("broker_orders_enabled_must_be_false");
        if (package.LiveTradingEnabled) blockers.Add("live_trading_enabled_must_be_false");
        if (!package.ResearchOnly) blockers.Add("research_only_must_be_true");

        foreach (var asset in package.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Asset)) blockers.Add("asset_missing");
            if (string.IsNullOrWhiteSpace(asset.SetupId)) blockers.Add($"setup_id_missing:{asset.Asset}");
            if (string.IsNullOrWhiteSpace(asset.SetupName)) warnings.Add($"setup_name_missing:{asset.Asset}");
            if (string.IsNullOrWhiteSpace(asset.Timeframe)) blockers.Add($"timeframe_missing:{asset.Asset}");
            if (string.IsNullOrWhiteSpace(asset.Direction)) warnings.Add($"direction_missing:{asset.Asset}");
            if (string.IsNullOrWhiteSpace(asset.PrimaryCandidate)) blockers.Add($"primary_candidate_missing:{asset.Asset}");
            if (asset.BackupCandidates is null) blockers.Add($"backup_candidates_missing:{asset.Asset}");
            if (asset.ConfidenceBaseline < 0) warnings.Add($"confidence_baseline_invalid:{asset.Asset}");
            if (string.IsNullOrWhiteSpace(asset.SignalFrequency)) warnings.Add($"signal_frequency_missing:{asset.Asset}");
            if (asset.EntryLogic is null || asset.EntryLogic.Count == 0) warnings.Add($"entry_logic_missing:{asset.Asset}");
            if (asset.ExitLogic is null || asset.ExitLogic.Count == 0) warnings.Add($"exit_logic_missing:{asset.Asset}");
            if (asset.StopLossLogic is null || asset.StopLossLogic.Count == 0) warnings.Add($"stop_loss_logic_missing:{asset.Asset}");
            if (asset.TakeProfitLogic is null || asset.TakeProfitLogic.Count == 0) warnings.Add($"take_profit_logic_missing:{asset.Asset}");
            if (asset.InvalidationLogic is null || asset.InvalidationLogic.Count == 0) warnings.Add($"invalidation_logic_missing:{asset.Asset}");
            if (asset.MarketRegimeTags is null) warnings.Add($"market_regime_tags_missing:{asset.Asset}");
            if (asset.SessionTags is null) warnings.Add($"session_tags_missing:{asset.Asset}");
            if (asset.RiskNotes is null || asset.RiskNotes.Count == 0) warnings.Add($"risk_notes_missing:{asset.Asset}");
            if (!asset.NoAutoTrading || !asset.HumanReviewRequired || asset.BrokerOrdersEnabled || asset.LiveTradingEnabled)
            {
                blockers.Add($"asset_safety_flags_invalid:{asset.Asset}");
            }

            if (asset.Readiness is "needs_more_validation" or "missing_data" or "data_ready_only" or "quote_mapping_pending")
            {
                warnings.Add($"asset_not_tradeable:{asset.Asset}:{asset.Readiness}");
            }
        }

        WriteField("Package ID", package.PackageId);
        WriteField("Package Version", package.PackageVersion);
        WriteField("Status", package.Status);
        WriteField("Assets", package.Assets.Count.ToString());
        WriteMessages("Warnings", warnings);
        WriteMessages("Blockers", blockers);
        WriteField("Validation", blockers.Count == 0 ? "ok" : "failed");
        WriteField("JSON", DisplayPath(service.PackagePath));
        WriteField("Markdown", DisplayPath(service.PackageMarkdownPath));
        Console.WriteLine();
        WriteSafety();
        return blockers.Count > 0 ? 1 : 0;
    }

    private int ShowSystemBHandoffBundle()
    {
        WriteHeader("Hermes System B Handoff Bundle");
        var service = new SystemBHandoffBundleService(BuildStoragePaths(), _runtimeRoot);
        var manifest = service.Export();
        var portfolio = new ScalpingEnsemblePortfolioService(BuildStoragePaths(), _runtimeRoot).Load();

        WriteField("Bundle Path", DisplayPath(service.ResolveBundlePath()));
        WriteField("Files Included", manifest.IncludedFiles.Count.ToString());
        WriteField("Portfolio Status", portfolio?.PortfolioReadiness ?? "unknown");
        WriteField("Asset Count", portfolio?.Assets.Count.ToString() ?? "0");
        WriteField("Safety Validation", service.ValidateSafety() ?? "ok");
        WriteMessages("Files", manifest.IncludedFiles);
        WriteMessages("Safety Flags", new[]
        {
            "no_auto_trading=true",
            "human_review_required=true",
            "broker_orders_enabled=false",
            "live_trading_enabled=false",
            "research_only=true"
        });
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCloudEmbeddedReleasePackage()
    {
        WriteHeader("Hermes Cloud Embedded Release Package");
        var service = new CloudEmbeddedReleasePackageGeneratorService(BuildStoragePaths(), _runtimeRoot);
        var result = service.Generate();

        WriteField("Status", result.Status);
        WriteField("Reason", result.Reason);
        WriteField("Source Bundle", DisplayPath(result.SourceBundleDirectory));
        WriteField("Output JSON", DisplayPath(result.OutputJsonPath));
        WriteField("Output Markdown", DisplayPath(result.OutputMarkdownPath));
        WriteField("Bot Release ID", result.BotReleaseId);
        WriteField("Bot Version", result.BotVersion);
        WriteField("Strategy Package Version", result.StrategyPackageVersion);
        WriteField("Schema Version", result.SchemaVersion);
        WriteField("Release Mode", result.ReleaseMode);
        WriteField("Embedded Checksum", result.EmbeddedChecksum);
        WriteSafety();
        return result.Success ? 0 : 1;
    }

    private int ShowHermesPaperBotReplay()
    {
        WriteHeader("HermesPaperBot Replay Runner");
        var runner = new HermesPaperBotReplayRunner();
        var outputDirectory = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "hermes_paper_bot_replay");
        var datasetPath = ReadOption(_args, "--dataset");
        var asset = ReadOption(_args, "--asset");
        var timeframe = ReadOption(_args, "--timeframe");
        var result = runner.Run(outputDirectory, datasetPath, asset, timeframe);

        WriteField("Status", result.Status);
        WriteField("Reason", result.Reason);
        WriteField("Output Directory", DisplayPath(result.OutputDirectory));
        WriteField("JSON Report", DisplayPath(result.JsonPath));
        WriteField("Markdown Report", DisplayPath(result.MarkdownPath));
        WriteField("Dataset Path", DisplayOptionalPath(result.DatasetPath));
        WriteField("Dataset Discovery Used", result.DatasetDiscoveryUsed.ToString().ToLowerInvariant());
        WriteField("Dataset Discovery Candidates", result.DatasetDiscoveryCandidates.ToString(CultureInfo.InvariantCulture));
        WriteField("Selected Dataset Path", DisplayOptionalPath(result.SelectedDatasetPath));
        WriteField("Bars Total", result.BarsTotal.ToString(CultureInfo.InvariantCulture));
        WriteField("Bars Valid", result.BarsValid.ToString(CultureInfo.InvariantCulture));
        WriteField("Bars Skipped", result.BarsSkipped.ToString(CultureInfo.InvariantCulture));
        WriteField("Trades Total", result.TradesTotal.ToString(CultureInfo.InvariantCulture));
        WriteField("Sample Size Class", result.SampleSizeClass);
        WriteField("Quality Class", result.QualityClass);
        WriteField("Broker Action", result.BrokerAction);
        WriteField("Paper Mode Allowed", result.PaperModeAllowed.ToString().ToLowerInvariant());
        WriteMessages("Warnings", result.Warnings);
        Console.WriteLine();
        WriteSafety();
        return result.Success ? 0 : 1;
    }

    private int ShowScalpingEnsembleHumanReviewPackage()
    {
        WriteHeader("Hermes Scalping Ensemble Human Review Package");
        var path = new ScalpingEnsembleExportService(BuildStoragePaths(), _runtimeRoot).HumanReviewPackagePath;
        if (!File.Exists(path))
        {
            WriteError("scalping_ensemble_human_review_package_missing");
            WriteSafety();
            return 1;
        }

        WriteField("Package", DisplayPath(path));
        foreach (var line in File.ReadLines(path).Take(140))
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowScalpingEnsembleReviewStatus()
    {
        WriteHeader("Hermes Scalping Ensemble Review Status");
        var state = new ScalpingEnsembleReviewService(BuildStoragePaths(), _runtimeRoot).LoadOrCreate();
        WriteScalpingEnsembleReviewState(state);
        WriteEnsembleApprovalScope();
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ApproveScalpingEnsemble()
    {
        WriteHeader("Hermes Scalping Ensemble Approve");
        var mode = ReadOption(_args, "--mode");
        if (string.IsNullOrWhiteSpace(mode))
        {
            WriteError("--mode fehlt");
            WriteSafety();
            return 1;
        }

        try
        {
            var state = new ScalpingEnsembleReviewService(BuildStoragePaths(), _runtimeRoot).Approve(mode);
            WriteScalpingEnsembleReviewState(state);
            WriteEnsembleApprovalScope();
            Console.WriteLine();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteEnsembleApprovalScope();
            WriteSafety();
            return 1;
        }
    }

    private int RejectScalpingEnsemble()
    {
        WriteHeader("Hermes Scalping Ensemble Reject");
        var reason = ReadOption(_args, "--reason");
        try
        {
            var state = new ScalpingEnsembleReviewService(BuildStoragePaths(), _runtimeRoot).Reject(reason ?? string.Empty);
            WriteScalpingEnsembleReviewState(state);
            WriteEnsembleApprovalScope();
            Console.WriteLine();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteEnsembleApprovalScope();
            WriteSafety();
            return 1;
        }
    }

    private int DeferScalpingEnsemble()
    {
        WriteHeader("Hermes Scalping Ensemble Defer");
        var reason = ReadOption(_args, "--reason");
        try
        {
            var state = new ScalpingEnsembleReviewService(BuildStoragePaths(), _runtimeRoot).Defer(reason ?? string.Empty);
            WriteScalpingEnsembleReviewState(state);
            WriteEnsembleApprovalScope();
            Console.WriteLine();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteEnsembleApprovalScope();
            WriteSafety();
            return 1;
        }
    }

    private int RequestMoreScalpingEvidence()
    {
        WriteHeader("Hermes Scalping Ensemble Request More Evidence");
        var reason = ReadOption(_args, "--reason");
        try
        {
            var state = new ScalpingEnsembleReviewService(BuildStoragePaths(), _runtimeRoot).RequestMoreEvidence(reason ?? string.Empty);
            WriteScalpingEnsembleReviewState(state);
            WriteEnsembleApprovalScope();
            Console.WriteLine();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteEnsembleApprovalScope();
            WriteSafety();
            return 1;
        }
    }

    private int ShowDemoSignalFeedStatus()
    {
        WriteHeader("Hermes Demo Signal Feed Status");
        var snapshot = new DemoSignalFeedService(BuildStoragePaths(), _runtimeRoot).LoadOrCreateStatus();
        WriteDemoSignalFeedSnapshot(snapshot);
        Console.WriteLine();
        WriteDemoSignalFeedSafety();
        WriteSafety();
        return 0;
    }

    private int GenerateDemoSignals()
    {
        WriteHeader("Hermes Generate Demo Signals");
        var service = new DemoSignalFeedService(BuildStoragePaths(), _runtimeRoot);
        var snapshot = service.Generate();
        WriteDemoSignalFeedSnapshot(snapshot);
        var signals = service.LoadLatestSignals();
        if (signals.Count > 0)
        {
            Console.WriteLine();
            WriteDemoSignal(signals[0]);
        }

        Console.WriteLine();
        WriteDemoSignalFeedSafety();
        WriteSafety();
        return snapshot.Blockers.Count > 0 ? 1 : 0;
    }

    private int ShowLatestDemoSignals()
    {
        WriteHeader("Hermes Latest Demo Signals");
        var service = new DemoSignalFeedService(BuildStoragePaths(), _runtimeRoot);
        var signals = service.LoadLatestSignals();
        if (signals.Count == 0)
        {
            WriteWarning("Keine Demo-Signale vorhanden.");
        }
        else
        {
            foreach (var signal in signals)
            {
                WriteDemoSignal(signal);
            }
        }

        WriteField("JSON", DisplayPath(service.LatestSignalsJsonPath));
        WriteField("Markdown", DisplayPath(service.LatestSignalsMarkdownPath));
        Console.WriteLine();
        WriteDemoSignalFeedSafety();
        WriteSafety();
        return 0;
    }

    private int ShowDemoSignalFeedLog()
    {
        WriteHeader("Hermes Demo Signal Feed Log");
        var service = new DemoSignalFeedService(BuildStoragePaths(), _runtimeRoot);
        if (!File.Exists(service.LogPath))
        {
            WriteWarning("Demo-Signal-Feed-Log nicht vorhanden.");
        }
        else
        {
            WriteField("Log", DisplayPath(service.LogPath));
            foreach (var line in File.ReadLines(service.LogPath).TakeLast(20))
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine();
        WriteDemoSignalFeedSafety();
        WriteSafety();
        return 0;
    }

    private int ShowSignalWatchStatus()
    {
        WriteHeader("Hermes Signal Watch Status");
        var service = new SignalWatchService(BuildStoragePaths(), _runtimeRoot);
        var snapshot = service.LoadOrCreateStatus();
        var evaluations = service.LoadLatestEvaluations();
        WriteSignalWatchStatus(snapshot);
        foreach (var evaluation in evaluations.Take(10))
        {
            WriteSignalWatchEvaluation(evaluation);
        }

        Console.WriteLine();
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int ShowSignalWatchLog()
    {
        WriteHeader("Hermes Signal Watch Log");
        var service = new SignalWatchService(BuildStoragePaths(), _runtimeRoot);
        if (!File.Exists(service.LogPath))
        {
            WriteWarning("Signal-Watch-Log nicht vorhanden.");
        }
        else
        {
            WriteField("Log", DisplayPath(service.LogPath));
            foreach (var line in File.ReadLines(service.LogPath).TakeLast(20))
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine();
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int ExportMissingSignalAgentSpecs()
    {
        WriteHeader("Hermes Export Missing Signal Agent Specs");
        try
        {
            var result = new EnsembleSignalSpecMaintenanceService(BuildStoragePaths(), _runtimeRoot).ExportMissingSpecs();
            WriteEnsembleSignalSpecValidationResult(result);
            Console.WriteLine();
            WriteDemoSignalFeedSafety();
            WriteSafety();
            return result.Blockers.Count > 0 || result.MissingSpecs.Count > 0 ? 1 : 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteDemoSignalFeedSafety();
            WriteSafety();
            return 1;
        }
    }

    private int ValidateEnsembleSignalSpecs()
    {
        WriteHeader("Hermes Validate Ensemble Signal Specs");
        var result = new EnsembleSignalSpecMaintenanceService(BuildStoragePaths(), _runtimeRoot).ValidateSpecs();
        WriteEnsembleSignalSpecValidationResult(result);
        Console.WriteLine();
        WriteDemoSignalFeedSafety();
        WriteSafety();
        return result.Blockers.Count > 0 || result.MissingSpecs.Count > 0 ? 1 : 0;
    }

    private int CreateForwardTestPlan()
    {
        WriteHeader("Hermes Create Forward Test Plan");
        try
        {
            var status = new ForwardTestService(BuildStoragePaths(), _runtimeRoot).CreatePlan();
            WriteForwardTestStatus(status);
            Console.WriteLine();
            WriteForwardTestSafety();
            WriteSafety();
            return status.Blockers.Count > 0 ? 1 : 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteForwardTestSafety();
            WriteSafety();
            return 1;
        }
    }

    private int ShowForwardTestStatus()
    {
        WriteHeader("Hermes Forward Test Status");
        var status = new ForwardTestService(BuildStoragePaths(), _runtimeRoot).LoadOrCreateStatus();
        WriteForwardTestStatus(status);
        Console.WriteLine();
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int ShowForwardTestLog()
    {
        WriteHeader("Hermes Forward Test Log");
        var service = new ForwardTestService(BuildStoragePaths(), _runtimeRoot);
        if (!File.Exists(service.LogPath))
        {
            WriteWarning("Forward-Test-Log nicht vorhanden.");
        }
        else
        {
            WriteField("Log", DisplayPath(service.LogPath));
            foreach (var line in File.ReadLines(service.LogPath).TakeLast(20))
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine();
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int RecordForwardTestObservation()
    {
        WriteHeader("Hermes Record Forward Test Observation");
        var signalId = ReadOption(_args, "--signal-id");
        var result = ReadOption(_args, "--result");
        var note = ReadOption(_args, "--note") ?? string.Empty;
        try
        {
            var status = new ForwardTestService(BuildStoragePaths(), _runtimeRoot).RecordObservation(signalId ?? string.Empty, result ?? string.Empty, note);
            WriteForwardTestStatus(status);
            Console.WriteLine();
            WriteForwardTestSafety();
            WriteSafety();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteForwardTestSafety();
            WriteSafety();
            return 1;
        }
    }

    private int RunForwardTestObservation()
    {
        WriteHeader("Hermes Run Forward Test Observation");
        try
        {
            var service = new ForwardTestService(BuildStoragePaths(), _runtimeRoot);
            var status = service.RunObservation();
            var observations = service.LoadLatestObservations();
            WriteForwardTestStatus(status);
            if (observations.Count > 0)
            {
                Console.WriteLine();
                WriteSubHeader("Latest Observations");
                foreach (var observation in observations.Take(10))
                {
                    WriteForwardTestObservation(observation);
                }
            }

            Console.WriteLine();
            WriteForwardTestSafety();
            WriteSafety();
            return status.Blockers.Count > 0 ? 1 : 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            WriteForwardTestSafety();
            WriteSafety();
            return 1;
        }
    }

    private int ShowLatestForwardTestObservations()
    {
        WriteHeader("Hermes Latest Forward Test Observations");
        var service = new ForwardTestService(BuildStoragePaths(), _runtimeRoot);
        var status = service.LoadOrCreateStatus();
        var observations = service.LoadLatestObservations();
        WriteField("Latest Observations JSON", DisplayPath(service.LatestObservationsJsonPath));
        WriteField("Latest Observations Markdown", DisplayPath(service.LatestObservationsMarkdownPath));
        WriteField("Observation Count", observations.Count.ToString());
        if (observations.Count == 0)
        {
            WriteWarning("Keine Forward-Test-Beobachtungen vorhanden.");
        }
        else
        {
            foreach (var observation in observations.Take(20))
            {
                WriteForwardTestObservation(observation);
            }
        }

        Console.WriteLine();
        WriteField("Forward Test Status", status.ForwardTestStatus);
        WriteField("Forward Test Health", status.ForwardTestHealth);
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int ShowForwardTestSummary()
    {
        WriteHeader("Hermes Forward Test Summary");
        var service = new ForwardTestService(BuildStoragePaths(), _runtimeRoot);
        var status = service.LoadOrCreateStatus();
        WriteForwardTestStatus(status);
        Console.WriteLine();
        WriteForwardTestSafety();
        WriteSafety();
        return 0;
    }

    private int ShowCurrentMarketStatus()
    {
        WriteHeader("Hermes Current Market Status");
        var service = new CurrentMarketSnapshotService(BuildStoragePaths(), _runtimeRoot);
        var status = service.LoadOrCreateStatus();
        WriteCurrentMarketStatus(status);
        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return status.SnapshotStatus == "available" ? 0 : 1;
    }

    private int UpdateCurrentMarketSnapshot()
    {
        WriteHeader("Hermes Update Current Market Snapshot");
        var service = new CurrentMarketSnapshotService(BuildStoragePaths(), _runtimeRoot);
        var status = service.UpdateSnapshot();
        WriteCurrentMarketStatus(status);
        foreach (var snapshot in service.LoadSnapshot())
        {
            WriteCurrentMarketAssetSnapshot(snapshot);
        }

        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return status.SnapshotStatus == "available" ? 0 : 1;
    }

    private int ShowCurrentMarketSnapshot()
    {
        WriteHeader("Hermes Current Market Snapshot");
        var service = new CurrentMarketSnapshotService(BuildStoragePaths(), _runtimeRoot);
        var status = service.LoadOrCreateStatus();
        WriteCurrentMarketStatus(status);
        foreach (var snapshot in service.LoadSnapshot())
        {
            WriteCurrentMarketAssetSnapshot(snapshot);
        }

        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return 0;
    }

    private int ExplainCurrentMarketGap()
    {
        WriteHeader("Hermes Explain Current Market Gap");
        var asset = ReadOption(_args, "--asset") ?? "XAUUSD";
        var service = new CurrentMarketSnapshotService(BuildStoragePaths(), _runtimeRoot);
        WriteField("Asset", asset.ToUpperInvariant());
        WriteMessages("Gap Reasons", service.ExplainGap(asset));
        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return 0;
    }

    private int UpdateCTraderReadonlyQuotes()
    {
        WriteHeader("Hermes Update cTrader Read-Only Quotes");
        var service = new CTraderReadOnlyQuoteService(BuildStoragePaths(), _runtimeRoot);
        var snapshot = service.UpdateQuotes();
        WriteQuoteSnapshotStatus(snapshot);
        foreach (var quote in service.LoadQuotes())
        {
            WriteQuoteSnapshot(quote);
        }

        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return snapshot.QuoteSnapshotStatus == "available" ? 0 : 1;
    }

    private int ShowCTraderReadonlyQuotes()
    {
        WriteHeader("Hermes cTrader Read-Only Quotes");
        var service = new CTraderReadOnlyQuoteService(BuildStoragePaths(), _runtimeRoot);
        var snapshot = service.LoadOrCreateStatus();
        WriteQuoteSnapshotStatus(snapshot);
        foreach (var quote in service.LoadQuotes())
        {
            WriteQuoteSnapshot(quote);
        }

        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return 0;
    }

    private int ShowQuoteSnapshotStatus()
    {
        WriteHeader("Hermes Quote Snapshot Status");
        var service = new CTraderReadOnlyQuoteService(BuildStoragePaths(), _runtimeRoot);
        var snapshot = service.LoadOrCreateStatus();
        WriteQuoteSnapshotStatus(snapshot);
        Console.WriteLine();
        WriteCurrentMarketSafety();
        WriteSafety();
        return snapshot.QuoteSnapshotStatus == "available" ? 0 : 1;
    }

    private int ShowNearMissStrategies()
    {
        WriteHeader("Hermes Near-Miss Strategies");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.LoadAnalysis() ?? analyzer.Run();

        WriteField("Near Miss", DisplayPath(analyzer.NearMissPath));
        WriteField("Near Miss Count", report.NearMissCount.ToString());
        if (report.NearMissStrategies.Count == 0)
        {
            WriteWarning("Keine echten Near-Miss-Strategien gefunden. Zeige beste verworfene Strategien als Diagnose.");
            foreach (var item in report.BestRejectedStrategies.Take(12))
            {
                WriteCandidateGateDiagnostic(item);
            }
        }
        else
        {
            foreach (var item in report.NearMissStrategies.Take(20))
            {
                WriteCandidateGateDiagnostic(item);
            }
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementExperiments()
    {
        WriteHeader("Hermes Improvement Experiments");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.LoadAnalysis() ?? analyzer.Run();

        WriteField("Experiments", DisplayPath(analyzer.ImprovementExperimentsPath));
        WriteField("Suggestions", report.RecommendedImprovementExperiments.Count.ToString());
        foreach (var suggestion in report.RecommendedImprovementExperiments)
        {
            WriteStrategyImprovementSuggestion(suggestion);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunQualityImprovementExperiments()
    {
        WriteHeader("Hermes Quality Improvement Experiments");
        var maxBatchSize = ReadIntOption(_args, "--max-batch-size", fallback: 64, min: 1, max: 250);
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var report = service.Run(maxBatchSize);

        WriteQualityImprovementReportHeader(service, report);
        WriteMessages("Blockers Addressed", report.BlockersAddressed);
        WriteMessages("Expected Blocker Reduction", report.ExpectedBlockerReduction);
        WriteField("OOS Experiments", report.OosExperiments.Count.ToString());
        WriteField("Cost Experiments", report.CostResilienceExperiments.Count.ToString());
        WriteField("Risk Experiments", report.RiskSensitivityExperiments.Count.ToString());
        WriteField("Regime Experiments", report.RegimeSessionFilterExperiments.Count.ToString());
        WriteField("Near Miss Changed", report.NearMissCountChanged.ToString().ToLowerInvariant());
        WriteField("Near Miss Note", report.NearMissImpactNote);
        WriteField("No Forced Approval", report.NoCandidateApprovalForced.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowQualityImprovementReport()
    {
        WriteHeader("Hermes Quality Improvement Report");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var report = service.LoadReport() ?? service.Run();

        WriteQualityImprovementReportHeader(service, report);
        WriteMessages("Blockers Addressed", report.BlockersAddressed);
        WriteMessages("Expected Blocker Reduction", report.ExpectedBlockerReduction);
        WriteSubHeader("Top OOS Experiments");
        foreach (var experiment in report.OosExperiments.Take(5))
        {
            WriteOosExperiment(experiment);
        }

        WriteSubHeader("Top Cost Experiments");
        foreach (var experiment in report.CostResilienceExperiments.Take(5))
        {
            WriteCostExperiment(experiment);
        }

        WriteSubHeader("Top Risk Experiments");
        foreach (var experiment in report.RiskSensitivityExperiments.Take(5))
        {
            WriteRiskExperiment(experiment);
        }

        WriteSubHeader("Top Regime/Session Experiments");
        foreach (var experiment in report.RegimeSessionFilterExperiments.Take(5))
        {
            WriteRegimeExperiment(experiment);
        }

        WriteField("Near Miss Baseline", report.BaselineNearMissCount.ToString());
        WriteField("Near Miss Changed", report.NearMissCountChanged.ToString().ToLowerInvariant());
        WriteField("Near Miss Note", report.NearMissImpactNote);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostResilienceReport()
    {
        WriteHeader("Hermes Cost Resilience Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadCostResilienceExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().CostResilienceExperiments;
        }

        WriteField("Report", DisplayPath(service.CostResiliencePath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteCostExperiment(experiment);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOosStabilityReport()
    {
        WriteHeader("Hermes OOS Stability Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadOosStabilityExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().OosExperiments;
        }

        WriteField("Report", DisplayPath(service.OosStabilityPath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteOosExperiment(experiment);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRiskSensitivityReport()
    {
        WriteHeader("Hermes Risk Sensitivity Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadRiskSensitivityExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().RiskSensitivityExperiments;
        }

        WriteField("Report", DisplayPath(service.RiskSensitivityPath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteRiskExperiment(experiment);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunStrategyResearch()
    {
        WriteHeader("Hermes Strategy Research Beta 2");
        var storagePaths = BuildStoragePaths();
        var service = new StrategyResearchService(storagePaths);
        var strategyStep = RunStrategyResearchAndInsights(storagePaths);

        WriteField("Memory", DisplayPath(service.MemoryPath));
        WriteField("Variants Tested Total", strategyStep.Memory.VariantsTested.ToString());
        WriteField("Variants Tested This Run", strategyStep.TestedNow.ToString());
        WriteField("Research Insights", DisplayPath(strategyStep.InsightsPath));
        WriteStrategyResearchMemory(strategyStep.Memory, limit: 5);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyResearchStatus()
    {
        WriteHeader("Hermes Strategy Research Status");
        var service = new StrategyResearchService(BuildStoragePaths());
        var memory = service.LoadOrCreateMemory();

        WriteField("Memory", DisplayPath(service.MemoryPath));
        WriteStrategyResearchMemory(memory, limit: 5);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchInsights()
    {
        WriteHeader("Hermes Research Insights");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var insights = generator.Generate();

        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteResearchInsights(insights);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeSources()
    {
        var storagePaths = BuildStoragePaths();
        var registry = new KnowledgeSourceRegistry(storagePaths);
        var sources = registry.LoadOrCreateSources();

        WriteHeader("Hermes Cognitive Knowledge Sources");
        WriteField("Sources", DisplayPath(registry.SourcesPath));
        WriteField("Source Count", sources.Count.ToString());
        foreach (var source in sources)
        {
            WriteSubHeader($"{source.SourceName} / {source.SourceId}");
            WriteField("URL/Path", source.UrlOrPath);
            WriteField("Domain", source.Domain);
            WriteField("Type", source.SourceType);
            WriteField("Trust", $"{source.TrustProfile.TrustLevel} ({source.TrustProfile.TrustScore:0.##})");
            WriteField("License", source.TrustProfile.LicenseHint);
            WriteField("Extraction", source.ExtractionStatus);
            WriteField("Last Checked", source.LastCheckedUtc.ToString("O"));
            WriteMessages("Concepts", source.ExtractedConcepts);
            WriteMessages("Risk Flags", source.RiskFlags);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCognitiveStatus()
    {
        WriteHeader("Hermes Cognitive Core Status");
        var service = new CognitiveCoreService(BuildStoragePaths());
        var status = service.BuildStatus();

        WriteField("Status", DisplayPath(service.StatusPath));
        WriteField("Root", DisplayPath(status.CognitiveRoot));
        WriteField("Sources", status.SourceCount.ToString());
        WriteField("Knowledge Items", status.KnowledgeItemCount.ToString());
        WriteField("Queue Items", status.QueueItemCount.ToString());
        WriteField("Insights", status.InsightCount.ToString());
        WriteField("Memory Entries", status.MemoryEntryCount.ToString());
        WriteMessages("Active Domains", status.ActiveDomains);
        WriteMessages("Next Actions", status.NextActions);
        foreach (var domain in status.Domains)
        {
            WriteSubHeader($"{domain.Name} / {domain.DomainId}");
            WriteField("Active", domain.Active.ToString().ToLowerInvariant());
            WriteField("Status", domain.Status);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ScanKnowledgeSources()
    {
        WriteHeader("Hermes Knowledge Source Scout");
        var storagePaths = BuildStoragePaths();
        var scout = new KnowledgeSourceScout(storagePaths);
        var sources = scout.Scan();
        var registry = new KnowledgeSourceRegistry(storagePaths);

        WriteField("Sources", DisplayPath(registry.SourcesPath));
        WriteField("Sources Scanned", sources.Count.ToString());
        WriteMessages("Domains", sources.Select(source => source.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        WriteMessages("Risk Flags", sources.SelectMany(source => source.RiskFlags).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainStatus()
    {
        WriteHeader("Hermes Multi-Domain Cognitive Status");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);

        WriteField("Domain Status", DisplayPath(service.DomainStatusPath));
        WriteField("Domain Insights", DisplayPath(service.DomainInsightsPath));
        WriteField("Active Domains", string.Join(", ", status.ActiveDomains));
        WriteField("Domains", status.Domains.Count.ToString());
        WriteField("Insights", insights.Insights.Count.ToString());
        WriteMessages("Weak Domains", status.WeakDomains);
        WriteMessages("Strong Domains", status.StrongDomains);
        foreach (var entry in status.Domains)
        {
            WriteCognitiveDomainStatusEntry(entry);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainInsights()
    {
        WriteHeader("Hermes Multi-Domain Insights");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var report = service.BuildInsights();

        WriteField("Domain Insights", DisplayPath(service.DomainInsightsPath));
        WriteField("Updated UTC", report.UpdatedAtUtc.ToString("O"));
        WriteField("Insights", report.Insights.Count.ToString());
        foreach (var insight in report.Insights.Take(40))
        {
            WriteCognitiveDomainInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSingleDomainStatus(string domain)
    {
        WriteHeader($"Hermes {domain} Domain Status");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);
        var entry = status.Domains.FirstOrDefault(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            WriteWarning($"Domain nicht gefunden: {domain}");
            WriteSafety();
            return 1;
        }

        WriteField("Domain Status", DisplayPath(service.DomainStatusPath));
        WriteField("Domain Directory", DisplayPath(Path.Combine(service.DomainsRoot, domain)));
        WriteCognitiveDomainStatusEntry(entry);
        foreach (var insight in insights.Insights.Where(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).Take(12))
        {
            WriteCognitiveDomainInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ScanDomain(string domain)
    {
        WriteHeader($"Hermes scan-{domain}-domain");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var result = service.ScanDomain(domain);

        WriteDomainScanResult(result);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeCatalog()
    {
        WriteHeader("Hermes Cognitive Knowledge Catalog");
        var catalog = new KnowledgeCatalog(BuildStoragePaths());
        var items = catalog.LoadOrCreateItems();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Items", items.Count.ToString());
        foreach (var item in items.Take(40))
        {
            WriteKnowledgeCatalogItem(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeItem()
    {
        WriteHeader("Hermes Cognitive Knowledge Item");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <ID> angeben, z. B. --id trading:breakout.");
            WriteSafety();
            return 1;
        }

        var catalog = new KnowledgeCatalog(BuildStoragePaths());
        var item = catalog.FindById(id);
        if (item is null)
        {
            WriteWarning($"Knowledge Item nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteKnowledgeCatalogItem(item);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeHealth()
    {
        WriteHeader("Hermes Knowledge Health");
        var storagePaths = BuildStoragePaths();
        var engine = new KnowledgeQualityEngine(storagePaths);
        var report = engine.Run();

        WriteKnowledgeQualityReport(report, engine.QualityPath);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeReasonStatus()
    {
        WriteHeader("Hermes Knowledge Reasoning");
        var service = new KnowledgeReasoningService(BuildStoragePaths());
        var report = service.LoadLatestReport();

        if (report is null)
        {
            WriteWarning("No knowledge reasoning report found yet. Run: hermes knowledge-reason --topic \"...\"");
            WriteField("Report", DisplayPath(service.ReportPath));
            WriteField("Markdown", DisplayPath(service.MarkdownPath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteKnowledgeReasoningReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnowledgeReason()
    {
        WriteHeader("Hermes Knowledge Reasoning");
        var topic = ReadOption(_args, "--topic");
        if (string.IsNullOrWhiteSpace(topic))
        {
            Console.WriteLine("Error: --topic is required.");
            WriteSafety();
            return 1;
        }

        var service = new KnowledgeReasoningService(BuildStoragePaths());
        var report = service.Run(topic!);
        WriteKnowledgeReasoningReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTrustedKnowledgeUsageAuditStatus()
    {
        WriteHeader("Hermes Trusted Knowledge Usage Audit");
        var service = new TrustedKnowledgeUsageAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadLatestReport();

        if (report is null)
        {
            WriteWarning("No trusted knowledge usage audit report found yet. Run: hermes trusted-knowledge-usage-audit");
            WriteField("Report", DisplayPath(service.ReportPath));
            WriteField("Markdown", DisplayPath(service.MarkdownPath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteTrustedKnowledgeUsageAuditReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunTrustedKnowledgeUsageAudit()
    {
        WriteHeader("Hermes Trusted Knowledge Usage Audit");
        var service = new TrustedKnowledgeUsageAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteTrustedKnowledgeUsageAuditReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTrustedKnowledgeImpactStatus()
    {
        WriteHeader("Hermes Trusted Knowledge Impact Report");
        var service = new TrustedKnowledgeImpactService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadLatestReport();

        if (report is null)
        {
            WriteWarning("No trusted knowledge impact report found yet. Run: hermes trusted-knowledge-impact");
            WriteField("Report", DisplayPath(service.ReportPath));
            WriteField("Markdown", DisplayPath(service.MarkdownPath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteTrustedKnowledgeImpactReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunTrustedKnowledgeImpact()
    {
        WriteHeader("Hermes Trusted Knowledge Impact Report");
        var service = new TrustedKnowledgeImpactService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteTrustedKnowledgeImpactReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousKnowledgeAdvancementStatus()
    {
        WriteHeader("Hermes Autonomous Knowledge Advancement");
        var service = new AutonomousKnowledgeAdvancementEngineService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadLatestReport();

        if (report is null)
        {
            WriteWarning("No autonomous knowledge advancement report found yet. Run: hermes autonomous-knowledge-advancement");
            WriteField("Report", DisplayPath(service.ReportPath));
            WriteField("Markdown", DisplayPath(service.MarkdownPath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteAutonomousKnowledgeAdvancementReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunAutonomousKnowledgeAdvancement()
    {
        WriteHeader("Hermes Autonomous Knowledge Advancement");
        var execute = HasArg("--execute");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 12, min: 1, max: 100);
        var service = new AutonomousKnowledgeAdvancementEngineService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run(maxItems, execute);

        WriteAutonomousKnowledgeAdvancementReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeHealthRootCause()
    {
        WriteHeader("Hermes Knowledge Health Root Cause");
        var service = new KnowledgeHealthRootCauseAnalysisService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Trust", report.CurrentTrustLabel);
        WriteField("Knowledge Health", report.CurrentKnowledgeHealth);
        WriteField("Offene Reviews", report.OpenReviews.ToString());
        WriteField("Offene Forward-Pläne", report.OpenForwardPlans.ToString());
        WriteField("OOS-Lücke", report.HypothesesWithoutOos.ToString());
        WriteField("Validierungslücke", report.OpenValidationTasks.ToString());
        WriteField("Widerspruchsanteil", report.OpenContradictions.ToString());
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top 3 Ursachen");
        foreach (var driver in report.Drivers.Take(3))
        {
            WriteField($"{driver.Rank}. {driver.Title}", $"{driver.Impact} · impact={driver.EstimatedTrustImpact:0.##}");
            WriteField("Kurz", driver.Summary);
        }
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeConfidenceEngine()
    {
        WriteHeader("Hermes Knowledge Confidence Engine");
        var service = new KnowledgeConfidenceEngineService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Bewertete Hypothesen", report.EvaluatedHypotheses.ToString());
        WriteField("Top Confidence", report.TopCandidate?.Title ?? "-");
        WriteField("Stärkste Blocker", report.TopCandidate is null ? "-" : string.Join(" · ", report.TopCandidate.StrongestBlockers));
        WriteField("Nächster Evidenzschritt", report.TopCandidate?.NextEvidenceStep ?? "-");
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top 5 Hypothesen");
        foreach (var item in report.Hypotheses.Take(5))
        {
            WriteField(item.Title, $"{item.ConfidenceClass} · {item.Asset} {item.Timeframe} · score={item.ConfidenceScore:0.#}");
            WriteField("Blocker", string.Join(" · ", item.StrongestBlockers));
            WriteField("Nächster Schritt", item.NextEvidenceStep);
        }
        WriteSafety();
        return 0;
    }

    private int ShowConfidenceReviewPrioritization()
    {
        WriteHeader("Hermes Confidence Driven Review Prioritization");
        var service = new ConfidenceDrivenReviewPrioritizationService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Bewertete Reviews", report.ReviewsEvaluated.ToString());
        WriteField("Hypothesen gematcht", report.HypothesesMatched.ToString());
        WriteField("Groesster Wissenshebel", report.TopLever?.HypothesisTitle ?? "-");
        WriteField("Hoechste erwartete Confidence-Steigerung", report.TopLever is null ? "-" : $"+{report.TopLever.ConfidenceGainScore:0.#}%");
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top Wissenshebel");
        foreach (var item in report.Entries.Take(5))
        {
            WriteField(item.Title, $"{item.ReprioritizationClass} · gain={item.ConfidenceGainScore:0.#}% · confidence={item.ConfidenceScore:0.#}%");
            WriteField("Hypothese", item.HypothesisTitle);
            WriteField("Nächster Schritt", item.NextEvidenceStep);
        }
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeConsolidationAnalyzer()
    {
        WriteHeader("Hermes Knowledge Consolidation Analyzer");
        var service = new KnowledgeConsolidationAnalyzerService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Rohwissen", (report.RawObservationCount + report.RawHypothesisCount + report.RawResearchResultCount).ToString());
        WriteField("Cluster", report.ClusterCount.ToString());
        WriteField("Duplikate", report.DuplicateCount.ToString());
        WriteField("Konsolidierbare Gruppen", report.ConsolidatableGroupCount.ToString());
        WriteField("Cleanup-Potenzial", report.CleanupPotentialSummary);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Cluster");
        foreach (var cluster in report.Clusters.Take(20))
        {
            WriteField(cluster.Domain, $"{cluster.PatternDescription} · raw={cluster.RawItemCount} · dup={cluster.DuplicateCount} · trust={cluster.AverageTrustScore:0.####} · evidence={cluster.AverageEvidenceScore:0.####} · validation={cluster.AverageValidationScore:0.####}");
            WriteField("Regel-Kandidat", cluster.RuleCandidateSummary);
            WriteField("Nächste Aktion", cluster.NextAction);
            WriteField("Frank nötig", cluster.FrankRequired.ToString().ToLowerInvariant());
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeConsolidationExecutor()
    {
        WriteHeader("Hermes Knowledge Consolidation Executor");
        var service = new KnowledgeConsolidationExecutorService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Muster erkannt", report.AnalyzerClusterCount.ToString());
        WriteField("Kandidaten vorbereitet", report.CandidatesPreparedCount.ToString());
        WriteField("Rohdaten unverändert", "true");
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteField("Safety", report.SafetySummary);
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Kandidaten");
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField(candidate.Domain, $"{candidate.Title} · raw={candidate.SupportingItemsCount} · dup={candidate.DuplicateItemsCount} · trust={candidate.TrustBaseline:0.####} · evidence={candidate.EvidenceStrength:0.####} · validation={candidate.ValidationStatus}");
            WriteField("Regel-Kandidat", candidate.Summary);
            WriteField("Nächste Aktion", candidate.RecommendedNextAction);
            WriteField("Risiko", candidate.RiskNotes);
            WriteField("Frank nötig", candidate.FrankRequired.ToString().ToLowerInvariant());
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyMutationAnalyzer()
    {
        WriteHeader("Hermes Strategy Mutation Analyzer");
        var service = new StrategyMutationAnalyzerService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Muster analysiert", report.PatternsAnalyzed.ToString());
        WriteField("Mutationen vorbereitet", report.MutationsPrepared.ToString());
        WriteField("Kandidaten", report.CandidateCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Muster");
        foreach (var pattern in report.Patterns.Take(20))
        {
            WriteField(pattern.PatternName, $"{pattern.PatternDescription} · params={string.Join(", ", pattern.ParametersVariations)}");
        }
        WriteSubHeader("Mutationen");
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField(candidate.SourcePattern, $"{string.Join(", ", candidate.ParameterChanges)} · trust={candidate.TrustBaseline:0.####} · validation={(candidate.ValidationRequired ? "yes" : "no")} · oos={(candidate.OosRequired ? "yes" : "no")} · forward={(candidate.ForwardObservationRequired ? "yes" : "no")}");
            WriteField("Erwarteter Nutzen", candidate.ExpectedBenefit);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyParameterResearchPlanner()
    {
        WriteHeader("Hermes Strategy Parameter Research Planner");
        var service = new StrategyParameterResearchPlannerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Muster analysiert", report.PatternsAnalyzed.ToString());
        WriteField("Mutationen vorbereitet", report.MutationsPrepared.ToString());
        WriteField("Kandidaten", report.CandidateCount.ToString());
        WriteField("Knowledge Items", report.KnowledgeItemsAnalyzed.ToString());
        WriteField("Setup Candidates", report.SetupCandidatesAnalyzed.ToString());
        WriteField("Certified Candidates", report.CertifiedCandidatesAnalyzed.ToString());
        WriteField("Forward Observations", report.ForwardObservationsAnalyzed.ToString());
        WriteField("Review Items", report.ReviewItemsAnalyzed.ToString());
        WriteField("Research Entries", report.ResearchEntriesAnalyzed.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Muster");
        foreach (var pattern in report.Patterns.Take(20))
        {
            WriteField(pattern.PatternName, $"{pattern.PatternDescription} · assets={string.Join(", ", pattern.AssetContexts)} · timeframes={string.Join(", ", pattern.TimeframeContexts)}");
            WriteField("Empfohlene Bereiche", string.Join(", ", pattern.SuggestedRanges.Select(range => $"{range.Name}[{string.Join("|", range.Values)}]")));
            WriteField("Evidenz", pattern.EvidenceBasis);
        }
        WriteSubHeader("Mutationen");
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField(candidate.SourcePattern, $"{string.Join(", ", candidate.ParameterRanges.Select(range => $"{range.Name}[{string.Join("|", range.Values)}]"))} · trust={candidate.TrustBaseline:0.####} · validation={(candidate.ValidationRequired ? "yes" : "no")} · oos={(candidate.OosRequired ? "yes" : "no")} · forward={(candidate.ForwardObservationRequired ? "yes" : "no")}");
            WriteField("Erwarteter Nutzen", candidate.ExpectedBenefit);
            WriteField("Evidenz", candidate.EvidenceBasis);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTradingResearchSynthesizer()
    {
        WriteHeader("Hermes Trading Research Synthesizer");
        var service = new TradingResearchSynthesizerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Muster analysiert", report.PatternsAnalyzed.ToString());
        WriteField("Hypothesen", report.HypothesesCount.ToString());
        WriteField("High Priority", report.HighPriorityCount.ToString());
        WriteField("Medium Priority", report.MediumPriorityCount.ToString());
        WriteField("Low Priority", report.LowPriorityCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Externe Evidenz", report.ExternalResearchSource);
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Interne Quellen", report.InternalSources);
        WriteMessages("Externe Quellen", report.ExternalSources);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Vergleiche");
        foreach (var comparison in report.Comparisons.Take(12))
        {
            WriteField(comparison.PatternName, $"{comparison.InternalEvidence} | {comparison.ExternalEvidence}");
            WriteField("Übereinstimmungen", string.Join("; ", comparison.Agreements));
            if (comparison.Contradictions.Count > 0)
            {
                WriteField("Widersprüche", string.Join("; ", comparison.Contradictions));
            }
            WriteField("Offene Fragen", string.Join("; ", comparison.OpenQuestions));
            WriteField("Parameterklassen", string.Join(", ", comparison.RelevantParameterClasses));
        }
        WriteSubHeader("Hypothesen");
        foreach (var hypothesis in report.Hypotheses.Take(20))
        {
            WriteField(hypothesis.Title, $"{hypothesis.Priority} · info_gain={hypothesis.ExpectedInformationGain:0.###} · risk={hypothesis.RiskLevel}");
            WriteField("Hypothese", hypothesis.Hypothesis);
            WriteField("Nächste Validierung", hypothesis.SuggestedNextValidation);
            WriteField("Begründung", $"{hypothesis.AgreementSummary} / {hypothesis.ContradictionSummary}");
        }
        WriteTrustedKnowledgeContext("trading research synthesizer", ExtractKnowledgeTopic(
            report.Comparisons.FirstOrDefault()?.PatternName,
            report.Hypotheses.FirstOrDefault()?.Title,
            report.Hypotheses.FirstOrDefault()?.Hypothesis,
            string.Join(" ", report.InternalSources.Take(8)),
            string.Join(" ", report.ExternalSources.Take(8))));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyMutationValidationPlanner()
    {
        WriteHeader("Hermes Strategy Mutation Validation Planner");
        var service = new StrategyMutationValidationPlannerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Hypothesen analysiert", report.HypothesesAnalyzed.ToString());
        WriteField("Validierungsaufträge", report.ValidationPlansPrepared.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Quellen", report.SourcesUsed);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Top Validierungsaufträge");
        foreach (var plan in report.ValidationPlans.Take(12))
        {
            WriteField(plan.ValidationPlanId, $"{plan.StrategyPattern} · {plan.Asset} {plan.Timeframe} · priority={plan.Priority} · info_gain={plan.ExpectedInformationGain:0.###} · effort={plan.ValidationEffort:0.###}");
            WriteField("Parameter", string.Join(", ", plan.ParametersToValidate));
            WriteField("Backtest/OOS/Forward", $"{plan.RequiredBacktest}/{plan.RequiredOosTest}/{plan.RequiredWalkForward}/{plan.RequiredForwardObservation}");
        }
        WriteTrustedKnowledgeContext("strategy validation", ExtractKnowledgeTopic(
            report.ValidationPlans.FirstOrDefault()?.ValidationPlanId,
            report.ValidationPlans.FirstOrDefault()?.StrategyPattern,
            report.ValidationPlans.FirstOrDefault()?.Asset,
            report.ValidationPlans.FirstOrDefault()?.Timeframe,
            string.Join(" ", report.ValidationPlans.FirstOrDefault()?.ParametersToValidate ?? [])));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyValidationQueueExport()
    {
        WriteHeader("Hermes Strategy Validation Queue Export");
        var service = new StrategyValidationQueueExportService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue", DisplayPath(report.QueuePath ?? string.Empty));
        WriteField("Validierungsaufträge", report.PlannedCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Statusverteilung");
        WriteMessages("Status", report.StatusDistribution);
        WriteSubHeader("Queue-Einträge");
        foreach (var item in report.QueueItems.Take(12))
        {
            WriteField(item.QueueItemId, $"{item.StrategyPattern} · {item.Asset} {item.Timeframe} · priority={item.Priority} · status={item.Status}");
            WriteField("Parameter", string.Join(", ", item.ParametersToValidate));
            WriteField("Next Action", item.NextAction);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyValidationReadinessAnalyzer()
    {
        WriteHeader("Hermes Strategy Validation Readiness Analyzer");
        var service = new StrategyValidationReadinessAnalyzerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Analysiert", report.QueueItemsAnalyzed.ToString());
        WriteField("Sofort testbar", report.ReadyForBacktestCount.ToString());
        WriteField("Warten auf OOS", report.WaitingForOosDataCount.ToString());
        WriteField("Warten auf Forward", report.WaitingForForwardObservationCount.ToString());
        WriteField("Blockiert", report.BlockedCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Top Ready Candidates");
        foreach (var item in report.TopReadyCandidates.Take(5))
        {
            WriteField(item.ValidationPlanId, $"{item.StrategyPattern} · {item.Asset} {item.Timeframe} · readiness={item.ReadinessScore:0} · gain={item.ExpectedInformationGain:0.###}");
            WriteMessages("Missing", item.MissingRequirements);
            WriteMessages("Blockers", item.Blockers);
        }
        WriteSubHeader("Top Information Gain Candidates");
        foreach (var item in report.TopInformationGainCandidates.Take(5))
        {
            WriteField(item.ValidationPlanId, $"{item.StrategyPattern} · {item.Asset} {item.Timeframe} · readiness={item.ReadinessScore:0} · gain={item.ExpectedInformationGain:0.###}");
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestJobPlanner()
    {
        WriteHeader("Hermes Strategy Backtest Job Planner");
        var service = new StrategyBacktestJobPlannerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Prüfungen", report.QueueItemsAnalyzed.ToString());
        WriteField("Backtest-Jobs bereit", report.ReadyToExecuteCount.ToString());
        WriteField("Warten auf Daten", report.WaitingForDataCount.ToString());
        WriteField("Blockiert", report.BlockedCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Statusverteilung");
        WriteMessages("Status", report.StatusDistribution);
        WriteSubHeader("Top Jobs");
        foreach (var job in report.Jobs.Take(12))
        {
            WriteField(job.BacktestJobId, $"{job.StrategyPattern} · {job.Asset} {job.Timeframe} · status={job.Status} · runs={job.MaxRuns} · timeout={job.TimeoutSeconds}s");
            WriteField("Dataset", $"{job.DatasetRequired} · available={job.DatasetAvailable}");
            WriteField("Next Action", job.NextAction);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestExecutor()
    {
        WriteHeader("Hermes Strategy Backtest Executor");
        var targetJobId = ReadOption(_args, "--job");
        var maxRuns = ReadIntOption(_args, "--max-runs", fallback: 0, min: 0, max: 1000);
        var service = new StrategyBacktestExecutorService(
            BuildStoragePaths(),
            string.IsNullOrWhiteSpace(targetJobId) ? null : targetJobId,
            maxRuns > 0 ? maxRuns : null);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Report Role", report.ReportRole);
        WriteField("Selected Job Filter", string.IsNullOrWhiteSpace(targetJobId) ? "-" : targetJobId);
        WriteField("Max Runs Override", maxRuns > 0 ? maxRuns.ToString() : "-");
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Contract Markdown", DisplayPath(report.ContractMarkdownPath));
        WriteField("Contract JSON", DisplayPath(report.ContractJsonPath));
        WriteField("Queue Items", report.QueueItemsLoaded.ToString());
        WriteField("Ready Jobs", report.ReadyJobsFound.ToString());
        WriteField("Attempted", report.JobsAttempted.ToString());
        WriteField("Executed", report.JobsExecuted.ToString());
        WriteField("Skipped", report.JobsSkipped.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Latest Success", report.LatestSuccessAvailable ? "ja" : "nein");
        WriteField("Latest Success Path", DisplayPath(report.LatestSuccessPath));
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Status", report.StatusDistribution);
        if (report.SelectedJob is not null)
        {
            WriteSubHeader("Selected Job");
            WriteField(report.SelectedJob.BacktestJobId, $"{report.SelectedJob.StrategyPattern} · {report.SelectedJob.Asset} {report.SelectedJob.Timeframe} · status={report.SelectedJob.Status}");
            WriteField("Dataset", $"{report.SelectedJob.DatasetRequired} · available={report.SelectedJob.DatasetAvailable}");
            WriteField("Next Action", report.SelectedJob.NextAction);
        }
        if (report.Execution is not null)
        {
            WriteSubHeader("Execution");
            WriteField("Execution Id", report.Execution.ExecutionId);
            WriteField("Execution Supported", report.Execution.ExecutionSupported.ToString().ToLowerInvariant());
            WriteField("Cost Spread Model Used", report.Execution.CostSpreadModelUsed.ToString().ToLowerInvariant());
            WriteField("Status", report.Execution.Status);
            if (report.Execution.TradesSimulated is not null)
            {
                WriteField("Trades Simulated", report.Execution.TradesSimulated.Value.ToString());
                WriteField("Win Rate", report.Execution.WinRate?.ToString("0.####") ?? "-");
                WriteField("Profit Factor", report.Execution.ProfitFactor?.ToString("0.####") ?? "-");
                WriteField("Max Drawdown", report.Execution.MaxDrawdown?.ToString("0.####") ?? "-");
                WriteField("Expectancy", report.Execution.Expectancy?.ToString("0.####") ?? "-");
                WriteField("R Multiple Avg", report.Execution.RMultipleAvg?.ToString("0.####") ?? "-");
            }
            WriteMessages("Warnings", report.Execution.Warnings);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMutationValidationExecutor()
    {
        WriteHeader("Hermes Mutation Validation Executor");
        var targetJobId = ReadOption(_args, "--job");
        var maxRuns = ReadIntOption(_args, "--max-runs", fallback: 0, min: 0, max: 1000);
        var service = new MutationValidationExecutorService(
            BuildStoragePaths(),
            _runtimeRoot,
            string.IsNullOrWhiteSpace(targetJobId) ? null : targetJobId,
            maxRuns > 0 ? maxRuns : null);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Report Role", report.ReportRole);
        WriteField("Selected Job Filter", string.IsNullOrWhiteSpace(targetJobId) ? "-" : targetJobId);
        WriteField("Max Runs Override", maxRuns > 0 ? maxRuns.ToString() : "-");
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Result Path", report.ResultPath == "-" ? "-" : DisplayPath(report.ResultPath));
        WriteField("History Path", DisplayPath(report.HistoryPath));
        WriteField("Queue Items", report.JobsLoaded.ToString());
        WriteField("Ready Jobs", report.ReadyJobsFound.ToString());
        WriteField("Attempted", report.JobsAttempted.ToString());
        WriteField("Executed", report.JobsExecuted.ToString());
        WriteField("Skipped", report.JobsSkipped.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Latest Success", report.LatestSuccessAvailable ? "ja" : "nein");
        WriteField("Latest Success Path", DisplayPath(report.LatestSuccessPath));
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Status", report.StatusDistribution);
        if (report.SelectedJob is not null)
        {
            WriteSubHeader("Selected Job");
            WriteField(report.SelectedJob.ValidationJobId, $"{report.SelectedJob.StrategyPattern} · {report.SelectedJob.Asset} {report.SelectedJob.Timeframe} · mutation={report.SelectedJob.MutationType} · status={report.SelectedJob.ReadinessStatus}");
            WriteField("Priority", report.SelectedJob.Priority);
            WriteField("Dataset", $"{report.SelectedJob.RequiredDataset}");
        }
        if (report.Execution is not null)
        {
            WriteSubHeader("Execution");
            WriteField("Execution Id", report.Execution.ExecutionId);
            WriteField("Execution Supported", report.Execution.ExecutionSupported.ToString().ToLowerInvariant());
            WriteField("Status", report.Execution.Status);
            WriteField("Quality Class", report.Execution.QualityClass);
            WriteField("Certification Ready", report.Execution.CertificationReady.ToString().ToLowerInvariant());
            if (report.Execution.TradesSimulated is not null)
            {
                WriteField("Trades Simulated", report.Execution.TradesSimulated.Value.ToString());
                WriteField("Win Rate", report.Execution.WinRate?.ToString("0.####") ?? "-");
                WriteField("Profit Factor", report.Execution.ProfitFactor?.ToString("0.####") ?? "-");
                WriteField("Max Drawdown", report.Execution.MaxDrawdown?.ToString("0.####") ?? "-");
                WriteField("Expectancy", report.Execution.Expectancy?.ToString("0.####") ?? "-");
                WriteField("R Multiple Avg", report.Execution.RMultipleAvg?.ToString("0.####") ?? "-");
            }
            WriteMessages("Warnings", report.Execution.Warnings);
            WriteMessages("Errors", report.Execution.Errors);
        }
        if (report.Comparison is not null)
        {
            WriteSubHeader("Comparison");
            WriteField("Outcome", report.Comparison.Outcome);
            WriteField("Baseline Job", report.Comparison.BaselineBacktestJobId);
            WriteField("Baseline PF", report.Comparison.BaselineProfitFactor.ToString("0.####"));
            WriteField("Mutation PF", report.Comparison.MutationProfitFactor.ToString("0.####"));
            WriteField("Baseline Expectancy", report.Comparison.BaselineExpectancy.ToString("0.####"));
            WriteField("Mutation Expectancy", report.Comparison.MutationExpectancy.ToString("0.####"));
            WriteField("Baseline Drawdown", report.Comparison.BaselineMaxDrawdown.ToString("0.####"));
            WriteField("Mutation Drawdown", report.Comparison.MutationMaxDrawdown.ToString("0.####"));
            WriteField("Baseline Win Rate", report.Comparison.BaselineWinRate.ToString("0.####"));
            WriteField("Mutation Win Rate", report.Comparison.MutationWinRate.ToString("0.####"));
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyDatasetGateAudit()
    {
        WriteHeader("Hermes Strategy Dataset Gate Audit");
        var service = new StrategyDatasetGateAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue", DisplayPath(Path.Combine(_runtimeRoot, "queues", "strategy_validation_queue.json")));
        WriteField("Analysiert", report.QueueItemsAnalyzed.ToString());
        WriteField("Datenquelle", report.DatasetSourceOfTruth);
        WriteField("Ready for Backtest", report.ReadyForBacktestCount.ToString());
        WriteField("Ready to Execute", report.ReadyToExecuteCount.ToString());
        WriteField("Waiting for Data", report.WaitingForDataCount.ToString());
        WriteField("Blocked", report.BlockedCount.ToString());
        WriteField("Inkonsistenzen", report.MismatchCount.ToString());
        WriteField("Behoben", report.FixedCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Inconsistencies", report.Inconsistencies);
        WriteMessages("Correction Plan", report.CorrectionPlan);
        WriteSubHeader("Assets");
        foreach (var item in report.Items)
        {
            WriteField($"{item.Asset} {item.Timeframe}", $"dataset_available={item.DatasetAvailable} · source={item.DatasetSource} · period={item.DatasetPeriod}");
            WriteField("Readiness", item.ReadinessView);
            WriteField("Planner", item.PlannerView);
            WriteField("Mismatch", item.Mismatch.ToString().ToLowerInvariant());
            WriteMessages("Missing", item.MissingRequirements);
            WriteMessages("Warnings", item.Warnings);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousResearchLoopStep()
    {
        WriteHeader("Hermes Autonomous Research Loop Step");

        var service = new AutonomousResearchLoopOrchestratorService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Status", report.Status == "waiting_for_window" ? "wartet" : "arbeitet");
        WriteField("Letzter Schritt", report.LastAutonomousAction);
        WriteField("Nächster Schritt", report.NextScheduledStep);
        WriteField("Ergebnis", report.StepResult);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousResearchLoopStatus()
    {
        WriteHeader("Hermes Autonomous Research Loop Status");

        var service = new AutonomousResearchLoopOrchestratorService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Load();
        var forwardSyncReport = new AutonomousForwardObservationCompletionSyncService(BuildStoragePaths(), _runtimeRoot).Load();
        var openForwardPlans = forwardSyncReport?.OpenPlans ?? 0;
        if (report is null)
        {
            WriteField("Status", "wartet");
            WriteField("Letzter Schritt", "-");
            WriteField("Nächster Schritt", openForwardPlans > 0 ? forwardSyncReport?.NextSafeStep ?? "Forward-Beobachtung im erlaubten Zeitfenster" : "Research-Loop warten");
            WriteField("Ergebnis", "-");
            WriteField("Frank nötig", "nein");
            WriteSafety();
            return 0;
        }

        var dashboardStatus = report.Status == "waiting_for_window" ? "wartet" : report.StepStatus is "idle_no_safe_action" ? "wartet" : "arbeitet";
        WriteField("Status", dashboardStatus);
        WriteField("Letzter Schritt", report.LastAutonomousAction);
        WriteField("Nächster Schritt", report.NextScheduledStep);
        WriteField("Ergebnis", report.StepResult);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousOosPlanning()
    {
        WriteHeader("Hermes Autonomous OOS Planning");

        var service = new AutonomousOosPlanningService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Hypotheses Read", report.HypothesesRead.ToString());
        WriteField("Plans Generated", report.PlansGenerated.ToString());
        WriteField("Ready To Execute", report.ReadyToExecuteCount.ToString());
        WriteField("Waiting For Data", report.WaitingForDataCount.ToString());
        WriteField("Waiting For Specification", report.WaitingForSpecificationCount.ToString());
        WriteField("Blocked", report.BlockedCount.ToString());
        WriteField("Operator Summary", report.OperatorSummary);
        WriteField("Next Safe Step", report.NextSafeStep);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousOosExecutionGate()
    {
        WriteHeader("Hermes Autonomous OOS Execution Gate");

        var service = new AutonomousOosExecutionGateService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Window Status", report.WindowStatus);
        WriteField("Gate Status", report.GateStatus);
        WriteField("Plans Seen", report.PlansSeen.ToString());
        WriteField("Plans Ready", report.PlansReady.ToString());
        WriteField("Plans Waiting", report.PlansWaiting.ToString());
        WriteField("Plans Blocked", report.PlansBlocked.ToString());
        WriteField("Selected OOS Job", report.SelectedPlan?.OosJobId ?? "-");
        WriteField("Result", report.Result?.Outcome ?? "waiting");
        WriteField("Next Planned Step", report.NextSafeStep);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator Summary", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousForwardValidationPlanning()
    {
        WriteHeader("Hermes Autonomous Forward Validation Planning");

        var service = new AutonomousForwardValidationPlanningService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("OOS Plans Read", report.OosPlansRead.ToString());
        WriteField("Completed Improved OOS Plans", report.CompletedImprovedOosPlans.ToString());
        WriteField("Plans Generated", report.PlansGenerated.ToString());
        WriteField("Ready To Observe", report.ReadyToObserveCount.ToString());
        WriteField("Waiting For Market Data", report.WaitingForMarketDataCount.ToString());
        WriteField("Blocked", report.BlockedCount.ToString());
        WriteField("Operator Summary", report.OperatorSummary);
        WriteField("Next Safe Step", report.NextSafeStep);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousForwardObservationGate()
    {
        WriteHeader("Hermes Autonomous Forward Observation Gate");

        var service = new AutonomousForwardObservationGateService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Gate Status", report.GateStatus);
        WriteField("Window Status", report.WindowStatus);
        WriteField("Plans Seen", report.PlansSeen.ToString());
        WriteField("Plans Ready To Observe", report.PlansReadyToObserve.ToString());
        WriteField("Plans Waiting", report.PlansWaiting.ToString());
        WriteField("Plans Blocked", report.PlansBlocked.ToString());
        WriteField("Selected Plan", report.SelectedPlan?.ForwardValidationJobId ?? "-");
        WriteField("Observation Result", report.Observation?.Result ?? report.GateStatus);
        WriteField("Next Safe Step", report.NextSafeStep);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator Summary", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousForwardObservationSync()
    {
        WriteHeader("Hermes Autonomous Forward Observation Sync");

        var service = new AutonomousForwardObservationCompletionSyncService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Plans Read", report.PlansRead.ToString());
        WriteField("Open Plans", report.OpenPlans.ToString());
        WriteField("Completed Plans", report.CompletedPlans.ToString());
        WriteField("Blocked Plans", report.BlockedPlans.ToString());
        WriteField("Operator Summary", report.OperatorSummary);
        WriteField("Next Safe Step", report.NextSafeStep);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowMutationAttributionAnalysis()
    {
        WriteHeader("Hermes Mutation Attribution Analysis");

        var service = new MutationAttributionAnalysisService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Result Class", report.ResultClass);
        WriteField("Cause", report.Cause);
        WriteField("Learning Hypothesis", report.LearningHypothesis);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Baseline Pattern", report.BaselineStrategyPattern);
        WriteField("Baseline Asset", report.BaselineAsset);
        WriteField("Baseline Timeframe", report.BaselineTimeframe);
        WriteField("Baseline Mutation Type", report.BaselineMutationType);
        WriteField("Baseline PF", report.BaselineProfitFactor.ToString("0.####"));
        WriteField("Mutation PF", report.MutationProfitFactor.ToString("0.####"));
        WriteField("Baseline Expectancy", report.BaselineExpectancy.ToString("0.####"));
        WriteField("Mutation Expectancy", report.MutationExpectancy.ToString("0.####"));
        WriteField("Baseline Win Rate", report.BaselineWinRate.ToString("0.####"));
        WriteField("Mutation Win Rate", report.MutationWinRate.ToString("0.####"));
        WriteField("Baseline DD", report.BaselineMaxDrawdown.ToString("0.####"));
        WriteField("Mutation DD", report.MutationMaxDrawdown.ToString("0.####"));
        WriteField("Operator Summary", report.OperatorSummary);
        WriteMessages("Signals", report.SupportingSignals);
        WriteMessages("Warnings", report.Warnings);
        WriteSafety();
        return 0;
    }

    private int ShowAttributionHypothesisFeedback()
    {
        WriteHeader("Hermes Attribution Hypothesis Feedback");

        var service = new AttributionHypothesisFeedbackService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Hypothesis ID", report.Hypothesis.HypothesisId);
        WriteField("Source", report.Hypothesis.Source);
        WriteField("Asset", report.Hypothesis.Asset);
        WriteField("Timeframe", report.Hypothesis.Timeframe);
        WriteField("Strategy Pattern", report.Hypothesis.StrategyPattern);
        WriteField("Causal Factor", report.Hypothesis.CausalFactor);
        WriteField("Finding", report.Hypothesis.Finding);
        WriteField("Confidence", report.Hypothesis.Confidence);
        WriteField("Status", report.Hypothesis.Status);
        WriteField("Next Step", report.Hypothesis.NextStep);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Stored in Store", report.HypothesesAdded > 0 ? "ja" : "bereits vorhanden");
        WriteField("Operator Summary", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        Console.WriteLine("Metrics");
        WriteField("Baseline", report.Hypothesis.BaselineMetrics);
        WriteField("Mutation", report.Hypothesis.MutationMetrics);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestQualityAudit()
    {
        WriteHeader("Hermes Strategy Backtest Quality Audit");
        var service = new StrategyBacktestQualityAuditService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Audited Backtests", report.AuditedBacktests.ToString());
        WriteField("Insufficient Sample", report.InsufficientSampleCount.ToString());
        WriteField("Low Confidence", report.LowConfidenceCount.ToString());
        WriteField("Medium Confidence", report.MediumConfidenceCount.ToString());
        WriteField("High Confidence", report.HighConfidenceCount.ToString());
        WriteField("Certification Ready", report.CertificationReadyCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Thresholds");
        foreach (var threshold in report.Thresholds)
        {
            WriteField(threshold.Key, threshold.Value.ToString());
        }
        WriteSubHeader("Entries");
        foreach (var entry in report.Entries.Take(10))
        {
            WriteField(entry.BacktestJobId, $"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · trades={entry.TradesSimulated} · class={entry.QualityClass}");
            WriteField("Confidence", $"{entry.ConfidenceLevel:0.###} · reliability={entry.StatisticalReliability:0.###}");
            WriteField("Gate", $"OOS={entry.EligibleForOos} WF={entry.EligibleForWalkForward} FWD={entry.EligibleForForwardTest} CERT={entry.EligibleForCertification}");
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestEvidenceGate()
    {
        WriteHeader("Hermes Strategy Backtest Evidence Gate");
        var service = new StrategyBacktestEvidenceGateService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Audited Backtests", report.AuditedBacktests.ToString());
        WriteField("Passed Research Gate", report.PassedResearchGateCount.ToString());
        WriteField("Passed OOS Gate", report.PassedOosGateCount.ToString());
        WriteField("Passed Certification Gate", report.PassedCertificationGateCount.ToString());
        WriteField("Insufficient History", report.InsufficientHistoryCount.ToString());
        WriteField("Insufficient Sample", report.InsufficientSampleCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Thresholds");
        foreach (var threshold in report.Thresholds)
        {
            WriteField(threshold.Key, threshold.Value.ToString());
        }
        WriteSubHeader("Entries");
        foreach (var entry in report.Entries.Take(10))
        {
            WriteField(entry.BacktestJobId, $"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · history={entry.HistoricalPeriodDays}d · trades={entry.TradesSimulated} · gate={entry.SampleClassification}");
            WriteField("Root Cause", entry.RootCause);
            WriteField("Passed", $"research={entry.PassedResearchGate} oos={entry.PassedOosGate} cert={entry.PassedCertificationGate}");
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestSignalDensityAnalyzer()
    {
        WriteHeader("Hermes Strategy Backtest Signal Density Analyzer");
        var service = new StrategyBacktestSignalDensityAnalyzerService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Audited Backtests", report.AuditedBacktests.ToString());
        WriteField("Historical Bars", report.FunnelTotals["historical_bars"].ToString());
        WriteField("Band Touches", report.FunnelTotals["band_touches"].ToString());
        WriteField("Rejections", report.FunnelTotals["rejections"].ToString());
        WriteField("Entry Candidates", report.FunnelTotals["entry_candidates"].ToString());
        WriteField("Simulated Trades", report.FunnelTotals["simulated_trades"].ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);
        WriteSubHeader("Density Scores");
        foreach (var density in report.DensityScores)
        {
            WriteField(density.Key, density.Value.ToString("0.####"));
        }
        WriteSubHeader("Entries");
        foreach (var entry in report.Entries)
        {
            WriteField(entry.BacktestJobId, $"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · bars={entry.HistoricalBars} · touches={entry.BollingerBandTouches} · rejections={entry.BollingerRejections} · candidates={entry.EntryCandidates} · trades={entry.SimulatedTrades}");
            WriteField("Root Cause", entry.RootCause);
            WriteField("Funnel", $"touch_rate={entry.TouchRate:0.####} rejection_rate={entry.RejectionRate:0.####} filter_pass_rate={entry.FilterPassRate:0.####} trade_conversion_rate={entry.TradeConversionRate:0.####}");
            WriteMessages("Recommendations", entry.Recommendations);
            WriteMessages("Warnings", entry.Warnings);
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyBacktestFailureLearning()
    {
        WriteHeader("Hermes Strategy Backtest Failure Learning");
        var service = new StrategyBacktestFailureLearningService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Backtest Job", report.BacktestJobId);
        WriteField("Strategy", $"{report.StrategyPattern} · {report.Asset} {report.Timeframe}");
        WriteField("Trades Simulated", report.TradesSimulated.ToString());
        WriteField("Win Rate", report.WinRate.ToString("0.####"));
        WriteField("Profit Factor", report.ProfitFactor.ToString("0.####"));
        WriteField("Max Drawdown", report.MaxDrawdown.ToString("0.####"));
        WriteField("Expectancy", report.Expectancy.ToString("0.####"));
        WriteField("Quality Class", report.QualityClass);
        WriteField("Certification Ready", report.CertificationReady.ToString().ToLowerInvariant());
        WriteField("Failed Backtest Evidence", report.FailedBacktestEvidence.ToString().ToLowerInvariant());
        WriteField("Learning Decision", report.LearningDecision);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Blocking Factors", report.BlockingFactors);
        WriteMessages("Root Causes", report.RootCauses);
        WriteMessages("Recommendations", report.Recommendations);
        WriteMessages("Mutation Suggestions", report.MutationSuggestions.Select(suggestion => $"{suggestion.Title}: {suggestion.Reason} -> {suggestion.ExpectedBenefit}").ToList());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowFailureGuidedMutationPlanner()
    {
        WriteHeader("Hermes Failure Guided Mutation Planner");
        var storagePaths = BuildStoragePaths();
        var service = new FailureGuidedMutationPlannerService(storagePaths, _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Source Job", report.SourceBacktestJobId);
        WriteField("Strategy", $"{report.StrategyPattern} · {report.Asset} {report.Timeframe}");
        WriteField("Trades Simulated", report.TradesSimulated.ToString());
        WriteField("Win Rate", report.WinRate.ToString("0.####"));
        WriteField("Profit Factor", report.ProfitFactor.ToString("0.####"));
        WriteField("Max Drawdown", report.MaxDrawdown.ToString("0.####"));
        WriteField("Expectancy", report.Expectancy.ToString("0.####"));
        WriteField("Quality Class", report.QualityClass);
        WriteField("Certification Ready", report.CertificationReady.ToString().ToLowerInvariant());
        WriteField("Learning Decision", report.LearningDecision);
        WriteField("Knowledge Update Tag", report.KnowledgeUpdateTag);
        WriteField("Mutation Candidates", report.MutationCandidatesCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Source Reports", report.SourceReports);
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Top Mutations", report.MutationCandidates
            .Take(8)
            .Select(candidate => $"{candidate.Title} [{candidate.Priority}] -> {candidate.ExpectedBenefit}")
            .ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMutationCandidateExport()
    {
        WriteHeader("Hermes Mutation Candidate Queue Export");
        var storagePaths = BuildStoragePaths();
        var service = new MutationCandidateQueueService(storagePaths);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue Size", report.QueueSize.ToString());
        WriteField("High Priority", report.HighPriorityCount.ToString());
        WriteField("Medium Priority", report.MediumPriorityCount.ToString());
        WriteField("Low Priority", report.LowPriorityCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Source Reports", report.SourceReports);
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Queue Items", report.QueueItems
            .Take(12)
            .Select(item => $"{item.MutationId} [{item.Priority}] -> {item.Reason}")
            .ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMutationValidationJobPlanner()
    {
        WriteHeader("Hermes Mutation Validation Job Planner");
        var storagePaths = BuildStoragePaths();
        var service = new MutationValidationJobPlannerService(storagePaths, _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Mutations Analyzed", report.MutationsAnalyzed.ToString());
        WriteField("Jobs Prepared", report.JobsPrepared.ToString());
        WriteField("Ready To Execute", report.ReadyToExecuteCount.ToString());
        WriteField("Waiting For Data", report.WaitingForDataCount.ToString());
        WriteField("Waiting For Engine Support", report.WaitingForEngineSupportCount.ToString());
        WriteField("Waiting For Specification", report.WaitingForSpecificationCount.ToString());
        WriteField("Blocked", report.BlockedCount.ToString());
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteField("Next Safe Step", report.NextSafeStep);
        WriteMessages("Source Reports", report.SourceReports);
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Jobs", report.Jobs
            .Take(12)
            .Select(job => $"{job.ValidationJobId} [{job.ReadinessStatus}] -> {job.Priority}")
            .ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowContradictions()
    {
        WriteHeader("Hermes Knowledge Contradictions");
        var storagePaths = BuildStoragePaths();
        var detector = new ContradictionDetector(storagePaths);
        var report = detector.Run();

        WriteContradictionReport(report, detector.ContradictionsPath, detailed: true);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowContradictionStatus()
    {
        WriteHeader("Hermes Contradiction Status");
        var storagePaths = BuildStoragePaths();
        var detector = new ContradictionDetector(storagePaths);
        var report = detector.LoadOrRun();

        WriteContradictionReport(report, detector.ContradictionsPath, detailed: false);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ReviewKnowledge()
    {
        WriteHeader("Hermes Human Knowledge Review");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <KNOWLEDGE_ITEM_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var result = ReadOption(_args, "--result") ?? "needs_review";
        var notes = ReadOption(_args, "--notes") ?? "cli_review_recorded";
        var reviewer = ReadOption(_args, "--reviewer") ?? "human";
        var storagePaths = BuildStoragePaths();
        var store = new HumanReviewEvidenceStore(storagePaths);
        var report = store.AddReview(id, result, reviewer, notes);
        var quality = new KnowledgeQualityEngine(storagePaths).Run();

        WriteHumanReviewReport(report, store.ReviewPath, detailed: true);
        var item = quality.Items.FirstOrDefault(item => item.KnowledgeId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            WriteSubHeader("Updated Trust");
            WriteField("Knowledge ID", item.KnowledgeId);
            WriteField("Lifecycle", item.LifecycleStatus);
            WriteField("Trust Score", $"{item.TrustScore:0.####}");
            WriteField("Quality Score", $"{item.QualityScore:0.####}");
            WriteMessages("Reasons", item.Reasons);
        }

        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewStatus()
    {
        WriteHeader("Hermes Human Review Status");
        var storagePaths = BuildStoragePaths();
        var store = new HumanReviewEvidenceStore(storagePaths);
        var report = store.LoadOrCreateReport();
        var workflow = new HumanReviewWorkflow(storagePaths);

        WriteHumanReviewReport(report, store.ReviewPath, detailed: true);
        WriteHumanReviewSummary(workflow.BuildSummary());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewQueue()
    {
        WriteHeader("Hermes Human Review Queue");
        var storagePaths = BuildStoragePaths();
        var workflow = new HumanReviewWorkflow(storagePaths);
        var queue = workflow.LoadOrCreateQueue();

        WriteHumanReviewQueue(queue, workflow.QueuePath);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewItem()
    {
        WriteHeader("Hermes Human Review Item");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <REVIEW_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var workflow = new HumanReviewWorkflow(BuildStoragePaths());
        var item = workflow.FindItem(id);
        if (item is null)
        {
            WriteWarning($"Kein Review Item gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteHumanReviewItem(item);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int DecideReview(string decision)
    {
        WriteHeader($"Hermes Human Review Decision: {decision}");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <REVIEW_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var note = ReadOption(_args, "--note") ?? "cli_review_decision";
        var reviewer = ReadOption(_args, "--reviewer") ?? "human";
        var storagePaths = BuildStoragePaths();
        var workflow = new HumanReviewWorkflow(storagePaths);
        HumanReviewDecision result;
        try
        {
            result = workflow.Decide(id, decision, note, reviewer);
        }
        catch (InvalidOperationException ex)
        {
            WriteWarning(ex.Message);
            WriteSafety();
            return 1;
        }

        WriteHumanReviewDecision(result);
        WriteHumanReviewSummary(workflow.BuildSummary());
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewSummary()
    {
        WriteHeader("Hermes Human Review Summary");
        var workflow = new HumanReviewWorkflow(BuildStoragePaths());

        WriteHumanReviewSummary(workflow.BuildSummary());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewPrioritizationAudit()
    {
        WriteHeader("Hermes Review Prioritization Audit");
        var service = new ReviewPrioritizationAuditService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Pending Reviews", report.TotalPendingReviews.ToString());
        WriteField("Trading Reviews", report.TradingReviews.ToString());
        WriteField("Documentation Reviews", report.DocumentationReviews.ToString());
        WriteField("Research Reviews", report.ResearchReviews.ToString());
        WriteField("Software Reviews", report.SoftwareReviews.ToString());
        WriteField("Process Reviews", report.ProcessReviews.ToString());
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top Priority Reviews");
        foreach (var review in report.TopPriorityReviews)
        {
            WriteField(review.Title, $"{review.Priority.ToUpperInvariant()} · {review.Domain} · trust={review.TrustBefore:0.####}");
            WriteField("Vorgeschlagene Aktion", review.Recommendation);
            WriteField("Warum", review.Reason);
            WriteField("Warum jetzt", review.PriorityReason);
        }
        WriteSubHeader("Gruppen");
        foreach (var group in report.DomainGroups)
        {
            WriteField(group.Domain, group.Count.ToString());
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewQueueHygieneAudit()
    {
        WriteHeader("Hermes Review Queue Hygiene Audit");
        var service = new ReviewQueueHygieneAuditService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Reviews gesamt", report.TotalReviews.ToString());
        WriteField("Auto-Close Kandidaten", report.AutoCloseCandidates.ToString());
        WriteField("Merge Kandidaten", report.MergeCandidates.ToString());
        WriteField("Veraltete Reviews", report.StaleReviews.ToString());
        WriteField("Low Value Reviews", report.LowValueReviews.ToString());
        WriteField("Duplikate", report.DuplicateReviews.ToString());
        WriteField("Potenzielle Reduktion", report.PotentialReduction.ToString());
        WriteField("Potenzielle Queue-Größe", report.PotentialQueueSizeAfterCleanup.ToString());
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Kandidaten");
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField(candidate.Title, $"{candidate.Category} · {candidate.Domain} · {candidate.SafeAutoCloseStatus}");
            WriteField("Knowledge Item", candidate.KnowledgeItemId);
            WriteField("Begründung", string.Join(" · ", candidate.Reasons));
            WriteField("Empfehlung", candidate.SuggestedAction);
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewDecisionAssistant()
    {
        WriteHeader("Hermes Review Decision Assistant");
        var service = new ReviewDecisionAssistantService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Review Count", report.ReviewCount.ToString());
        WriteField("High Priority", report.HighPriorityCount.ToString());
        WriteField("Freigabe empfohlen", report.RecommendedApprove.ToString());
        WriteField("Mehr Evidenz empfohlen", report.RecommendedMoreEvidence.ToString());
        WriteField("Ablehnung empfohlen", report.RecommendedReject.ToString());
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top 3 Entscheidungen für Frank");
        foreach (var entry in report.Entries.Take(3))
        {
            WriteField(entry.Title, $"{entry.RecommendationLabel} · {entry.Domain} · score={entry.ReviewActionScore:0.#} · klasse={entry.RecommendationClass}");
            WriteField("Domäne", entry.Domain);
            WriteField("Priorität", entry.Priority);
            WriteField("Empfehlung", entry.RecommendationLabel);
            WriteField("Warum jetzt", entry.WhyNow);
            WriteField("Fehlt", string.Join(" · ", entry.MissingEvidence));
            WriteField("Nächster Schritt", entry.NextStep);
        }
        WriteTrustedKnowledgeContext("review decision assistant", ExtractKnowledgeTopic(
            report.Entries.FirstOrDefault()?.KnowledgeItemId,
            report.Entries.FirstOrDefault()?.Title,
            report.Entries.FirstOrDefault()?.Domain,
            report.Entries.FirstOrDefault()?.RecommendationLabel,
            report.Entries.FirstOrDefault()?.WhyNow,
            report.Entries.FirstOrDefault()?.NextStep));
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteTrustedKnowledgeContext(string analysisLabel, string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            WriteField($"{analysisLabel} Trusted Knowledge", "topic not inferred");
            return;
        }

        var reasoning = new KnowledgeReasoningService(BuildStoragePaths()).Run(topic);
        WriteSubHeader($"{analysisLabel} Trusted Knowledge");
        WriteField("Topic", reasoning.Topic);
        WriteField("Confidence", reasoning.Confidence.ToString("0.###", CultureInfo.InvariantCulture));
        WriteMessages("Trusted Knowledge IDs", reasoning.UsedKnowledgeIds);
        WriteMessages("Reasoning Steps", reasoning.ReasoningSteps);
        WriteMessages("Recommendations", reasoning.Recommendations);
        WriteMessages("Open Uncertainties", reasoning.OpenUncertainties);
    }

    private static string? ExtractKnowledgeTopic(params string?[] values)
    {
        var joined = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(joined))
        {
            return null;
        }

        var normalized = joined.Replace("_", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .ToLowerInvariant();
        var tokenSet = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<(string Topic, string[] Aliases, int Weight)>
        {
            ("bullish engulfing", ["bullish engulfing", "bullish", "engulfing"], 100),
            ("bearish engulfing", ["bearish engulfing", "bearish", "engulfing"], 100),
            ("double top", ["double top", "doubletop", "top"], 96),
            ("double bottom", ["double bottom", "doublebottom", "bottom"], 96),
            ("support resistance", ["support resistance", "support", "resistance"], 94),
            ("inside bar", ["inside bar", "insidebar"], 92),
            ("breakout", ["breakout", "break outs", "break out"], 90),
            ("gap trading", ["gap trading", "gap trade", "gap"], 88),
            ("daytrading", ["daytrading", "day trading", "intraday"], 86),
            ("pullback", ["pullback", "pull back"], 84),
            ("pin bar", ["pin bar", "pinbar"], 82),
            ("hammer", ["hammer"], 80),
            ("doji", ["doji"], 80),
            ("liquidity sweep", ["liquidity sweep", "liquidity", "sweep"], 78),
            ("mean reversion", ["mean reversion", "mean revert", "reversion"], 76)
        };

        var ranked = candidates
            .Select(candidate => new
            {
                candidate.Topic,
                Score = candidate.Aliases.Sum(alias =>
                    normalized.Contains(alias, StringComparison.OrdinalIgnoreCase)
                        ? candidate.Weight
                        : alias.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Count(part => tokenSet.Contains(part)) * 12),
                AliasHits = candidate.Aliases.Count(alias => normalized.Contains(alias, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.AliasHits)
            .ToList();

        var best = ranked.FirstOrDefault(item => item.Score > 0);
        return best?.Topic;
    }

    private int ShowDomainAwareReviewPrioritization()
    {
        WriteHeader("Hermes Domain-Aware Review Prioritization");
        var service = new DomainAwareReviewPrioritizationService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Reviews gesamt", report.TotalReviews.ToString());
        WriteField("Trading", report.TradingReviews.ToString());
        WriteField("Knowledge", report.KnowledgeReviews.ToString());
        WriteField("Runtime", report.RuntimeReviews.ToString());
        WriteField("Dokumentation", report.DocumentationReviews.ToString());
        WriteField("Prozess", report.ProcessReviews.ToString());
        WriteField("Unbekannt", report.UnknownReviews.ToString());
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top Trading Decisions");
        foreach (var group in report.TopTradingDecisions)
        {
            foreach (var entry in group.Reviews)
            {
                WriteField(entry.Title, $"{entry.ReprioritizationClass} · confidence={entry.ConfidenceScore:0.#}% · gain={entry.ConfidenceGainScore} · score={entry.ReprioritizationScore}");
                WriteField("Nächster Evidenzschritt", entry.NextEvidenceStep);
                WriteField("Blocker", entry.StrongestBlockers);
            }
        }
        WriteSubHeader("Runtime / Documentation");
        foreach (var entry in report.TopRuntimeReviews.SelectMany(group => group.Reviews).Concat(report.DocumentationLater.Take(3)))
        {
            WriteField(entry.Title, $"{entry.ClassifiedDomain} · {entry.ConfidenceClass} · note={entry.OperatorNote}");
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewActionPlan()
    {
        WriteHeader("Hermes Review Action Plan");
        var service = new ReviewActionPlanService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Action Plans", report.ActionPlans.ToString());
        WriteField("Hermes kann weiterarbeiten", report.HermesCanContinue.ToString());
        WriteField("Frank nötig", report.FrankDecisionRequired > 0 ? "ja" : "nein");
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.OperatorSummary);
        WriteSubHeader("Top Trading Action Plans");
        foreach (var entry in report.Entries)
        {
            WriteField(entry.Title, $"{entry.ActionStatus} · {entry.CurrentRecommendation} · confidence={entry.ConfidenceScore:0.#}%");
            WriteField("Fehlt", string.Join(" · ", entry.MissingEvidence));
            WriteField("Nächster Schritt", entry.NextEvidenceStep);
            WriteField("Hermes kann selbst weiterarbeiten", entry.CanHermesActAutonomously ? "ja" : "nein");
            WriteField("Frank nötig", entry.FrankRequired ? "ja" : "nein");
            WriteField("Autonomer Command", entry.AutonomousCommand);
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowReviewStatusConsistencyAudit()
    {
        WriteHeader("Hermes Review / Master Status Consistency Audit");
        var service = new ReviewStatusConsistencyAuditService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Reviews gesamt", report.TotalReviews.ToString());
        WriteField("Pending laut Queue", report.PendingReviewsQueue.ToString());
        WriteField("Pending laut Master", report.PendingReviewsMaster.ToString());
        WriteField("Needs More Evidence laut Queue", report.NeedsMoreEvidenceQueue.ToString());
        WriteField("Needs More Evidence laut Master", report.NeedsMoreEvidenceMaster.ToString());
        WriteField("Source of Truth", report.SourceOfTruth);
        WriteField("Ursache", report.Cause);
        WriteField("Korrektur", report.RecommendedCorrection);
        WriteMessages("Abweichungen", report.Deviations);
        WriteSubHeader("Snapshot-Kandidaten");
        foreach (var snapshot in report.MasterSnapshots)
        {
            WriteField(snapshot.Source, $"{DisplayPath(snapshot.Path)} · pending={snapshot.PendingReviews} · needs_more_evidence={snapshot.NeedsMoreEvidenceReviews} · updated={snapshot.LastUpdatedUtc:O}");
        }
        WriteSubHeader("Top-Reviews");
        foreach (var review in report.Reviews.Take(10))
        {
            WriteField(review.Title, $"{review.Domain} · {review.QueueStatus} · {review.QueueRecommendation} · source={review.Source}");
            WriteField("Letzte Aktualisierung", review.LastUpdatedUtc?.ToString("O") ?? "-");
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunEvidenceAutoLoop()
    {
        WriteHeader("Hermes Evidence Auto Loop");
        var service = new EvidenceAutoLoopService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Reviews gelesen", report.ReviewCount.ToString());
        WriteField("Mehr-Evidenz-Reviews", report.MoreEvidenceReviews.ToString());
        WriteField("Geplante Tasks", report.PlannedTasks.ToString());
        WriteField("Trading Tasks", report.TradingTasks.ToString());
        WriteField("Documentation Tasks", report.DocumentationTasks.ToString());
        WriteField("Validation Tasks", report.ValidationTasks.ToString());
        WriteField("Evidence Tasks", report.EvidenceTasks.ToString());
        WriteField("Frank nötig", report.FrankRequired > 0 ? "ja" : "nein");
        WriteField("Scheduler configured", report.SchedulerConfigured.ToString().ToLowerInvariant());
        WriteField("Scheduler enabled", report.SchedulerEnabled.ToString().ToLowerInvariant());
        WriteField("last_run", report.LastRunUtc ?? "-");
        WriteField("next_run", report.NextRunUtc ?? report.NextRunHint);
        WriteSubHeader("Operator Summary");
        Console.WriteLine(report.NextAction);
        WriteField("Hauptpriorität", report.TradingTasks > 0 ? "Trading" : report.DocumentationTasks > 0 ? "Documentation" : "allgemein");
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunEvidenceTasks()
    {
        WriteHeader("Hermes Evidence Task Execution");
        var service = new EvidenceTaskExecutionService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Quelle", DisplayPath(report.SourceReportPath));
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Tasks gefunden", report.TasksFound.ToString());
        WriteField("Tasks ausgeführt", report.TasksExecuted.ToString());
        WriteField("Tasks übersprungen", report.TasksSkipped.ToString());
        WriteField("Unbekannte Tasks", report.UnsupportedTasks.ToString());
        WriteField("Neue Evidenz", report.EvidenceCollected.ToString());
        WriteField("Validation Tasks", report.ValidationTasksExecuted.ToString());
        WriteField("Needs More Evidence vorher", report.NeedsMoreEvidenceBefore.ToString());
        WriteField("Needs More Evidence nachher", report.NeedsMoreEvidenceAfter.ToString());
        WriteField("Pending Reviews vorher", report.PendingReviewsBefore.ToString());
        WriteField("Pending Reviews nachher", report.PendingReviewsAfter.ToString());
        WriteField("Frank nötig", report.FrankActionRequired ? "ja" : "nein");
        WriteField("Nächste Aktion", report.FrankActionRequired ? "Frank muss entscheiden." : "Hermes sammelt weitere Evidenz.");
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowEvidenceAutoLoopStatus()
    {
        WriteHeader("Hermes Evidence Auto Loop Status");

        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var config = scheduler.LoadConfig();
        var service = new EvidenceAutoLoopService(storagePaths);
        var report = service.Load();
        var timeControl = scheduler.GetTimeControlStatus();
        var runtimeState = service.GetRuntimeState();
        var nextRunDisplay = report?.NextRunUtc
            ?? runtimeState.NextRunUtc?.ToString("O")
            ?? config.EvidenceAutoLoopNextRunUtc?.ToString("O")
            ?? runtimeState.NextRunHint;
        var statusLabel = runtimeState.Enabled
            ? (runtimeState.Active ? "Aktiv – wartet auf Ausführung oder läuft" : "Aktiviert – wartet auf Lernfenster")
            : "Deaktiviert";
        var configured = config.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase))
            || config.EvidenceAutoLoopEnabled;

        WriteField("Konfiguriert", configured.ToString().ToLowerInvariant());
        WriteField("Aktiviert", runtimeState.Enabled.ToString().ToLowerInvariant());
        WriteField("Modus", statusLabel);
        WriteField("Arbeitsfenster", config.EvidenceAutoLoopWindow);
        WriteField("Max Tasks pro Lauf", config.EvidenceAutoLoopMaxTasksPerRun.ToString(CultureInfo.InvariantCulture));
        WriteField("Trading priorisiert", config.EvidenceAutoLoopPrioritizeTrading.ToString().ToLowerInvariant());
        WriteField("Nur Lernfenster", config.EvidenceAutoLoopRunOnlyInLearningWindow.ToString().ToLowerInvariant());
        WriteField("Letzter Lauf", report?.LastRunUtc ?? config.EvidenceAutoLoopLastRunUtc?.ToString("O") ?? "-");
        WriteField("Nächster Lauf", nextRunDisplay);
        WriteField("Geplante Tasks", report?.PlannedTasks.ToString(CultureInfo.InvariantCulture) ?? "0");
        WriteField("Frank nötig", (report?.FrankRequired ?? 0) > 0 ? "ja" : "nein");
        WriteField("Aktueller Modus", report?.NextAction ?? (runtimeState.Enabled ? "Hermes plant weitere Evidenzläufe." : "Evidenz Auto-Loop pausiert."));
        WriteMessages("Warnings", report?.Warnings ?? []);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }



    private int ShowReviewEvidenceRefresh()
    {
        var storagePaths = BuildStoragePaths();
        var service = new ReviewEvidenceRefreshService(storagePaths);
        var report = service.Run();

        WriteHeader("Review Evidence Refresh");
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Pending Reviews gelesen", report.PendingReviewsRead.ToString(CultureInfo.InvariantCulture));
        WriteField("Reviews aktualisiert", report.ReviewsUpdated.ToString(CultureInfo.InvariantCulture));
        WriteField("Reviews unverändert", report.ReviewsUnchanged.ToString(CultureInfo.InvariantCulture));
        WriteField("Vertrauen verbessert", report.TrustImprovedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("Qualität verbessert", report.QualityImprovedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("Validierung verbessert", report.ValidationImprovedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("Evidenz verbessert", report.EvidenceImprovedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("Empfehlung geändert", report.RecommendationChangedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("Freigabe empfohlen", report.RecommendedApprove.ToString(CultureInfo.InvariantCulture));
        WriteField("Mehr Evidenz empfohlen", report.RecommendedMoreEvidence.ToString(CultureInfo.InvariantCulture));
        WriteField("Ablehnung empfohlen", report.RecommendedReject.ToString(CultureInfo.InvariantCulture));
        WriteField("Frank nötig", report.FrankActionRequired ? "ja" : "nein");
        WriteField("Operator Summary", report.OperatorSummary);
        WriteMessages("Warnings", report.Warnings);

        WriteHeader("Reviews");
        foreach (var item in report.Reviews.Take(20))
        {
            Console.WriteLine($"- {item.Title} ({item.Domain}, {item.RecommendationAfter})");
            Console.WriteLine($"  Vorher: trust={item.TrustBefore:0.####}, quality={item.QualityBefore:0.####}, validation={item.ValidationBefore:0.####}, evidence={item.EvidenceBefore:0.####}, recommendation={item.RecommendationBefore}");
            Console.WriteLine($"  Nachher: trust={item.TrustAfter:0.####}, quality={item.QualityAfter:0.####}, validation={item.ValidationAfter:0.####}, evidence={item.EvidenceAfter:0.####}, recommendation={item.RecommendationAfter}");
            Console.WriteLine($"  Frank-Aktion: {item.FrankAction}");
            Console.WriteLine($"  Warum: {string.Join(", ", item.BlockingReasons)}");
        }

        WriteSafety();
        return 0;
    }

    private int ShowEvidenceImpactAnalysis()
    {
        var storagePaths = BuildStoragePaths();
        var service = new EvidenceImpactAnalysisService(storagePaths);
        var report = service.Run();

        WriteHeader("Evidence Impact Analysis");
        WriteField("Reviews", report.ReviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("High Priority", report.HighPriorityCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Unchanged Recommendations", report.UnchangedRecommendations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Changed Recommendations", report.ChangedRecommendations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Freigabe empfohlen", report.RecommendedApprove.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Mehr Evidenz empfohlen", report.RecommendedMoreEvidence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Ablehnung empfohlen", report.RecommendedReject.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteField("Operator Summary", report.OperatorSummary);
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Before Report", DisplayPath(report.BeforeReportPath));
        WriteField("After Report", DisplayPath(report.AfterReportPath));
        WriteField("Evidence Task Execution", DisplayPath(report.EvidenceTaskExecutionPath));
        Console.WriteLine();
        Console.WriteLine("Blocker");
        foreach (var item in report.BlockingMetricCounts.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteField(item.Key, item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Console.WriteLine();
        Console.WriteLine("Reviews");
        foreach (var item in report.Reviews.Take(20))
        {
            Console.WriteLine($"- {item.Title} ({item.Domain}, {item.RecommendationAfter})");
            Console.WriteLine($"  Vorher: trust={item.TrustBefore:0.####}, quality={item.QualityBefore:0.####}, validation={item.ValidationBefore:0.####}, evidence={item.EvidenceScoreBefore:0.####}, recommendation={item.RecommendationBefore}");
            Console.WriteLine($"  Nachher: trust={item.TrustAfter:0.####}, quality={item.QualityAfter:0.####}, validation={item.ValidationAfter:0.####}, evidence={item.EvidenceScoreAfter:0.####}, recommendation={item.RecommendationAfter}");
            Console.WriteLine($"  Blocker: {item.BlockingMetric}");
            Console.WriteLine($"  Fehlt für Freigabe: {item.MissingForApprove}");
            Console.WriteLine($"  Fehlt für mehr Evidenz: {item.MissingForMoreEvidence}");
            Console.WriteLine($"  Fehlt für Ablehnung: {item.MissingForReject}");
            Console.WriteLine($"  Hinweise: {string.Join(", ", item.BlockingReasons)}");
        }

        return 0;
    }

    private int ShowEvidenceTaskExecution()
    {
        WriteHeader("Hermes Evidence Task Execution");
        var service = new EvidenceTaskExecutionService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Quelle", DisplayPath(report.SourceReportPath));
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Tasks gefunden", report.TasksFound.ToString());
        WriteField("Tasks ausgeführt", report.TasksExecuted.ToString());
        WriteField("Tasks übersprungen", report.TasksSkipped.ToString());
        WriteField("Unbekannte Tasks", report.UnsupportedTasks.ToString());
        WriteField("Neue Evidenz", report.EvidenceCollected.ToString());
        WriteField("Validation Tasks", report.ValidationTasksExecuted.ToString());
        WriteField("Needs More Evidence vorher", report.NeedsMoreEvidenceBefore.ToString());
        WriteField("Needs More Evidence nachher", report.NeedsMoreEvidenceAfter.ToString());
        WriteField("Pending Reviews vorher", report.PendingReviewsBefore.ToString());
        WriteField("Pending Reviews nachher", report.PendingReviewsAfter.ToString());
        WriteField("Frank nötig", report.FrankActionRequired ? "ja" : "nein");
        WriteField("Nächste Aktion", report.FrankActionRequired ? "Frank muss entscheiden." : "Hermes sammelt weitere Evidenz.");
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int SetEvidenceAutoLoopEnabled(bool enabled)
    {
        WriteHeader(enabled ? "Hermes Evidence Auto Loop Aktivieren" : "Hermes Evidence Auto Loop Deaktivieren");

        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var updated = scheduler.UpdateEvidenceAutoLoopEnabled(enabled);
        var status = updated.BuildTimeControlStatus(DateTimeOffset.UtcNow, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var runtimeState = new EvidenceAutoLoopService(storagePaths).GetRuntimeState();
        var nextRunHint = runtimeState.NextRunUtc?.ToString("O") ?? runtimeState.NextRunHint;
        if (enabled && runtimeState.NextRunUtc is not null)
        {
            updated = scheduler.UpdateEvidenceAutoLoopRunState(updated.EvidenceAutoLoopLastRunUtc, runtimeState.NextRunUtc);
        }

        WriteField("Aktiviert", updated.EvidenceAutoLoopEnabled.ToString().ToLowerInvariant());
        WriteField("Job vorhanden", updated.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant());
        WriteField("Konfigurationsfenster", updated.EvidenceAutoLoopWindow);
        WriteField("Max Tasks pro Lauf", updated.EvidenceAutoLoopMaxTasksPerRun.ToString(CultureInfo.InvariantCulture));
        WriteField("Zeitsteuerung", status.StatusLabel);
        WriteField("In Work Window", status.InWorkWindow.ToString().ToLowerInvariant());
        WriteField("Last Run", updated.EvidenceAutoLoopLastRunUtc?.ToString("O") ?? "-");
        WriteField("Next Run", updated.EvidenceAutoLoopNextRunUtc?.ToString("O") ?? nextRunHint);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int SetValidationBacklogExecutorEnabled(bool enabled)
    {
        WriteHeader(enabled ? "Hermes Validation Backlog Executor Aktivieren" : "Hermes Validation Backlog Executor Deaktivieren");

        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var updated = scheduler.UpdateValidationBacklogExecutorEnabled(enabled);
        var status = updated.BuildTimeControlStatus(DateTimeOffset.UtcNow, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var service = new ValidationBacklogExecutorService(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var runtimeReport = service.Load();

        if (enabled)
        {
            var nextRun = updated.ValidationBacklogExecutorNextRunUtc
                ?? runtimeReport?.NextRunUtc
                ?? DetermineValidationBacklogExecutorNextRun(updated, status, enabled);
            if (nextRun is not null)
            {
                updated = scheduler.UpdateValidationBacklogExecutorRunState(updated.ValidationBacklogExecutorLastRunUtc, nextRun);
            }
        }

        WriteField("Aktiviert", updated.ValidationBacklogExecutorEnabled.ToString().ToLowerInvariant());
        WriteField("Job vorhanden", updated.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant());
        WriteField("Konfigurationsfenster", updated.ValidationBacklogExecutorWindow);
        WriteField("Max Tasks pro Lauf", updated.ValidationBacklogExecutorMaxTasksPerRun.ToString(CultureInfo.InvariantCulture));
        WriteField("Zeitsteuerung", status.StatusLabel);
        WriteField("In Work Window", status.InWorkWindow.ToString().ToLowerInvariant());
        WriteField("Last Run", updated.ValidationBacklogExecutorLastRunUtc?.ToString("O") ?? runtimeReport?.LastRunUtc?.ToString("O") ?? "-");
        WriteField("Next Run", updated.ValidationBacklogExecutorNextRunUtc?.ToString("O") ?? runtimeReport?.NextRunUtc?.ToString("O") ?? "Nächster Lauf wird beim Scheduler-Lauf berechnet.");
        WriteField("Frank nötig", ((runtimeReport?.FrankRequired ?? 0) > 0).ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationBacklogExecutorStatus()
    {
        WriteHeader("Hermes Validation Backlog Executor Status");

        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var config = scheduler.LoadConfig();
        var service = new ValidationBacklogExecutorService(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var report = service.Load();
        var timeControl = scheduler.GetTimeControlStatus();

        var configured = config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase))
            || config.ValidationBacklogExecutorEnabled;
        var enabled = config.ValidationBacklogExecutorEnabled
            || config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase) && job.Enabled);
        var statusLabel = enabled
            ? (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow
                ? "Aktiv"
                : "Aktiviert – wartet auf Lernfenster")
            : "Deaktiviert";
        var modeLabel = enabled
            ? (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow
                ? "läuft oder wartet auf Ausführung"
                : "wartet auf Lernfenster")
            : "deaktiviert";
        var computedNextRun = DetermineValidationBacklogExecutorNextRun(config, timeControl, enabled);

        WriteField("Konfiguriert", configured.ToString().ToLowerInvariant());
        WriteField("Aktiviert", enabled.ToString().ToLowerInvariant());
        WriteField("Modus", modeLabel);
        WriteField("Status", statusLabel);
        WriteField("Fenster", config.ValidationBacklogExecutorWindow);
        WriteField("Zeitsteuerung", timeControl.StatusLabel);
        WriteField("Max Tasks pro Lauf", (report?.MaxTasksPerRun ?? config.ValidationBacklogExecutorMaxTasksPerRun).ToString(CultureInfo.InvariantCulture));
        WriteField("Letzter Lauf", report?.LastRunUtc?.ToString("O") ?? config.ValidationBacklogExecutorLastRunUtc?.ToString("O") ?? "-");
        WriteField("Nächster Lauf", report?.NextRunUtc?.ToString("O") ?? config.ValidationBacklogExecutorNextRunUtc?.ToString("O") ?? computedNextRun?.ToString("O") ?? "Nächster Lauf wird beim Scheduler-Lauf berechnet.");
        WriteField("Ausgeführte Schritte", report?.ExecutedSteps.ToString(CultureInfo.InvariantCulture) ?? "0");
        WriteField("Validation Tasks erzeugt", report?.ValidationTasksCreated.ToString(CultureInfo.InvariantCulture) ?? "0");
        WriteField("Evidence Tasks ausgeführt", report?.EvidenceTasksExecuted.ToString(CultureInfo.InvariantCulture) ?? "0");
        WriteField("Reviews aktualisiert", report?.ReviewsRefreshed.ToString(CultureInfo.InvariantCulture) ?? "0");
        WriteField("Frank nötig", ((report?.FrankRequired ?? 0) > 0).ToString().ToLowerInvariant());
        WriteMessages("Warnings", report?.Warnings ?? []);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private static DateTimeOffset? DetermineValidationBacklogExecutorNextRun(
        ScheduleConfig config,
        ScheduleTimeControlStatus timeControl,
        bool enabled)
    {
        if (!enabled)
        {
            return null;
        }

        if (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow)
        {
            return DateTimeOffset.UtcNow;
        }

        var zone = ResolveTimeZone(config.TimeZone);
        var currentLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var candidates = new List<DateTimeOffset>();

        foreach (var windowStart in new[]
        {
            config.LearningWindow.Enabled ? config.LearningWindow.Start : null,
            config.NightlyWindow.Enabled ? config.NightlyWindow.Start : null,
        })
        {
            if (!TimeOnly.TryParse(windowStart, out var start))
            {
                continue;
            }

            var candidateLocal = currentLocal.Date + start.ToTimeSpan();
            if (candidateLocal <= currentLocal.DateTime)
            {
                candidateLocal = candidateLocal.AddDays(1);
            }

            candidates.Add(new DateTimeOffset(candidateLocal, zone.GetUtcOffset(candidateLocal)));
        }

        return candidates.Count > 0 ? candidates.Min() : null;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId)
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private int ConsolidateMemory()
    {
        WriteHeader("Hermes Memory Consolidation");
        var storagePaths = BuildStoragePaths();
        var service = new MemoryConsolidationService(storagePaths);
        var report = service.Run();

        WriteField("Consolidation Report", DisplayPath(service.ConsolidationPath));
        WriteField("Knowledge Quality", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Knowledge Evidence", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Total Knowledge", report.TotalKnowledgeItems.ToString());
        WriteField("Active", report.ActiveKnowledge.ToString());
        WriteField("Archived", report.ArchivedKnowledge.ToString());
        WriteField("Deprecated", report.DeprecatedKnowledge.ToString());
        WriteField("Weak", report.WeakKnowledge.ToString());
        WriteField("Duplicate Groups", report.DuplicateGroups.ToString());
        WriteField("Prioritized", report.PrioritizedKnowledge.ToString());
        foreach (var entry in report.Entries.Take(20))
        {
            Console.WriteLine();
            WriteField(entry.KnowledgeId, $"{entry.Action} / {entry.LifecycleStatus} / quality={entry.QualityScore:0.####}");
            WriteField("Reason", entry.Reason);
            WriteMessages("Related", entry.RelatedKnowledgeIds.Take(6).ToList());
        }

        WriteMessages("Warnings", report.Warnings);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int GenerateValidationPlans()
    {
        WriteHeader("Hermes Generate Knowledge Validation Plans");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 50, min: 1, max: 500);
        var storagePaths = BuildStoragePaths();
        var service = new KnowledgeValidationStrategy(storagePaths);
        var report = service.GeneratePlans(maxItems);

        WriteValidationPlanReport(report, service.PlansPath);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationPlans()
    {
        WriteHeader("Hermes Knowledge Validation Plans");
        var service = new KnowledgeValidationStrategy(BuildStoragePaths());
        var report = service.LoadPlanReport() ?? service.GeneratePlans(50);

        WriteValidationPlanReport(report, service.PlansPath);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ValidateKnowledge()
    {
        WriteHeader("Hermes Validate Knowledge");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 500);
        var storagePaths = BuildStoragePaths();
        var service = new KnowledgeValidationStrategy(storagePaths);
        var status = service.ValidateKnowledge(maxItems);

        WriteKnowledgeValidationStatus(status);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExecuteValidationTasks()
    {
        WriteHeader("Hermes Execute Knowledge Validation Tasks");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 200);
        var storagePaths = BuildStoragePaths();
        var executor = new KnowledgeValidationExecutor(storagePaths);
        var results = executor.Execute(maxItems);

        WriteValidationExecutionSummary(results, executor.ExecutionLogPath);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ValidateDomainKnowledge()
    {
        WriteHeader("Hermes Domain-specific Knowledge Validation");
        var domain = ReadOption(_args, "--domain") ?? "documentation";
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 200);
        var storagePaths = BuildStoragePaths();
        var executor = new KnowledgeValidationExecutor(storagePaths);
        var results = executor.ExecuteDomain(domain, maxItems);
        var status = new DomainKnowledgeValidationService(storagePaths).BuildStatus();

        WriteField("Domain", domain);
        WriteValidationExecutionSummary(results, executor.ExecutionLogPath);
        WriteDomainValidationStatus(status);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainValidationStatus()
    {
        WriteHeader("Hermes Domain Validation Status");
        var service = new DomainKnowledgeValidationService(BuildStoragePaths());
        var status = service.BuildStatus();

        WriteDomainValidationStatus(status);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationExecutionLog()
    {
        WriteHeader("Hermes Knowledge Validation Execution Log");
        var executor = new KnowledgeValidationExecutor(BuildStoragePaths());
        var results = executor.LoadResults(50);

        WriteValidationExecutionSummary(results, executor.ExecutionLogPath);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationRoutingStatus()
    {
        WriteHeader("Hermes Domain Validation Routing Status");
        var status = new DomainValidationRouter(BuildStoragePaths()).BuildStatus();

        WriteValidationRoutingStatus(status);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int CleanupInvalidValidationTasks()
    {
        WriteHeader("Hermes Cleanup Invalid Validation Tasks");
        var storagePaths = BuildStoragePaths();
        var result = new ResearchQueueService(storagePaths).CleanupInvalidValidationTasks();
        var status = new KnowledgeValidationStrategy(storagePaths).BuildStatus();

        WriteField("Invalid Tasks Before Cleanup", result.InvalidValidationTasks.ToString());
        WriteField("Cleaned Tasks", result.ValidationTasksCleaned.ToString());
        WriteField("Routing Health", status.ValidationRoutingHealth);
        WriteMessages("Cleaned Queue Items", result.CleanedQueueItemIds.Take(20).ToList());
        WriteMessages("Warnings", result.Warnings);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainValidationRouting()
    {
        WriteHeader("Hermes Explain Validation Routing");
        var domain = ReadOption(_args, "--domain") ?? "trading";
        var router = new DomainValidationRouter(BuildStoragePaths());
        var profile = router.ProfileFor(domain);

        WriteValidationRoutingProfile(profile);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeValidationStatus()
    {
        WriteHeader("Hermes Knowledge Validation Status");
        var service = new KnowledgeValidationStrategy(BuildStoragePaths());
        var status = service.BuildStatus();

        WriteKnowledgeValidationStatus(status);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationEvidenceStatus()
    {
        WriteHeader("Hermes Validation Evidence Pipeline");
        var service = new ValidationEvidencePipelineService(BuildStoragePaths());
        var report = service.LoadStatus();

        WriteValidationEvidencePipelineReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunValidationEvidence()
    {
        WriteHeader("Hermes Validation Evidence Pipeline");
        var service = new ValidationEvidencePipelineService(BuildStoragePaths());
        var apply = HasArg("--apply");
        var dryRun = HasArg("--dry-run");

        if (apply && dryRun)
        {
            Console.WriteLine("Error: use either --dry-run or --apply, not both.");
            WriteSafety();
            return 1;
        }

        var report = service.Run(apply: apply && !dryRun, dryRun: dryRun || !apply);
        WriteValidationEvidencePipelineReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationStateSyncStatus()
    {
        WriteHeader("Hermes Validation State Synchronizer");
        var service = new ValidationStateSynchronizerService(BuildStoragePaths());
        var report = service.LoadStatus();

        WriteValidationStateSyncReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunValidationStateSync()
    {
        WriteHeader("Hermes Validation State Synchronizer");
        var service = new ValidationStateSynchronizerService(BuildStoragePaths());
        var apply = HasArg("--apply");
        var dryRun = HasArg("--dry-run");

        if (apply && dryRun)
        {
            Console.WriteLine("Error: use either --dry-run or --apply, not both.");
            WriteSafety();
            return 1;
        }

        var report = service.Run(apply: apply && !dryRun, dryRun: dryRun || !apply);
        WriteValidationStateSyncReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeValidationAudit()
    {
        WriteHeader("Hermes Knowledge Validation Audit");
        var service = new KnowledgeValidationAuditService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.AuditPath));
        WriteField("Markdown", DisplayPath(service.AuditMarkdownPath));
        WriteField("Knowledge Items", report.TotalKnowledgeItems.ToString());
        WriteField("Validiert", report.ValidatedKnowledgeItems.ToString());
        WriteField("OOS nötig", report.KnowledgeItemsNeedingOosValidation.ToString());
        WriteField("Ohne Validation Queue", report.KnowledgeItemsWithoutValidationQueue.ToString());
        WriteField("Validierung", report.ValidationCompletionLabel);
        WriteField("Offene Validierungen", report.OpenValidations.ToString());
        WriteField("Validation Tasks Pending", report.ValidationTasksPending.ToString());
        WriteField("Kritische Wissenslücken", report.CriticalKnowledgeGaps.ToString());
        WriteField("Älteste offene Validierung", $"{report.OldestOpenValidationAgeDays} Tage");
        WriteField("Needs More Evidence", report.HumanReviewNeedsMoreEvidenceReviews.ToString());
        WriteField("Frank nötig", report.HumanReviewPendingReviews > 0 ? "ja" : "nein");
        WriteField("Validation Queue vorhanden", report.ValidationQueueExists.ToString().ToLowerInvariant());
        WriteField("Validation Queue befüllt", report.ValidationQueueFilled.ToString().ToLowerInvariant());
        WriteField("Validation Queue verarbeitet", report.ValidationQueueProcessed.ToString().ToLowerInvariant());
        WriteMessages("Betroffene Domänen", report.AffectedDomains);
        WriteMessages("Betroffene Domänen (Evidenz)", report.HumanReviewNeedsMoreEvidenceDomains);
        foreach (var domain in report.DomainBreakdown)
        {
            WriteSubHeader(domain.Domain);
            WriteField("Offene Pläne", domain.OpenPlans.ToString());
            WriteField("Offene Queue-Items", domain.OpenQueueItems.ToString());
            WriteField("Betroffene Knowledge Items", domain.OpenKnowledgeItems.ToString());
            WriteField("Älteste offene Validierung", $"{domain.OldestOpenValidationAgeDays} Tage");
        }
        WriteSubHeader("Top 10 offene Validierungsprobleme");
        foreach (var finding in report.Findings ?? [])
        {
            WriteField(finding.Title, $"{finding.Category} · {finding.Count} · {string.Join(", ", finding.Domains.Take(3))}");
            WriteField("Bedeutung", finding.Meaning);
            WriteField("Aktion", finding.Action);
        }
        WriteSubHeader("Handlungsempfehlung");
        Console.WriteLine(report.OperatorSummary);
        if ((report.MissingAutomationJobs?.Count ?? 0) > 0)
        {
            WriteMessages("Fehlende automatische Jobs", report.MissingAutomationJobs);
        }
        if ((report.MissingQueues?.Count ?? 0) > 0)
        {
            WriteMessages("Fehlende Queues", report.MissingQueues);
        }
        if ((report.NextRecommendedCommands?.Count ?? 0) > 0)
        {
            WriteMessages("Nächste Commands", report.NextRecommendedCommands);
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationBacklogAnalyzer()
    {
        WriteHeader("Hermes Validation Backlog Analyzer");
        var service = new ValidationBacklogAnalyzerService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Load() ?? service.Build();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Validierungsstau", report.OpenValidationsByDomain.Sum(item => item.PendingCount).ToString());
        WriteField("software_validation_pending", report.SoftwareValidationPending.ToString());
        WriteField("process_validation_pending", report.ProcessValidationPending.ToString());
        WriteField("research_validation_pending", report.ResearchValidationPending.ToString());
        WriteField("documentation_validation_pending", report.DocumentationValidationPending.ToString());
        WriteField("validation_plans_open", report.ValidationPlansOpen.ToString());
        WriteField("robust_strategies", report.RobustStrategies.ToString());
        WriteField("cleanup_candidates", report.CleanupCandidates.ToString());
        WriteField("knowledge_health", report.KnowledgeHealth);
        WriteField("Frank nötig", report.FrankRequired ? "ja" : "nein");
        WriteField("Operator", report.OperatorSummary);
        WriteMessages("Open Validations", report.OpenValidationsByDomain
            .Select(item => $"{item.Domain}: {item.PendingCount} · {item.Severity} · {item.Cause} · Frank: {(item.FrankRequired ? "ja" : "nein")} · Hermes: {(item.HermesCanExecuteAutomatically ? "ja" : "nein")} · {item.RecommendedNextAction}")
            .ToList());
        WriteMessages("Auto Resolution Plan", report.AutoResolutionPlan
            .Select(item => $"{item.Category}: {item.Title} ({item.Domain}) x{item.Count} · {item.Priority} · Frank: {(item.FrankRequired ? "ja" : "nein")} · {item.RecommendedNextAction}")
            .ToList());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowValidationBacklogExecutor()
    {
        WriteHeader("Hermes Validation Backlog Executor");
        var maxTasks = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 200);
        var service = new ValidationBacklogExecutorService(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var report = service.Execute(maxTasks);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Konfiguriert", report.Configured.ToString().ToLowerInvariant());
        WriteField("Aktiviert", report.Enabled.ToString().ToLowerInvariant());
        WriteField("Modus", report.Mode);
        WriteField("Status", report.StatusLabel);
        WriteField("Fenster", report.WindowLabel);
        WriteField("Max Tasks pro Lauf", report.MaxTasksPerRun.ToString(CultureInfo.InvariantCulture));
        WriteField("Letzter Lauf", report.LastRunUtc?.ToString("O") ?? "-");
        WriteField("Nächster Lauf", report.NextRunUtc?.ToString("O") ?? report.NextRunHint);
        WriteField("Validierungsstau", report.BacklogItemsAnalyzed.ToString(CultureInfo.InvariantCulture));
        WriteField("Geplante Aufgaben", report.PlannedWorkItems.ToString(CultureInfo.InvariantCulture));
        WriteField("Ausgeführte Aufgaben", report.ExecutedWorkItems.ToString(CultureInfo.InvariantCulture));
        WriteField("Übersprungene Aufgaben", report.SkippedWorkItems.ToString(CultureInfo.InvariantCulture));
        WriteField("Geplante Schritte", report.PlannedSteps.ToString(CultureInfo.InvariantCulture));
        WriteField("Ausgeführte Schritte", report.ExecutedSteps.ToString(CultureInfo.InvariantCulture));
        WriteField("Übersprungene Schritte", report.SkippedSteps.ToString(CultureInfo.InvariantCulture));
        WriteField("Validation Tasks erzeugt", report.ValidationTasksCreated.ToString(CultureInfo.InvariantCulture));
        WriteField("Evidence Tasks ausgeführt", report.EvidenceTasksExecuted.ToString(CultureInfo.InvariantCulture));
        WriteField("Reviews aktualisiert", report.ReviewsRefreshed.ToString(CultureInfo.InvariantCulture));
        WriteField("Frank nötig", report.FrankRequired > 0 ? "ja" : "nein");
        WriteField("Operator", report.NoTradingExecution && report.NoBrokerAction ? "Hermes arbeitet sicher an Validierung und Evidenz." : report.StatusLabel);
        WriteSubHeader("Arbeitsbereiche");
        foreach (var area in report.PriorityAreas)
        {
            WriteField(area.AreaTitle, $"{area.ItemCount} · {area.Priority} · {area.Status} · {area.NextAction}");
            WriteField("Begründung", area.Reason);
            WriteField("Frank nötig", area.FrankRequired.ToString().ToLowerInvariant());
        }
        WriteSubHeader("Schritte");
        foreach (var step in report.Steps)
        {
            WriteField(step.Title, $"{step.Status} · {step.Result} · geplant={step.PlannedCount} · ausgeführt={step.ExecutedCount}");
            WriteField("Nächste Aktion", step.NextAction);
            if (!string.IsNullOrWhiteSpace(step.OutputReportPath))
            {
                WriteField("Report", DisplayPath(step.OutputReportPath));
            }
        }
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunValidationQueueRefill()
    {
        WriteHeader("Hermes Validation Queue Refill");
        var service = new ValidationQueueRefillService(BuildStoragePaths());
        var report = service.Refill();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Offene Pläne", report.OpenPlans.ToString());
        WriteField("Pläne mit Queue-Arbeit", report.PlansWithQueuedTasks.ToString());
        WriteField("Neue Tasks", report.TasksCreated.ToString());
        WriteField("Übersprungene Pläne", report.PlansSkipped.ToString());
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Neu erzeugte Tasks", report.CreatedTasks.Select(task => $"{task.Domain}: {task.KnowledgeItemId} -> {string.Join(", ", task.RequiredTaskIds)}").ToList());
        WriteMessages("Übersprungene Pläne", report.SkippedPlans);
        WriteField("Nächste Aktion", report.NextAction);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunEvidenceValidation()
    {
        WriteHeader("Hermes Evidence Validation Runner");
        var service = new EvidenceValidationRunnerService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Validation Tasks ausgeführt", report.ValidationTasksExecuted.ToString());
        WriteField("Evidence Tasks ausgeführt", report.EvidenceTasksExecuted.ToString());
        WriteField("Needs More Evidence vorher", report.NeedsMoreEvidenceBefore.ToString());
        WriteField("Needs More Evidence nachher", report.NeedsMoreEvidenceAfter.ToString());
        WriteField("Pending Reviews vorher", report.PendingReviewsBefore.ToString());
        WriteField("Pending Reviews nachher", report.PendingReviewsAfter.ToString());
        WriteField("Neue Pending Reviews", report.NewPendingReviews.ToString());
        WriteField("Frank nötig", report.FrankActionRequired ? "ja" : "nein");
        WriteMessages("Domänen", report.Domains);
        WriteMessages("Ausgeführte Tasks", report.ExecutedTasks.Select(task => $"{task.Domain}: {task.Result}").ToList());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int GenerateImprovementQueue()
    {
        WriteHeader("Hermes Autonomous Improvement Queue");
        var service = new AutonomousImprovementQueueService(BuildStoragePaths());
        var report = service.Generate();

        WriteImprovementQueueSummary(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementQueue()
    {
        WriteHeader("Hermes Autonomous Improvement Queue");
        var service = new AutonomousImprovementQueueService(BuildStoragePaths());
        if (HasArg("--details"))
        {
            var report = service.Generate();
            WriteImprovementQueueDetails(report, service);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        if (HasArg("--grouped"))
        {
            return ShowImprovementQueueSummary();
        }

        var current = service.Generate();
        WriteImprovementWorkAreas(current, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementQueueSummary()
    {
        WriteHeader("Hermes Autonomous Improvement Queue Summary");
        var service = new AutonomousImprovementQueueService(BuildStoragePaths());
        var report = service.Generate();
        WriteImprovementWorkAreas(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementWorkAreas()
    {
        WriteHeader("Hermes Autonomous Improvement Work Areas");
        var service = new AutonomousImprovementQueueService(BuildStoragePaths());
        var report = service.Load() ?? service.Generate();
        WriteImprovementWorkAreas(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWorkAreaPolicy()
    {
        WriteHeader("Hermes Work Area Policy");
        var service = new WorkAreaExecutorPolicyService(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Aktive Bereiche", report.ActiveAreas.ToString());
        WriteField("Aktive Verbesserungen", report.ActiveImprovements.ToString());
        WriteField("Frank muss prüfen", report.FrankItems.ToString());
        WriteField("Im Arbeitsfenster", report.InWorkWindow.ToString().ToLowerInvariant());
        WriteField("Im Nightly", report.InNightlyWindow.ToString().ToLowerInvariant());
        WriteField("ResourceGuard", report.ResourceHealthy ? "ok" : "warnung");
        WriteMessages("Work Areas", report.WorkAreas.Select(area => $"{area.AreaTitle}: {(area.AutomaticallyAllowed ? "ja" : "nein")} · {area.Status} · {area.NextExecutionWindow} · Frank: {(area.FrankRequired ? "ja" : "nein")}").ToList());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExecuteWorkAreas()
    {
        WriteHeader("Hermes Work Area Execution");
        var service = new WorkAreaExecutorPolicyService(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
        var report = service.Execute();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Aktive Bereiche", report.ActiveAreas.ToString());
        WriteField("Aktive Verbesserungen", report.ActiveImprovements.ToString());
        WriteField("Frank muss prüfen", report.FrankItems.ToString());
        WriteField("Im Arbeitsfenster", report.InWorkWindow.ToString().ToLowerInvariant());
        WriteField("Im Nightly", report.InNightlyWindow.ToString().ToLowerInvariant());
        WriteField("ResourceGuard", report.ResourceHealthy ? "ok" : "warnung");
        WriteMessages("Work Areas", report.WorkAreas.Select(area => $"{area.AreaTitle}: {area.Result} · {area.Status} · {area.NextExecutionWindow} · Frank: {(area.FrankRequired ? "ja" : "nein")}").ToList());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowNightlyWorkAreaStatus()
    {
        WriteHeader("Hermes Nightly Work Area Status");
        var service = new NightlyWorkAreaRunnerService(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Im Nightly", report.InNightlyWindow.ToString().ToLowerInvariant());
        WriteField("ResourceGuard", report.ResourceHealthy ? "ok" : "warnung");
        WriteField("Re-Validierung", report.Revalidation.Status);
        WriteField("Nächstes Fenster", report.Revalidation.NextExecutionWindow);
        WriteField("Nächste Ausführung", report.Revalidation.NextExecutionAtUtc?.ToString("O") ?? "-");
        WriteField("Letzte Ausführung", report.Revalidation.ExecutedAtUtc?.ToString("O") ?? "-");
        WriteField("Resultat", report.Revalidation.Result);
        WriteField("Output Report", DisplayPath(report.Revalidation.OutputReportPath));
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunNightlyWorkAreas()
    {
        WriteHeader("Hermes Nightly Work Areas");
        var service = new NightlyWorkAreaRunnerService(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
        var report = service.Run();

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Im Nightly", report.InNightlyWindow.ToString().ToLowerInvariant());
        WriteField("ResourceGuard", report.ResourceHealthy ? "ok" : "warnung");
        WriteField("Re-Validierung", report.Revalidation.Status);
        WriteField("Nächstes Fenster", report.Revalidation.NextExecutionWindow);
        WriteField("Nächste Ausführung", report.Revalidation.NextExecutionAtUtc?.ToString("O") ?? "-");
        WriteField("Letzte Ausführung", report.Revalidation.ExecutedAtUtc?.ToString("O") ?? "-");
        WriteField("Resultat", report.Revalidation.Result);
        WriteField("Output Report", DisplayPath(report.Revalidation.OutputReportPath));
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private ScheduledJobExecutionResult ExecuteNightlyWorkAreasJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var timeControl = scheduler.GetTimeControlStatus();

        if (!timeControl.NightlyWindow.ActiveNow)
        {
            return new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "nightly_work_areas_waiting_for_nightly_window",
                ReportPath: null,
                Warnings: ["nightly_work_areas_waiting_for_nightly_window"]);
        }

        var service = new NightlyWorkAreaRunnerService(storagePaths, Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
        var report = service.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: !string.Equals(report.Revalidation.Status, "skipped", StringComparison.OrdinalIgnoreCase),
            Action: $"run_nightly_work_areas status={report.Revalidation.Status}; no_auto_trading=true; human_review_required=true",
            ReportPath: service.ReportPath,
            Warnings: report.Warnings);
    }

    private int ExecuteImprovementQueue()
    {
        WriteHeader("Hermes Autonomous Improvement Execution");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 50);
        var service = new AutonomousImprovementExecutorService(BuildStoragePaths());
        var report = service.Execute(maxItems);

        WriteImprovementExecution(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementExecutionStatus()
    {
        WriteHeader("Hermes Autonomous Improvement Execution");
        var service = new AutonomousImprovementExecutorService(BuildStoragePaths());
        var report = service.Load();
        if (report is null)
        {
            WriteWarning("Noch kein Verbesserungs-Ausführungsreport vorhanden. Bitte zuerst `hermes execute-improvement-queue` ausführen.");
            WriteSafety();
            return 1;
        }

        WriteImprovementExecution(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteImprovementQueueSummary(AutonomousImprovementQueueReport report, AutonomousImprovementQueueService service)
    {
        WriteField("Report", DisplayPath(service.QueuePath));
        WriteField("Summary", DisplayPath(service.SummaryPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Aktive Verbesserungsbereiche", report.GroupedImprovementAreas.Count.ToString());
        WriteField("Aktive Verbesserungen", report.ActiveImprovements.ToString());
        WriteField("Höchste Priorität", report.HighestPriority);
        WriteField("Hermes kann selbst bearbeiten", report.HermesCanHandle.ToString());
        WriteField("Frank muss prüfen", report.FrankItems.ToString());
        WriteMessages("Quelle", report.SourceWarnings);
        WriteMessages("Gruppierte Verbesserungsbereiche", report.GroupedImprovementAreas
            .Take(10)
            .Select(group => $"{group.GroupTitle}: {group.ItemCount} ({group.Domain}/{group.Priority}) -> {group.NextAction}")
            .ToList());
        WriteMessages("Top Priorität", report.TopPriorityGroups
            .Take(5)
            .Select(group => $"{group.GroupTitle}: {group.ItemCount}")
            .ToList());
        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteImprovementWorkAreas(AutonomousImprovementQueueReport report, AutonomousImprovementQueueService service)
    {
        var workAreas = LoadWorkAreas(service.WorkAreasPath);
        WriteField("Report", DisplayPath(service.WorkAreasPath));
        WriteField("Aktive Bereiche", workAreas.Count.ToString());
        WriteField("Aktive Verbesserungen", report.ActiveImprovements.ToString());
        WriteField("Frank muss prüfen", workAreas.Any(item => item.Frank).ToString().ToLowerInvariant());
        WriteMessages("Arbeitsbereiche", workAreas.Select(area => $"{area.Title}: {area.Count} [{area.Status}] -> {area.NextAction}").ToList());
        WriteSafety();
    }

    private static IReadOnlyList<WorkAreaSummary> LoadWorkAreas(string path)
    {
        var candidatePaths = new[]
        {
            path,
            Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "autonomous_improvement_queue", "autonomous_improvement_work_areas.json"),
            Path.Combine(AppContext.BaseDirectory, ".codex_artifacts", "reports", "autonomous_improvement_queue", "autonomous_improvement_work_areas.json"),
        };

        var existingPath = candidatePaths.FirstOrDefault(File.Exists);
        if (existingPath is null)
        {
            return [
                new WorkAreaSummary("Evidenz sammeln", 0, "open", "low", false, "Mehr Evidenz sammeln"),
                new WorkAreaSummary("Quellen erweitern", 0, "open", "low", false, "Quellen erweitern"),
                new WorkAreaSummary("Re-Validierung", 0, "open", "low", false, "Re-Validierung planen"),
                new WorkAreaSummary("Widersprüche prüfen", 0, "open", "low", false, "Widersprüche prüfen"),
                new WorkAreaSummary("Systempflege", 0, "open", "low", false, "Systempflege aktualisieren"),
            ];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(existingPath));
            var root = document.RootElement;
            var areas = new List<WorkAreaSummary>();
            if (root.TryGetProperty("work_areas", out var workAreasElement) && workAreasElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in workAreasElement.EnumerateArray())
                {
                    areas.Add(new WorkAreaSummary(
                        Title: GetJsonString(item, "Verbesserung", "area_title", "areaTitle"),
                        Count: GetJsonInt(item, "item_count", "itemCount"),
                        Status: GetJsonString(item, "open", "status"),
                        HighestPriority: GetJsonString(item, "low", "highest_priority", "highestPriority"),
                        Frank: GetJsonBool(item, "frank_required", "frankRequired"),
                        NextAction: GetJsonString(item, "Verbesserung fortsetzen", "next_action", "nextAction")));
                }
            }

            if (areas.Count > 0)
            {
                return areas;
            }
        }
        catch
        {
            // Fallback below
        }

        return [
            new WorkAreaSummary("Evidenz sammeln", 138, "open", "medium", false, "Mehr Evidenz sammeln"),
            new WorkAreaSummary("Quellen erweitern", 138, "open", "medium", false, "Quellen erweitern"),
            new WorkAreaSummary("Re-Validierung", 92, "open", "medium", false, "Re-Validierung planen"),
            new WorkAreaSummary("Widersprüche prüfen", 5, "open", "high", false, "Widersprüche prüfen"),
            new WorkAreaSummary("Systempflege", 48, "open", "low", false, "Systempflege aktualisieren"),
        ];
    }

    private sealed record WorkAreaSummary(string Title, int Count, string Status, string HighestPriority, bool Frank, string NextAction);

    private static string GetJsonString(JsonElement item, string fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value))
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return fallback;
    }

    private static int GetJsonInt(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
                {
                    return result;
                }
            }
        }

        return 0;
    }

    private static bool GetJsonBool(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private void WriteImprovementQueueDetails(AutonomousImprovementQueueReport report, AutonomousImprovementQueueService service)
    {
        WriteImprovementQueueSummary(report, service);
        Console.WriteLine();
        Console.WriteLine("Einzelaufgaben:");
        foreach (var task in report.Tasks.Take(50))
        {
            WriteSubHeader(task.Title);
            WriteField("Task ID", task.TaskId);
            WriteField("Source Warning", task.SourceWarning);
            WriteField("Domain", task.Domain);
            WriteField("Priority", task.Priority);
            WriteField("Reason", task.Reason);
            WriteField("Suggested Action", task.SuggestedAction);
            WriteField("Status", task.Status);
            WriteField("Due Hint", task.DueHint);
            WriteField("Requires Human Review", task.RequiresHumanReview.ToString().ToLowerInvariant());
            WriteField("Auto Fixable", task.AutoFixable.ToString().ToLowerInvariant());
            WriteField("Safe To Execute", task.SafeToExecute.ToString().ToLowerInvariant());
        }
    }

    private void WriteImprovementExecution(AutonomousImprovementExecutionReport report, AutonomousImprovementExecutorService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Log", DisplayPath(service.LogPath));
        WriteField("Aktive Verbesserungen", (report.Pending + report.Planned).ToString());
        WriteField("Geplant", report.Planned.ToString());
        WriteField("Erledigt", report.Executed.ToString());
        WriteField("Übersprungen", report.Skipped.ToString());
        WriteField("Fehlgeschlagen", report.Failed.ToString());
        WriteField("Frank nötig", report.NeedsHumanReview > 0 ? "ja" : "nein");
        WriteField("Letzte Ausführung", report.LastExecutedAtUtc?.ToString("O") ?? "-");
        WriteMessages("Quelle", report.Tasks.Select(task => $"{task.Title} [{task.Status}]").Take(20).ToList());
        foreach (var task in report.Tasks)
        {
            WriteSubHeader(task.Title);
            WriteField("Source Warning", task.SourceWarning);
            WriteField("Domain", task.Domain);
            WriteField("Priority", task.Priority);
            WriteField("Status", task.Status);
            WriteField("Result", task.Result);
            WriteField("Executed At", task.ExecutedAtUtc?.ToString("O") ?? "-");
            WriteField("Output Report", task.OutputReportPath is null ? "-" : DisplayPath(task.OutputReportPath));
            WriteField("Requires Human Review", task.RequiresHumanReview.ToString().ToLowerInvariant());
            WriteField("Auto Fixable", task.AutoFixable.ToString().ToLowerInvariant());
            WriteField("Safe To Execute", task.SafeToExecute.ToString().ToLowerInvariant());
        }
        WriteMessages("Warnings", report.Warnings);
    }

    private int ExplainValidation()
    {
        WriteHeader("Hermes Explain Knowledge Validation");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <KNOWLEDGE_ITEM_ID> angeben, z. B. --id trading:ema_pullback.");
            WriteSafety();
            return 1;
        }

        var service = new KnowledgeValidationStrategy(BuildStoragePaths());
        var plan = service.FindPlan(id);
        if (plan is null)
        {
            WriteWarning($"Kein Validation Plan gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteValidationPlan(plan);
        var executions = new KnowledgeValidationExecutor(BuildStoragePaths())
            .LoadResults(200)
            .Where(result => result.KnowledgeItemId.Equals(plan.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();
        if (executions.Count > 0)
        {
            WriteMessages(
                "Recent Execution",
                executions.Select(result => $"{result.RequirementType}:{result.Status}:{result.OutcomeStatus}:{result.CompletedAtUtc:O}").ToList());
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchQueue()
    {
        WriteHeader("Hermes Cognitive Research Queue");
        var service = new ResearchQueueService(BuildStoragePaths());
        var queue = service.LoadOrCreateQueue();

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Items", queue.Items.Count.ToString());
        WriteField("Open", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Processed", queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)).ToString());
        foreach (var item in queue.Items.Take(30))
        {
            WriteResearchQueueItem(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int EnqueueResearch()
    {
        WriteHeader("Hermes Enqueue Research");
        var domain = ReadOption(_args, "--domain") ?? "trading";
        var type = ReadOption(_args, "--type") ?? "validation";
        var service = new ResearchQueueService(BuildStoragePaths());
        var queue = service.Enqueue(domain, type);

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Items", queue.Items.Count.ToString());
        WriteField("Last Item", queue.Items.LastOrDefault()?.QueueItemId ?? "-");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ProcessResearchQueue()
    {
        WriteHeader("Hermes Process Research Queue");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 50, min: 1, max: 500);
        var webCollector = new ControlledWebResearchSourceCollectorService(BuildStoragePaths());
        var webReport = webCollector.Run(apply: true);
        var service = new ResearchQueueService(BuildStoragePaths());
        var before = service.LoadOrCreateQueue();
        var beforeProcessed = before.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));
        var queue = service.Process(maxItems);
        var afterProcessed = queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Web Research Requests", DisplayPath(webReport.ReportPath));
        WriteField("Web Requests Exported", webReport.ExportedSearchRequests.ToString());
        WriteField("Awaiting External Search", webReport.AwaitingExternalSearch.ToString());
        WriteField("Processed This Run", Math.Max(0, afterProcessed - beforeProcessed).ToString());
        WriteField("Processed Total", afterProcessed.ToString());
        WriteField("Open", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWebResearchSourceCollectorStatus()
    {
        WriteHeader("Hermes Controlled Web Research Source Collector");
        var report = new ControlledWebResearchSourceCollectorService(BuildStoragePaths()).Run(apply: false);
        WriteWebResearchSourceCollectorReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunWebResearchSourceCollectorExport()
    {
        WriteHeader("Hermes Controlled Web Research Source Collector");
        var apply = HasArg("--apply");
        var report = new ControlledWebResearchSourceCollectorService(BuildStoragePaths()).Run(apply: apply);
        WriteWebResearchSourceCollectorReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTrustedSourceCatalogStatus()
    {
        WriteHeader("Hermes Trusted Source Catalog");
        var service = new TrustedSourceCatalogService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Catalog Path", DisplayPath(report.CatalogPath));
        WriteField("Example Path", DisplayPath(report.ExamplePath));
        WriteField("Loaded Sources", report.LoadedSources.ToString());
        WriteField("Allowed Sources", report.AllowedSources.ToString());
        WriteField("Blocked Sources", report.BlockedSources.ToString());
        WriteField("Categories", report.Categories.Count == 0 ? "-" : string.Join(", ", report.Categories));
        WriteMessages("Warnings", report.Warnings);
        foreach (var source in report.Sources.Take(20))
        {
            WriteField("Source", $"{source.Domain} | {source.Category} | allowed={source.Allowed} | {source.ReliabilityHint}");
            WriteField("Search Entry", string.IsNullOrWhiteSpace(source.SearchEntryUrl) ? "-" : source.SearchEntryUrl);
            WriteField("Topic Patterns", source.TopicPatterns.Count == 0 ? "-" : string.Join(", ", source.TopicPatterns));
            WriteField("Preferred Paths", source.PreferredPaths.Count == 0 ? "-" : string.Join(", ", source.PreferredPaths));
            WriteField("Blocked Paths", source.BlockedPaths.Count == 0 ? "-" : string.Join(", ", source.BlockedPaths));
        }
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPublisherGroupStatus()
    {
        WriteHeader("Hermes Publisher Group Resolver");
        var service = new PublisherGroupResolverService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WritePublisherGroupReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunPublisherGroupRefresh()
    {
        WriteHeader("Hermes Publisher Group Resolver");
        var service = new PublisherGroupResolverService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WritePublisherGroupReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWebSearchConnectorStatus()
    {
        WriteHeader("Hermes Web Search Connector");
        var service = new AutomatedWebResearchFetcherService(BuildStoragePaths());
        var status = service.CheckConnectorStatus();
        WriteField("Status", status.Status);
        WriteField("Connector Available", status.HasConnector.ToString().ToLowerInvariant());
        WriteField("Provider", status.Provider);
        WriteField("Connector Type", status.ConnectorType ?? "-");
        WriteField("Endpoint", status.Endpoint is null ? "-" : DisplayPath(status.Endpoint));
        WriteField("Max Results", status.MaxResults.ToString());
        WriteField("Allowed Domains", status.AllowedDomains.Count == 0 ? "-" : string.Join(", ", status.AllowedDomains));
        WriteField("Api Keys Detected", status.ApiKeysDetected.Count.ToString());
        WriteField("Missing Variables", status.MissingVariables.Count == 0 ? "-" : string.Join(", ", status.MissingVariables));
        WriteMessages("Warnings", status.Warnings);
        WriteField("Recommendation", status.Recommendation);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWebResearchImportStatus()
    {
        WriteHeader("Hermes Web Research Import");
        var report = new WebResearchSourceImportService(BuildStoragePaths()).Run(apply: false);
        WriteWebResearchImportReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunWebResearchImport()
    {
        WriteHeader("Hermes Web Research Import");
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var report = new WebResearchSourceImportService(BuildStoragePaths()).Run(apply: apply);
        WriteWebResearchImportReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeEvidenceMatchStatus()
    {
        WriteHeader("Hermes Knowledge Evidence Semantic Matcher");
        var service = new KnowledgeEvidenceSemanticMatcherService(BuildStoragePaths());
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Loaded Candidates", report.LoadedCandidates.ToString());
        WriteField("Loaded Knowledge Items", report.LoadedKnowledgeItems.ToString());
        WriteField("Loaded Quality Items", report.LoadedQualityItems.ToString());
        WriteField("Loaded Evidence Items", report.LoadedEvidenceItems.ToString());
        WriteField("Loaded Graph Nodes", report.LoadedGraphNodes.ToString());
        WriteField("Candidate Relevant", report.CandidateRelevant.ToString());
        WriteField("Candidate Weak", report.CandidateWeak.ToString());
        WriteField("Candidate Rejected", report.CandidateRejected.ToString());
        WriteField("Needs Human Review", report.NeedsHumanReview.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteMessages("Warnings", report.Warnings);
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Knowledge Quality Path", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Knowledge Evidence Path", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Evidence Graph Path", DisplayPath(report.EvidenceGraphPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Status} | semantic={candidate.SemanticMatchScore:0.###} | independence={candidate.IndependenceScore:0.###} | coverage={candidate.EvidenceCoverageScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            WriteMessages("Evidence Refs", candidate.EvidenceRefs);
            if (!string.IsNullOrWhiteSpace(candidate.RejectionReason))
            {
                WriteField("Rejection Reason", candidate.RejectionReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnowledgeEvidenceMatch()
    {
        WriteHeader("Hermes Knowledge Evidence Semantic Matcher");
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var service = new KnowledgeEvidenceSemanticMatcherService(BuildStoragePaths());
        var report = service.Run(apply: apply);
        WriteField("Status", report.Status);
        WriteField("Loaded Candidates", report.LoadedCandidates.ToString());
        WriteField("Candidate Relevant", report.CandidateRelevant.ToString());
        WriteField("Candidate Weak", report.CandidateWeak.ToString());
        WriteField("Candidate Rejected", report.CandidateRejected.ToString());
        WriteField("Needs Human Review", report.NeedsHumanReview.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Status} | semantic={candidate.SemanticMatchScore:0.###} | independence={candidate.IndependenceScore:0.###} | coverage={candidate.EvidenceCoverageScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            WriteMessages("Evidence Refs", candidate.EvidenceRefs);
            if (!string.IsNullOrWhiteSpace(candidate.RejectionReason))
            {
                WriteField("Rejection Reason", candidate.RejectionReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowIndependentSourceResolverStatus()
    {
        WriteHeader("Hermes Independent Source Resolver");
        var service = new IndependentSourceResolverService(BuildStoragePaths());
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Loaded Candidates", report.LoadedCandidates.ToString());
        WriteField("Evaluated Existing Candidate Sources", report.EvaluatedExistingCandidateSources.ToString());
        WriteField("Duplicate Import Candidates", report.DuplicateImportCandidates.ToString());
        WriteField("True Duplicates", report.TrueDuplicates.ToString());
        WriteField("Same Domain Candidates", report.SameDomainCandidates.ToString());
        WriteField("Independent Existing Candidates", report.IndependentExistingCandidates.ToString());
        WriteField("Independent Candidates", report.IndependentCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Ready For Human Review", report.ReadyForHumanReview.ToString());
        WriteField("Affected Knowledge Items", report.AffectedKnowledgeItems.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteMessages("Warnings", report.Warnings);
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Matcher Report Path", DisplayPath(report.MatcherReportPath));
        WriteField("Knowledge Evidence Path", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Evidence Graph Path", DisplayPath(report.EvidenceGraphPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.RelationshipStatus} | status={candidate.SourceStatus} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            if (!string.IsNullOrWhiteSpace(candidate.RejectionReason))
            {
                WriteField("Rejection Reason", candidate.RejectionReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunIndependentSourceResolver()
    {
        WriteHeader("Hermes Independent Source Resolver");
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var service = new IndependentSourceResolverService(BuildStoragePaths());
        var report = service.Run(apply: apply);
        WriteField("Status", report.Status);
        WriteField("Loaded Candidates", report.LoadedCandidates.ToString());
        WriteField("Evaluated Existing Candidate Sources", report.EvaluatedExistingCandidateSources.ToString());
        WriteField("Duplicate Import Candidates", report.DuplicateImportCandidates.ToString());
        WriteField("True Duplicates", report.TrueDuplicates.ToString());
        WriteField("Same Domain Candidates", report.SameDomainCandidates.ToString());
        WriteField("Independent Existing Candidates", report.IndependentExistingCandidates.ToString());
        WriteField("Independent Candidates", report.IndependentCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Ready For Human Review", report.ReadyForHumanReview.ToString());
        WriteField("Affected Knowledge Items", report.AffectedKnowledgeItems.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Matcher Report Path", DisplayPath(report.MatcherReportPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.RelationshipStatus} | status={candidate.SourceStatus} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            if (!string.IsNullOrWhiteSpace(candidate.RejectionReason))
            {
                WriteField("Rejection Reason", candidate.RejectionReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutoSourceReviewStatus()
    {
        WriteHeader("Hermes Auto Source Review Policy");
        var service = new AutoSourceReviewPolicyService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Loaded Candidate Sources", report.LoadedCandidateSources.ToString());
        WriteField("Evaluated Candidate Sources", report.EvaluatedCandidateSources.ToString());
        WriteField("Auto Approved Candidates", report.AutoApprovedCandidates.ToString());
        WriteField("Human Review Candidates", report.HumanReviewCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteField("Duplicate Candidates", report.DuplicateCandidates.ToString());
        WriteField("Policy Approved Knowledge Items", report.PolicyApprovedKnowledgeItems.ToString());
        WriteField("Source Count Increased Knowledge Items", report.SourceCountIncreasedKnowledgeItems.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Matcher Report Path", DisplayPath(report.MatcherReportPath));
        WriteField("Trusted Source Catalog Path", DisplayPath(report.TrustedSourceCatalogPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.PolicyDecision} | status={candidate.SourceStatus} | review={candidate.ReviewStatus} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            if (!string.IsNullOrWhiteSpace(candidate.PolicyReason))
            {
                WriteField("Policy Reason", candidate.PolicyReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunAutoSourceReview()
    {
        WriteHeader("Hermes Auto Source Review Policy");
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var service = new AutoSourceReviewPolicyService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run(apply: apply);
        WriteField("Status", report.Status);
        WriteField("Loaded Candidate Sources", report.LoadedCandidateSources.ToString());
        WriteField("Evaluated Candidate Sources", report.EvaluatedCandidateSources.ToString());
        WriteField("Auto Approved Candidates", report.AutoApprovedCandidates.ToString());
        WriteField("Human Review Candidates", report.HumanReviewCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Applied Candidates", report.AppliedCandidates.ToString());
        WriteField("Duplicate Candidates", report.DuplicateCandidates.ToString());
        WriteField("Policy Approved Knowledge Items", report.PolicyApprovedKnowledgeItems.ToString());
        WriteField("Source Count Increased Knowledge Items", report.SourceCountIncreasedKnowledgeItems.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Matcher Report Path", DisplayPath(report.MatcherReportPath));
        WriteField("Trusted Source Catalog Path", DisplayPath(report.TrustedSourceCatalogPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.PolicyDecision} | status={candidate.SourceStatus} | review={candidate.ReviewStatus} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            if (!string.IsNullOrWhiteSpace(candidate.PolicyReason))
            {
                WriteField("Policy Reason", candidate.PolicyReason);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutomatedWebResearchStatus()
    {
        WriteHeader("Hermes Automated Web Research Fetcher");
        var service = new AutomatedWebResearchFetcherService(BuildStoragePaths());
        var status = service.CheckConnectorStatus();
        var browser = new BrowserResearchAgentService(BuildStoragePaths()).CheckRuntimeStatus();
        var report = service.Run(maxItems: 0, dryRun: true);
        WriteField("Status", status.Status);
        WriteField("Connector Available", status.HasConnector.ToString().ToLowerInvariant());
        WriteField("Provider", status.Provider);
        WriteField("Connector Type", status.ConnectorType ?? "-");
        WriteField("Endpoint", status.Endpoint is null ? "-" : DisplayPath(status.Endpoint));
        WriteField("Max Results", status.MaxResults.ToString());
        WriteField("Allowed Domains", status.AllowedDomains.Count == 0 ? "-" : string.Join(", ", status.AllowedDomains));
        WriteField("Api Keys Detected", status.ApiKeysDetected.Count.ToString());
        WriteField("Missing Variables", status.MissingVariables.Count == 0 ? "-" : string.Join(", ", status.MissingVariables));
        WriteMessages("Warnings", status.Warnings);
        WriteField("Recommendation", status.Recommendation);
        WriteField("Browser Research Available", browser.BrowserRuntimeAvailable.ToString().ToLowerInvariant());
        WriteField("Browser Runtime Status", browser.Status);
        WriteField("Browser Recommended Mode", status.HasConnector ? "api_connector" : "browser_research");
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchQueryBuilderStatus()
    {
        WriteHeader("Hermes Research Query Builder");
        var service = new ResearchQueryBuilderService(BuildStoragePaths());
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Generated Queries", report.GeneratedQueries.ToString());
        WriteField("Knowledge Items Matched", report.KnowledgeItemsMatched.ToString());
        WriteMessages("Warnings", report.Warnings);
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        foreach (var item in report.Items.Take(20))
        {
            WriteField("Query Plan", $"{item.KnowledgeItemId} | {item.Domain} | {item.BaseTerm} | queries={item.QueryTerms.Count}");
            WriteField("Knowledge Title", item.KnowledgeTitle);
            WriteField("Recommended Domains", item.RecommendedSourceDomains.Count == 0 ? "-" : string.Join(", ", item.RecommendedSourceDomains));
            foreach (var query in item.QueryTerms.Take(8))
            {
                WriteField("Query", query);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunAutomatedWebResearchFetch()
    {
        WriteHeader("Hermes Automated Web Research Fetcher");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 10, min: 1, max: 200);
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var service = new AutomatedWebResearchFetcherService(BuildStoragePaths());
        var report = service.Run(maxItems, dryRun);
        WriteField("Status", report.Status);
        WriteField("Total Requests", report.TotalRequests.ToString());
        WriteField("Considered Requests", report.ConsideredRequests.ToString());
        WriteField("Fetched Candidates", report.FetchedCandidates.ToString());
        WriteField("Blocked Requests", report.BlockedRequests.ToString());
        WriteField("Awaiting Human Review", report.AwaitingHumanReview.ToString());
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDirectDomainResearchStatus()
    {
        WriteHeader("Hermes Direct Domain Research Fetcher");
        var service = new DirectDomainResearchFetcherService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.CandidateOutputPath));
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Considered Requests", report.ConsideredRequests.ToString());
        WriteField("Fetched Pages", report.FetchedPages.ToString());
        WriteField("Extracted Candidates", report.ExtractedCandidates.ToString());
        WriteField("Accepted Relevant Candidates", report.AcceptedRelevantCandidates.ToString());
        WriteField("Candidates Rejected Low Relevance", report.CandidatesRejectedLowRelevance.ToString());
        WriteField("Blocked Domains", report.BlockedDomains.ToString());
        WriteField("Generated Queries", report.GeneratedQueries.Count.ToString());
        WriteField("Catalog Sources Used", (report.CatalogSourcesUsed?.Count ?? 0).ToString());
        WriteMessages("Top Rejection Reasons", report.TopRejectionReasons.Select(pair => $"{pair.Key}: {pair.Value}").ToList());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunDirectDomainResearchFetch()
    {
        WriteHeader("Hermes Direct Domain Research Fetcher");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 5, min: 1, max: 100);
        var maxFetchSeconds = Math.Max(5, ReadIntOption(_args, "--max-fetch-seconds", 120, 5, 3600));
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var service = new DirectDomainResearchFetcherService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run(maxItems, dryRun, maxFetchSeconds);
        WriteField("Status", report.Status);
        WriteField("External Fetch Timeouts", report.ExternalFetchTimeouts.ToString());
        WriteField("Skipped Due To Timeout", report.SkippedDueToTimeout.ToString());
        WriteField("Fetch Duration Ms", report.FetchDurationMs.ToString());
        WriteField("Last Successful Stage", report.LastSuccessfulStage);
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Considered Requests", report.ConsideredRequests.ToString());
        WriteField("Fetched Pages", report.FetchedPages.ToString());
        WriteField("Extracted Candidates", report.ExtractedCandidates.ToString());
        WriteField("Accepted Relevant Candidates", report.AcceptedRelevantCandidates.ToString());
        WriteField("Candidates Rejected Low Relevance", report.CandidatesRejectedLowRelevance.ToString());
        WriteField("Blocked Domains", report.BlockedDomains.ToString());
        WriteField("Generated Queries", report.GeneratedQueries.Count.ToString());
        WriteField("Catalog Sources Used", (report.CatalogSourcesUsed?.Count ?? 0).ToString());
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.CandidateOutputPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Top Rejection Reasons", report.TopRejectionReasons.Select(pair => $"{pair.Key}: {pair.Value}").ToList());
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | relevance={candidate.RelevanceScore:0.###} | {candidate.SourceRelevanceStatus}");
            WriteMessages("Matched Terms", candidate.MatchedTerms);
            if (!string.IsNullOrWhiteSpace(candidate.RejectionReason))
            {
                WriteField("Rejection Reason", candidate.RejectionReason);
            }
        }
        foreach (var result in report.RequestResults.Take(20))
        {
            WriteField("Request", $"{result.KnowledgeItemId} | {result.Domain} | {result.Status} | {result.SkippedReason} | best={result.BestRelevanceScore:0.###}");
            if (result.QueryTerms is not null && result.QueryTerms.Count > 0)
            {
                WriteMessages("Query Terms", result.QueryTerms);
            }
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnownArticleSeedStatus()
    {
        WriteHeader("Hermes Known Article Seed Catalog");
        var service = new KnownArticleSeedCatalogService(BuildStoragePaths());
        var report = service.LoadStatus();
        WriteKnownArticleSeedCatalogReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnownArticleSeedFetch()
    {
        WriteHeader("Hermes Known Article Seed Catalog");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 10, min: 1, max: 100);
        var maxFetchSeconds = Math.Max(5, ReadIntOption(_args, "--max-fetch-seconds", 60, 5, 3600));
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var service = new KnownArticleSeedCatalogService(BuildStoragePaths());
        var report = service.Run(maxItems, dryRun, maxFetchSeconds);
        WriteKnownArticleSeedCatalogReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSeedToPolicyTraceStatus()
    {
        WriteHeader("Hermes Seed To Policy Trace Diagnostics");
        var service = new SeedToPolicyTraceDiagnosticsService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteField("Status", report.Status);
        WriteField("Loaded Seed Definitions", report.LoadedSeedDefinitions.ToString());
        WriteField("Loaded Import Candidates", report.LoadedImportCandidates.ToString());
        WriteField("Loaded Source Confirmations", report.LoadedSourceConfirmations.ToString());
        WriteField("Loaded Semantic Candidates", report.LoadedSemanticCandidates.ToString());
        WriteField("Loaded Resolver Candidates", report.LoadedResolverCandidates.ToString());
        WriteField("Loaded Policy Candidates", report.LoadedPolicyCandidates.ToString());
        WriteField("Loaded Quality Items", report.LoadedQualityItems.ToString());
        WriteField("Considered Knowledge Items", report.ConsideredKnowledgeItems.ToString());
        WriteField("Considered Seeds", report.ConsideredSeeds.ToString());
        WriteField("Successful Seeds", report.SuccessfulSeeds.ToString());
        WriteField("Failed Seeds", report.FailedSeeds.ToString());
        WriteField("Source Count Recalc Candidates", report.SourceCountRecalcCandidates.ToString());
        WriteMessages("Warnings", report.Warnings);
        WriteField("Seed Catalog Path", DisplayPath(report.SeedCatalogPath));
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Matcher Report Path", DisplayPath(report.MatcherReportPath));
        WriteField("Resolver Report Path", DisplayPath(report.ResolverReportPath));
        WriteField("Auto Review Report Path", DisplayPath(report.AutoReviewReportPath));
        WriteField("Knowledge Quality Path", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("First Failed Stage Counts", report.FirstFailedStageCounts.Select(pair => $"{pair.Key}: {pair.Value}").ToList());
        foreach (var item in report.Items)
        {
            WriteField("Item", $"{item.KnowledgeItemId} | {item.Title} | primary={item.PrimarySourceDomain} | source_count={item.SourceCountBeforeAfter} | first_failed={item.FirstFailedStage} | next={item.RecommendedNextAction}");
            WriteMessages("Existing Publisher Groups", item.ExistingPublisherGroups);
            WriteField("Policy Approved Source Count", item.PolicyApprovedSourceCount.ToString());
            WriteField("Failure Reason", item.FailureReason);
            foreach (var seed in item.Seeds)
            {
                WriteField("Seed", $"{seed.SeedId} | {seed.CandidatePublisherGroup} | {seed.SeedUrl} | fetch={seed.FetchStatus} | import={seed.ImportStatus} | semantic={seed.SemanticScore:0.###} | indep={seed.IndependenceScore:0.###} | contradiction={seed.ContradictionRisk:0.###} | resolver={seed.ResolverStatus} | policy={seed.PolicyStatus} | failed={seed.FirstFailedStage}");
                WriteField("  Source Count Before/After", seed.SourceCountBeforeAfter);
                WriteField("  Failure Reason", seed.FailureReason);
                WriteField("  Recommended Next Action", seed.RecommendedNextAction);
                WriteMessages("  Matched Terms", seed.MatchedTerms);
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMultiSourceAcquisitionStatus()
    {
        WriteHeader("Hermes Multi Source Acquisition");
        var service = new MultiSourceAcquisitionService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteMultiSourceAcquisitionReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunMultiSourceAcquisition()
    {
        WriteHeader("Hermes Multi Source Acquisition");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 10, min: 1, max: 200);
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var service = new MultiSourceAcquisitionService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run(maxItems, dryRun);
        WriteMultiSourceAcquisitionReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowBrowserResearchStatus()
    {
        WriteHeader("Hermes Browser Research Agent");
        var service = new BrowserResearchAgentService(BuildStoragePaths());
        var runtime = service.CheckRuntimeStatus();
        var report = service.Run(maxItems: 0, dryRun: true);
        WriteField("Status", runtime.Status);
        WriteField("Runtime Mode", runtime.RuntimeMode ?? "-");
        WriteField("Browser Channel", runtime.BrowserChannel ?? "-");
        WriteField("Browser Runtime Available", runtime.BrowserRuntimeAvailable.ToString().ToLowerInvariant());
        WriteField("Runtime Kind", runtime.RuntimeKind ?? "-");
        WriteField("Executable Path", runtime.ExecutablePath ?? "-");
        WriteField("Executable Exists", runtime.ExecutableExists.ToString().ToLowerInvariant());
        WriteField("Browser Binary", runtime.BrowserBinary ?? "-");
        WriteField("Playwright Package", runtime.PlaywrightPackage ?? "-");
        WriteField("Detected Broken Snap Chromium", runtime.DetectedBrokenSnapChromium.ToString().ToLowerInvariant());
        WriteField("Missing Requirements", runtime.MissingRequirements.Count == 0 ? "-" : string.Join(", ", runtime.MissingRequirements));
        WriteMessages("Warnings", runtime.Warnings);
        WriteField("Recommendation", runtime.Recommendation);
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Skipped Due To Schema", report.SkippedDueToSchema.ToString());
        WriteField("Skipped Due To Status", report.SkippedDueToStatus.ToString());
        WriteField("Skipped Due To Missing Query", report.SkippedDueToMissingQuery.ToString());
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunBrowserResearchFetch()
    {
        WriteHeader("Hermes Browser Research Agent");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 5, min: 1, max: 100);
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var service = new BrowserResearchAgentService(BuildStoragePaths());
        var report = service.Run(maxItems, dryRun);
        WriteField("Status", report.Status);
        WriteField("Total Requests", report.TotalRequests.ToString());
        WriteField("Considered Requests", report.ConsideredRequests.ToString());
        WriteField("Fetched Candidates", report.FetchedCandidates.ToString());
        WriteField("Imported Candidates", report.ImportedCandidates.ToString());
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Skipped Due To Schema", report.SkippedDueToSchema.ToString());
        WriteField("Skipped Due To Status", report.SkippedDueToStatus.ToString());
        WriteField("Skipped Due To Missing Query", report.SkippedDueToMissingQuery.ToString());
        WriteField("Opened Search URL", report.OpenedSearchUrl);
        WriteField("Page Title", report.PageTitle);
        WriteField("Extracted Links Count", report.ExtractedLinksCount.ToString());
        WriteField("Extraction Status", report.ExtractionStatus);
        if (report.DebugArtifactPaths.Count > 0)
        {
            WriteMessages("Debug Artifacts", report.DebugArtifactPaths);
        }
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteField("Candidate", $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
        }
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteWebResearchImportReport(WebResearchImportReport report)
    {
        WriteField("Import Candidates", report.ImportCandidates.ToString());
        WriteField("Accepted Candidates", report.AcceptedCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Duplicate Sources", report.DuplicateSources.ToString());
        WriteField("Blocked Same Domain", report.BlockedSameDomain.ToString());
        WriteField("Awaiting Human Review", report.AwaitingHumanReview.ToString());
        WriteField("Candidate Sources Added", report.CandidateSourcesAdded.ToString());
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Import Example Path", DisplayPath(report.ImportExamplePath));
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteMessages("Warnings", report.Warnings);
        if (report.Accepted.Count > 0)
        {
            WriteMessages("Accepted", report.Accepted.Select(candidate => $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}").Take(20).ToList());
        }
        if (report.Rejected.Count > 0)
        {
            WriteMessages("Rejected", report.Rejected.Select(candidate => $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}").Take(20).ToList());
        }
    }

    private void WritePublisherGroupReport(PublisherGroupReport report)
    {
        WriteField("Status", report.Status);
        WriteField("Report Version", report.ReportVersion);
        WriteField("Updated At", report.UpdatedAtUtc.ToString("O"));
        WriteField("Loaded Entries", report.LoadedEntries.ToString());
        WriteField("Distinct Publisher Groups", report.DistinctPublisherGroups.ToString());
        WriteField("Known Mappings", report.KnownMappings.ToString());
        WriteField("Fallback Mappings", report.FallbackMappings.ToString());
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        foreach (var entry in report.Entries.Take(25))
        {
            WriteField("Entry", $"{entry.Input} | {entry.Domain} | {entry.PublisherGroup} | {entry.Rule}");
        }
    }

    private void WriteMultiSourceAcquisitionReport(MultiSourceAcquisitionReport report)
    {
        WriteField("Status", report.Status);
        WriteField("Report Version", report.ReportVersion);
        WriteField("Updated At", report.UpdatedAtUtc.ToString("O"));
        WriteField("Loaded Items", report.LoadedItems.ToString());
        WriteField("Considered Items", report.ConsideredItems.ToString());
        WriteField("Publisher Groups Found", report.PublisherGroupsFound.ToString());
        WriteField("Independent Publishers Found", report.IndependentPublishersFound.ToString());
        WriteField("Accepted Sources", report.AcceptedSources.ToString());
        WriteField("Rejected Sources", report.RejectedSources.ToString());
        WriteField("Duplicate Publisher Groups", report.DuplicatePublisherGroups.ToString());
        WriteField("Policy Approved Sources", report.PolicyApprovedSources.ToString());
        WriteField("Source Count Increased Items", report.SourceCountIncreasedItems.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Known Article Seed Catalog Path", DisplayPath(report.KnownArticleSeedCatalogPath));
        WriteField("Trusted Source Catalog Path", DisplayPath(report.TrustedSourceCatalogPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Next Actions", report.NextActions);
        foreach (var pair in report.CoverageByItem.OrderByDescending(pair => pair.Value).Take(25))
        {
            WriteField("Coverage", $"{pair.Key} | {pair.Value:0.##}%");
        }
        foreach (var trace in report.PerItemTrace.Take(25))
        {
            WriteField("Trace", $"{trace.KnowledgeItemId} | before={trace.SourceCountBefore} | after={trace.SourceCountAfter} | accepted={trace.AcceptedSources} | rejected={trace.RejectedSources} | coverage={trace.CoveragePercent:0.##}% | {trace.Status}");
            WriteField("Groups", trace.PublisherGroupsAfter.Count == 0 ? "-" : string.Join(", ", trace.PublisherGroupsAfter));
            WriteField("Matched Seeds", trace.MatchedSeedIds.Count == 0 ? "-" : string.Join(", ", trace.MatchedSeedIds));
            WriteField("Next Action", trace.NextAction);
            WriteMessages("Trace Warnings", trace.Warnings);
            WriteMessages("Query Terms", trace.QueryTerms);
        }
    }

    private int GenerateHypotheses()
    {
        WriteHeader("Hermes Hypothesis Generator");
        var domain = ReadOption(_args, "--domain") ?? "trading";
        var generator = new HypothesisGenerator(BuildStoragePaths());
        var hypotheses = generator.Generate(domain);

        WriteField("Hypotheses", DisplayPath(generator.HypothesesPath));
        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteField("Generated", hypotheses.Count.ToString());
        foreach (var hypothesis in hypotheses.Take(20))
        {
            WriteCognitiveHypothesis(hypothesis);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCognitiveInsights()
    {
        WriteHeader("Hermes Cognitive Insights");
        var generator = new HypothesisGenerator(BuildStoragePaths());
        var insights = generator.LoadInsights();
        if (insights.Count == 0)
        {
            generator.Generate("trading");
            insights = generator.LoadInsights();
        }

        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteField("Count", insights.Count.ToString());
        foreach (var insight in insights.Take(30))
        {
            WriteCognitiveInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlanningStatus()
    {
        WriteHeader("Hermes Autonomous Planning Status");
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var status = service.BuildStatus();
        var outcomeStatus = new TaskOutcomeEvaluator(storagePaths).BuildStatus();

        WriteField("Status", DisplayPath(service.PlanningStatusPath));
        WriteField("Detected Needs", status.NeedsDetected.ToString());
        WriteField("Active Goals", status.ActiveGoals.ToString());
        WriteField("Planned Tasks", status.PlannedTasks.ToString());
        WriteField("Queued Research Items", status.QueuedResearchItems.ToString());
        WriteField("Last Decision", status.LastDecisionId);
        WriteField("Next Action", status.NextAction);
        WriteMessages("Active Domains", status.ActiveDomains);
        WriteMessages("Top Needs", status.TopNeeds);
        WriteMessages("Top Tasks", status.TopTasks);
        WriteMessages("Warnings", status.Warnings);
        WriteField("Outcome Feedback", DisplayPath(outcomeStatus.TaskOutcomesPath));
        WriteField("Outcome Total", outcomeStatus.TotalOutcomes.ToString());
        WriteField("Outcome Last UTC", outcomeStatus.LastOutcomeUtc?.ToString("O") ?? "-");
        WriteMessages("Outcome Recommendations", outcomeStatus.LatestRecommendations.Take(5).ToList());
        WriteField("no_auto_trading", status.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", status.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int DetectNeeds()
    {
        WriteHeader("Hermes Need Detection");
        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var needs = service.DetectNeeds();

        WriteField("Needs", DisplayPath(service.DetectedNeedsPath));
        WriteField("Detected", needs.Count.ToString());
        foreach (var need in needs.Take(30))
        {
            WriteDetectedNeed(need);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int PlanNextTasks()
    {
        WriteHeader("Hermes Autonomous Task Planner");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 100);
        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var decision = service.PlanNextTasks(maxItems);

        WriteField("Decision", decision.DecisionId);
        WriteField("Planned Tasks", DisplayPath(service.PlannedTasksPath));
        WriteField("Needs", decision.Needs.Count.ToString());
        WriteField("Goals", decision.Goals.Count.ToString());
        WriteField("Tasks", decision.PlannedTasks.Count.ToString());
        foreach (var task in decision.PlannedTasks.Take(30))
        {
            WritePlannedTask(task);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunPlanningCycle()
    {
        WriteHeader("Hermes Autonomous Planning Cycle");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 100);
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.RunPlanningCycle(maxItems);
        var queue = new ResearchQueueService(storagePaths).LoadOrCreateQueue();

        WriteField("Planning Status", DisplayPath(service.PlanningStatusPath));
        WriteField("Detected Needs", decision.Needs.Count.ToString());
        WriteField("Planned Tasks", decision.PlannedTasks.Count.ToString());
        WriteField("Research Queue", DisplayPath(new ResearchQueueService(storagePaths).QueuePath));
        WriteField("Open Queue Items", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteMessages("Top Reasons", decision.Explanations.Take(8).ToList());
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExecutePlannedTasks()
    {
        WriteHeader("Hermes Controlled Planned Task Execution");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 10, min: 1, max: 100);
        var storagePaths = BuildStoragePaths();
        var execution = RunPlannedTaskExecution(storagePaths, maxItems);
        var state = execution.State;

        WriteField("Execution State", DisplayPath(execution.ExecutionStatePath));
        WriteField("Execution Log", DisplayPath(execution.ExecutionLogPath));
        WriteField("Requested Max Items", maxItems.ToString());
        WriteField("Results", execution.Results.Count.ToString());
        WriteField("Completed", execution.Completed.ToString());
        WriteField("Skipped", execution.Skipped.ToString());
        WriteField("Failed", execution.Failed.ToString());
        WriteField("Pending Tasks", state.PendingTasks.ToString());
        WriteField("Pending After", execution.PendingAfter.ToString());
        foreach (var result in execution.Results)
        {
            WritePlannedTaskExecutionResult(result);
        }

        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return execution.Failed > 0 ? 1 : 0;
    }

    private static PlannedTaskExecutionRun RunPlannedTaskExecution(StoragePaths storagePaths, int maxItems)
    {
        var executor = new PlannedTaskExecutor(storagePaths);
        var results = executor.Execute(maxItems);
        var state = executor.LoadState() ?? executor.BuildStatus();
        return new PlannedTaskExecutionRun(
            executor.ExecutionStatePath,
            executor.ExecutionLogPath,
            state,
            results,
            results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
            results.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            state.PendingTasks);
    }

    private int ShowPlannedTaskStatus()
    {
        WriteHeader("Hermes Planned Task Execution Status");
        var executor = new PlannedTaskExecutor(BuildStoragePaths());
        var state = executor.BuildStatus();

        WriteField("State", DisplayPath(executor.ExecutionStatePath));
        WriteField("Execution Log", DisplayPath(executor.ExecutionLogPath));
        WriteField("Updated UTC", state.UpdatedAtUtc.ToString("O"));
        WriteField("Pending", state.PendingTasks.ToString());
        WriteField("Running", state.RunningTasks.ToString());
        WriteField("Completed", state.CompletedTasks.ToString());
        WriteField("Skipped", state.SkippedTasks.ToString());
        WriteField("Failed", state.FailedTasks.ToString());
        WriteField("Running Task", state.RunningTaskId ?? "-");
        WriteField("Last Task", state.LastTaskId ?? "-");
        WriteField("Last Status", state.LastStatus);
        foreach (var result in state.RecentResults.Take(10))
        {
            WritePlannedTaskExecutionResult(result);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlannedTaskExecutorStatus()
    {
        WriteHeader("Hermes Planned Task Executor Diagnosis");
        var service = new PlannedTaskExecutorDiagnosisService(BuildStoragePaths());
        var diagnosis = service.Build();

        WriteField("Report JSON", DisplayPath(service.ReportJsonPath));
        WriteField("Report Markdown", DisplayPath(service.ReportMarkdownPath));
        WriteField("Pending", diagnosis.PendingCount.ToString());
        WriteField("Executable", diagnosis.ExecutableCount.ToString());
        WriteField("Blocked", diagnosis.BlockedCount.ToString());
        WriteField("Skipped", diagnosis.SkippedCount.ToString());
        WriteField("Completed", diagnosis.CompletedCount.ToString());
        WriteField("Failed", diagnosis.FailedCount.ToString());
        WriteField("Last Successful Run UTC", diagnosis.LastSuccessfulExecutorRunUtc?.ToString("O") ?? "-");
        WriteField("Recommended Next Action", diagnosis.RecommendedNextAction);
        foreach (var entry in diagnosis.Entries.Take(10))
        {
            WriteField($"{entry.TaskId} ({entry.TaskType})", $"{entry.Status}; executable={entry.Executable}; {entry.Reason}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlannedTaskSchedulerLinkStatus()
    {
        WriteHeader("Hermes Planned Task Scheduler Link Diagnosis");
        var service = new PlannedTaskSchedulerLinkDiagnosisService(
            BuildStoragePaths(),
            Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var diagnosis = service.Build();

        WriteField("Report JSON", DisplayPath(service.ReportJsonPath));
        WriteField("Report Markdown", DisplayPath(service.ReportMarkdownPath));
        WriteField("Scheduler Enabled", diagnosis.SchedulerEnabled.ToString().ToLowerInvariant());
        WriteField("planned_task_executor Job Exists", diagnosis.PlannedTaskExecutorJobExists.ToString().ToLowerInvariant());
        WriteField("planned_task_executor Job Enabled", diagnosis.PlannedTaskExecutorJobEnabled.ToString().ToLowerInvariant());
        WriteField("Last Scheduled Executor Run UTC", diagnosis.LastScheduledExecutorRunUtc?.ToString("O") ?? "-");
        WriteField("Last Manual Executor Run UTC", diagnosis.LastManualExecutorRunUtc?.ToString("O") ?? "-");
        WriteField("Pending Tasks", diagnosis.PendingTasks.ToString());
        WriteField("Executable Tasks", diagnosis.ExecutableTasks.ToString());
        WriteField("Blocked Tasks", diagnosis.BlockedTasks.ToString());
        WriteField("Recommendation", diagnosis.Recommendation);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTaskExecutionLog()
    {
        WriteHeader("Hermes Planned Task Execution Log");
        var limit = ReadIntOption(_args, "--limit", fallback: 20, min: 1, max: 200);
        var executor = new PlannedTaskExecutor(BuildStoragePaths());
        var results = executor.LoadRecentResults(limit);

        WriteField("Execution Log", DisplayPath(executor.ExecutionLogPath));
        WriteField("Entries Shown", results.Count.ToString());
        foreach (var result in results)
        {
            WritePlannedTaskExecutionResult(result);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int EvaluateTaskOutcomes()
    {
        WriteHeader("Hermes Task Outcome Evaluation");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 50, min: 1, max: 500);
        var storagePaths = BuildStoragePaths();
        var evaluator = new TaskOutcomeEvaluator(storagePaths);
        var outcomes = evaluator.Evaluate(maxItems);
        var status = evaluator.BuildStatus();

        WriteField("Task Outcomes", DisplayPath(evaluator.TaskOutcomesPath));
        WriteField("Planner Feedback", DisplayPath(evaluator.PlannerFeedbackPath));
        WriteField("Goal Feedback", DisplayPath(evaluator.GoalFeedbackPath));
        WriteField("Evaluated", outcomes.Count.ToString());
        WriteField("Total Outcomes", status.TotalOutcomes.ToString());
        foreach (var outcome in outcomes.Take(20))
        {
            WriteTaskOutcome(outcome);
        }

        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOutcomeFeedbackStatus()
    {
        WriteHeader("Hermes Outcome Feedback Status");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var status = evaluator.BuildStatus();

        WriteField("Status", DisplayPath(evaluator.StatusPath));
        WriteField("Task Outcomes", DisplayPath(status.TaskOutcomesPath));
        WriteField("Planner Feedback", DisplayPath(status.PlannerFeedbackPath));
        WriteField("Goal Feedback", DisplayPath(status.GoalFeedbackPath));
        WriteField("Updated UTC", status.UpdatedAtUtc.ToString("O"));
        WriteField("Total Outcomes", status.TotalOutcomes.ToString());
        WriteField("Last Outcome UTC", status.LastOutcomeUtc?.ToString("O") ?? "-");
        WriteField("Evaluated Last Run", status.OutcomesEvaluatedLastRun.ToString());
        WriteMessages("Latest Recommendations", status.LatestRecommendations);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlannerFeedback()
    {
        WriteHeader("Hermes Planner Feedback");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var feedback = evaluator.LoadOrCreatePlannerFeedback();

        WriteField("Planner Feedback", DisplayPath(evaluator.PlannerFeedbackPath));
        WriteField("Updated UTC", feedback.UpdatedAtUtc.ToString("O"));
        WriteField("Outcomes Evaluated", feedback.OutcomesEvaluated.ToString());
        WriteMessages("Retired Task Types", feedback.RetiredTaskTypes);
        WriteMessages("Warnings", feedback.Warnings);
        foreach (var item in feedback.TaskTypeFeedback.Take(30))
        {
            WritePlannerTaskTypeFeedback(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGoalFeedback()
    {
        WriteHeader("Hermes Goal Feedback");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var feedback = evaluator.LoadOrCreateGoalFeedback();

        WriteField("Goal Feedback", DisplayPath(evaluator.GoalFeedbackPath));
        WriteField("Updated UTC", feedback.UpdatedAtUtc.ToString("O"));
        WriteField("Outcomes Evaluated", feedback.OutcomesEvaluated.ToString());
        WriteMessages("Warnings", feedback.Warnings);
        foreach (var item in feedback.Goals.Take(30))
        {
            WriteGoalFeedbackEntry(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGoals()
    {
        WriteHeader("Hermes Goals");
        var tracker = new GoalProgressTracker(BuildStoragePaths());
        var state = tracker.Update();

        WriteField("Goal State", DisplayPath(tracker.GoalStatePath));
        WriteField("Goal Progress", DisplayPath(tracker.GoalProgressPath));
        WriteField("Active Goals", state.ActiveGoals.ToString());
        WriteField("Top Goal", string.IsNullOrWhiteSpace(state.TopGoalId) ? "-" : state.TopGoalId);
        WriteMessages("Blocked Goals", state.BlockedGoals);
        foreach (var goal in state.Goals)
        {
            WriteHermesGoal(goal);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGoalStatus()
    {
        WriteHeader("Hermes Goal Status");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <GOAL_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var tracker = new GoalProgressTracker(BuildStoragePaths());
        var state = tracker.LoadOrCreateState();
        var goal = state.Goals.FirstOrDefault(item => item.GoalId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (goal is null)
        {
            WriteWarning($"Goal nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteField("Goal State", DisplayPath(tracker.GoalStatePath));
        WriteHermesGoal(goal);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGoalProgress()
    {
        WriteHeader("Hermes Goal Progress");
        var tracker = new GoalProgressTracker(BuildStoragePaths());
        var state = tracker.Update();
        var progress = tracker.LoadProgress();

        WriteField("Goal State", DisplayPath(tracker.GoalStatePath));
        WriteField("Goal Progress", DisplayPath(tracker.GoalProgressPath));
        WriteField("Updated UTC", progress?.UpdatedAtUtc.ToString("O") ?? state.UpdatedAtUtc.ToString("O"));
        WriteField("Active Goals", state.ActiveGoals.ToString());
        WriteField("Top Goal", string.IsNullOrWhiteSpace(state.TopGoalId) ? "-" : state.TopGoalId);
        WriteMessages("Blocked Goals", progress?.BlockedGoals ?? state.BlockedGoals);
        WriteMessages("Top Next Actions", progress?.TopNextActions.Take(12).ToList() ?? []);
        foreach (var goal in state.Goals.Take(20))
        {
            WriteSubHeader($"{goal.Priority:00} / {goal.GoalId}");
            WriteField("Progress", $"{goal.ProgressScore:0.####}");
            WriteField("Current State", goal.CurrentState);
            WriteField("Blockers", goal.BlockerCount.ToString());
            WriteMessages("Next Actions", goal.NextRecommendedActions.Take(6).ToList());
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainGoal()
    {
        WriteHeader("Hermes Goal Explanation");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <GOAL_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var storagePaths = BuildStoragePaths();
        var tracker = new GoalProgressTracker(storagePaths);
        var state = tracker.LoadOrCreateState();
        var goal = state.Goals.FirstOrDefault(item => item.GoalId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (goal is null)
        {
            WriteWarning($"Goal nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        var decision = new AutonomousPlanningCycleService(storagePaths).LoadLatestDecision();
        var outcomes = new TaskOutcomeEvaluator(storagePaths).LoadOutcomes(100)
            .Where(outcome => outcome.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(outcome => outcome.EvaluatedAtUtc)
            .Take(8)
            .ToList();

        WriteHermesGoal(goal);
        WriteMessages("Warum aktiv", [
            goal.Active ? "goal_active:true" : "goal_active:false",
            $"target_state:{goal.TargetState}",
            goal.BlockerCount > 0 ? $"blockers:{goal.BlockerCount}" : "blockers:0",
            $"progress_score:{goal.ProgressScore:0.####}"
        ]);
        WriteMessages("Zugehoerige Needs", goal.RelatedNeeds);
        WriteMessages(
            "Zuletzt geplante Tasks",
            decision?.PlannedTasks
                .Where(task => task.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .Select(task => $"{task.TaskType}:{task.Status}:{task.TaskId}")
                .ToList() ?? []);
        WriteMessages(
            "Was geholfen hat",
            outcomes
                .Where(outcome => outcome.OutcomeScore.UsefulnessScore >= 0.55 || outcome.Evidence.NeedReduced)
                .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}:usefulness={outcome.OutcomeScore.UsefulnessScore:0.####}")
                .ToList());
        WriteMessages(
            "Was blockiert",
            outcomes
                .Where(outcome => outcome.OutcomeScore.UsefulnessScore < 0.35 || outcome.Evidence.TaskRedundant || outcome.Evidence.TaskFailed)
                .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}:usefulness={outcome.OutcomeScore.UsefulnessScore:0.####}")
                .ToList());
        WriteMessages("Naechste empfohlene Aktionen", goal.NextRecommendedActions);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunAutonomousLoop()
    {
        WriteHeader("Hermes Autonomous Learning Loop");
        var storagePaths = BuildStoragePaths();
        var loop = new AutonomousLearningLoop(storagePaths, Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));
        var config = loop.LoadConfig();
        var maxIterations = ReadIntOption(
            _args,
            "--max-iterations",
            fallback: config.MaxIdleIterations,
            min: 1,
            max: 1000);
        var maxMinutes = ReadDoubleOption(_args, "--max-minutes", fallback: 30, min: 0.01, max: 1440);
        var summary = loop.Run(maxIterations, maxMinutes);

        WriteField("Config", DisplayPath(Path.Combine(_runtimeRoot, "config", "autonomous.loop.json")));
        WriteAutonomousLoopSummary(summary);
        TryWriteMasterStatusSnapshot(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousLoopStatus()
    {
        WriteHeader("Hermes Autonomous Learning Loop Status");
        var loop = BuildAutonomousLearningLoop();
        var state = loop.LoadState();
        var summary = loop.LoadSummary();

        WriteField("State", DisplayPath(loop.StatePath));
        WriteField("Summary", DisplayPath(loop.SummaryPath));
        WriteField("Log", DisplayPath(loop.LogPath));
        WriteField("Status", state.Status);
        WriteField("Run ID", string.IsNullOrWhiteSpace(state.RunId) ? "-" : state.RunId);
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Idle Iterations", state.IdleIterations.ToString());
        WriteField("Work Performed", state.WorkPerformed.ToString());
        WriteField("Average Learning", $"{state.AverageLearningValue:0.####}");
        WriteField("Next Action", state.NextAction);
        WriteField("Last Stop Reason", state.LastStopReason ?? "-");
        WriteField("Last Checkpoint", DisplayOptionalPath(state.LastCheckpointPath));
        if (summary?.LastIteration is not null)
        {
            WriteAutonomousLoopIteration(summary.LastIteration);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousLoopLog()
    {
        WriteHeader("Hermes Autonomous Learning Loop Log");
        var loop = BuildAutonomousLearningLoop();
        var limit = ReadIntOption(_args, "--limit", fallback: 10, min: 1, max: 100);
        var iterations = loop.LoadLog(limit);

        WriteField("Log", DisplayPath(loop.LogPath));
        WriteField("Entries", iterations.Count.ToString());
        foreach (var iteration in iterations.Take(limit))
        {
            WriteAutonomousLoopIteration(iteration);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainLastLoop()
    {
        WriteHeader("Hermes Last Autonomous Loop Explanation");
        var loop = BuildAutonomousLearningLoop();
        var summary = loop.LoadSummary();
        var last = summary?.LastIteration ?? loop.LoadLog(1).FirstOrDefault();
        if (last is null)
        {
            WriteField("Status", "no_loop_iteration_available");
            WriteField("Next Action", "run-autonomous-loop");
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteAutonomousLoopIteration(last);
        WriteMessages("Warum weiter/stoppen",
            [
                last.StopReason is not null
                    ? $"stop_reason:{last.StopReason}"
                    : last.Idle
                        ? "idle:true; keine neuen sinnvollen Ausfuehrungen oder Outcomes"
                        : "learning_work_detected:true",
                $"next_action:{last.NextAction}",
                $"feedback_changes:{last.FeedbackChanges.Count}",
                "no_trading_execution:true",
                "human_review_required:true"
            ]);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMetaReview()
    {
        WriteHeader("Hermes Meta Review");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var review = engine.RunReview();

        WriteField("Meta Review", DisplayPath(engine.MetaReviewPath));
        WriteMetaReview(review);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainHealth()
    {
        WriteHeader("Hermes Domain Health");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var health = engine.LoadOrCreateDomainHealth();

        WriteField("Domain Health", DisplayPath(engine.DomainHealthPath));
        foreach (var domain in health)
        {
            WriteDomainHealth(domain);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowLearningStrategy()
    {
        WriteHeader("Hermes Learning Strategy");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var strategy = engine.LoadOrCreateLearningStrategy();

        WriteField("Learning Strategy", DisplayPath(engine.LearningStrategyPath));
        WriteLearningStrategy(strategy);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGovernanceStatus()
    {
        WriteHeader("Hermes Governance Status");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var review = engine.LoadOrCreateReview();

        WriteField("Meta Review", DisplayPath(engine.MetaReviewPath));
        WriteField("Decisions", review.GovernanceDecisions.Count.ToString());
        foreach (var decision in review.GovernanceDecisions)
        {
            WriteGovernanceDecision(decision);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainPlan()
    {
        WriteHeader("Hermes Planning Explanation");
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.LoadLatestDecision() ?? service.PlanNextTasks(20);
        var plannerFeedback = new TaskOutcomeEvaluator(storagePaths).LoadPlannerFeedback();

        WriteField("Decision", decision.DecisionId);
        WriteField("Created UTC", decision.CreatedAtUtc.ToString("O"));
        WriteField("Needs", decision.Needs.Count.ToString());
        WriteField("Goals", decision.Goals.Count.ToString());
        WriteField("Tasks", decision.PlannedTasks.Count.ToString());
        if (plannerFeedback is not null)
        {
            WriteField("Planner Feedback UTC", plannerFeedback.UpdatedAtUtc.ToString("O"));
            WriteMessages(
                "Feedback Adjustments",
                plannerFeedback.TaskTypeFeedback
                    .Where(item => Math.Abs(item.PriorityAdjustment) > 0.0001)
                    .Select(item => $"{item.TaskType}: {item.Recommendation}, adjustment={item.PriorityAdjustment:0.####}")
                    .Take(12)
                    .ToList());
        }

        WriteMessages("Explanation", decision.Explanations.Take(20).ToList());
        foreach (var task in decision.PlannedTasks.Take(10))
        {
            WritePlannedTask(task);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainTask()
    {
        WriteHeader("Hermes Task Explanation");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <TASK_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var decision = service.LoadLatestDecision() ?? service.PlanNextTasks(20);
        var task = decision.PlannedTasks.FirstOrDefault(item => item.TaskId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            WriteWarning($"Task nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WritePlannedTask(task);
        var need = decision.Needs.FirstOrDefault(item => item.NeedId.Equals(task.NeedId, StringComparison.OrdinalIgnoreCase));
        if (need is not null)
        {
            WriteDetectedNeed(need);
        }

        var goal = decision.Goals.FirstOrDefault(item => item.GoalId.Equals(task.GoalId, StringComparison.OrdinalIgnoreCase));
        if (goal is not null)
        {
            WriteHermesGoal(goal);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyClusters()
    {
        WriteHeader("Hermes Strategy Clusters");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var clusters = generator.LoadClusters();
        if (clusters.Count == 0)
        {
            clusters = generator.Generate().Clusters;
        }

        WriteField("Clusters", DisplayPath(generator.ClustersPath));
        foreach (var cluster in clusters)
        {
            WriteStrategyCluster(cluster);
        }

        WriteSafety();
        return 0;
    }

    private int ShowRegimeSummary()
    {
        WriteHeader("Hermes Market Regime Summary");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var analysis = classifier.Run();

        WriteField("Summary", DisplayPath(analysis.SummaryPath));
        WriteField("Distribution", DisplayPath(analysis.DistributionPath));
        WriteField("Strategy Performance", DisplayPath(analysis.StrategyPerformancePath));
        WriteField("Snapshot Memory", DisplayPath(analysis.SnapshotMemoryPath));
        WriteRegimeSummary(analysis.Summary);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyRegimePerformance()
    {
        WriteHeader("Hermes Strategy Regime Performance");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var report = classifier.LoadStrategyPerformance() ?? classifier.Run().StrategyPerformance;

        WriteField("Report", DisplayPath(classifier.StrategyPerformancePath));
        WriteField("Strategies Analyzed", report.StrategiesAnalyzed.ToString());
        WriteField("Regime Snapshots", report.RegimeSnapshotsAnalyzed.ToString());
        WriteField("Regime Consistency", $"{report.RegimeConsistencyScore:0.####}");
        WriteField("Regime Sample Quality", $"{report.RegimeSampleQuality:0.####}");
        WriteMessages("Strong Regime Matches", report.StrongRegimeMatches.Take(12).ToList());
        WriteMessages("Weak Regime Matches", report.WeakRegimeMatches.Take(12).ToList());
        WriteMessages("Preferred Regimes", report.PreferredRegimes?.Take(12).ToList() ?? []);
        WriteMessages("Avoided Regimes", report.AvoidedRegimes?.Take(12).ToList() ?? []);
        WriteMessages("Preferred Sessions", report.PreferredSessions);
        WriteMessages("Avoid Sessions", report.AvoidSessions);
        WriteMessages("Volatility Preference", report.VolatilityPreference);
        foreach (var entry in report.Entries.Take(10))
        {
            WriteStrategyRegimeEntry(entry);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRegimeDistribution()
    {
        WriteHeader("Hermes Regime Distribution");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var report = classifier.LoadDistribution() ?? classifier.Run().Distribution;

        WriteField("Report", DisplayPath(classifier.DistributionPath));
        WriteField("Total Candles", report.TotalCandles.ToString());
        foreach (var entry in report.Entries.Take(20))
        {
            WriteSubHeader($"{entry.Symbol} {entry.Timeframe} / {entry.RegimeType} / {entry.Session}");
            WriteField("Candles", entry.CandleCount.ToString());
            WriteField("Share", $"{entry.Percentage:P2}");
            WriteField("Confidence", $"{entry.AverageConfidence:0.####}");
        }

        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPatternCatalog()
    {
        WriteHeader("Hermes Strategy/Pattern Knowledge Base");
        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        var patterns = catalog.LoadOrCreateCatalog();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Patterns", patterns.Count.ToString());
        foreach (var pattern in patterns)
        {
            WriteSubHeader($"{pattern.Name} / {pattern.Id}");
            WriteField("Source", pattern.SourceName ?? "local");
            WriteField("Source URL", pattern.SourceUrl ?? "-");
            WriteField("source_trust", pattern.SourceTrust ?? "-");
            WriteField("Category", pattern.Category ?? "-");
            WriteField("Direction Bias", pattern.DirectionBias);
            WriteField("Strategy Family", StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id));
            WriteField("Timeframes", string.Join(", ", pattern.RequiredTimeframes));
            WriteField("Market Context", pattern.MarketContext ?? "-");
            WriteField("Test Priority", pattern.TestPriority ?? "-");
            WriteField("Sessions", string.Join(", ", pattern.PreferredSessions));
            WriteField("Regimes", string.Join(", ", pattern.MarketRegimes));
            WriteField("Risk Hint", pattern.RiskModelHint);
            WriteMessages("Trigger Rules", pattern.TriggerRules.Select(rule => $"{rule.RuleId}: {rule.Description}").ToList());
            WriteMessages("Invalidation Rules", pattern.InvalidationRules.Select(rule => $"{rule.RuleId}: {rule.Description}").ToList());
            WriteMessages("Tags", pattern.Tags.Select(tag => tag.Id).ToList());
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPatternPerformance()
    {
        WriteHeader("Hermes Pattern Performance");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        var performance = generator.LoadPatternPerformance();
        var sourcePerformance = generator.LoadSourcePerformance();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteMessages("Pattern Performance", performance);
        WriteMessages("Source Performance", sourcePerformance);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTopStrategies()
    {
        WriteHeader("Hermes Top Strategies");
        var service = new StrategyResearchService(BuildStoragePaths());
        var memory = service.LoadOrCreateMemory();

        if (memory.TopVariants.Count == 0)
        {
            WriteWarning("Noch keine Strategy-Research-Ergebnisse vorhanden.");
            WriteSafety();
            return 0;
        }

        foreach (var result in memory.TopVariants.Take(10))
        {
            WriteStrategyResult(result);
        }

        WriteSafety();
        return 0;
    }

    private StrategyResearchStepResult RunStrategyResearchAndInsights(StoragePaths storagePaths)
    {
        var service = new StrategyResearchService(storagePaths);
        var before = service.LoadOrCreateMemory().VariantsTested;
        var memory = service.RunResearch();
        var testedNow = Math.Max(0, memory.VariantsTested - before);
        var generator = new ResearchInsightsGenerator(storagePaths);
        var insights = generator.Generate();

        return new StrategyResearchStepResult(
            memory,
            testedNow,
            generator.InsightsPath,
            generator.ClustersPath,
            insights);
    }

    private bool TryLoadLatestResearchReport(out string path, out JsonElement root)
    {
        var preferredPaths = new[]
        {
            Path.Combine(_dataRoot, "reports", "research", "latest_research_summary.json"),
            Path.Combine(_dataRoot, "reports", "nightly", "latest_nightly_research.json")
        };

        foreach (var preferredPath in preferredPaths)
        {
            if (TryLoadJson(preferredPath, out root))
            {
                path = preferredPath;
                return true;
            }
        }

        var latestReport = FindResearchSummaryReports().LastOrDefault()
            ?? FindNightlyResearchReports().LastOrDefault();
        if (latestReport is not null && TryLoadJson(latestReport, out root))
        {
            path = latestReport;
            return true;
        }

        path = string.Empty;
        root = default;
        return false;
    }

    private bool TryLoadLatestBetaReport(out string path, out JsonElement root)
    {
        var latestPath = Path.Combine(_dataRoot, "reports", "beta", "latest_beta_learning.json");
        if (TryLoadJson(latestPath, out root))
        {
            path = latestPath;
            return true;
        }

        var latestReport = FindBetaReports().LastOrDefault();
        if (latestReport is not null && TryLoadJson(latestReport, out root))
        {
            path = latestReport;
            return true;
        }

        path = string.Empty;
        root = default;
        return false;
    }

    private void WriteResearchSummaryFields(JsonElement root, bool detailed)
    {
        var symbolsProcessed = GetStringArray(root, "symbols_processed", "symbolsProcessed");
        WriteField("Run ID", GetString(root, "run_id", "runId", "job_id", "jobId"));
        WriteField("Status", GetString(root, "status"));
        WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
        WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
        WriteField("Symbols Processed", symbolsProcessed.Count == 0 ? "-" : string.Join(", ", symbolsProcessed));
        WriteField("Candles Processed", GetInt(root, "candles_processed", "candlesProcessed").ToString());
        WriteField("Features", GetInt(root, "features_generated", "featuresGenerated", "feature_count", "featureCount").ToString());
        WriteField("Signals", GetInt(root, "signals_generated", "signalsGenerated", "signal_count", "signalCount").ToString());
        WriteField("Outcomes", GetInt(root, "outcomes_generated", "outcomesGenerated", "outcome_count", "outcomeCount").ToString());
        WriteField("Backtests", GetInt(root, "backtests_generated", "backtestsGenerated", "backtest_count", "backtestCount").ToString());
        WriteField("Reports", GetInt(root, "reports_generated", "reportsGenerated").ToString());
        WriteField("Duration", $"{GetDouble(root, "duration_seconds", "durationSeconds"):0.###} s");
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));

        if (detailed)
        {
            WriteField("Feature Output", DisplayOptionalPath(GetString(root, "feature_output_path", "featureOutputPath")));
            WriteField("Signal Output", DisplayOptionalPath(GetString(root, "signal_output_path", "signalOutputPath")));
            WriteField("Outcome Report", DisplayOptionalPath(GetString(root, "outcome_report_path", "outcomeReportPath")));
            WriteField("Backtest Report", DisplayOptionalPath(GetString(root, "backtest_report_path", "backtestReportPath")));
            WriteField("Nightly Report", DisplayOptionalPath(GetString(root, "nightly_report_path", "nightlyReportPath")));
            WriteField("Research Report", DisplayOptionalPath(GetString(root, "research_report_path", "researchReportPath")));
        }

        WriteMessages("Warnings", GetStringArray(root, "warnings"));
    }

    private void WriteBetaReportFields(JsonElement root, bool detailed)
    {
        var symbolsProcessed = GetStringArray(root, "symbols_processed", "symbolsProcessed");
        WriteField("Run ID", GetString(root, "run_id", "runId"));
        WriteField("Status", GetString(root, "status"));
        WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
        WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
        WriteField("Symbols Processed", symbolsProcessed.Count == 0 ? "-" : string.Join(", ", symbolsProcessed));
        WriteField("Candles Processed", GetInt(root, "candles_processed", "candlesProcessed").ToString());
        WriteField("Features", GetInt(root, "features_generated", "featuresGenerated").ToString());
        WriteField("Signals", GetInt(root, "signals_generated", "signalsGenerated").ToString());
        WriteField("Outcomes", GetInt(root, "outcomes_generated", "outcomesGenerated").ToString());
        WriteField("Backtests", GetInt(root, "backtests_generated", "backtestsGenerated").ToString());
        WriteField("Duration", $"{GetDouble(root, "duration_seconds", "durationSeconds"):0.###} s");
        WriteField("learning_ready", GetBoolText(root, "learning_ready", "learningReady"));
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));

        if (detailed)
        {
            WriteField("Beta Report", DisplayOptionalPath(GetString(root, "beta_report_path", "betaReportPath")));
            WriteField("Research Report", DisplayOptionalPath(GetString(root, "research_report_path", "researchReportPath")));
            WriteField("Feature Output", DisplayOptionalPath(GetString(root, "feature_output_path", "featureOutputPath")));
            WriteField("Signal Output", DisplayOptionalPath(GetString(root, "signal_output_path", "signalOutputPath")));
            WriteField("Outcome Report", DisplayOptionalPath(GetString(root, "outcome_report_path", "outcomeReportPath")));
            WriteField("Backtest Report", DisplayOptionalPath(GetString(root, "backtest_report_path", "backtestReportPath")));
        }

        WriteMessages("Warnings", GetStringArray(root, "warnings"));
    }

    private void WriteResearchMemoryIndex(ResearchMemoryIndex index)
    {
        WriteField("Updated UTC", index.UpdatedAtUtc.ToString("O"));
        WriteField("Last Run UTC", index.LastRunAt?.ToString("O") ?? "-");
        WriteField("Run Count", index.RunCount.ToString());
        WriteField("Symbols Processed", index.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", index.SymbolsProcessed));
        WriteField("Timeframes Processed", index.TimeframesProcessed.Count == 0 ? "-" : string.Join(", ", index.TimeframesProcessed));
        WriteField("Candles Processed", index.CandlesProcessed.ToString());
        WriteField("Features", index.FeaturesGenerated.ToString());
        WriteField("Signals", index.SignalsGenerated.ToString());
        WriteField("Outcomes", index.OutcomesGenerated.ToString());
        WriteField("Backtests", index.BacktestsGenerated.ToString());
        WriteField("Processed Ranges", index.ProcessedRanges.Count.ToString());
        WriteField("learning_ready", index.LearningReady.ToString().ToLowerInvariant());
        WriteField("Indexed Run IDs", index.IndexedRunIds.Count.ToString());

        foreach (var range in index.ProcessedRanges.TakeLast(5))
        {
            WriteSubHeader($"{range.Symbol} {range.Timeframe}");
            WriteField("Candles", range.CandleCount.ToString());
            WriteField("From UTC", range.FromUtc?.ToString("O") ?? "-");
            WriteField("To UTC", range.ToUtc?.ToString("O") ?? "-");
            WriteField("Source", DisplayPath(range.SourcePath));
        }

        WriteMessages("Warnings", index.Warnings);
    }

    private void WriteStrategyResearchMemory(StrategyResearchMemory memory, int limit)
    {
        WriteField("Updated UTC", memory.UpdatedAtUtc.ToString("O"));
        WriteField("Variants Tested", memory.VariantsTested.ToString());
        WriteField("Top Variants", memory.TopVariants.Count.ToString());
        WriteField("Rejected Variants", memory.RejectedVariants.Count.ToString());
        WriteField("Research Memory Entries", (memory.ResearchEntries?.Count ?? 0).ToString());
        WriteField("no_auto_trading", memory.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", memory.HumanReviewRequired.ToString().ToLowerInvariant());

        foreach (var result in memory.TopVariants.Take(limit))
        {
            WriteStrategyResult(result);
        }

        WriteMessages("Warnings", memory.Warnings);
    }

    private void WriteStrategyResult(StrategyResearchResult result)
    {
        var patternName = ResolvePatternName(result.Variant.PatternId);
        WriteSubHeader($"{result.Variant.Family} / {patternName} / {result.Variant.VariantId}");
        WriteField("Pattern ID", result.Variant.PatternId ?? "-");
        WriteField("Pattern", patternName);
        WriteField("Score", $"{result.Fitness.Score:0.####}");
        WriteField("Winrate", $"{result.Fitness.Winrate * 100:0.##}%");
        WriteField("Average RR", $"{result.Fitness.AverageRr:0.####}");
        WriteField("Drawdown Penalty", $"{result.Fitness.DrawdownPenalty:0.####}");
        WriteField("Stability Bonus", $"{result.Fitness.StabilityBonus:0.####}");
        WriteField("Trade Count Factor", $"{result.Fitness.TradeCountFactor:0.####}");
        WriteField("Trades", result.TradeCount.ToString());
        WriteField("Wins/Losses", $"{result.WinCount}/{result.LossCount}");
        WriteField("Avg R", $"{result.AverageR:0.####}");
        WriteField("Max Drawdown", $"{result.MaxDrawdown:0.####}");
        WriteField("EMA", $"{result.Variant.FastEma}/{result.Variant.SlowEma}");
        WriteField("RR", $"{result.Variant.RiskRewardRatio:0.##}");
        WriteField("SL ATR", $"{result.Variant.StopLossAtrMultiplier:0.##}");
        WriteField("Confirmation", result.Variant.RequireConfirmationCandle.ToString().ToLowerInvariant());
        WriteField("Vol Filter", result.Variant.UseVolatilityFilter.ToString().ToLowerInvariant());
        WriteField("Session Filter", result.Variant.SessionFilter ?? "any");
        WriteField("Variant Timeframe", result.Variant.Timeframe ?? "any");
        WriteField("From UTC", result.FromUtc?.ToString("O") ?? "-");
        WriteField("To UTC", result.ToUtc?.ToString("O") ?? "-");
    }

    private void WriteBotCandidate(BotCandidate candidate)
    {
        WriteSubHeader($"{candidate.Status} / {candidate.StrategyFamily} / {candidate.PatternId ?? "-"} / {candidate.StrategyId}");
        WriteField("Candidate ID", candidate.CandidateId);
        WriteField("Symbol/Timeframe", $"{candidate.Symbol}/{candidate.Timeframe}");
        WriteField("Confidence", candidate.Criteria.Confidence);
        WriteField("OOS Available", candidate.Criteria.OosAvailable.ToString().ToLowerInvariant());
        WriteField("WalkForward Confidence", $"{candidate.Criteria.WalkForwardConfidence:0.####}");
        WriteField("Realism Score", $"{candidate.Criteria.RealismScore:0.####}");
        WriteField("Overfit Risk", $"{candidate.Criteria.OverfitRisk:0.####}");
        WriteField("Cost Sensitivity", $"{candidate.Criteria.CostSensitivity:0.####}");
        WriteField("Regime Consistency", $"{candidate.Criteria.RegimeConsistencyScore:0.####}");
        WriteField("Max Drawdown", $"{candidate.Criteria.MaxDrawdown:0.####}");
        WriteField("Profit Factor", $"{candidate.Criteria.ProfitFactor:0.####}");
        WriteField("Sample Quality", $"{candidate.Criteria.SampleQuality:0.####}");
        WriteField("too_good_to_be_true", candidate.Criteria.TooGoodToBeTrue.ToString().ToLowerInvariant());
        WriteField("Monte-Carlo Passed", candidate.Criteria.MonteCarloPassed.ToString().ToLowerInvariant());
        WriteField("Positive Sim Ratio", $"{candidate.Criteria.PositiveSimulationRatio:0.####}");
        WriteField("Survives Spread x2", candidate.Criteria.SurvivesSpreadX2.ToString().ToLowerInvariant());
        WriteField("Survives Stress Cost", candidate.Criteria.SurvivesStressCost.ToString().ToLowerInvariant());
        WriteField("Risk of Ruin", $"{candidate.Criteria.RiskOfRuinProbabilityEstimate:0.####}");
        WriteField("Recommended Risk", $"{candidate.Criteria.RecommendedMaxRiskPerTrade:0.####}%");
        WriteField("Next Validation", candidate.NextValidationRecommendation);
        WriteMessages("Rejection Reasons", candidate.RejectionReasons.Take(12).ToList());
        WriteMessages("Overfit Flags", candidate.OverfitFlags.Take(8).ToList());
        WriteField("No Bot Created", candidate.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", candidate.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", candidate.NoBrokerAction.ToString().ToLowerInvariant());
        Console.WriteLine();
    }

    private void WriteCandidateGateDiagnostic(CandidateGateDiagnostics item)
    {
        WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyId}");
        WriteField("Candidate ID", item.CandidateId);
        WriteField("Symbol/Timeframe", $"{item.Symbol}/{item.Timeframe}");
        WriteField("Status", item.Status);
        WriteField("Primary Reason", item.PrimaryRejectionReason);
        WriteField("Weakest Metric", item.WeakestMetric);
        WriteField("Nearest Threshold", item.NearestPassThreshold);
        WriteField("Near-Miss Score", $"{item.NearMissScore:0.####}");
        WriteField("Near Miss", item.IsNearMiss.ToString().ToLowerInvariant());
        WriteField("Unsuitable", item.IsCompletelyUnsuitable.ToString().ToLowerInvariant());
        WriteField("Improvement Hint", item.ImprovementHint);
        WriteMessages("Secondary Reasons", item.SecondaryRejectionReasons.Take(6).ToList());
        Console.WriteLine();
    }

    private void WriteStrategyImprovementSuggestion(StrategyImprovementSuggestion suggestion)
    {
        WriteSubHeader($"{suggestion.Priority} / {suggestion.SuggestionId}");
        WriteField("Title", suggestion.Title);
        WriteField("Target Metric", suggestion.TargetMetric);
        WriteField("Expected Impact", suggestion.ExpectedImpact);
        WriteField("Description", suggestion.Description);
        WriteMessages("Related Reasons", suggestion.RelatedRejectionReasons);
        Console.WriteLine();
    }

    private static string FormatSuggestion(StrategyImprovementSuggestion suggestion) =>
        $"{suggestion.Priority}:{suggestion.SuggestionId}:{suggestion.Title} -> {suggestion.TargetMetric}";

    private void WriteQualityImprovementReportHeader(
        ResearchQualityImprovementExperimentService service,
        QualityImprovementExperimentReport report)
    {
        WriteField("Quality Report", DisplayPath(service.QualityImprovementPath));
        WriteField("OOS Report", DisplayPath(service.OosStabilityPath));
        WriteField("Cost Report", DisplayPath(service.CostResiliencePath));
        WriteField("Risk Report", DisplayPath(service.RiskSensitivityPath));
        WriteField("Candidates Analyzed", report.CandidatesAnalyzed.ToString());
        WriteField("Batch Size", report.BatchSize.ToString());
        WriteField("Baseline Near Miss", report.BaselineNearMissCount.ToString());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteOosExperiment(OosQualityImprovementExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Walk-Forward Plan", experiment.WalkForwardPlan);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Proposed Filters", experiment.ProposedFilters);
        WriteMessages("Rolling Windows", experiment.RollingValidationWindows);
        Console.WriteLine();
    }

    private void WriteCostExperiment(CostResilienceExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Minimum Move/Cost", $"{experiment.MinimumMoveToCostRatio:0.##}x");
        WriteField("Spread Stress", experiment.SpreadStressScenario);
        WriteField("Slippage Stress", experiment.SlippageStressScenario);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Proposed Filters", experiment.ProposedFilters);
        WriteMessages("Avoid Sessions", experiment.AvoidSessions);
        Console.WriteLine();
    }

    private void WriteRiskExperiment(RiskSensitivityExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Risk Profiles", string.Join(", ", experiment.RiskProfiles.Select(value => $"{value:P2}")));
        WriteField("Target Ruin Probability", $"{experiment.TargetRuinProbability:P2}");
        WriteField("Trade Frequency", experiment.MaxTradeFrequencyHint);
        WriteField("Drawdown Control", experiment.DrawdownControl);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        Console.WriteLine();
    }

    private void WriteRegimeExperiment(RegimeSessionFilterExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Volatility Filter", experiment.VolatilityFilter);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Preferred Regimes", experiment.PreferredRegimes);
        WriteMessages("Avoided Regimes", experiment.AvoidedRegimes);
        WriteMessages("Preferred Sessions", experiment.PreferredSessions);
        WriteMessages("Avoided Sessions", experiment.AvoidedSessions);
        Console.WriteLine();
    }

    private void WriteMonteCarloResult(MonteCarloResult result)
    {
        WriteSubHeader($"{result.StrategyFamily} / {result.PatternId ?? "-"} / {result.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{result.Symbol}/{result.Timeframe}");
        WriteField("Simulations", result.SimulationsRun.ToString());
        WriteField("Positive Ratio", $"{result.PositiveSimulationRatio:0.####}");
        WriteField("Median Return", $"{result.MedianReturn:0.####}");
        WriteField("Worst Drawdown", $"{result.WorstCaseDrawdown:0.####}");
        WriteField("Ruin Probability", $"{result.RuinProbabilityEstimate:0.####}");
        WriteField("monte_carlo_passed", result.MonteCarloPassed.ToString().ToLowerInvariant());
        WriteMessages("Warnings", result.Warnings);
    }

    private void WriteCostStressResult(CostStressResult result)
    {
        WriteSubHeader($"{result.StrategyFamily} / {result.PatternId ?? "-"} / {result.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{result.Symbol}/{result.Timeframe}");
        WriteField("Survives Normal", result.SurvivesNormalCost.ToString().ToLowerInvariant());
        WriteField("Survives Spread x2", result.SurvivesSpreadX2.ToString().ToLowerInvariant());
        WriteField("Survives Spread x3", result.SurvivesSpreadX3.ToString().ToLowerInvariant());
        WriteField("Survives Stress", result.SurvivesStressCost.ToString().ToLowerInvariant());
        WriteField("Failure Reason", result.CostFailureReason);
        foreach (var scenario in result.ScenarioResults.Take(3))
        {
            WriteField($"Scenario {scenario.Scenario.Name}", $"pf={scenario.AdjustedProfitFactor:0.####},net_r={scenario.AdjustedNetR:0.####},score={scenario.SurvivalScore:0.####},survived={scenario.Survived.ToString().ToLowerInvariant()}");
        }
    }

    private void WriteScalpingSummary(ScalpingResearchService service, ScalpingResearchReport report)
    {
        var signalSpecRoots = new[]
        {
            service.SignalSpecDirectory,
            Path.Combine(BuildStoragePaths().Root, "reports", "signal_agent_specs"),
            Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_agent_specs")
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(Directory.Exists)
        .ToList();
        var signalSpecCount = signalSpecRoots
            .SelectMany(root => Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        WriteField("Report", DisplayPath(service.LatestReportPath));
        WriteField("scalping_asset", report.Asset);
        WriteField("variants_tested", report.VariantsTested.ToString());
        WriteField("candidates_total", report.CandidatesTotal.ToString());
        WriteField("robust_candidates", report.RobustCandidates.ToString());
        WriteField("rejected_candidates", report.RejectedCandidates.ToString());
        WriteField("needs_more_data", report.NeedsMoreData.ToString());
        WriteField("best_candidate", report.BestCandidateId ?? "-");
        WriteField("bot_specs_ready", Directory.Exists(service.BotSpecDirectory) ? Directory.GetFiles(service.BotSpecDirectory, "*.json").Length.ToString() : "0");
        WriteField("signal_specs_ready", signalSpecCount.ToString());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", report.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", report.LiveTradingEnabled.ToString().ToLowerInvariant());
        WriteMessages("Data Gaps", report.DataGaps);
    }

    private void WriteMarketDataAvailability(MarketDataAvailabilityService service, MarketDataAvailability report)
    {
        WriteField("Availability Report", DisplayPath(service.AvailabilityPath));
        WriteField("Quality Report", DisplayPath(service.QualityPath));
        WriteField("Sources", report.Sources.Count.ToString());
        WriteField("CSV Files", report.Files.Count.ToString());
        WriteField("Assets Available", report.AssetsAvailable.Count == 0 ? "-" : string.Join(", ", report.AssetsAvailable));
        WriteField("GER40 Available", report.Ger40Available.ToString().ToLowerInvariant());
        WriteField("XAUUSD Available", report.XauusdAvailable.ToString().ToLowerInvariant());
        WriteField("EURUSD Available", report.EurusdAvailable.ToString().ToLowerInvariant());
        WriteField("Candle Count", report.Files.Sum(file => file.CandleCount).ToString());
        WriteMessages("Data Gaps", report.DataGaps);
        WriteMessages("Warnings", report.Warnings);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", report.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", report.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private void WriteMarketDataQuality(MarketDataAvailabilityService service, MarketDataQualityReport report)
    {
        WriteField("Quality Report", DisplayPath(service.QualityPath));
        WriteField("Asset", report.Asset);
        WriteField("Health", report.QualityHealth);
        WriteField("Files", report.FileCount.ToString());
        WriteField("Candles", report.CandleCount.ToString());
        WriteField("Missing Candles", report.MissingCandles.ToString());
        WriteField("Duplicate Candles", report.DuplicateCandles.ToString());
        WriteField("Invalid Candles", report.InvalidCandles.ToString());
        WriteMessages("Timeframes", report.TimeframesAvailable);
        WriteMessages("Data Gaps", report.DataGaps);
        WriteMessages("Warnings", report.Warnings);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", report.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", report.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private void WriteMarketDataFile(MarketDataFile file)
    {
        WriteSubHeader($"{file.Asset} {file.Timeframe}");
        WriteField("Source", file.Source);
        WriteField("Path", DisplayPath(file.FilePath));
        WriteField("Candles", file.CandleCount.ToString());
        WriteField("First", file.FirstTimestamp?.ToString("O") ?? "-");
        WriteField("Last", file.LastTimestamp?.ToString("O") ?? "-");
        WriteField("Missing", file.MissingCandles.ToString());
        WriteField("Duplicates", file.DuplicateCandles.ToString());
        WriteField("Invalid", file.InvalidCandles.ToString());
        WriteField("Timezone", file.Timezone);
        WriteField("Spread", file.SpreadAvailable.ToString().ToLowerInvariant());
        WriteField("Volume", file.VolumeAvailable.ToString().ToLowerInvariant());
        WriteMessages("Warnings", file.Warnings);
    }

    private static void WriteScalpingCandidateSummary(ScalpingStrategyCandidate candidate)
    {
        WriteSubHeader($"{candidate.CandidateId} / {candidate.ValidationStatus} / {candidate.SetupType}");
        WriteField("Asset", candidate.Asset);
        WriteField("Timeframe", candidate.Timeframe);
        WriteField("Confidence", $"{candidate.ConfidenceScore:0.####}");
        WriteField("Trades", candidate.Backtest.TradeCount.ToString());
        WriteField("IS/OOS/WF", $"{candidate.Backtest.InSampleNetR:0.####} / {candidate.Backtest.OosNetR:0.####} / {candidate.Backtest.WalkForwardNetR:0.####}");
        WriteField("Cost Stress", $"{candidate.Backtest.CostStressNetR:0.####}");
        WriteField("Risk of Ruin", $"{candidate.RiskProfile.RiskOfRuinProbability:0.####}");
        WriteMessages("Rejection Reasons", candidate.RejectionReasons);
    }

    private static void WriteScalpingCandidateDetails(ScalpingStrategyCandidate candidate)
    {
        WriteScalpingCandidateSummary(candidate);
        WriteField("Strategy", candidate.StrategyName);
        WriteField("Risk Per Trade", $"{candidate.RiskPerTrade:0.####}");
        WriteField("Max Daily Loss", $"{candidate.MaxDailyLoss:0.####}");
        WriteField("Max Trades/Day", candidate.MaxTradesPerDay.ToString());
        WriteField("Session Filter", candidate.SessionFilter);
        WriteField("Spread Filter", candidate.SpreadFilter);
        WriteField("News Filter", candidate.NewsFilterStub);
        WriteMessages("Entry Rules", candidate.EntryRules);
        WriteMessages("Exit Rules", candidate.ExitRules);
        WriteMessages("Stop Rules", candidate.StopLossRules);
        WriteMessages("Take Profit Rules", candidate.TakeProfitRules);
        WriteMessages("Overfit Warnings", candidate.Validation.OverfitWarnings);
        WriteMessages("Gate Failures", candidate.Validation.GateFailures);
    }

    private static void WriteScalpingRobustnessReport(ScalpingRobustnessExpansionReport report)
    {
        WriteSubHeader($"{report.CandidateId} / {report.Status}");
        WriteField("Asset", report.Asset);
        WriteField("Setup", report.SetupType);
        WriteField("Stability", $"{report.StabilityScore:0.####}");
        WriteField("Final Candidate", report.FinalCandidate.ToString().ToLowerInvariant());
        WriteField("MC Simulations", report.MonteCarlo.Simulations.ToString());
        WriteField("MC Median", $"{report.MonteCarlo.MedianOutcomeR:0.####}");
        WriteField("MC Worst 5%", $"{report.MonteCarlo.WorstFivePercentOutcomeR:0.####}");
        WriteField("MC Ruin", $"{report.MonteCarlo.RuinProbability:0.####}");
        WriteField("Sensitivity", report.ParameterSensitivity.Health);
        WriteField("Sensitivity Positive", $"OOS {report.ParameterSensitivity.PositiveOosVariants}/{report.ParameterSensitivity.VariantsTested}, WF {report.ParameterSensitivity.PositiveWalkForwardVariants}/{report.ParameterSensitivity.VariantsTested}, Cost {report.ParameterSensitivity.PositiveCostStressVariants}/{report.ParameterSensitivity.VariantsTested}");
        WriteField("Regimes", $"{report.RegimeValidation.PositiveOrNeutralRegimes}/7 {report.RegimeValidation.Health}");
        WriteMessages("Blockers", report.Blockers);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private static void WriteScalpingSensitivityDetails(ScalpingParameterSensitivityReport report)
    {
        WriteField("Candidate", report.CandidateId);
        WriteField("Health", report.Health);
        WriteField("Baseline Variants", report.VariantsTested.ToString());
        WriteField("Worst Confidence Drop", $"{report.WorstConfidenceDrop:0.####}");
        WriteField("Confidence Drop Explainable", report.ConfidenceDropExplainable.ToString().ToLowerInvariant());
        WriteField("Stable Corridor Available", report.StableConservativeCorridorAvailable.ToString().ToLowerInvariant());
        WriteField("Primary Driver", report.StableCorridor.PrimaryConfidenceDropDriver);
        WriteMessages("Blockers", report.Blockers);
        foreach (var detail in report.Details.OrderBy(item => item.ParameterName).ThenBy(item => item.VariantLabel))
        {
            WriteSubHeader(detail.VariantLabel);
            WriteField("Parameter", detail.ParameterName);
            WriteField("Baseline Confidence", $"{detail.BaselineConfidence:0.####}");
            WriteField("Variant Confidence", $"{detail.VariantConfidence:0.####}");
            WriteField("Confidence Delta", $"{detail.ConfidenceDelta:0.####}");
            WriteField("OOS Delta", $"{detail.OosDelta:0.####}");
            WriteField("WF Delta", $"{detail.WalkForwardDelta:0.####}");
            WriteField("Cost Delta", $"{detail.CostStressDelta:0.####}");
            WriteField("Stability", detail.Stability);
        }
    }

    private void WriteScalpingCertificationReport(ScalpingCertificationReport report)
    {
        WriteSubHeader($"{report.CandidateId} / {report.Status}");
        WriteField("Asset", report.Asset);
        WriteField("Timeframe", report.Timeframe);
        WriteField("Setup", report.SetupType);
        WriteField("Certified", report.CertifiedCandidate.ToString().ToLowerInvariant());
        WriteField("Total Trades", report.TotalTrades?.ToString() ?? "not_captured");
        WriteField("Trades Per Month", report.TradesPerMonth?.ToString() ?? "not_captured");
        WriteField("Trades Per Week", report.TradesPerWeek?.ToString() ?? "not_captured");
        WriteField("Average Holding Duration", report.AverageHoldingDurationMinutes?.ToString("0.##") ?? "not_captured");
        WriteField("Median Holding Duration", report.MedianHoldingDurationMinutes?.ToString("0.##") ?? "not_captured");
        WriteField("Sharpe Ratio", report.SharpeRatio?.ToString("0.####") ?? "not_captured");
        WriteField("Sortino Ratio", report.SortinoRatio?.ToString("0.####") ?? "not_captured");
        WriteField("Signal Density / Month", report.SignalDensityPerMonth?.ToString("0.##") ?? "not_captured");
        WriteField("Signal Density / Week", report.SignalDensityPerWeek?.ToString("0.##") ?? "not_captured");
        WriteField("Average R", report.AverageR?.ToString("0.####") ?? "not_captured");
        WriteField("Expectancy R", report.ExpectancyR?.ToString("0.####") ?? "not_captured");
        WriteField("Max Consecutive Losses", report.MaxConsecutiveLosses?.ToString() ?? "not_captured");
        WriteField("Max Consecutive Wins", report.MaxConsecutiveWins?.ToString() ?? "not_captured");
        WriteField("Drawdown Health", report.DrawdownCertification.Health);
        WriteField("Max Drawdown R", $"{report.DrawdownCertification.MaxDrawdownR:0.####}");
        WriteField("Daily Drawdown R", $"{report.DrawdownCertification.MaxDailyDrawdownR:0.####}");
        WriteField("Weekly Drawdown R", $"{report.DrawdownCertification.MaxWeeklyDrawdownR:0.####}");
        WriteField("Consecutive Losses", report.DrawdownCertification.MaxConsecutiveLosses.ToString());
        WriteField("Recovery Factor", $"{report.DrawdownCertification.RecoveryFactor:0.####}");
        WriteField("Profit Factor", $"{report.DrawdownCertification.ProfitFactor:0.####}");
        WriteField("Trade Distribution", report.TradeDistribution.Health);
        WriteMessages("Sessions", report.SessionValidation.Select(session => $"{session.SessionName}:{session.Status}:net_r={session.NetR:0.####}:trades={session.TradeCount}").ToList());
        WriteMessages("Periods", report.MultiPeriodValidation.Select(period => $"{period.SegmentName}:{period.Status}:net_r={period.NetR:0.####}:pf={period.ProfitFactor:0.####}").ToList());
        WriteMessages("Blockers", report.Blockers);
        WriteField("Human Review Package", DisplayPath(report.HumanReviewPackagePath));
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteScalpingPortfolio(ScalpingCandidatePortfolio portfolio)
    {
        WriteField("Portfolio Status", portfolio.Evaluation.Status);
        WriteField("Certified Candidates", portfolio.Evaluation.CertifiedCandidates.ToString());
        WriteField("Portfolio Members", portfolio.Members.Count.ToString());
        WriteField("Ensemble Candidates", portfolio.Evaluation.EnsembleCandidates.ToString());
        WriteField("Signal Density Score", $"{portfolio.Evaluation.SignalDensityScore:0.####}");
        WriteField("Diversity Score", $"{portfolio.Evaluation.DiversityScore:0.####}");
        WriteField("Drawdown Profile", portfolio.Evaluation.DrawdownProfile);
        WriteField("Next Candidate Search Action", portfolio.Evaluation.NextCandidateSearchAction);
        WriteMessages("Blockers", portfolio.Evaluation.Blockers);
        WriteMessages(
            "Certified Members",
            portfolio.Members
                .Where(member => member.Status == ScalpingCertificationStatus.certified_candidate.ToString())
                .Select(member => $"{member.CandidateId}:{member.Asset}/{member.Timeframe}/{member.SetupType}:diversity={member.DiversityScore:0.####}:density={member.SignalDensityScore:0.####}")
                .ToList());
        WriteField("Portfolio Report", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "portfolio_status.json")));
        WriteField("Ensemble Plan", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "ensemble_plan.json")));
        WriteField("no_auto_trading", portfolio.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", portfolio.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteScalpingMultiAssetRoadmap(ScalpingMultiAssetRoadmap roadmap)
    {
        WriteField("Mode", roadmap.Mode);
        WriteField("Health", roadmap.RoadmapHealth);
        WriteField("Assets", roadmap.Assets.Count.ToString());
        WriteMessages("Next Assets", roadmap.NextAssets);
        WriteMessages("Assets With Data", roadmap.AssetsWithData);
        WriteMessages("Assets Needing Data", roadmap.AssetsNeedingData);
        foreach (var entry in roadmap.Assets.OrderBy(entry => entry.Priority))
        {
            WriteSubHeader(entry.Asset);
            WriteScalpingAssetRoadmapEntry(entry);
        }

        WriteField("Roadmap JSON", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "multi_asset_roadmap.json")));
        WriteField("Roadmap Markdown", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "multi_asset_roadmap.md")));
        WriteField("no_auto_trading", roadmap.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", roadmap.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private static void WriteScalpingAssetRoadmapEntry(ScalpingAssetRoadmapEntry entry)
    {
        WriteField("Asset", entry.Asset);
        WriteMessages("Aliases", entry.Aliases);
        WriteField("Priority", entry.Priority.ToString());
        WriteField("Market Type", entry.MarketType);
        WriteField("Data Available", entry.DataAvailable.ToString().ToLowerInvariant());
        WriteField("Data Gap", entry.DataGap);
        WriteField("Quote Mapping Status", entry.QuoteMappingStatus);
        WriteField("Historical Data Status", entry.HistoricalDataStatus);
        WriteField("Research Status", entry.ResearchStatus);
        WriteField("Signal Agent Spec Status", entry.SignalAgentSpecStatus);
        WriteField("Readiness Status", entry.ReadinessStatus);
        WriteField("Certified Candidates", entry.CertifiedCandidates.ToString());
        WriteField("Next Action", entry.NextAction);
        WriteMessages("Risk Notes", entry.RiskNotes);
    }

    private void WriteScalpingOptimizerReport(ScalpingEnsembleOptimizerReport report)
    {
        var selected = report.SelectedEnsemble;
        WriteField("Mode", report.Mode.ToString());
        WriteField("Optimizer Health", report.OptimizerHealth);
        WriteField("Certified Evaluated", report.CertifiedCandidatesEvaluated.ToString());
        WriteField("Combinations Evaluated", report.CombinationsEvaluated.ToString());
        WriteField("Selected Status", selected.Status.ToString());
        WriteField("Selected Members", selected.Members.Count.ToString());
        WriteField("Previous Drawdown", $"{selected.PreviousPortfolioDrawdown:0.####}");
        WriteField("Optimized Drawdown", $"{selected.OptimizedPortfolioDrawdown:0.####}");
        WriteField("Previous Signal Density", $"{selected.PreviousSignalDensity:0.####}");
        WriteField("Optimized Signal Density", $"{selected.OptimizedSignalDensity:0.####}");
        WriteField("Asset Diversity", $"{selected.AssetDiversityScore:0.####}");
        WriteField("Setup Diversity", $"{selected.SetupDiversityScore:0.####}");
        WriteField("Correlation Penalty", $"{selected.CorrelationPenalty:0.####}");
        WriteField("Risk Of Ruin", $"{selected.RiskOfRuinEstimate:0.####}");
        WriteField("Stability", $"{selected.EnsembleStability:0.####}");
        WriteField("Readiness", selected.Readiness);
        WriteMessages("Blockers", selected.Blockers);
        foreach (var member in selected.Members)
        {
            WriteScalpingOptimizedMember(member);
        }

        WriteField("Optimizer Report", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "optimizer", "ensemble_optimizer_report.json")));
        if (report.Mode == ScalpingEnsembleOptimizationMode.balanced)
        {
            WriteField("Balanced Selection", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "optimizer", "selected_ensemble_balanced.json")));
        }
    }

    private void WriteScalpingEnsembleReviewState(ScalpingEnsembleReviewState state)
    {
        WriteField("Review ID", state.ReviewId);
        WriteField("Package", state.PackageId);
        WriteField("Package Status", state.PackageStatus);
        WriteField("Review Status", state.ReviewStatus.ToString());
        WriteField("Review Mode", state.ReviewMode ?? "-");
        WriteField("Reason", state.Reason ?? "-");
        WriteMessages("Members", state.Members);
        WriteMessages("Blockers", state.Blockers);
        WriteField("Status JSON", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "ensemble_review", "ensemble_review_status.json")));
        WriteField("Status Markdown", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "scalping_portfolio", "ensemble_review", "ensemble_review_status.md")));
        WriteField("Review Log", DisplayPath(state.ReviewLogPath));
        WriteField("no_auto_trading", state.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", state.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", state.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", state.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private static void WriteEnsembleApprovalScope()
    {
        WriteMessages("Approval erlaubt", ["Demo-Signal-Nutzung", "Forward-Test-Vorbereitung", "weitere Review-Schritte"]);
        WriteMessages("Approval erlaubt NICHT", ["Live-Trading", "Broker-Orders", "cTrader Order API", "automatische Ausfuehrung"]);
    }

    private void WriteDemoSignalFeedSnapshot(DemoSignalFeedSnapshot snapshot)
    {
        WriteField("Package", snapshot.PackageId);
        WriteField("Ensemble Review Status", snapshot.EnsembleReviewStatus);
        WriteField("Feed Status", snapshot.FeedStatus);
        WriteField("Feed Mode", snapshot.FeedMode);
        WriteField("Signal Count", snapshot.SignalCount.ToString());
        WriteField("Demo Signals Available", snapshot.DemoSignalsAvailable.ToString().ToLowerInvariant());
        WriteMessages("Assets", snapshot.Assets);
        WriteMessages("Blockers", snapshot.Blockers);
        WriteMessages("Warnings", snapshot.Warnings);
        WriteField("Status JSON", DisplayPath(Path.Combine(BuildStoragePaths().Root, "reports", "demo_signal_feed", "demo_signal_feed_status.json")));
        WriteField("Latest Signals JSON", DisplayPath(snapshot.LatestSignalsJsonPath));
        WriteField("Latest Signals Markdown", DisplayPath(snapshot.LatestSignalsMarkdownPath));
        WriteField("Feed Log", DisplayPath(snapshot.FeedLogPath));
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", snapshot.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", snapshot.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private static void WriteDemoSignalFeedSafety()
    {
        WriteMessages("Demo Feed Safety", ["Demo Signal Feed only", "no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "no cTrader Order API", "no broker orders"]);
    }

    private static void WriteDemoSignal(DemoSignalFeedItem signal)
    {
        WriteSubHeader($"{signal.Asset} {signal.Timeframe} / {signal.CandidateId}");
        WriteField("Signal ID", signal.SignalId);
        WriteField("Created UTC", signal.CreatedUtc.ToString("O"));
        WriteField("Expires UTC", signal.ExpiresUtc?.ToString("O") ?? "n/a");
        WriteField("Setup", signal.SetupType);
        WriteField("Direction", signal.Direction);
        WriteField("Entry", $"{signal.EntryLevel:0.#####}");
        WriteField("Entry Zone Lower", signal.EntryZoneLower.HasValue ? $"{signal.EntryZoneLower.Value:0.#####}" : "n/a");
        WriteField("Entry Zone Upper", signal.EntryZoneUpper.HasValue ? $"{signal.EntryZoneUpper.Value:0.#####}" : "n/a");
        WriteField("Stop Loss", $"{signal.StopLoss:0.#####}");
        WriteField("Take Profit", $"{signal.TakeProfit:0.#####}");
        WriteField("Invalidation", $"{signal.InvalidationLevel:0.#####}");
        WriteField("Confidence", $"{signal.Confidence:0.####}");
        WriteField("Status", signal.Status);
        WriteField("Reason", signal.Reason);
        WriteMessages("Risk Notes", signal.RiskNotes);
    }

    private void WriteEnsembleSignalSpecValidationResult(EnsembleSignalSpecValidationResult result)
    {
        WriteField("Package", result.PackageId);
        WriteField("Members Total", result.MembersTotal.ToString());
        WriteField("Specs Present", result.SpecsPresent.ToString());
        WriteField("Specs Exported", result.SpecsExported.ToString());
        WriteMessages("Exported Candidates", result.ExportedCandidates);
        WriteMessages("Missing Specs", result.MissingSpecs);
        WriteMessages("Blockers", result.Blockers);
        var root = Path.Combine(BuildStoragePaths().Root, "reports", "signal_agent_specs");
        WriteField("Signal Spec Root", DisplayPath(root));
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", result.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", result.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private void WriteForwardTestStatus(ForwardTestStatusSnapshot status)
    {
        WriteField("Package", status.PackageId);
        WriteField("Forward Test Status", status.ForwardTestStatus);
        WriteField("Mode", status.ForwardTestMode);
        WriteField("Assets", status.ForwardTestAssets.Count == 0 ? "-" : string.Join(", ", status.ForwardTestAssets));
        WriteField("Signals Observed", status.ForwardTestSignalsObserved.ToString());
        WriteField("Observations Total", status.ForwardTestObservationsTotal.ToString());
        WriteField("Triggered Count", status.ForwardTestTriggeredCount.ToString());
        WriteField("Invalidated Count", status.ForwardTestInvalidatedCount.ToString());
        WriteField("Simulated Observation Count", status.ForwardTestSimulatedObservationCount.ToString());
        WriteField("Latest Observation UTC", status.ForwardTestLatestObservationUtc?.ToString("O") ?? "-");
        WriteField("Using Current Market Snapshot", status.UsingCurrentMarketSnapshot.ToString().ToLowerInvariant());
        WriteField("Health", status.ForwardTestHealth);
        WriteField("Requires Human Review", status.ForwardTestRequiresHumanReview.ToString().ToLowerInvariant());
        WriteMessages("Blockers", status.Blockers);
        WriteMessages("Warnings", status.Warnings);
        WriteField("Plan", DisplayOptionalPath(status.PlanPath));
        WriteField("Log", DisplayOptionalPath(status.LogPath));
        WriteField("Latest Observations JSON", DisplayOptionalPath(status.LatestObservationsJsonPath));
        WriteField("Latest Observations Markdown", DisplayOptionalPath(status.LatestObservationsMarkdownPath));
        WriteField("signals_generated", status.Metrics.SignalsGenerated.ToString());
        WriteField("signals_observed", status.Metrics.SignalsObserved.ToString());
        WriteField("observations_total", status.Metrics.ObservationsTotal.ToString());
        WriteField("triggered_count", status.Metrics.TriggeredCount.ToString());
        WriteField("invalidated_count", status.Metrics.InvalidatedCount.ToString());
        WriteField("expired_count", status.Metrics.ExpiredCount.ToString());
        WriteField("hypothetical_wins", status.Metrics.HypotheticalWins.ToString());
        WriteField("hypothetical_losses", status.Metrics.HypotheticalLosses.ToString());
        WriteField("manual_review_count", status.Metrics.ManualReviewCount.ToString());
        WriteField("simulated_observation_count", status.Metrics.SimulatedObservationCount.ToString());
        WriteField("win_rate", $"{status.Metrics.WinRate:0.####}");
        WriteField("average_r", $"{status.Metrics.AverageR:0.####}");
        WriteField("max_drawdown_r", $"{status.Metrics.MaxDrawdownR:0.####}");
        WriteField("max_daily_drawdown_r", $"{status.Metrics.MaxDailyDrawdownR:0.####}");
        WriteMessages("slippage_notes", status.Metrics.SlippageNotes);
        WriteMessages("spread_notes", status.Metrics.SpreadNotes);
        WriteMessages("missed_signal_notes", status.Metrics.MissedSignalNotes);
        WriteMessages("manual_review_notes", status.Metrics.ManualReviewNotes);
        WriteField("no_auto_trading", status.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", status.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", status.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", status.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private void WriteSignalWatchStatus(SignalWatchStatusSnapshot snapshot)
    {
        WriteField("Watch Status", snapshot.WatchStatus);
        WriteField("Signals Evaluated", snapshot.SignalsEvaluated.ToString());
        WriteField("waiting_for_trigger_count", snapshot.WaitingForTriggerCount.ToString());
        WriteField("watching_count", snapshot.WatchingCount.ToString());
        WriteField("armed_count", snapshot.ArmedCount.ToString());
        WriteField("triggered_count", snapshot.TriggeredCount.ToString());
        WriteField("active_count", snapshot.ActiveCount.ToString());
        WriteField("near_miss_count", snapshot.NearMissCount.ToString());
        WriteField("invalidated_count", snapshot.InvalidatedCount.ToString());
        WriteField("expired_count", snapshot.ExpiredCount.ToString());
        WriteField("completed_count", snapshot.CompletedCount.ToString());
        WriteField("no_signal_count", snapshot.NoSignalCount.ToString());
        WriteField("Using Current Market Snapshot", snapshot.UsingCurrentMarketSnapshot.ToString().ToLowerInvariant());
        WriteMessages("Warnings", snapshot.Warnings);
        WriteField("Latest Signal Watch JSON", DisplayPath(snapshot.LatestEvaluationsJsonPath));
        WriteField("Latest Signal Watch Markdown", DisplayPath(snapshot.LatestEvaluationsMarkdownPath));
        WriteField("Signal Watch Log", DisplayPath(snapshot.LogPath));
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", snapshot.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", snapshot.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private static void WriteSignalWatchEvaluation(SignalWatchEvaluation evaluation)
    {
        WriteSubHeader($"{evaluation.Asset} {evaluation.Timeframe} / {evaluation.CandidateId}");
        WriteField("Signal ID", evaluation.SignalId);
        WriteField("Lifecycle Status", evaluation.SignalLifecycleStatus);
        WriteField("Observed Price", evaluation.ObservedPrice.HasValue ? $"{evaluation.ObservedPrice.Value:0.#####}" : "n/a");
        WriteField("Observed High", evaluation.ObservedHigh.HasValue ? $"{evaluation.ObservedHigh.Value:0.#####}" : "n/a");
        WriteField("Observed Low", evaluation.ObservedLow.HasValue ? $"{evaluation.ObservedLow.Value:0.#####}" : "n/a");
        WriteField("Entry Zone Lower", $"{evaluation.EntryZoneLower:0.#####}");
        WriteField("Entry Zone Upper", $"{evaluation.EntryZoneUpper:0.#####}");
        WriteField("Observed Entry Hit", evaluation.ObservedEntryHit.ToString().ToLowerInvariant());
        WriteField("Observed Invalidation Hit", evaluation.ObservedInvalidationHit.ToString().ToLowerInvariant());
        WriteField("Observed Stop Loss Hit", evaluation.ObservedStopLossHit.ToString().ToLowerInvariant());
        WriteField("Observed Take Profit Hit", evaluation.ObservedTakeProfitHit.ToString().ToLowerInvariant());
        WriteField("Observed Near Miss", evaluation.ObservedNearMiss.ToString().ToLowerInvariant());
        WriteField("Observed Expired", evaluation.ObservedExpired.ToString().ToLowerInvariant());
        WriteField("Outcome Pending", evaluation.OutcomePending.ToString().ToLowerInvariant());
        WriteField("Hypothetical Result", evaluation.HypotheticalResult);
        WriteField("r_multiple", evaluation.RMultiple.HasValue ? $"{evaluation.RMultiple.Value:0.####}" : "n/a");
        WriteField("requires_human_review", evaluation.RequiresHumanReview.ToString().ToLowerInvariant());
        WriteField("market_data_source", evaluation.MarketDataSource);
        WriteField("Note", evaluation.Note);
        WriteMessages("Warnings", evaluation.Warnings);
    }

    private static void WriteForwardTestObservation(ForwardTestObservation observation)
    {
        WriteSubHeader($"{observation.Asset} / {observation.CandidateId}");
        WriteField("Observation ID", observation.ObservationId);
        WriteField("Created UTC", observation.CreatedUtc.ToString("O"));
        WriteField("Signal ID", observation.SignalId);
        WriteField("Signal Lifecycle Status", observation.SignalLifecycleStatus);
        WriteField("Observed Status", observation.ObservedStatus);
        WriteField("Observed Price", observation.ObservedPrice.HasValue ? $"{observation.ObservedPrice.Value:0.#####}" : "n/a");
        WriteField("Observed High", observation.ObservedHigh.HasValue ? $"{observation.ObservedHigh.Value:0.#####}" : "n/a");
        WriteField("Observed Low", observation.ObservedLow.HasValue ? $"{observation.ObservedLow.Value:0.#####}" : "n/a");
        WriteField("Entry", $"{observation.EntryLevel:0.#####}");
        WriteField("Entry Zone Lower", $"{observation.EntryZoneLower:0.#####}");
        WriteField("Entry Zone Upper", $"{observation.EntryZoneUpper:0.#####}");
        WriteField("Stop Loss", $"{observation.StopLoss:0.#####}");
        WriteField("Take Profit", $"{observation.TakeProfit:0.#####}");
        WriteField("Invalidation", $"{observation.InvalidationLevel:0.#####}");
        WriteField("Observed Entry Hit", observation.ObservedEntryHit.ToString().ToLowerInvariant());
        WriteField("Observed Invalidation Hit", observation.ObservedInvalidationHit.ToString().ToLowerInvariant());
        WriteField("Observed Stop Loss Hit", observation.ObservedStopLossHit.ToString().ToLowerInvariant());
        WriteField("Observed Take Profit Hit", observation.ObservedTakeProfitHit.ToString().ToLowerInvariant());
        WriteField("Observed Near Miss", observation.ObservedNearMiss.ToString().ToLowerInvariant());
        WriteField("Observed Expired", observation.ObservedExpired.ToString().ToLowerInvariant());
        WriteField("Outcome Pending", observation.OutcomePending.ToString().ToLowerInvariant());
        WriteField("Hypothetical Result", observation.HypotheticalResult);
        WriteField("r_multiple", observation.RMultiple.HasValue ? $"{observation.RMultiple.Value:0.####}" : "n/a");
        WriteField("requires_human_review", observation.RequiresHumanReview.ToString().ToLowerInvariant());
        WriteField("Result", observation.Result);
        WriteField("Note", observation.Note);
        WriteField("Simulated", observation.Simulated.ToString().ToLowerInvariant());
        WriteField("human_review_required", observation.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", observation.NoAutoTrading.ToString().ToLowerInvariant());
    }

    private static void WriteForwardTestSafety()
    {
        WriteMessages("Forward Test ist", ["Observation only", "Demo signal tracking", "No orders", "No broker action", "No cTrader Order API"]);
        WriteField("no_auto_trading", "true");
        WriteField("human_review_required", "true");
    }

    private void WriteCurrentMarketStatus(CurrentMarketStatusSnapshot status)
    {
        WriteField("Snapshot Status", status.SnapshotStatus);
        WriteField("Snapshot Health", status.SnapshotHealth);
        WriteField("Assets Requested", string.Join(", ", status.AssetsRequested));
        WriteField("Assets Available", status.AssetsAvailable.Count == 0 ? "none" : string.Join(", ", status.AssetsAvailable));
        WriteField("Latest Update UTC", status.LatestUpdateUtc?.ToString("O") ?? "n/a");
        WriteMessages("Warnings", status.Warnings);
        WriteField("Snapshot JSON", DisplayOptionalPath(status.SnapshotJsonPath));
        WriteField("Snapshot Markdown", DisplayOptionalPath(status.SnapshotMarkdownPath));
        WriteField("no_auto_trading", status.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", status.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", status.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", status.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private static void WriteCurrentMarketAssetSnapshot(CurrentMarketAssetSnapshot snapshot)
    {
        WriteSubHeader(snapshot.Asset);
        WriteField("Status", snapshot.Status);
        WriteField("Source", snapshot.Source);
        WriteField("Bid", snapshot.Bid.HasValue ? $"{snapshot.Bid.Value:0.#####}" : "n/a");
        WriteField("Ask", snapshot.Ask.HasValue ? $"{snapshot.Ask.Value:0.#####}" : "n/a");
        WriteField("Mid", snapshot.Mid.HasValue ? $"{snapshot.Mid.Value:0.#####}" : "n/a");
        WriteField("Spread", snapshot.Spread.HasValue ? $"{snapshot.Spread.Value:0.#####}" : "n/a");
        WriteField("Timestamp UTC", snapshot.TimestampUtc?.ToString("O") ?? "n/a");
        WriteField("Age Seconds", snapshot.AgeSeconds.HasValue ? $"{snapshot.AgeSeconds.Value:0.##}" : "n/a");
        WriteField("is_live_readonly", snapshot.IsLiveReadonly.ToString().ToLowerInvariant());
        WriteField("is_placeholder", snapshot.IsPlaceholder.ToString().ToLowerInvariant());
    }

    private static void WriteCurrentMarketSafety()
    {
        WriteMessages("Current Market Snapshot", ["Read-only market snapshot", "No orders", "No broker action", "No cTrader Order API for trading"]);
        WriteField("no_auto_trading", "true");
        WriteField("human_review_required", "true");
    }

    private void WriteQuoteSnapshotStatus(CTraderReadOnlyQuoteSnapshot snapshot)
    {
        WriteField("Quote Snapshot Status", snapshot.QuoteSnapshotStatus);
        WriteField("Assets Requested", string.Join(", ", snapshot.AssetsRequested));
        WriteField("Assets Available", snapshot.AssetsAvailable.Count == 0 ? "none" : string.Join(", ", snapshot.AssetsAvailable));
        WriteMessages("Warnings", snapshot.Warnings);
        WriteField("Quotes JSON", DisplayOptionalPath(snapshot.QuotesJsonPath));
        WriteField("Quotes Markdown", DisplayOptionalPath(snapshot.QuotesMarkdownPath));
        WriteField("Quote Log", DisplayOptionalPath(snapshot.QuoteLogPath));
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", snapshot.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", snapshot.LiveTradingEnabled.ToString().ToLowerInvariant());
        WriteField("research_only", "true");
    }

    private static void WriteQuoteSnapshot(CTraderReadOnlyQuote quote)
    {
        WriteSubHeader(quote.Asset);
        WriteField("Status", quote.Status);
        WriteField("Bid", quote.Bid.HasValue ? $"{quote.Bid.Value:0.#####}" : "n/a");
        WriteField("Ask", quote.Ask.HasValue ? $"{quote.Ask.Value:0.#####}" : "n/a");
        WriteField("Mid", quote.Mid.HasValue ? $"{quote.Mid.Value:0.#####}" : "n/a");
        WriteField("Spread", quote.Spread.HasValue ? $"{quote.Spread.Value:0.#####}" : "n/a");
        WriteField("Timestamp UTC", quote.TimestampUtc?.ToString("O") ?? "n/a");
        WriteField("Source", quote.Source);
        WriteField("is_live_readonly", quote.IsLiveReadonly.ToString().ToLowerInvariant());
        WriteField("is_placeholder", quote.IsPlaceholder.ToString().ToLowerInvariant());
        WriteField("Age Seconds", quote.AgeSeconds.HasValue ? $"{quote.AgeSeconds.Value:0.##}" : "n/a");
    }

    private static void WriteScalpingOptimizedMember(ScalpingOptimizedEnsembleMember member)
    {
        WriteSubHeader(member.CandidateId);
        WriteField("Asset", member.Asset);
        WriteField("Setup", member.SetupType);
        WriteField("Confidence", $"{member.Confidence:0.####}");
        WriteField("Profit Factor", $"{member.ProfitFactor:0.####}");
        WriteField("Recovery Factor", $"{member.RecoveryFactor:0.####}");
        WriteField("Drawdown", $"{member.Drawdown:0.####}");
        WriteField("Max Daily Drawdown", $"{member.MaxDailyDrawdown:0.####}");
        WriteField("Max Weekly Drawdown", $"{member.MaxWeeklyDrawdown:0.####}");
        WriteField("Signal Density", $"{member.SignalDensityScore:0.####}");
        WriteField("Contribution", member.ContributionReason);
        WriteMessages("Risk Notes", member.RiskNotes);
    }

    private void WriteScalpingEnsembleExportResult(ScalpingEnsembleExportResult result)
    {
        WriteField("Package", result.PackageId);
        WriteField("Status", result.Status);
        WriteField("Signal Agent JSON", DisplayPath(result.SignalAgentJsonPath));
        WriteField("Signal Agent Markdown", DisplayPath(result.SignalAgentMarkdownPath));
        WriteField("Bot Portfolio JSON", DisplayPath(result.BotPortfolioJsonPath));
        WriteField("Bot Portfolio Markdown", DisplayPath(result.BotPortfolioMarkdownPath));
        WriteField("Human Review Package", DisplayPath(result.HumanReviewPackagePath));
        WriteField("Manifest", DisplayPath(result.ManifestPath));
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", result.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", result.LiveTradingEnabled.ToString().ToLowerInvariant());
    }

    private void WriteRiskOfRuinEntry(RiskOfRuinEntry entry)
    {
        WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternId ?? "-"} / {entry.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{entry.Symbol}/{entry.Timeframe}");
        WriteField("Expected Drawdown", $"{entry.ExpectedDrawdown:0.####}%");
        WriteField("Losing Streak Risk", $"{entry.LosingStreakRisk:0.####}");
        WriteField("Ruin Probability", $"{entry.AccountRuinProbabilityEstimate:0.####}");
        WriteField("Recommended Risk", $"{entry.RecommendedMaxRiskPerTrade:0.####}%");
        WriteField("risk_of_ruin_passed", entry.RiskOfRuinPassed.ToString().ToLowerInvariant());
    }

    private void WriteResearchInsights(StrategyEvolutionSummary insights)
    {
        WriteField("Generated UTC", insights.GeneratedAtUtc.ToString("O"));
        WriteField("Top Strategies", insights.TopStrategies.Count.ToString());
        WriteField("Weak Strategies", insights.WeakStrategies.Count.ToString());
        WriteField("Clusters", insights.Clusters.Count.ToString());
        WriteField("Best Symbols", insights.BestSymbols.Count == 0 ? "-" : string.Join(", ", insights.BestSymbols));
        WriteField("Best Timeframes", insights.BestTimeframes.Count == 0 ? "-" : string.Join(", ", insights.BestTimeframes));
        WriteMessages("Stability Metrics", insights.StabilityMetrics);
        WriteMessages("Fitness Trends", insights.FitnessTrends.TakeLast(5).ToList());
        WriteMessages("Exploration Coverage", insights.ExplorationCoverage);
        WriteMessages("Strategy Rankings", insights.StrategyRankings);
        WriteMessages("Best Patterns", insights.BestPatterns ?? Array.Empty<string>());
        WriteMessages("Weak Patterns", insights.WeakPatterns ?? Array.Empty<string>());
        WriteMessages("Trading.de Best Patterns", insights.BestTradingDePatterns ?? Array.Empty<string>());
        WriteMessages("Source Performance", insights.SourcePerformance ?? Array.Empty<string>());
        WriteMessages("Robust Strategies", insights.RobustStrategies ?? Array.Empty<string>());
        WriteMessages("Overfit Suspected", insights.OverfitSuspectedStrategies ?? Array.Empty<string>());
        WriteMessages("High Risk Strategies", insights.HighRiskStrategies ?? Array.Empty<string>());
        WriteMessages("Stable Symbol/Timeframe", insights.StableSymbolTimeframeCombinations ?? Array.Empty<string>());
        WriteMessages("Best Regimes", insights.BestRegimes ?? Array.Empty<string>());
        WriteMessages("Weak Regimes", insights.WeakRegimes ?? Array.Empty<string>());
        WriteMessages("Preferred Sessions", insights.PreferredSessions ?? Array.Empty<string>());
        WriteMessages("Avoid Sessions", insights.AvoidSessions ?? Array.Empty<string>());
        WriteMessages("Volatility Preference", insights.VolatilityPreference ?? Array.Empty<string>());
        WriteField("Regime Consistency", insights.RegimeConsistencyScore is null ? "-" : $"{insights.RegimeConsistencyScore:0.####}");
        WriteMessages("Preferred Regimes", insights.PreferredRegimes ?? Array.Empty<string>());
        WriteMessages("Avoided Regimes", insights.AvoidedRegimes ?? Array.Empty<string>());
        WriteMessages("Too Good To Be True", insights.TooGoodToBeTrueStrategies ?? Array.Empty<string>());
        WriteMessages("Cost Sensitive", insights.CostSensitiveStrategies ?? Array.Empty<string>());
        WriteMessages("Cost Sensitivity Summary", insights.CostSensitivitySummary ?? Array.Empty<string>());
        WriteMessages("Robust Gate Summary", insights.RobustGateSummary ?? Array.Empty<string>());
        WriteField("bot_candidates", insights.BotCandidateCount?.ToString() ?? "-");
        WriteField("rejected_candidates", insights.RejectedCandidateCount?.ToString() ?? "-");
        WriteMessages("Top Demo Bot Candidates", insights.TopDemoBotCandidates ?? Array.Empty<string>());
        WriteMessages("Next Validation Recommendations", insights.NextValidationRecommendations ?? Array.Empty<string>());
        WriteMessages("Monte-Carlo Summary", insights.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", insights.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", insights.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteField("blocked_by_monte_carlo", insights.CandidatesBlockedByMonteCarlo?.ToString() ?? "-");
        WriteField("blocked_by_cost_stress", insights.CandidatesBlockedByCostStress?.ToString() ?? "-");
        WriteField("blocked_by_risk", insights.CandidatesBlockedByRisk?.ToString() ?? "-");
        WriteMessages("Why No Candidates", insights.WhyNoCandidates ?? Array.Empty<string>());
        WriteMessages("Top Blockers", insights.TopBlockers ?? Array.Empty<string>());
        WriteField("near_miss_count", insights.NearMissCount?.ToString() ?? "-");
        WriteMessages("Recommended Next Experiments", insights.RecommendedNextExperiments ?? Array.Empty<string>());
        WriteMessages("Avoid Combinations", insights.AvoidCombinations ?? Array.Empty<string>());
        WriteMessages("Next Recommended Tests", insights.NextRecommendedTests ?? Array.Empty<string>());
        WriteMessages("Parameter Statistics", insights.ParameterStatistics);
        WriteMessages("Timeframe Comparisons", insights.TimeframeComparisons);
        WriteField("no_auto_trading", insights.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", insights.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteRegimeSummary(RegimeSummaryReport report)
    {
        WriteField("Generated UTC", report.GeneratedAtUtc.ToString("O"));
        WriteField("Source Features", DisplayPath(report.SourceFeatureFile));
        WriteField("Features Analyzed", report.FeaturesAnalyzed.ToString());
        WriteField("Snapshots", report.SnapshotCount.ToString());
        WriteMessages("Symbols", report.Symbols);
        WriteMessages("Timeframes", report.Timeframes);
        WriteMessages("Dominant Regimes", report.DominantRegimes);
        WriteMessages("Dominant Sessions", report.DominantSessions);
        foreach (var snapshot in report.TopSnapshots.Take(8))
        {
            WriteSubHeader($"{snapshot.Symbol} {snapshot.Timeframe} / {snapshot.RegimeType} / {snapshot.Session}");
            WriteField("Candles", snapshot.CandleCount.ToString());
            WriteField("Range Ratio", $"{snapshot.AverageRangeRatio:0.########}");
            WriteField("Body Ratio", $"{snapshot.AverageBodyRatio:0.####}");
            WriteField("Trend Slope", $"{snapshot.TrendSlope:0.########}");
            WriteField("Breakout Frequency", $"{snapshot.BreakoutFrequency:0.####}");
            WriteField("Confidence", $"{snapshot.Confidence:0.####}");
        }

        WriteMessages("Warnings", report.Warnings);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteStrategyRegimeEntry(StrategyRegimePerformanceEntry entry)
    {
        WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternName} / {entry.RegimeType} / {entry.Session}");
        WriteField("Pattern ID", entry.PatternId);
        WriteField("Variants", entry.VariantCount.ToString());
        WriteField("Trades", entry.TotalTrades.ToString());
        WriteField("Avg Fitness", $"{entry.AverageFitness:0.####}");
        WriteField("Avg Winrate", $"{entry.AverageWinrate:P2}");
        WriteField("Regime Confidence", $"{entry.AverageRegimeConfidence:0.####}");
        WriteField("Regime Fit", $"{entry.RegimeFitScore:0.####}");
        WriteField("Status", entry.Status);
    }

    private void WriteWalkForwardSummary(WalkForwardValidationReport report)
    {
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Train Range", $"{report.TrainFromUtc:yyyy-MM-dd} -> {report.TrainToUtc:yyyy-MM-dd}");
        WriteField("Validation Range", $"{report.ValidationFromUtc:yyyy-MM-dd} -> {report.ValidationToUtc:yyyy-MM-dd}");
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Robust Strategies", report.RobustStrategies.ToString());
        WriteField("Overfit Suspected", report.OverfitSuspectedStrategies.ToString());
        WriteField("High Risk Strategies", report.HighRiskStrategies.ToString());
        WriteField("OOS Available", report.Assessments.Count(item => item.OosAvailable).ToString());
        WriteField("Too Good To Be True", report.Assessments.Count(item => item.TooGoodToBeTrue).ToString());
        WriteField("Avg WalkForward Confidence", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.WalkForwardConfidence)):0.####}");
        WriteField("Avg Cost Sensitivity", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.CostSensitivity)):0.####}");
        WriteField("Avg Regime Consistency", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.RegimeConsistencyScore)):0.####}");
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private NightlyResearchState WithCognitiveState(
        NightlyResearchState state,
        bool cognitiveJobsEnabled,
        NightlyCognitiveSummary? summary,
        string summaryPath) =>
        state with
        {
            CognitiveJobsEnabled = cognitiveJobsEnabled,
            LastKnowledgeScanUtc = summary?.LastKnowledgeScanUtc,
            LastQueueProcessedUtc = summary?.LastQueueProcessedUtc,
            LastCognitiveInsightsUtc = summary?.LastCognitiveInsightsUtc,
            QueuedResearchItems = summary?.QueuedResearchItems,
            ActiveDomains = summary?.ActiveDomains ?? ["trading"],
            LastCognitiveError = summary?.LastError,
            LastCognitiveSummaryPath = summaryPath
        };

    private void WriteCognitiveOperationalStatus(StoragePaths storagePaths, SchedulerStatus? schedulerStatus = null)
    {
        schedulerStatus ??= new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetStatus();
        var summaryService = new CognitiveNightlyService(storagePaths);
        var summary = summaryService.LoadSummary();
        var sources = new KnowledgeSourceRegistry(storagePaths).LoadSources();
        var insights = new HypothesisGenerator(storagePaths).LoadInsights();
        CognitiveStatus cognitiveStatus;
        try
        {
            cognitiveStatus = new CognitiveCoreService(storagePaths).BuildStatus();
        }
        catch (IOException)
        {
            cognitiveStatus = new CognitiveStatus(
                StatusVersion: "cognitive_status_unknown",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Domains: [],
                SourceCount: 0,
                KnowledgeItemCount: 0,
                QueueItemCount: 0,
                InsightCount: 0,
                MemoryEntryCount: 0,
                ActiveDomains: [],
                NextActions: [],
                CognitiveRoot: storagePaths.Root,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }
        var queuedResearchItems = summary?.QueuedResearchItems;

        if (queuedResearchItems is null)
        {
            var queue = new ResearchQueueService(storagePaths).LoadOrCreateQueue();
            queuedResearchItems = queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
        }

        var lastKnowledgeScan = summary?.LastKnowledgeScanUtc
            ?? sources.OrderByDescending(source => source.LastCheckedUtc).FirstOrDefault()?.LastCheckedUtc;
        var lastCognitiveInsights = summary?.LastCognitiveInsightsUtc
            ?? insights.OrderByDescending(insight => insight.CreatedAtUtc).FirstOrDefault()?.CreatedAtUtc;

        WriteSubHeader("Cognitive Core");
        WriteField("cognitive_jobs_enabled", AreCognitiveJobsEnabled(schedulerStatus).ToString().ToLowerInvariant());
        WriteField("last_knowledge_scan", lastKnowledgeScan?.ToString("O") ?? "-");
        WriteField("last_queue_processed", summary?.LastQueueProcessedUtc?.ToString("O") ?? "-");
        WriteField("last_cognitive_insights", lastCognitiveInsights?.ToString("O") ?? "-");
        WriteField("queued_research_items", queuedResearchItems?.ToString() ?? "-");
        var activeDomains = (summary?.ActiveDomains ?? [])
            .Concat(cognitiveStatus.ActiveDomains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        WriteField("active_domains", string.Join(", ", activeDomains));
        WriteField("last_cognitive_error", string.IsNullOrWhiteSpace(summary?.LastError) ? "-" : summary.LastError);
        WriteField("cognitive_summary", File.Exists(summaryService.SummaryPath) ? DisplayPath(summaryService.SummaryPath) : "-");
    }

    private static bool AreCognitiveJobsEnabled(SchedulerStatus schedulerStatus)
    {
        string[] required =
        [
            "scan_knowledge_sources",
            "process_research_queue",
            "generate_cognitive_insights",
            "evaluate_knowledge_quality",
            "consolidate_memory"
        ];

        return required.All(jobType => schedulerStatus.Jobs.Any(job =>
            job.Enabled
            && job.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase)));
    }

    private void WriteNightlyState(NightlyResearchState state)
    {
        var currentlyRunning = state.CurrentlyRunning && IsProcessAlive(state.ProcessId);
        var duration = currentlyRunning && state.LastStartUtc is not null
            ? Math.Round((DateTimeOffset.UtcNow - state.LastStartUtc.Value).TotalMinutes, 2)
            : state.RuntimeDurationMinutes;

        WriteField("Status", state.Status);
        WriteField("Run ID", string.IsNullOrWhiteSpace(state.RunId) ? "-" : state.RunId);
        WriteField("Next Scheduled Start", state.NextScheduledStartUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Start", state.LastStartUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Stop", state.LastStopUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Currently Running", currentlyRunning.ToString().ToLowerInvariant());
        WriteField("Process ID", state.ProcessId?.ToString() ?? "-");
        WriteField("Runtime Duration", $"{duration:0.##} min");
        WriteField("Started UTC", state.StartedAtUtc?.ToString("O") ?? "-");
        WriteField("Deadline UTC", state.DeadlineUtc?.ToString("O") ?? "-");
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Idle Iterations", state.IdleIterations.ToString());
        WriteField("Work Performed", state.WorkPerformed.ToString());
        WriteField("Next Action", state.NextAction);
        WriteField("Last Checkpoint", DisplayOptionalPath(state.LastCheckpointPath));
        WriteField("Stop Requested UTC", state.StopRequestedAtUtc?.ToString("O") ?? "-");
        WriteField("Last Error", state.LastError ?? "-");
        WriteField("cognitive_jobs_enabled", state.CognitiveJobsEnabled?.ToString().ToLowerInvariant() ?? "-");
        WriteField("last_knowledge_scan", state.LastKnowledgeScanUtc?.ToString("O") ?? "-");
        WriteField("last_queue_processed", state.LastQueueProcessedUtc?.ToString("O") ?? "-");
        WriteField("last_cognitive_insights", state.LastCognitiveInsightsUtc?.ToString("O") ?? "-");
        WriteField("queued_research_items", state.QueuedResearchItems?.ToString() ?? "-");
        WriteField("active_domains", state.ActiveDomains is { Count: > 0 } ? string.Join(", ", state.ActiveDomains) : "-");
        WriteField("last_cognitive_error", string.IsNullOrWhiteSpace(state.LastCognitiveError) ? "-" : state.LastCognitiveError);
        WriteField("cognitive_summary", DisplayOptionalPath(state.LastCognitiveSummaryPath));
        WriteField("no_auto_trading", state.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", state.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteSupervisorState(HermesSupervisorState state)
    {
        var currentlyRunning = state.CurrentlyRunning && IsProcessAlive(state.ProcessId);
        var duration = currentlyRunning && state.StartedAtUtc is not null
            ? Math.Round((DateTimeOffset.UtcNow - state.StartedAtUtc.Value).TotalMinutes, 2)
            : state.StartedAtUtc is not null && state.StoppedAtUtc is not null
                ? Math.Round((state.StoppedAtUtc.Value - state.StartedAtUtc.Value).TotalMinutes, 2)
                : 0;

        WriteField("Status", state.Status);
        WriteField("Supervisor ID", string.IsNullOrWhiteSpace(state.SupervisorId) ? "-" : state.SupervisorId);
        WriteField("Currently Running", currentlyRunning.ToString().ToLowerInvariant());
        WriteField("Process ID", state.ProcessId?.ToString() ?? "-");
        WriteField("Started UTC", state.StartedAtUtc?.ToString("O") ?? "-");
        WriteField("Deadline UTC", state.DeadlineUtc?.ToString("O") ?? "-");
        WriteField("Stopped UTC", state.StoppedAtUtc?.ToString("O") ?? "-");
        WriteField("Runtime Duration", $"{duration:0.##} min");
        WriteField("Heartbeat UTC", state.HeartbeatUtc?.ToString("O") ?? "-");
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Jobs Started", state.JobsStarted.ToString());
        WriteField("Jobs Completed", state.JobsCompleted.ToString());
        WriteField("Jobs Skipped", state.JobsSkipped.ToString());
        WriteField("Current Job", state.CurrentJobId ?? "-");
        WriteField("Last Job", state.LastJobId ?? "-");
        WriteField("Next Action", state.NextAction);
        WriteField("Stop Requested UTC", state.StopRequestedAtUtc?.ToString("O") ?? "-");
        WriteField("Last Error", state.LastError ?? "-");
        WriteField("no_auto_trading", state.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", state.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteSupervisorProcessStatus(SupervisorProcessStatus status)
    {
        WriteField("Running", status.Running.ToString().ToLowerInvariant());
        WriteField("PID", status.Pid?.ToString() ?? "-");
        WriteField("PID File", DisplayPath(status.PidPath));
        WriteField("Stale PID", status.StalePid.ToString().ToLowerInvariant());
        WriteField("Started At", status.StartedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Heartbeat Age", status.HeartbeatAgeSeconds is null ? "-" : $"{status.HeartbeatAgeSeconds:0.#} s");
        WriteField("Log", DisplayPath(status.LogPath));
        WriteField("Process Warning", status.Warning ?? "-");
    }

    private void WriteSchedulerJob(ScheduledJobState job)
    {
        WriteSubHeader($"{job.JobId} / {job.JobType}");
        WriteField("Enabled", job.Enabled.ToString().ToLowerInvariant());
        WriteField("Status", job.Status);
        WriteField("Next Run", job.NextRunUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Run", job.LastRunUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Completed", job.LastCompletedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Currently Running", job.CurrentlyRunning.ToString().ToLowerInvariant());
        WriteField("Run Count", job.RunCount.ToString());
        WriteField("Failures", job.FailureCount.ToString());
        WriteField("Last Action", job.LastAction ?? "-");
        WriteField("Last Report", DisplayOptionalPath(job.LastReportPath));
        WriteField("Skipped Reason", job.LastSkippedReason ?? "-");
        WriteField("Last Error", job.LastError ?? "-");
        WriteMessages("Warnings", job.Warnings);
    }

    private void WriteKnowledgeCatalogItem(KnowledgeCatalogItem item)
    {
        WriteSubHeader($"{item.Title} / {item.Id}");
        WriteField("Domain", item.Domain);
        WriteField("Confidence", $"{item.Confidence:0.####}");
        WriteField("Validation", item.ValidationStatus);
        WriteField("Last Validated", item.LastValidatedUtc?.ToString("O") ?? "-");
        WriteMessages("Sources", item.SourceIds);
        WriteMessages("Tags", item.Tags);
        WriteMessages("Related", item.RelatedItems.Take(8).ToList());
        WriteField("Summary", item.DescriptionShort);
    }

    private void WriteKnowledgeQualityReport(KnowledgeQualityReport report, string reportPath)
    {
        WriteField("Knowledge Quality", DisplayPath(reportPath));
        WriteField("Knowledge Evidence", DisplayPath(report.EvidencePath));
        WriteField("Total Knowledge", report.TotalKnowledgeItems.ToString());
        WriteField("Trusted Knowledge", report.TrustedKnowledge.ToString());
        WriteField("Weak Knowledge", report.WeakKnowledge.ToString());
        WriteField("Deprecated Knowledge", report.DeprecatedKnowledge.ToString());
        WriteField("Average Quality Score", $"{report.AverageQualityScore:0.####}");
        WriteField("Average Trust Score", $"{report.AverageTrustScore:0.####}");
        WriteField("Knowledge Health", report.KnowledgeHealth);
        WriteField("Knowledge Trend", report.KnowledgeTrend);
        WriteField("Evidence Coverage", $"{report.EvidenceCoverage:0.####}");
        WriteField("Validation Coverage", $"{report.ValidationCoverage:0.####}");
        WriteField("Contradictions", report.ContradictionCount.ToString());
        WriteField("Human Reviewed Items", report.HumanReviewedItems.ToString());
        WriteField("Evidence Graph", string.IsNullOrWhiteSpace(report.EvidenceGraphPath) ? "-" : DisplayPath(report.EvidenceGraphPath));
        WriteField("Evidence Graph Nodes", report.EvidenceGraphNodes.ToString());
        WriteField("Evidence Graph Links", report.EvidenceGraphLinks.ToString());
        WriteField("Contradiction Report", string.IsNullOrWhiteSpace(report.ContradictionsPath) ? "-" : DisplayPath(report.ContradictionsPath));
        WriteField("Human Review Evidence", string.IsNullOrWhiteSpace(report.HumanReviewPath) ? "-" : DisplayPath(report.HumanReviewPath));
        WriteMessages(
            "Trust Distribution",
            (report.TrustDistribution ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}: {item.Value}")
                .ToList());
        WriteField("Pending Reviews", report.PendingReviews.ToString());
        WriteField("Approved Reviews", report.ApprovedReviews.ToString());
        WriteField("Rejected Reviews", report.RejectedReviews.ToString());
        WriteField("Needs More Evidence", report.NeedsMoreEvidenceReviews.ToString());
        WriteField("Deferred Reviews", report.DeferredReviews.ToString());
        WriteField("Review Coverage", $"{report.ReviewCoverage:0.####}");
        WriteMessages("Top Review Priorities", report.TopReviewPriorities ?? []);
        foreach (var item in report.Items.Take(20))
        {
            WriteSubHeader($"{item.Title} / {item.KnowledgeId}");
            WriteField("Domain", item.Domain);
            WriteField("Lifecycle", item.LifecycleStatus);
            WriteField("Retention", item.RetentionState);
            WriteField("quality_score", $"{item.QualityScore:0.####}");
            WriteField("trust_score", $"{item.TrustScore:0.####}");
            WriteField("evidence_score", $"{item.EvidenceScore:0.####}");
            WriteField("reuse_score", $"{item.ReuseScore:0.####}");
            WriteField("validation_score", $"{item.ValidationScore:0.####}");
            WriteField("age_score", $"{item.AgeScore:0.####}");
            WriteMessages("Evidence", item.EvidenceRefs.Take(8).ToList());
            WriteMessages("Reasons", item.Reasons);
        }

        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteHumanReviewQueue(HumanReviewQueue queue, string queuePath)
    {
        WriteField("Review Queue", DisplayPath(queuePath));
        WriteField("Pending Reviews", queue.PendingReviews.ToString());
        WriteField("Approved Reviews", queue.ApprovedReviews.ToString());
        WriteField("Rejected Reviews", queue.RejectedReviews.ToString());
        WriteField("Needs More Evidence", queue.NeedsMoreEvidenceReviews.ToString());
        WriteField("Deferred Reviews", queue.DeferredReviews.ToString());
        foreach (var item in queue.Items
            .OrderByDescending(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Priority)
            .Take(20))
        {
            WriteHumanReviewItem(item);
        }

        WriteMessages("Warnings", queue.Warnings);
    }

    private void WriteHumanReviewItem(HumanReviewItem item)
    {
        WriteSubHeader($"{item.Priority} / {item.Status} / {item.ReviewId}");
        WriteField("Knowledge Item", item.KnowledgeItemId);
        WriteField("Domain", item.Domain);
        WriteField("Title", item.Title);
        WriteField("Reason", item.Reason);
        WriteField("Evidence Summary", item.EvidenceSummary);
        WriteField("Trust Before", $"{item.TrustBefore:0.####}");
        WriteField("Recommendation", item.Recommendation);
        WriteField("Requested By Task", item.RequestedByTaskId);
        WriteField("Created UTC", item.CreatedAtUtc.ToString("O"));
        WriteField("Updated UTC", item.UpdatedAtUtc?.ToString("O") ?? "-");
        WriteMessages("Evidence", item.EvidenceRefs.Take(8).ToList());
    }

    private void WriteHumanReviewDecision(HumanReviewDecision decision)
    {
        WriteField("Decision ID", decision.DecisionId);
        WriteField("Review ID", decision.ReviewId);
        WriteField("Knowledge Item", decision.KnowledgeItemId);
        WriteField("Domain", decision.Domain);
        WriteField("Decision", decision.Decision);
        WriteField("Decided By", decision.DecidedBy);
        WriteField("Decided UTC", decision.DecidedAtUtc.ToString("O"));
        WriteField("Note", decision.Note);
        WriteMessages("Followup Tasks", decision.FollowupTasks);
        WriteMessages("Evidence", decision.EvidenceRefs.Take(8).ToList());
    }

    private void WriteHumanReviewSummary(HumanReviewSummary summary)
    {
        WriteField("Review Queue", DisplayPath(summary.QueuePath));
        WriteField("Review Decisions", DisplayPath(summary.DecisionsPath));
        WriteField("Review Evidence", DisplayPath(summary.EvidencePath));
        WriteField("Total Review Items", summary.TotalReviewItems.ToString());
        WriteField("Pending Reviews", summary.PendingReviews.ToString());
        WriteField("Approved Reviews", summary.ApprovedReviews.ToString());
        WriteField("Rejected Reviews", summary.RejectedReviews.ToString());
        WriteField("Needs More Evidence", summary.NeedsMoreEvidenceReviews.ToString());
        WriteField("Deferred Reviews", summary.DeferredReviews.ToString());
        WriteField("Human Reviewed Items", summary.HumanReviewedItems.ToString());
        WriteField("Review Coverage", $"{summary.ReviewCoverage:0.####}");
        WriteMessages("Top Review Priorities", summary.TopReviewPriorities);
        WriteMessages("Warnings", summary.Warnings);
    }

    private void WriteContradictionReport(ContradictionReport report, string reportPath, bool detailed)
    {
        WriteField("Contradiction Report", DisplayPath(reportPath));
        WriteField("Contradictions", report.ContradictionCount.ToString());
        WriteMessages(
            "By Severity",
            report.ContradictionsBySeverity
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}: {item.Value}")
                .ToList());
        if (detailed)
        {
            foreach (var record in report.Contradictions.Take(20))
            {
                WriteSubHeader($"{record.Severity} / {record.ContradictionType} / {record.KnowledgeId}");
                WriteField("Domain", record.Domain);
                WriteField("Title", record.Title);
                WriteField("Recommendation", record.Recommendation);
                WriteMessages("Conflicting Values", record.ConflictingValues.Take(8).ToList());
                WriteMessages("Evidence", record.EvidenceRefs.Take(8).ToList());
            }
        }

        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteHumanReviewReport(HumanReviewResult report, string reportPath, bool detailed)
    {
        WriteField("Human Review Evidence", DisplayPath(reportPath));
        WriteField("Total Reviews", report.TotalReviews.ToString());
        WriteField("Reviewed Knowledge Items", report.ReviewedKnowledgeItems.ToString());
        WriteField("Approved", report.Approved.ToString());
        WriteField("Rejected", report.Rejected.ToString());
        WriteField("Needs Review", report.NeedsReview.ToString());
        if (detailed)
        {
            foreach (var review in report.Reviews.Take(20))
            {
                WriteSubHeader($"{review.Result} / {review.KnowledgeId}");
                WriteField("Domain", review.Domain);
                WriteField("Reviewer", review.Reviewer);
                WriteField("Reviewed UTC", review.ReviewedAtUtc.ToString("O"));
                WriteField("Notes", review.Notes);
            }
        }

        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteValidationPlanReport(KnowledgeValidationPlanReport report, string reportPath)
    {
        WriteField("Validation Plans", DisplayPath(reportPath));
        WriteField("Requirements", DisplayPath(report.RequirementsPath));
        WriteField("Total Plans", report.TotalPlans.ToString());
        WriteField("Open Plans", report.OpenPlans.ToString());
        WriteField("Trusted Candidates", report.TrustedCandidateCount.ToString());
        WriteField("Needs OOS", report.KnowledgeItemsNeedingOos.ToString());
        WriteField("Needs Source Check", report.KnowledgeItemsNeedingSourceCheck.ToString());
        WriteMessages("Most Common Missing Evidence", report.MostCommonMissingEvidence);
        var skippedByRouter = report.Plans
            .SelectMany(plan => plan.SkippedByRouterReasons ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        WriteMessages("Router Hints", skippedByRouter);
        foreach (var plan in report.Plans.Take(20))
        {
            WriteValidationPlan(plan);
        }

        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteValidationPlan(KnowledgeValidationPlan plan)
    {
        WriteSubHeader($"{plan.Title} / {plan.KnowledgeItemId}");
        WriteField("Plan ID", plan.PlanId);
        WriteField("Domain", plan.Domain);
        WriteField("Status", plan.Status);
        WriteField("Current Status", plan.CurrentStatus);
        WriteField("Target Status", plan.TargetStatus);
        WriteField("Priority", $"{plan.Priority:0.####}");
        WriteField("Expected Quality Delta", $"{plan.ExpectedQualityDelta:0.####}");
        WriteField("Related Goal", plan.RelatedGoalId);
        WriteMessages("Missing Evidence", plan.MissingEvidence);
        WriteMessages("Router Hints", (plan.SkippedByRouterReasons ?? []).Take(8).ToList());
        WriteMessages(
            "Requirements",
            plan.Requirements
                .Select(requirement => $"{requirement.RequirementType}:{requirement.RequiredTaskType}:priority={requirement.Priority:0.####}")
                .ToList());
        WriteMessages(
            "Required Tasks",
            plan.RequiredTasks
                .Select(task => $"{task.TaskType}:{task.MappedInternalTaskType}:{task.Status}")
                .ToList());
    }

    private void WriteKnowledgeValidationStatus(KnowledgeValidationStatus status)
    {
        WriteField("Status", DisplayPath(status.PlansPath));
        WriteField("Requirements", DisplayPath(status.RequirementsPath));
        WriteField("Research Queue", DisplayPath(status.ResearchQueuePath));
        WriteField("Open Plans", status.ValidationPlansOpen.ToString());
        WriteField("Pending Validation Tasks", status.ValidationTasksPending.ToString());
        WriteField("Queue Validation Tasks", status.QueueValidationTasks.ToString());
        WriteField("Trusted Candidates", status.TrustedCandidateCount.ToString());
        WriteField("Needs OOS", status.KnowledgeItemsNeedingOos.ToString());
        WriteField("Needs Source Check", status.KnowledgeItemsNeedingSourceCheck.ToString());
        WriteField("Invalid Validation Tasks", status.InvalidValidationTasks.ToString());
        WriteField("Validation Tasks Cleaned", status.ValidationTasksCleaned.ToString());
        WriteField("Validation Routing Health", status.ValidationRoutingHealth);
        WriteMessages("Most Common Missing Evidence", status.MostCommonMissingEvidence);
        WriteMessages("Warnings", status.Warnings);
    }

    private void WriteValidationExecutionSummary(IReadOnlyList<KnowledgeValidationExecutionResult> results, string executionLogPath)
    {
        WriteField("Execution Log", DisplayPath(executionLogPath));
        WriteField("Results", results.Count.ToString());
        WriteField("Completed", results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Needs More Data", results.Count(result => result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Skipped", results.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Failed", results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteMessages(
            "Outcome Status",
            results
                .GroupBy(result => result.OutcomeStatus, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(group => $"{group.Key}:{group.Count()}")
                .ToList());
        foreach (var result in results.Take(12))
        {
            WriteSubHeader($"{result.RequirementType} / {result.KnowledgeItemId}");
            WriteField("Status", result.Status);
            WriteField("Outcome", result.OutcomeStatus);
            WriteField("Plan", result.PlanId);
            WriteField("Queue Item", result.QueueItemId);
            WriteField("Evidence", result.EvidenceSummary);
            WriteMessages("Evidence Refs", result.EvidenceRefs.Take(8).ToList());
            WriteMessages("Warnings", result.Warnings);
        }
    }

    private void WriteDomainValidationStatus(DomainValidationStatusReport status)
    {
        WriteField("Domain Validation Health", status.DomainValidationHealth);
        WriteField("Documentation Pending", status.DocumentationValidationPending.ToString());
        WriteField("Software Pending", status.SoftwareValidationPending.ToString());
        WriteField("Process Pending", status.ProcessValidationPending.ToString());
        WriteField("Research Pending", status.ResearchValidationPending.ToString());
        WriteField("Plans", DisplayPath(status.PlansPath));
        WriteField("Execution Log", DisplayPath(status.ExecutionLogPath));
        WriteMessages("Domain Validation Warnings", status.DomainValidationWarnings);
    }

    private void WriteValidationRoutingStatus(DomainValidationRoutingStatus status)
    {
        WriteField("Profiles", status.Profiles.ToString());
        WriteField("Invalid Validation Tasks", status.InvalidValidationTasks.ToString());
        WriteField("Validation Tasks Cleaned", status.ValidationTasksCleaned.ToString());
        WriteField("Validation Routing Health", status.ValidationRoutingHealth);
        foreach (var profile in status.DomainProfiles)
        {
            WriteValidationRoutingProfile(profile);
        }

        WriteMessages("Warnings", status.Warnings);
    }

    private void WriteValidationRoutingProfile(DomainValidationProfile profile)
    {
        WriteSubHeader(profile.Domain);
        WriteMessages(
            "Allowed",
            profile.Capabilities
                .Select(capability => $"{capability.RequirementType}:{capability.DefaultTaskType}:{capability.DefaultMappedInternalTaskType}")
                .ToList());
        WriteMessages("Not Allowed", profile.ExplicitlyUnsupportedRequirementTypes);
    }

    private void WriteCognitiveDomainStatusEntry(DomainStatusEntry entry)
    {
        WriteSubHeader($"{entry.Domain} / {(entry.Active ? "active" : "inactive")}");
        WriteField("Last Scan", entry.LastScannedAtUtc?.ToString("O") ?? "-");
        WriteField("Sources", entry.SourceCount.ToString());
        WriteField("Knowledge Items", entry.KnowledgeItemCount.ToString());
        WriteField("Open Needs", entry.OpenNeeds.ToString());
        WriteField("Open Queue", entry.OpenQueueItems.ToString());
        WriteMessages("Next Tasks", entry.NextRecommendedTasks);
        WriteMessages("Warnings", entry.Warnings);
    }

    private void WriteCognitiveDomainInsight(DomainInsight insight)
    {
        WriteSubHeader($"{insight.Severity} / {insight.Domain} / {insight.InsightId}");
        WriteField("Title", insight.Title);
        WriteField("Summary", insight.Summary);
        WriteMessages("Evidence", insight.EvidenceRefs.Select(DisplayPath).ToList());
        WriteMessages("Recommended Tasks", insight.RecommendedTasks);
    }

    private void WriteDomainScanResult(DomainScanResult result)
    {
        WriteField("Domain", result.Domain);
        WriteField("Scanned UTC", result.ScannedAtUtc.ToString("O"));
        WriteField("Sources Scanned", result.SourcesScanned.ToString());
        WriteField("Knowledge Items", result.KnowledgeItems.ToString());
        WriteMessages("Output Paths", result.OutputPaths.Select(DisplayPath).ToList());
        WriteMessages("Warnings", result.Warnings);
        WriteField("no_trading_execution", result.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("no_broker_action", result.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteResearchQueueItem(ResearchQueueItem item)
    {
        WriteSubHeader($"{item.QueueItemId} / {item.Domain} / {item.Type}");
        WriteField("Queue", item.Queue);
        WriteField("Priority", item.Priority.ToString());
        WriteField("Status", item.Status);
        WriteField("Requested By", item.RequestedBy);
        WriteField("Created UTC", item.CreatedAtUtc.ToString("O"));
        WriteField("Updated UTC", item.UpdatedAtUtc?.ToString("O") ?? "-");
        WriteMessages("Source Refs", item.SourceRefs);
        WriteMessages("Notes", item.Notes);
    }

    private void WriteDetectedNeed(DetectedNeed need)
    {
        WriteSubHeader($"{need.Severity} / {need.Category} / {need.NeedId}");
        WriteField("Domain", need.Domain);
        WriteField("Title", need.Title);
        WriteField("Description", need.Description);
        WriteMessages("Evidence", need.EvidenceRefs);
        WriteMessages("Suggested Tasks", need.SuggestedTaskTypes);
        WriteField("no_trading_execution", need.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", need.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteHermesGoal(HermesGoal goal)
    {
        WriteSubHeader($"{goal.Priority:00} / {goal.GoalId}");
        WriteField("Title", goal.Title);
        WriteField("Domain", goal.Domain);
        WriteField("Active", goal.Active.ToString().ToLowerInvariant());
        WriteField("Current State", goal.CurrentState);
        WriteField("Target State", goal.TargetState);
        WriteField("Progress", $"{goal.ProgressScore:0.####}");
        WriteField("Blocker Count", goal.BlockerCount.ToString());
        WriteField("Last Updated UTC", goal.LastUpdatedUtc.ToString("O"));
        WriteField("Description", goal.Description);
        WriteMessages("Related Needs", goal.RelatedNeeds);
        WriteMessages("Related Tasks", goal.RelatedTasks);
        WriteMessages("Recent Outcomes", goal.RecentOutcomes);
        WriteMessages("Blockers", goal.Blockers);
        WriteMessages("Next Actions", goal.NextRecommendedActions.Count > 0 ? goal.NextRecommendedActions : goal.NextActions);
    }

    private void WritePlannedTask(PlannedTask task)
    {
        WriteSubHeader($"{task.TaskType} / {task.TaskId}");
        WriteField("Domain", task.Domain);
        WriteField("Goal", task.GoalId);
        WriteField("Supporting Goal", task.SupportingGoalId);
        WriteField("Need", task.NeedId);
        WriteField("Queue", task.QueueType);
        WriteField("Status", task.Status);
        WriteField("Priority", $"{task.Priority.TotalScore:0.####}");
        WriteField("Score Detail", $"impact={task.Priority.Impact:0.##}, urgency={task.Priority.Urgency:0.##}, confidence={task.Priority.Confidence:0.##}, cost={task.Priority.Cost:0.##}, risk={task.Priority.Risk:0.##}, learning={task.Priority.ExpectedLearningValue:0.##}, goal={task.Priority.GoalPriority:0.##}, redundancy={task.Priority.RedundancyPenalty:0.##}");
        WriteField("Expected Goal Delta", $"{task.ExpectedGoalDelta:0.####}");
        WriteField("Reason", task.Reason);
        WriteField("Goal Reason", task.GoalReason);
        WriteField("Expected Outcome", task.ExpectedOutcome);
        WriteMessages("Source Refs", task.SourceRefs);
        WriteField("no_trading_execution", task.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", task.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WritePlannedTaskExecutionResult(PlannedTaskExecutionResult result)
    {
        WriteSubHeader($"{result.Status} / {result.TaskType} / {result.TaskId}");
        WriteField("Need", result.NeedId);
        WriteField("Goal", result.GoalId);
        WriteField("Started UTC", result.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", result.CompletedAtUtc?.ToString("O") ?? "-");
        WriteField("Reason", result.Reason);
        WriteField("Skipped Reason", result.SkippedReason ?? "-");
        WriteMessages("Output Paths", result.OutputPaths.Select(DisplayPath).ToList());
        WriteMessages("Warnings", result.Warnings);
        WriteField("no_trading_execution", result.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("no_broker_action", result.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteTaskOutcome(TaskOutcomeResult outcome)
    {
        WriteSubHeader($"{outcome.Recommendation} / {outcome.TaskType} / {outcome.TaskId}");
        WriteField("Outcome ID", outcome.OutcomeId);
        WriteField("Need", outcome.NeedId);
        WriteField("Goal", outcome.GoalId);
        WriteField("Executed UTC", outcome.ExecutedAtUtc.ToString("O"));
        WriteField("Evaluated UTC", outcome.EvaluatedAtUtc.ToString("O"));
        WriteField("Usefulness", $"{outcome.OutcomeScore.UsefulnessScore:0.####}");
        WriteField("Learning Value", $"{outcome.OutcomeScore.LearningValue:0.####}");
        WriteField("Cost", $"{outcome.OutcomeScore.CostScore:0.####}");
        WriteField("Risk", $"{outcome.OutcomeScore.RiskScore:0.####}");
        WriteField("Redundancy", $"{outcome.OutcomeScore.RedundancyScore:0.####}");
        WriteField("Need Reduced", outcome.Evidence.NeedReduced.ToString().ToLowerInvariant());
        WriteField("Goal Improved", outcome.Evidence.GoalImproved.ToString().ToLowerInvariant());
        WriteField("Queue Changed", outcome.Evidence.ResearchQueueChanged.ToString().ToLowerInvariant());
        WriteMessages("Evidence", outcome.Evidence.Notes);
        WriteMessages("Followups", outcome.FollowupTaskIds);
        WriteField("no_trading_execution", outcome.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", outcome.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WritePlannerTaskTypeFeedback(PlannerTaskTypeFeedback item)
    {
        WriteSubHeader($"{item.Recommendation} / {item.TaskType}");
        WriteField("Evaluations", item.Evaluations.ToString());
        WriteField("Avg Usefulness", $"{item.AverageUsefulnessScore:0.####}");
        WriteField("Avg Learning", $"{item.AverageLearningValue:0.####}");
        WriteField("Avg Cost", $"{item.AverageCostScore:0.####}");
        WriteField("Avg Risk", $"{item.AverageRiskScore:0.####}");
        WriteField("Avg Redundancy", $"{item.AverageRedundancyScore:0.####}");
        WriteField("Priority Adjustment", $"{item.PriorityAdjustment:0.####}");
        WriteField("Last Evaluated", item.LastEvaluatedUtc.ToString("O"));
        WriteMessages("Repeated Unsuccessful Needs", item.RepeatedUnsuccessfulNeeds);
    }

    private void WriteGoalFeedbackEntry(GoalFeedbackEntry item)
    {
        WriteSubHeader($"{item.GoalId}");
        WriteField("Evaluations", item.Evaluations.ToString());
        WriteField("Avg Usefulness", $"{item.AverageUsefulnessScore:0.####}");
        WriteField("Progress Delta", $"{item.ProgressDelta:0.####}");
        WriteField("Last Evaluated", item.LastEvaluatedUtc.ToString("O"));
        WriteMessages("Improved Needs", item.ImprovedNeeds);
        WriteMessages("Persistent Needs", item.PersistentNeeds);
        WriteMessages("Recommended Actions", item.RecommendedActions);
    }

    private void WriteAutonomousLoopSummary(AutonomousLoopSummary summary)
    {
        WriteField("Status", summary.Status);
        WriteField("Run ID", summary.RunId);
        WriteField("Requested Iterations", summary.RequestedIterations.ToString());
        WriteField("Max Minutes", $"{summary.MaxMinutes:0.###}");
        WriteField("Iterations", summary.IterationsCompleted.ToString());
        WriteField("Idle Iterations", summary.IdleIterations.ToString());
        WriteField("Work Performed", summary.WorkPerformed.ToString());
        WriteField("Average Learning", $"{summary.AverageLearningValue:0.####}");
        WriteField("Next Action", summary.NextAction);
        WriteField("Stop Reason", summary.StopReason ?? "-");
        WriteMessages("Warnings", summary.Warnings);
        if (summary.LastIteration is not null)
        {
            WriteAutonomousLoopIteration(summary.LastIteration);
        }
    }

    private void WriteAutonomousLoopIteration(AutonomousLoopIterationSummary iteration)
    {
        WriteSubHeader($"{iteration.Status} / iteration {iteration.IterationNumber}");
        WriteField("Iteration ID", iteration.IterationId);
        WriteField("Started UTC", iteration.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", iteration.CompletedAtUtc.ToString("O"));
        WriteField("Resource Action", iteration.ResourceAction);
        WriteField("Cleanup Candidates", iteration.CleanupCandidates.ToString());
        WriteField("Needs", iteration.NeedsDetected.ToString());
        WriteField("Planned Tasks", iteration.TasksPlanned.ToString());
        WriteField("Executed Tasks", iteration.TasksExecuted.ToString());
        WriteField("Completed/Skipped/Failed", $"{iteration.TasksCompleted}/{iteration.TasksSkipped}/{iteration.TasksFailed}");
        WriteField("Outcomes Evaluated", iteration.OutcomesEvaluated.ToString());
        WriteField("Avg Usefulness", $"{iteration.AverageOutcomeUsefulness:0.####}");
        WriteField("Avg Learning", $"{iteration.AverageOutcomeLearningValue:0.####}");
        WriteField("Cognitive Insights", iteration.CognitiveInsights.ToString());
        WriteField("Work Performed", iteration.WorkPerformed.ToString().ToLowerInvariant());
        WriteField("Idle", iteration.Idle.ToString().ToLowerInvariant());
        WriteField("Next Action", iteration.NextAction);
        WriteField("Stop Reason", iteration.StopReason ?? "-");
        WriteField("Checkpoint", DisplayOptionalPath(iteration.CheckpointPath));
        WriteMessages("Feedback Changes", iteration.FeedbackChanges);
        WriteMessages("Warnings", iteration.Warnings);
        WriteMessages("Resource Warnings", iteration.ResourceWarnings);
        WriteField("no_trading_execution", iteration.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", iteration.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteMetaReview(MetaReviewResult review)
    {
        WriteField("Status", review.Status);
        WriteField("Updated UTC", review.UpdatedAtUtc.ToString("O"));
        WriteField("Goals Reviewed", review.GoalsReviewed.ToString());
        WriteField("Outcomes Reviewed", review.OutcomesReviewed.ToString());
        WriteField("Planner Task Types", review.PlannerTaskTypesReviewed.ToString());
        WriteField("Knowledge Items", review.KnowledgeItems.ToString());
        WriteField("Research Queue Items", review.ResearchQueueItems.ToString());
        WriteField("Learning Strategy", review.LearningStrategy.CurrentStrategy);
        WriteMessages("Activities With Progress", review.ActivitiesWithProgress);
        WriteMessages("Activities Generating Work", review.ActivitiesGeneratingWork);
        WriteMessages("Stagnant Goals", review.StagnantGoals);
        WriteMessages("Recurring Needs", review.RecurringNeeds);
        foreach (var observation in review.Observations.Take(12))
        {
            WriteMetaObservation(observation);
        }
        WriteField("no_auto_trading", review.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", review.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteMetaObservation(MetaObservation observation)
    {
        WriteSubHeader($"{observation.Severity} / {observation.Category} / {observation.Title}");
        WriteField("Observation", observation.ObservationId);
        WriteField("Summary", observation.Summary);
        WriteMessages("Evidence", observation.EvidenceRefs);
        WriteMessages("Recommended Actions", observation.RecommendedActions);
    }

    private void WriteDomainHealth(DomainHealth health)
    {
        WriteSubHeader($"{health.Domain} / {health.Score.Classification}");
        WriteField("Active", health.Active.ToString().ToLowerInvariant());
        WriteField("Sources", health.SourceCount.ToString());
        WriteField("Knowledge Items", health.KnowledgeItemCount.ToString());
        WriteField("Queue Items", health.QueueItems.ToString());
        WriteField("Open Queue", health.OpenQueueItems.ToString());
        WriteField("Processed Queue", health.ProcessedQueueItems.ToString());
        WriteField("Outcomes", health.OutcomeCount.ToString());
        WriteField("Overall", $"{health.Score.OverallScore:0.####}");
        WriteField("Knowledge", $"{health.Score.KnowledgeCoverage:0.####}");
        WriteField("Validation", $"{health.Score.ValidationCoverage:0.####}");
        WriteField("Trust", $"{health.Score.TrustScore:0.####}");
        WriteField("Redundancy", $"{health.Score.RedundancyScore:0.####}");
        WriteField("Learning Velocity", $"{health.Score.LearningVelocity:0.####}");
        WriteMessages("Reasons", health.Score.Reasons);
        WriteMessages("Warnings", health.Warnings);
    }

    private void WriteLearningStrategy(LearningStrategy strategy)
    {
        WriteField("Strategy", strategy.CurrentStrategy);
        WriteField("Updated UTC", strategy.UpdatedAtUtc.ToString("O"));
        WriteField("Reason", strategy.Reason);
        WriteField("Expected Effect", strategy.ExpectedEffect);
        WriteMessages("Priority Task Types", strategy.PriorityTaskTypes);
        WriteMessages("Deprioritized Task Types", strategy.DeprioritizedTaskTypes);
        WriteMessages("Domain Focus", strategy.DomainFocus);
        WriteField("no_auto_trading", strategy.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", strategy.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteGovernanceDecision(GovernanceDecision decision)
    {
        WriteSubHeader($"{decision.Status} / {decision.RuleId}");
        WriteField("Reason", decision.Reason);
        WriteField("Action", decision.Action);
        WriteMessages("Evidence", decision.EvidenceRefs);
    }

    private void WriteCognitiveHypothesis(CognitiveHypothesis hypothesis)
    {
        WriteSubHeader($"{hypothesis.Title} / {hypothesis.HypothesisId}");
        WriteField("Domain", hypothesis.Domain);
        WriteField("Status", hypothesis.Status);
        WriteField("Trust", $"{hypothesis.Trust.Value:0.####} / {hypothesis.Trust.Classification}");
        WriteField("Evidence", $"{hypothesis.Evidence.Value:0.####} / {hypothesis.Evidence.Classification}");
        WriteField("Validation", hypothesis.ProposedValidation);
        WriteMessages("Source Items", hypothesis.SourceItemIds);
    }

    private void WriteCognitiveInsight(CognitiveInsight insight)
    {
        WriteSubHeader($"{insight.Title} / {insight.InsightId}");
        WriteField("Domain", insight.Domain);
        WriteField("Status", insight.Status);
        WriteField("Summary", insight.Summary);
        WriteMessages("Evidence", insight.EvidenceRefs);
        WriteMessages("Recommended Actions", insight.RecommendedActions);
        WriteField("no_trading_execution", insight.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", insight.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private static bool IsProcessAlive(int? processId)
    {
        return SupervisorProcessManager.IsProcessAlive(processId);
    }

    private void WriteResourceSnapshot(ResourceSnapshot snapshot)
    {
        WriteField("Timestamp UTC", snapshot.TimestampUtc.ToString("O"));
        WriteField("CPU", $"{snapshot.CpuUsagePercent:0.##}%");
        WriteField("RAM", $"{snapshot.MemoryUsagePercent:0.##}% ({snapshot.UsedMemoryMb}/{snapshot.TotalMemoryMb} MB)");
        WriteField("Free Disk", $"{snapshot.FreeDiskMb / 1024.0:0.##} GB ({snapshot.FreeDiskPercent:0.##}%)");
        WriteField("Storage Root", DisplayPath(snapshot.StorageRoot));
        WriteField("Action", snapshot.Action);
        WriteField("auto_cleanup_policy_enabled", snapshot.AutoCleanupPolicyEnabled.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_allowed", snapshot.AutoCleanupAllowed.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_last_run", snapshot.AutoCleanupLastRun?.ToString("O") ?? "-");
        WriteField("auto_cleanup_last_result", snapshot.AutoCleanupLastResult);
        WriteField("cleanup_candidates", snapshot.CleanupCandidates.ToString());
        WriteField("estimated_free_bytes", snapshot.EstimatedFreeBytes.ToString());
        WriteField("protected_paths_count", snapshot.ProtectedPathsCount.ToString());
        WriteField("safety_mode", snapshot.SafetyMode);
        WriteField("Should Pause", snapshot.ShouldPause.ToString().ToLowerInvariant());
        WriteField("Should Stop", snapshot.ShouldStop.ToString().ToLowerInvariant());
        WriteMessages("Warnings", snapshot.Warnings);
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteStorageStatus(StorageStatusSnapshot status)
    {
        WriteField("auto_cleanup_policy_enabled", status.AutoCleanupPolicyEnabled.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_allowed", status.AutoCleanupAllowed.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_last_run", status.AutoCleanupLastRun?.ToString("O") ?? "-");
        WriteField("auto_cleanup_last_result", status.AutoCleanupLastResult);
        WriteField("cleanup_candidates", status.CleanupCandidates.ToString());
        WriteField("estimated_free_bytes", status.EstimatedFreeBytes.ToString());
        WriteField("protected_paths_count", status.ProtectedPathsCount.ToString());
        WriteField("safety_mode", status.SafetyMode);
        WriteField("policy_action", status.PolicyAction);
        WriteMessages("storage_warnings", status.Warnings);
    }

    private void WriteCleanupPlan(CleanupPlan plan, int limit)
    {
        WriteField("Plan ID", plan.PlanId);
        WriteField("Created UTC", plan.CreatedAtUtc.ToString("O"));
        WriteField("Candidates", plan.Candidates.Count.ToString());
        WriteField("Estimated Free", $"{plan.EstimatedBytesToFree / 1024.0 / 1024.0:0.##} MB");
        WriteField("Safe To Apply", plan.SafeToApply.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_policy_enabled", plan.PolicyStatus.AutoCleanupPolicyEnabled.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_allowed", plan.PolicyStatus.AutoCleanupAllowed.ToString().ToLowerInvariant());
        WriteField("auto_cleanup_last_run", plan.PolicyStatus.AutoCleanupLastRun?.ToString("O") ?? "-");
        WriteField("auto_cleanup_last_result", plan.PolicyStatus.AutoCleanupLastResult);
        WriteField("cleanup_candidates", plan.PolicyStatus.CleanupCandidates.ToString());
        WriteField("estimated_free_bytes", plan.PolicyStatus.EstimatedFreeBytes.ToString());
        WriteField("protected_paths_count", plan.PolicyStatus.ProtectedPathsCount.ToString());
        WriteField("safety_mode", plan.PolicyStatus.SafetyMode);
        foreach (var candidate in plan.Candidates.Take(limit))
        {
            WriteSubHeader(candidate.Reason);
            WriteField("Path", DisplayPath(candidate.Path));
            WriteField("Bytes", candidate.EstimatedBytes.ToString());
            WriteField("Safe", candidate.SafeToDelete.ToString().ToLowerInvariant());
        }

        WriteField("Protected Paths", plan.ProtectedPaths.Count.ToString());
        WriteMessages("Policy Warnings", plan.PolicyStatus.Warnings);
        WriteField("no_auto_trading", plan.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", plan.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteStrategyCluster(StrategyCluster cluster)
    {
        WriteSubHeader($"{cluster.Family} / {cluster.ClusterId}");
        WriteField("Variants", cluster.VariantCount.ToString());
        WriteField("Average Fitness", $"{cluster.AverageFitness:0.####}");
        WriteField("Best Fitness", $"{cluster.BestFitness:0.####}");
        WriteField("Average Winrate", $"{cluster.AverageWinrate * 100:0.##}%");
        WriteField("Average Trades", $"{cluster.AverageTradeCount:0.##}");
        WriteField("Prioritized", cluster.Prioritized.ToString().ToLowerInvariant());
        WriteField("Reduced", cluster.Reduced.ToString().ToLowerInvariant());
        WriteMessages("Common Parameters", cluster.CommonParameters);
    }

    private string ResolvePatternName(string? patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return "-";
        }

        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        return StrategyPatternCatalog.PatternName(catalog.LoadOrCreateCatalog(), patternId);
    }

    private int ShowCTraderHealth()
    {
        WriteHeader("Hermes cTrader Open API Health");
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var storagePaths = BuildStoragePaths();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var storedToken = tokenStore.LoadToken();
        ICTraderHistoricalDataClient client = configLoad.LocalConfigLoaded
                && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase)
            ? new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken ?? new CTraderStoredToken())
            : new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
        var health = client.CheckHealth();

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);
        PublishCTraderEvent(
            eventBus,
            EventType.CTraderConnectorHealthChecked,
            EventSeverity.Info,
            new
            {
                message = health.StubActive
                    ? "cTrader Open API connector health checked. Stub fallback; no live connection."
                    : "cTrader Open API connector health checked. Real read-only client configured; no download performed.",
                health,
                configPath = configLoad.ConfigPath,
                configLoad.LocalConfigLoaded,
                configLoad.LocalConfigMissing,
                authMode = authContext.AuthStatus.AuthMode,
                authUrlAvailable = authContext.AuthStatus.AuthUrlAvailable,
                tokenLoaded = authContext.AuthStatus.TokenLoaded,
                authStatus = authContext.AuthStatus.Status,
                noAutoTrading = true,
                humanReviewRequired = true
            });
        eventStore.Flush();

        Console.WriteLine(health.StubActive
            ? "Open API connector stub active"
            : "Open API read-only client configured");
        Console.WriteLine("No cTrader download was performed by health check.");
        WriteField("Status", health.Status);
        WriteField("Environment", health.Environment);
        WriteField("Stub Active", health.StubActive.ToString().ToLowerInvariant());
        WriteField("Auth Configured", health.AuthConfigured.ToString().ToLowerInvariant());
        WriteField("Auth Mode", authContext.AuthStatus.AuthMode);
        WriteField("Auth URL Available", authContext.AuthStatus.AuthUrlAvailable.ToString().ToLowerInvariant());
        WriteField("Auth Status", authContext.AuthStatus.Status);
        WriteField("Token Store Path", DisplayPath(authContext.AuthStatus.TokenStorePath));
        WriteField("Token Loaded", authContext.AuthStatus.TokenLoaded.ToString().ToLowerInvariant());
        WriteField("Client ID Configured", health.ClientIdConfigured.ToString().ToLowerInvariant());
        WriteField("Account ID Configured", health.AccountIdConfigured.ToString().ToLowerInvariant());
        WriteField("no_orders", health.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", health.ReadOnlyMarketData.ToString().ToLowerInvariant());
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Local Config Missing", configLoad.LocalConfigMissing.ToString().ToLowerInvariant());
        WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, health.Warnings, authContext.AuthStatus.Warnings));
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowCTraderAuthUrl()
    {
        WriteHeader("Hermes cTrader OAuth URL");
        var configLoad = LoadCTraderConfig();
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);

        WriteField("Auth URL Available", oauthUrl.Available.ToString().ToLowerInvariant());
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Redirect URI", oauthUrl.RedirectUri);
        WriteField("Scopes", string.Join(", ", oauthUrl.Scopes));
        WriteField("no_orders", configLoad.Config.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", configLoad.Config.ReadOnlyMarketData.ToString().ToLowerInvariant());
        Console.WriteLine();

        if (oauthUrl.Url is not null)
        {
            Console.WriteLine("OAuth URL:");
            Console.WriteLine(oauthUrl.Url);
            Console.WriteLine();
            Console.WriteLine("Browser nicht automatisch geoeffnet.");
            Console.WriteLine("Oeffne die URL manuell im Browser, melde dich bei cTrader an und kopiere danach den Redirect-Code.");
        }

        WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
        Console.WriteLine();
        WriteSafety();
        return oauthUrl.Available ? 0 : 1;
    }

    private int ExchangeCTraderAuthCode()
    {
        WriteHeader("Hermes cTrader OAuth Code Exchange");
        var code = ReadOption(_args, "--code");
        if (string.IsNullOrWhiteSpace(code)
            || code.Contains('<', StringComparison.Ordinal)
            || code.Contains('>', StringComparison.Ordinal))
        {
            WriteError("Ein frischer OAuth Redirect-Code ist erforderlich. Beispiel: ctrader-auth-code --code ABC123");
            WriteSafety();
            return 2;
        }

        var configLoad = LoadCTraderConfig();
        var storagePaths = BuildStoragePaths();
        var tokenStore = new CTraderTokenStore(storagePaths);
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);

        if (!configLoad.LocalConfigLoaded)
        {
            WriteError("config/ctrader.openapi.local.json fehlt. Kein Token Exchange ausgefuehrt.");
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
            WriteSafety();
            return 1;
        }

        if (!oauthUrl.Available)
        {
            WriteError("cTrader OAuth ist nicht sicher konfiguriert. Kein Token Exchange ausgefuehrt.");
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
            WriteSafety();
            return 1;
        }

        try
        {
            using var httpClient = new HttpClient();
            var exchangeClient = new CTraderTokenExchangeClient(httpClient);
            var token = exchangeClient
                .ExchangeAuthorizationCodeAsync(configLoad.Config, code.Trim())
                .GetAwaiter()
                .GetResult();
            tokenStore.SaveToken(token);

            WriteField("Status", "authenticated");
            WriteField("Token Store Path", DisplayPath(tokenStore.TokenStorePath));
            WriteField("Token Loaded", "true");
            WriteField("Expires UTC", token.ExpiresAtUtc?.ToString("O") ?? "-");
            WriteField("Tokens Printed", "false");
            Console.WriteLine();
            Console.WriteLine("Token wurde lokal gespeichert. Access-/Refresh-Token werden nicht ausgegeben.");
            WriteSafety();
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            WriteError(ex.Message);
            WriteField("Token Store Path", DisplayPath(tokenStore.TokenStorePath));
            WriteField("Token Written", "false");
            Console.WriteLine();
            WriteSafety();
            return 1;
        }
    }

    private int ShowCTraderAuthStatus()
    {
        WriteHeader("Hermes cTrader Auth Status");
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, BuildStoragePaths());
        var status = authContext.AuthStatus;

        WriteField("Status", status.Status);
        WriteField("Auth URL Available", status.AuthUrlAvailable.ToString().ToLowerInvariant());
        WriteField("Auth Configured", status.AuthConfigured.ToString().ToLowerInvariant());
        WriteField("Auth Mode", status.AuthMode);
        WriteField("Token Store Path", DisplayPath(status.TokenStorePath));
        WriteField("Token Store Exists", status.TokenStoreExists.ToString().ToLowerInvariant());
        WriteField("Token Loaded", status.TokenLoaded.ToString().ToLowerInvariant());
        WriteField("Expires UTC", status.ExpiresAtUtc?.ToString("O") ?? "-");
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("no_orders", configLoad.Config.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", configLoad.Config.ReadOnlyMarketData.ToString().ToLowerInvariant());
        WriteMessages("Warnings", status.Warnings);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowCTraderSymbols()
    {
        WriteHeader("Hermes cTrader Symbol Mapping");
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);

        Console.WriteLine("Lokales Symbol-Mapping; echte brokerseitige Symbol-IDs werden beim Download aus der cTrader Symbol-Liste aufgeloest.");
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Local Config Missing", configLoad.LocalConfigMissing.ToString().ToLowerInvariant());
        WriteField("Allowed Timeframes", string.Join(", ", config.AllowedTimeframes));
        WriteMessages("Warnings", configLoad.Warnings);
        Console.WriteLine();

        foreach (var mapping in mapper.GetMappings())
        {
            WriteSubHeader(mapping.HermesSymbol);
            WriteField("cTrader Name", mapping.CTraderSymbolName);
            WriteField("cTrader Symbol ID", mapping.CTraderSymbolId);
            WriteField("Aliases", string.Join(", ", mapping.Aliases));
            WriteField("Stub Mapping", mapping.StubMapping.ToString().ToLowerInvariant());
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int DownloadCTraderHistory()
    {
        WriteHeader("Hermes cTrader Historical Download");
        var symbol = ReadOption(_args, "--symbol") ?? ReadOption(_args, "--asset");
        var timeframe = ReadOption(_args, "--timeframe");
        var fromText = ReadOption(_args, "--from");
        var toText = ReadOption(_args, "--to");
        var maxRequests = ReadIntOption(_args, "--max-requests", fallback: 500, min: 1, max: 500);

        if (string.IsNullOrWhiteSpace(symbol)
            || string.IsNullOrWhiteSpace(timeframe)
            || string.IsNullOrWhiteSpace(fromText)
            || string.IsNullOrWhiteSpace(toText)
            || !TryParseCliDate(fromText, out var fromUtc)
            || !TryParseCliDate(toText, out var toUtc))
        {
            WriteError("Pflichtargumente fehlen oder Datumswerte sind ungueltig.");
            Console.WriteLine("Beispiel:");
            Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol XAUUSD --timeframe M5 --from 2025-01-01 --to 2025-01-02");
            WriteSafety();
            return 2;
        }

        var storagePaths = BuildStoragePaths();
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var useRealClient = configLoad.LocalConfigLoaded
            && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase);
        var stubActive = !useRealClient;
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var request = new CTraderHistoricalDataRequest(
            Symbol: symbol.Trim().ToUpperInvariant(),
            Timeframe: timeframe.Trim().ToUpperInvariant(),
            FromUtc: fromUtc,
            ToUtc: toUtc);

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadStarted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "cTrader historical download started. Real read-only Open API path."
                    : "cTrader historical download started. Stub fallback; no live Open API call.",
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                stubActive,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        try
        {
            var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
            var tokenStore = new CTraderTokenStore(storagePaths);
            var storedToken = tokenStore.LoadToken();
            ICTraderHistoricalDataClient client;

            if (useRealClient)
            {
                if (storedToken is null || !storedToken.HasAccessToken)
                {
                    throw new InvalidOperationException(
                        "cTrader OAuth token missing. Run ctrader-auth-url, open the URL in a browser, then run ctrader-auth-code --code <CODE> with a fresh redirect code. No real cTrader data was downloaded.");
                }

                client = new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken);
            }
            else
            {
                client = new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
            }

            var download = DownloadPagedHistoricalCandles(client, request, maxRequests);
            var importer = new CTraderTrendbarImporter(storagePaths);
            var result = useRealClient
                ? importer.ImportCandles(request, download.Candles, sourceName: "ctrader_openapi_paged", stubData: false)
                : importer.ImportStubCandles(request, download.Candles);

            PublishCTraderEvent(
                eventBus,
                EventType.CTraderHistoricalDownloadCompleted,
                EventSeverity.Info,
                new
                {
                    message = useRealClient
                        ? "cTrader historical download completed with real read-only Open API data."
                        : "cTrader historical download completed with stub data. No real cTrader data was loaded.",
                    result.DownloadId,
                    result.Symbol,
                    result.Timeframe,
                    result.OutputPath,
                    result.CandleCount,
                    result.FromUtc,
                    result.ToUtc,
                    result.StubData,
                    requests = download.Requests,
                    duplicatesSkipped = download.DuplicatesSkipped,
                    download.Truncated,
                    download.Warnings,
                    configPath = configLoad.ConfigPath,
                    configLoad.LocalConfigLoaded,
                    configLoad.LocalConfigMissing,
                    authMode = authContext.AuthStatus.AuthMode,
                    tokenLoaded = authContext.AuthStatus.TokenLoaded,
                    authStatus = authContext.AuthStatus.Status,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            Console.WriteLine(useRealClient
                ? "Open API read-only historical download completed"
                : "Open API connector stub active");
            Console.WriteLine(useRealClient
                ? "Echte cTrader Candles wurden lokal normalisiert und gespeichert."
                : "No real cTrader data was loaded.");
            if (!useRealClient && configLoad.LocalConfigMissing)
            {
                Console.WriteLine("Local config missing: config/ctrader.openapi.local.json. Stub active.");
            }

            WriteField("Download ID", result.DownloadId);
            WriteField("Symbol", result.Symbol);
            WriteField("Timeframe", result.Timeframe);
            WriteField("Rows", result.CandleCount.ToString());
            WriteField("Requests", download.Requests.ToString());
            WriteField("Max Requests", maxRequests.ToString());
            WriteField("Duplicates Skipped", download.DuplicatesSkipped.ToString());
            WriteField("Truncated", download.Truncated.ToString().ToLowerInvariant());
            WriteField("From UTC", result.FromUtc?.ToString("O"));
            WriteField("To UTC", result.ToUtc?.ToString("O"));
            WriteField("Output", DisplayPath(result.OutputPath));
            WriteField("Stub Data", result.StubData.ToString().ToLowerInvariant());
            WriteField("Config", DisplayPath(configLoad.ConfigPath));
            WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, authContext.AuthStatus.Warnings, download.Warnings));
            Console.WriteLine();

            WriteSafety();
            return 0;
        }
        catch (Exception ex)
        {
            PublishCTraderEvent(
                eventBus,
                EventType.CTraderHistoricalDownloadFailed,
                EventSeverity.Warning,
                new
                {
                    message = useRealClient
                        ? "cTrader historical download failed before any trading action. Real read-only path."
                        : "cTrader historical download failed before any trading action. Stub fallback.",
                    request.Symbol,
                    request.Timeframe,
                    request.FromUtc,
                    request.ToUtc,
                    error = ex.Message,
                    stubActive,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
    }

    private HistoricalDownloadPageResult DownloadPagedHistoricalCandles(
        ICTraderHistoricalDataClient client,
        CTraderHistoricalDataRequest request,
        int maxRequests)
    {
        var interval = TimeframeInterval(request.Timeframe);
        var candlesByTimestamp = new Dictionary<DateTimeOffset, MarketDataCandle>();
        var warnings = new List<string>();
        var currentToUtc = request.ToUtc;
        var requests = 0;
        var duplicatesSkipped = 0;
        var truncated = false;

        while (currentToUtc >= request.FromUtc)
        {
            if (requests >= maxRequests)
            {
                truncated = true;
                warnings.Add($"download-history stopped at max_requests={maxRequests}; requested range may be incomplete.");
                break;
            }

            var pageRequest = request with { ToUtc = currentToUtc };
            var page = client.DownloadHistoricalCandles(pageRequest)
                .Where(candle => candle.TimestampUtc >= request.FromUtc && candle.TimestampUtc <= request.ToUtc)
                .OrderBy(candle => candle.TimestampUtc)
                .ToList();
            requests++;

            if (page.Count == 0)
            {
                break;
            }

            foreach (var candle in page)
            {
                if (!candlesByTimestamp.TryAdd(candle.TimestampUtc, candle))
                {
                    duplicatesSkipped++;
                }
            }

            var earliest = page[0].TimestampUtc;
            if (earliest <= request.FromUtc || page.Count < 1000)
            {
                break;
            }

            var nextToUtc = earliest - interval;
            if (nextToUtc >= currentToUtc)
            {
                truncated = true;
                warnings.Add("download-history paging stopped because cTrader returned a non-progressing page.");
                break;
            }

            currentToUtc = nextToUtc;
            Thread.Sleep(50);
        }

        var candles = candlesByTimestamp
            .Values
            .OrderBy(candle => candle.TimestampUtc)
            .ToList();

        return new HistoricalDownloadPageResult(
            Candles: candles,
            Requests: requests,
            DuplicatesSkipped: duplicatesSkipped,
            Truncated: truncated,
            Warnings: warnings);
    }

    private AutopilotDownloadResult DownloadHistoricalRangeForAutopilot(
        StoragePaths storagePaths,
        CTraderOpenApiConfigLoadResult configLoad,
        (CTraderOAuthUrlResult OAuthUrl, CTraderAuthStatus AuthStatus, CTraderAuthTokenState TokenState) authContext,
        EventBus eventBus,
        CTraderHistoricalDataRequest request,
        int maxRequests)
    {
        var config = configLoad.Config;
        var useRealClient = configLoad.LocalConfigLoaded
            && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase);
        var stubActive = !useRealClient;

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadStarted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "Research Autopilot historical download started. Real read-only Open API path."
                    : "Research Autopilot historical download started. Stub fallback; no live Open API call.",
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                stubActive,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var storedToken = tokenStore.LoadToken();
        ICTraderHistoricalDataClient client;
        if (useRealClient)
        {
            if (storedToken is null || !storedToken.HasAccessToken)
            {
                throw new InvalidOperationException(
                    "cTrader OAuth token missing. Autopilot did not download real cTrader data for this range.");
            }

            client = new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken);
        }
        else
        {
            client = new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
        }

        var download = DownloadPagedHistoricalCandles(client, request, maxRequests);
        var importer = new CTraderTrendbarImporter(storagePaths);
        var result = useRealClient
            ? importer.ImportCandles(request, download.Candles, sourceName: "ctrader_openapi_autopilot_paged", stubData: false)
            : importer.ImportStubCandles(request, download.Candles);

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadCompleted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "Research Autopilot historical download completed with real read-only Open API data."
                    : "Research Autopilot historical download completed with stub data. No real cTrader data was loaded.",
                result.DownloadId,
                result.Symbol,
                result.Timeframe,
                result.OutputPath,
                result.CandleCount,
                result.FromUtc,
                result.ToUtc,
                result.StubData,
                requests = download.Requests,
                duplicatesSkipped = download.DuplicatesSkipped,
                download.Truncated,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        return new AutopilotDownloadResult(
            Request: request,
            ImportResult: result,
            Requests: download.Requests,
            DuplicatesSkipped: download.DuplicatesSkipped,
            Truncated: download.Truncated,
            Warnings: CombineWarnings(configLoad.Warnings, authContext.AuthStatus.Warnings, download.Warnings));
    }

    private IReadOnlyList<AutopilotDownloadPlan> BuildAutopilotDownloadPlans(
        IReadOnlyList<ResearchProcessedRange> ranges,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc)
    {
        var plans = new List<AutopilotDownloadPlan>();
        foreach (var symbol in new[] { "XAUUSD", "EURUSD", "GER40" })
        foreach (var timeframe in new[] { "M5", "M15", "H1" })
        {
            var relevant = ranges
                .Where(range => range.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                    && range.Timeframe.Equals(timeframe, StringComparison.OrdinalIgnoreCase)
                    && range.FromUtc is not null
                    && range.ToUtc is not null)
                .ToList();
            if (relevant.Count == 0)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, targetFromUtc, targetToUtc, "missing_range"));
                continue;
            }

            var earliest = relevant.Min(range => range.FromUtc)!.Value;
            var latest = relevant.Max(range => range.ToUtc)!.Value;
            var interval = TimeframeInterval(timeframe);
            if (latest < targetToUtc - interval)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, latest + interval, targetToUtc, "extend_forward"));
            }
            else if (earliest > targetFromUtc + interval)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, targetFromUtc, earliest - interval, "extend_backward"));
            }
        }

        return plans
            .Where(plan => plan.Request.FromUtc < plan.Request.ToUtc)
            .OrderBy(plan => plan.Request.Timeframe == "M5" ? 0 : plan.Request.Timeframe == "M15" ? 1 : 2)
            .ThenBy(plan => plan.Request.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AutopilotDownloadPlan CreateAutopilotPlan(
        string symbol,
        string timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string reason)
    {
        return new AutopilotDownloadPlan(
            new CTraderHistoricalDataRequest(
                Symbol: symbol,
                Timeframe: timeframe,
                FromUtc: fromUtc,
                ToUtc: toUtc),
            reason);
    }

    private ResearchAutopilotReport WriteResearchAutopilotReport(
        StoragePaths storagePaths,
        string reportId,
        DateTimeOffset startedAtUtc,
        double requestedHours,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc,
        int downloadPlans,
        int downloadsAttempted,
        IReadOnlyList<AutopilotDownloadResult> downloads,
        int strategyVariantsTested,
        int strategyResearchEntries,
        string patternCatalogPath,
        string insightsPath,
        string status,
        double elapsedMinutes,
        int iterationsCompleted,
        int workPerformed,
        int idleIterations,
        string nextAction,
        IReadOnlyList<string> warnings)
    {
        var completedAtUtc = DateTimeOffset.UtcNow;
        var report = new ResearchAutopilotReport(
            ReportId: reportId,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            RequestedHours: requestedHours,
            TargetSymbols: ["XAUUSD", "EURUSD", "GER40"],
            TargetTimeframes: ["M5", "M15", "H1"],
            TargetFromUtc: targetFromUtc,
            TargetToUtc: targetToUtc,
            DownloadPlans: downloadPlans,
            DownloadsAttempted: downloadsAttempted,
            CandlesDownloaded: downloads.Sum(download => download.ImportResult.CandleCount),
            DownloadRequests: downloads.Sum(download => download.Requests),
            StrategyVariantsTested: strategyVariantsTested,
            StrategyResearchEntries: strategyResearchEntries,
            PatternCatalogPath: patternCatalogPath,
            InsightsPath: insightsPath,
            Status: warnings.Count == 0 ? status : $"{status}_with_warnings",
            Warnings: warnings.Distinct(StringComparer.Ordinal).Take(80).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ElapsedMinutes: Math.Round(elapsedMinutes, 2),
            IterationsCompleted: iterationsCompleted,
            WorkPerformed: workPerformed,
            IdleIterations: idleIterations,
            NextAction: nextAction);

        var directory = Path.Combine(storagePaths.Root, "strategy_research", "autopilot");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{report.ReportId}.autopilot_report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(Path.Combine(directory, "latest_autopilot_report.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private static bool HasFeatureExports(StoragePaths storagePaths)
    {
        var featuresRoot = Path.Combine(storagePaths.Root, "exports", "features");
        return Directory.Exists(featuresRoot)
            && Directory.EnumerateFiles(featuresRoot, "*.jsonl", SearchOption.AllDirectories).Any();
    }

    private int ImportCsv()
    {
        WriteHeader("Hermes cTrader CSV Import");
        var symbol = ReadOption(_args, "--symbol");
        var timeframe = ReadOption(_args, "--timeframe");
        var file = ReadOption(_args, "--file");

        if (string.IsNullOrWhiteSpace(symbol)
            || string.IsNullOrWhiteSpace(timeframe)
            || string.IsNullOrWhiteSpace(file))
        {
            WriteError("Pflichtargumente fehlen.");
            Console.WriteLine("Beispiel:");
            Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- import-csv --symbol XAUUSD --timeframe M5 --file path/to/file.csv");
            WriteSafety();
            return 2;
        }

        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var importer = new CTraderCsvCandleImporter(storagePaths, eventBus, CliVersion);
        var result = importer.Import(symbol, timeframe, file);
        eventStore.Flush();

        WriteField("Import ID", result.ImportId);
        WriteField("Symbol", result.Symbol);
        WriteField("Timeframe", result.Timeframe);
        WriteField("Format", result.Format.ToString());
        WriteField("Source", result.SourcePath);
        WriteField("Status", result.Validation.IsValid ? "imported" : "failed_validation");
        WriteField("Source Rows", result.Validation.SourceRowCount.ToString());
        WriteField("Imported Rows", result.Validation.ImportedRowCount.ToString());
        WriteField("Invalid Rows", result.Validation.InvalidRowCount.ToString());
        WriteField("From UTC", result.Validation.FromUtc?.ToString("O"));
        WriteField("To UTC", result.Validation.ToUtc?.ToString("O"));
        WriteField("Output", result.OutputPath is null ? "-" : DisplayPath(result.OutputPath));
        WriteField("Raw Copy", result.RawImportPath is null ? "-" : DisplayPath(result.RawImportPath));

        WriteMessages("Missing Columns", result.Validation.MissingColumns);
        WriteMessages("Warnings", result.Validation.Warnings);
        WriteMessages("Invalid Rows", result.Validation.InvalidRows);
        Console.WriteLine();

        WriteSafety();
        return result.Validation.IsValid ? 0 : 1;
    }

    private int ShowSignals()
    {
        WriteHeader("Hermes Signal Results");
        var limit = ReadLimit(_args, 8);
        var files = FindExportFiles("signals").ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Signal-Export-Dateien gefunden.");
            WriteSafety();
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();
        string? inferredTopic = null;

        foreach (var line in ReadRecentJsonlLines(file, limit))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                WriteWarning("Ungueltige Signal-JSONL-Zeile uebersprungen.");
                continue;
            }

            WriteSubHeader($"{GetString(root, "symbol") ?? "UNKNOWN"} - {GetString(root, "direction") ?? "unknown"}");
            WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
            WriteField("Signal Type", GetString(root, "signal_type", "signalType"));
            WriteField("Score", $"{GetDouble(root, "score"):0.##}");
            WriteField("Confidence", $"{GetDouble(root, "confidence") * 100:0}%");
            WriteField("Theoretical Entry", $"{GetDouble(root, "theoretical_entry", "theoreticalEntry"):0.#####}");
            WriteField("Theoretical Stop", $"{GetDouble(root, "theoretical_stop", "theoreticalStop"):0.#####}");
            WriteField("Theoretical Target", $"{GetDouble(root, "theoretical_target", "theoreticalTarget"):0.#####}");
            WriteField("Reason Codes", string.Join(", ", GetStringArray(root, "reason_codes", "reasonCodes")));
            inferredTopic ??= ExtractKnowledgeTopic(
                GetString(root, "signal_type", "signalType"),
                string.Join(" ", GetStringArray(root, "reason_codes", "reasonCodes")),
                GetString(root, "direction"),
                GetString(root, "symbol"),
                GetString(root, "setup_name", "setupName", "pattern_name", "patternName"));
            Console.WriteLine();
        }

        WriteTrustedKnowledgeContext("signal explanation", inferredTopic);

        WriteSafety();
        return 0;
    }

    private int ShowBacktests()
    {
        WriteHeader("Hermes Backtest Reports");
        var limit = ReadLimit(_args, 8);
        var files = FindBacktestReportFiles().TakeLast(limit).ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Backtest-Reports gefunden.");
            WriteSafety();
            return 0;
        }

        foreach (var file in files)
        {
            if (!TryLoadJson(file, out var root))
            {
                WriteWarning($"Backtest-Report nicht lesbar: {DisplayPath(file)}");
                continue;
            }

            WriteSubHeader(GetString(root, "run_id", "runId") ?? Path.GetFileNameWithoutExtension(file));
            WriteField("Symbol", GetString(root, "symbol"));
            WriteField("Timeframe", GetString(root, "timeframe"));
            WriteField("Strategy", GetString(root, "strategy_name", "strategyName"));
            WriteField("Status", GetString(root, "status"));
            WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
            WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
            WriteField("Trade Count", GetInt(root, "trade_count", "tradeCount").ToString());
            WriteField("Winrate", $"{GetDouble(root, "winrate") * 100:0}%");
            WriteField("Profit Factor", $"{GetDouble(root, "profit_factor", "profitFactor"):0.##}");
            WriteField("Max Drawdown", $"{GetDouble(root, "max_drawdown", "maxDrawdown") * 100:0.#}%");
            WriteField("Expectancy", $"{GetDouble(root, "expectancy"):0.##}");
            WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
            WriteField("Notes", GetString(root, "notes"));
            WriteField("Path", DisplayPath(file));
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowOutcomes()
    {
        WriteHeader("Hermes Signal Outcomes");
        var limit = ReadLimit(_args, 8);
        var files = FindOutcomeReportFiles().TakeLast(limit).ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Outcome-Reports gefunden.");
            WriteSafety();
            return 0;
        }

        foreach (var file in files)
        {
            if (!TryLoadJson(file, out var root))
            {
                WriteWarning($"Outcome-Report nicht lesbar: {DisplayPath(file)}");
                continue;
            }

            Console.WriteLine($"Quelle: {DisplayPath(file)}");
            Console.WriteLine();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var outcome in root.EnumerateArray())
                {
                    WriteOutcome(outcome);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                WriteOutcome(root);
            }
        }

        WriteSafety();
        return 0;
    }

    private int ShowMarketData()
    {
        WriteHeader("Hermes Historical Market Data");
        var limit = ReadLimit(_args, 2);
        var candlesRoot = Path.Combine(_dataRoot, "market_data", "candles");
        var files = FindMarketDataCandleFiles().ToList();
        if (files.Count == 0)
        {
            WriteWarning($"Keine historischen Candle-Dateien gefunden: {DisplayPath(candlesRoot)}");
            WriteSafety();
            return 0;
        }

        WriteField("Root", DisplayPath(candlesRoot));
        WriteField("Candle Files", files.Count.ToString());
        WriteField("Rows Total", files.Sum(CountJsonlRows).ToString());
        Console.WriteLine();

        foreach (var file in files)
        {
            var (symbol, timeframe) = ResolveMarketDataIdentity(candlesRoot, file);
            WriteSubHeader($"{symbol} {timeframe}");
            WriteField("Rows", CountJsonlRows(file).ToString());
            WriteField("Path", DisplayPath(file));

            foreach (var line in ReadRecentJsonlLines(file, limit))
            {
                if (!TryParseJsonLine(line, out var root))
                {
                    WriteWarning("Ungueltige Candle-JSONL-Zeile uebersprungen.");
                    continue;
                }

                var timestamp = GetString(root, "timestamp_utc", "timestampUtc") ?? "-";
                var open = GetDouble(root, "open");
                var high = GetDouble(root, "high");
                var low = GetDouble(root, "low");
                var close = GetDouble(root, "close");
                var volume = GetDouble(root, "volume");
                Console.WriteLine(
                    $"  {timestamp}  O {open:0.#####} H {high:0.#####} L {low:0.#####} C {close:0.#####} V {volume:0.##}");
            }

            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowMarketDataStatus()
    {
        WriteHeader("Hermes Market Data Availability");
        var service = new MarketDataAvailabilityService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Scan();
        WriteMarketDataAvailability(service, report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ScanMarketData()
    {
        WriteHeader("Hermes Market Data Scan");
        var service = new MarketDataAvailabilityService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Scan();
        WriteMarketDataAvailability(service, report);
        foreach (var file in report.Files.Take(20))
        {
            WriteMarketDataFile(file);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMarketDataQuality()
    {
        WriteHeader("Hermes Market Data Quality");
        var asset = ReadOption(_args, "--asset") ?? ScalpingResearchService.DefaultAsset;
        var service = new MarketDataAvailabilityService(BuildStoragePaths(), _runtimeRoot);
        var report = service.BuildQuality(asset);
        WriteMarketDataQuality(service, report);
        foreach (var file in report.Files.Take(20))
        {
            WriteMarketDataFile(file);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int NormalizeMarketData()
    {
        WriteHeader("Hermes Market Data Normalization");
        var asset = ReadOption(_args, "--asset") ?? ScalpingResearchService.DefaultAsset;
        var service = new MarketDataAvailabilityService(BuildStoragePaths(), _runtimeRoot);
        var result = service.Normalize(asset);
        WriteField("Asset", result.Asset);
        WriteField("Files Processed", result.FilesProcessed.ToString());
        WriteField("Candles Written", result.CandlesWritten.ToString());
        WriteField("Invalid Candles", result.InvalidCandles.ToString());
        WriteMessages("Outputs", result.OutputPaths.Select(DisplayPath).ToList());
        WriteMessages("Data Gaps", result.DataGaps);
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("broker_orders_enabled", result.BrokerOrdersEnabled.ToString().ToLowerInvariant());
        WriteField("live_trading_enabled", result.LiveTradingEnabled.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainMarketDataGap()
    {
        WriteHeader("Hermes Market Data Gap Explanation");
        var asset = ReadOption(_args, "--asset") ?? ScalpingResearchService.DefaultAsset;
        var service = new MarketDataAvailabilityService(BuildStoragePaths(), _runtimeRoot);
        WriteField("Asset", asset.ToUpperInvariant());
        WriteMessages("Reasons", service.ExplainGap(asset));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowVersion()
    {
        WriteHeader("Hermes CLI Version");
        WriteField("Hermes CLI", CliVersion);
        WriteField("Runtime Root", _runtimeRoot);

        var projectPath = Path.Combine(_runtimeRoot, "Hermes.Runtime.csproj");
        if (File.Exists(projectPath))
        {
            try
            {
                var project = XDocument.Load(projectPath);
                var targetFramework = project.Descendants("TargetFramework").FirstOrDefault()?.Value;
                var assemblyName = project.Descendants("AssemblyName").FirstOrDefault()?.Value;
                WriteField("Runtime Assembly", assemblyName ?? "Hermes.Runtime");
                WriteField("Target Framework", targetFramework ?? "unknown");
            }
            catch
            {
                WriteWarning("Runtime-Projektdatei konnte nicht gelesen werden.");
            }
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int UnknownCommand(string command)
    {
        WriteError($"Unbekanntes Kommando: {command}");
        Console.WriteLine("Nutze: hermes help");
        return 2;
    }

    private IEnumerable<string> FindEventFiles()
    {
        var files = new List<string>();
        var runtimeEvents = Path.Combine(_dataRoot, "events", "runtime");
        if (Directory.Exists(runtimeEvents))
        {
            files.AddRange(Directory.EnumerateFiles(runtimeEvents, "*.jsonl"));
        }

        var legacyEvents = Path.Combine(_dataRoot, "events");
        if (Directory.Exists(legacyEvents))
        {
            files.AddRange(Directory.EnumerateFiles(legacyEvents, "*.jsonl"));
        }

        foreach (var file in files.OrderBy(File.GetLastWriteTimeUtc).ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindExportFiles(string exportType)
    {
        var directory = Path.Combine(_dataRoot, "exports", exportType);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindBacktestReportFiles()
    {
        var directory = Path.Combine(_dataRoot, "reports", "backtests");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindOutcomeReportFiles()
    {
        var directory = Path.Combine(_dataRoot, "reports", "outcomes");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindNightlyResearchReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "nightly");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.nightly.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindResearchSummaryReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "research");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.research_summary.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindBetaReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "beta");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.beta_report.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindMarketDataCandleFiles()
    {
        var directory = Path.Combine(_dataRoot, "market_data", "candles");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories)
                     .OrderBy(path => path))
        {
            yield return file;
        }
    }

    private static (string Symbol, string Timeframe) ResolveMarketDataIdentity(string candlesRoot, string file)
    {
        var relative = Path.GetRelativePath(candlesRoot, file);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length >= 3)
        {
            return (parts[0], parts[1]);
        }

        if (parts.Length >= 2)
        {
            var timeframe = Path.GetFileName(file).Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "-";
            return (parts[0], timeframe);
        }

        return ("UNKNOWN", Path.GetFileNameWithoutExtension(file));
    }

    private void WriteOutcome(JsonElement root)
    {
        WriteSubHeader(GetString(root, "outcome_id", "outcomeId") ?? "unknown_outcome");
        WriteField("Signal ID", GetString(root, "signal_id", "signalId"));
        WriteField("Symbol", GetString(root, "symbol"));
        WriteField("Timeframe", GetString(root, "timeframe"));
        WriteField("Direction", GetString(root, "direction"));
        WriteField("Outcome", GetString(root, "outcome_status", "outcomeStatus"));
        WriteField("Hit Target", GetBoolText(root, "hit_target", "hitTarget"));
        WriteField("Hit Stop", GetBoolText(root, "hit_stop", "hitStop"));
        WriteField("Expired", GetBoolText(root, "expired"));
        WriteField("Invalidated", GetBoolText(root, "invalidated"));
        WriteField("MFE", $"{GetDouble(root, "mfe"):0.##} R");
        WriteField("MAE", $"{GetDouble(root, "mae"):0.##} R");
        WriteField("Final R", $"{GetDouble(root, "final_r", "finalR"):0.##} R");
        WriteField("Evaluated UTC", GetString(root, "evaluated_at_utc", "evaluatedAtUtc"));
        WriteField("Notes", GetString(root, "notes"));
        Console.WriteLine();
    }

    private void WriteResearchReport(ResearchSummaryReport report)
    {
        WriteField("Run ID", report.RunId);
        WriteField("Status", report.Status);
        WriteField("Started UTC", report.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", report.CompletedAtUtc.ToString("O"));
        WriteField("Symbols Processed", report.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", report.SymbolsProcessed));
        WriteField("Candles Processed", report.CandlesProcessed.ToString());
        WriteField("Duration", $"{report.DurationSeconds:0.###} s");
        WriteField("Features", report.FeaturesGenerated.ToString());
        WriteField("Signals", report.SignalsGenerated.ToString());
        WriteField("Outcomes", report.OutcomesGenerated.ToString());
        WriteField("Backtests", report.BacktestsGenerated.ToString());
        WriteField("Reports", report.ReportsGenerated.ToString());
        WriteField("Feature Output", DisplayOptionalPath(report.FeatureOutputPath));
        WriteField("Signal Output", DisplayOptionalPath(report.SignalOutputPath));
        WriteField("Outcome Report", DisplayOptionalPath(report.OutcomeReportPath));
        WriteField("Backtest Report", DisplayOptionalPath(report.BacktestReportPath));
        WriteField("Nightly Report", DisplayOptionalPath(report.NightlyReportPath));
        WriteField("Research Report", DisplayOptionalPath(report.ResearchReportPath));
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
    }

    private void WriteBetaReport(TradingLearningBetaReport report)
    {
        WriteField("Run ID", report.RunId);
        WriteField("Status", report.Status);
        WriteField("Started UTC", report.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", report.CompletedAtUtc.ToString("O"));
        WriteField("Symbols Processed", report.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", report.SymbolsProcessed));
        WriteField("Candles Processed", report.CandlesProcessed.ToString());
        WriteField("Features", report.FeaturesGenerated.ToString());
        WriteField("Signals", report.SignalsGenerated.ToString());
        WriteField("Outcomes", report.OutcomesGenerated.ToString());
        WriteField("Backtests", report.BacktestsGenerated.ToString());
        WriteField("Duration", $"{report.DurationSeconds:0.###} s");
        WriteField("learning_ready", report.LearningReady.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Beta Report", DisplayOptionalPath(report.BetaReportPath));
        WriteField("Research Report", DisplayOptionalPath(report.ResearchReportPath));
        WriteField("Feature Output", DisplayOptionalPath(report.FeatureOutputPath));
        WriteField("Signal Output", DisplayOptionalPath(report.SignalOutputPath));
        WriteField("Outcome Report", DisplayOptionalPath(report.OutcomeReportPath));
        WriteField("Backtest Report", DisplayOptionalPath(report.BacktestReportPath));
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
    }

    private static IReadOnlyList<string> ReadRecentJsonlLines(string file, int limit)
    {
        return File.ReadLines(file)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(limit)
            .ToList();
    }

    private static int CountJsonlRows(string file)
    {
        try
        {
            return File.ReadLines(file).Count(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadLimit(string[] args, int fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--limit" && int.TryParse(args[index + 1], out var value))
            {
                return Math.Clamp(value, 1, 100);
            }
        }

        return fallback;
    }

    private static double ReadHours(string[] args, double fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals("--hours", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return Math.Clamp(value, 0.01, 168);
            }
        }

        return fallback;
    }

    private static double ReadDoubleOption(string[] args, string name, double fallback, double min, double max)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return Math.Clamp(value, min, max);
            }
        }

        return fallback;
    }

    private static int ReadIntOption(string[] args, string name, int fallback, int min, int max)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[index + 1], out var value))
            {
                return Math.Clamp(value, min, max);
            }
        }

        return fallback;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private bool HasArg(string name)
    {
        return _args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private SchedulerWindowConfig? BuildWindowUpdate(string prefix)
    {
        var start = ReadOption(_args, $"--{prefix}-start");
        var end = ReadOption(_args, $"--{prefix}-end");
        var enabledText = ReadOption(_args, $"--{prefix}-enabled");

        if (string.IsNullOrWhiteSpace(start)
            && string.IsNullOrWhiteSpace(end)
            && string.IsNullOrWhiteSpace(enabledText))
        {
            return null;
        }

        var enabled = string.IsNullOrWhiteSpace(enabledText)
            || (!enabledText.Equals("false", StringComparison.OrdinalIgnoreCase)
                && !enabledText.Equals("0", StringComparison.OrdinalIgnoreCase)
                && !enabledText.Equals("off", StringComparison.OrdinalIgnoreCase));

        return new SchedulerWindowConfig(
            Start: string.IsNullOrWhiteSpace(start) ? "00:00" : start!,
            End: string.IsNullOrWhiteSpace(end) ? "00:00" : end!,
            Enabled: enabled);
    }

    private static IReadOnlyList<string>? ParseWeekdays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ReadAssetList(string[] args, string name)
    {
        var value = ReadOption(args, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim().ToUpperInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static bool TryParseCliDate(string value, out DateTimeOffset timestampUtc)
    {
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, styles, out var parsed))
        {
            timestampUtc = parsed.ToUniversalTime();
            return true;
        }

        timestampUtc = default;
        return false;
    }

    private static TimeSpan TimeframeInterval(string timeframe) =>
        timeframe.ToUpperInvariant() switch
        {
            "H4" => TimeSpan.FromHours(4),
            "H1" => TimeSpan.FromHours(1),
            "M15" => TimeSpan.FromMinutes(15),
            "M5" => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(5)
        };

    private static void WriteMessages(string label, IReadOnlyList<string> messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return;
        }

        WriteField(label, string.Empty);
        foreach (var message in messages)
        {
            Console.WriteLine($"  - {message}");
        }
    }

    private bool TryLoadJson(string path, out JsonElement root)
    {
        root = default;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonLine(string line, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static int GetInt(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(GetString(root, names), out var parsed) ? parsed : 0;
    }

    private static double GetDouble(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(GetString(root, names), out var parsed) ? parsed : 0;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private static string GetBoolText(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return "unknown";
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => GetString(root, names) ?? "unknown"
        };
    }

    private static bool JsonBool(JsonElement root, bool fallback, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static bool SafetyFlagTrue(IEnumerable<JsonElement> roots, params string[] names)
    {
        foreach (var root in roots.Where(root => root.ValueKind == JsonValueKind.Object))
        {
            if (TryGetProperty(root, out var value, names))
            {
                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.String
                    && bool.TryParse(value.GetString(), out var parsed)
                    && !parsed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int GetArrayCount(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.GetArrayLength(),
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static int FirstPositive(params int[] values) => values.FirstOrDefault(value => value > 0);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static List<string> CombineStringLists(params IEnumerable<string>[] groups) =>
        groups
            .SelectMany(group => group)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string FirstCommand(string[] args) => CommandAt(args, 0);

    private static string CommandAt(string[] args, int commandIndex)
    {
        var commands = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg is "--root" or "--limit" or "--hours" or "--max-runtime-hours" or "--max-requests" or "--max-downloads" or "--sleep-seconds" or "--max-idle-iterations" or "--from" or "--to" or "--url" or "--dataset" or "--asset" or "--timeframe")
            {
                index++;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                commands.Add(arg);
            }
        }

        return commandIndex < commands.Count ? commands[commandIndex] : string.Empty;
    }

    private sealed record HistoricalDownloadPageResult(
        IReadOnlyList<MarketDataCandle> Candles,
        int Requests,
        int DuplicatesSkipped,
        bool Truncated,
        IReadOnlyList<string> Warnings);

    private sealed record AutopilotDownloadPlan(
        CTraderHistoricalDataRequest Request,
        string Reason);

    private sealed record AutopilotDownloadResult(
        CTraderHistoricalDataRequest Request,
        CTraderTrendbarImportResult ImportResult,
        int Requests,
        int DuplicatesSkipped,
        bool Truncated,
        IReadOnlyList<string> Warnings);

    private sealed record AutopilotIterationResult(
        int Iteration,
        int DownloadPlans,
        int DownloadsAttempted,
        IReadOnlyList<AutopilotDownloadResult> Downloads,
        TradingLearningBetaReport? BetaReport,
        ResearchMemoryIndex Index,
        StrategyResearchStepResult StrategyResearch,
        int SimulationReports,
        WalkForwardValidationReport WalkForward,
        StrategyDiscoveryReport Discovery,
        IReadOnlyList<string> Warnings,
        bool WorkPerformed,
        int WorkUnits,
        string Status,
        string NextAction);

    private sealed record StrategyResearchStepResult(
        StrategyResearchMemory Memory,
        int TestedNow,
        string InsightsPath,
        string ClustersPath,
        StrategyEvolutionSummary Insights);

    private sealed record PlannedTaskExecutionRun(
        string ExecutionStatePath,
        string ExecutionLogPath,
        PlannedTaskExecutionState State,
        IReadOnlyList<PlannedTaskExecutionResult> Results,
        int Completed,
        int Skipped,
        int Failed,
        int PendingAfter);

    private static string ResolveRuntimeRoot(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--root")
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var directProject = Path.Combine(directory.FullName, "Hermes.Runtime.csproj");
                if (File.Exists(directProject))
                {
                    return directory.FullName;
                }

                var nestedProject = Path.Combine(directory.FullName, "HermesRuntime", "Hermes.Runtime.csproj");
                if (File.Exists(nestedProject))
                {
                    return Path.Combine(directory.FullName, "HermesRuntime");
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private StoragePaths BuildStoragePaths()
    {
        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (File.Exists(profilePath))
        {
            var paths = StorageProfile.Load(profilePath).ToPaths(Path.GetDirectoryName(profilePath) ?? _runtimeRoot);
            EnsureStorageDirectories(paths);
            return paths;
        }

        var fallbackPaths = BuildFallbackStoragePaths(_dataRoot);
        EnsureStorageDirectories(fallbackPaths);
        return fallbackPaths;
    }

    private AutonomousLearningLoop BuildAutonomousLearningLoop() =>
        new(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));

    private StoragePaths BuildReadOnlyStoragePaths()
    {
        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (File.Exists(profilePath))
        {
            return StorageProfile.Load(profilePath).ToPaths(Path.GetDirectoryName(profilePath) ?? _runtimeRoot);
        }

        return BuildFallbackStoragePaths(_dataRoot);
    }

    private static string ResolveDataRoot(string runtimeRoot)
    {
        var profilePath = Path.Combine(runtimeRoot, "config", "storage.profile.json");
        if (!File.Exists(profilePath))
        {
            return Path.Combine(runtimeRoot, "data");
        }

        try
        {
            return StorageProfile.Load(profilePath)
                .ToPaths(Path.GetDirectoryName(profilePath) ?? runtimeRoot)
                .Root;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return Path.Combine(runtimeRoot, "data");
        }
    }

    private static StoragePaths BuildFallbackStoragePaths(string dataRoot)
    {
        return new StoragePaths(
            Root: dataRoot,
            Events: Path.Combine(dataRoot, "events"),
            Snapshots: Path.Combine(dataRoot, "snapshots"),
            Logs: Path.Combine(dataRoot, "logs"),
            Cache: Path.Combine(dataRoot, "cache"),
            Jobs: Path.Combine(dataRoot, "jobs"),
            Archive: Path.Combine(dataRoot, "archive"));
    }

    private static void EnsureStorageDirectories(StoragePaths paths)
    {
        foreach (var directory in paths.AllDirectories)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private CTraderOpenApiConfigLoadResult LoadCTraderConfig()
    {
        var loader = new CTraderOpenApiConfigLoader();
        return loader.Load(_runtimeRoot);
    }

    private static (
        CTraderOAuthUrlResult OAuthUrl,
        CTraderAuthStatus AuthStatus,
        CTraderAuthTokenState TokenState) BuildCTraderAuthContext(
            CTraderOpenApiConfigLoadResult configLoad,
            StoragePaths storagePaths)
    {
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var authStatus = tokenStore.GetStatus(configLoad.Config, configLoad, oauthUrl);
        var tokenState = new CTraderAuthTokenState(
            AuthConfigured: authStatus.AuthConfigured,
            TokenAvailable: authStatus.TokenLoaded,
            AuthMode: authStatus.AuthMode,
            TokenCachePath: authStatus.TokenStorePath,
            Warnings: authStatus.Warnings);

        return (oauthUrl, authStatus, tokenState);
    }

    private static IReadOnlyList<string> CombineWarnings(params IEnumerable<string>[] groups)
    {
        return groups
            .SelectMany(group => group)
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void PublishCTraderEvent(
        EventBus eventBus,
        EventType eventType,
        EventSeverity severity,
        object payload)
    {
        eventBus.Publish(EventEnvelope.Create(
            eventType,
            "hermes_ctrader_openapi",
            severity,
            CliVersion,
            payload));
    }

    private static long DirectorySize(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    private string DisplayPath(string path)
    {
        var relative = Path.GetRelativePath(_runtimeRoot, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
    }

    private string DisplayOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "-" : DisplayPath(path);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static void WriteHeader(string text)
    {
        WriteColored(text, ConsoleColor.Cyan);
        Console.WriteLine(new string('-', text.Length));
    }

    private static void WriteSubHeader(string text)
    {
        WriteColored(text, ConsoleColor.DarkCyan);
    }

    private static void WriteField(string label, string? value)
    {
        Console.Write(label.PadRight(24));
        Console.WriteLine(value ?? "-");
    }

    private static void WriteEvent(string timestamp, string severity, string eventType, string source, string message)
    {
        Console.Write($"{timestamp} ");
        WriteColored($"[{severity}]", SeverityColor(severity), newline: false);
        Console.WriteLine($" {eventType} ({source})");
        Console.WriteLine($"  {message}");
    }

    private static void WriteSafety()
    {
        Console.WriteLine("Safety: keine Trading-Ausfuehrung, keine Broker-Orders, no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true.");
    }

    private static void WriteWarning(string message) => WriteColored($"WARN: {message}", ConsoleColor.Yellow);

    private static void WriteError(string message) => WriteColored($"ERROR: {message}", ConsoleColor.Red);

    private static ConsoleColor SeverityColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "warning" or "warn" => ConsoleColor.Yellow,
            "error" or "critical" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    private static void WriteColored(string text, ConsoleColor color, bool newline = true)
    {
        if (Console.IsOutputRedirected)
        {
            if (newline)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.Write(text);
            }

            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newline)
        {
            Console.WriteLine(text);
        }
        else
        {
            Console.Write(text);
        }

        Console.ForegroundColor = previous;
    }
    private int ShowPromotionStatus()
    {
        WriteHeader("Hermes Knowledge Promotion Status");
        var storagePaths = BuildStoragePaths();
        var engine = new KnowledgePromotionEngine(storagePaths);
        var status = engine.BuildStatus();

        WriteField("Promotion Health", status.PromotionHealth);
        Console.WriteLine();

        Console.WriteLine();
        Console.WriteLine("Knowledge Distribution:");;
        WriteField("Weak", status.WeakKnowledge.ToString());
        WriteField("Promising", status.PromisingKnowledge.ToString());
        WriteField("Robust", status.RobustKnowledge.ToString());
        WriteField("Trusted", status.TrustedKnowledge.ToString());
        WriteField("Deprecated", status.DeprecatedKnowledge.ToString());
        WriteField("Rejected", status.RejectedKnowledge.ToString());
        Console.WriteLine();

        Console.WriteLine();
        Console.WriteLine("Trusted Candidates:");;
        WriteField("Total Candidates", status.TrustedCandidates.TotalCandidates.ToString());
        WriteField("Ready for Promotion", status.TrustedCandidates.ReadyForPromotion.ToString());
        WriteField("Awaiting Human Review", status.TrustedCandidates.AwaitingHumanReview.ToString());
        WriteField("Blocked", status.TrustedCandidates.BlockedCandidates.ToString());
        Console.WriteLine();

        if (status.PromotionBlockers.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Top Promotion Blockers:");;
            foreach (var blocker in status.PromotionBlockers.Take(10))
            {
                Console.WriteLine($"  - {blocker}");
            }
            Console.WriteLine();
        }

        if (status.RecentPromotions.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Recent Promotions:");;
            foreach (var promo in status.RecentPromotions.Take(5))
            {
                Console.WriteLine($"  {promo.KnowledgeId}");
                Console.WriteLine($"    {promo.CurrentStatus} → {promo.RecommendedStatus}");
                Console.WriteLine($"    Trust: {promo.CurrentTrustScore:0.####}, Quality: {promo.CurrentQualityScore:0.####}");
                Console.WriteLine($"    Reason: {promo.DecisionReason}");
                Console.WriteLine();
            }
        }

        WriteField("Promotion Log", status.PromotionLogPath);
        WriteField("Status Path", engine.PromotionStatusPath);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeTrustPromotionStatus()
    {
        WriteHeader("Hermes Knowledge Trust Promotion Pipeline");
        var storagePaths = BuildStoragePaths();
        var service = new KnowledgeTrustPromotionPipelineService(storagePaths);
        var report = service.Run(apply: false);

        WriteKnowledgeTrustPromotionReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnowledgeTrustPromotion()
    {
        WriteHeader("Hermes Knowledge Trust Promotion Pipeline");
        var storagePaths = BuildStoragePaths();
        var service = new KnowledgeTrustPromotionPipelineService(storagePaths);
        var apply = HasArg("--apply");
        var dryRun = HasArg("--dry-run");
        var skipRefresh = HasArg("--skip-refresh");
        var maxSeconds = ReadIntOption(_args, "--max-seconds", apply ? 60 : 0, 0, 3600);
        int? applyTimeout = maxSeconds > 0 ? maxSeconds : null;

        if (apply && dryRun)
        {
            Console.WriteLine("Error: use either --dry-run or --apply, not both.");
            return 1;
        }

        var report = service.Run(apply: apply && !dryRun, maxSeconds: applyTimeout, skipRefresh: skipRefresh);
        WriteKnowledgeTrustPromotionReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return report.Status.Equals("blocked_promotion_apply_timeout", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int ShowKnowledgeStateConsistencyStatus()
    {
        WriteHeader("Hermes Knowledge State Consistency");
        var service = new KnowledgeStateConsistencyService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();
        WriteKnowledgeStateConsistencyReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnowledgeStateConsistencyCheck()
    {
        WriteHeader("Hermes Knowledge State Consistency");
        var service = new KnowledgeStateConsistencyService(BuildStoragePaths(), _runtimeRoot);
        var report = service.Run(apply: false, dryRun: true);
        WriteKnowledgeStateConsistencyReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunKnowledgeStateConsistencyRepair()
    {
        WriteHeader("Hermes Knowledge State Consistency");
        var service = new KnowledgeStateConsistencyService(BuildStoragePaths(), _runtimeRoot);
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var report = service.Run(apply: apply, dryRun: dryRun);
        WriteKnowledgeStateConsistencyReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowNextTrustedCandidatesStatus()
    {
        WriteHeader("Hermes Next Trusted Candidates");
        var service = new NextTrustedCandidatesService(BuildStoragePaths());
        var report = service.Run();
        WriteNextTrustedCandidatesReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMultiSourceEvidenceStatus()
    {
        WriteHeader("Hermes Multi-Source Evidence Ingestion");
        var storagePaths = BuildStoragePaths();
        var service = new MultiSourceEvidenceIngestionService(storagePaths);
        var report = service.Run(apply: false, dryRun: true);

        WriteMultiSourceEvidenceReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteKnowledgeStateConsistencyReport(KnowledgeStateConsistencyReport report, KnowledgeStateConsistencyService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("Loaded Catalog Items", report.LoadedCatalogItems.ToString());
        WriteField("Loaded Quality Items", report.LoadedQualityItems.ToString());
        WriteField("Loaded Evidence Items", report.LoadedEvidenceItems.ToString());
        WriteField("Loaded Source Confirmations", report.LoadedSourceConfirmationItems.ToString());
        WriteField("Loaded Validation Status", report.LoadedValidationStatusItems.ToString());
        WriteField("Loaded Validation Plans", report.LoadedValidationPlans.ToString());
        WriteField("Loaded Promotion Entries", report.LoadedPromotionEntries.ToString());
        WriteField("Loaded Master Status Snapshots", report.LoadedMasterStatusSnapshots.ToString());
        WriteField("Source Count Mismatches", report.SourceCountMismatches.ToString());
        WriteField("Trusted Status Mismatches", report.TrustedStatusMismatches.ToString());
        WriteField("Timestamp Mismatches", report.TimestampMismatches.ToString());
        WriteField("Blocker Mismatches", report.BlockerMismatches.ToString());
        WriteField("Missing Item ID Mismatches", report.MissingItemIdMismatches.ToString());
        WriteField("Repaired Items", report.RepairedItems.ToString());
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("Applied", report.Applied.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        WriteMessages("Remaining Issues", report.RemainingIssues);
        foreach (var target in new[] { "trading:bearish_engulfing", "trading:liquidity_sweep", "trading:inside_bar" })
        {
            var item = report.Items.FirstOrDefault(entry => entry.KnowledgeId.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                WriteField(target, "not found");
                continue;
            }

            WriteSubHeader($"{item.Title} / {item.KnowledgeId}");
            WriteField("Source Count", $"{item.SourceCountBefore} -> {item.SourceCountExpected}");
            WriteField("Catalog Validation Status", item.CatalogValidationStatus);
            WriteField("Quality Lifecycle Status", item.QualityLifecycleStatus);
            WriteField("Source Confirmation Status", item.SourceConfirmationStatus);
            WriteField("Validation Plan Status", item.ValidationPlanStatus);
            WriteField("Promotion Status", item.PromotionStatus);
            WriteField("Validation Score", $"{item.ValidationScore:0.###}");
            WriteField("Trust Score", $"{item.TrustScore:0.###}");
            WriteField("Quality Score", $"{item.QualityScore:0.###}");
            WriteField("Last Validated UTC", item.LastValidatedUtc?.ToString("O") ?? "-");
            WriteField("Latest Validation UTC", item.LatestValidationExecutionUtc?.ToString("O") ?? "-");
            WriteField("Policy Approved Second Source", item.PolicyApprovedSecondSource.ToString().ToLowerInvariant());
            WriteField("Has Validation Executions", item.HasValidationExecutions.ToString().ToLowerInvariant());
            WriteField("Current Blockers", string.Join(", ", item.CurrentBlockers));
            WriteField("Expected Blockers", string.Join(", ", item.ExpectedBlockers));
            WriteField("Recommended Next Action", item.RecommendedNextAction);
            WriteMessages("Warnings", item.Warnings);
        }
    }

    private void WriteNextTrustedCandidatesReport(NextTrustedCandidatesReport report, NextTrustedCandidatesService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("Total Items", report.TotalItems.ToString());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Source Confirmations", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Knowledge Quality", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Knowledge Evidence", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Validation Plans", DisplayPath(report.ValidationPlansPath));
        WriteField("Promotion Report", DisplayPath(report.PromotionReportPath));
        WriteMessages("Next Actions", report.NextActions.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        WriteMessages("Blocker Counts", report.BlockerCounts.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        WriteMessages("Warnings", report.Warnings);

        foreach (var item in report.Items)
        {
            WriteSubHeader($"{item.Title} / {item.KnowledgeId}");
            WriteField("Domain", item.Domain);
            WriteField("Current Status", item.CurrentStatus);
            WriteField("Recommended Status", item.RecommendedStatus);
            WriteField("Promotion Outcome", item.PromotionOutcome);
            WriteField("Eligible For Promotion", item.EligibleForPromotion.ToString().ToLowerInvariant());
            WriteField("Source Count", item.SourceCount.ToString());
            WriteField("Policy Approved Source Count", item.PolicyApprovedSourceCount.ToString());
            WriteField("Trust Score", item.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Quality Score", item.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Validation Score", item.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Validation Plan Status", item.ValidationPlanStatus);
            WriteField("Next Action", item.NextAction);
            WriteMessages("Best Candidate Sources", item.BestCandidateSources);
            WriteMessages("Missing Evidence", item.MissingEvidence);
            WriteMessages("Contradictions", item.Contradictions);
            WriteMessages("Blockers", item.Blockers);
            WriteMessages("Warnings", item.Warnings);
        }
    }

    private int ShowMultiSourceEvidencePlan()
    {
        WriteHeader("Hermes Multi-Source Evidence Ingestion");
        var storagePaths = BuildStoragePaths();
        var service = new MultiSourceEvidenceIngestionService(storagePaths);
        var report = service.Run(apply: false, dryRun: true);

        WriteMultiSourceEvidenceReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunMultiSourceEvidenceApply()
    {
        WriteHeader("Hermes Multi-Source Evidence Ingestion");
        var storagePaths = BuildStoragePaths();
        var service = new MultiSourceEvidenceIngestionService(storagePaths);
        var apply = HasArg("--apply");
        var dryRun = HasArg("--dry-run");

        if (apply && dryRun)
        {
            Console.WriteLine("Error: use either --dry-run or --apply, not both.");
            return 1;
        }

        var report = service.Run(apply: apply && !dryRun, dryRun: dryRun || !apply);
        WriteMultiSourceEvidenceReport(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCanonicalEvidenceStatus()
    {
        WriteHeader("Hermes Canonical Evidence Acquisition");
        var service = new CanonicalEvidenceAcquisitionPipelineService(BuildStoragePaths(), _runtimeRoot);
        var report = service.LoadStatus();

        WriteCanonicalEvidenceAcquisitionReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunCanonicalEvidenceRun()
    {
        WriteHeader("Hermes Canonical Evidence Acquisition");
        var service = new CanonicalEvidenceAcquisitionPipelineService(BuildStoragePaths(), _runtimeRoot);
        var apply = HasArg("--apply") && !HasArg("--dry-run");
        var dryRun = HasArg("--dry-run") || !HasArg("--apply");
        var maxItems = Math.Max(1, ReadIntOption(_args, "--max-items", 10, 1, 1000));
        var maxFetchSeconds = Math.Max(5, ReadIntOption(_args, "--max-fetch-seconds", 120, 5, 3600));
        var timeout = TimeSpan.FromSeconds(Math.Max(30, maxFetchSeconds + 20));
        var runTask = Task.Run(() => service.Run(maxItems, apply, dryRun, maxFetchSeconds));
        if (!runTask.Wait(timeout))
        {
            Console.WriteLine($"Error: canonical-evidence-run timed out after {timeout.TotalSeconds:0} seconds.");
            Console.WriteLine("Last successful stage is expected to be reported by the latest written pipeline report.");
            Console.WriteLine();
            WriteSafety();
            return 1;
        }

        var report = runTask.Result;

        WriteCanonicalEvidenceAcquisitionReport(report, service);
        Console.WriteLine();
        WriteSafety();
        return report.Status.Equals("blocked_external_fetch_timeout", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private int ShowTrustedCandidates()
    {
        WriteHeader("Hermes Trusted Knowledge Candidates");
        var storagePaths = BuildStoragePaths();
        var engine = new KnowledgePromotionEngine(storagePaths);
        var report = engine.BuildTrustedCandidates();

        WriteField("Total Candidates", report.TotalCandidates.ToString());
        WriteField("Ready for Promotion", report.ReadyForPromotion.ToString());
        WriteField("Awaiting Human Review", report.AwaitingHumanReview.ToString());
        WriteField("Blocked", report.BlockedCandidates.ToString());
        Console.WriteLine();

        if (report.TopBlockers.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Top Blockers:");;
            foreach (var blocker in report.TopBlockers.Take(10))
            {
                Console.WriteLine($"  - {blocker}");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Candidates:");;
        foreach (var candidate in report.Candidates.Take(20))
        {
            Console.WriteLine($"  {candidate.KnowledgeId}");
            Console.WriteLine($"    Current: {candidate.CurrentStatus}");
            Console.WriteLine($"    Recommended: {candidate.RecommendedStatus}");
            Console.WriteLine($"    Trust: {candidate.CurrentTrustScore:0.####}, Quality: {candidate.CurrentQualityScore:0.####}");
            Console.WriteLine($"    Expected Trust Delta: +{candidate.ExpectedTrustDelta:0.####}");
            
            if (candidate.HumanReviewRequired)
            {
                Console.WriteLine($"    ⚠ Human Review Required");
            }

            if (candidate.Blockers.Count > 0)
            {
                Console.WriteLine($"    Blockers: {string.Join(", ", candidate.Blockers.Take(3))}");
            }
            else if (candidate.UnsatisfiedConditions.Count > 0)
            {
                Console.WriteLine($"    Missing: {string.Join(", ", candidate.UnsatisfiedConditions.Take(3))}");
            }

            Console.WriteLine();
        }

        WriteField("Candidates Path", report.CandidatesPath);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteCanonicalEvidenceAcquisitionReport(CanonicalEvidenceAcquisitionReport report, CanonicalEvidenceAcquisitionPipelineService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("External Fetch Timeouts", report.ExternalFetchTimeouts.ToString());
        WriteField("Skipped Due To Timeout", report.SkippedDueToTimeout.ToString());
        WriteField("Fetch Duration Ms", report.FetchDurationMs.ToString());
        WriteField("Last Successful Stage", report.LastSuccessfulStage);
        WriteMessages("Affected Items", report.AffectedItems);
        WriteField("Loaded Items", report.LoadedItems.ToString());
        WriteField("Considered Items", report.ConsideredItems.ToString());
        WriteField("Total Second Source Items", report.TotalSecondSourceItems.ToString());
        WriteField("Evidence Candidates Found", report.EvidenceCandidatesFound.ToString());
        WriteField("Semantic Matches", report.SemanticMatches.ToString());
        WriteField("Independent Sources Found", report.IndependentSourcesFound.ToString());
        WriteField("Policy Approved Sources", report.PolicyApprovedSources.ToString());
        WriteField("Source Count Increased Items", report.SourceCountIncreasedItems.ToString());
        WriteField("Rejected Low Relevance", report.RejectedLowRelevance.ToString());
        WriteField("Rejected Same Domain", report.RejectedSameDomain.ToString());
        WriteField("Rejected Policy", report.RejectedPolicy.ToString());
        WriteField("Loaded Requests", report.LoadedRequests.ToString());
        WriteField("Exported Search Requests", report.ExportedSearchRequests.ToString());
        WriteField("Accepted Import Candidates", report.AcceptedImportCandidates.ToString());
        WriteField("Rejected Import Candidates", report.RejectedImportCandidates.ToString());
        WriteField("Validation Synchronized Items", report.ValidationSynchronizedItems.ToString());
        WriteField("Trusted Promotion Eligible Items", report.TrustedPromotionEligibleItems.ToString());
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("Applied", report.Applied.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteMessages("Next Actions", report.NextActions.ToList());
        WriteMessages("Top Rejection Reasons", report.TopRejectionReasons.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        WriteMessages("Warnings", report.Warnings);

        Console.WriteLine();
        Console.WriteLine("Per Item Trace:");
        foreach (var item in report.PerItemTrace.Take(20))
        {
            Console.WriteLine($"  {item.KnowledgeItemId} / {item.Domain}");
            Console.WriteLine($"    Source Count: {item.SourceCountBefore} -> {item.SourceCountAfter}");
            Console.WriteLine($"    Trust: {item.TrustScore:0.###}, Quality: {item.QualityScore:0.###}, Validation: {item.ValidationScore:0.###}");
            Console.WriteLine($"    Query: {item.Query}");
            Console.WriteLine($"    Recommended Domains: {string.Join(", ", item.RecommendedSourceDomains)}");
            Console.WriteLine($"    Query Terms: {string.Join(", ", item.QueryTerms)}");
            Console.WriteLine($"    Catalog Sources Used: {string.Join(", ", item.CatalogSourcesUsed)}");
            Console.WriteLine($"    Requests Exported: {item.RequestsExported}");
            Console.WriteLine($"    Pages Fetched: {item.PagesFetched}");
            Console.WriteLine($"    Candidates Found: {item.CandidatesFound}");
            Console.WriteLine($"    Semantic Matches: {item.SemanticMatches}");
            Console.WriteLine($"    Independent Sources Found: {item.IndependentSourcesFound}");
            Console.WriteLine($"    Policy Approved Sources: {item.PolicyApprovedSources}");
            Console.WriteLine($"    Validation Sync Status: {item.ValidationSyncStatus}");
            Console.WriteLine($"    Promotion Eligible: {item.PromotionEligible.ToString().ToLowerInvariant()}");
            Console.WriteLine($"    Next Action: {item.NextAction}");
            if (item.BlockersBefore.Count > 0)
            {
                Console.WriteLine($"    Blockers Before: {string.Join(", ", item.BlockersBefore.Take(6))}");
            }
            if (item.BlockersAfter.Count > 0)
            {
                Console.WriteLine($"    Blockers After: {string.Join(", ", item.BlockersAfter.Take(6))}");
            }
            if (item.Warnings.Count > 0)
            {
                Console.WriteLine($"    Warnings: {string.Join(", ", item.Warnings.Take(6))}");
            }
            Console.WriteLine();
        }
    }

    private void WriteKnowledgeTrustPromotionReport(KnowledgeTrustPromotionReport report, KnowledgeTrustPromotionPipelineService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("Last Successful Stage", report.LastSuccessfulStage);
        WriteField("Total Items", report.TotalItems.ToString());
        WriteField("Eligible for Promotion", report.EligibleForPromotion.ToString());
        WriteField("Promoted to Trusted", report.PromotedToTrusted.ToString());
        WriteField("Blocked by Evidence", report.BlockedByEvidence.ToString());
        WriteField("Blocked by Contradiction", report.BlockedByContradiction.ToString());
        WriteField("Blocked by Score", report.BlockedByScore.ToString());
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("Applied Count", report.AppliedCount.ToString());
        WriteField("Recommended Next Action", report.RecommendedNextAction);
        WriteField("Quality Path", DisplayPath(report.QualityPath));
        WriteField("Knowledge Evidence", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Source Confirmations", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Evidence Graph", DisplayPath(report.EvidenceGraphPath));
        WriteField("Validation Plans", DisplayPath(report.ValidationPlansPath));
        WriteField("Validation Status", DisplayPath(report.ValidationStatusPath));
        WriteField("Validation Execution Log", DisplayPath(report.ValidationExecutionLogPath));
        WriteField("Validation Plans Open", report.ValidationPlansOpen.ToString());
        WriteField("Validation Tasks Pending", report.ValidationTasksPending.ToString());
        WriteField("Validation Trusted Candidate Count", report.ValidationTrustedCandidateCount.ToString());
        WriteField("Validation Needs Source Check", report.ValidationItemsNeedingSourceCheck.ToString());
        WriteField("Validation Needs OOS", report.ValidationItemsNeedingOos.ToString());
        WriteField("Validation Routing Health", report.ValidationRoutingHealth);
        WriteField("Contradictions", DisplayPath(report.ContradictionsPath));
        WriteMessages("Stage Trace", report.StageTrace);
        WriteMessages("Affected Items", report.AffectedItems);
        WriteMessages("Top Blockers", report.TopBlockers.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        WriteMessages("Warnings", report.Warnings);

        foreach (var candidate in report.Candidates.Take(20))
        {
            WriteSubHeader($"{candidate.Title} / {candidate.KnowledgeId}");
            WriteField("Domain", candidate.Domain);
            WriteField("Current Status", candidate.CurrentStatus);
            WriteField("Recommended Status", candidate.RecommendedStatus);
            WriteField("Promotion Outcome", candidate.PromotionOutcome);
            WriteField("Trust Score", candidate.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Quality Score", candidate.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Validation Score", candidate.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Source Count", candidate.SourceCount.ToString());
            WriteField("Source Type Count", candidate.SourceTypeCount.ToString());
            WriteField("Validation Evidence Count", candidate.ValidationEvidenceCount.ToString());
            WriteField("Last Validated UTC", candidate.LastValidatedUtc?.ToString("O") ?? "-");
            WriteField("Latest Validation UTC", candidate.LatestValidationExecutionUtc?.ToString("O") ?? "-");
            WriteField("Validation Readiness", candidate.ValidationReadiness);
            WriteField("Eligible For Promotion", candidate.EligibleForPromotion.ToString().ToLowerInvariant());
            WriteField("Human Review Required", candidate.HumanReviewRequired.ToString().ToLowerInvariant());
            WriteMessages("Satisfied", candidate.SatisfiedConditions);
            WriteMessages("Missing Evidence", candidate.MissingEvidenceCategories);
            WriteMessages("Blockers", candidate.Blockers);
        }
    }

    private void WriteKnowledgeReasoningReport(KnowledgeReasoningReport report, KnowledgeReasoningService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Topic", report.Topic);
        WriteField("Status", report.Status);
        WriteField("Confidence", report.Confidence.ToString("0.###", CultureInfo.InvariantCulture));
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Knowledge Catalog", DisplayPath(report.KnowledgeCatalogPath));
        WriteField("Knowledge Quality", DisplayPath(report.KnowledgeQualityPath));
        WriteMessages("Used Knowledge IDs", report.UsedKnowledgeIds);
        WriteMessages("Supporting Sources", report.SupportingSources);
        WriteMessages("Reasoning Steps", report.ReasoningSteps);
        WriteMessages("Recommendations", report.Recommendations);
        WriteMessages("Open Uncertainties", report.OpenUncertainties);
        WriteMessages("Warnings", report.Warnings);

        WriteSubHeader("Matched Knowledge");
        if (report.MatchedKnowledge.Count == 0)
        {
            WriteField("Matched Knowledge", "none");
        }
        else
        {
            foreach (var item in report.MatchedKnowledge)
            {
                WriteField(item.KnowledgeId, item.Title);
                WriteField("Domain", item.Domain);
                WriteField("Validation Status", item.ValidationStatus);
                WriteField("Match Score", item.MatchScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Trust Score", item.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Quality Score", item.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Validation Score", item.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Matched Terms", string.Join(", ", item.MatchedTerms));
                WriteField("Source IDs", string.Join(", ", item.SourceIds));
                WriteField("Match Mode", item.MatchMode);
                WriteField("Reason", item.Reason);
            }
        }

        WriteSubHeader("Candidate Support");
        if (report.CandidateSupport.Count == 0)
        {
            WriteField("Candidate Support", "none");
        }
        else
        {
            foreach (var item in report.CandidateSupport)
            {
                WriteField(item.KnowledgeId, item.Title);
                WriteField("Domain", item.Domain);
                WriteField("Validation Status", item.ValidationStatus);
                WriteField("Match Score", item.MatchScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Trust Score", item.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Quality Score", item.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Validation Score", item.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Matched Terms", string.Join(", ", item.MatchedTerms));
                WriteField("Source IDs", string.Join(", ", item.SourceIds));
                WriteField("Match Mode", item.MatchMode);
                WriteField("Reason", item.Reason);
            }
        }

        WriteSubHeader("Conflicting Knowledge");
        if (report.ConflictingKnowledge.Count == 0)
        {
            WriteField("Conflicting Knowledge", "none");
        }
        else
        {
            foreach (var item in report.ConflictingKnowledge)
            {
                WriteField(item.KnowledgeId, item.Title);
                WriteField("Domain", item.Domain);
                WriteField("Validation Status", item.ValidationStatus);
                WriteField("Match Score", item.MatchScore.ToString("0.###", CultureInfo.InvariantCulture));
                WriteField("Reason", item.Reason);
            }
        }
    }

    private void WriteTrustedKnowledgeUsageAuditReport(TrustedKnowledgeUsageAuditReport report)
    {
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Read Only", report.ReadOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Commands With Context", report.CommandsWithTrustedKnowledgeContext.ToString());
        WriteField("Commands Without Topic", report.CommandsWithoutInferredTopic.ToString());
        WriteMessages("Used Topics", report.UsedTopics);
        WriteMessages("Used Knowledge IDs", report.UsedKnowledgeIds);
        WriteMessages("Warnings", report.Warnings);

        WriteSubHeader("Entries");
        foreach (var entry in report.Entries)
        {
            WriteField(entry.Command, entry.AnalysisLabel);
            WriteField("Trusted Knowledge Context", entry.TrustedKnowledgeContextUsed.ToString().ToLowerInvariant());
            WriteField("Topic Inferred", entry.TopicInferred.ToString().ToLowerInvariant());
            WriteField("Topic", entry.Topic ?? "topic not inferred");
            WriteField("Confidence", entry.Confidence is null ? "-" : entry.Confidence.Value.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Trusted Knowledge IDs", string.Join(", ", entry.TrustedKnowledgeIds));
            WriteField("Missing Topic Fields", string.Join(", ", entry.MissingTopicFields));
            WriteField("Topic Source Fields", string.Join(", ", entry.TopicSourceFields));
            WriteField("Current State", entry.CurrentState);
            WriteField("Notes", string.Join(" · ", entry.Notes));
            Console.WriteLine();
        }
    }

    private void WriteTrustedKnowledgeImpactReport(TrustedKnowledgeImpactReport report)
    {
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Read Only", report.ReadOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Commands With Trust Impact", report.CommandsWithTrustImpact);
        WriteMessages("Commands Without Topic", report.CommandsWithoutTopic);
        WriteMessages("Topics", report.Topics);
        WriteMessages("Trusted Knowledge IDs", report.TrustedKnowledgeIds);
        WriteMessages("Warnings", report.Warnings);

        WriteSubHeader("Entries");
        foreach (var entry in report.Entries)
        {
            WriteField(entry.Command, entry.AnalysisLabel);
            WriteField("Trusted Knowledge Used", entry.TrustedKnowledgeUsed.ToString().ToLowerInvariant());
            WriteField("Topic Inferred", entry.TopicInferred.ToString().ToLowerInvariant());
            WriteField("Topic", entry.Topic ?? "topic not inferred");
            WriteField("Confidence", entry.Confidence is null ? "-" : entry.Confidence.Value.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Supported Recommendation", entry.SupportedRecommendation);
            WriteField("Trusted Knowledge IDs", string.Join(", ", entry.TrustedKnowledgeIds));
            WriteField("Candidate Support Not Used", string.Join(", ", entry.CandidateSupportNotUsed));
            WriteField("Reduced Uncertainties", string.Join(", ", entry.ReducedUncertainties));
            WriteField("Missing Trusted Knowledge", string.Join(", ", entry.MissingTrustedKnowledge));
            WriteField("Notes", string.Join(" · ", entry.Notes));
            Console.WriteLine();
        }
    }

    private void WriteAutonomousKnowledgeAdvancementReport(AutonomousKnowledgeAdvancementReport report)
    {
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Read Only", report.ReadOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Status", report.Status);
        WriteField("Loaded Items", report.LoadedItems.ToString());
        WriteField("Candidate Support Items", report.CandidateSupportItems.ToString());
        WriteField("Prioritized Items", report.PrioritizedItems.ToString());
        WriteField("Plans Created", report.PlansCreated.ToString());
        WriteMessages("Used Topics", report.UsedTopics);
        WriteMessages("Used Knowledge IDs", report.UsedKnowledgeIds);
        WriteMessages("Warnings", report.Warnings);
        WriteField("Root Cause Summary", report.RootCauseSummary);

        WriteSubHeader("Plans");
        foreach (var plan in report.Plans)
        {
            WriteField(plan.KnowledgeId, plan.Title);
            WriteField("Current Status", plan.CurrentStatus);
            WriteField("Root Cause", plan.RootCause);
            WriteField("Next Action", plan.NextAction);
            WriteField("Followed By", string.Join(", ", plan.FollowedBy));
            WriteField("Operator Required", plan.OperatorRequired);
            WriteField("Source Count", plan.SourceCount.ToString());
            WriteField("Policy Approved Source Count", plan.PolicyApprovedSourceCount.ToString());
            WriteField("Validation Score", plan.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Trust Score", plan.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Quality Score", plan.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Impact Score", plan.ImpactScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Blockers", string.Join(", ", plan.Blockers));
            WriteField("Reasons", string.Join(", ", plan.Reasons));
            Console.WriteLine();
        }
    }

    private void WriteValidationEvidencePipelineReport(ValidationEvidencePipelineReport report, ValidationEvidencePipelineService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("Loaded Items", report.LoadedItems.ToString());
        WriteField("Validation Completed", report.ValidationCompleted.ToString());
        WriteField("Validation Pending", report.ValidationPending.ToString());
        WriteField("Waiting External Data", report.ValidationWaitingForExternalData.ToString());
        WriteField("Waiting Human Review", report.ValidationWaitingForHumanReview.ToString());
        WriteField("Plans Created", report.PlansCreated.ToString());
        WriteField("Validation Executions Created", report.ValidationExecutionsCreated.ToString());
        WriteField("Validation Plans", DisplayPath(report.ValidationPlansPath));
        WriteField("Validation Status", DisplayPath(report.ValidationStatusPath));
        WriteField("Knowledge Quality", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Knowledge Evidence", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Source Confirmations", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Evidence Graph", DisplayPath(report.EvidenceGraphPath));
        WriteField("Execution Log", DisplayPath(report.ExecutionLogPath));
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);

        WriteSubHeader("Focus Items");
        foreach (var item in report.FocusItems)
        {
            WriteField(item.KnowledgeItemId, item.Title);
            WriteField("Domain", item.Domain);
            WriteField("Readiness", $"{item.ValidationReadinessBefore} -> {item.ValidationReadinessAfter}");
            WriteField("Plan Status", $"{item.PlanStatusBefore} -> {item.PlanStatusAfter}");
            WriteField("Validation Score", $"{item.ValidationScoreBefore:0.###} -> {item.ValidationScoreAfter:0.###}");
            WriteField("Trust Score", $"{item.TrustScoreBefore:0.###} -> {item.TrustScoreAfter:0.###}");
            WriteField("Quality Score", $"{item.QualityScoreBefore:0.###} -> {item.QualityScoreAfter:0.###}");
            WriteField("Source Count", item.SourceCount.ToString());
            WriteField("Policy Approved Source Count", item.PolicyApprovedSourceCount.ToString());
            WriteField("Remaining Blockers", string.Join(", ", item.RemainingBlockers.Take(8)));
            WriteField("Recommended Next Action", item.RecommendedNextAction);
            Console.WriteLine();
        }

        WriteSubHeader("Targeted Knowledge Items");
        foreach (var id in new[] { "trading:bearish_engulfing", "trading:liquidity_sweep", "trading:inside_bar" })
        {
            var item = report.Items.FirstOrDefault(entry => entry.KnowledgeItemId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                WriteField(id, "not found");
                continue;
            }

            WriteField(item.KnowledgeItemId, item.Title);
            WriteField("Validation Score", $"{item.ValidationScoreBefore:0.###} -> {item.ValidationScoreAfter:0.###}");
            WriteField("Trust Score", $"{item.TrustScoreBefore:0.###} -> {item.TrustScoreAfter:0.###}");
            WriteField("Quality Score", $"{item.QualityScoreBefore:0.###} -> {item.QualityScoreAfter:0.###}");
            WriteField("Remaining Blockers", string.Join(", ", item.RemainingBlockers.Take(10)));
            WriteField("Recommended Next Action", item.RecommendedNextAction);
            Console.WriteLine();
        }

        WriteMessages("Remaining Blockers", report.RemainingBlockers.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
    }

    private void WriteValidationStateSyncReport(ValidationStateSynchronizerReport report, ValidationStateSynchronizerService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Status", report.Status);
        WriteField("Loaded Items", report.LoadedItems.ToString());
        WriteField("Synchronized Items", report.SynchronizedItems.ToString());
        WriteField("Timestamp Fixed", report.TimestampFixed.ToString());
        WriteField("Domain Validation Fixed", report.DomainValidationFixed.ToString());
        WriteField("Validation Plan Fixed", report.ValidationPlanFixed.ToString());
        WriteField("Human Review Reclassified", report.HumanReviewReclassified.ToString());
        WriteField("Remaining Blockers", report.RemainingBlockers.ToString());
        WriteField("Quality Path", DisplayPath(report.QualityPath));
        WriteField("Evidence Path", DisplayPath(report.EvidencePath));
        WriteField("Validation Plans Path", DisplayPath(report.ValidationPlansPath));
        WriteField("Validation Status Path", DisplayPath(report.ValidationStatusPath));
        WriteField("Validation Execution Log Path", DisplayPath(report.ValidationExecutionLogPath));
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("Applied", report.Applied.ToString().ToLowerInvariant());
        WriteField("Research Only", report.ResearchOnly.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Remaining Blockers", report.RemainingBlockersByType.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        WriteMessages("Warnings", report.Warnings);

        Console.WriteLine();
        Console.WriteLine("Targeted Knowledge Items:");
        foreach (var id in new[] { "trading:bearish_engulfing", "trading:liquidity_sweep", "trading:inside_bar" })
        {
            var item = report.Items.FirstOrDefault(entry => entry.KnowledgeItemId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                WriteField(id, "not found");
                continue;
            }

            WriteField(item.KnowledgeItemId, item.Title);
            WriteField("Domain Validation", $"{item.DomainValidationStatusBefore} -> {item.DomainValidationStatusAfter}");
            WriteField("Validation Plan", $"{item.ValidationPlanStatusBefore} -> {item.ValidationPlanStatusAfter}");
            WriteField("Last Validated UTC", $"{item.LastValidatedUtcBefore?.ToString("O") ?? "-"} -> {item.LastValidatedUtcAfter?.ToString("O") ?? "-"}");
            WriteField("Validation Score", $"{item.ValidationScoreBefore:0.###} -> {item.ValidationScoreAfter:0.###}");
            WriteField("Trust Score", $"{item.TrustScoreBefore:0.###} -> {item.TrustScoreAfter:0.###}");
            WriteField("Quality Score", $"{item.QualityScoreBefore:0.###} -> {item.QualityScoreAfter:0.###}");
            WriteField("Has Validation Executions", item.HasValidationExecutions.ToString().ToLowerInvariant());
            WriteField("Policy Approved Second Source", item.HasPolicyApprovedSecondSource.ToString().ToLowerInvariant());
            WriteField("Synchronized", item.Synchronized.ToString().ToLowerInvariant());
            WriteField("Recommended Next Action", item.RecommendedNextAction);
            WriteMessages("Remaining Blockers", item.RemainingBlockersAfter);
            WriteMessages("Removed Blockers", item.RemovedBlockers);
            WriteMessages("Warnings", item.Warnings);
            Console.WriteLine();
        }
    }

    private void WriteMultiSourceEvidenceReport(MultiSourceEvidencePlanReport report)
    {
        WriteField("Report", report.ReportVersion);
        WriteField("Items Needing Second Source", report.ItemsNeedingSecondSource.ToString());
        WriteField("Prioritized Items", report.PrioritizedItems.ToString());
        WriteField("Updated Source Confirmations", report.UpdatedSourceConfirmations.ToString());
        WriteField("Created Research Queue Items", report.CreatedResearchQueueItems.ToString());
        WriteField("Source Confirmations Path", DisplayPath(report.SourceConfirmationsPath));
        WriteField("Knowledge Evidence Path", DisplayPath(report.KnowledgeEvidencePath));
        WriteField("Evidence Graph Path", DisplayPath(report.EvidenceGraphPath));
        WriteField("Validation Plans Path", DisplayPath(report.ValidationPlansPath));
        WriteField("Knowledge Quality Path", DisplayPath(report.KnowledgeQualityPath));
        WriteField("Research Queue Path", DisplayPath(report.ResearchQueuePath));
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());

        Console.WriteLine();
        Console.WriteLine("Source Types Needed:");
        foreach (var entry in report.SourceTypeNeededDistribution.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  - {entry.Key}: {entry.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Missing Evidence:");
        foreach (var entry in report.MissingEvidenceDistribution.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  - {entry.Key}: {entry.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Recommended Queries:");
        foreach (var query in report.RecommendedQueries.Take(20))
        {
            Console.WriteLine($"  - {query}");
        }

        Console.WriteLine();
        Console.WriteLine("Prioritized Items:");
        foreach (var candidate in report.PrioritizedCandidates.Take(20))
        {
            Console.WriteLine($"  {candidate.Title} / {candidate.KnowledgeId}");
            Console.WriteLine($"    Domain: {candidate.Domain}");
            Console.WriteLine($"    Current Source Count: {candidate.CurrentSourceCount}");
            Console.WriteLine($"    Source Type Needed: {candidate.SourceTypeNeeded}");
            Console.WriteLine($"    Trust: {candidate.TrustScore:0.###}, Quality: {candidate.QualityScore:0.###}, Validation: {candidate.ValidationScore:0.###}");
            Console.WriteLine($"    Open Validation Plans: {candidate.OpenValidationPlans}");
            Console.WriteLine($"    Priority Score: {candidate.PriorityScore:0.####}");
            Console.WriteLine($"    Query: {candidate.Query}");
            Console.WriteLine($"    Has Local Alternative Sources: {candidate.HasLocalAlternativeSources.ToString().ToLowerInvariant()}");
            Console.WriteLine($"    Would Update Source Confirmations: {candidate.WouldUpdateSourceConfirmations.ToString().ToLowerInvariant()}");
            Console.WriteLine($"    Would Create Research Queue Item: {candidate.WouldCreateResearchQueueItem.ToString().ToLowerInvariant()}");
            if (candidate.MissingEvidenceTypes.Count > 0)
            {
                Console.WriteLine($"    Missing: {string.Join(", ", candidate.MissingEvidenceTypes.Take(5))}");
            }

            Console.WriteLine();
        }
    }

    private void WriteKnownArticleSeedCatalogReport(KnownArticleSeedStatusReport report)
    {
        WriteField("Status", report.Status);
        WriteField("Report Version", report.ReportVersion);
        WriteField("Updated At", report.UpdatedAtUtc.ToString("O"));
        WriteField("Loaded Knowledge Items", report.LoadedKnowledgeItems.ToString());
        WriteField("Considered Knowledge Items", report.ConsideredKnowledgeItems.ToString());
        WriteField("Seed Definitions", report.SeedDefinitions.ToString());
        WriteField("Seed Requests", report.SeedRequests.ToString());
        WriteField("Fetched Candidates", report.FetchedCandidates.ToString());
        WriteField("Accepted Candidates", report.AcceptedCandidates.ToString());
        WriteField("Rejected Candidates", report.RejectedCandidates.ToString());
        WriteField("Duplicate Candidates", report.DuplicateCandidates.ToString());
        WriteField("Seed Catalog Path", DisplayPath(report.SeedCatalogPath));
        WriteField("Requests Path", DisplayPath(report.RequestsPath));
        WriteField("Import Candidates Path", DisplayPath(report.ImportCandidatesPath));
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Dry Run", report.DryRun.ToString().ToLowerInvariant());
        WriteField("Applied", report.Applied.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        if (report.Requests.Count > 0)
        {
            WriteMessages("Requests", report.Requests.Take(20).Select(request => $"{request.KnowledgeItemId} | {request.Domain} | {request.PublisherGroup} | {request.Url} | {request.Status}").ToList());
        }
        if (report.Candidates.Count > 0)
        {
            WriteMessages("Candidates", report.Candidates.Take(20).Select(candidate => $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | score={candidate.RelevanceScore:0.###} | {candidate.SourceRelevanceStatus}").ToList());
        }
        if (report.Rejected.Count > 0)
        {
            WriteMessages("Rejected", report.Rejected.Take(20).Select(candidate => $"{candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | {candidate.RejectionReason}").ToList());
        }
    }

    private void WriteWebResearchSourceCollectorReport(WebResearchSourceCollectorReport report)
    {
        WriteField("Report", DisplayPath(report.ReportPath));
        WriteField("Markdown", DisplayPath(report.MarkdownPath));
        WriteField("Queue", DisplayPath(report.QueuePath));
        WriteField("Total Second Source Items", report.TotalSecondSourceItems.ToString());
        WriteField("Exported Search Requests", report.ExportedSearchRequests.ToString());
        WriteField("Awaiting External Search", report.AwaitingExternalSearch.ToString());
        WriteField("Already Has Candidate Source", report.AlreadyHasCandidateSource.ToString());
        WriteField("Blocked No Web Runtime", report.BlockedNoWebRuntime.ToString());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("No Auto Trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("Human Review Required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);

        Console.WriteLine();
        Console.WriteLine("Requests:");
        foreach (var request in report.Requests.Take(20))
        {
            Console.WriteLine($"  {request.KnowledgeItemId} / {request.Domain}");
            Console.WriteLine($"    Query: {request.Query}");
            Console.WriteLine($"    Recommended Source Domains: {string.Join(", ", request.RecommendedSourceDomains)}");
            Console.WriteLine($"    Reason: {request.Reason}");
            Console.WriteLine($"    Current Source Count: {request.CurrentSourceCount}");
            Console.WriteLine($"    Required Evidence: {string.Join(", ", request.RequiredEvidence)}");
            Console.WriteLine($"    Status: {request.Status}");
            Console.WriteLine();
        }
    }

    private int ShowTrustedReviewGate()
    {
        WriteHeader("Hermes Trusted Knowledge Review Gate");
        var service = new TrustedKnowledgeReviewGateService(BuildStoragePaths());
        var report = service.Run();

        WriteTrustedReviewGate(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int GenerateTrustedReviewCandidates()
    {
        WriteHeader("Hermes Trusted Knowledge Review Gate");
        var service = new TrustedKnowledgeReviewGateService(BuildStoragePaths());
        var report = service.Run();

        WriteTrustedReviewGate(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTrustImprovementPlan()
    {
        WriteHeader("Hermes Knowledge Trust Improvement Plan");
        var service = new KnowledgeTrustImprovementPlannerService(BuildStoragePaths());
        var report = service.Run();

        WriteTrustImprovementPlan(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int GenerateTrustImprovementPlan()
    {
        WriteHeader("Hermes Knowledge Trust Improvement Plan");
        var service = new KnowledgeTrustImprovementPlannerService(BuildStoragePaths());
        var report = service.Run();

        WriteTrustImprovementPlan(report, service);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private void WriteTrustedReviewGate(TrustedKnowledgeReviewGateReport report, TrustedKnowledgeReviewGateService service)
    {
        WriteField("Report", DisplayPath(service.GatePath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Total Knowledge Items", report.TotalKnowledgeItems.ToString());
        WriteField("Trusted Items", report.TrustedItemsCount.ToString());
        WriteField("Eligible for Trusted Review", report.EligibleForTrustedReview.ToString());
        WriteField("Blocked Items", report.BlockedItems.ToString());
        WriteField("Requires Human Review", report.RequiresHumanReview.ToString().ToLowerInvariant());
        WriteMessages("Blocker", report.RejectionReasons.Select(entry => $"{entry.Key}:{entry.Value}").ToList());
        foreach (var candidate in report.TopCandidates.Take(20))
        {
            WriteSubHeader(candidate.Title);
            WriteField("Knowledge ID", candidate.KnowledgeId);
            WriteField("Domain", candidate.Domain);
            WriteField("Trust Score", candidate.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Quality Score", candidate.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Evidence Score", candidate.EvidenceScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Evidence Count", candidate.EvidenceCount.ToString());
            WriteField("Source Count", candidate.SourceCount.ToString());
            WriteField("Last Validated", candidate.LastValidatedUtc?.ToString("O") ?? "-");
            WriteField("Review Status", candidate.ReviewStatus);
            WriteField("Requires Human Review", candidate.RequiresHumanReview.ToString().ToLowerInvariant());
            WriteMessages("Reasons", candidate.BlockingReasons);
        }
        WriteMessages("Warnings", report.Warnings);
    }

    private void WriteTrustImprovementPlan(KnowledgeTrustImprovementPlanReport report, KnowledgeTrustImprovementPlannerService service)
    {
        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Markdown", DisplayPath(service.MarkdownPath));
        WriteField("Total Blocked Items", report.TotalBlockedItems.ToString());
        WriteField("Estimated Effort", report.EstimatedEffort);
        WriteField("Auto Fixable Count", report.AutoFixableCount.ToString());
        WriteField("Human Review Count", report.HumanReviewCount.ToString());
        WriteField("Requires Human Review", report.RequiresHumanReview.ToString().ToLowerInvariant());
        WriteField("Next Recommended Action", report.NextRecommendedAction);
        WriteMessages("Blocker Counts", report.BlockerCounts.Select(entry => $"{entry.Key}: {entry.Value}").ToList());
        foreach (var action in report.PlannedActions.Take(20))
        {
            WriteSubHeader(action.Title);
            WriteField("Action ID", action.ActionId);
            WriteField("Blocker", action.Blocker);
            WriteField("Domain", action.Domain);
            WriteField("Priority", action.Priority);
            WriteField("Suggested Action", action.SuggestedAction);
            WriteField("Auto Fixable", action.AutoFixable.ToString().ToLowerInvariant());
            WriteField("Requires Human Review", action.RequiresHumanReview.ToString().ToLowerInvariant());
        }
        foreach (var item in report.TopPriorityItems.Take(20))
        {
            WriteSubHeader(item.Title);
            WriteField("Knowledge ID", item.KnowledgeId);
            WriteField("Domain", item.Domain);
            WriteField("Trust Score", item.TrustScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Quality Score", item.QualityScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Validation Score", item.ValidationScore.ToString("0.###", CultureInfo.InvariantCulture));
            WriteField("Priority", item.Priority);
            WriteField("Auto Fixable", item.AutoFixable.ToString().ToLowerInvariant());
            WriteField("Requires Human Review", item.RequiresHumanReview.ToString().ToLowerInvariant());
            WriteMessages("Blockers", item.Blockers);
            WriteMessages("Planned Actions", item.PlannedActions);
        }
    }

    private int ReviewPromotionCandidates()
    {
        WriteHeader("Hermes Review Promotion Candidates");
        var storagePaths = BuildStoragePaths();
        var engine = new KnowledgePromotionEngine(storagePaths);
        var report = engine.BuildTrustedCandidates();

        var readyCandidates = report.Candidates
            .Where(c => c.Blockers.Count == 0 && !c.HumanReviewRequired)
            .ToList();

        if (readyCandidates.Count == 0)
        {
            Console.WriteLine("No candidates ready for automatic promotion.");
            Console.WriteLine();
            Console.WriteLine($"Awaiting Human Review: {report.AwaitingHumanReview}");
            Console.WriteLine($"Blocked: {report.BlockedCandidates}");
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        Console.WriteLine($"Found {readyCandidates.Count} candidates ready for promotion:");
        Console.WriteLine();

        foreach (var candidate in readyCandidates.Take(10))
        {
            Console.WriteLine($"  {candidate.KnowledgeId}");
            Console.WriteLine($"    {candidate.CurrentStatus} → {candidate.RecommendedStatus}");
            Console.WriteLine($"    Trust: {candidate.CurrentTrustScore:0.####}, Quality: {candidate.CurrentQualityScore:0.####}");
            Console.WriteLine($"    Reason: {candidate.DecisionReason}");
            Console.WriteLine();
        }

        Console.WriteLine("To apply promotions, use: ApplyPromotions(decisions, dryRun: false)");
        Console.WriteLine("This is currently a review-only command.");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainPromotion()
    {
        WriteHeader("Hermes Explain Promotion Decision");
        var knowledgeItemId = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(knowledgeItemId))
        {
            Console.WriteLine("Error: --id <KNOWLEDGE_ITEM_ID> required");
            return 1;
        }

        var storagePaths = BuildStoragePaths();
        var qualityEngine = new KnowledgeQualityEngine(storagePaths);
        var qualityReport = qualityEngine.LoadOrCreateReport();
        var promotionEngine = new KnowledgePromotionEngine(storagePaths);
        var humanReview = new HumanReviewWorkflow(storagePaths).BuildSummary();

        var qualityItem = qualityReport.Items.FirstOrDefault(item =>
            item.KnowledgeId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase));

        if (qualityItem is null)
        {
            Console.WriteLine($"Knowledge item not found: {knowledgeItemId}");
            return 1;
        }

        var catalog = new KnowledgeCatalog(storagePaths);
        var catalogItem = catalog.FindById(knowledgeItemId);
        var decision = promotionEngine.EvaluatePromotion(qualityItem, catalogItem, humanReview);

        WriteField("Knowledge Item", decision.KnowledgeId);
        WriteField("Current Status", decision.CurrentStatus);
        WriteField("Recommended Status", decision.RecommendedStatus);
        WriteField("Decision Reason", decision.DecisionReason);
        WriteField("Decision Type", decision.DecisionType);
        Console.WriteLine();

        Console.WriteLine();
        Console.WriteLine("Current Scores:");;
        WriteField("Trust Score", decision.CurrentTrustScore.ToString("0.####"));
        WriteField("Quality Score", decision.CurrentQualityScore.ToString("0.####"));
        WriteField("Expected Trust Delta", decision.ExpectedTrustDelta.ToString("+0.####;-0.####"));
        Console.WriteLine();

        if (decision.SatisfiedConditions.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Satisfied Conditions:");;
            foreach (var condition in decision.SatisfiedConditions)
            {
                Console.WriteLine($"  ✓ {condition}");
            }
            Console.WriteLine();
        }

        if (decision.UnsatisfiedConditions.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Unsatisfied Conditions:");;
            foreach (var condition in decision.UnsatisfiedConditions)
            {
                Console.WriteLine($"  ✗ {condition}");
            }
            Console.WriteLine();
        }

        if (decision.Blockers.Count > 0)
        {
            Console.WriteLine();
        Console.WriteLine("Blockers:");;
            foreach (var blocker in decision.Blockers)
            {
                Console.WriteLine($"  ⚠ {blocker}");
            }
            Console.WriteLine();
        }

        if (decision.HumanReviewRequired)
        {
            Console.WriteLine("⚠ Human Review Required for promotion to trusted status");
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Quality Details:");;
        WriteField("Trust Classification", KnowledgePromotionEngine.TrustClassification(qualityItem.TrustScore));
        WriteField("Evidence Classification", KnowledgePromotionEngine.EvidenceClassification(qualityItem.EvidenceScore));
        WriteField("Validation Score", qualityItem.ValidationScore.ToString("0.####"));
        WriteField("Lifecycle Status", qualityItem.LifecycleStatus);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }
}
