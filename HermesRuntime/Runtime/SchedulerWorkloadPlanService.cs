using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WorkloadJobWindow(
    string JobId,
    string JobType,
    string ScheduleType,
    DateTimeOffset? NextRunUtc,
    bool Enabled,
    bool Heavy,
    bool CurrentlyRunning,
    string Status,
    string? LastAction,
    string? LastSkippedReason);

public sealed record SchedulerWorkloadPlanReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    string TimeZone,
    DateTimeOffset CurrentUtc,
    DateTimeOffset CurrentLocal,
    string CurrentTimeWindow,
    bool DayJobsEnabled,
    bool NightHeavyJobsEnabled,
    bool LearningWindowActive,
    bool HumanReviewWindowActive,
    IReadOnlyList<WorkloadJobWindow> HeavyJobsNextRun,
    IReadOnlyList<string> StaleRunningJobs,
    string ResearchInsightsStatus,
    string NightlyStatus,
    string RecommendedAction,
    bool NoAutoTrading,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly,
    string ReportPath,
    string MarkdownPath);

public sealed class SchedulerWorkloadPlanService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;

    public SchedulerWorkloadPlanService(StoragePaths storagePaths, string configPath)
    {
        _storagePaths = storagePaths;
        _configPath = configPath;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scheduler_workload");

    public string ReportPath => Path.Combine(Root, "scheduler_workload_plan.json");

    public string MarkdownPath => Path.Combine(Root, "scheduler_workload_plan.md");

    public SchedulerWorkloadPlanReport Build()
    {
        Directory.CreateDirectory(Root);
        var scheduler = new HermesInternalScheduler(_storagePaths, _configPath);
        var config = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();
        var schedulerStatus = scheduler.GetStatus();
        var nightly = new NightlyResearchService(_storagePaths, Path.Combine(_storagePaths.Root, "config", "nightly.research.json"));
        var nightlyState = nightly.LoadState();

        var heavyJobs = schedulerStatus.Jobs
            .Where(IsHeavyJob)
            .OrderBy(job => job.NextRunUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
            .Select(job => new WorkloadJobWindow(
                JobId: job.JobId,
                JobType: job.JobType,
                ScheduleType: GuessScheduleType(job),
                NextRunUtc: job.NextRunUtc,
                Enabled: job.Enabled,
                Heavy: true,
                CurrentlyRunning: job.CurrentlyRunning,
                Status: job.Status,
                LastAction: job.LastAction,
                LastSkippedReason: job.LastSkippedReason))
            .ToList();

        var staleJobs = schedulerStatus.Jobs
            .Where(job => IsStaleRunningMarker(job))
            .Select(job => $"{job.JobId}|{job.JobType}|{job.Status}|{job.LastAction ?? "-"}")
            .ToList();

        var currentWindow = DetermineCurrentTimeWindow(timeControl);
        var report = new SchedulerWorkloadPlanReport(
            ReportVersion: "scheduler_workload_plan_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            TimeZone: timeControl.TimeZone,
            CurrentUtc: timeControl.CurrentUtc,
            CurrentLocal: timeControl.CurrentLocal,
            CurrentTimeWindow: currentWindow,
            DayJobsEnabled: timeControl.InWorkWindow || timeControl.LearningWindow.ActiveNow || timeControl.HumanReviewWindow.ActiveNow,
            NightHeavyJobsEnabled: timeControl.NightlyWindow.ActiveNow,
            LearningWindowActive: timeControl.LearningWindow.ActiveNow,
            HumanReviewWindowActive: timeControl.HumanReviewWindow.ActiveNow,
            HeavyJobsNextRun: heavyJobs,
            StaleRunningJobs: staleJobs,
            ResearchInsightsStatus: FormatJobStatus(schedulerStatus.Jobs.FirstOrDefault(job => job.JobId.Equals("research_insights", StringComparison.OrdinalIgnoreCase))),
            NightlyStatus: FormatNightlyStatus(nightlyState),
            RecommendedAction: RecommendAction(timeControl, heavyJobs, staleJobs, nightlyState),
            NoAutoTrading: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report, config));
        return report;
    }

    public SchedulerWorkloadPlanReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SchedulerWorkloadPlanReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string DetermineCurrentTimeWindow(ScheduleTimeControlStatus timeControl)
    {
        if (timeControl.NightlyWindow.ActiveNow)
        {
            return "night_heavy";
        }

        if (timeControl.LearningWindow.ActiveNow)
        {
            return "learning_window";
        }

        if (timeControl.HumanReviewWindow.ActiveNow)
        {
            return "human_review";
        }

        if (timeControl.InWorkWindow)
        {
            return "day_work";
        }

        return "off";
    }

    private static bool IsHeavyJob(ScheduledJobState job) =>
        job.JobType.Equals("nightly_beta3_research", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("trading_nightly_beta3", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_nightly_work_areas", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("process_research_queue", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("walkforward_validation", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("strategy_discovery", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_walkforward_validation", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_strategy_research", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_realism_report", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_overfit_report", StringComparison.OrdinalIgnoreCase)
        || job.JobType.Equals("run_scalping_robustness_expansion", StringComparison.OrdinalIgnoreCase);

    private static string GuessScheduleType(ScheduledJobState job) =>
        job.CurrentlyRunning ? "running" : job.NextRunUtc is null ? "disabled" : "scheduled";

    private static bool IsStaleRunningMarker(ScheduledJobState job) =>
        job.LastAction?.Equals("stale_running_recovered", StringComparison.OrdinalIgnoreCase) == true
        || job.Warnings.Any(warning => warning.Equals("stale_running_without_completion", StringComparison.OrdinalIgnoreCase));

    private static string FormatJobStatus(ScheduledJobState? job)
    {
        if (job is null)
        {
            return "missing";
        }

        return $"{job.Status}; running={job.CurrentlyRunning.ToString().ToLowerInvariant()}; last_action={job.LastAction ?? "-"}; next_run={(job.NextRunUtc?.ToString("O") ?? "-")}";
    }

    private static string FormatNightlyStatus(NightlyResearchState state) =>
        $"{state.Status}; running={state.CurrentlyRunning.ToString().ToLowerInvariant()}; next_action={state.NextAction}; stop_requested={(state.StopRequestedAtUtc is not null).ToString().ToLowerInvariant()}";

    private static string RecommendAction(
        ScheduleTimeControlStatus timeControl,
        IReadOnlyList<WorkloadJobWindow> heavyJobs,
        IReadOnlyList<string> staleJobs,
        NightlyResearchState nightlyState)
    {
        if (staleJobs.Count > 0)
        {
            return "repair stale running jobs and rerun safe planner/executor windows";
        }

        if (nightlyState.Status.Equals("stop_requested", StringComparison.OrdinalIgnoreCase))
        {
            return "wait for safe nightly stop checkpoint or recover stale nightly state";
        }

        if (timeControl.NightlyWindow.ActiveNow)
        {
            return heavyJobs.Count > 0
                ? "night heavy window active; run heavy research workloads"
                : "night heavy window active; no heavy jobs configured";
        }

        if (timeControl.LearningWindow.ActiveNow)
        {
            return "learning window active; prefer validation/evidence jobs";
        }

        if (timeControl.HumanReviewWindow.ActiveNow || timeControl.InWorkWindow)
        {
            return "day window active; run light planning/evidence/health tasks";
        }

        return "wait for next safe execution window";
    }

    private static string BuildMarkdown(SchedulerWorkloadPlanReport report, ScheduleConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Scheduler Workload Plan");
        sb.AppendLine();
        sb.AppendLine($"- Current window: {report.CurrentTimeWindow}");
        sb.AppendLine($"- Day jobs enabled: {report.DayJobsEnabled.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Night heavy jobs enabled: {report.NightHeavyJobsEnabled.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Learning window active: {report.LearningWindowActive.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Human review window active: {report.HumanReviewWindowActive.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Research insights: {report.ResearchInsightsStatus}");
        sb.AppendLine($"- Nightly state: {report.NightlyStatus}");
        sb.AppendLine($"- Recommended action: {report.RecommendedAction}");
        sb.AppendLine();
        sb.AppendLine("## Heavy Jobs");
        foreach (var job in report.HeavyJobsNextRun)
        {
            sb.AppendLine($"- {job.JobId} | {job.JobType} | next_run={(job.NextRunUtc?.ToString("O") ?? "-")} | status={job.Status}");
        }

        sb.AppendLine();
        sb.AppendLine("## Stale Running Jobs");
        if (report.StaleRunningJobs.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var stale in report.StaleRunningJobs)
            {
                sb.AppendLine($"- {stale}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Safety: no_auto_trading={report.NoAutoTrading.ToString().ToLowerInvariant()}, broker_orders_enabled={report.BrokerOrdersEnabled.ToString().ToLowerInvariant()}, live_trading_enabled={report.LiveTradingEnabled.ToString().ToLowerInvariant()}, research_only={report.ResearchOnly.ToString().ToLowerInvariant()}");
        sb.AppendLine($"Config: version={config.ScheduleVersion}; timezone={config.TimeZone}; work_window={config.WorkWindow.Start}-{config.WorkWindow.End}; nightly_window={config.NightlyWindow.Start}-{config.NightlyWindow.End}");
        return sb.ToString();
    }
}
