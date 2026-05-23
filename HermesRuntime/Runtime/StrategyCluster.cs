namespace Hermes.Runtime;

public sealed record StrategyCluster(
    string ClusterId,
    string Family,
    int VariantCount,
    double AverageFitness,
    double BestFitness,
    double AverageWinrate,
    double AverageTradeCount,
    IReadOnlyList<string> CommonParameters,
    bool Prioritized,
    bool Reduced);

