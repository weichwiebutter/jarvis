using System.Text.Json;

namespace Hermes.Runtime;

public sealed class AutonomousLearningLoop
{
    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;

    public AutonomousLearningLoop(StoragePaths storagePaths, string configPath)
    {
        _storagePaths = storagePaths;
        _configPath = configPath;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string StatePath => Path.Combine(Root, "autonomous_loop_state.json");

    public string SummaryPath => Path.Combine(Root, "autonomous_loop_summary.json");

    public string LogPath => Path.Combine(Root, "autonomous_loop_log.jsonl");

    public string CheckpointRoot => Path.Combine(Root, "checkpoints");

    public AutonomousLoopConfig LoadConfig() => AutonomousLoopConfig.LoadOrDefault(_configPath);

    public AutonomousLoopSummary Run(int maxIterations, double maxMinutes)
    {
        var config = LoadConfig();
        maxIterations = Math.Clamp(maxIterations, 1, 1000);
        maxMinutes = Math.Clamp(maxMinutes, 0.01, 24 * 60);
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CheckpointRoot);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var deadlineUtc = startedAtUtc.AddMinutes(maxMinutes);
        var runId = $"autonomous_loop_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var iterations = new List<AutonomousLoopIterationSummary>();
        var warnings = new List<string>();
        var status = config.Enabled ? "running" : "disabled";
        var nextAction = config.Enabled ? "start_iteration" : "enable_autonomous_loop_config";
        string? stopReason = config.Enabled ? null : "autonomous_loop_disabled";
        var idleIterations = 0;
        var totalWork = 0;

        if (!config.Enabled)
        {
            return WriteSummary(
                runId,
                status,
                maxIterations,
                maxMinutes,
                [],
                idleIterations,
                totalWork,
                nextAction,
                stopReason,
                warnings,
                startedAtUtc,
                deadlineUtc);
        }

        WriteState(new AutonomousLoopState(
            StateVersion: "autonomous_loop_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: "running",
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            AverageLearningValue: 0,
            NextAction: nextAction,
            LastIterationId: null,
            LastCheckpointPath: null,
            LastStopReason: null,
            StatePath: StatePath,
            SummaryPath: SummaryPath,
            LogPath: LogPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));

        while (iterations.Count < maxIterations && DateTimeOffset.UtcNow < deadlineUtc)
        {
            var iteration = RunIteration(runId, iterations.Count + 1, config);
            iterations.Add(iteration);
            AppendLog(iteration);
            totalWork += iteration.WorkPerformed ? 1 : 0;

            if (iteration.Idle)
            {
                idleIterations++;
            }
            else
            {
                idleIterations = 0;
            }

            nextAction = iteration.NextAction;
            if (!string.IsNullOrWhiteSpace(iteration.StopReason))
            {
                status = iteration.Status;
                stopReason = iteration.StopReason;
                break;
            }

            if (idleIterations >= config.MaxIdleIterations)
            {
                status = "stopped_max_idle_iterations";
                stopReason = "max_idle_iterations_reached";
                nextAction = "wait_for_new_needs_or_new_execution_results";
                break;
            }

            if (iterations.Count >= maxIterations)
            {
                status = "completed_max_iterations";
                stopReason = "max_iterations_reached";
                nextAction = "review_loop_summary";
                break;
            }

            if (DateTimeOffset.UtcNow >= deadlineUtc)
            {
                status = "completed_deadline_reached";
                stopReason = "max_minutes_reached";
                nextAction = "review_loop_summary";
                break;
            }

            if (config.SleepSecondsBetweenIterations > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(config.SleepSecondsBetweenIterations));
            }
        }

        if (status == "running")
        {
            status = DateTimeOffset.UtcNow >= deadlineUtc
                ? "completed_deadline_reached"
                : "completed";
            stopReason ??= DateTimeOffset.UtcNow >= deadlineUtc ? "max_minutes_reached" : "loop_completed";
            nextAction = "review_loop_summary";
        }

