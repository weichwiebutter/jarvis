namespace Hermes.Runtime;

public enum EventType
{
    RuntimeStarted,
    StorageInitialized,
    RuntimeSafeModeEnabled,
    SnapshotCreated,
    SnapshotValidationFailed,
    RuntimeStopped
}
