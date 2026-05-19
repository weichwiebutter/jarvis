using System.Text.Json;

namespace Hermes.Runtime;

public sealed class StorageProfile
{
    public string ProfileName { get; init; } = "local-default";

    public string RootPath { get; init; } = "../data";

    public string EventsDirectory { get; init; } = "events";

    public string SnapshotsDirectory { get; init; } = "snapshots";

    public string LogsDirectory { get; init; } = "logs";

    public string CacheDirectory { get; init; } = "cache";

    public string ArchiveDirectory { get; init; } = "archive";

    public long MinimumFreeDiskMb { get; init; } = 512;

    public static StorageProfile Load(string profilePath)
    {
        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<StorageProfile>(json, JsonDefaults.ReadOptions);

        if (profile is null)
        {
            throw new InvalidOperationException($"Storage profile is empty or invalid: {profilePath}");
        }

        return profile;
    }

    public StoragePaths ToPaths(string profileDirectory)
    {
        var root = Path.IsPathRooted(RootPath)
            ? RootPath
            : Path.Combine(profileDirectory, RootPath);

        root = Path.GetFullPath(root);

        return new StoragePaths(
            root,
            Path.Combine(root, EventsDirectory),
            Path.Combine(root, SnapshotsDirectory),
            Path.Combine(root, LogsDirectory),
            Path.Combine(root, CacheDirectory),
            Path.Combine(root, ArchiveDirectory));
    }
}
