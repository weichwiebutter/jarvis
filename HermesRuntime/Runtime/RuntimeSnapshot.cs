namespace Hermes.Runtime;

public sealed record RuntimeSnapshot(
    string SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string RuntimeVersion,
    string RuntimeMode,
    RuntimeState State,
    SnapshotHealth Health,
    string? LastEventId,
    string? Sha256Hash);
