using System.Text.Json;

namespace Hermes.Runtime;

public sealed class JsonlLogger
{
    private readonly string _eventsDirectory;

    public JsonlLogger(string eventsDirectory)
    {
        _eventsDirectory = eventsDirectory;
    }

    public string Append(RuntimeEvent runtimeEvent)
    {
        Directory.CreateDirectory(_eventsDirectory);

        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd}.runtime.events.jsonl";
        var path = Path.Combine(_eventsDirectory, fileName);
        var json = JsonSerializer.Serialize(runtimeEvent, JsonDefaults.WriteOptions);

        File.AppendAllText(path, json + Environment.NewLine);

        return path;
    }
}