        return WriteSummary(
            runId,
            status,
            maxIterations,
            maxMinutes,
            iterations,
            idleIterations,
            totalWork,
            nextAction,
            stopReason,
            warnings,
            startedAtUtc,
            deadlineUtc);
    }

    public AutonomousLoopState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return EmptyState("not_started");
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousLoopState>(
                File.ReadAllText(StatePath),
                JsonDefaults.SnapshotReadOptions) ?? EmptyState("state_unreadable");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptyState("state_unreadable");
        }
    }

    public AutonomousLoopSummary? LoadSummary()
    {
        if (!File.Exists(SummaryPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousLoopSummary>(
                File.ReadAllText(SummaryPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<AutonomousLoopIterationSummary> LoadLog(int limit)
    {
        if (!File.Exists(LogPath))
        {
            return [];
        }

        var iterations = new List<AutonomousLoopIterationSummary>();
        foreach (var line in File.ReadLines(LogPath).Reverse())
        {
            if (iterations.Count >= Math.Clamp(limit, 1, 500))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var iteration = JsonSerializer.Deserialize<AutonomousLoopIterationSummary>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (iteration is not null)
                {
                    iterations.Add(iteration);
                }
            }
            catch (JsonException)
            {
                // Keep loop logs append-only; malformed historical lines should not block status.
            }
        }

        return iterations;
    }

    private AutonomousLoopIterationSummary RunIteration(string runId, int iterationNumber, AutonomousLoopConfig config)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var feedbackChanges = new List<string>();
        var status = "completed";
        string? stopReason = null;
        var nextAction = "continue_learning_loop";
        var resource = new ResourceGuard(_storagePaths).Check();
        var storageHygiene = new StorageHygieneService(_storagePaths);
        var cleanupPlan = storageHygiene.LoadPlan() ?? storageHygiene.BuildPlan();

        if (resource.ShouldStop)
        {
            status = "stopped_resource_guard";
            stopReason = "resource_guard_safe_stop";
            nextAction = "review_resource_status";
            warnings.AddRange(resource.Warnings);
            return BuildIteration(
                runId,
                iterationNumber,
                startedAtUtc,
                status,
                resource,
                cleanupPlan,
                needsDetected: 0,
                tasksPlanned: 0,
                executionResults: [],
                outcomes: [],
                cognitiveInsights: 0,
                workPerformed: false,
                idle: true,
                nextAction,
                stopReason,
                checkpointPath: null,
                warnings,
                feedbackChanges);
        }

        if (resource.ShouldPause)
        {
            status = "paused_resource_guard";
            stopReason = "resource_guard_pause";
            nextAction = "sleep_then_recheck_resources";
            warnings.AddRange(resource.Warnings);
            return BuildIteration(
                runId,
                iterationNumber,
                startedAtUtc,
                status,
                resource,
                cleanupPlan,
                needsDetected: 0,
                tasksPlanned: 0,
                executionResults: [],
                outcomes: [],
                cognitiveInsights: 0,
                workPerformed: false,
                idle: true,
                nextAction,
                stopReason,
                checkpointPath: null,
                warnings,
                feedbackChanges);
        }

        var planning = new AutonomousPlanningCycleService(_storagePaths);
        var decision = planning.RunPlanningCycle(config.MaxTasksPerIteration);
        var executor = new PlannedTaskExecutor(_storagePaths);
        var executionResults = executor.Execute(config.MaxTasksPerIteration);
        var evaluator = new TaskOutcomeEvaluator(_storagePaths);
        var outcomes = evaluator.Evaluate(config.MaxOutcomesPerIteration);
        var plannerFeedback = evaluator.LoadOrCreatePlannerFeedback();
        var goalFeedback = evaluator.LoadOrCreateGoalFeedback();
        var insights = new HypothesisGenerator(_storagePaths).Generate("trading");
        new CognitiveCoreService(_storagePaths).BuildStatus();
        var metaReview = new MetaReviewEngine(_storagePaths).RunReview();

        feedbackChanges.AddRange(plannerFeedback.TaskTypeFeedback
            .Where(item => Math.Abs(item.PriorityAdjustment) > 0.0001)
            .Select(item => $"{item.TaskType}:{item.Recommendation}:{item.PriorityAdjustment:0.####}")
            .Take(12));
        feedbackChanges.AddRange(goalFeedback.Goals
            .Where(goal => Math.Abs(goal.ProgressDelta) > 0.0001)
            .Select(goal => $"{goal.GoalId}:progress_delta={goal.ProgressDelta:0.####}")
            .Take(12));
        feedbackChanges.Add($"learning_strategy:{metaReview.LearningStrategy.CurrentStrategy}");
        feedbackChanges.AddRange(metaReview.GovernanceDecisions
            .Where(decision => !decision.Status.Equals("pass", StringComparison.OrdinalIgnoreCase))
            .Select(decision => $"governance:{decision.RuleId}:{decision.Status}")
            .Take(8));

        var averageLearning = outcomes.Count == 0
            ? 0
            : outcomes.Average(outcome => outcome.OutcomeScore.LearningValue);
        var executableResults = executionResults.Count > 0 || outcomes.Count > 0;
        var meaningfulWork = executableResults && (outcomes.Count == 0 || averageLearning >= config.MinLearningValueToContinue);
        var idle = !meaningfulWork;
        if (idle)
        {
            status = "idle_no_meaningful_work";
            nextAction = "wait_for_new_needs_or_unseen_execution_results";
            warnings.Add("no_new_meaningful_execution_or_outcome_feedback");
        }

        var checkpointPath = WriteCheckpoint(
            runId,
            iterationNumber,
            status,
            decision,
            executionResults,
            outcomes,
            insights.Count,
            evaluator.PlannerFeedbackPath);

        return BuildIteration(
            runId,
            iterationNumber,
            startedAtUtc,
            status,
            resource,
            cleanupPlan,
            decision.Needs.Count,
            decision.PlannedTasks.Count,
            executionResults,
            outcomes,
            insights.Count,
            workPerformed: meaningfulWork,
            idle,
            nextAction,
            stopReason,
            checkpointPath,
            warnings,
            feedbackChanges);
    }

    private AutonomousLoopIterationSummary BuildIteration(
        string runId,
        int iterationNumber,
        DateTimeOffset startedAtUtc,
        string status,
        ResourceSnapshot resource,
        CleanupPlan cleanupPlan,
        int needsDetected,
        int tasksPlanned,
        IReadOnlyList<PlannedTaskExecutionResult> executionResults,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        int cognitiveInsights,
        bool workPerformed,
        bool idle,
        string nextAction,
        string? stopReason,
        string? checkpointPath,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> feedbackChanges)
    {
        var planner = new AutonomousPlanningCycleService(_storagePaths);
        var executor = new PlannedTaskExecutor(_storagePaths);
        var evaluator = new TaskOutcomeEvaluator(_storagePaths);
        var insights = new HypothesisGenerator(_storagePaths);
        return new AutonomousLoopIterationSummary(
            IterationId: $"autonomous_loop_iteration_{runId}_{iterationNumber:000}",
            RunId: runId,
            IterationNumber: iterationNumber,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            ResourceAction: resource.Action,
            ResourceWarnings: resource.Warnings,
            CleanupCandidates: cleanupPlan.Candidates.Count,
            NeedsDetected: needsDetected,
            TasksPlanned: tasksPlanned,
            TasksExecuted: executionResults.Count,
            TasksCompleted: executionResults.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
            TasksSkipped: executionResults.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            TasksFailed: executionResults.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            OutcomesEvaluated: outcomes.Count,
            AverageOutcomeUsefulness: Math.Round(outcomes.Count == 0 ? 0 : outcomes.Average(outcome => outcome.OutcomeScore.UsefulnessScore), 4),
            AverageOutcomeLearningValue: Math.Round(outcomes.Count == 0 ? 0 : outcomes.Average(outcome => outcome.OutcomeScore.LearningValue), 4),
            CognitiveInsights: cognitiveInsights,
            WorkPerformed: workPerformed,
            Idle: idle,
            NextAction: nextAction,
            StopReason: stopReason,
            CheckpointPath: checkpointPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            FeedbackChanges: feedbackChanges.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PlanningStatusPath: planner.PlanningStatusPath,
            PlannedTasksPath: planner.PlannedTasksPath,
            TaskExecutionStatePath: executor.ExecutionStatePath,
            PlannerFeedbackPath: evaluator.PlannerFeedbackPath,
            GoalFeedbackPath: evaluator.GoalFeedbackPath,
            CognitiveInsightsPath: insights.InsightsPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private string WriteCheckpoint(
        string runId,
        int iterationNumber,
        string status,
        PlanningDecision decision,
        IReadOnlyList<PlannedTaskExecutionResult> executionResults,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        int cognitiveInsights,
        string plannerFeedbackPath)
    {
        Directory.CreateDirectory(CheckpointRoot);
        var path = Path.Combine(
            CheckpointRoot,
            $"{runId}.iteration_{iterationNumber:000}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.checkpoint.json");
        var checkpoint = new
        {
            checkpoint_version = "autonomous_loop_checkpoint_v1",
            updated_at_utc = DateTimeOffset.UtcNow,
            run_id = runId,
            iteration = iterationNumber,
            status,
            needs = decision.Needs.Count,
            planned_tasks = decision.PlannedTasks.Count,
            execution_results = executionResults.Count,
            outcomes = outcomes.Count,
            cognitive_insights = cognitiveInsights,
            planner_feedback_path = plannerFeedbackPath,
            no_trading_execution = true,
            no_broker_action = true,
            no_auto_trading = true,
            human_review_required = true
        };
        File.WriteAllText(path, JsonSerializer.Serialize(checkpoint, JsonDefaults.WriteOptions));
        return path;
    }

    private AutonomousLoopSummary WriteSummary(
        string runId,
        string status,
        int requestedIterations,
        double maxMinutes,
        IReadOnlyList<AutonomousLoopIterationSummary> iterations,
        int idleIterations,
        int workPerformed,
        string nextAction,
        string? stopReason,
        IReadOnlyList<string> warnings,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineUtc)
    {
        var recent = iterations
            .OrderByDescending(iteration => iteration.CompletedAtUtc)
            .Take(20)
            .ToList();
        var averageLearning = Math.Round(iterations.Count == 0 ? 0 : iterations.Average(iteration => iteration.AverageOutcomeLearningValue), 4);
        var last = recent.FirstOrDefault();
        var summary = new AutonomousLoopSummary(
            SummaryVersion: "autonomous_loop_summary_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            RunId: runId,
            Status: status,
            RequestedIterations: requestedIterations,
            MaxMinutes: Math.Round(maxMinutes, 3),
            IterationsCompleted: iterations.Count,
            IdleIterations: idleIterations,
            WorkPerformed: workPerformed,
            AverageLearningValue: averageLearning,
            NextAction: nextAction,
            StopReason: stopReason,
            LastIteration: last,
            RecentIterations: recent,
            Warnings: warnings
                .Concat(iterations.SelectMany(iteration => iteration.Warnings))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(SummaryPath, JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions));

        WriteState(new AutonomousLoopState(
            StateVersion: "autonomous_loop_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: iterations.Count,
            IdleIterations: idleIterations,
            WorkPerformed: workPerformed,
            AverageLearningValue: averageLearning,
            NextAction: nextAction,
            LastIterationId: last?.IterationId,
            LastCheckpointPath: last?.CheckpointPath,
            LastStopReason: stopReason,
            StatePath: StatePath,
            SummaryPath: SummaryPath,
            LogPath: LogPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));
        return summary;
    }

    private void AppendLog(AutonomousLoopIterationSummary iteration)
    {
        Directory.CreateDirectory(Root);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(iteration, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private AutonomousLoopState WriteState(AutonomousLoopState state)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    private AutonomousLoopState EmptyState(string status) =>
        new(
            StateVersion: "autonomous_loop_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: string.Empty,
            StartedAtUtc: null,
            DeadlineUtc: null,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            AverageLearningValue: 0,
            NextAction: "run_autonomous_loop",
            LastIterationId: null,
            LastCheckpointPath: null,
            LastStopReason: null,
            StatePath: StatePath,
            SummaryPath: SummaryPath,
            LogPath: LogPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
}
