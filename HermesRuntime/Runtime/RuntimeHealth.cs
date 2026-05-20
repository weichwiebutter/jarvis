namespace Hermes.Runtime;

public sealed record RuntimeHealth(
    DateTimeOffset TimestampUtc,
    string RuntimeState,
    bool SafeMode,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    double FreeDiskGb,
    int PendingJobs,
    int RunningJobs,
    int FailedJobs,
    int QuarantinedJobs,
    int ActiveSetupWatches,
    string? LastSnapshotId,
    string? LastError);
