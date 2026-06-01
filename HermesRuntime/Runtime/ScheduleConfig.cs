using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScheduleConfig(
    string ScheduleVersion,
    int CheckIntervalSeconds,
    IReadOnlyList<ScheduledJobDefinition> Jobs)
{
    public static ScheduleConfig Default => new(
        ScheduleVersion: "schedules_v1",
        CheckIntervalSeconds: 60,
        Jobs:
        [
            new(
                JobId: "planning_cycle_hourly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "interval",
                Command: "run-planning-cycle",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "planning_cycle_before_nightly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "daily",
                Command: "run-planning-cycle",
                DailyAt: "22:45",
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "nightly_beta3_research",
                JobType: "trading_nightly_beta3",
                Enabled: true,
                ScheduleType: "window",
                Command: "run-nightly-beta3",
                WindowStart: "23:00",
                WindowEnd: "05:00",
                MaxRuntimeMinutes: 360,
                SleepSeconds: 60,
                MaxIdleIterations: 10),
            new(
                JobId: "storage_hygiene",
                JobType: "storage_hygiene",
                Enabled: true,
                ScheduleType: "daily",
                DailyAt: "05:15"),
            new(
                JobId: "research_insights",
                JobType: "research_insights",
                Enabled: true,
                ScheduleType: "daily",
                Command: "research-insights",
                DailyAt: "05:30"),
            new(
                JobId: "scan_knowledge_sources",
                JobType: "scan_knowledge_sources",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-knowledge-sources",
                DailyAt: "05:40"),
            new(
                JobId: "process_research_queue",
                JobType: "process_research_queue",
                Enabled: true,
                ScheduleType: "window",
                Command: "process-research-queue",
                WindowStart: "23:00",
                WindowEnd: "05:00",
                MaxRuntimeMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "50" }),
            new(
                JobId: "generate_cognitive_insights",
                JobType: "generate_cognitive_insights",
                Enabled: true,
                ScheduleType: "daily",
                Command: "generate-hypotheses",
                DailyAt: "05:45",
                Parameters: new Dictionary<string, string> { ["domain"] = "trading" }),
            new(
                JobId: "planning_cycle_after_nightly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "daily",
                Command: "run-planning-cycle",
                DailyAt: "05:55",
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "process_planned_tasks_after_planning",
                JobType: "process_planned_tasks",
                Enabled: true,
                ScheduleType: "interval",
                Command: "execute-planned-tasks",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "10" }),
            new(
                JobId: "health_snapshot",
                JobType: "health_snapshot",
                Enabled: true,
                ScheduleType: "interval",
                Command: "resource-status",
                EveryMinutes: 60),
            new(
                JobId: "market_data_refresh",
                JobType: "market_data_refresh",
                Enabled: false,
                ScheduleType: "daily",
                Command: "download-history",
                DailyAt: "22:30")
        ]);

    public static ScheduleConfig LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduleConfig>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Default;
        }
    }
}
