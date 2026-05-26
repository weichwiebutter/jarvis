namespace Hermes.Runtime;

public sealed record SupervisorHeartbeat(
    DateTimeOffset TimestampUtc,
    string SupervisorId,
    int ProcessId,
    string Status,
    int IterationsCompleted,
    string? CurrentJobId,
    string ResourceAction,
    string StorageAction,
    string NextAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
