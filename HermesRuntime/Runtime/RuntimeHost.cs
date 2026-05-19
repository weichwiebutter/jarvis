using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RuntimeHost
{
    private const string RuntimeSource = "hermes_minimal_runtime";
    private const string RuntimeVersion = "1.0.0-sprint2";

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

        using var eventStore = new EventStore(storage.Paths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        PublishRuntimeStarted(eventBus, state, diskSpaceCheck);
        PublishStorageInitialized(eventBus, state, storage.Paths, eventStore.EventFilePath);

        if (state.SafeMode)
        {
            PublishRuntimeSafeModeEnabled(eventBus, state, diskSpaceCheck);
        }

        state.LastSnapshotPath = WriteSnapshot(storage.Paths, config.SnapshotFileName, state, diskSpaceCheck);

        state.IsRunning = false;
        state.StoppedAtUtc = DateTimeOffset.UtcNow;
        PublishRuntimeStopped(eventBus, state, diskSpaceCheck);
        eventStore.Flush();

        Console.WriteLine("Hermes Runtime completed.");
        Console.WriteLine($"Storage: {state.StorageRoot}");
        Console.WriteLine($"Events: {eventStore.EventFilePath}");
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

    private static void PublishRuntimeStarted(
        EventBus eventBus,
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.RuntimeStarted,
            RuntimeSource,
            state.SafeMode ? EventSeverity.Warning : EventSeverity.Info,
            RuntimeVersion,
            new
            {
                message = "Runtime started.",
                state.RuntimeName,
                state.Environment,
                state.StorageProfile,
                state.StorageRoot,
                state.SafeMode,
                state.SafeModeReason,
                state.DiskSpaceWarning,
                diskSpaceCheck.FreeMb,
                diskSpaceCheck.MinimumFreeMb
            }));
    }

    private static void PublishStorageInitialized(
        EventBus eventBus,
        RuntimeState state,
        StoragePaths paths,
        string eventFilePath)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.StorageInitialized,
            RuntimeSource,
            state.SafeMode ? EventSeverity.Warning : EventSeverity.Info,
            RuntimeVersion,
            new
            {
                message = "Storage initialized.",
                state.StorageProfile,
                state.StorageRoot,
                paths.Events,
                paths.Snapshots,
                paths.Logs,
                paths.Cache,
                paths.Archive,
                eventFilePath,
                state.SafeMode,
                state.SafeModeReason
            }));
    }

    private static void PublishRuntimeSafeModeEnabled(
        EventBus eventBus,
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.RuntimeSafeModeEnabled,
            RuntimeSource,
            EventSeverity.Warning,
            RuntimeVersion,
            new
            {
                message = "Runtime safe mode enabled.",
                state.SafeModeReason,
                state.DiskSpaceWarning,
                diskSpaceCheck.FreeMb,
                diskSpaceCheck.MinimumFreeMb
            }));
    }

    private static void PublishRuntimeStopped(
        EventBus eventBus,
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.RuntimeStopped,
            RuntimeSource,
            state.SafeMode ? EventSeverity.Warning : EventSeverity.Info,
            RuntimeVersion,
            new
            {
                message = "Runtime stopped cleanly.",
                state.RuntimeName,
                state.Environment,
                state.StorageProfile,
                state.StorageRoot,
                state.StartedAtUtc,
                state.StoppedAtUtc,
                state.SafeMode,
                state.SafeModeReason,
                state.LastSnapshotPath,
                diskSpaceCheck.FreeMb,
                diskSpaceCheck.MinimumFreeMb
            }));
    }
}
