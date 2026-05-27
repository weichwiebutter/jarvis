namespace Hermes.Runtime;

public sealed record MonteCarloScenario(
    string ScenarioId,
    int SimulationRuns,
    int TradeSampleSize,
    double SpreadVariation,
    double SlippageVariation,
    double ExecutionDelayProbability,
    bool WorstCaseDrawdownSimulation);
