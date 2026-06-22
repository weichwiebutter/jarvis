using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PlannedTaskExecutorDiagnosis(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int PendingCount,
    int ExecutableCount,
    int BlockedCount,
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
                var executable = AutonomousTaskPlanner.AllowedTaskTypes.Contains(task.TaskType)
                    && task.Status is not ("completed" or "completed_with_missing_evidence" or "blocked_waiting_for_evidence" or "skipped" or "failed" or "running");
                var reason = executable
                    ? "current_planned_task_is_executable"
                    : task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)
                        ? "task_is_running"
                        : $"task_status_{task.Status}";
                return new PlannedTaskDiagnosisEntry(task.TaskId, task.TaskType, task.Status, executable, reason);
            })
            .ToList();

        var diagnosis = new PlannedTaskExecutorDiagnosis(
            ReportVersion: "planned_task_executor_diagnosis_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PendingCount: tasks.Count(task => !PlannedTaskExecutor.TerminalStatuses.Contains(task.Status) && !task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            ExecutableCount: entries.Count(entry => entry.Executable),
            BlockedCount: entries.Count(entry => !entry.Executable && !entry.Status.Equals("running", StringComparison.OrdinalIgnoreCase)),
            SkippedCount: recent.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            CompletedCount: recent.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || result.Status.Equals("completed_with_missing_evidence", StringComparison.OrdinalIgnoreCase)),
            FailedCount: recent.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
            LastSuccessfulExecutorRunUtc: recentTerminal.FirstOrDefault()?.CompletedAtUtc ?? recentTerminal.FirstOrDefault()?.StartedAtUtc,
            Entries: entries,
            RecommendedNextAction: entries.Any(entry => entry.Executable)
                ? "run planned-task-executor now"
                : "inspect blocker reasons and refresh planning if needed",
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
}
