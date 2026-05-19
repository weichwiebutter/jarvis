namespace Hermes.Runtime;

public sealed record JobManifest(
    string JobId,
    string JobType,
    int Priority,
    JobStatus Status,
    DateTimeOffset CreatedAtUtc,
    string RequestedBy,
    string ResourceProfile,
    int MaxRuntimeMinutes,
    int MaxRetries,
    int RetryCount,
    IReadOnlyDictionary<string, object?> Parameters);
