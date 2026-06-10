namespace Hermes.Runtime;

public sealed record CleanupReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    string PlanId,
    int FilesDeleted,
    long BytesFreed,
    int UnsafeCandidatesSkipped,
    int ProtectedCandidatesSkipped,
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<string> SkippedPaths,
    string AuditLogPath,
    bool SafeMode,
    bool NoAutoTrading,
    bool HumanReviewRequired);
