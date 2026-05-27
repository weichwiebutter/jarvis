namespace Hermes.Runtime;

public sealed record CostStressScenario(
    string ScenarioId,
    string Name,
    double SpreadMultiplier,
    double SlippagePips,
    double CommissionMultiplier,
    double ExecutionDelayPenaltyR);

public sealed record CostStressScenarioResult(
    CostStressScenario Scenario,
    double AdjustedProfitFactor,
    double AdjustedNetR,
    double SurvivalScore,
    bool Survived);
