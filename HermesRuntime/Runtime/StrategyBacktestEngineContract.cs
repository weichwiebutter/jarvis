namespace Hermes.Runtime;

public sealed record StrategyBacktestRequest(
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> ParametersToTest,
    string DatasetPath,
    string DatasetId,
    string BacktestPeriod,
    string OosPeriod,
    string CostSpreadModel,
    int MaxRuns,
    int TimeoutSeconds,
    string SafetyMode);

public sealed record StrategyBacktestDatasetDescriptor(
    string DatasetPath,
    string DatasetId,
    string Asset,
    string Timeframe,
    string Period,
    bool Available,
    IReadOnlyList<string> Warnings);

public sealed record StrategyBacktestSafetyContext(
    bool NoAutoTrading,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool HumanReviewRequired,
    bool ResearchOnly,
    string SafetyMode,
    IReadOnlyList<string> SafetyFlags);

public sealed record StrategyBacktestResult(
    string ExecutionId,
    string BacktestJobId,
    bool ExecutionSupported,
    string Status,
    int? TradesSimulated,
    double? WinRate,
    double? ProfitFactor,
    double? MaxDrawdown,
    double? Expectancy,
    double? RMultipleAvg,
    bool CostSpreadModelUsed,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool RequiresHumanReview,
    DateTimeOffset GeneratedAtUtc);

public sealed record StrategyBacktestEngineContractDocument(
    string Title,
    string Purpose,
    IReadOnlyList<string> InputContracts,
    IReadOnlyList<string> OutputContracts,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> SafetyRules,
    bool StubEngineAvailable);

public interface IStrategyBacktestEngine
{
    bool CanExecute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext);
    StrategyBacktestResult Execute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext);
}

public sealed class StrategyBacktestEngineStub : IStrategyBacktestEngine
{
    public bool CanExecute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext)
        => false;

    public StrategyBacktestResult Execute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext)
        => new(
            ExecutionId: $"stub_execution_{NormalizeId(request.BacktestJobId)}",
            BacktestJobId: request.BacktestJobId,
            ExecutionSupported: false,
            Status: "ready_to_execute",
            TradesSimulated: null,
            WinRate: null,
            ProfitFactor: null,
            MaxDrawdown: null,
            Expectancy: null,
            RMultipleAvg: null,
            CostSpreadModelUsed: request.CostSpreadModel.Equals("true", StringComparison.OrdinalIgnoreCase),
            Warnings: ["execution_engine_missing", "backtest_not_started"],
            Errors: ["execution_engine_missing"],
            RequiresHumanReview: true,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
