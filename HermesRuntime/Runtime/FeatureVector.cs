namespace Hermes.Runtime;

public sealed record FeatureVector(
    DateTimeOffset TimestampUtc,
    string Symbol,
    string Timeframe,
    string Session,
    string H4Regime,
    string H1Bias,
    string M15Setup,
    string M5Trigger,
    double Adx,
    double Atr,
    double Rsi,
    string StructureState,
    string PatternCandidate,
    double SignalScore,
    double Spread);
