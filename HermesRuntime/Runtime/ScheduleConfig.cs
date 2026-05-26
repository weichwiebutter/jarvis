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
                JobId: "nightly_beta3_research",
                JobType: "nightly_beta3_research",
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
                DailyAt: "05:30"),
            new(
                JobId: "health_snapshot",
                JobType: "health_snapshot",
                Enabled: true,
                ScheduleType: "interval",
                EveryMinutes: 60),
            new(
                JobId: "market_data_refresh",
                JobType: "market_data_refresh",
                Enabled: false,
                ScheduleType: "daily",
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
