using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PlannedTaskExecutorDiagnosis(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int TotalCount,
    int PendingCount,
    int ActiveCount,
    int ExecutableCount,
    int BlockedCount,
    int ReviewCount,
    int DoneCount,
    int ArchivedCount,
    int DeletableCount,
    int KeepRetentionCount,
    int Retain7dRetentionCount,
    int Retain30dRetentionCount,
    int DeletableRetentionCount,
    int UnsupportedCount,
    int WaitingForEvidenceCount,
    int SkippedCount,
    int CompletedCount,
    int FailedCount,
    DateTimeOffset? LastSuccessfulExecutorRunUtc,
    IReadOnlyList<PlannedTaskDiagnosisEntry> Entries,
    string RecommendedNextAction,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record PlannedTaskDiagnosisEntry(
    string TaskId,
    string TaskType,
    string Status,
    bool Executable,
    string Reason,
    string LifecycleClass,
    string RetentionClass,
    double AgeDays);

public sealed class PlannedTaskExecutorDiagnosisService
{
    private readonly StoragePaths _storagePaths;

    public PlannedTaskExecutorDiagnosisService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue");

    public string ReportJsonPath => Path.Combine(Root, "planned_task_executor_diagnosis.json");

    public string ReportMarkdownPath => Path.Combine(Root, "planned_task_executor_diagnosis.md");

    public PlannedTaskExecutorDiagnosis? Load()
    {
        if (!File.Exists(ReportJsonPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlannedTaskExecutorDiagnosis>(
                File.ReadAllText(ReportJsonPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public PlannedTaskExecutorDiagnosis Build()
    {
        Directory.CreateDirectory(Root);
        var executor = new PlannedTaskExecutor(_storagePaths);
        var planning = new AutonomousPlanningCycleService(_storagePaths);
        var decision = planning.LoadLatestDecision();
        var tasks = decision?.PlannedTasks ?? [];
        var recent = executor.LoadRecentResults(200);
        var recentTerminal = recent.Where(result =>
                result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || result.Status.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase)
                || result.Status.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
                || result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
                || result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entries = tasks
            .OrderByDescending(task => task.Priority.TotalScore)
            .ThenBy(task => task.TaskType, StringComparer.Ordinal)
            .Select(task =>
            {
                var (executable, reason) = EvaluateExecutability(task);
                var ageDays = Math.Max(0d, (DateTimeOffset.UtcNow - task.CreatedAtUtc).TotalDays);
                var lifecycleClass = ClassifyLifecycle(task, ageDays);
                var retentionClass = ClassifyRetention(lifecycleClass, ageDays);
                return new PlannedTaskDiagnosisEntry(
                    task.TaskId,
                    task.TaskType,
                    task.Status,
                    executable,
                    reason,
                    lifecycleClass,
                    retentionClass,
                    ageDays);
            })
            .ToList();

        var activeEntries = entries.Where(entry => IsActivePlannedStatus(entry.Status)).ToList();
        var keepRetentionCount = entries.Count(entry => entry.RetentionClass.Equals("keep", StringComparison.OrdinalIgnoreCase));
        var retain7dRetentionCount = entries.Count(entry => entry.RetentionClass.Equals("retain_7d", StringComparison.OrdinalIgnoreCase));
        var retain30dRetentionCount = entries.Count(entry => entry.RetentionClass.Equals("retain_30d", StringComparison.OrdinalIgnoreCase));
        var deletableRetentionCount = entries.Count(entry => entry.RetentionClass.Equals("deletable", StringComparison.OrdinalIgnoreCase));
        var diagnosis = new PlannedTaskExecutorDiagnosis(
            ReportVersion: "planned_task_executor_diagnosis_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            TotalCount: tasks.Count,
            PendingCount: activeEntries.Count,
            ActiveCount: entries.Count(entry => entry.LifecycleClass.Equals("active", StringComparison.OrdinalIgnoreCase)),
            ExecutableCount: activeEntries.Count(entry => entry.Executable),
            BlockedCount: entries.Count(entry => entry.LifecycleClass.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            ReviewCount: entries.Count(entry => entry.LifecycleClass.Equals("review", StringComparison.OrdinalIgnoreCase)),
            DoneCount: entries.Count(entry => entry.LifecycleClass.Equals("done", StringComparison.OrdinalIgnoreCase)),
            ArchivedCount: entries.Count(entry => entry.LifecycleClass.Equals("archived", StringComparison.OrdinalIgnoreCase)),
            DeletableCount: entries.Count(entry => entry.LifecycleClass.Equals("deletable", StringComparison.OrdinalIgnoreCase)),
            KeepRetentionCount: keepRetentionCount,
            Retain7dRetentionCount: retain7dRetentionCount,
            Retain30dRetentionCount: retain30dRetentionCount,
            DeletableRetentionCount: deletableRetentionCount,
            UnsupportedCount: activeEntries.Count(entry =>
                entry.Reason.Equals("unsupported_task_type", StringComparison.OrdinalIgnoreCase)
                || entry.Reason.Equals("not_allowed_task_type", StringComparison.OrdinalIgnoreCase)),
            WaitingForEvidenceCount: activeEntries.Count(entry =>
                entry.Reason.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
                || entry.Reason.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase)),
            SkippedCount: recent.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            CompletedCount: recent.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || result.Status.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase)),
            FailedCount: recent.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            LastSuccessfulExecutorRunUtc: recentTerminal.FirstOrDefault()?.CompletedAtUtc ?? recentTerminal.FirstOrDefault()?.StartedAtUtc,
            Entries: entries,
            RecommendedNextAction: entries.Any(entry => entry.Executable)
                ? "run planned-task-executor now"
                : activeEntries.Any(entry => !entry.Executable && IsRealBlockerReason(entry.Reason))
                    ? "inspect blockers"
                : activeEntries.Any(entry => IsUnsupportedReason(entry.Reason))
                        ? "inspect unsupported task types"
                        : activeEntries.Any(entry => entry.Reason.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
                            || entry.Reason.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase))
                        ? "inspect evidence backlog"
                        : activeEntries.Count == 0 && tasks.Any()
                        ? "no pending executable tasks"
                        : "inspect planner/state mismatch",
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportJsonPath, JsonSerializer.Serialize(diagnosis, JsonDefaults.WriteOptions));
        File.WriteAllText(ReportMarkdownPath, BuildMarkdown(diagnosis));
        return diagnosis;
    }

    private static string BuildMarkdown(PlannedTaskExecutorDiagnosis diagnosis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Planned Task Executor Diagnosis");
        sb.AppendLine();
        sb.AppendLine($"- Known Tasks: {diagnosis.TotalCount}");
        sb.AppendLine($"- Active: {diagnosis.ActiveCount}");
        sb.AppendLine($"- Blocked: {diagnosis.BlockedCount}");
        sb.AppendLine($"- Review: {diagnosis.ReviewCount}");
        sb.AppendLine($"- Done: {diagnosis.DoneCount}");
        sb.AppendLine($"- Archived: {diagnosis.ArchivedCount}");
        sb.AppendLine($"- Deletable: {diagnosis.DeletableCount}");
        sb.AppendLine($"- Retention Keep: {diagnosis.KeepRetentionCount}");
        sb.AppendLine($"- Retention 30d: {diagnosis.Retain30dRetentionCount}");
        sb.AppendLine($"- Retention 7d: {diagnosis.Retain7dRetentionCount}");
        sb.AppendLine($"- Retention Deletable: {diagnosis.DeletableRetentionCount}");
        sb.AppendLine($"- Pending (legacy): {diagnosis.PendingCount}");
        sb.AppendLine($"- Executable: {diagnosis.ExecutableCount}");
        sb.AppendLine($"- Unsupported: {diagnosis.UnsupportedCount}");
        sb.AppendLine($"- Waiting for evidence: {diagnosis.WaitingForEvidenceCount}");
        sb.AppendLine($"- Skipped: {diagnosis.SkippedCount}");
        sb.AppendLine($"- Completed: {diagnosis.CompletedCount}");
        sb.AppendLine($"- Failed: {diagnosis.FailedCount}");
        sb.AppendLine($"- Last successful run UTC: {diagnosis.LastSuccessfulExecutorRunUtc:O}");
        sb.AppendLine($"- Recommended next action: {diagnosis.RecommendedNextAction}");
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in diagnosis.Entries)
        {
            sb.AppendLine($"- {entry.TaskId} | {entry.TaskType} | {entry.Status} | executable={entry.Executable} | lifecycle={entry.LifecycleClass} | retention={entry.RetentionClass} | age_days={entry.AgeDays:0.##} | {entry.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("Safety: no_trading_execution=true, no_broker_action=true, no_auto_trading=true, human_review_required=true.");
        return sb.ToString();
    }

    private static string ClassifyLifecycle(PlannedTask task, double ageDays)
    {
        if (task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        if (task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            return "blocked";
        }

        if (task.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
        {
            return IsReviewLike(task) ? "review" : "blocked";
        }

        if (task.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return ageDays > 90 ? "deletable"
                : ageDays > 30 ? "archived"
                : "done";
        }

        if (task.Status.Equals("archived", StringComparison.OrdinalIgnoreCase))
        {
            return ageDays > 90 ? "deletable" : "archived";
        }

        return IsReviewLike(task) ? "review" : "blocked";
    }

    private static string ClassifyRetention(string lifecycleClass, double ageDays)
    {
        return lifecycleClass switch
        {
            "active" => "keep",
            "blocked" or "review" => ageDays > 30 ? "deletable" : "retain_7d",
            "done" or "archived" => ageDays > 90 ? "deletable" : "retain_30d",
            "deletable" => "deletable",
            _ => "retain_30d"
        };
    }

    private static bool IsReviewLike(PlannedTask task) =>
        task.Reason.Contains("review", StringComparison.OrdinalIgnoreCase)
        || task.Reason.Contains("evidence", StringComparison.OrdinalIgnoreCase)
        || task.GoalReason.Contains("review", StringComparison.OrdinalIgnoreCase)
        || task.GoalReason.Contains("evidence", StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminalStatus(string status) =>
        PlannedTaskExecutor.TerminalStatuses.Contains(status);

    private static (bool Executable, string Reason) EvaluateExecutability(PlannedTask task)
    {
        if (!PlannedTaskExecutor.IsActivePlannedTask(task))
        {
            return (false, $"task_status_{task.Status}");
        }

        if (!task.NoTradingExecution || !task.HumanReviewRequired)
        {
            return (false, "task_has_invalid_safety_flags");
        }

        if (!AutonomousTaskPlanner.AllowedTaskTypes.Contains(task.TaskType))
        {
            return (false, "not_allowed_task_type");
        }

        if (!PlannedTaskExecutor.IsSupportedTaskType(task.TaskType))
        {
            return (false, "unsupported_task_type");
        }

        if (HasExplicitBlocker(task))
        {
            return (false, HasWaitingForEvidence(task) ? "blocked_waiting_for_evidence" : "task_has_explicit_blocker");
        }

        return (true, "current_planned_task_is_executable");
    }

    private static bool HasExplicitBlocker(PlannedTask task) =>
        !task.NoTradingExecution
        || !task.HumanReviewRequired
        || task.Reason.Contains("blocked", StringComparison.OrdinalIgnoreCase)
        || task.Reason.Contains("waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
        || task.Reason.Contains("missing_evidence", StringComparison.OrdinalIgnoreCase)
        || task.GoalReason.Contains("blocked", StringComparison.OrdinalIgnoreCase);

    private static bool HasWaitingForEvidence(PlannedTask task) =>
        task.Reason.Contains("waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
        || task.Reason.Contains("missing_evidence", StringComparison.OrdinalIgnoreCase)
        || task.GoalReason.Contains("waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
        || task.GoalReason.Contains("missing_evidence", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsupportedReason(string reason) =>
        reason.Equals("unsupported_task_type", StringComparison.OrdinalIgnoreCase)
        || reason.Equals("not_allowed_task_type", StringComparison.OrdinalIgnoreCase);

    private static bool IsRealBlockerReason(string reason) =>
        !IsUnsupportedReason(reason)
        && !reason.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
        && !reason.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase);

    private static bool IsActivePlannedStatus(string status) =>
        !string.IsNullOrWhiteSpace(status)
        && !PlannedTaskExecutor.TerminalStatuses.Contains(status)
        && !status.Equals("running", StringComparison.OrdinalIgnoreCase);
}
