namespace Hermes.Runtime;

public sealed record RuntimeEvent(
    string SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string Source,
    string Category,
    string Severity,
    string EventType,
    string Message,
    object Metadata,
    bool RequiresAttention);
