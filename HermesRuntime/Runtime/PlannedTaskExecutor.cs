using System.Text.Json;

namespace Hermes.Runtime;

public sealed class PlannedTaskExecutor
{
    private static readonly ISet<string> TerminalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "skipped",
        "failed"
    };

    private readonly StoragePaths _storagePaths;

    public PlannedTaskExecutor(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ExecutionLogPath => Path.Combine(Root, "task_execution_log.jsonl");

    public string ExecutionStatePath => Path.Combine(Root, "task_execution_state.json");

    public IReadOnlyList<PlannedTaskExecutionResult> Execute(int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 100);
        Directory.CreateDirectory(Root);

        var planning = new AutonomousPlanningCycleService(_storagePaths);
        var decision = planning.LoadLatestDecision() ?? planning.PlanNextTasks(maxItems);
        var alreadyLogged = LoadRecentResults(1000)
            .Where(result => TerminalStatuses.Contains(result.Status))
            .Select(result => result.TaskId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = decision.PlannedTasks
            .Where(task => AutonomousTaskPlanner.AllowedTaskTypes.Contains(task.TaskType))
            .Where(task => !TerminalStatuses.Contains(task.Status))
            .Where(task => !alreadyLogged.Contains(task.TaskId))
            .OrderByDescending(task => task.Priority.TotalScore)
            .ThenBy(task => task.TaskType, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        var results = new List<PlannedTaskExecutionResult>();
        var workKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in candidates)
        {
            var workKey = WorkKey(task.TaskType);
            PlannedTaskExecutionResult result;
            if (!workKeys.Add(workKey))
            {
                result = BuildResult(
                    task,
                    "skipped",
                    $"Task work already covered in this batch by '{workKey}'.",
                    [],
                    ["duplicate_work_key_in_current_batch"],
                    "duplicate_work_key_in_current_batch");
            }
            else
            {
                result = ExecuteOne(task, planning);
            }

            results.Add(result);
            AppendLog(result);
            planning.UpdateTaskStatuses(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [task.TaskId] = result.Status
            });
            new ResearchQueueService(_storagePaths).MarkPlannedTaskExecution(
                task.TaskId,
                result.Status,
                result.SkippedReason ?? result.Reason,
                result.Warnings);
        }

        if (results.Count > 0)
        {
            RefreshCognitivePlanningOutputs();
        }

        WriteState(results);
        return results;
    }

    public PlannedTaskExecutionState BuildStatus()
    {
        Directory.CreateDirectory(Root);
        var planning = new AutonomousPlanningCycleService(_storagePaths);
        var decision = planning.LoadLatestDecision();
        var tasks = decision?.PlannedTasks ?? [];
        var recent = LoadRecentResults(20);
        var state = new PlannedTaskExecutionState(
            StateVersion: "planned_task_execution_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PendingTasks: tasks.Count(task => !TerminalStatuses.Contains(task.Status) && !task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            RunningTasks: tasks.Count(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            CompletedTasks: tasks.Count(task => task.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
            SkippedTasks: tasks.Count(task => task.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            FailedTasks: tasks.Count(task => task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            RunningTaskId: tasks.FirstOrDefault(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase))?.TaskId,
            LastTaskId: recent.FirstOrDefault()?.TaskId,
            LastExecutionUtc: recent.FirstOrDefault()?.CompletedAtUtc ?? recent.FirstOrDefault()?.StartedAtUtc,
            LastStatus: recent.FirstOrDefault()?.Status ?? "not_started",
            ExecutionLogPath: ExecutionLogPath,
            RecentResults: recent,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(ExecutionStatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    public PlannedTaskExecutionState? LoadState()
    {
        if (!File.Exists(ExecutionStatePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlannedTaskExecutionState>(
                File.ReadAllText(ExecutionStatePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<PlannedTaskExecutionResult> LoadRecentResults(int limit)
    {
        if (!File.Exists(ExecutionLogPath))
        {
            return [];
        }

        var results = new List<PlannedTaskExecutionResult>();
        foreach (var line in File.ReadLines(ExecutionLogPath).Reverse())
        {
            if (results.Count >= Math.Clamp(limit, 1, 1000))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var result = JsonSerializer.Deserialize<PlannedTaskExecutionResult>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
            catch (JsonException)
            {
                // Keep the execution log append-only; unreadable historical lines are ignored.
            }
        }

        return results;
    }

    private PlannedTaskExecutionResult ExecuteOne(PlannedTask task, AutonomousPlanningCycleService planning)
    {
        planning.UpdateTaskStatuses(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [task.TaskId] = "running"
        });
        new ResearchQueueService(_storagePaths).MarkPlannedTaskExecution(
            task.TaskId,
            "running",
            "controlled_execution_started",
            []);

        var guard = CheckGuards(task);
        if (guard is not null)
        {
            return guard;
        }

        try
        {
            return ExecuteAllowedTask(task);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return BuildResult(
                task,
                "failed",
                $"Task failed with {ex.GetType().Name}.",
                [],
                [ex.Message],
                null);
        }
    }

    private PlannedTaskExecutionResult? CheckGuards(PlannedTask task)
    {
        var resource = new ResourceGuard(_storagePaths).Check();
        var hygiene = new StorageHygieneService(_storagePaths);
        var cleanupPlan = hygiene.LoadPlan() ?? hygiene.BuildPlan();
        var warnings = resource.Warnings
            .Concat(cleanupPlan.Candidates.Count > 0 ? [$"cleanup_candidates:{cleanupPlan.Candidates.Count}"] : [])
            .ToList();

        if (!task.NoTradingExecution || !task.HumanReviewRequired)
        {
            return BuildResult(
                task,
                "skipped",
                "Task safety flags are not acceptable for controlled execution.",
                [],
                warnings.Concat(["planned_task_safety_flags_invalid"]).ToList(),
                "planned_task_safety_flags_invalid");
        }

        if (resource.ShouldStop)
        {
            return BuildResult(
                task,
                "skipped",
                "ResourceGuard requested safe stop.",
                [new ResourceGuard(_storagePaths).StatusPath],
                warnings,
                "resource_guard_stop");
        }

        if (resource.ShouldPause)
        {
            return BuildResult(
                task,
                "skipped",
                "ResourceGuard requested pause.",
                [new ResourceGuard(_storagePaths).StatusPath],
                warnings,
                "resource_guard_pause");
        }

        if (resource.FreeDiskPercent < 8)
        {
            return BuildResult(
                task,
                "skipped",
                "StorageGuard critical disk threshold reached.",
                [new ResourceGuard(_storagePaths).StatusPath, hygiene.CleanupPlanPath],
                warnings,
                "storage_guard_critical_disk");
        }

        return null;
    }

    private PlannedTaskExecutionResult ExecuteAllowedTask(PlannedTask task)
    {
        return task.TaskType switch
        {
            "scan_knowledge_sources" => ExecuteScanKnowledgeSources(task),
            "process_research_queue" => ExecuteProcessResearchQueue(task),
            "generate_hypotheses" => ExecuteGenerateHypotheses(task),
            "run_walkforward_validation" => ExecuteWalkForwardValidation(task),
            "run_strategy_research" => ExecuteStrategyResearch(task),
            "run_realism_report" => ExecuteRealismReport(task),
            "run_overfit_report" => ExecuteOverfitReport(task),
            "run_storage_hygiene" => ExecuteStorageHygiene(task),
            "download_missing_market_data" => BuildResult(
                task,
                "skipped",
                "Market-data download needs explicit symbol/timeframe/range configuration and is not executed from a generic planned task.",
                [],
                ["download_missing_market_data_requires_explicit_parameters"],
                "requires_explicit_market_data_parameters"),
            "generate_cognitive_insights" => ExecuteGenerateCognitiveInsights(task),
            _ => BuildResult(
                task,
                "skipped",
                "Task type is not in the controlled execution whitelist.",
                [],
                [$"unsupported_task_type:{task.TaskType}"],
                "unsupported_task_type")
        };
    }

    private PlannedTaskExecutionResult ExecuteScanKnowledgeSources(PlannedTask task)
    {
        var sources = new KnowledgeSourceScout(_storagePaths).Scan();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        return BuildResult(
            task,
            "completed",
            $"Knowledge sources scanned: {sources.Count}; catalog items: {catalog.Count}.",
            [new KnowledgeSourceRegistry(_storagePaths).SourcesPath, new KnowledgeCatalog(_storagePaths).CatalogPath],
            []);
    }

    private PlannedTaskExecutionResult ExecuteProcessResearchQueue(PlannedTask task)
    {
        var queueService = new ResearchQueueService(_storagePaths);
        var before = queueService.LoadOrCreateQueue();
        var beforeProcessed = before.Items
            .Where(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.QueueItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var after = queueService.ProcessNonPlannedItems(maxItems: 20);
        var processed = after.Items.Count(item =>
            item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)
            && !beforeProcessed.Contains(item.QueueItemId));
        return BuildResult(
            task,
            "completed",
            $"Research Queue controlled processing completed; non-planned items processed: {processed}.",
            [queueService.QueuePath],
            processed == 0 ? ["no_non_planned_open_queue_items_processed"] : []);
    }

    private PlannedTaskExecutionResult ExecuteGenerateHypotheses(PlannedTask task)
    {
        var domain = string.IsNullOrWhiteSpace(task.Domain) ? "trading" : task.Domain;
        var service = new HypothesisGenerator(_storagePaths);
        var hypotheses = service.Generate(domain);
        return BuildResult(
            task,
            "completed",
            $"Hypotheses generated for domain '{domain}': {hypotheses.Count}.",
            [service.HypothesesPath, service.InsightsPath],
            hypotheses.Count == 0 ? ["no_hypotheses_generated"] : []);
    }

    private PlannedTaskExecutionResult ExecuteWalkForwardValidation(PlannedTask task)
    {
        var service = new WalkForwardValidationService(_storagePaths);
        var report = service.LoadReport();
        if (report is null)
        {
            return BuildResult(
                task,
                "skipped",
                "Walk-forward validation is a heavy research batch and no existing report is available for controlled execution.",
                [service.WalkForwardPath, service.WalkForwardSummaryPath],
                ["walkforward_report_missing"],
                "requires_nightly_or_explicit_walkforward_batch");
        }

        return BuildResult(
            task,
            "completed",
            $"Walk-forward validation report checked; strategies={report.StrategiesEvaluated}; robust={report.RobustStrategies}; overfit={report.OverfitSuspectedStrategies}.",
            [service.WalkForwardPath, service.WalkForwardSummaryPath, service.OverfitReportPath],
            report.StrategiesEvaluated == 0 ? ["no_strategies_evaluated"] : []);
    }

    private PlannedTaskExecutionResult ExecuteStrategyResearch(PlannedTask task)
    {
        var service = new StrategyResearchService(_storagePaths);
        var memory = service.LoadOrCreateMemory();
        if (memory.VariantsTested == 0)
        {
            return BuildResult(
                task,
                "skipped",
                "Strategy research is a heavy batch and no existing strategy memory is available for controlled execution.",
                [service.MemoryPath],
                ["strategy_research_memory_empty"],
                "requires_nightly_or_explicit_strategy_research_batch");
        }

        return BuildResult(
            task,
            "completed",
            $"Strategy research memory checked; variants tested: {memory.VariantsTested}; top variants: {memory.TopVariants.Count}.",
            [service.MemoryPath],
            memory.Warnings);
    }

    private PlannedTaskExecutionResult ExecuteRealismReport(PlannedTask task)
    {
        var service = new RealisticSimulationService(_storagePaths);
        var report = service.LoadRealismReport();
        if (report is null)
        {
            return BuildResult(
                task,
                "skipped",
                "Realism report generation is a heavy simulation batch and no existing report is available for controlled execution.",
                [service.RealismReportPath],
                ["realism_report_missing"],
                "requires_nightly_or_explicit_realism_batch");
        }

        return BuildResult(
            task,
            "completed",
            $"Realism report checked; strategies={report.StrategiesEvaluated}; suspicious={report.SuspiciousStrategies}; too_good_to_be_true={report.TooGoodToBeTrueStrategies}.",
            [service.RealismReportPath, service.CostSensitivityReportPath, service.LatestStatusPath],
            report.StrategiesEvaluated == 0 ? ["no_realism_strategies_evaluated"] : []);
    }

    private PlannedTaskExecutionResult ExecuteOverfitReport(PlannedTask task)
    {
        var service = new WalkForwardValidationService(_storagePaths);
        var report = service.LoadReport();
        if (report is null || !File.Exists(service.OverfitReportPath))
        {
            return BuildResult(
                task,
                "skipped",
                "Overfit report generation depends on walk-forward simulation and is deferred to Nightly/explicit validation.",
                [service.OverfitReportPath, service.WalkForwardSummaryPath],
                ["overfit_report_missing"],
                "requires_nightly_or_explicit_overfit_batch");
        }

        return BuildResult(
            task,
            "completed",
            $"Overfit report checked; overfit suspected: {report.OverfitSuspectedStrategies}; high risk: {report.HighRiskStrategies}.",
            [service.OverfitReportPath, service.StrategyResearchOverfitReportPath, service.WalkForwardSummaryPath],
            []);
    }

    private PlannedTaskExecutionResult ExecuteStorageHygiene(PlannedTask task)
    {
        var service = new StorageHygieneService(_storagePaths);
        var plan = service.BuildPlan();
        return BuildResult(
            task,
            "completed",
            $"Storage hygiene plan updated; safe cleanup candidates: {plan.Candidates.Count}.",
            [service.CleanupPlanPath],
            []);
    }

    private PlannedTaskExecutionResult ExecuteGenerateCognitiveInsights(PlannedTask task)
    {
        var hypotheses = new HypothesisGenerator(_storagePaths).Generate("trading");
        var status = new CognitiveCoreService(_storagePaths).BuildStatus();
        return BuildResult(
            task,
            "completed",
            $"Cognitive insights updated; hypotheses={hypotheses.Count}; queue_items={status.QueueItemCount}.",
            [new HypothesisGenerator(_storagePaths).InsightsPath, new CognitiveCoreService(_storagePaths).StatusPath],
            hypotheses.Count == 0 ? ["no_cognitive_insights_generated"] : []);
    }

    private void RefreshCognitivePlanningOutputs()
    {
        try
        {
            new HypothesisGenerator(_storagePaths).Generate("trading");
            var needs = new NeedDetectionEngine(_storagePaths).Detect();
            new GoalManager(_storagePaths).EvaluateGoals(needs);
            new CognitiveCoreService(_storagePaths).BuildStatus();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            var warning = BuildResult(
                new PlannedTask(
                    TaskId: "planned_task_executor_refresh",
                    TaskType: "generate_cognitive_insights",
                    Domain: "trading",
                    GoalId: "improve_cognitive_memory_quality",
                    NeedId: "executor_refresh",
                    QueueType: "review",
                    Priority: new PriorityScore(0, 0, 0, 0, 0, 0, 0),
                    Reason: "Refresh Cognitive Core outputs after controlled task execution.",
                    ExpectedOutcome: "cognitive_outputs_refreshed",
                    SourceRefs: [],
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Status: "planned",
                    NoTradingExecution: true,
                    HumanReviewRequired: true),
                "failed",
                "Post-execution cognitive refresh failed.",
                [],
                [ex.Message],
                null);
            AppendLog(warning);
        }
    }

    private void WriteState(IReadOnlyList<PlannedTaskExecutionResult> latestResults)
    {
        var planning = new AutonomousPlanningCycleService(_storagePaths);
        var decision = planning.LoadLatestDecision();
        var tasks = decision?.PlannedTasks ?? [];
        var recent = latestResults
            .Concat(LoadRecentResults(20))
            .GroupBy(result => $"{result.TaskId}:{result.StartedAtUtc:O}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(result => result.CompletedAtUtc ?? result.StartedAtUtc)
            .Take(20)
            .ToList();

        var state = new PlannedTaskExecutionState(
            StateVersion: "planned_task_execution_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PendingTasks: tasks.Count(task => !TerminalStatuses.Contains(task.Status) && !task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            RunningTasks: tasks.Count(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            CompletedTasks: tasks.Count(task => task.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
            SkippedTasks: tasks.Count(task => task.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            FailedTasks: tasks.Count(task => task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            RunningTaskId: tasks.FirstOrDefault(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase))?.TaskId,
            LastTaskId: recent.FirstOrDefault()?.TaskId,
            LastExecutionUtc: recent.FirstOrDefault()?.CompletedAtUtc ?? recent.FirstOrDefault()?.StartedAtUtc,
            LastStatus: recent.FirstOrDefault()?.Status ?? "not_started",
            ExecutionLogPath: ExecutionLogPath,
            RecentResults: recent,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(ExecutionStatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
    }

    private void AppendLog(PlannedTaskExecutionResult result)
    {
        Directory.CreateDirectory(Root);
        File.AppendAllText(ExecutionLogPath, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private static PlannedTaskExecutionResult BuildResult(
        PlannedTask task,
        string status,
        string reason,
        IReadOnlyList<string> outputPaths,
        IReadOnlyList<string> warnings,
        string? skippedReason = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new PlannedTaskExecutionResult(
            TaskId: task.TaskId,
            TaskType: task.TaskType,
            StartedAtUtc: now,
            CompletedAtUtc: now,
            Status: status,
            Reason: reason,
            NeedId: task.NeedId,
            GoalId: task.GoalId,
            OutputPaths: outputPaths,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SkippedReason: skippedReason,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static string WorkKey(string taskType) =>
        taskType.ToLowerInvariant() switch
        {
            "run_walkforward_validation" or "run_overfit_report" => "walkforward_validation",
            "run_realism_report" => "realistic_simulation",
            _ => taskType
        };
}
