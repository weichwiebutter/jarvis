using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PlannedTaskSchedulerLinkDiagnosis(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    bool SchedulerEnabled,
    bool PlannedTaskExecutorJobExists,
    bool PlannedTaskExecutorJobEnabled,
    DateTimeOffset? LastScheduledExecutorRunUtc,
    DateTimeOffset? LastManualExecutorRunUtc,
    int PendingTasks,
    int ExecutableTasks,
    int BlockedTasks,
    string Recommendation,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class PlannedTaskSchedulerLinkDiagnosisService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _scheduleConfigPath;

    public PlannedTaskSchedulerLinkDiagnosisService(StoragePaths storagePaths, string scheduleConfigPath)
    {
        _storagePaths = storagePaths;
        _scheduleConfigPath = scheduleConfigPath;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue");

    public string ReportJsonPath => Path.Combine(Root, "planned_task_scheduler_link_diagnosis.json");

    public string ReportMarkdownPath => Path.Combine(Root, "planned_task_scheduler_link_diagnosis.md");

    public PlannedTaskSchedulerLinkDiagnosis Build()
    {
        Directory.CreateDirectory(Root);
        var scheduler = new HermesInternalScheduler(_storagePaths, _scheduleConfigPath);
        var config = scheduler.LoadConfig();
        var schedulerStatus = scheduler.GetStatus();
        var executorStatus = new PlannedTaskExecutorDiagnosisService(_storagePaths).Build();
        var jobDefinitions = config.Jobs
            .Where(item =>
                item.JobId.Equals("planned_task_executor", StringComparison.OrdinalIgnoreCase)
                || item.JobId.Equals("process_planned_tasks_after_planning", StringComparison.OrdinalIgnoreCase)
                || item.JobType.Equals("process_planned_tasks", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var jobStates = schedulerStatus.Jobs
            .Where(item => item.JobType.Equals("process_planned_tasks", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var jobExists = jobDefinitions.Count > 0;
        var jobEnabled = jobDefinitions.Any(item => item.Enabled);
        var lastScheduledRunUtc = jobStates
            .Where(item => item.LastRunUtc is not null)
            .Select(item => item.LastRunUtc)
            .Max();
        var report = new PlannedTaskSchedulerLinkDiagnosis(
            ReportVersion: "planned_task_scheduler_link_diagnosis_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            SchedulerEnabled: schedulerStatus.Jobs.Any(item => item.Enabled),
            PlannedTaskExecutorJobExists: jobExists,
            PlannedTaskExecutorJobEnabled: jobEnabled,
            LastScheduledExecutorRunUtc: lastScheduledRunUtc,
            LastManualExecutorRunUtc: LoadLatestManualExecutorRunUtc(executorStatus),
            PendingTasks: executorStatus.PendingCount,
            ExecutableTasks: executorStatus.ExecutableCount,
            BlockedTasks: executorStatus.BlockedCount,
            Recommendation: BuildRecommendation(schedulerStatus, jobExists, jobEnabled, executorStatus),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportJsonPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(ReportMarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static DateTimeOffset? LoadLatestManualExecutorRunUtc(PlannedTaskExecutorDiagnosis diagnosis)
    {
        return diagnosis.Entries.Any(entry => entry.Executable)
            ? diagnosis.GeneratedAtUtc
            : diagnosis.LastSuccessfulExecutorRunUtc;
    }

    private static string BuildRecommendation(
        SchedulerStatus schedulerStatus,
        bool jobExists,
        bool jobEnabled,
        PlannedTaskExecutorDiagnosis executorStatus)
    {
        if (!schedulerStatus.Jobs.Any(item => item.Enabled))
        {
            return "enable scheduler";
        }

        if (!jobExists)
        {
            return "add planned_task_executor scheduler job";
        }

        if (!jobEnabled)
        {
            return "enable planned_task_executor scheduler job";
        }

        if (executorStatus.ExecutableCount > 0)
        {
            return "scheduler is linked; executor should run on next due cycle";
        }

        if (executorStatus.BlockedCount > 0)
        {
            return "inspect blockers and current planned tasks";
        }

        if (executorStatus.PendingCount > 0)
        {
            return "inspect planner/state mismatch";
        }

        return "no pending executable tasks";
    }

    private static string BuildMarkdown(PlannedTaskSchedulerLinkDiagnosis report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Planned Task Scheduler Link Diagnosis");
        sb.AppendLine();
        sb.AppendLine($"- Scheduler enabled: {report.SchedulerEnabled}");
        sb.AppendLine($"- planned_task_executor job exists: {report.PlannedTaskExecutorJobExists}");
        sb.AppendLine($"- planned_task_executor job enabled: {report.PlannedTaskExecutorJobEnabled}");
        sb.AppendLine($"- last scheduled executor run UTC: {report.LastScheduledExecutorRunUtc:O}");
        sb.AppendLine($"- last manual executor run UTC: {report.LastManualExecutorRunUtc:O}");
        sb.AppendLine($"- pending tasks: {report.PendingTasks}");
        sb.AppendLine($"- executable tasks: {report.ExecutableTasks}");
        sb.AppendLine($"- blocked tasks: {report.BlockedTasks}");
        sb.AppendLine($"- recommendation: {report.Recommendation}");
        sb.AppendLine();
        sb.AppendLine("Safety: no_trading_execution=true, no_broker_action=true, no_auto_trading=true, human_review_required=true.");
        return sb.ToString();
    }
}
