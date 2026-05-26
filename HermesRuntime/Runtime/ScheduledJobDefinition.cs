namespace Hermes.Runtime;

public sealed record ScheduledJobDefinition(
    string JobId,
    string JobType,
    bool Enabled,
    string ScheduleType,
    string? Command = null,
    string? WindowStart = null,
    string? WindowEnd = null,
    string? DailyAt = null,
    int? EveryMinutes = null,
    int? MaxRuntimeMinutes = null,
    int? SleepSeconds = null,
    int? MaxIdleIterations = null,
    IReadOnlyDictionary<string, string>? Parameters = null);
