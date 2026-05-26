namespace Hermes.Runtime;

public sealed record BridgeHealthSnapshot(
    string Status,
    string BridgeVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset TimestampUtc,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    int ReportsConfigured,
    int ReportsAvailable,
    IReadOnlyList<string> Endpoints);
