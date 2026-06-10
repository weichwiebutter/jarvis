namespace Hermes.Runtime;

public sealed record CleanupCandidate(
    string Path,
    string Reason,
    long EstimatedBytes,
    bool SafeToDelete);

public sealed record StoragePolicyStatus(
    string PolicyVersion,
    bool AutoCleanupPolicyEnabled,
    bool AutoCleanupAllowed,
    string SafetyMode,
    double DiskUsagePercent,
    double FreeDiskPercent,
    string PolicyAction,
    DateTimeOffset? AutoCleanupLastRun,
    string AutoCleanupLastResult,
    int CleanupCandidates,
    long EstimatedFreeBytes,
    int ProtectedPathsCount,
    IReadOnlyList<string> Warnings);

public sealed record CleanupPlan(
    string PlanId,
    DateTimeOffset CreatedAtUtc,
    string StorageRoot,
    IReadOnlyList<string> ProtectedPaths,
    IReadOnlyList<CleanupCandidate> Candidates,
    long EstimatedBytesToFree,
    StoragePolicyStatus PolicyStatus,
    bool SafeToApply,
    bool NoAutoTrading,
    bool HumanReviewRequired);
