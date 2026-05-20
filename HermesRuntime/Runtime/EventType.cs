namespace Hermes.Runtime;

public enum EventType
{
    RuntimeStarted,
    StorageInitialized,
    RuntimeSafeModeEnabled,
    JobCreated,
    WorkerStarted,
    WorkerHeartbeat,
    JobStarted,
    FeatureExportStarted,
    FeatureExportCompleted,
    JobCompleted,
    JobFailed,
    WorkerStopped,
    ReplayManifestCreated,
    SetupWatchCreated,
    SetupWatchUpdated,
    SetupWatchExpired,
    SnapshotCreated,
    SnapshotValidationFailed,
    RuntimeStopped
}
