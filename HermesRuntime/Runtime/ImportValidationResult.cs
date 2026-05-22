namespace Hermes.Runtime;

public sealed record ImportValidationResult(
    bool IsValid,
    int SourceRowCount,
    int ImportedRowCount,
    int InvalidRowCount,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    IReadOnlyList<string> MissingColumns,
    IReadOnlyList<string> InvalidRows,
    IReadOnlyList<string> Warnings);
