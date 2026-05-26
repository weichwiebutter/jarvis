namespace Hermes.Runtime;

public sealed record ReportIndex(
    DateTimeOffset TimestampUtc,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    IReadOnlyList<ReportIndexItem> Reports);

public sealed record ReportIndexItem(
    string Key,
    string Label,
    string Endpoint,
    bool Available,
    DateTimeOffset? UpdatedAtUtc,
    long? SizeBytes);
