namespace Hermes.Runtime;

public sealed record CleanupReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    string PlanId,
    int FilesDeleted,
    long BytesFreed,
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<string> SkippedPaths,
    bool SafeMode,
    bool NoAutoTrading,
    bool HumanReviewRequired);
