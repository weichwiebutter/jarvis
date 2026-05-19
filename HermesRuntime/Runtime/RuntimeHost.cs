using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RuntimeHost
{
    private readonly string _configPath;

    public RuntimeHost(string configPath)
    {
        _configPath = Path.GetFullPath(configPath);
    }

    public void Run()
    {
        var configDirectory = Path.GetDirectoryName(_configPath)
            ?? throw new InvalidOperationException("Runtime config directory could not be resolved.");

        var config = RuntimeConfig.Load(_configPath);
        var profilePath = ResolveProfilePath(config.StorageProfilePath, configDirectory);
        var profileLoadWarning = default(string);
        var profile = LoadStorageProfile(profilePath, config.SafeModeOnStorageFailure, out profileLoadWarning);

        var storageManager = new StorageManager(config.SafeModeOnStorageFailure);
        var storage = storageManager.Initialize(profile, Path.GetDirectoryName(profilePath)!);

        var state = new RuntimeState
        {
            RuntimeName = config.RuntimeName,
            Environment = config.Environment,
            StorageProfile = storage.ProfileName,
            StorageRoot = storage.Paths.Root,
            StartedAtUtc = DateTimeOffset.UtcNow,
            IsRunning = true,
            SafeMode = storage.SafeMode || profileLoadWarning is not null,
            SafeModeReason = profileLoadWarning ?? storage.SafeModeReason
        };

        var diskSpaceCheck = new DiskSpaceGuard().Check(storage.Paths, profile.MinimumFreeDiskMb);
        if (!diskSpaceCheck.IsOk)
        {
            state.SafeMode = true;
            state.DiskSpaceWarning = diskSpaceCheck.Warning;
            state.SafeModeReason ??= diskSpaceCheck.Warning;
        }

        var logger = new JsonlLogger(storage.Paths.Events);
        logger.Append(CreateEvent("runtime_started", "Runtime started.", state, diskSpaceCheck));

        state.LastSnapshotPath = WriteSnapshot(storage.Paths, config.SnapshotFileName, state, diskSpaceCheck);

        state.IsRunning = false;
        state.StoppedAtUtc = DateTimeOffset.UtcNow;
        logger.Append(CreateEvent("runtime_stopped", "Runtime stopped cleanly.", state, diskSpaceCheck));

        Console.WriteLine("Hermes Runtime completed.");
        Console.WriteLine($"Storage: {state.StorageRoot}");
        Console.WriteLine($"Snapshot: {state.LastSnapshotPath}");
        Console.WriteLine($"SafeMode: {state.SafeMode}");
    }

    private static string ResolveProfilePath(string configuredPath, string configDirectory)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(configDirectory, configuredPath));
    }

    private static StorageProfile LoadStorageProfile(
        string profilePath,
        bool safeModeOnStorageFailure,
        out string? profileLoadWarning)
    {
        try
        {
            profileLoadWarning = null;
            return StorageProfile.Load(profilePath);
        }
        catch (Exception ex) when (safeModeOnStorageFailure)
        {
            profileLoadWarning = $"Storage profile failed to load; safe-mode defaults were used: {ex.Message}";
            return new StorageProfile
            {
                ProfileName = "safe-mode-default",
                RootPath = "../data/safemode"
            };
        }
    }

    private static string WriteSnapshot(
        StoragePaths paths,
        string snapshotFileName,
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck)
    {
        Directory.CreateDirectory(paths.Snapshots);

        var snapshot = new
        {
            schema_version = "hermes.runtime_snapshot.v1",
            generated_at = DateTimeOffset.UtcNow,
            runtime = state,
            disk = diskSpaceCheck,
            storage = new
            {
                paths.Root,
                paths.Events,
                paths.Snapshots,
                paths.Logs,
                paths.Cache,
                paths.Archive
            }
        };

        var path = Path.Combine(paths.Snapshots, snapshotFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));

        return path;
    }

    private static RuntimeEvent CreateEvent(
        string eventType,
        string message,
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck)
    {
        return new RuntimeEvent(
            SchemaVersion: "hermes.runtime_event.v1",
            EventId: $"evt_{eventType}_{Guid.NewGuid():N}",
            Timestamp: DateTimeOffset.UtcNow,
            Source: "hermes_minimal_runtime",
            Category: "runtime",
            Severity: state.SafeMode ? "warning" : "info",
            EventType: eventType,
            Message: message,
            Metadata: new
            {
                state.RuntimeName,
                state.Environment,
                state.StorageProfile,
                state.StorageRoot,
                state.SafeMode,
                state.SafeModeReason,
                state.DiskSpaceWarning,
                diskSpaceCheck.FreeMb,
                diskSpaceCheck.MinimumFreeMb
            },
            RequiresAttention: state.SafeMode);
    }
}
