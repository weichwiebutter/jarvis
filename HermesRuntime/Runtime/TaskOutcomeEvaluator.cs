using System.Text.Json;

namespace Hermes.Runtime;

public sealed class TaskOutcomeEvaluator
{
    private readonly StoragePaths _storagePaths;

    public TaskOutcomeEvaluator(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string TaskOutcomesPath => Path.Combine(Root, "task_outcomes.jsonl");

    public string PlannerFeedbackPath => Path.Combine(Root, "planner_feedback.json");

    public string GoalFeedbackPath => Path.Combine(Root, "goal_feedback.json");

    public string StatusPath => Path.Combine(Root, "outcome_feedback_status.json");

    public IReadOnlyList<TaskOutcomeResult> Evaluate(int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 500);
        Directory.CreateDirectory(Root);
        var executor = new PlannedTaskExecutor(_storagePaths);
        var existing = LoadOutcomes(5000);
        var evaluatedTaskIds = existing
            .Select(outcome => outcome.TaskId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executions = executor.LoadRecentResults(Math.Max(maxItems * 5, 100))
            .Where(result => result.Status is "completed" or "skipped" or "failed")
            .Where(result => !evaluatedTaskIds.Contains(result.TaskId))
            .OrderBy(result => result.CompletedAtUtc ?? result.StartedAtUtc)
            .Take(maxItems)
            .ToList();

        var needs = new NeedDetectionEngine(_storagePaths).LoadNeeds();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var insights = new HypothesisGenerator(_storagePaths).LoadInsights();
        var outcomes = executions
            .Select(result => EvaluateOne(result, needs, queue, insights))
            .ToList();

        foreach (var outcome in outcomes)
        {
            File.AppendAllText(TaskOutcomesPath, JsonSerializer.Serialize(outcome, JsonDefaults.WriteOptions) + Environment.NewLine);
        }

        var allOutcomes = existing
            .Concat(outcomes)
            .GroupBy(outcome => outcome.TaskId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(outcome => outcome.EvaluatedAtUtc).First())
            .OrderByDescending(outcome => outcome.EvaluatedAtUtc)
            .ToList();
        WritePlannerFeedback(allOutcomes);
        WriteGoalFeedback(allOutcomes);
        new GoalOutcomeEvaluator(_storagePaths).Evaluate(outcomes);
        new GoalProgressTracker(_storagePaths).Update();
        WriteStatus(outcomes, allOutcomes);
        new CognitiveCoreService(_storagePaths).BuildStatus();
        return outcomes;
    }

    public IReadOnlyList<TaskOutcomeResult> LoadOutcomes(int limit = 1000)
    {
        if (!File.Exists(TaskOutcomesPath))
        {
            return [];
        }

        var outcomes = new List<TaskOutcomeResult>();
        foreach (var line in File.ReadLines(TaskOutcomesPath).Reverse())
        {
            if (outcomes.Count >= Math.Clamp(limit, 1, 10000))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var outcome = JsonSerializer.Deserialize<TaskOutcomeResult>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (outcome is not null)
                {
                    outcomes.Add(outcome);
                }
            }
            catch (JsonException)
            {
                // Keep feedback append-only; bad historical lines should not block future evaluation.
            }
        }

        return outcomes;
    }

    public PlannerFeedback LoadOrCreatePlannerFeedback()
    {
        var existing = LoadPlannerFeedback();
        if (existing is not null)
        {
            return existing;
        }

        return WritePlannerFeedback(LoadOutcomes(5000));
    }

