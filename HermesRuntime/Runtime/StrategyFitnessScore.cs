namespace Hermes.Runtime;

public sealed record StrategyFitnessScore(
    double Score,
    double Winrate,
    double AverageRr,
    double DrawdownPenalty,
    double StabilityBonus,
    double TradeCountFactor);

