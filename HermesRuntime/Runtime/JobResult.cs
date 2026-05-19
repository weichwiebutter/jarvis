namespace Hermes.Runtime;

public sealed record JobResult(
    string JobId,
    JobStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? OutputPath,
    string? ErrorMessage,
    IReadOnlyDictionary<string, object?> Metrics);
