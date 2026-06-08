using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MasterStatusService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public MasterStatusService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string SnapshotDirectory => Path.Combine(_storagePaths.Root, "reports", "master-status");

    public string SnapshotPath => Path.Combine(SnapshotDirectory, "master_status.json");

    public MasterStatusSnapshot BuildSnapshot()
    {
        var scheduleConfigPath = Path.Combine(_runtimeRoot, "config", "schedules.json");
        var nightlyConfigPath = Path.Combine(_runtimeRoot, "config", "nightly.research.json");
        var schedulerStatus = new HermesInternalScheduler(_storagePaths, scheduleConfigPath).GetStatus();
        var supervisor = new HermesSupervisor(_storagePaths, scheduleConfigPath);
        var supervisorState = supervisor.LoadState();
        var supervisorHeartbeat = supervisor.LoadHeartbeat();
        var supervisorProcess = new SupervisorProcessManager(_storagePaths)
            .GetStatus(supervisorState, supervisorHeartbeat?.SupervisorId == supervisorState.SupervisorId ? supervisorHeartbeat : null);
        var nightlyState = new NightlyResearchService(_storagePaths, nightlyConfigPath).LoadState();

        var runtimeHealthPath = Path.Combine(_storagePaths.Root, "reports", "runtime_health.json");
        var nightlyStatePath = Path.Combine(_storagePaths.Root, "reports", "nightly_beta3", "nightly_state.json");
        var resourceStatusPath = new ResourceGuard(_storagePaths).StatusPath;
        var storageStatusPath = Path.Combine(_storagePaths.Root, "reports", "storage", "storage_status.json");
        var storagePlanPath = new StorageHygieneService(_storagePaths).CleanupPlanPath;
        var cognitiveRoot = Path.Combine(_storagePaths.Root, "cognitive_core");
        var strategyRoot = Path.Combine(_storagePaths.Root, "strategy_research");
        var botCandidatePath = Path.Combine(_storagePaths.Root, "bot_candidates", "latest_bot_candidate_report.json");
        var scalpingService = new ScalpingResearchService(_storagePaths);
        var marketDataService = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var simulationRoot = Path.Combine(_storagePaths.Root, "reports", "simulation");
        var requestedWalkforwardPath = Path.Combine(_storagePaths.Root, "simulation", "walkforward_validation.json");
        var fallbackWalkforwardPath = Path.Combine(simulationRoot, "walkforward_summary.json");
        var walkforwardPath = File.Exists(requestedWalkforwardPath) ? requestedWalkforwardPath : fallbackWalkforwardPath;

        var runtimeHealth = LoadOrDefault(runtimeHealthPath);
        var resourceStatus = LoadOrDefault(resourceStatusPath);
        var storageStatus = LoadOrDefault(storageStatusPath);
        var storagePlan = LoadOrDefault(storagePlanPath);
        var cognitiveStatus = LoadOrDefault(Path.Combine(cognitiveRoot, "cognitive_status.json"));
        var researchQueue = LoadOrDefault(Path.Combine(cognitiveRoot, "research_queue.json"));
        var domainStatus = LoadOrDefault(Path.Combine(cognitiveRoot, "domain_status.json"));
        var planningStatus = LoadOrDefault(Path.Combine(cognitiveRoot, "planning_status.json"));
        var autonomousLoopState = LoadOrDefault(Path.Combine(cognitiveRoot, "autonomous_loop_state.json"));
        var autonomousLoopSummary = LoadOrDefault(Path.Combine(cognitiveRoot, "autonomous_loop_summary.json"));
        var outcomeStatus = LoadOrDefault(Path.Combine(cognitiveRoot, "outcome_feedback_status.json"));
        var metaReview = LoadOrDefault(Path.Combine(cognitiveRoot, "meta_review.json"));
        var learningStrategy = LoadOrDefault(Path.Combine(cognitiveRoot, "learning_strategy.json"));
        var researchInsights = LoadOrDefault(Path.Combine(strategyRoot, "research_insights.json"));
        var nightlyReport = LoadOrDefault(nightlyStatePath);
        var robustStrategies = LoadOrDefault(Path.Combine(strategyRoot, "robust_strategies.json"));
        var overfitReport = LoadOrDefault(Path.Combine(strategyRoot, "overfit_report.json"));
        var goalState = new GoalProgressTracker(_storagePaths).LoadOrCreateState();
        if (overfitReport.ValueKind != JsonValueKind.Object)
        {
            overfitReport = LoadOrDefault(Path.Combine(simulationRoot, "overfit_report.json"));
        }

        var walkforward = LoadOrDefault(walkforwardPath);
        var botCandidateReport = LoadOrDefault(botCandidatePath);
        var scalpingReport = scalpingService.LoadReport();
        var marketDataAvailability = marketDataService.LoadAvailability() ?? marketDataService.Scan();
        var xauusdQuality = marketDataService.BuildQuality(ScalpingResearchService.DefaultAsset);
        var knowledgeQuality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var knowledgeValidation = new KnowledgeValidationStrategy(_storagePaths).LoadStatus();
        var domainValidation = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        var humanReview = new HumanReviewWorkflow(_storagePaths).BuildSummary();
        var promotionStatus = new KnowledgePromotionEngine(_storagePaths).BuildStatus();

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
            GetStringArray(domainStatus, "weak_domains", "weakDomains").Select(item => $"weak_domain:{item}"),
            knowledgeQuality.WeakKnowledge > 0 ? [$"weak_knowledge:{knowledgeQuality.WeakKnowledge}"] : [],
            knowledgeQuality.DeprecatedKnowledge > 0 ? [$"deprecated_knowledge:{knowledgeQuality.DeprecatedKnowledge}"] : [],
            knowledgeQuality.EvidenceCoverage < 0.55 ? [$"evidence_gap:{knowledgeQuality.EvidenceCoverage:0.####}"] : [],
            knowledgeQuality.ContradictionCount > 0 ? [$"contradictions:{knowledgeQuality.ContradictionCount}"] : [],
            knowledgeQuality.AverageTrustScore < 0.55 ? [$"trust_gap:{knowledgeQuality.AverageTrustScore:0.####}"] : [],
            humanReview.PendingReviews > 0 ? [$"pending_human_reviews:{humanReview.PendingReviews}"] : [],
            humanReview.NeedsMoreEvidenceReviews > 0 ? [$"review_needs_more_evidence:{humanReview.NeedsMoreEvidenceReviews}"] : [],
            promotionStatus.PromotionBlockers,
            knowledgeValidation?.ValidationPlansOpen > 0 ? [$"validation_plans_open:{knowledgeValidation.ValidationPlansOpen}"] : [],
            knowledgeValidation?.KnowledgeItemsNeedingOos > 0 ? [$"knowledge_needs_oos:{knowledgeValidation.KnowledgeItemsNeedingOos}"] : [],
            knowledgeValidation?.InvalidValidationTasks > 0 ? [$"invalid_validation_tasks:{knowledgeValidation.InvalidValidationTasks}"] : [],
            domainValidation.DocumentationValidationPending > 0 ? [$"documentation_validation_pending:{domainValidation.DocumentationValidationPending}"] : [],
            domainValidation.SoftwareValidationPending > 0 ? [$"software_validation_pending:{domainValidation.SoftwareValidationPending}"] : [],
            domainValidation.ProcessValidationPending > 0 ? [$"process_validation_pending:{domainValidation.ProcessValidationPending}"] : [],
            domainValidation.ResearchValidationPending > 0 ? [$"research_validation_pending:{domainValidation.ResearchValidationPending}"] : [],
            goalState.BlockedGoals.Select(item => $"blocked_goal:{item}"))
            .Take(10)
            .ToList();

        var nextRecommendedActions = CombineStringLists(
            GetStringArray(planningStatus, "top_tasks", "topTasks"),
            GetStringArray(learningStrategy, "priority_task_types", "priorityTaskTypes"),
            GetStringArray(researchInsights, "recommended_next_experiments", "recommendedNextExperiments"),
            GetStringArray(researchInsights, "next_validation_recommendations", "nextValidationRecommendations"),
            knowledgeValidation?.InvalidValidationTasks > 0 ? ["cleanup-invalid-validation-tasks"] : [],
            domainValidation.DomainValidationHealth is "pending" or "needs_attention" ? ["validate-domain-knowledge"] : [],
            goalState.Goals
                .OrderBy(goal => goal.Priority)
                .SelectMany(goal => goal.NextRecommendedActions.Select(action => $"{goal.GoalId}:{action}")),
            knowledgeQuality.KnowledgeHealth is "critical" or "needs_consolidation"
                ? ["generate_validation_plans", "validate_knowledge_items", "execute_validation_tasks", "cleanup-invalid-validation-tasks", "consolidate_memory", "evaluate_knowledge_quality"]
                : [])
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
            GetInt(cognitiveStatus, "queue_item_count", "queueItemCount"),
            CountOpenResearchQueueItems(researchQueue),
            GetArrayCount(cognitiveStatus, "queue", "items"));

        var cleanupCandidates = FirstPositive(
            GetArrayCount(storagePlan, "candidates"),
            GetInt(storageStatus, "cleanup_candidates", "cleanupCandidates"));
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
        var scalpingAsset = scalpingReport?.Asset ?? ScalpingResearchService.DefaultAsset;
        var scalpingCandidatesTotal = scalpingReport?.CandidatesTotal ?? 0;
        var scalpingRobustCandidates = scalpingReport?.RobustCandidates ?? 0;
        var scalpingRejectedCandidates = scalpingReport?.RejectedCandidates ?? 0;
        var scalpingNeedsMoreData = scalpingReport?.NeedsMoreData ?? 0;
        var bestScalpingCandidate = scalpingReport?.BestCandidateId;
        var signalAgentSpecsReady = Directory.Exists(scalpingService.SignalSpecDirectory)
            ? Directory.GetFiles(scalpingService.SignalSpecDirectory, "signal_agent_spec.json", SearchOption.AllDirectories).Length
            : 0;
        var latestSignalAgentSpec = Directory.Exists(scalpingService.SignalSpecDirectory)
            ? Directory.GetFiles(scalpingService.SignalSpecDirectory, "signal_agent_spec.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        var cTraderBotSpecsReady = Directory.Exists(scalpingService.BotSpecDirectory)
            ? Directory.GetFiles(scalpingService.BotSpecDirectory, "*.json").Length
            : 0;
        var scalpingDataGap = xauusdQuality.DataGaps.Count == 0 ? "-" : string.Join(",", xauusdQuality.DataGaps);
        var expansionReports = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReports();
        var scalpingRobustnessExpanded = expansionReports.Count(report => report.Status == ScalpingExpansionStatus.robustness_expanded);
        var scalpingFinalCandidates = expansionReports.Count(report => report.Status == ScalpingExpansionStatus.final_candidate);
        var scalpingRejectedAfterExpansion = expansionReports.Count(report => report.Status == ScalpingExpansionStatus.rejected_after_expansion);
        var bestFinalScalpingCandidate = expansionReports
            .Where(report => report.Status == ScalpingExpansionStatus.final_candidate)
            .OrderByDescending(report => report.StabilityScore)
            .FirstOrDefault()?.CandidateId;
        var scalpingMonteCarloHealth = expansionReports.Count == 0
            ? "missing"
            : expansionReports.Any(report => report.MonteCarlo.Health != "ok") ? "needs_attention" : "ok";
        var scalpingParameterSensitivityHealth = expansionReports.Count == 0
            ? "missing"
            : expansionReports.Any(report => report.ParameterSensitivity.Health != "ok") ? "needs_attention" : "ok";
        var scalpingRegimeValidationHealth = expansionReports.Count == 0
            ? "missing"
            : expansionReports.Any(report => report.RegimeValidation.Health != "ok") ? "needs_attention" : "ok";
        var scalpingCandidatesWithStableCorridor = expansionReports.Count(report => report.ParameterSensitivity.StableConservativeCorridorAvailable);
        var scalpingCandidatesBlockedBySensitivity = expansionReports.Count(report => report.Blockers.Any(blocker => blocker.Contains("sensitivity", StringComparison.OrdinalIgnoreCase)));
        var scalpingSensitivityExplainabilityHealth = expansionReports.Count == 0
            ? "missing"
            : expansionReports.Any(report => report.ParameterSensitivity.Blockers.Any(blocker => blocker.Contains("unexplained", StringComparison.OrdinalIgnoreCase))) ? "needs_attention" : "ok";
        var bestScalpingParameterCorridorCandidate = expansionReports
            .Where(report => report.ParameterSensitivity.StableConservativeCorridorAvailable)
            .OrderByDescending(report => report.StabilityScore)
            .FirstOrDefault()?.CandidateId;
        var certificationService = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        var certificationReports = certificationService.LoadReports();
        var scalpingCertifiedCandidates = certificationReports.Count(report => report.Status == ScalpingCertificationStatus.certified_candidate);
        var scalpingCertificationFailed = certificationReports.Count(report => report.Status == ScalpingCertificationStatus.certification_failed);
        var bestCertifiedScalpingCandidate = certificationReports
            .Where(report => report.Status == ScalpingCertificationStatus.certified_candidate)
            .OrderByDescending(report => report.DrawdownCertification.RecoveryFactor)
            .ThenByDescending(report => report.TradeDistribution.ExpectancyR)
            .FirstOrDefault()?.CandidateId;
        var scalpingHumanReviewPackagesReady = Directory.Exists(certificationService.CertificationDirectory)
            ? Directory.GetFiles(certificationService.CertificationDirectory, "human_review_package.md", SearchOption.AllDirectories).Length
            : 0;
        var scalpingCertificationHealth = certificationReports.Count == 0
            ? "missing"
            : scalpingCertificationFailed > 0 ? "needs_attention" : "ok";
        var certifiedCandidateSignalReady = certificationReports
            .Where(report => report.Status == ScalpingCertificationStatus.certified_candidate)
            .Any(report => File.Exists(Path.Combine(scalpingService.SignalSpecDirectory, report.CandidateId, "signal_agent_spec.json")));
        var signalAgentExportHealth = certificationReports.Any(report => report.Status == ScalpingCertificationStatus.certified_candidate)
            ? certifiedCandidateSignalReady ? "ok" : "needs_export"
            : "missing_certified_candidate";

        var noAutoTrading = schedulerStatus.NoAutoTrading
            && supervisorState.NoAutoTrading
            && nightlyState.NoAutoTrading
            && SafetyFlagTrue([runtimeHealth, resourceStatus, storageStatus, storagePlan, cognitiveStatus, planningStatus, autonomousLoopState, autonomousLoopSummary, outcomeStatus, metaReview, learningStrategy, researchInsights, botCandidateReport], "no_auto_trading", "noAutoTrading");
        var humanReviewRequired = schedulerStatus.HumanReviewRequired
            && supervisorState.HumanReviewRequired
            && nightlyState.HumanReviewRequired
            && SafetyFlagTrue([runtimeHealth, resourceStatus, storageStatus, storagePlan, cognitiveStatus, planningStatus, autonomousLoopState, autonomousLoopSummary, outcomeStatus, metaReview, learningStrategy, researchInsights, botCandidateReport], "human_review_required", "humanReviewRequired");

        var criticalReasons = new List<string>();
        var warningReasons = new List<string>();
        var requiredReportStates = new (string Name, JsonElement Root)[]
        {
            ("cognitive_status", cognitiveStatus),
            ("research_queue", researchQueue),
            ("autonomous_loop_state", autonomousLoopState),
            ("nightly_state", nightlyReport),
            ("resource_status", resourceStatus),
            ("research_insights", researchInsights)
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

        warningReasons.AddRange(schedulerStatus.Warnings.Select(warning => $"scheduler:{warning}"));
        warningReasons.AddRange(schedulerStatus.Jobs
            .Where(job => job.FailureCount > 0 || job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            .Select(job => $"scheduled_job_issue:{job.JobId}:{job.Status}"));

        if (cleanupCandidates > 0)
        {
            warningReasons.Add($"cleanup_candidates:{cleanupCandidates}");
        }

        if (knowledgeQuality.KnowledgeHealth is "critical" or "needs_consolidation")
        {
            warningReasons.Add($"knowledge_health:{knowledgeQuality.KnowledgeHealth}");
        }

        if (knowledgeValidation?.KnowledgeItemsNeedingOos > 0)
        {
            warningReasons.Add($"knowledge_validation_needs_oos:{knowledgeValidation.KnowledgeItemsNeedingOos}");
        }

        if (knowledgeValidation?.InvalidValidationTasks > 0)
        {
            warningReasons.Add($"invalid_validation_tasks:{knowledgeValidation.InvalidValidationTasks}");
        }

        warningReasons.AddRange(topBlockers.Take(5));
        warningReasons = warningReasons
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();

        var overallStatus = criticalReasons.Count > 0
            ? "critical"
            : warningReasons.Count > 0
                ? "warning"
                : "ok";

        var autonomousUpdated = FirstNonEmpty(
            GetString(autonomousLoopSummary, "updated_at_utc", "updatedAtUtc"),
            GetString(autonomousLoopState, "updated_at_utc", "updatedAtUtc"));
        var lastMetaReview = GetString(metaReview, "updated_at_utc", "updatedAtUtc");
        var lastNightlyRun = nightlyState.LastStartUtc?.ToString("O") ?? nightlyState.StartedAtUtc?.ToString("O");
        var resourceAction = GetString(resourceStatus, "action") ?? "unknown";

        return new MasterStatusSnapshot(
            SnapshotVersion: "master_status_snapshot_v1",
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            DataRoot: _storagePaths.Root,
            OverallStatus: overallStatus,
            CurrentFocus: currentFocus,
            ActiveDomains: activeDomains,
            CognitiveStatus: new MasterStatusSection(
                Status: GetString(cognitiveStatus, "status") ?? "unknown",
                ReportPath: Path.Combine(cognitiveRoot, "cognitive_status.json"),
                Metrics: new Dictionary<string, object?>
                {
                    ["sources"] = GetInt(cognitiveStatus, "source_count", "sourceCount"),
                    ["knowledge_items"] = GetInt(cognitiveStatus, "knowledge_item_count", "knowledgeItemCount"),
                    ["trusted_knowledge"] = knowledgeQuality.TrustedKnowledge,
                    ["weak_knowledge"] = knowledgeQuality.WeakKnowledge,
                    ["deprecated_knowledge"] = knowledgeQuality.DeprecatedKnowledge,
                    ["promising_knowledge"] = promotionStatus.PromisingKnowledge,
                    ["robust_knowledge"] = promotionStatus.RobustKnowledge,
                    ["trusted_candidates"] = promotionStatus.TrustedCandidates.TotalCandidates,
                    ["ready_for_promotion"] = promotionStatus.TrustedCandidates.ReadyForPromotion,
                    ["awaiting_human_review"] = promotionStatus.TrustedCandidates.AwaitingHumanReview,
                    ["promotion_health"] = promotionStatus.PromotionHealth,
                    ["average_quality_score"] = knowledgeQuality.AverageQualityScore,
                    ["average_trust_score"] = knowledgeQuality.AverageTrustScore,
                    ["knowledge_health"] = knowledgeQuality.KnowledgeHealth,
                    ["evidence_coverage"] = knowledgeQuality.EvidenceCoverage,
                    ["contradiction_count"] = knowledgeQuality.ContradictionCount,
                    ["human_reviewed_items"] = knowledgeQuality.HumanReviewedItems,
                    ["validation_coverage"] = knowledgeQuality.ValidationCoverage,
                    ["trust_distribution"] = knowledgeQuality.TrustDistribution ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    ["pending_reviews"] = humanReview.PendingReviews,
                    ["approved_reviews"] = humanReview.ApprovedReviews,
                    ["rejected_reviews"] = humanReview.RejectedReviews,
                    ["needs_more_evidence"] = humanReview.NeedsMoreEvidenceReviews,
                    ["deferred_reviews"] = humanReview.DeferredReviews,
                    ["review_coverage"] = humanReview.ReviewCoverage,
                    ["top_review_priorities"] = humanReview.TopReviewPriorities,
                    ["validation_plans_open"] = knowledgeValidation?.ValidationPlansOpen ?? 0,
                    ["validation_tasks_pending"] = knowledgeValidation?.ValidationTasksPending ?? 0,
                    ["trusted_candidate_count"] = knowledgeValidation?.TrustedCandidateCount ?? 0,
                    ["knowledge_items_needing_oos"] = knowledgeValidation?.KnowledgeItemsNeedingOos ?? 0,
                    ["knowledge_items_needing_source_check"] = knowledgeValidation?.KnowledgeItemsNeedingSourceCheck ?? 0,
                    ["invalid_validation_tasks"] = knowledgeValidation?.InvalidValidationTasks ?? 0,
                    ["validation_tasks_cleaned"] = knowledgeValidation?.ValidationTasksCleaned ?? 0,
                    ["validation_routing_health"] = knowledgeValidation?.ValidationRoutingHealth ?? "unknown",
                    ["domain_validation_health"] = domainValidation.DomainValidationHealth,
                    ["documentation_validation_pending"] = domainValidation.DocumentationValidationPending,
                    ["software_validation_pending"] = domainValidation.SoftwareValidationPending,
                    ["process_validation_pending"] = domainValidation.ProcessValidationPending,
                    ["research_validation_pending"] = domainValidation.ResearchValidationPending,
                    ["queue_items"] = FirstPositive(GetInt(cognitiveStatus, "queue_item_count", "queueItemCount"), queuedTasks),
                    ["insights"] = GetInt(cognitiveStatus, "insight_count", "insightCount"),
                    ["active_domains"] = activeDomains
                },
                Warnings: CombineStringLists(
                    GetStringArray(cognitiveStatus, "warnings"),
                    knowledgeQuality.Warnings,
                    domainValidation.DomainValidationWarnings,
                    knowledgeQuality.KnowledgeHealth is "critical" or "needs_consolidation" ? [$"knowledge_health:{knowledgeQuality.KnowledgeHealth}"] : [])),
            ResearchQueueStatus: new MasterStatusSection(
                Status: queuedTasks > 0 ? "open_items" : "empty_or_idle",
                ReportPath: Path.Combine(cognitiveRoot, "research_queue.json"),
                Metrics: new Dictionary<string, object?>
                {
                    ["queued_tasks"] = queuedTasks,
                    ["open_items"] = CountOpenResearchQueueItems(researchQueue),
                    ["total_items"] = GetArrayCount(researchQueue, "items")
                },
                Warnings: GetStringArray(researchQueue, "warnings")),
            AutonomousLoopStatus: new MasterStatusSection(
                Status: FirstNonEmpty(GetString(autonomousLoopSummary, "status"), GetString(autonomousLoopState, "status"), "unknown"),
                ReportPath: Path.Combine(cognitiveRoot, "autonomous_loop_state.json"),
                Metrics: new Dictionary<string, object?>
                {
                    ["summary_path"] = Path.Combine(cognitiveRoot, "autonomous_loop_summary.json"),
                    ["iterations"] = FirstPositive(GetInt(autonomousLoopSummary, "iterations_completed", "iterationsCompleted"), GetInt(autonomousLoopState, "iterations_completed", "iterationsCompleted")),
                    ["work_performed"] = FirstPositive(GetInt(autonomousLoopSummary, "work_performed", "workPerformed"), GetInt(autonomousLoopState, "work_performed", "workPerformed")),
                    ["average_learning_value"] = GetDouble(autonomousLoopSummary, "average_learning_value", "averageLearningValue"),
                    ["next_action"] = FirstNonEmpty(GetString(autonomousLoopSummary, "next_action", "nextAction"), GetString(autonomousLoopState, "next_action", "nextAction"), "-"),
                    ["last_updated_utc"] = autonomousUpdated
                },
                Warnings: CombineStringLists(GetStringArray(autonomousLoopState, "warnings"), GetStringArray(autonomousLoopSummary, "warnings"))),
            NightlyStatus: new MasterStatusSection(
                Status: nightlyState.Status,
                ReportPath: nightlyStatePath,
                Metrics: new Dictionary<string, object?>
                {
                    ["last_start_utc"] = lastNightlyRun,
                    ["last_stop_utc"] = nightlyState.LastStopUtc?.ToString("O"),
                    ["next_scheduled_start_utc"] = nightlyState.NextScheduledStartUtc?.ToString("O"),
                    ["iterations"] = nightlyState.IterationsCompleted,
                    ["work_performed"] = nightlyState.WorkPerformed,
                    ["currently_running"] = nightlyState.CurrentlyRunning,
                    ["next_action"] = nightlyState.NextAction,
                    ["cognitive_jobs_enabled"] = nightlyState.CognitiveJobsEnabled,
                    ["queued_research_items"] = nightlyState.QueuedResearchItems
                },
                Warnings: string.IsNullOrWhiteSpace(nightlyState.LastError) ? [] : [nightlyState.LastError]),
            SchedulerStatus: new MasterStatusSection(
                Status: schedulerStatus.Jobs.Any(job => job.CurrentlyRunning) ? "running_job" : "idle",
                ReportPath: schedulerStatus.StatePath,
                Metrics: new Dictionary<string, object?>
                {
                    ["config_path"] = schedulerStatus.ConfigPath,
                    ["enabled_jobs"] = schedulerStatus.Jobs.Count(job => job.Enabled),
                    ["active_jobs"] = schedulerStatus.Jobs.Count(job => job.CurrentlyRunning),
                    ["failed_jobs"] = schedulerStatus.Jobs.Count(job => job.FailureCount > 0 || job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
                    ["next_job"] = schedulerStatus.Jobs.FirstOrDefault(job => job.NextRunUtc is not null)?.JobId,
                    ["check_interval_seconds"] = schedulerStatus.CheckIntervalSeconds
                },
                Warnings: schedulerStatus.Warnings),
            SupervisorStatus: new MasterStatusSection(
                Status: supervisorProcess.Running ? "running" : supervisorState.Status,
                ReportPath: supervisor.StatePath,
                Metrics: new Dictionary<string, object?>
                {
                    ["heartbeat_path"] = supervisor.HeartbeatPath,
                    ["running"] = supervisorProcess.Running,
                    ["pid"] = supervisorProcess.Pid,
                    ["stale_pid"] = supervisorProcess.StalePid,
                    ["heartbeat_age_seconds"] = supervisorProcess.HeartbeatAgeSeconds,
                    ["current_job"] = supervisorState.CurrentJobId,
                    ["next_action"] = supervisorState.NextAction,
                    ["log_path"] = supervisorProcess.LogPath
                },
                Warnings: CombineStringLists(
                    string.IsNullOrWhiteSpace(supervisorProcess.Warning) ? [] : [supervisorProcess.Warning],
                    string.IsNullOrWhiteSpace(supervisorState.LastError) ? [] : [supervisorState.LastError])),
            ResourceStatus: new MasterStatusSection(
                Status: JsonBool(resourceStatus, false, "should_stop", "shouldStop")
                    ? "critical"
                    : JsonBool(resourceStatus, false, "should_pause", "shouldPause")
                        ? "pause"
                        : resourceAction,
                ReportPath: resourceStatusPath,
                Metrics: new Dictionary<string, object?>
                {
                    ["action"] = resourceAction,
                    ["cpu_usage_percent"] = GetDouble(resourceStatus, "cpu_usage_percent", "cpuUsagePercent"),
                    ["memory_usage_percent"] = GetDouble(resourceStatus, "memory_usage_percent", "memoryUsagePercent"),
                    ["free_disk_percent"] = GetDouble(resourceStatus, "free_disk_percent", "freeDiskPercent"),
                    ["should_pause"] = JsonBool(resourceStatus, false, "should_pause", "shouldPause"),
                    ["should_stop"] = JsonBool(resourceStatus, false, "should_stop", "shouldStop")
                },
                Warnings: GetStringArray(resourceStatus, "warnings")),
            StorageStatus: new MasterStatusSection(
                Status: cleanupCandidates > 0 ? "cleanup_plan_available" : "ok",
                ReportPath: storagePlanPath,
                Metrics: new Dictionary<string, object?>
                {
                    ["storage_status_path"] = storageStatusPath,
                    ["storage_root"] = FirstNonEmpty(GetString(storagePlan, "storage_root", "storageRoot"), GetString(storageStatus, "storage_root", "storageRoot"), _storagePaths.Root),
                    ["cleanup_candidates"] = cleanupCandidates,
                    ["safe_to_apply"] = JsonBool(storagePlan, true, "safe_to_apply", "safeToApply"),
                    ["estimated_bytes_to_free"] = GetInt(storagePlan, "estimated_bytes_to_free", "estimatedBytesToFree")
                },
                Warnings: CombineStringLists(GetStringArray(storageStatus, "warnings"), GetStringArray(storagePlan, "warnings"))),
            TradingDomainStatus: new MasterStatusSection(
                Status: demoBotCandidates > 0 ? "demo_candidates_available" : "research_only",
                ReportPath: Path.Combine(strategyRoot, "research_insights.json"),
                Metrics: new Dictionary<string, object?>
                {
                    ["robust_strategies"] = robustCount,
                    ["overfit_suspected"] = overfitCount,
                    ["high_risk_strategies"] = highRiskCount,
                    ["demo_bot_candidates"] = demoBotCandidates,
                    ["rejected_candidates"] = rejectedCandidates,
                    ["scalping_asset"] = scalpingAsset,
                    ["scalping_candidates_total"] = scalpingCandidatesTotal,
                    ["scalping_robust_candidates"] = scalpingRobustCandidates,
                    ["scalping_rejected_candidates"] = scalpingRejectedCandidates,
                    ["scalping_needs_more_data"] = scalpingNeedsMoreData,
                    ["best_scalping_candidate"] = bestScalpingCandidate,
                    ["signal_agent_specs_ready"] = signalAgentSpecsReady,
                    ["latest_signal_agent_spec"] = latestSignalAgentSpec,
                    ["signal_agent_export_health"] = signalAgentExportHealth,
                    ["certified_candidate_signal_ready"] = certifiedCandidateSignalReady,
                    ["ctrader_bot_specs_ready"] = cTraderBotSpecsReady,
                    ["market_data_assets_available"] = marketDataAvailability.AssetsAvailable,
                    ["market_data_xauusd_available"] = marketDataAvailability.XauusdAvailable,
                    ["market_data_eurusd_available"] = marketDataAvailability.EurusdAvailable,
                    ["market_data_quality_health"] = xauusdQuality.QualityHealth,
                    ["scalping_data_gap"] = scalpingDataGap,
                    ["scalping_robustness_expanded"] = scalpingRobustnessExpanded,
                    ["scalping_final_candidates"] = scalpingFinalCandidates,
                    ["scalping_rejected_after_expansion"] = scalpingRejectedAfterExpansion,
                    ["best_final_scalping_candidate"] = bestFinalScalpingCandidate,
                    ["scalping_monte_carlo_health"] = scalpingMonteCarloHealth,
                    ["scalping_parameter_sensitivity_health"] = scalpingParameterSensitivityHealth,
                    ["scalping_regime_validation_health"] = scalpingRegimeValidationHealth,
                    ["scalping_sensitivity_explainability_health"] = scalpingSensitivityExplainabilityHealth,
                    ["scalping_candidates_with_stable_corridor"] = scalpingCandidatesWithStableCorridor,
                    ["scalping_candidates_blocked_by_sensitivity"] = scalpingCandidatesBlockedBySensitivity,
                    ["best_scalping_parameter_corridor_candidate"] = bestScalpingParameterCorridorCandidate,
                    ["scalping_certification_health"] = scalpingCertificationHealth,
                    ["scalping_certified_candidates"] = scalpingCertifiedCandidates,
                    ["scalping_certification_failed"] = scalpingCertificationFailed,
                    ["best_certified_scalping_candidate"] = bestCertifiedScalpingCandidate,
                    ["scalping_human_review_packages_ready"] = scalpingHumanReviewPackagesReady,
                    ["walkforward_path"] = walkforwardPath,
                    ["walkforward_confidence"] = GetDouble(walkforward, "walkforward_confidence", "walkforwardConfidence"),
                    ["next_validation_recommendations"] = GetStringArray(researchInsights, "next_validation_recommendations", "nextValidationRecommendations").Take(8).ToList()
                },
                Warnings: CombineStringLists(
                    GetStringArray(researchInsights, "warnings"),
                    overfitCount > 0 ? [$"overfit_suspected:{overfitCount}"] : [],
                    highRiskCount > 0 ? [$"high_risk_strategies:{highRiskCount}"] : [])),
            SafetyFlags: new MasterStatusSafetyFlags(
                NoAutoTrading: noAutoTrading,
                HumanReviewRequired: humanReviewRequired,
                BrokerOrdersEnabled: false,
                LiveTradingEnabled: false,
                NoBrokerOrders: true,
                NoTradingExecution: true),
            Warnings: warningReasons,
            TopBlockers: topBlockers,
            NextRecommendedActions: nextRecommendedActions,
            QueuedTasks: queuedTasks,
            LastNightlyRun: lastNightlyRun,
            LastAutonomousLoop: autonomousUpdated,
            LastMetaReview: lastMetaReview,
            LearningStrategy: learningStrategyName,
            SupervisorRunning: supervisorProcess.Running,
            SchedulerEnabled: schedulerStatus.Jobs.Count(job => job.Enabled),
            ResourceAction: resourceAction,
            StorageCleanup: cleanupCandidates,
            RobustStrategies: robustCount,
            DemoBotCandidates: demoBotCandidates,
            TrustedKnowledge: knowledgeQuality.TrustedKnowledge,
            WeakKnowledge: knowledgeQuality.WeakKnowledge,
            DeprecatedKnowledge: knowledgeQuality.DeprecatedKnowledge,
            AverageQualityScore: knowledgeQuality.AverageQualityScore,
            AverageTrustScore: knowledgeQuality.AverageTrustScore,
            KnowledgeHealth: knowledgeQuality.KnowledgeHealth,
            KnowledgeTrend: knowledgeQuality.KnowledgeTrend,
            EvidenceCoverage: knowledgeQuality.EvidenceCoverage,
            ContradictionCount: knowledgeQuality.ContradictionCount,
            HumanReviewedItems: knowledgeQuality.HumanReviewedItems,
            ValidationCoverage: knowledgeQuality.ValidationCoverage,
            TrustDistribution: knowledgeQuality.TrustDistribution ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            PendingReviews: humanReview.PendingReviews,
            ApprovedReviews: humanReview.ApprovedReviews,
            RejectedReviews: humanReview.RejectedReviews,
            NeedsMoreEvidenceReviews: humanReview.NeedsMoreEvidenceReviews,
            DeferredReviews: humanReview.DeferredReviews,
            ReviewCoverage: humanReview.ReviewCoverage,
            TopReviewPriorities: humanReview.TopReviewPriorities,
            ValidationPlansOpen: knowledgeValidation?.ValidationPlansOpen ?? 0,
            ValidationTasksPending: knowledgeValidation?.ValidationTasksPending ?? 0,
            TrustedCandidateCount: knowledgeValidation?.TrustedCandidateCount ?? 0,
            KnowledgeItemsNeedingOos: knowledgeValidation?.KnowledgeItemsNeedingOos ?? 0,
            KnowledgeItemsNeedingSourceCheck: knowledgeValidation?.KnowledgeItemsNeedingSourceCheck ?? 0,
            InvalidValidationTasks: knowledgeValidation?.InvalidValidationTasks ?? 0,
            ValidationTasksCleaned: knowledgeValidation?.ValidationTasksCleaned ?? 0,
            ValidationRoutingHealth: knowledgeValidation?.ValidationRoutingHealth ?? "unknown",
            DomainValidationHealth: domainValidation.DomainValidationHealth,
            DocumentationValidationPending: domainValidation.DocumentationValidationPending,
            SoftwareValidationPending: domainValidation.SoftwareValidationPending,
            ProcessValidationPending: domainValidation.ProcessValidationPending,
            ResearchValidationPending: domainValidation.ResearchValidationPending,
            ScalpingAsset: scalpingAsset,
            ScalpingCandidatesTotal: scalpingCandidatesTotal,
            ScalpingRobustCandidates: scalpingRobustCandidates,
            ScalpingRejectedCandidates: scalpingRejectedCandidates,
            ScalpingNeedsMoreData: scalpingNeedsMoreData,
            BestScalpingCandidate: bestScalpingCandidate,
            SignalAgentSpecsReady: signalAgentSpecsReady,
            CTraderBotSpecsReady: cTraderBotSpecsReady,
            LatestSignalAgentSpec: latestSignalAgentSpec,
            SignalAgentExportHealth: signalAgentExportHealth,
            CertifiedCandidateSignalReady: certifiedCandidateSignalReady,
            MarketDataAssetsAvailable: marketDataAvailability.AssetsAvailable,
            MarketDataXauusdAvailable: marketDataAvailability.XauusdAvailable,
            MarketDataEurusdAvailable: marketDataAvailability.EurusdAvailable,
            MarketDataQualityHealth: xauusdQuality.QualityHealth,
            ScalpingDataGap: scalpingDataGap,
            ScalpingRobustnessExpanded: scalpingRobustnessExpanded,
            ScalpingFinalCandidates: scalpingFinalCandidates,
            ScalpingRejectedAfterExpansion: scalpingRejectedAfterExpansion,
            BestFinalScalpingCandidate: bestFinalScalpingCandidate,
            ScalpingMonteCarloHealth: scalpingMonteCarloHealth,
            ScalpingParameterSensitivityHealth: scalpingParameterSensitivityHealth,
            ScalpingRegimeValidationHealth: scalpingRegimeValidationHealth,
            ScalpingSensitivityExplainabilityHealth: scalpingSensitivityExplainabilityHealth,
            ScalpingCandidatesWithStableCorridor: scalpingCandidatesWithStableCorridor,
            ScalpingCandidatesBlockedBySensitivity: scalpingCandidatesBlockedBySensitivity,
            BestScalpingParameterCorridorCandidate: bestScalpingParameterCorridorCandidate,
            ScalpingCertificationHealth: scalpingCertificationHealth,
            ScalpingCertifiedCandidates: scalpingCertifiedCandidates,
            ScalpingCertificationFailed: scalpingCertificationFailed,
            BestCertifiedScalpingCandidate: bestCertifiedScalpingCandidate,
            ScalpingHumanReviewPackagesReady: scalpingHumanReviewPackagesReady,
            DomainValidationWarnings: domainValidation.DomainValidationWarnings,
            ActiveGoals: goalState.Goals.Where(goal => goal.Active).Select(goal => goal.GoalId).ToList(),
            TopGoal: goalState.TopGoalId,
            BlockedGoals: goalState.BlockedGoals,
            GoalProgressSummary: goalState.Goals.ToDictionary(goal => goal.GoalId, goal => goal.ProgressScore, StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: noAutoTrading,
            HumanReviewRequired: humanReviewRequired,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static JsonElement LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.Clone();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                if (attempt == 4)
                {
                    return default;
                }

                Thread.Sleep(50);
            }
        }

        return default;
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out value))
            {
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
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
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

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : 0;
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

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number)
            ? number
            : 0;
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

    private static IReadOnlyList<string> GetStringArray(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        return [];
    }

    private static int GetArrayCount(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
    }

    private static int CountOpenResearchQueueItems(JsonElement queue)
    {
        if (!TryGetProperty(queue, out var items, "items") || items.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return items.EnumerateArray()
            .Count(item => string.Equals(GetString(item, "status") ?? "open", "open", StringComparison.OrdinalIgnoreCase));
    }

    private static bool SafetyFlagTrue(IEnumerable<JsonElement> roots, params string[] names)
    {
        foreach (var root in roots)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

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

    private static int FirstPositive(params int[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static List<string> CombineStringLists(params IEnumerable<string>[] lists)
    {
        return lists
            .SelectMany(item => item)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
