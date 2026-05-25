namespace Hermes.Runtime;

public sealed record CleanupCandidate(
    string Path,
    string Reason,
    long EstimatedBytes,
    bool SafeToDelete);

public sealed record CleanupPlan(
    string PlanId,
    DateTimeOffset CreatedAtUtc,
    string StorageRoot,
    IReadOnlyList<string> ProtectedPaths,
    IReadOnlyList<CleanupCandidate> Candidates,
    long EstimatedBytesToFree,
    bool SafeToApply,
    bool NoAutoTrading,
    bool HumanReviewRequired);
