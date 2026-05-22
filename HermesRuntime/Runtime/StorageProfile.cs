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

    public string JobsDirectory { get; init; } = "jobs";

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
        var normalizedRootPath = NormalizeRootPath(RootPath);
        var root = Path.IsPathRooted(normalizedRootPath)
            ? normalizedRootPath
            : Path.Combine(profileDirectory, normalizedRootPath);

        root = Path.GetFullPath(root);

        return new StoragePaths(
            root,
            Path.Combine(root, EventsDirectory),
            Path.Combine(root, SnapshotsDirectory),
            Path.Combine(root, LogsDirectory),
            Path.Combine(root, CacheDirectory),
            Path.Combine(root, JobsDirectory),
            Path.Combine(root, ArchiveDirectory));
    }

    private static string NormalizeRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return "../data";
        }

        if (!OperatingSystem.IsWindows()
            && rootPath.Length >= 3
            && char.IsLetter(rootPath[0])
            && rootPath[1] == ':'
            && (rootPath[2] == '/' || rootPath[2] == '\\'))
        {
            var drive = char.ToLowerInvariant(rootPath[0]);
            var remainder = rootPath[3..].Replace('\\', '/').TrimStart('/');
            return string.IsNullOrWhiteSpace(remainder)
                ? $"/mnt/{drive}"
                : $"/mnt/{drive}/{remainder}";
        }

        return rootPath;
    }
}
