using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class NeedDetectionEngine
{
    private readonly StoragePaths _storagePaths;

    public NeedDetectionEngine(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string NeedsPath => Path.Combine(Root, "detected_needs.json");

    public IReadOnlyList<DetectedNeed> Detect()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var needs = new List<DetectedNeed>();
        var sourceRegistry = new KnowledgeSourceRegistry(_storagePaths);
        var sources = sourceRegistry.LoadOrCreateSources();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var cognitiveInsights = new HypothesisGenerator(_storagePaths).LoadInsights();
        var researchInsights = new ResearchInsightsGenerator(_storagePaths).LoadInsights();
        var walkForward = new WalkForwardValidationService(_storagePaths).LoadReport();
        var resource = new ResourceGuard(_storagePaths).Check();
        var cleanupPlan = new StorageHygieneService(_storagePaths).LoadPlan();
        var domainService = new DomainCognitiveService(_storagePaths);
        var domainStatus = domainService.BuildStatus();
        var domainInsights = domainService.BuildInsights(domainStatus);

        DateTimeOffset? lastSourceCheck = sources.Count == 0
            ? null
            : sources.Max(source => source.LastCheckedUtc);
        if (sources.Count == 0 || lastSourceCheck is null)
        {
            needs.Add(Need(
                "knowledge_sources_missing",
                NeedCategory.knowledge_gap,
                NeedSeverity.high,
                "trading",
                "Knowledge Sources fehlen",
                "Es sind keine Cognitive Knowledge Sources registriert.",
                [sourceRegistry.SourcesPath],
                ["scan_knowledge_sources"]));
        }
        else if ((now - lastSourceCheck.Value).TotalHours > 24)
        {
            needs.Add(Need(
                "knowledge_sources_stale",
                NeedCategory.knowledge_gap,
                NeedSeverity.medium,
                "trading",
                "Knowledge Sources veraltet",
                $"Letzter Knowledge-Source-Scan ist {Math.Round((now - lastSourceCheck.Value).TotalHours, 1)} Stunden alt.",
                [sourceRegistry.SourcesPath],
                ["scan_knowledge_sources"]));
        }

        if (catalog.Count == 0)
        {
            needs.Add(Need(
                "knowledge_catalog_empty",
                NeedCategory.knowledge_gap,
                NeedSeverity.high,
                "trading",
                "Knowledge Catalog leer",
                "Der Cognitive Knowledge Catalog enthält keine nutzbaren Items.",
                [new KnowledgeCatalog(_storagePaths).CatalogPath],
                ["scan_knowledge_sources", "generate_hypotheses"]));
        }

        var knowledgeQuality = new KnowledgeQualityEngine(_storagePaths).LoadReport();
        if (knowledgeQuality is null)
        {
            needs.Add(Need(
                "knowledge_quality_missing",
                NeedCategory.knowledge_gap,
                NeedSeverity.medium,
                "research",
                "Knowledge Quality Report fehlt",
                "Hermes kann noch nicht erklaeren, warum Knowledge Items vertrauenswuerdig sind.",
                [new KnowledgeQualityEngine(_storagePaths).QualityPath],
                ["evaluate_knowledge_quality", "consolidate_memory"]));
        }
        else
        {
            if (knowledgeQuality.WeakKnowledge > Math.Max(5, knowledgeQuality.TotalKnowledgeItems / 3))
            {
                needs.Add(Need(
                    "weak_knowledge_detected",
                    NeedCategory.quality_risk,
                    NeedSeverity.medium,
                    "research",
                    "Zu viele schwache Knowledge Items",
                    $"Weak={knowledgeQuality.WeakKnowledge}, total={knowledgeQuality.TotalKnowledgeItems}, average_quality={knowledgeQuality.AverageQualityScore:0.####}.",
                    [new KnowledgeQualityEngine(_storagePaths).QualityPath],
                    ["generate_validation_plans", "validate_knowledge_items", "consolidate_memory", "evaluate_knowledge_quality"]));
            }

            if (knowledgeQuality.DeprecatedKnowledge > 0)
            {
                needs.Add(Need(
                    "deprecated_knowledge_present",
                    NeedCategory.maintenance,
                    NeedSeverity.low,
                    "research",
                    "Deprecated Knowledge markieren",
                    $"{knowledgeQuality.DeprecatedKnowledge} Knowledge Items sind veraltet oder retention-deprecated.",
                    [new KnowledgeQualityEngine(_storagePaths).QualityPath, new MemoryConsolidationService(_storagePaths).ConsolidationPath],
                    ["consolidate_memory", "evaluate_knowledge_quality"]));
            }
        }

        var validationStatus = new KnowledgeValidationStrategy(_storagePaths).LoadStatus();
        if (validationStatus is null && knowledgeQuality?.WeakKnowledge > 0)
        {
            needs.Add(Need(
                "knowledge_validation_plans_missing",
                NeedCategory.validation_gap,
                NeedSeverity.high,
                "research",
                "Knowledge Validation Plans fehlen",
                "Schwaches Wissen existiert, aber es gibt noch keine konkreten Validierungsplaene.",
                [new KnowledgeValidationStrategy(_storagePaths).PlansPath],
                ["generate_validation_plans"]));
        }
        else if (validationStatus is not null)
        {
            if (validationStatus.ValidationPlansOpen > 0 && validationStatus.ValidationTasksPending == 0)
            {
                needs.Add(Need(
                    "knowledge_validation_queue_missing",
                    NeedCategory.validation_gap,
                    NeedSeverity.high,
                    "research",
                    "Validation Plans ohne Queue Tasks",
                    $"{validationStatus.ValidationPlansOpen} offene Validation Plans haben keine offenen Queue-Tasks.",
                    [validationStatus.PlansPath, validationStatus.ResearchQueuePath],
                    ["validate_knowledge_items", "process_research_queue"]));
            }

            if (validationStatus.KnowledgeItemsNeedingOos > 0)
            {
                needs.Add(Need(
                    "knowledge_items_need_oos_validation",
                    NeedCategory.data_gap,
                    NeedSeverity.high,
                    "trading",
                    "Knowledge Items brauchen OOS-Validierung",
                    $"{validationStatus.KnowledgeItemsNeedingOos} Knowledge Items duerfen ohne OOS/Walk-Forward-Evidenz nicht trusted werden.",
                    [validationStatus.PlansPath],
                    ["download_missing_market_data", "run_walkforward_validation", "generate_validation_plans"]));
            }

            if (validationStatus.KnowledgeItemsNeedingSourceCheck > 0)
            {
                needs.Add(Need(
                    "knowledge_items_need_source_check",
                    NeedCategory.knowledge_gap,
                    NeedSeverity.medium,
                    "research",
                    "Knowledge Items brauchen Source Checks",
                    $"{validationStatus.KnowledgeItemsNeedingSourceCheck} Knowledge Items benoetigen Quellen- oder Cross-Source-Pruefung.",
                    [validationStatus.RequirementsPath],
                    ["scan_knowledge_sources", "generate_validation_plans"]));
            }
        }

        var openQueue = queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
        if (openQueue == 0)
        {
            needs.Add(Need(
                "research_queue_empty",
                NeedCategory.validation_gap,
                NeedSeverity.medium,
                "trading",
                "Research Queue leergelaufen",
                "Es sind keine offenen Research-Queue-Items vorhanden.",
                [new ResearchQueueService(_storagePaths).QueuePath],
                ["process_research_queue", "generate_hypotheses"]));
        }
        else if (openQueue > 100)
        {
            needs.Add(Need(
                "research_queue_backlog",
                NeedCategory.validation_gap,
                NeedSeverity.high,
                "trading",
                "Research Queue Backlog",
                $"Es warten {openQueue} offene Research-Queue-Items.",
                [new ResearchQueueService(_storagePaths).QueuePath],
                ["process_research_queue"]));
        }

        if (cognitiveInsights.Count > 0 && openQueue == 0)
        {
            needs.Add(Need(
                "hypotheses_without_validation_queue",
                NeedCategory.validation_gap,
                NeedSeverity.high,
                "trading",
                "Hypothesen ohne Validierungsarbeit",
                $"Es gibt {cognitiveInsights.Count} Cognitive Insights, aber keine offenen Queue-Items zur Validierung.",
                [new HypothesisGenerator(_storagePaths).InsightsPath],
                ["process_research_queue", "run_walkforward_validation"]));
        }

        if (walkForward is null)
        {
            needs.Add(Need(
                "walkforward_missing",
                NeedCategory.data_gap,
                NeedSeverity.high,
                "trading",
                "Walk-Forward/OOS Report fehlt",
                "Es liegt kein aktueller Walk-Forward-Validation-Report vor.",
                [new WalkForwardValidationService(_storagePaths).WalkForwardPath],
                ["run_walkforward_validation"]));
        }
        else
        {
            var oos = walkForward.Assessments.Count(item => item.OosAvailable);
            if (oos == 0)
            {
                needs.Add(Need(
                    "oos_data_missing",
                    NeedCategory.data_gap,
                    NeedSeverity.high,
                    "trading",
                    "Out-of-Sample Daten fehlen",
                    "Keine Strategie verfügt aktuell über ausreichende OOS-Bewertung.",
                    [new WalkForwardValidationService(_storagePaths).WalkForwardPath],
                    ["download_missing_market_data", "run_walkforward_validation"]));
            }

            if (walkForward.OverfitSuspectedStrategies > Math.Max(3, walkForward.RobustStrategies))
            {
                needs.Add(Need(
                    "too_many_overfit_candidates",
                    NeedCategory.quality_risk,
                    NeedSeverity.high,
                    "trading",
                    "Zu viele Overfit-Kandidaten",
                    $"Overfit={walkForward.OverfitSuspectedStrategies}, robust={walkForward.RobustStrategies}.",
                    [new WalkForwardValidationService(_storagePaths).OverfitReportPath],
                    ["run_overfit_report", "run_realism_report", "run_walkforward_validation"]));
            }
        }

        var robustCount = researchInsights?.RobustStrategies?.Count ?? walkForward?.RobustStrategies ?? 0;
        if (robustCount == 0)
        {
            needs.Add(Need(
                "no_robust_strategies",
                NeedCategory.quality_risk,
                NeedSeverity.high,
                "trading",
                "Keine robusten Strategien",
                "Aktuell ist keine Strategie als robust ausgewiesen.",
                [new ResearchInsightsGenerator(_storagePaths).InsightsPath],
                ["run_strategy_research", "run_walkforward_validation", "run_overfit_report"]));
        }

        if (resource.ShouldStop || resource.ShouldPause || resource.Warnings.Count > 0)
        {
            needs.Add(Need(
                resource.ShouldStop ? "resource_guard_stop" : "resource_guard_warning",
                NeedCategory.resource_risk,
                resource.ShouldStop ? NeedSeverity.critical : NeedSeverity.high,
                "process",
                "ResourceGuard meldet Risiko",
                resource.Warnings.Count == 0 ? $"Resource action: {resource.Action}." : string.Join("; ", resource.Warnings),
                [new ResourceGuard(_storagePaths).StatusPath],
                ["run_storage_hygiene"]));
        }

        if (cleanupPlan?.Candidates.Count > 0)
        {
            needs.Add(Need(
                "storage_cleanup_candidates",
                NeedCategory.maintenance,
                cleanupPlan.Candidates.Count > 1000 ? NeedSeverity.high : NeedSeverity.medium,
                "process",
                "Storage Hygiene Arbeit offen",
                $"{cleanupPlan.Candidates.Count} sichere Cleanup-Kandidaten sind geplant.",
                [new StorageHygieneService(_storagePaths).CleanupPlanPath],
                ["run_storage_hygiene"]));
        }

        foreach (var entry in domainStatus.Domains.Where(entry => entry.Active))
        {
            var domainItems = domainService.LoadKnowledgeItems(entry.Domain);
            if (entry.Domain.Equals("software", StringComparison.OrdinalIgnoreCase)
                && (entry.LastScannedAtUtc is null || entry.KnowledgeItemCount == 0))
            {
                needs.Add(Need(
                    "software_domain_missing_scan",
                    NeedCategory.domain_gap,
                    NeedSeverity.medium,
                    "software",
                    "Software-Domäne ohne aktuellen Scan",
                    "Die aktive Software-Domäne braucht strukturierte Repo-, Architektur- und Test-Knowledge-Items.",
                    [DomainFile(entry.Domain, "domain_profile.json"), DomainFile(entry.Domain, "knowledge_items.json")],
                    ["scan_software_domain", "generate_domain_insights"]));
            }

            if (entry.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase)
                && (entry.LastScannedAtUtc is null
                    || entry.KnowledgeItemCount == 0
                    || entry.Warnings.Count > 0
                    || domainItems.Any(item => item.Tags.Contains("todo", StringComparer.OrdinalIgnoreCase)
                        || item.Tags.Contains("documentation_gap", StringComparer.OrdinalIgnoreCase))))
            {
                needs.Add(Need(
                    "documentation_gaps",
                    NeedCategory.knowledge_gap,
                    NeedSeverity.medium,
                    "documentation",
                    "Dokumentationsluecken prüfen",
                    "Die Dokumentations-Domäne meldet fehlende, offene oder noch nicht geprüfte Dokumentationssignale.",
                    [DomainFile(entry.Domain, "knowledge_items.json"), domainService.DomainInsightsPath],
                    ["scan_documentation_domain", "generate_domain_insights"]));
            }

            if (entry.Domain.Equals("process", StringComparison.OrdinalIgnoreCase)
                && (entry.LastScannedAtUtc is null
                    || domainItems.Any(item => item.Tags.Contains("automation_candidate", StringComparer.OrdinalIgnoreCase))))
            {
                needs.Add(Need(
                    "process_automation_candidates",
                    NeedCategory.domain_gap,
                    NeedSeverity.low,
                    "process",
                    "Prozess-Automation Kandidaten prüfen",
                    "Die Prozess-Domäne enthält wiederkehrende Workflows oder sichere Automationskandidaten.",
                    [DomainFile(entry.Domain, "knowledge_items.json")],
                    ["scan_process_domain", "generate_domain_insights"]));
            }

            if (entry.Domain.Equals("research", StringComparison.OrdinalIgnoreCase)
                && (entry.LastScannedAtUtc is null || (now - entry.LastScannedAtUtc.Value).TotalDays > 7))
            {
                needs.Add(Need(
                    "research_sources_stale",
                    NeedCategory.knowledge_gap,
                    NeedSeverity.medium,
                    "research",
                    "Research-Quellen veraltet oder ungescannt",
                    "Die allgemeine Research-Domäne benötigt einen aktuellen metadata-only Source Scan.",
                    [DomainFile(entry.Domain, "knowledge_sources.json")],
                    ["scan_research_domain", "generate_domain_insights"]));
            }

            var hasRecentDomainInsight = domainInsights.Insights.Any(insight =>
                insight.Domain.Equals(entry.Domain, StringComparison.OrdinalIgnoreCase));
            if (!hasRecentDomainInsight
                && entry.LastScannedAtUtc is not null
                && (now - entry.LastScannedAtUtc.Value).TotalHours > 24)
            {
                needs.Add(Need(
                    $"domain_without_recent_insights_{entry.Domain}",
                    NeedCategory.domain_gap,
                    NeedSeverity.low,
                    entry.Domain,
                    $"Domäne {entry.Domain} ohne aktuelle Insights",
                    "Aktive Domänen sollen regelmäßig zusammengefasst werden, damit Trading nicht alle kognitiven Ressourcen bindet.",
                    [domainService.DomainInsightsPath, DomainFile(entry.Domain, "domain_profile.json")],
                    ["generate_domain_insights"]));
            }
        }

        var goalState = LoadGoalState();
        if (goalState is not null)
        {
            var activeGoalIds = goalState.Goals
                .Where(goal => goal.Active)
                .Select(goal => goal.GoalId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (activeGoalIds.Contains("improve_autonomous_planning_quality")
                && !File.Exists(new TaskOutcomeEvaluator(_storagePaths).StatusPath))
            {
                needs.Add(Need(
                    "goal_planning_feedback_missing",
                    NeedCategory.validation_gap,
                    NeedSeverity.medium,
                    "research",
                    "Planning-Feedback fehlt",
                    "Das aktive Ziel improve_autonomous_planning_quality benötigt Outcome Feedback, damit Prioritäten angepasst werden können.",
                    [new TaskOutcomeEvaluator(_storagePaths).StatusPath],
                    ["process_research_queue", "generate_cognitive_insights"]));
            }

            if (activeGoalIds.Contains("improve_research_efficiency")
                && queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)) > 50)
            {
                needs.Add(Need(
                    "goal_research_efficiency_queue_backlog",
                    NeedCategory.validation_gap,
                    NeedSeverity.medium,
                    "process",
                    "Research-Effizienz durch Queue-Backlog belastet",
                    "Das aktive Ziel improve_research_efficiency priorisiert Queue-Verarbeitung und Redundanzabbau.",
                    [new ResearchQueueService(_storagePaths).QueuePath],
                    ["process_research_queue", "generate_cognitive_insights"]));
            }

            if (activeGoalIds.Contains("improve_knowledge_quality") && knowledgeQuality is not null && knowledgeQuality.AverageQualityScore < 0.58)
            {
                needs.Add(Need(
                    "goal_knowledge_quality_low",
                    NeedCategory.quality_risk,
                    NeedSeverity.medium,
                    "research",
                    "Knowledge Quality unter Zielwert",
                    $"Das aktive Ziel improve_knowledge_quality priorisiert Konsolidierung; average_quality={knowledgeQuality.AverageQualityScore:0.####}.",
                    [new KnowledgeQualityEngine(_storagePaths).QualityPath],
                    ["generate_validation_plans", "validate_knowledge_items", "consolidate_memory", "evaluate_knowledge_quality"]));
            }

            if (activeGoalIds.Contains("reduce_low_confidence_knowledge") && knowledgeQuality is not null && knowledgeQuality.WeakKnowledge > 0)
            {
                needs.Add(Need(
                    "goal_low_confidence_knowledge",
                    NeedCategory.quality_risk,
                    NeedSeverity.medium,
                    "research",
                    "Low-Confidence Knowledge reduzieren",
                    $"{knowledgeQuality.WeakKnowledge} Knowledge Items benoetigen bessere Evidenz, Validierung oder Deprecation-Markierung.",
                    [new KnowledgeQualityEngine(_storagePaths).QualityPath, new KnowledgeQualityEngine(_storagePaths).EvidencePath],
                    ["generate_validation_plans", "validate_knowledge_items", "consolidate_memory", "evaluate_knowledge_quality"]));
            }
        }

        var distinct = needs
            .GroupBy(need => need.NeedId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(need => SeverityRank(need.Severity)).First())
            .OrderByDescending(need => SeverityRank(need.Severity))
            .ThenBy(need => need.NeedId, StringComparer.Ordinal)
            .ToList();
        File.WriteAllText(NeedsPath, JsonSerializer.Serialize(distinct, JsonDefaults.WriteOptions));
        return distinct;

        DetectedNeed Need(
            string id,
            NeedCategory category,
            NeedSeverity severity,
            string domain,
            string title,
            string description,
            IReadOnlyList<string> evidenceRefs,
            IReadOnlyList<string> suggestedTaskTypes) =>
            new(
                NeedId: id,
                Category: category,
                Severity: severity,
                Domain: domain,
                Title: title,
                Description: description,
                EvidenceRefs: evidenceRefs,
                SuggestedTaskTypes: suggestedTaskTypes,
                DetectedAtUtc: now,
                NoTradingExecution: true,
                HumanReviewRequired: true);

        string DomainFile(string domain, string fileName) =>
            Path.Combine(domainService.DomainsRoot, domain, fileName);
    }

    public IReadOnlyList<DetectedNeed> LoadNeeds()
    {
        if (!File.Exists(NeedsPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<DetectedNeed>>(
                File.ReadAllText(NeedsPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    internal static int SeverityRank(NeedSeverity severity) =>
        severity switch
        {
            NeedSeverity.critical => 4,
            NeedSeverity.high => 3,
            NeedSeverity.medium => 2,
            _ => 1
        };

    private GoalState? LoadGoalState()
    {
        var path = new GoalManager(_storagePaths).GoalStatePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoalState>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}

public sealed class GoalManager
{
    private readonly StoragePaths _storagePaths;

    public GoalManager(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core", "goals");

    public string LegacyRoot => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string GoalProgressPath => Path.Combine(Root, "goal_progress.json");

    public string GoalStatePath => Path.Combine(Root, "goal_state.json");

    public string GoalOutcomesPath => Path.Combine(Root, "goal_outcomes.jsonl");

    public IReadOnlyList<HermesGoal> EvaluateGoals(IReadOnlyList<DetectedNeed> needs)
    {
        return EvaluateGoalState(needs).Goals;
    }

    internal static int DefaultGoalCount => Defaults().Count;

    public GoalState EvaluateGoalState(IReadOnlyList<DetectedNeed> needs)
    {
        Directory.CreateDirectory(Root);
        var goalFeedback = new TaskOutcomeEvaluator(_storagePaths).LoadGoalFeedback();
        var recentOutcomes = new TaskOutcomeEvaluator(_storagePaths).LoadOutcomes(250);
        var goals = Defaults()
            .Select(goal =>
            {
                var blockers = BlockersFor(goal.GoalId, needs);
                var feedback = goalFeedback?.Goals.FirstOrDefault(item =>
                    item.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase));
                var goalOutcomes = recentOutcomes
                    .Where(outcome => outcome.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(outcome => outcome.EvaluatedAtUtc)
                    .Take(8)
                    .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}:{outcome.OutcomeScore.UsefulnessScore:0.####}")
                    .ToList();
                var nextActions = NextActionsFor(goal.GoalId, blockers, needs)
                    .Concat(feedback?.RecommendedActions ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
                var severityPenalty = blockers
                    .Select(blocker => needs.FirstOrDefault(need => need.NeedId.Equals(blocker, StringComparison.OrdinalIgnoreCase)))
                    .Where(need => need is not null)
                    .Sum(need => NeedDetectionEngine.SeverityRank(need!.Severity) * 0.08);
                var progress = Math.Round(Math.Clamp(0.78 - severityPenalty + (feedback?.ProgressDelta ?? 0), 0.05, 0.95), 4);
                var relatedTasks = needs
                    .Where(need => blockers.Contains(need.NeedId, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(need => need.SuggestedTaskTypes)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToList();
                var currentState = blockers.Count == 0
                    ? progress >= 0.8 ? "on_track" : "active"
                    : blockers.Count >= 3 || progress < 0.35 ? "blocked" : "needs_attention";
                return goal with
                {
                    CurrentState = currentState,
                    ProgressScore = progress,
                    BlockerCount = blockers.Count,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                    NextRecommendedActions = nextActions,
                    RelatedNeeds = blockers,
                    RelatedTasks = relatedTasks,
                    RecentOutcomes = goalOutcomes,
                    Blockers = blockers,
                    NextActions = nextActions
                };
            })
            .ToList();

        goals = new GoalPriorityAdjuster().AdjustPriorities(goals, goalFeedback)
            .OrderBy(goal => goal.Priority)
            .ToList();

        var progressItems = goals
            .Select(goal => new GoalProgress(
                GoalId: goal.GoalId,
                Title: goal.Title,
                Domain: goal.Domain,
                Priority: goal.Priority,
                TargetState: goal.TargetState,
                CurrentState: goal.CurrentState,
                ProgressScore: goal.ProgressScore,
                BlockerCount: goal.BlockerCount,
                RelatedNeeds: goal.RelatedNeeds,
                RelatedTasks: goal.RelatedTasks,
                RecentOutcomes: goal.RecentOutcomes,
                NextRecommendedActions: goal.NextRecommendedActions,
                Blockers: goal.Blockers,
                NextActions: goal.NextActions,
                UpdatedAtUtc: DateTimeOffset.UtcNow))
            .ToList();
        var progressReport = new GoalProgressReport(
            ReportVersion: "goal_progress_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Goals: progressItems,
            ProgressSummary: goals.ToDictionary(goal => goal.GoalId, goal => goal.ProgressScore, StringComparer.OrdinalIgnoreCase),
            BlockedGoals: goals.Where(goal => goal.BlockerCount > 0).Select(goal => goal.GoalId).ToList(),
            TopNextActions: goals.SelectMany(goal => goal.NextRecommendedActions.Select(action => $"{goal.GoalId}:{action}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(GoalProgressPath, JsonSerializer.Serialize(progressReport, JsonDefaults.WriteOptions));
        var state = new GoalState(
            StateVersion: "goal_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Goals: goals,
            ActiveGoals: goals.Count(goal => goal.Active),
            TopGoalId: goals.FirstOrDefault(goal => goal.Active)?.GoalId ?? string.Empty,
            BlockedGoals: goals.Where(goal => goal.BlockerCount > 0 || goal.CurrentState == "blocked").Select(goal => goal.GoalId).ToList(),
            Warnings: goals
                .Where(goal => goal.ProgressScore < 0.35 || goal.BlockerCount >= 3)
                .Select(goal => $"{goal.GoalId}:{goal.CurrentState}:progress={goal.ProgressScore:0.####}")
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(GoalStatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    private static IReadOnlyList<HermesGoal> Defaults() =>
    [
        Goal("improve_trading_robustness", "Trading-Robustheit verbessern", "trading", "Trading-Domain robuster und OOS-stabiler bewerten.", "Robuste, realistisch bewertete Trading-Research-Kandidaten mit OOS- und Kostenresilienz.", 10),
        Goal("reduce_overfit_risk", "Overfit-Risiko reduzieren", "trading", "Overfit-Risiko durch Validierung und Realism-Gates reduzieren.", "Overfit-Verdacht sinkt; zu perfekte Strategien werden markiert oder abgelehnt.", 20),
        Goal("expand_knowledge_sources", "Knowledge Sources erweitern", "research", "Kuratierte Quellen aktuell halten und Knowledge Gaps schließen.", "Kuratierte Quellen sind aktuell, vertrauensbewertet und im Knowledge Catalog nutzbar.", 30),
        Goal("improve_cognitive_memory_quality", "Cognitive Memory verbessern", "research", "Research Queue, Hypothesen und Insights in verwertbare Erinnerung überführen.", "Memory enthält validierte, nicht redundante Knowledge Items und klare Insights.", 40),
        Goal("maintain_storage_health", "Storage Health sichern", "process", "Storage/Resource-Zustand für Dauerbetrieb stabil halten.", "Dauerbetrieb bleibt innerhalb ResourceGuard-/StorageGuard-Grenzen.", 50),
        Goal("prepare_multi_domain_learning", "Multi-Domain Learning vorbereiten", "research", "Nicht-Trading-Domänen strukturiert vorbereiten, ohne Trading zum Kern zu machen.", "Software, Documentation, Process und Research liefern nutzbare Domain-Signale.", 60),
        Goal("improve_autonomous_planning_quality", "Planning-Qualität verbessern", "research", "Needs, Tasks und Feedback zielgerichteter verbinden.", "Planner erzeugt wenige, relevante, nicht redundante Tasks mit messbarem Nutzen.", 70),
        Goal("improve_research_efficiency", "Research-Effizienz verbessern", "process", "Mehr Lernwert pro kontrolliertem Task erreichen und Doppelarbeit reduzieren.", "Wiederholte Low-Value-Tasks werden reduziert, High-Learning-Tasks priorisiert.", 80),
        Goal("improve_knowledge_quality", "Knowledge Quality verbessern", "research", "Wissen nach Trust, Evidenz, Validierung, Wiederverwendung und Alter bewerten.", "Knowledge Items besitzen nachvollziehbare Quality Scores und Evidenzbelege.", 90),
        Goal("reduce_low_confidence_knowledge", "Low-Confidence Knowledge reduzieren", "research", "Schwaches, unvalidiertes oder veraltetes Wissen markieren und priorisiert nacharbeiten.", "Weak Knowledge sinkt; deprecated Wissen bleibt markiert, aber wird nicht geloescht.", 95)
    ];

    private static HermesGoal Goal(string id, string title, string domain, string description, string targetState, int priority) =>
        new(
            GoalId: id,
            Title: title,
            Domain: domain,
            Description: description,
            Priority: priority,
            Active: true,
            TargetState: targetState,
            CurrentState: "not_evaluated",
            ProgressScore: 0.5,
            BlockerCount: 0,
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            NextRecommendedActions: [],
            RelatedNeeds: [],
            RelatedTasks: [],
            RecentOutcomes: [],
            Blockers: [],
            NextActions: []);

    private static IReadOnlyList<string> BlockersFor(string goalId, IReadOnlyList<DetectedNeed> needs) =>
        goalId switch
        {
            "improve_trading_robustness" => NeedIds(needs, NeedCategory.quality_risk, NeedCategory.data_gap, NeedCategory.validation_gap),
            "reduce_overfit_risk" => NeedIds(needs, NeedCategory.quality_risk, NeedCategory.validation_gap),
            "expand_knowledge_sources" => NeedIds(needs, NeedCategory.knowledge_gap, NeedCategory.domain_gap),
            "improve_cognitive_memory_quality" => NeedIds(needs, NeedCategory.validation_gap, NeedCategory.knowledge_gap),
            "maintain_storage_health" => NeedIds(needs, NeedCategory.resource_risk, NeedCategory.maintenance),
            "prepare_multi_domain_learning" => NeedIds(needs, NeedCategory.domain_gap, NeedCategory.knowledge_gap),
            "improve_autonomous_planning_quality" => NeedIds(needs, NeedCategory.validation_gap, NeedCategory.domain_gap, NeedCategory.quality_risk),
            "improve_research_efficiency" => NeedIds(needs, NeedCategory.maintenance, NeedCategory.validation_gap, NeedCategory.resource_risk),
            "improve_knowledge_quality" => NeedIds(needs, NeedCategory.knowledge_gap, NeedCategory.quality_risk, NeedCategory.validation_gap),
            "reduce_low_confidence_knowledge" => NeedIds(needs, NeedCategory.quality_risk, NeedCategory.knowledge_gap, NeedCategory.maintenance),
            _ => []
        };

    private static IReadOnlyList<string> NeedIds(IReadOnlyList<DetectedNeed> needs, params NeedCategory[] categories) =>
        needs
            .Where(need => categories.Contains(need.Category))
            .Select(need => need.NeedId)
            .Take(8)
            .ToList();

    private static IReadOnlyList<string> NextActionsFor(string goalId, IReadOnlyList<string> blockers, IReadOnlyList<DetectedNeed> needs)
    {
        if (blockers.Count == 0)
        {
            return ["monitor_current_state"];
        }

        return needs
            .Where(need => blockers.Contains(need.NeedId, StringComparer.OrdinalIgnoreCase))
            .SelectMany(need => need.SuggestedTaskTypes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }
}

public sealed class GoalProgressTracker
{
    private readonly StoragePaths _storagePaths;
    private readonly GoalManager _goalManager;

    public GoalProgressTracker(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _goalManager = new GoalManager(storagePaths);
    }

    public string GoalStatePath => _goalManager.GoalStatePath;

    public string GoalProgressPath => _goalManager.GoalProgressPath;

    public GoalState Update()
    {
        var needs = new NeedDetectionEngine(_storagePaths).LoadNeeds();
        if (needs.Count == 0)
        {
            needs = new NeedDetectionEngine(_storagePaths).Detect();
        }

        return _goalManager.EvaluateGoalState(needs);
    }

    public GoalState LoadOrCreateState()
    {
        var loaded = LoadState();
        return loaded is null || loaded.Goals.Count < GoalManager.DefaultGoalCount ? Update() : loaded;
    }

    public GoalState? LoadState()
    {
        if (!File.Exists(GoalStatePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoalState>(
                File.ReadAllText(GoalStatePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public GoalProgressReport? LoadProgress()
    {
        if (!File.Exists(GoalProgressPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoalProgressReport>(
                File.ReadAllText(GoalProgressPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}

public sealed class GoalOutcomeEvaluator
{
    private readonly StoragePaths _storagePaths;

    public GoalOutcomeEvaluator(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string GoalOutcomesPath => new GoalManager(_storagePaths).GoalOutcomesPath;

    public IReadOnlyList<GoalOutcomeEvaluation> Evaluate(IReadOnlyList<TaskOutcomeResult> taskOutcomes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GoalOutcomesPath)!);
        if (!File.Exists(GoalOutcomesPath))
        {
            File.WriteAllText(GoalOutcomesPath, string.Empty);
        }

        var existingTaskIds = LoadRecent(10000)
            .Select(item => item.TaskId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var evaluations = taskOutcomes
            .Where(outcome => !string.IsNullOrWhiteSpace(outcome.GoalId))
            .Where(outcome => !existingTaskIds.Contains(outcome.TaskId))
            .Select(ToGoalOutcome)
            .ToList();

        foreach (var evaluation in evaluations)
        {
            File.AppendAllText(GoalOutcomesPath, JsonSerializer.Serialize(evaluation, JsonDefaults.WriteOptions) + Environment.NewLine);
        }

        return evaluations;
    }

    public IReadOnlyList<GoalOutcomeEvaluation> LoadRecent(int limit)
    {
        if (!File.Exists(GoalOutcomesPath))
        {
            return [];
        }

        var items = new List<GoalOutcomeEvaluation>();
        foreach (var line in File.ReadLines(GoalOutcomesPath).Reverse())
        {
            if (items.Count >= Math.Clamp(limit, 1, 10000))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var item = JsonSerializer.Deserialize<GoalOutcomeEvaluation>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // Append-only feedback should remain resilient to older malformed rows.
            }
        }

        return items;
    }

    private static GoalOutcomeEvaluation ToGoalOutcome(TaskOutcomeResult outcome)
    {
        var delta = Math.Round((outcome.OutcomeScore.UsefulnessScore - 0.5) * 0.2
            + (outcome.Evidence.NeedReduced ? 0.05 : 0)
            - (outcome.Evidence.TaskRedundant ? 0.04 : 0)
            - (outcome.Evidence.TaskFailed ? 0.08 : 0), 4);
        return new GoalOutcomeEvaluation(
            OutcomeId: $"goal_outcome_{outcome.TaskId}_{outcome.EvaluatedAtUtc:yyyyMMddHHmmssfff}",
            GoalId: outcome.GoalId,
            TaskId: outcome.TaskId,
            NeedId: outcome.NeedId,
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            GoalDelta: Math.Clamp(delta, -0.2, 0.2),
            Recommendation: outcome.Recommendation,
            EvidenceRefs: outcome.Evidence.EvidenceRefs,
            Notes: outcome.Evidence.Notes
                .Concat([$"task_usefulness:{outcome.OutcomeScore.UsefulnessScore:0.####}", $"learning_value:{outcome.OutcomeScore.LearningValue:0.####}"])
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }
}

public sealed class GoalPriorityAdjuster
{
    public IReadOnlyList<HermesGoal> AdjustPriorities(IReadOnlyList<HermesGoal> goals, GoalFeedback? feedback)
    {
        return goals
            .Select(goal =>
            {
                var goalFeedback = feedback?.Goals.FirstOrDefault(item =>
                    item.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase));
                if (goalFeedback is null)
                {
                    return goal;
                }

                var adjustment = goalFeedback.AverageUsefulnessScore switch
                {
                    >= 0.72 => -3,
                    < 0.35 => 5,
                    _ => 0
                };
                var blockerAdjustment = goal.BlockerCount >= 3 ? -2 : 0;
                return goal with
                {
                    Priority = Math.Clamp(goal.Priority + adjustment + blockerAdjustment, 1, 100)
                };
            })
            .ToList();
    }
}

public sealed class AutonomousTaskPlanner
{
    public static readonly ISet<string> AllowedTaskTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "scan_knowledge_sources",
        "process_research_queue",
        "generate_hypotheses",
        "run_walkforward_validation",
        "run_strategy_research",
        "run_realism_report",
        "run_overfit_report",
        "run_storage_hygiene",
        "download_missing_market_data",
        "generate_cognitive_insights",
        "scan_software_domain",
        "scan_documentation_domain",
        "scan_process_domain",
        "scan_research_domain",
        "generate_domain_insights",
        "evaluate_knowledge_quality",
        "consolidate_memory",
        "generate_validation_plans",
        "validate_knowledge_items"
    };

    public PlanningDecision Plan(IReadOnlyList<DetectedNeed> needs, IReadOnlyList<HermesGoal> goals, int maxItems)
    {
        return Plan(needs, goals, maxItems, plannerFeedback: null);
    }

    public PlanningDecision Plan(
        IReadOnlyList<DetectedNeed> needs,
        IReadOnlyList<HermesGoal> goals,
        int maxItems,
        PlannerFeedback? plannerFeedback)
    {
        maxItems = Math.Clamp(maxItems, 1, 100);
        var now = DateTimeOffset.UtcNow;
        var planned = new List<PlannedTask>();

        foreach (var need in needs)
        {
            var matchingGoals = GoalsForNeed(need, goals);
            foreach (var taskType in TaskTypesFor(need).Where(AllowedTaskTypes.Contains))
            {
                var goal = matchingGoals.FirstOrDefault() ?? goals.OrderBy(goal => goal.Priority).First();
                var priority = ApplyFeedback(Score(need, goal, taskType), taskType, need.NeedId, plannerFeedback);
                var feedbackNote = FeedbackNoteFor(taskType, need.NeedId, plannerFeedback);
                planned.Add(new PlannedTask(
                    TaskId: StableTaskId(need.NeedId, goal.GoalId, taskType),
                    TaskType: taskType,
                    Domain: need.Domain,
                    GoalId: goal.GoalId,
                    NeedId: need.NeedId,
                    QueueType: QueueTypeFor(taskType, need),
                    Priority: priority,
                    Reason: $"Need '{need.Title}' ({need.Category}, {need.Severity}) unterstuetzt Ziel '{goal.GoalId}'.{feedbackNote}",
                    ExpectedOutcome: ExpectedOutcomeFor(taskType),
                    SourceRefs: need.EvidenceRefs,
                    CreatedAtUtc: now,
                    Status: "planned",
                    SupportingGoalId: goal.GoalId,
                    GoalReason: GoalReasonFor(goal, need, taskType),
                    ExpectedGoalDelta: ExpectedGoalDeltaFor(priority, need, taskType),
                    NoTradingExecution: true,
                    HumanReviewRequired: true));
            }
        }

        var tasks = planned
            .GroupBy(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(task => task.Priority.TotalScore).First())
            .OrderByDescending(task => task.Priority.TotalScore)
            .ThenBy(task => task.TaskType, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        return new PlanningDecision(
            DecisionId: $"planning_decision_{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: now,
            Needs: needs,
            Goals: goals,
            PlannedTasks: tasks,
            Explanations: tasks.Select(task => $"{task.TaskId}: {task.Reason} priority={task.Priority.TotalScore:0.####}").ToList(),
            NoTradingExecution: true,
            HumanReviewRequired: true);
    }

    private static IReadOnlyList<HermesGoal> GoalsForNeed(DetectedNeed need, IReadOnlyList<HermesGoal> goals) =>
        goals
            .Where(goal => goal.Blockers.Contains(need.NeedId, StringComparer.OrdinalIgnoreCase))
            .OrderBy(goal => goal.Priority)
            .ToList();

    private static IReadOnlyList<string> TaskTypesFor(DetectedNeed need)
    {
        if (need.SuggestedTaskTypes.Count > 0)
        {
            return need.SuggestedTaskTypes;
        }

        return need.Category switch
        {
            NeedCategory.knowledge_gap => ["scan_knowledge_sources", "generate_hypotheses"],
            NeedCategory.validation_gap => ["process_research_queue", "run_walkforward_validation"],
            NeedCategory.data_gap => ["download_missing_market_data", "run_walkforward_validation"],
            NeedCategory.quality_risk => ["run_overfit_report", "run_realism_report"],
            NeedCategory.resource_risk or NeedCategory.maintenance => ["run_storage_hygiene"],
            NeedCategory.domain_gap => ["scan_knowledge_sources", "generate_domain_insights"],
            _ => ["generate_cognitive_insights"]
        };
    }

    private static PriorityScore Score(DetectedNeed need, HermesGoal goal, string taskType)
    {
        var severity = NeedDetectionEngine.SeverityRank(need.Severity) / 4.0;
        var impact = Math.Clamp(severity + (goal.Priority <= 20 ? 0.18 : 0.06), 0, 1);
        var urgency = Math.Clamp(severity + (need.Category is NeedCategory.resource_risk ? 0.2 : 0), 0, 1);
        var confidence = taskType is "download_missing_market_data" ? 0.55 : 0.72;
        var cost = taskType is "run_strategy_research" or "run_walkforward_validation" ? 0.55 : 0.25;
        var risk = taskType is "download_missing_market_data" ? 0.35 : 0.12;
        var learning = need.Category is NeedCategory.quality_risk or NeedCategory.validation_gap ? 0.82 : 0.58;
        var goalPriority = Math.Clamp(1 - ((goal.Priority - 1) / 100.0), 0.05, 1);
        const double redundancyPenalty = 0;
        var total = impact * 0.22
            + urgency * 0.18
            + confidence * 0.14
            + learning * 0.2
            + goalPriority * 0.16
            - cost * 0.06
            - risk * 0.03
            - redundancyPenalty * 0.05;
        return new PriorityScore(
            Math.Round(impact, 4),
            Math.Round(urgency, 4),
            Math.Round(confidence, 4),
            Math.Round(cost, 4),
            Math.Round(risk, 4),
            Math.Round(learning, 4),
            Math.Round(goalPriority, 4),
            Math.Round(redundancyPenalty, 4),
            Math.Round(Math.Clamp(total, 0, 1), 4));
    }

    private static PriorityScore ApplyFeedback(
        PriorityScore score,
        string taskType,
        string needId,
        PlannerFeedback? feedback)
    {
        var taskFeedback = feedback?.TaskTypeFeedback.FirstOrDefault(item =>
            item.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase));
        if (taskFeedback is null)
        {
            return score;
        }

        var repeatedNeed = taskFeedback.RepeatedUnsuccessfulNeeds.Contains(needId, StringComparer.OrdinalIgnoreCase);
        var repeatedNeedPenalty = repeatedNeed ? -0.05 : 0;
        var adjustment = taskFeedback.PriorityAdjustment + repeatedNeedPenalty;
        var learning = Math.Clamp(score.ExpectedLearningValue + Math.Max(0, adjustment) * 0.5, 0, 1);
        var risk = Math.Clamp(score.Risk + Math.Max(0, -adjustment) * 0.4, 0, 1);
        var redundancyPenalty = Math.Clamp(score.RedundancyPenalty + (repeatedNeed ? 0.12 : 0), 0, 1);
        return score with
        {
            ExpectedLearningValue = Math.Round(learning, 4),
            Risk = Math.Round(risk, 4),
            RedundancyPenalty = Math.Round(redundancyPenalty, 4),
            TotalScore = Math.Round(Math.Clamp(score.TotalScore + adjustment - redundancyPenalty * 0.05, 0, 1), 4)
        };
    }

    private static string FeedbackNoteFor(string taskType, string needId, PlannerFeedback? feedback)
    {
        var taskFeedback = feedback?.TaskTypeFeedback.FirstOrDefault(item =>
            item.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase));
        if (taskFeedback is null)
        {
            return string.Empty;
        }

        var repeatedNeed = taskFeedback.RepeatedUnsuccessfulNeeds.Contains(needId, StringComparer.OrdinalIgnoreCase)
            ? "; repeated_unsuccessful_need"
            : string.Empty;
        return $" Planner feedback: {taskFeedback.Recommendation}, priority_adjustment={taskFeedback.PriorityAdjustment:0.####}{repeatedNeed}.";
    }

    private static string QueueTypeFor(string taskType, DetectedNeed need) =>
        taskType switch
        {
            "scan_knowledge_sources" => "discovery",
            "scan_software_domain" => "discovery",
            "scan_documentation_domain" => "discovery",
            "scan_process_domain" => "discovery",
            "scan_research_domain" => "discovery",
            "download_missing_market_data" => "discovery",
            "evaluate_knowledge_quality" => "review",
            "consolidate_memory" => "review",
            "generate_validation_plans" => "review",
            "validate_knowledge_items" => "validation",
            "process_research_queue" => "validation",
            "run_walkforward_validation" => "validation",
            "run_strategy_research" => "simulation",
            "run_realism_report" => "review",
            "run_overfit_report" => "review",
            "run_storage_hygiene" => "review",
            "generate_domain_insights" => "review",
            _ => need.Category == NeedCategory.domain_gap ? "discovery" : "validation"
        };

    private static string ExpectedOutcomeFor(string taskType) =>
        taskType switch
        {
            "scan_knowledge_sources" => "knowledge_sources_refreshed",
            "process_research_queue" => "queued_research_items_processed",
            "generate_hypotheses" => "new_or_refreshed_cognitive_hypotheses",
            "run_walkforward_validation" => "oos_and_walkforward_quality_updated",
            "run_strategy_research" => "strategy_variant_evidence_updated",
            "run_realism_report" => "realism_and_cost_quality_updated",
            "run_overfit_report" => "overfit_risk_review_updated",
            "run_storage_hygiene" => "storage_health_reviewed",
            "download_missing_market_data" => "historical_data_gap_reduced",
            "generate_cognitive_insights" => "cognitive_insights_updated",
            "scan_software_domain" => "software_domain_knowledge_updated",
            "scan_documentation_domain" => "documentation_domain_gaps_updated",
            "scan_process_domain" => "process_domain_workflows_updated",
            "scan_research_domain" => "research_domain_sources_updated",
            "generate_domain_insights" => "multi_domain_insights_updated",
            "evaluate_knowledge_quality" => "knowledge_quality_scores_updated",
            "consolidate_memory" => "memory_consolidation_updated_no_delete",
            "generate_validation_plans" => "knowledge_validation_plans_created",
            "validate_knowledge_items" => "knowledge_validation_tasks_queued",
            _ => "structured_research_progress"
        };

    private static string StableTaskId(string needId, string goalId, string taskType)
    {
        var input = $"{needId}|{goalId}|{taskType}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..12];
        return $"planned_{taskType}_{hash}";
    }

    private static string GoalReasonFor(HermesGoal goal, DetectedNeed need, string taskType) =>
        $"Goal '{goal.GoalId}' ({goal.Title}) is active; need '{need.NeedId}' maps to {need.Category}; task '{taskType}' is expected to reduce blockers or improve progress.";

    private static double ExpectedGoalDeltaFor(PriorityScore priority, DetectedNeed need, string taskType)
    {
        var severityBoost = NeedDetectionEngine.SeverityRank(need.Severity) * 0.01;
        var heavyCostPenalty = taskType is "run_strategy_research" or "run_walkforward_validation" ? 0.015 : 0;
        return Math.Round(Math.Clamp((priority.TotalScore * 0.12) + severityBoost - heavyCostPenalty, 0.01, 0.2), 4);
    }
}

public sealed class AutonomousPlanningCycleService
{
    private readonly StoragePaths _storagePaths;
    private readonly NeedDetectionEngine _needs;
    private readonly GoalManager _goals;
    private readonly AutonomousTaskPlanner _planner = new();

    public AutonomousPlanningCycleService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _needs = new NeedDetectionEngine(storagePaths);
        _goals = new GoalManager(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string PlanningStatusPath => Path.Combine(Root, "planning_status.json");

    public string PlannedTasksPath => Path.Combine(Root, "planned_tasks.json");

    public string GoalProgressPath => _goals.GoalProgressPath;

    public string DetectedNeedsPath => _needs.NeedsPath;

    public IReadOnlyList<DetectedNeed> DetectNeeds() => _needs.Detect();

    public PlanningDecision PlanNextTasks(int maxItems)
    {
        var needs = _needs.Detect();
        var goals = _goals.EvaluateGoals(needs);
        var feedback = new TaskOutcomeEvaluator(_storagePaths).LoadPlannerFeedback();
        var decision = _planner.Plan(needs, goals, maxItems, feedback);
        WriteDecision(decision);
        WriteStatus(decision, queuedResearchItems: new ResearchQueueService(_storagePaths).LoadOrCreateQueue().Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)));
        return decision;
    }

    public PlanningDecision RunPlanningCycle(int maxItems)
    {
        var decision = PlanNextTasks(maxItems);
        var queue = new ResearchQueueService(_storagePaths).EnqueuePlannedTasks(decision.PlannedTasks);
        foreach (var domain in CognitiveCoreService.Domains().Where(domain => domain.Active).Select(domain => domain.DomainId))
        {
            new HypothesisGenerator(_storagePaths).Generate(domain);
        }

        new DomainCognitiveService(_storagePaths).BuildInsights();
        new CognitiveCoreService(_storagePaths).BuildStatus();
        WriteStatus(decision, queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)));
        return decision;
    }

    public PlanningDecision? LoadLatestDecision()
    {
        if (!File.Exists(PlannedTasksPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlanningDecision>(
                File.ReadAllText(PlannedTasksPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public PlanningDecision? UpdateTaskStatuses(IReadOnlyDictionary<string, string> statuses)
    {
        var decision = LoadLatestDecision();
        if (decision is null)
        {
            return null;
        }

        var updated = decision with
        {
            PlannedTasks = decision.PlannedTasks
                .Select(task => statuses.TryGetValue(task.TaskId, out var status)
                    ? task with { Status = status }
                    : task)
                .ToList()
        };
        WriteDecision(updated);
        WriteStatus(
            updated,
            queuedResearchItems: new ResearchQueueService(_storagePaths)
                .LoadOrCreateQueue()
                .Items
                .Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)));
        return updated;
    }

    public AutonomousPlanningStatus BuildStatus()
    {
        var decision = LoadLatestDecision();
        if (decision is null)
        {
            decision = PlanNextTasks(20);
        }

        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        return WriteStatus(decision, queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)));
    }

    private void WriteDecision(PlanningDecision decision)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(PlannedTasksPath, JsonSerializer.Serialize(decision, JsonDefaults.WriteOptions));
    }

    private AutonomousPlanningStatus WriteStatus(PlanningDecision decision, int queuedResearchItems)
    {
        Directory.CreateDirectory(Root);
        var status = new AutonomousPlanningStatus(
            StatusVersion: "autonomous_planning_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            NeedsDetected: decision.Needs.Count,
            ActiveGoals: decision.Goals.Count(goal => goal.Active),
            PlannedTasks: decision.PlannedTasks.Count,
            QueuedResearchItems: queuedResearchItems,
            ActiveDomains: CognitiveCoreService.Domains()
                .Where(domain => domain.Active)
                .Select(domain => domain.DomainId)
                .ToList(),
            LastDecisionId: decision.DecisionId,
            NextAction: decision.PlannedTasks.Count == 0 ? "monitor_current_state" : "process_research_queue",
            TopNeeds: decision.Needs.Take(8).Select(need => $"{need.Severity}:{need.Category}:{need.NeedId}").ToList(),
            TopTasks: decision.PlannedTasks.Take(8).Select(task => $"{task.TaskType}:{task.Priority.TotalScore:0.####}:{task.TaskId}").ToList(),
            Warnings: decision.Needs.Where(need => need.Severity is NeedSeverity.high or NeedSeverity.critical).Select(need => need.NeedId).Take(10).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(PlanningStatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }
}
