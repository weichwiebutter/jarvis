using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RuntimeConfig
{
    public string RuntimeName { get; init; } = "Hermes Minimal Runtime";

    public string Environment { get; init; } = "local";

    public string StorageProfilePath { get; init; } = "storage.profile.json";

    public bool SafeModeOnStorageFailure { get; init; } = true;

    public string SnapshotFileName { get; init; } = "runtime_snapshot.json";

    public static RuntimeConfig Load(string configPath)
    {
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<RuntimeConfig>(json, JsonDefaults.ReadOptions);

        if (config is null)
        {
            throw new InvalidOperationException($"Runtime config is empty or invalid: {configPath}");
        }

        return config;
    }
}
