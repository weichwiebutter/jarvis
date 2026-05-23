namespace Hermes.Runtime;

public sealed record StrategyResearchResult(
    string ResultId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    StrategyVariant Variant,
    StrategyFitnessScore Fitness,
    int TradeCount,
    int WinCount,
    int LossCount,
    double AverageR,
    double MaxDrawdown,
    IReadOnlyList<string> SymbolsProcessed,
    IReadOnlyList<string> TimeframesProcessed,
    string Status,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);

