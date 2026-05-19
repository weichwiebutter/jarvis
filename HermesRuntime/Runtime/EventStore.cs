using System.Text.Json;

namespace Hermes.Runtime;

public sealed class EventStore : IDisposable
{
    private readonly JsonlLogger _logger;

    public EventStore(StoragePaths storagePaths)
    {
        var runtimeEventsDirectory = Path.Combine(storagePaths.Events, "runtime");
        Directory.CreateDirectory(runtimeEventsDirectory);

        var fileName = $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.runtime.jsonl";
        EventFilePath = Path.Combine(runtimeEventsDirectory, fileName);
        _logger = new JsonlLogger(EventFilePath);
    }

    public string EventFilePath { get; }

    public void Append(EventEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, JsonDefaults.WriteOptions);
        _logger.AppendLine(json);
    }

    public void Flush()
    {
        _logger.Flush();
    }

    public void Dispose()
    {
        _logger.Dispose();
    }
}
