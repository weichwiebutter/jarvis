using System.Text.Json;

namespace Hermes.Runtime;

public sealed class BacktestWorker : IWorker
{
    public const string BacktestJobType = "backtest.demo";

    private readonly StoragePaths _storagePaths;

    public BacktestWorker(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string WorkerName => "backtest_worker_stub";

    public string JobType => BacktestJobType;

    public WorkerExecutionResult Execute(JobManifest job)
    {
        var request = ReadRequest(job);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var completedAtUtc = DateTimeOffset.UtcNow;
        var runId = $"bt_demo_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";

        var report = new BacktestReport(
            RunId: runId,
            Symbol: request.Symbol,
            Timeframe: request.Timeframe,
            StrategyName: request.StrategyName,
            Status: "completed_demo",
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            TradeCount: 12,
            Winrate: 0.58,
            ProfitFactor: 1.42,
            MaxDrawdown: 0.064,
            Expectancy: 0.37,
            Notes: $"Demo-only backtest report for {request.Period}. No market replay, broker, order, or optimization was executed.",
            NoAutoTrading: true);

        var reportDirectory = Path.Combine(_storagePaths.Root, "reports", "backtests");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, $"{runId}.backtest.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));

        return new WorkerExecutionResult(
            OutputPath: reportPath,
            Metrics: new Dictionary<string, object?>
            {
                ["run_id"] = report.RunId,
                ["symbol"] = report.Symbol,
                ["timeframe"] = report.Timeframe,
                ["strategy_name"] = report.StrategyName,
                ["status"] = report.Status,
                ["trade_count"] = report.TradeCount,
                ["winrate"] = report.Winrate,
                ["profit_factor"] = report.ProfitFactor,
                ["max_drawdown"] = report.MaxDrawdown,
                ["expectancy"] = report.Expectancy,
                ["no_auto_trading"] = report.NoAutoTrading,
                ["stub"] = true
            });
    }

    private static BacktestJobRequest ReadRequest(JobManifest job)
    {
        return new BacktestJobRequest(
            Symbol: ReadStringParameter(job, "symbol", "XAUUSD"),
            Timeframe: ReadStringParameter(job, "timeframe", "M5"),
            Period: ReadStringParameter(job, "period", "Demo"),
            StrategyName: ReadStringParameter(job, "strategy_name", "DemoTrendPullback"));
    }

    private static string ReadStringParameter(JobManifest job, string key, string fallback)
    {
        if (!job.Parameters.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? fallback,
            _ => value.ToString() ?? fallback
        };
    }
}
