namespace Hermes.Runtime;

public sealed record StorageStatusSnapshot(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    string StorageRoot,
    double FreeDiskPercent,
    double DiskUsagePercent,
    int CleanupCandidates,
    long EstimatedFreeBytes,
    int ProtectedPathsCount,
    bool AutoCleanupPolicyEnabled,
    bool AutoCleanupAllowed,
    DateTimeOffset? AutoCleanupLastRun,
    string AutoCleanupLastResult,
    string SafetyMode,
    string PolicyAction,
    IReadOnlyList<string> Warnings,
    bool SafeToApply,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);
