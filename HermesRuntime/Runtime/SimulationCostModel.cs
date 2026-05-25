namespace Hermes.Runtime;

public sealed record SimulationCostModel(
    string ModelVersion,
    double SpreadCostR,
    double CommissionR,
    double SlippageR,
    double SessionLiquidityPenaltyR,
    double SpreadWideningPenaltyR,
    double EstimatedCostR);
