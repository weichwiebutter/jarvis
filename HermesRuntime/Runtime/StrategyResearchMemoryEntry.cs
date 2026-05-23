namespace Hermes.Runtime;

public sealed record StrategyResearchMemoryEntry(
    string PatternId,
    string StrategyVariantId,
    string Symbol,
    string Timeframe,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    double FitnessScore,
    string Status);
