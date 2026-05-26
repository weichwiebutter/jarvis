namespace Hermes.Runtime;

public sealed record RegimeContext(
    DateTimeOffset TimestampUtc,
    string Symbol,
    string Timeframe,
    string RegimeType,
    string Session,
    double AtrProxy,
    double RangeRatio,
    double BodyRatio,
    double TrendSlope,
    double MomentumPersistence,
    double BreakoutFrequency,
    double VolatilityCompression,
    double Confidence);
