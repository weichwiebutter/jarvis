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
    SignalResultExported,
    BacktestStarted,
    BacktestCompleted,
    OutcomeEvaluationStarted,
    OutcomeEvaluationCompleted,
    HistoricalImportStarted,
    HistoricalImportCompleted,
    FeatureGenerationStarted,
    FeatureGenerationCompleted,
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
