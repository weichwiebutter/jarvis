using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousImprovementExecutionTask(
    string TaskId,
    string SourceWarning,
    string Title,
    string Domain,
    string Priority,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExecutedAtUtc,
    string Result,
    string? OutputReportPath,
    IReadOnlyList<string> Warnings,
    bool RequiresHumanReview,
    bool AutoFixable,
    bool SafeToExecute);

public sealed record AutonomousImprovementExecutionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int Pending,
    int Planned,
    int Executed,
    int Skipped,
    int Failed,
    int NeedsHumanReview,
    DateTimeOffset? LastExecutedAtUtc,
    IReadOnlyList<AutonomousImprovementExecutionTask> Tasks,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string QueuePath,
    string ReportPath,
    string MarkdownPath,
    string LogPath);

public sealed class AutonomousImprovementExecutorService
{
    private static readonly ISet<string> SafeWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "oos_data_missing",
        "knowledge_validation_queue_missing",
        "hypotheses_without_validation_queue",
        "storage_cleanup_candidates",
        "no_robust_strategies",
        "trust_score_too_low",
        "quality_score_too_low",
        "insufficient_sources",
        "validation_score_too_low",
        "not_recently_validated",
        "active_contradiction",
        "pending_human_review",
        "not_yet_trusted_or_robust"
    };

    private readonly StoragePaths _storagePaths;

    public AutonomousImprovementExecutorService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_execution");

    public string ReportPath => Path.Combine(Root, "autonomous_improvement_execution.json");

    public string MarkdownPath => Path.Combine(Root, "autonomous_improvement_execution.md");

    public string LogPath => Path.Combine(Root, "execution_log.jsonl");

    public AutonomousImprovementExecutionReport Execute(int maxItems = 20)
    {
        Directory.CreateDirectory(Root);
        var queueService = new AutonomousImprovementQueueService(_storagePaths);
        var queue = queueService.Load() ?? queueService.Generate();
        var eligible = queue.Tasks
            .Where(IsSafeToExecute)
            .Where(task => task.Status.Equals("open", StringComparison.OrdinalIgnoreCase) || task.Status.Equals("planned", StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(maxItems, 1, 50))
            .ToList();
        var tasks = new List<AutonomousImprovementExecutionTask>();
        foreach (var task in queue.Tasks)
        {
            var current = AutonomousImprovementExecutionTaskFromQueue(task, "pending", null, "Wartet auf Ausführung.", [], task.SafeToExecute);
            if (!eligible.Any(item => item.TaskId == task.TaskId))
            {
                if (task.RequiresHumanReview)
                {
                    current = current with { Status = "needs_human_review", Result = "Frank muss prüfen", SafeToExecute = false };
                }
                else if (!task.SafeToExecute)
                {
                    current = current with { Status = "skipped", Result = "Nicht sicher ausführbar", Warnings = ["task_not_safe_to_execute"], SafeToExecute = false };
                }
                tasks.Add(current);
                continue;
            }

            var planned = current with { Status = "planned", Result = "Aufgabe geplant.", Warnings = [] };
            tasks.Add(planned);
            var execution = ExecuteTask(task);
            tasks[^1] = execution;
            AppendLog(execution);
        }

        var report = new AutonomousImprovementExecutionReport(
            ReportVersion: "autonomous_improvement_execution_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Pending: tasks.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            Planned: tasks.Count(item => item.Status.Equals("planned", StringComparison.OrdinalIgnoreCase)),
            Executed: tasks.Count(item => item.Status.Equals("executed", StringComparison.OrdinalIgnoreCase)),
            Skipped: tasks.Count(item => item.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            Failed: tasks.Count(item => item.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            NeedsHumanReview: tasks.Count(item => item.Status.Equals("needs_human_review", StringComparison.OrdinalIgnoreCase)),
            LastExecutedAtUtc: tasks.Where(item => item.ExecutedAtUtc is not null).Select(item => item.ExecutedAtUtc).Max(),
            Tasks: tasks,
            Warnings: tasks.Count == 0 ? ["autonomous_improvement_execution_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            QueuePath: queueService.QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            LogPath: LogPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public AutonomousImprovementExecutionReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousImprovementExecutionReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private AutonomousImprovementExecutionTask ExecuteTask(AutonomousImprovementTask task)
    {
        try
        {
            return task.SourceWarning switch
            {
                "oos_data_missing" => ExecuteOosPlanning(task),
                "knowledge_validation_queue_missing" => ExecuteValidationQueueRepair(task),
                "hypotheses_without_validation_queue" => ExecuteHypothesesQueueing(task),
                "storage_cleanup_candidates" => ExecuteStoragePlanRefresh(task),
                "no_robust_strategies" => ExecuteRobustnessPlan(task),
                "trust_score_too_low" => ExecuteTrustImprovementPlan(task),
                "quality_score_too_low" => ExecuteTrustImprovementPlan(task),
                "insufficient_sources" => ExecuteTrustImprovementPlan(task),
                "validation_score_too_low" => ExecuteTrustImprovementPlan(task),
                "not_recently_validated" => ExecuteTrustImprovementPlan(task),
                "active_contradiction" => ExecuteTrustImprovementPlan(task),
                "not_yet_trusted_or_robust" => ExecuteTrustImprovementPlan(task),
                _ => AutonomousImprovementExecutionTaskFromQueue(task, "skipped", DateTimeOffset.UtcNow, "Unsupported task type.", ["unsupported_task_type"], task.SafeToExecute)
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return AutonomousImprovementExecutionTaskFromQueue(task, "failed", DateTimeOffset.UtcNow, ex.Message, [ex.Message], task.SafeToExecute);
        }
    }

    private AutonomousImprovementExecutionTask ExecuteOosPlanning(AutonomousImprovementTask task)
    {
        var validation = new KnowledgeValidationStrategy(_storagePaths);
        var report = validation.GeneratePlans(50);
        var status = validation.BuildStatus();
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"OOS-/Walk-Forward-Plan aktualisiert; open_plans={report.OpenPlans}; pending_tasks={status.ValidationTasksPending}.",
            [],
            task.SafeToExecute,
            validation.PlansPath);
    }

    private AutonomousImprovementExecutionTask ExecuteValidationQueueRepair(AutonomousImprovementTask task)
    {
        var validation = new KnowledgeValidationStrategy(_storagePaths);
        var report = validation.GeneratePlans(50);
        var status = validation.ValidateKnowledge(50);
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"Validation Queue repariert oder befüllt; open_plans={report.OpenPlans}; pending_tasks={status.ValidationTasksPending}.",
            [],
            task.SafeToExecute,
            status.ResearchQueuePath);
    }

    private AutonomousImprovementExecutionTask ExecuteHypothesesQueueing(AutonomousImprovementTask task)
    {
        var queue = new ResearchQueueService(_storagePaths);
        var validation = new KnowledgeValidationStrategy(_storagePaths);
        var report = validation.LoadPlanReport() ?? validation.GeneratePlans(50);
        var queued = queue.EnqueueValidationPlans(report.Plans.Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToList(), 100);
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"Hypothesen in die Validation Queue überführt; queue_items={queued.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase))}.",
            [],
            task.SafeToExecute,
            queue.QueuePath);
    }

    private AutonomousImprovementExecutionTask ExecuteStoragePlanRefresh(AutonomousImprovementTask task)
    {
        var hygiene = new StorageHygieneService(_storagePaths);
        var plan = hygiene.BuildPlan();
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"Cleanup-Plan aktualisiert; candidates={plan.Candidates.Count}.",
            [],
            task.SafeToExecute,
            hygiene.CleanupPlanPath);
    }

    private AutonomousImprovementExecutionTask ExecuteRobustnessPlan(AutonomousImprovementTask task)
    {
        var strategy = new StrategyResearchService(_storagePaths);
        var memory = strategy.LoadOrCreateMemory();
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"Research-/Robustness-Plan aktualisiert; variants_tested={memory.VariantsTested}; top_variants={memory.TopVariants.Count}.",
            [],
            task.SafeToExecute,
            strategy.MemoryPath);
    }

    private AutonomousImprovementExecutionTask ExecuteTrustImprovementPlan(AutonomousImprovementTask task)
    {
        var planner = new KnowledgeTrustImprovementPlannerService(_storagePaths);
        var report = planner.Run();
        return AutonomousImprovementExecutionTaskFromQueue(
            task,
            "executed",
            DateTimeOffset.UtcNow,
            $"Trust Improvement Plan aktualisiert; planned_actions={report.PlannedActions.Count}; blocker_counts={report.BlockerCounts.Count}.",
            [],
            task.SafeToExecute,
            planner.ReportPath);
    }

    private static bool IsSafeToExecute(AutonomousImprovementTask task)
    {
        return task.SafeToExecute
            && task.AutoFixable
            && !task.RequiresHumanReview
            && SafeWarnings.Contains(task.SourceWarning);
    }

    private static AutonomousImprovementExecutionTask AutonomousImprovementExecutionTaskFromQueue(
        AutonomousImprovementTask task,
        string status,
        DateTimeOffset? executedAtUtc,
        string result,
        IReadOnlyList<string> warnings,
        bool safeToExecute,
        string? outputReportPath = null)
    {
        return new AutonomousImprovementExecutionTask(
            TaskId: task.TaskId,
            SourceWarning: task.SourceWarning,
            Title: task.Title,
            Domain: task.Domain,
            Priority: task.Priority,
            Status: status,
            CreatedAtUtc: task.CreatedAtUtc,
            ExecutedAtUtc: executedAtUtc,
            Result: result,
            OutputReportPath: outputReportPath,
            Warnings: warnings,
            RequiresHumanReview: task.RequiresHumanReview,
            AutoFixable: task.AutoFixable,
            SafeToExecute: safeToExecute);
    }

    private void AppendLog(AutonomousImprovementExecutionTask task)
    {
        var line = JsonSerializer.Serialize(task, JsonDefaults.WriteOptions);
        File.AppendAllText(LogPath, line + Environment.NewLine);
    }

    private static string BuildMarkdown(AutonomousImprovementExecutionReport report)
    {
        var lines = new List<string>
        {
            "# Autonomous Improvement Execution",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Executed: {report.Executed}",
            $"- Planned: {report.Planned}",
            $"- Skipped: {report.Skipped}",
            $"- Failed: {report.Failed}",
            $"- Frank nötig: {report.NeedsHumanReview}",
            string.Empty,
            "## Aufgaben",
        };

        lines.AddRange(report.Tasks.Select(task =>
            $"- {task.Title}: {task.Status} -> {task.Result}"));
        return string.Join(Environment.NewLine, lines);
    }
}
