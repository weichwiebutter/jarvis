namespace Hermes.Runtime;

public sealed record SignalResult(
    DateTimeOffset TimestampUtc,
    string Symbol,
    string Direction,
    string SignalType,
    double Score,
    double Confidence,
    double TheoreticalEntry,
    double TheoreticalStop,
    double TheoreticalTarget,
    IReadOnlyList<string> ReasonCodes);
