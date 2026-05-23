namespace Hermes.Runtime;

public sealed record ResearchProcessedRange(
    string Symbol,
    string Timeframe,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int CandleCount,
    string SourcePath);

