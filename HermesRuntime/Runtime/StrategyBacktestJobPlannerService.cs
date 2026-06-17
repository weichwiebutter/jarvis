using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestJobPlan(
    string BacktestJobId,
    string SourceQueueItemId,
    string ValidationPlanId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> ParametersToTest,
    string DatasetRequired,
    bool DatasetAvailable,
    string BacktestPeriod,
    string OosPeriod,
    bool WalkForwardRequired,
    bool MonteCarloRequired,
    bool CostSpreadModelRequired,
    string AssumedSpreadSource,
    int MaxRuns,
    int TimeoutSeconds,
    string SafetyMode,
    string Status,
    IReadOnlyList<string> Blockers,
    string NextAction);

public sealed record StrategyBacktestJobPlannerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int QueueItemsAnalyzed,
    int BacktestJobsPrepared,
    int ReadyToExecuteCount,
    int WaitingForDataCount,
    int BlockedCount,
    IReadOnlyList<StrategyBacktestJobPlan> Jobs,
    IReadOnlyList<string> StatusDistribution,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string QueuePath,
    string ReadinessPath,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestJobPlannerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;
    private string? _resolvedQueuePath;

    public StrategyBacktestJobPlannerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_jobs");
    public string QueueDirectory => Path.Combine(_storagePaths.Root, "queues");
    public string QueuePath => _resolvedQueuePath ?? Path.Combine(QueueDirectory, "strategy_backtest_jobs.json");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_backtest_job_planner.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_backtest_job_planner.md");

    public StrategyBacktestJobPlannerReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(QueueDirectory);

        var queuePath = Path.Combine(_storagePaths.Root, "queues", "strategy_validation_queue.json");
        var readinessPath = Path.Combine(_storagePaths.Root, "reports", "strategy_validation_readiness", "strategy_validation_readiness_analyzer.json");
        var queueItems = LoadQueue(queuePath);
        var readiness = LoadReadiness(readinessPath) ?? new StrategyValidationReadinessAnalyzerService(_storagePaths, _runtimeRoot).Run();
        var jobs = BuildJobs(queueItems, readiness);

        var readyCount = jobs.Count(job => job.Status == "ready_to_execute");
        var waitingCount = jobs.Count(job => job.Status == "waiting_for_data");
        var blockedCount = jobs.Count(job => job.Status == "blocked");
        var report = new StrategyBacktestJobPlannerReport(
            ReportVersion: "strategy_backtest_job_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            QueueItemsAnalyzed: queueItems.Count,
            BacktestJobsPrepared: jobs.Count,
            ReadyToExecuteCount: readyCount,
            WaitingForDataCount: waitingCount,
            BlockedCount: blockedCount,
            Jobs: jobs,
            StatusDistribution: jobs.GroupBy(job => job.Status).Select(group => $"{group.Key}:{group.Count()}").OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: jobs.Any(job => job.Status == "waiting_for_data") ? ["dataset_unavailable_for_some_jobs"] : [],
            OperatorSummary: $"{queueItems.Count} Strategie-Aufträge geprüft. {readyCount} Backtest-Jobs bereit. {waitingCount} warten auf Daten. {blockedCount} blockiert. Keine Backtests gestartet. Frank nötig: nein.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            QueuePath: QueuePath,
            ReadinessPath: readinessPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public StrategyBacktestJobPlannerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestJobPlannerReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<StrategyValidationQueueItem> LoadQueue(string queuePath)
    {
        if (!File.Exists(queuePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StrategyValidationQueueItem>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static StrategyValidationReadinessAnalyzerReport? LoadReadiness(string readinessPath)
    {
        if (!File.Exists(readinessPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyValidationReadinessAnalyzerReport>(File.ReadAllText(readinessPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IReadOnlyList<StrategyBacktestJobPlan> BuildJobs(
        IReadOnlyList<StrategyValidationQueueItem> queueItems,
        StrategyValidationReadinessAnalyzerReport readiness)
    {
        var readinessByQueueId = readiness.Items.ToDictionary(item => item.QueueItemId, StringComparer.OrdinalIgnoreCase);
        var datasetGateService = new StrategyDatasetGateService(_storagePaths, _runtimeRoot);
        var jobs = new List<StrategyBacktestJobPlan>();

        foreach (var item in queueItems.Where(item => readinessByQueueId.TryGetValue(item.QueueItemId, out var readinessItem) && readinessItem.Status == "ready_for_backtest"))
        {
            var datasetGate = datasetGateService.Evaluate(item.Asset, item.Timeframe);
            var datasetAvailable = datasetGate.DatasetAvailable;
            var status = datasetAvailable ? "ready_to_execute" : "waiting_for_data";
            var blockers = new List<string>();
            if (!datasetAvailable)
            {
                blockers.Add("dataset_missing");
            }

            jobs.Add(new StrategyBacktestJobPlan(
                BacktestJobId: $"backtest_job_{NormalizeId(item.ValidationPlanId)}",
                SourceQueueItemId: item.QueueItemId,
                ValidationPlanId: item.ValidationPlanId,
                StrategyPattern: item.StrategyPattern,
                Asset: item.Asset,
                Timeframe: item.Timeframe,
                ParametersToTest: item.ParametersToValidate,
                DatasetRequired: datasetGate.DatasetPeriod,
                DatasetAvailable: datasetAvailable,
                BacktestPeriod: GetBacktestPeriod(item.Asset, item.Timeframe),
                OosPeriod: GetOosPeriod(item.Asset, item.Timeframe),
                WalkForwardRequired: true,
                MonteCarloRequired: true,
                CostSpreadModelRequired: true,
                AssumedSpreadSource: datasetGate.DatasetSource,
                MaxRuns: 3,
                TimeoutSeconds: 1800,
                SafetyMode: "no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true",
                Status: status,
                Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                NextAction: status == "ready_to_execute"
                    ? "Backtest-Job kann sicher an den Executor übergeben werden."
                    : "Dataset ergänzen, dann erneut prüfen."));
        }

        return jobs
            .OrderByDescending(job => job.Status == "ready_to_execute")
            .ThenByDescending(job => job.ValidationPlanId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetBacktestPeriod(string asset, string timeframe) => $"{asset}:{timeframe}:historical";
    private static string GetOosPeriod(string asset, string timeframe) => $"{asset}:{timeframe}:oos";

    private void WriteArtifacts(StrategyBacktestJobPlannerReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        File.WriteAllText(QueuePath, JsonSerializer.Serialize(report.Jobs, JsonDefaults.WriteOptions));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
        _resolvedQueuePath = QueuePath;
    }

    private static string BuildMarkdown(StrategyBacktestJobPlannerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Job Planner");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Queue items analyzed: {report.QueueItemsAnalyzed}");
        sb.AppendLine($"- Backtest jobs prepared: {report.BacktestJobsPrepared}");
        sb.AppendLine($"- Ready to execute: {report.ReadyToExecuteCount}");
        sb.AppendLine($"- Waiting for data: {report.WaitingForDataCount}");
        sb.AppendLine($"- Blocked: {report.BlockedCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
