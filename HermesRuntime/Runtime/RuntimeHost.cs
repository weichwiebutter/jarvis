namespace Hermes.Runtime;

public sealed class RuntimeHost
{
    private const string RuntimeSource = "hermes_minimal_runtime";
    private const string RuntimeVersion = "1.0.0-sprint6";

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
        var snapshotManager = new SnapshotManager(storage.Paths);
        var queueManager = new QueueManager(storage.Paths);

        var snapshotLoadResult = snapshotManager.LoadLastValidSnapshot();
        foreach (var failure in snapshotLoadResult.ValidationFailures)
        {
            PublishSnapshotValidationFailed(eventBus, failure);
        }

        PublishRuntimeStarted(eventBus, state, diskSpaceCheck);
        PublishStorageInitialized(eventBus, state, storage.Paths, eventStore.EventFilePath);
        var demoJob = CreateDemoFeatureExportJobIfMissing(queueManager);
        if (demoJob is not null)
        {
            PublishJobCreated(eventBus, demoJob, queueManager.Status);
        }

        if (state.SafeMode)
        {
            PublishRuntimeSafeModeEnabled(eventBus, state, diskSpaceCheck);
        }

        var workerHost = new WorkerHost(storage.Paths, queueManager, eventBus, RuntimeVersion);
        workerHost.RunOnce();

        var replayManifestService = new ReplayManifestService(storage.Paths, eventBus, RuntimeVersion);
        var replayManifestResult = replayManifestService.CreateDemoReplayManifest();
        var setupWatchService = new SetupWatchService(storage.Paths, eventBus, RuntimeVersion);
        var setupWatchResult = setupWatchService.CreateDemoSetupWatches();

        state.IsRunning = false;
        state.StoppedAtUtc = DateTimeOffset.UtcNow;
        var snapshotResult = snapshotManager.WriteRuntimeSnapshot(
            state,
            diskSpaceCheck,
            RuntimeVersion,
            config.Environment,
            queueManager.Status,
            eventBus.LastPublishedEventId);

        state.LastSnapshotPath = snapshotResult.SnapshotPath;
        PublishSnapshotCreated(eventBus, snapshotResult);

        if (!snapshotResult.Validation.IsValid)
        {
            PublishSnapshotValidationFailed(eventBus, snapshotResult.Validation);
        }

        var healthService = new RuntimeHealthService(storage.Paths);
        var healthResult = healthService.WriteHealth(
            state,
            diskSpaceCheck,
            queueManager.Status,
            snapshotResult,
            setupWatchResult.ActiveSetupWatches,
            snapshotResult.Validation.IsValid ? null : snapshotResult.Validation.Error);

        PublishRuntimeStopped(eventBus, state, diskSpaceCheck);
        eventStore.Flush();

        Console.WriteLine("Hermes Runtime completed.");
        Console.WriteLine($"Storage: {state.StorageRoot}");
        Console.WriteLine($"Events: {eventStore.EventFilePath}");
        Console.WriteLine($"Snapshot: {state.LastSnapshotPath}");
        Console.WriteLine($"ReplayManifest: {replayManifestResult.ManifestPath}");
        Console.WriteLine($"SetupWatch: {setupWatchResult.OutputPath}");
        Console.WriteLine($"Health: {healthResult.ReportPath}");
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
                paths.Jobs,
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

    private static JobManifest? CreateDemoFeatureExportJobIfMissing(QueueManager queueManager)
    {
        if (queueManager.GetJobs(JobStatus.Pending).Any(job => job.JobType == FeatureExportWorker.FeatureExportJobType))
        {
            return null;
        }

        var createdAtUtc = DateTimeOffset.UtcNow;
        var manifest = new JobManifest(
            JobId: $"job_feature_export_demo_{createdAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            JobType: FeatureExportWorker.FeatureExportJobType,
            Priority: 10,
            Status: JobStatus.Pending,
            CreatedAtUtc: createdAtUtc,
            RequestedBy: "hermes_runtime_sprint6",
            ResourceProfile: "local_minimal",
            MaxRuntimeMinutes: 5,
            MaxRetries: 0,
            RetryCount: 0,
            Parameters: new Dictionary<string, object?>
            {
                ["demo"] = true,
                ["symbol"] = "DEMO_FEATURE_EXPORT",
                ["source"] = "stub",
                ["note"] = "Sprint 5 demo feature export job. Uses stub data only."
            });

        return queueManager.Enqueue(manifest);
    }

    private static void PublishJobCreated(
        EventBus eventBus,
        JobManifest job,
        QueueStatus queueStatus)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.JobCreated,
            RuntimeSource,
            EventSeverity.Info,
            RuntimeVersion,
            new
            {
                message = "Queue job created.",
                job.JobId,
                job.JobType,
                job.Priority,
                job.Status,
                job.CreatedAtUtc,
                job.RequestedBy,
                job.ResourceProfile,
                job.MaxRuntimeMinutes,
                job.MaxRetries,
                job.RetryCount,
                queueStatus
            }));
    }

    private static void PublishSnapshotCreated(EventBus eventBus, SnapshotWriteResult snapshotResult)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.SnapshotCreated,
            RuntimeSource,
            snapshotResult.Validation.IsValid ? EventSeverity.Info : EventSeverity.Warning,
            RuntimeVersion,
            new
            {
                message = "Runtime snapshot created.",
                snapshotResult.Snapshot.SnapshotId,
                snapshotResult.Snapshot.CreatedAtUtc,
                snapshotResult.Snapshot.RuntimeVersion,
                snapshotResult.Snapshot.RuntimeMode,
                snapshotResult.Snapshot.QueueStatus,
                snapshotResult.Snapshot.LastEventId,
                snapshotResult.Snapshot.Sha256Hash,
                snapshotResult.SnapshotPath,
                snapshotResult.ManifestPath,
                validationStatus = snapshotResult.Validation.IsValid ? "valid" : "failed",
                validationError = snapshotResult.Validation.Error
            }));
    }

    private static void PublishSnapshotValidationFailed(
        EventBus eventBus,
        SnapshotValidationResult validation)
    {
        eventBus.Publish(EventEnvelope.Create(
            EventType.SnapshotValidationFailed,
            RuntimeSource,
            EventSeverity.Warning,
            RuntimeVersion,
            new
            {
                message = "Snapshot validation failed.",
                validation.Error,
                SnapshotId = validation.Manifest?.SnapshotId,
                SnapshotPath = validation.Manifest?.SnapshotPath,
                ManifestSha256Hash = validation.Manifest?.Sha256Hash
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
