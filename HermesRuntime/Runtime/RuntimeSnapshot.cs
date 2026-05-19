using System.Text.Json.Serialization;

namespace Hermes.Runtime;

public sealed record RuntimeSnapshot(
    string SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string RuntimeVersion,
    string RuntimeMode,
    RuntimeState State,
    SnapshotHealth Health,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    QueueStatus? QueueStatus,
    string? LastEventId,
    string? Sha256Hash);
