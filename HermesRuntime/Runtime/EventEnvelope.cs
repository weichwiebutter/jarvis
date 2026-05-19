namespace Hermes.Runtime;

public sealed record EventEnvelope(
    string EventId,
    DateTimeOffset TimestampUtc,
    EventType EventType,
    string Source,
    EventSeverity Severity,
    string? CorrelationId,
    string RuntimeVersion,
    object Payload)
{
    public static EventEnvelope Create(
        EventType eventType,
        string source,
        EventSeverity severity,
        string runtimeVersion,
        object payload,
        string? correlationId = null)
    {
        return new EventEnvelope(
            EventId: $"evt_{eventType.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}",
            TimestampUtc: DateTimeOffset.UtcNow,
            EventType: eventType,
            Source: source,
            Severity: severity,
            CorrelationId: correlationId,
            RuntimeVersion: runtimeVersion,
            Payload: payload);
    }
}
