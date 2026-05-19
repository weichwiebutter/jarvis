namespace Hermes.Runtime;

public sealed class EventBus
{
    private readonly object _sync = new();
    private readonly List<Action<EventEnvelope>> _handlers = [];

    public void Subscribe(Action<EventEnvelope> handler)
    {
        lock (_sync)
        {
            _handlers.Add(handler);
        }
    }

    public void Publish(EventEnvelope envelope)
    {
        Action<EventEnvelope>[] handlers;

        lock (_sync)
        {
            handlers = [.. _handlers];
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(envelope);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"EventBus handler failed for {envelope.EventType}: {ex.Message}");
            }
        }
    }
}
