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
    SnapshotCreated,
    SnapshotValidationFailed,
    RuntimeStopped
}
