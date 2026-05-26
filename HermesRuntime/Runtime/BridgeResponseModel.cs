namespace Hermes.Runtime;

public sealed record BridgeResponseModel(
    string Status,
    string DataSource,
    DateTimeOffset TimestampUtc,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    object? Data,
    IReadOnlyList<string> Warnings);
