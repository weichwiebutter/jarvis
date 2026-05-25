namespace Hermes.Runtime;

public sealed record RealisticTradeSimulation(
    string SimulationVersion,
    string ExecutionModel,
    bool CandleByCandle,
    bool PartialFillsStubbed,
    int MaxConcurrentTrades,
    IReadOnlyList<string> Assumptions);