    public PlannerFeedback? LoadPlannerFeedback()
    {
        if (!File.Exists(PlannerFeedbackPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlannerFeedback>(
                File.ReadAllText(PlannerFeedbackPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public GoalFeedback LoadOrCreateGoalFeedback()
    {
        var existing = LoadGoalFeedback();
        if (existing is not null)
        {
            return existing;
        }

        return WriteGoalFeedback(LoadOutcomes(5000));
    }

    public GoalFeedback? LoadGoalFeedback()
    {
        if (!File.Exists(GoalFeedbackPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoalFeedback>(
                File.ReadAllText(GoalFeedbackPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public OutcomeFeedbackStatus? LoadStatus()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OutcomeFeedbackStatus>(
                File.ReadAllText(StatusPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public OutcomeFeedbackStatus BuildStatus()
    {
        var outcomes = LoadOutcomes(5000);
        var existing = LoadStatus();
        return WriteStatus([], outcomes, existing?.OutcomesEvaluatedLastRun);
    }

    private TaskOutcomeResult EvaluateOne(
        PlannedTaskExecutionResult result,
        IReadOnlyList<DetectedNeed> currentNeeds,
        ResearchQueue queue,
        IReadOnlyList<CognitiveInsight> insights)
    {
        var taskFailed = result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
        var taskSkipped = result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase);
        var taskRedundant = taskSkipped
            && ((result.SkippedReason?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ?? false)
                || result.Warnings.Any(warning => warning.Contains("duplicate", StringComparison.OrdinalIgnoreCase)));
        var needReduced = !currentNeeds.Any(need => need.NeedId.Equals(result.NeedId, StringComparison.OrdinalIgnoreCase));
        var outputEvidence = result.OutputPaths.Count > 0
            && result.OutputPaths.Any(path => File.Exists(path) || Directory.Exists(path));
        var insightEvidence = result.TaskType.Equals("generate_hypotheses", StringComparison.OrdinalIgnoreCase)
            || result.TaskType.Equals("generate_cognitive_insights", StringComparison.OrdinalIgnoreCase)
            || result.TaskType.Equals("generate_domain_insights", StringComparison.OrdinalIgnoreCase)
            || result.OutputPaths.Any(path => path.Contains("cognitive_insights", StringComparison.OrdinalIgnoreCase))
            || result.OutputPaths.Any(path => path.Contains("domain_insights", StringComparison.OrdinalIgnoreCase))
            || insights.Count > 0;
        var queueChanged = queue.Items.Any(item =>
            item.Notes.Any(note => note.Contains(result.TaskId, StringComparison.OrdinalIgnoreCase))
            && (item.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || item.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
                || item.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)));
        var evidence = new TaskOutcomeEvidence(
            NeedReduced: needReduced,
            GoalImproved: result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) && (needReduced || outputEvidence),
            NewInsightsGenerated: insightEvidence && result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase),
            ResearchQueueChanged: queueChanged,
            OutputEvidenceAvailable: outputEvidence,
            WarningCount: result.Warnings.Count,
            TaskFailed: taskFailed,
            TaskSkipped: taskSkipped,
            TaskRedundant: taskRedundant,
            EvidenceRefs: result.OutputPaths
                .Concat([new ResearchQueueService(_storagePaths).QueuePath, new NeedDetectionEngine(_storagePaths).NeedsPath])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Notes: BuildEvidenceNotes(result, needReduced, outputEvidence, queueChanged, taskRedundant));
        var score = Score(result, evidence);
        var followups = FollowupTasks(result, score, evidence);
        return new TaskOutcomeResult(
            OutcomeId: $"task_outcome_{result.TaskId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
            TaskId: result.TaskId,
            TaskType: result.TaskType,
            NeedId: result.NeedId,
            GoalId: result.GoalId,
            ExecutedAtUtc: result.CompletedAtUtc ?? result.StartedAtUtc,
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            OutcomeScore: score,
            Evidence: evidence,
            Recommendation: score.Recommendation,
            FollowupTaskIds: followups,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static TaskOutcomeScore Score(PlannedTaskExecutionResult result, TaskOutcomeEvidence evidence)
    {
        var completed = result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);
        var skipped = result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase);
        var failed = result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
        var cost = CostFor(result.TaskType);
        var redundancy = evidence.TaskRedundant ? 0.9 : skipped ? 0.55 : result.Warnings.Any(warning => warning.Contains("no_", StringComparison.OrdinalIgnoreCase)) ? 0.35 : 0.12;
        var risk = failed ? 0.8 : result.Warnings.Count > 0 ? Math.Min(0.65, 0.18 + result.Warnings.Count * 0.08) : 0.08;
        var learning = LearningFor(result.TaskType);
        if (!completed)
        {
            learning *= skipped && evidence.TaskRedundant ? 0.2 : 0.45;
        }

        var usefulness = (completed ? 0.5 : failed ? 0.05 : 0.16)
            + (evidence.NeedReduced ? 0.18 : 0)
            + (evidence.OutputEvidenceAvailable ? 0.14 : 0)
            + (evidence.NewInsightsGenerated ? 0.1 : 0)
            + (evidence.ResearchQueueChanged ? 0.06 : 0)
            - (redundancy * 0.22)
            - (risk * 0.16)
            - (cost * 0.08);
        usefulness = Math.Clamp(usefulness, 0, 1);
        var recommendation = RecommendationFor(usefulness, learning, cost, risk, redundancy, result);
        return new TaskOutcomeScore(
            UsefulnessScore: Math.Round(usefulness, 4),
            LearningValue: Math.Round(Math.Clamp(learning, 0, 1), 4),
            CostScore: Math.Round(cost, 4),
            RiskScore: Math.Round(risk, 4),
            RedundancyScore: Math.Round(redundancy, 4),
            Recommendation: recommendation);
    }

    private PlannerFeedback WritePlannerFeedback(IReadOnlyList<TaskOutcomeResult> outcomes)
    {
        Directory.CreateDirectory(Root);
        var feedback = outcomes
            .GroupBy(outcome => outcome.TaskType, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(outcome => outcome.EvaluatedAtUtc).Take(50).ToList();
                var avgUseful = Average(ordered, outcome => outcome.OutcomeScore.UsefulnessScore);
                var avgLearning = Average(ordered, outcome => outcome.OutcomeScore.LearningValue);
                var avgCost = Average(ordered, outcome => outcome.OutcomeScore.CostScore);
                var avgRisk = Average(ordered, outcome => outcome.OutcomeScore.RiskScore);
                var avgRedundancy = Average(ordered, outcome => outcome.OutcomeScore.RedundancyScore);
                var adjustment = PriorityAdjustment(avgUseful, avgLearning, avgRisk, avgRedundancy);
                var repeatedUnsuccessful = ordered
                    .Where(outcome => outcome.OutcomeScore.UsefulnessScore < 0.35
                        || outcome.OutcomeScore.Recommendation is "reduce_priority" or "retire_task_type" or "needs_more_data")
                    .GroupBy(outcome => outcome.NeedId, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() >= 2 || ordered.Count == 1)
                    .Select(group => group.Key)
                    .Take(10)
                    .ToList();
                return new PlannerTaskTypeFeedback(
                    TaskType: group.Key,
                    Evaluations: ordered.Count,
                    AverageUsefulnessScore: avgUseful,
                    AverageLearningValue: avgLearning,
                    AverageCostScore: avgCost,
                    AverageRiskScore: avgRisk,
                    AverageRedundancyScore: avgRedundancy,
                    PriorityAdjustment: adjustment,
                    Recommendation: AggregateRecommendation(ordered, avgUseful, avgLearning, avgRisk, avgRedundancy),
                    RepeatedUnsuccessfulNeeds: repeatedUnsuccessful,
                    LastEvaluatedUtc: ordered.Max(outcome => outcome.EvaluatedAtUtc));
            })
            .OrderBy(item => item.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var report = new PlannerFeedback(
            FeedbackVersion: "planner_feedback_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            OutcomesEvaluated: outcomes.Count,
            TaskTypeFeedback: feedback,
            RetiredTaskTypes: feedback
                .Where(item => item.Recommendation == "retire_task_type")
                .Select(item => item.TaskType)
                .ToList(),
            Warnings: feedback
                .Where(item => item.AverageRiskScore >= 0.65 || item.AverageRedundancyScore >= 0.75)
                .Select(item => $"{item.TaskType}:{item.Recommendation}")
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(PlannerFeedbackPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private GoalFeedback WriteGoalFeedback(IReadOnlyList<TaskOutcomeResult> outcomes)
    {
        Directory.CreateDirectory(Root);
        var goals = outcomes
            .GroupBy(outcome => outcome.GoalId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(outcome => outcome.EvaluatedAtUtc).Take(50).ToList();
                var avgUseful = Average(ordered, outcome => outcome.OutcomeScore.UsefulnessScore);
                var improved = ordered
                    .Where(outcome => outcome.Evidence.NeedReduced || outcome.Evidence.GoalImproved)
                    .Select(outcome => outcome.NeedId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList();
                var persistent = ordered
                    .Where(outcome => !outcome.Evidence.NeedReduced || outcome.OutcomeScore.UsefulnessScore < 0.35)
                    .Select(outcome => outcome.NeedId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList();
                return new GoalFeedbackEntry(
                    GoalId: group.Key,
                    Evaluations: ordered.Count,
                    AverageUsefulnessScore: avgUseful,
                    ProgressDelta: Math.Round((avgUseful - 0.5) * 0.2, 4),
                    ImprovedNeeds: improved,
                    PersistentNeeds: persistent,
                    RecommendedActions: GoalActionsFor(ordered),
                    LastEvaluatedUtc: ordered.Max(outcome => outcome.EvaluatedAtUtc));
            })
            .OrderBy(item => item.GoalId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var report = new GoalFeedback(
            FeedbackVersion: "goal_feedback_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            OutcomesEvaluated: outcomes.Count,
            Goals: goals,
            Warnings: goals
                .Where(goal => goal.AverageUsefulnessScore < 0.35)
                .Select(goal => $"{goal.GoalId}:low_outcome_usefulness")
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(GoalFeedbackPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private OutcomeFeedbackStatus WriteStatus(
        IReadOnlyList<TaskOutcomeResult> latestRun,
        IReadOnlyList<TaskOutcomeResult> allOutcomes,
        int? outcomesEvaluatedLastRun = null)
    {
        Directory.CreateDirectory(Root);
        var status = new OutcomeFeedbackStatus(
            StatusVersion: "outcome_feedback_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalOutcomes: allOutcomes.Count,
            LastOutcomeUtc: allOutcomes.OrderByDescending(outcome => outcome.EvaluatedAtUtc).FirstOrDefault()?.EvaluatedAtUtc,
            OutcomesEvaluatedLastRun: outcomesEvaluatedLastRun ?? latestRun.Count,
            TaskOutcomesPath: TaskOutcomesPath,
            PlannerFeedbackPath: PlannerFeedbackPath,
            GoalFeedbackPath: GoalFeedbackPath,
            LatestRecommendations: allOutcomes
                .OrderByDescending(outcome => outcome.EvaluatedAtUtc)
                .Take(10)
                .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}:{outcome.TaskId}")
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    private static IReadOnlyList<string> BuildEvidenceNotes(
        PlannedTaskExecutionResult result,
        bool needReduced,
        bool outputEvidence,
        bool queueChanged,
        bool redundant)
    {
        var notes = new List<string>
        {
            $"execution_status:{result.Status}",
            needReduced ? "need_reduced:true" : "need_reduced:false",
            outputEvidence ? "output_evidence:true" : "output_evidence:false",
            queueChanged ? "research_queue_changed:true" : "research_queue_changed:false"
        };
        if (redundant)
        {
            notes.Add("task_redundant:true");
        }

        if (!string.IsNullOrWhiteSpace(result.SkippedReason))
        {
            notes.Add($"skipped_reason:{result.SkippedReason}");
        }

        return notes;
    }

    private static IReadOnlyList<string> FollowupTasks(
        PlannedTaskExecutionResult result,
        TaskOutcomeScore score,
        TaskOutcomeEvidence evidence)
    {
        if (score.Recommendation == "needs_more_data")
        {
            return ["download_missing_market_data", "run_walkforward_validation"];
        }

        if (score.Recommendation == "escalate_to_review")
        {
            return ["generate_cognitive_insights"];
        }

        if (!evidence.NeedReduced && result.TaskType is "run_overfit_report" or "run_strategy_research")
        {
            return ["run_walkforward_validation", "run_realism_report"];
        }

        if (score.Recommendation == "increase_priority")
        {
            return [result.TaskType];
        }

        return [];
    }

    private static double CostFor(string taskType) =>
        taskType switch
        {
            "run_strategy_research" or "run_walkforward_validation" => 0.65,
            "run_realism_report" or "run_overfit_report" => 0.48,
            "download_missing_market_data" => 0.55,
            "run_storage_hygiene" => 0.35,
            "process_research_queue" => 0.3,
            "scan_software_domain" or "scan_documentation_domain" or "scan_process_domain" or "scan_research_domain" => 0.2,
            "generate_domain_insights" => 0.18,
            _ => 0.22
        };

    private static double LearningFor(string taskType) =>
        taskType switch
        {
            "generate_hypotheses" or "generate_cognitive_insights" or "generate_domain_insights" => 0.82,
            "run_strategy_research" or "run_walkforward_validation" => 0.78,
            "run_overfit_report" or "run_realism_report" => 0.68,
            "scan_knowledge_sources" => 0.62,
            "scan_software_domain" or "scan_documentation_domain" or "scan_process_domain" or "scan_research_domain" => 0.66,
            "process_research_queue" => 0.56,
            "run_storage_hygiene" => 0.32,
            _ => 0.42
        };

    private static string RecommendationFor(
        double usefulness,
        double learning,
        double cost,
        double risk,
        double redundancy,
        PlannedTaskExecutionResult result)
    {
        if (result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase) || risk >= 0.7)
        {
            return "escalate_to_review";
        }

        if (result.TaskType.Equals("download_missing_market_data", StringComparison.OrdinalIgnoreCase)
            && result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
        {
            return "needs_more_data";
        }

        if (redundancy >= 0.75)
        {
            return "reduce_priority";
        }

        if (usefulness < 0.2 && learning < 0.3)
        {
            return "retire_task_type";
        }

        if (usefulness < 0.38)
        {
            return "reduce_priority";
        }

        if (usefulness >= 0.72 && learning >= 0.6)
        {
            return "increase_priority";
        }

        return usefulness >= 0.55 ? "repeat" : "needs_more_data";
    }

    private static double PriorityAdjustment(double usefulness, double learning, double risk, double redundancy)
    {
        var adjustment = 0.0;
        if (usefulness >= 0.72 && learning >= 0.55)
        {
            adjustment += 0.08;
        }
        else if (usefulness < 0.35)
        {
            adjustment -= 0.1;
        }

        if (redundancy >= 0.7)
        {
            adjustment -= 0.08;
        }

        if (risk >= 0.65)
        {
            adjustment -= 0.08;
        }

        return Math.Round(Math.Clamp(adjustment, -0.25, 0.15), 4);
    }

    private static string AggregateRecommendation(
        IReadOnlyList<TaskOutcomeResult> outcomes,
        double usefulness,
        double learning,
        double risk,
        double redundancy)
    {
        if (outcomes.Any(outcome => outcome.Recommendation == "needs_more_data"))
        {
            return "needs_more_data";
        }

        if (risk >= 0.7)
        {
            return "escalate_to_review";
        }

        if (redundancy >= 0.8)
        {
            return "reduce_priority";
        }

        if (outcomes.All(outcome => outcome.OutcomeScore.UsefulnessScore < 0.2)
            && usefulness < 0.22
            && learning < 0.3)
        {
            return "retire_task_type";
        }

        if (usefulness < 0.4)
        {
            return "reduce_priority";
        }

        if (usefulness >= 0.72 && learning >= 0.55)
        {
            return "increase_priority";
        }

        return "repeat";
    }

    private static IReadOnlyList<string> GoalActionsFor(IReadOnlyList<TaskOutcomeResult> outcomes)
    {
        return outcomes
            .Select(outcome => outcome.Recommendation switch
            {
                "increase_priority" => $"prioritize:{outcome.TaskType}",
                "reduce_priority" => $"reduce:{outcome.TaskType}",
                "needs_more_data" => "collect_more_evidence",
                "escalate_to_review" => "human_review",
                "retire_task_type" => $"retire_candidate:{outcome.TaskType}",
                _ => $"repeat:{outcome.TaskType}"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static double Average(IReadOnlyList<TaskOutcomeResult> outcomes, Func<TaskOutcomeResult, double> selector) =>
        Math.Round(outcomes.Count == 0 ? 0 : outcomes.Average(selector), 4);
}
