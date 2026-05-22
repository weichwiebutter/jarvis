namespace Hermes.Runtime;

public sealed record GeneratedFeatureVector(
    DateTimeOffset TimestampUtc,
    string Symbol,
    string Timeframe,
    double Close,
    double SimpleReturn,
    double CandleRange,
    double BodySize,
    string Direction,
    string MockSession,
    string MockRegime,
    double MockSignalScore);
