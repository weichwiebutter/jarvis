namespace Hermes.Runtime;

public sealed record MonteCarloResult(
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    string Symbol,
    string Timeframe,
    MonteCarloScenario Scenario,
    int SimulationsRun,
    double PositiveSimulationRatio,
    double MedianReturn,
    double WorstCaseDrawdown,
    double RuinProbabilityEstimate,
    bool MonteCarloPassed,
    IReadOnlyList<string> Warnings);

public sealed record MonteCarloReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int SimulationsPerStrategy,
    int Passed,
    int Failed,
    double AveragePositiveSimulationRatio,
    double AverageRuinProbabilityEstimate,
    IReadOnlyList<MonteCarloResult> Results,
    bool NoAutoTrading,
    bool HumanReviewRequired);
