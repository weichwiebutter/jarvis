namespace Hermes.Runtime;

public sealed record HermesSupervisorState(
    string StateVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string SupervisorId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? DeadlineUtc,
    DateTimeOffset? StoppedAtUtc,
    int? ProcessId,
    DateTimeOffset? HeartbeatUtc,
    int IterationsCompleted,
    int JobsStarted,
    int JobsCompleted,
    int JobsSkipped,
    string? CurrentJobId,
    string? LastJobId,
    string? LastError,
    string NextAction,
    DateTimeOffset? StopRequestedAtUtc,
    bool CurrentlyRunning,
    bool NoAutoTrading,
    bool HumanReviewRequired);
