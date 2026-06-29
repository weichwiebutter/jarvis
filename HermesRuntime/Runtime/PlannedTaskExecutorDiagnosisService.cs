using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PlannedTaskExecutorDiagnosis(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int PendingCount,
    int ExecutableCount,
    int BlockedCount,
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
    string Reason);

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
                return new PlannedTaskDiagnosisEntry(task.TaskId, task.TaskType, task.Status, executable, reason);
            })
            .ToList();

        var activeEntries = entries.Where(entry => IsActivePlannedStatus(entry.Status)).ToList();
        var diagnosis = new PlannedTaskExecutorDiagnosis(
            ReportVersion: "planned_task_executor_diagnosis_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PendingCount: activeEntries.Count,
            ExecutableCount: activeEntries.Count(entry => entry.Executable),
            BlockedCount: activeEntries.Count(entry =>
                !entry.Executable
                && !entry.Reason.Equals("unsupported_task_type", StringComparison.OrdinalIgnoreCase)
                && !entry.Reason.Equals("not_allowed_task_type", StringComparison.OrdinalIgnoreCase)
                && !entry.Reason.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase)
                && !entry.Reason.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase)),
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
        sb.AppendLine($"- Pending: {diagnosis.PendingCount}");
        sb.AppendLine($"- Executable: {diagnosis.ExecutableCount}");
        sb.AppendLine($"- Blocked: {diagnosis.BlockedCount}");
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
            sb.AppendLine($"- {entry.TaskId} | {entry.TaskType} | {entry.Status} | executable={entry.Executable} | {entry.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("Safety: no_trading_execution=true, no_broker_action=true, no_auto_trading=true, human_review_required=true.");
        return sb.ToString();
    }

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
