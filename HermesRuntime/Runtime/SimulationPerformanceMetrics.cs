namespace Hermes.Runtime;

public sealed record SimulationPerformanceMetrics(
    double NetR,
    double SharpeRatio,
    double ProfitFactor,
    double MaxDrawdown,
    double Expectancy,
    int ConsecutiveLosses,
    double StabilityScore,
    double Winrate,
    int TradeCount,
    double GrossProfitR = 0,
    double EstimatedCostR = 0,
    double RobustnessScore = 0);
