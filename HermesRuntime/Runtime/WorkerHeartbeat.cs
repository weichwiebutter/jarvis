namespace Hermes.Runtime;

public sealed record WorkerHeartbeat(
    string WorkerId,
    string WorkerName,
    DateTimeOffset TimestampUtc,
    string Status,
    string? CurrentJobId,
    QueueStatus QueueStatus);
