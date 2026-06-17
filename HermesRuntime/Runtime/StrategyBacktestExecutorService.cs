using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestExecutionResult(
    string BacktestExecutionId,
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> ParametersTested,
    string DatasetUsed,
    string PeriodUsed,
    int? TradesSimulated,
    double? WinRate,
    double? ProfitFactor,
    double? MaxDrawdown,
    double? Expectancy,
    double? RMultipleAvg,
    bool CostSpreadModelUsed,
    bool SimulatedPlaceholder,
    bool ExecutionSupported,
    string Status,
    IReadOnlyList<string> Warnings,
    bool RequiresHumanReview,
    DateTimeOffset AttemptedAtUtc);

public sealed record StrategyBacktestExecutorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string ReportRole,
    string QueuePath,
    int QueueItemsLoaded,
    int ReadyJobsFound,
    int JobsAttempted,
    int JobsExecuted,
    int JobsSkipped,
    StrategyBacktestJobPlan? SelectedJob,
    StrategyBacktestResult? Execution,
    IReadOnlyList<string> StatusDistribution,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool LatestSuccessAvailable,
    string LatestSuccessPath,
    string ContractMarkdownPath,
    string ContractJsonPath,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestExecutorService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;
    private string? _resolvedQueuePath;

    public StrategyBacktestExecutorService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_execution");
    public string QueuePath => Path.Combine(_storagePaths.Root, "queues", "strategy_backtest_jobs.json");
    public string ReportPath => _resolvedReportPath ?? StrategyBacktestResultArchiveService.LastRunReportPath(_storagePaths);
    public string MarkdownPath => _resolvedMarkdownPath ?? StrategyBacktestResultArchiveService.LastRunMarkdownPath(_storagePaths);
    public string QueueResolvedPath => _resolvedQueuePath ?? QueuePath;
    public string ContractMarkdownPath => Path.Combine(Root, "strategy_backtest_engine_contract.md");
    public string ContractJsonPath => Path.Combine(Root, "strategy_backtest_engine_contract.json");

    public StrategyBacktestExecutorReport Run()
    {
        StrategyBacktestResultArchiveService.EnsureDirectories(_storagePaths);

        var jobs = LoadJobs(QueuePath);
        var readyJobs = jobs.Where(job => job.Status.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase) && job.DatasetAvailable).ToList();
        var engine = new MinimalHistoricalBacktestEngine(_storagePaths);
        var selected = readyJobs.FirstOrDefault(job => CanEngineExecute(engine, job));
        selected ??= readyJobs.FirstOrDefault();

        StrategyBacktestResult? execution = null;
        var attempted = selected is null ? 0 : 1;
        var executed = 0;
        var skipped = jobs.Count - attempted;
        var warnings = new List<string>();
        var updatedJobs = jobs.ToList();

        if (selected is null)
        {
            warnings.Add("no_ready_to_execute_backtest_job_found");
        }
        else
        {
            var (request, dataset, safety) = BuildExecutionContext(selected);
            execution = engine.Execute(request, dataset, safety);
            warnings.AddRange(execution.Warnings);

            if (execution.ExecutionSupported && IsTerminalSuccess(execution.Status))
            {
                executed = 1;
                updatedJobs = jobs
                    .Select(job => job.BacktestJobId.Equals(selected.BacktestJobId, StringComparison.OrdinalIgnoreCase)
                        ? job with
                        {
                            Status = "completed",
                            NextAction = execution.Status == "completed_no_trades"
                                ? "Backtest abgeschlossen ohne Trades."
                                : "Backtest erfolgreich abgeschlossen."
                        }
                        : job)
                    .ToList();
                WriteQueue(updatedJobs);
            }
        }

        var latestSuccessPath = StrategyBacktestResultArchiveService.LatestSuccessReportPath(_storagePaths);
        var latestSuccessAvailable = File.Exists(latestSuccessPath);

        var report = new StrategyBacktestExecutorReport(
            ReportVersion: "strategy_backtest_executor_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReportRole: "last_run",
            QueuePath: QueuePath,
            QueueItemsLoaded: jobs.Count,
            ReadyJobsFound: readyJobs.Count,
            JobsAttempted: attempted,
            JobsExecuted: executed,
            JobsSkipped: skipped,
            SelectedJob: selected,
            Execution: execution,
            StatusDistribution: new[]
            {
                $"attempted:{attempted}",
                $"executed:{executed}",
                $"skipped:{skipped}",
            },
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: BuildOperatorSummary(attempted, executed, execution),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            LatestSuccessAvailable: latestSuccessAvailable,
            LatestSuccessPath: latestSuccessPath,
            ContractMarkdownPath: ContractMarkdownPath,
            ContractJsonPath: ContractJsonPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report, selected, execution, updatedJobs);
        WriteContractArtifacts();
        return report;
    }

    private static bool CanEngineExecute(MinimalHistoricalBacktestEngine engine, StrategyBacktestJobPlan job)
    {
        var (request, dataset, safety) = BuildExecutionContext(job);
        return engine.CanExecute(request, dataset, safety);
    }

    private static (StrategyBacktestRequest Request, StrategyBacktestDatasetDescriptor Dataset, StrategyBacktestSafetyContext Safety) BuildExecutionContext(StrategyBacktestJobPlan selected)
    {
        var request = new StrategyBacktestRequest(
            BacktestJobId: selected.BacktestJobId,
            StrategyPattern: selected.StrategyPattern,
            Asset: selected.Asset,
            Timeframe: selected.Timeframe,
            ParametersToTest: selected.ParametersToTest,
            DatasetPath: selected.DatasetRequired,
            DatasetId: selected.DatasetRequired,
            BacktestPeriod: selected.BacktestPeriod,
            OosPeriod: selected.OosPeriod,
            CostSpreadModel: selected.CostSpreadModelRequired ? "required" : "not_required",
            MaxRuns: selected.MaxRuns,
            TimeoutSeconds: selected.TimeoutSeconds,
            SafetyMode: selected.SafetyMode);
        var dataset = new StrategyBacktestDatasetDescriptor(
            DatasetPath: selected.DatasetRequired,
            DatasetId: selected.DatasetRequired,
            Asset: selected.Asset,
            Timeframe: selected.Timeframe,
            Period: selected.BacktestPeriod,
            Available: selected.DatasetAvailable,
            Warnings: selected.Blockers);
        var safety = new StrategyBacktestSafetyContext(
            NoAutoTrading: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            HumanReviewRequired: true,
            ResearchOnly: true,
            SafetyMode: selected.SafetyMode,
            SafetyFlags: selected.Blockers);
        return (request, dataset, safety);
    }

    private static bool IsTerminalSuccess(string status)
        => status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase);

    public StrategyBacktestExecutorReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestExecutorReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<StrategyBacktestJobPlan> LoadJobs(string queuePath)
    {
        if (!File.Exists(queuePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StrategyBacktestJobPlan>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private void WriteArtifacts(
        StrategyBacktestExecutorReport report,
        StrategyBacktestJobPlan? selected,
        StrategyBacktestResult? execution,
        IReadOnlyList<StrategyBacktestJobPlan> jobs)
    {
        StrategyBacktestResultArchiveService.WriteLastRun(_storagePaths, report);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;

        var attemptedJob = selected;
        var historyEntry = new StrategyBacktestRunHistoryEntry(
            AttemptedAtUtc: DateTimeOffset.UtcNow,
            BacktestJobId: attemptedJob?.BacktestJobId ?? "-",
            StrategyPattern: attemptedJob?.StrategyPattern ?? "-",
            Asset: attemptedJob?.Asset ?? "-",
            Timeframe: attemptedJob?.Timeframe ?? "-",
            ExecutionSupported: execution?.ExecutionSupported ?? false,
            Status: execution?.Status ?? "not_attempted",
            Successful: execution is not null && execution.ExecutionSupported && IsTerminalSuccess(execution.Status),
            Source: execution is not null && execution.ExecutionSupported && IsTerminalSuccess(execution.Status) ? "success" : "last_run",
            Warnings: execution?.Warnings ?? report.Warnings,
            Errors: execution?.Errors ?? []);
        StrategyBacktestResultArchiveService.WriteRunHistory(_storagePaths, historyEntry);

        if (selected is not null && execution is not null && execution.ExecutionSupported && IsTerminalSuccess(execution.Status))
        {
            StrategyBacktestResultArchiveService.WriteResult(_storagePaths, selected, execution);
        }
    }

    private void WriteQueue(IReadOnlyList<StrategyBacktestJobPlan> jobs)
    {
        File.WriteAllText(QueuePath, JsonSerializer.Serialize(jobs, JsonDefaults.WriteOptions));
        _resolvedQueuePath = QueuePath;
    }

    private void WriteContractArtifacts()
    {
        var contract = new StrategyBacktestEngineContractDocument(
            Title: "Strategy Backtest Engine Contract",
            Purpose: "Defines the safe interface between the strategy backtest executor and a future backtest engine.",
            InputContracts:
            [
                "StrategyBacktestRequest",
                "StrategyBacktestDatasetDescriptor",
                "StrategyBacktestSafetyContext",
            ],
            OutputContracts:
            [
                "StrategyBacktestResult",
                "IStrategyBacktestEngine",
                "StrategyBacktestEngineStub",
            ],
            ErrorCodes:
            [
                "execution_engine_missing",
                "dataset_missing",
                "unsupported_strategy_pattern",
                "unsupported_timeframe",
                "invalid_parameters",
                "timeout_limit_exceeded",
                "safety_gate_failed",
                "no_trades_generated",
            ],
            SafetyRules:
            [
                "No live trading",
                "No broker orders",
                "No cTrader API",
                "No demo orders",
                "No fake metrics",
                "No queue item completion without a real engine",
            ],
            StubEngineAvailable: true);

        var markdown = BuildContractMarkdown(contract);
        var json = JsonSerializer.Serialize(contract, JsonDefaults.WriteOptions);
        File.WriteAllText(ContractMarkdownPath, markdown);
        File.WriteAllText(ContractJsonPath, json);
    }

    private static string BuildMarkdown(StrategyBacktestExecutorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Executor");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Queue items loaded: {report.QueueItemsLoaded}");
        sb.AppendLine($"- Ready jobs found: {report.ReadyJobsFound}");
        sb.AppendLine($"- Jobs attempted: {report.JobsAttempted}");
        sb.AppendLine($"- Jobs executed: {report.JobsExecuted}");
        sb.AppendLine($"- Jobs skipped: {report.JobsSkipped}");
        sb.AppendLine($"- Contract markdown: {report.ContractMarkdownPath}");
        sb.AppendLine($"- Contract json: {report.ContractJsonPath}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        if (report.SelectedJob is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Selected Job");
            sb.AppendLine($"- {report.SelectedJob.StrategyPattern} @ {report.SelectedJob.Asset} {report.SelectedJob.Timeframe}");
            sb.AppendLine($"- status: {report.SelectedJob.Status}");
            sb.AppendLine($"- dataset_available: {report.SelectedJob.DatasetAvailable}");
        }
        if (report.Execution is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Execution");
            sb.AppendLine($"- supported: {report.Execution.ExecutionSupported}");
            sb.AppendLine($"- status: {report.Execution.Status}");
            if (report.Execution.TradesSimulated is not null)
            {
                sb.AppendLine($"- trades_simulated: {report.Execution.TradesSimulated}");
                sb.AppendLine($"- win_rate: {report.Execution.WinRate}");
                sb.AppendLine($"- profit_factor: {report.Execution.ProfitFactor}");
                sb.AppendLine($"- max_drawdown: {report.Execution.MaxDrawdown}");
                sb.AppendLine($"- expectancy: {report.Execution.Expectancy}");
                sb.AppendLine($"- r_multiple_avg: {report.Execution.RMultipleAvg}");
            }
        }
        return sb.ToString();
    }

    private static string BuildOperatorSummary(int attempted, int executed, StrategyBacktestResult? execution)
    {
        if (attempted == 0)
        {
            return "0 Backtest-Job geprüft. Kein Job verfügbar. Frank nötig: nein. Keine Broker-Aktionen.";
        }

        if (execution is null || !execution.ExecutionSupported)
        {
            return "1 Backtest-Job geprüft. 0/1 ausgeführt. Backtest-Engine noch nicht vorhanden. Frank nötig: nein. Keine Broker-Aktionen.";
        }

        if (execution.Status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase))
        {
            return "1 Backtest-Job geprüft. 1/1 ausgeführt. Backtest abgeschlossen ohne Trades. Frank nötig: nein. Keine Broker-Aktionen.";
        }

        return "1 Backtest-Job geprüft. 1/1 ausgeführt. Backtest erfolgreich abgeschlossen. Frank nötig: nein. Keine Broker-Aktionen.";
    }

    private static string BuildContractMarkdown(StrategyBacktestEngineContractDocument contract)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Engine Contract");
        sb.AppendLine();
        sb.AppendLine($"- Purpose: {contract.Purpose}");
        sb.AppendLine($"- Stub engine available: {contract.StubEngineAvailable}");
        sb.AppendLine();
        sb.AppendLine("## Input Contracts");
        foreach (var item in contract.InputContracts)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();
        sb.AppendLine("## Output Contracts");
        foreach (var item in contract.OutputContracts)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();
        sb.AppendLine("## Error Codes");
        foreach (var item in contract.ErrorCodes)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety Rules");
        foreach (var item in contract.SafetyRules)
        {
            sb.AppendLine($"- {item}");
        }
        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
