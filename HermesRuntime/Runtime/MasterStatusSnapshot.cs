namespace Hermes.Runtime;

public sealed record MasterStatusSnapshot(
    string SnapshotVersion,
    DateTimeOffset LastUpdatedUtc,
    string DataRoot,
    string OverallStatus,
    string CurrentFocus,
    IReadOnlyList<string> ActiveDomains,
    MasterStatusSection CognitiveStatus,
    MasterStatusSection ResearchQueueStatus,
    MasterStatusSection AutonomousLoopStatus,
    MasterStatusSection NightlyStatus,
    MasterStatusSection SchedulerStatus,
    MasterStatusSection SupervisorStatus,
    MasterStatusSection ResourceStatus,
    MasterStatusSection StorageStatus,
    MasterStatusSection TradingDomainStatus,
    MasterStatusSafetyFlags SafetyFlags,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> TopBlockers,
    IReadOnlyList<string> NextRecommendedActions,
    int QueuedTasks,
    string? LastNightlyRun,
    string? LastAutonomousLoop,
    string? LastMetaReview,
    string LearningStrategy,
    bool SupervisorRunning,
    int SchedulerEnabled,
    string ResourceAction,
    int StorageCleanup,
    int RobustStrategies,
    int DemoBotCandidates,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record MasterStatusSection(
    string Status,
    string? ReportPath,
    IReadOnlyDictionary<string, object?> Metrics,
    IReadOnlyList<string> Warnings);

public sealed record MasterStatusSafetyFlags(
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool NoBrokerOrders,
    bool NoTradingExecution);
