namespace Hermes.Runtime;

public sealed class StorageManager
{
    private readonly bool _safeModeOnStorageFailure;

    public StorageManager(bool safeModeOnStorageFailure)
    {
        _safeModeOnStorageFailure = safeModeOnStorageFailure;
    }

    public StorageInitializationResult Initialize(StorageProfile profile, string profileDirectory)
    {
        try
        {
            var paths = profile.ToPaths(profileDirectory);
            CreateDirectories(paths);
            return StorageInitializationResult.Ready(paths, profile.ProfileName);
        }
        catch (Exception ex) when (_safeModeOnStorageFailure)
        {
            var fallbackRoot = Path.GetFullPath(Path.Combine(profileDirectory, "..", "data", "safemode"));
            var fallbackPaths = new StoragePaths(
                fallbackRoot,
                Path.Combine(fallbackRoot, "events"),
                Path.Combine(fallbackRoot, "snapshots"),
                Path.Combine(fallbackRoot, "logs"),
                Path.Combine(fallbackRoot, "cache"),
                Path.Combine(fallbackRoot, "jobs"),
                Path.Combine(fallbackRoot, "archive"));

            CreateDirectories(fallbackPaths);

            return StorageInitializationResult.FromSafeMode(
                fallbackPaths,
                profile.ProfileName,
                $"Storage profile failed and fallback storage was used: {ex.Message}");
        }
    }

    private static void CreateDirectories(StoragePaths paths)
    {
        foreach (var directory in paths.AllDirectories)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
