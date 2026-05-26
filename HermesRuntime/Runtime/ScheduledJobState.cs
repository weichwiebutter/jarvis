namespace Hermes.Runtime;

public sealed record ScheduledJobState(
    string JobId,
    string JobType,
    bool Enabled,
    string Status,
    DateTimeOffset? LastRunUtc,
    DateTimeOffset? LastCompletedUtc,
    DateTimeOffset? NextRunUtc,
    int RunCount,
    int FailureCount,
    double LastDurationSeconds,
    bool CurrentlyRunning,
    string? LastSkippedReason,
    string? LastAction,
    string? LastReportPath,
    string? LastError,
    IReadOnlyList<string> Warnings);

public sealed record SchedulerStatus(
    DateTimeOffset UpdatedAtUtc,
    string ConfigPath,
    string StatePath,
    int CheckIntervalSeconds,
    IReadOnlyList<ScheduledJobState> Jobs,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);
