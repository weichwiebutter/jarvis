namespace Hermes.Runtime;

public enum EventType
{
    RuntimeStarted,
    StorageInitialized,
    RuntimeSafeModeEnabled,
    JobCreated,
    SnapshotCreated,
    SnapshotValidationFailed,
    RuntimeStopped
}
