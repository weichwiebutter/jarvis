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

        var activeSourceDomains = sources
            .Select(source => source.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in CognitiveCoreService.Domains().Where(domain => !domain.Active))
        {
            if (!activeSourceDomains.Contains(domain.DomainId))
            {
                needs.Add(Need(
                    $"domain_{domain.DomainId}_without_sources",
                    NeedCategory.domain_gap,
                    NeedSeverity.low,
                    domain.DomainId,
                    $"Domäne {domain.DomainId} ohne Quellen",
                    $"Die geplante Domäne '{domain.Name}' hat noch keine Knowledge Sources.",
                    [sourceRegistry.SourcesPath],
                    ["scan_knowledge_sources"]));
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
}

public sealed class GoalManager
{
    private readonly StoragePaths _storagePaths;

    public GoalManager(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string GoalProgressPath => Path.Combine(Root, "goal_progress.json");

    public IReadOnlyList<HermesGoal> EvaluateGoals(IReadOnlyList<DetectedNeed> needs)
    {
        Directory.CreateDirectory(Root);
        var goalFeedback = new TaskOutcomeEvaluator(_storagePaths).LoadGoalFeedback();
        var goals = Defaults()
            .Select(goal =>
            {
                var blockers = BlockersFor(goal.GoalId, needs);
                var feedback = goalFeedback?.Goals.FirstOrDefault(item =>
                    item.GoalId.Equals(goal.GoalId, StringComparison.OrdinalIgnoreCase));
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
                return goal with
                {
                    ProgressScore = progress,
                    Blockers = blockers,
                    NextActions = nextActions
                };
            })
            .OrderBy(goal => goal.Priority)
            .ToList();

        var progressItems = goals
            .Select(goal => new GoalProgress(
                GoalId: goal.GoalId,
                ProgressScore: goal.ProgressScore,
                Blockers: goal.Blockers,
                NextActions: goal.NextActions,
                UpdatedAtUtc: DateTimeOffset.UtcNow))
            .ToList();
        File.WriteAllText(GoalProgressPath, JsonSerializer.Serialize(progressItems, JsonDefaults.WriteOptions));
        return goals;
    }

    private static IReadOnlyList<HermesGoal> Defaults() =>
    [
        Goal("improve_trading_robustness", "Trading-Domain robuster und OOS-stabiler bewerten.", 10),
        Goal("reduce_overfit_risk", "Overfit-Risiko durch Validierung und Realism-Gates reduzieren.", 20),
        Goal("expand_knowledge_sources", "Kuratierte Quellen aktuell halten und Knowledge Gaps schließen.", 30),
        Goal("improve_cognitive_memory_quality", "Research Queue, Hypothesen und Insights in verwertbare Erinnerung überführen.", 40),
        Goal("maintain_storage_health", "Storage/Resource-Zustand für Dauerbetrieb stabil halten.", 50),
        Goal("prepare_multi_domain_learning", "Nicht-Trading-Domänen strukturiert vorbereiten, ohne Trading zum Kern zu machen.", 60)
    ];

    private static HermesGoal Goal(string id, string description, int priority) =>
        new(id, description, priority, Active: true, ProgressScore: 0.5, Blockers: [], NextActions: []);

    private static IReadOnlyList<string> BlockersFor(string goalId, IReadOnlyList<DetectedNeed> needs) =>
        goalId switch
        {
            "improve_trading_robustness" => NeedIds(needs, NeedCategory.quality_risk, NeedCategory.data_gap, NeedCategory.validation_gap),
            "reduce_overfit_risk" => NeedIds(needs, NeedCategory.quality_risk, NeedCategory.validation_gap),
            "expand_knowledge_sources" => NeedIds(needs, NeedCategory.knowledge_gap, NeedCategory.domain_gap),
            "improve_cognitive_memory_quality" => NeedIds(needs, NeedCategory.validation_gap, NeedCategory.knowledge_gap),
            "maintain_storage_health" => NeedIds(needs, NeedCategory.resource_risk, NeedCategory.maintenance),
            "prepare_multi_domain_learning" => NeedIds(needs, NeedCategory.domain_gap, NeedCategory.knowledge_gap),
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
        "generate_cognitive_insights"
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
            NeedCategory.domain_gap => ["scan_knowledge_sources"],
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
        var total = impact * 0.28
            + urgency * 0.22
            + confidence * 0.16
            + learning * 0.22
            - cost * 0.08
            - risk * 0.04;
        return new PriorityScore(
            Math.Round(impact, 4),
            Math.Round(urgency, 4),
            Math.Round(confidence, 4),
            Math.Round(cost, 4),
            Math.Round(risk, 4),
            Math.Round(learning, 4),
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

        var repeatedNeedPenalty = taskFeedback.RepeatedUnsuccessfulNeeds.Contains(needId, StringComparer.OrdinalIgnoreCase)
            ? -0.05
            : 0;
        var adjustment = taskFeedback.PriorityAdjustment + repeatedNeedPenalty;
        var learning = Math.Clamp(score.ExpectedLearningValue + Math.Max(0, adjustment) * 0.5, 0, 1);
        var risk = Math.Clamp(score.Risk + Math.Max(0, -adjustment) * 0.4, 0, 1);
        return score with
        {
            ExpectedLearningValue = Math.Round(learning, 4),
            Risk = Math.Round(risk, 4),
            TotalScore = Math.Round(Math.Clamp(score.TotalScore + adjustment, 0, 1), 4)
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
            "download_missing_market_data" => "discovery",
            "process_research_queue" => "validation",
            "run_walkforward_validation" => "validation",
            "run_strategy_research" => "simulation",
            "run_realism_report" => "review",
            "run_overfit_report" => "review",
            "run_storage_hygiene" => "review",
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
            _ => "structured_research_progress"
        };

    private static string StableTaskId(string needId, string goalId, string taskType)
    {
        var input = $"{needId}|{goalId}|{taskType}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..12];
        return $"planned_{taskType}_{hash}";
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
        new HypothesisGenerator(_storagePaths).Generate("trading");
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
            ActiveDomains: ["trading"],
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
