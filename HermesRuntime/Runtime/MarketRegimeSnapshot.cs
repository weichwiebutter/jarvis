namespace Hermes.Runtime;

public sealed record MarketRegimeSnapshot(
    string SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string Symbol,
    string Timeframe,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string RegimeType,
    string Session,
    int CandleCount,
    double AverageAtrProxy,
    double AverageRangeRatio,
    double AverageBodyRatio,
    double TrendSlope,
    double MomentumPersistence,
    double BreakoutFrequency,
    double VolatilityCompression,
    double Confidence,
    bool NoAutoTrading,
    bool HumanReviewRequired);
