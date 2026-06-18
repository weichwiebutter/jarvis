using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MutationValidationExecutionResult(
    string ExecutionId,
    string MutationValidationJobId,
    string MutationId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    string MutationType,
    bool ExecutionSupported,
    string Status,
    int? TradesSimulated,
    double? WinRate,
    double? ProfitFactor,
    double? MaxDrawdown,
    double? Expectancy,
    double? RMultipleAvg,
    string QualityClass,
    bool CertificationReady,
    bool CostSpreadModelUsed,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool RequiresHumanReview,
    DateTimeOffset GeneratedAtUtc);

public sealed record MutationValidationComparison(
    string BaselineBacktestJobId,
    int BaselineTradesSimulated,
    double BaselineWinRate,
    double BaselineProfitFactor,
    double BaselineMaxDrawdown,
    double BaselineExpectancy,
    string BaselineQualityClass,
    bool BaselineCertificationReady,
    int MutationTradesSimulated,
    double MutationWinRate,
    double MutationProfitFactor,
    double MutationMaxDrawdown,
    double MutationExpectancy,
    string MutationQualityClass,
    bool MutationCertificationReady,
    double WinRateDelta,
    double ProfitFactorDelta,
    double MaxDrawdownDelta,
    double ExpectancyDelta,
    string Outcome);

public sealed record MutationValidationExecutorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string ReportRole,
    string QueuePath,
    int JobsLoaded,
    int ReadyJobsFound,
    int JobsAttempted,
    int JobsExecuted,
    int JobsSkipped,
    MutationValidationJobPlan? SelectedJob,
    MutationValidationExecutionResult? Execution,
    MutationValidationComparison? Comparison,
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
    string ResultPath,
    string HistoryPath,
    string ReportPath,
    string MarkdownPath);

public sealed record MutationValidationRunHistoryEntry(
    DateTimeOffset AttemptedAtUtc,
    string MutationValidationJobId,
    string MutationId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    string MutationType,
    bool ExecutionSupported,
    string Status,
    string ComparisonOutcome,
    bool Successful,
    string Source,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public sealed record MutationValidationExecutionArtifact(
    string ReportRole,
    MutationValidationJobPlan Job,
    MutationValidationExecutionResult Execution,
    MutationValidationComparison Comparison,
    DateTimeOffset GeneratedAtUtc);

public sealed class MutationValidationExecutorService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private readonly string? _targetJobId;
    private readonly int? _maxRunsOverride;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public MutationValidationExecutorService(StoragePaths storagePaths, string runtimeRoot, string? targetJobId = null, int? maxRunsOverride = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
        _targetJobId = targetJobId;
        _maxRunsOverride = maxRunsOverride;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "mutation_validation_execution");
    public string ResultsRoot => Path.Combine(Root, "results");
    public string HistoryRoot => Path.Combine(Root, "history");
    public string QueuePath => Path.Combine(_storagePaths.Root, "reports", "mutation_validation_jobs", "mutation_validation_jobs.json");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "mutation_validation_execution.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "mutation_validation_execution.md");
    public string HistoryPath => Path.Combine(HistoryRoot, "mutation_validation_run_history.jsonl");
    public string LatestSuccessPath => StrategyBacktestResultArchiveService.LatestSuccessReportPath(_storagePaths);

    public MutationValidationExecutorReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ResultsRoot);
        Directory.CreateDirectory(HistoryRoot);

        var plannerService = new MutationValidationJobPlannerService(_storagePaths, _runtimeRoot);
        var planner = plannerService.Load() ?? plannerService.Run();
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);

        var readyJobs = planner.Jobs
            .Where(job => job.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var selected = SelectJob(readyJobs);
        var warnings = new List<string>();
        MutationValidationExecutionResult? execution = null;
        MutationValidationComparison? comparison = null;
        var jobsAttempted = selected is null ? 0 : 1;
        var jobsExecuted = 0;
        var jobsSkipped = Math.Max(0, planner.Jobs.Count - jobsAttempted);
        var resultPath = "-";

        if (selected is null)
        {
            warnings.Add("no_supported_ready_mutation_validation_job_found");
        }
        else
        {
            execution = ExecuteSelected(selected, latestSuccess, warnings);
            comparison = BuildComparison(selected, execution, latestSuccess);
            if (execution.ExecutionSupported && execution.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase))
            {
                execution = execution with
                {
                    Status = $"completed_{comparison!.Outcome}",
                };
            }

            if (execution.ExecutionSupported && execution.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase))
            {
                jobsExecuted = 1;
                resultPath = WriteResultArtifact(selected, execution, comparison!);
            }
        }

        if (latestSuccess is null)
        {
            warnings.Add("no_successful_backtest_found");
        }

        var report = new MutationValidationExecutorReport(
            ReportVersion: "mutation_validation_executor_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReportRole: "last_run",
            QueuePath: QueuePath,
            JobsLoaded: planner.Jobs.Count,
            ReadyJobsFound: readyJobs.Count,
            JobsAttempted: jobsAttempted,
            JobsExecuted: jobsExecuted,
            JobsSkipped: jobsSkipped,
            SelectedJob: selected,
            Execution: execution,
            Comparison: comparison,
            StatusDistribution: BuildStatusDistribution(jobsAttempted, jobsExecuted, jobsSkipped, execution),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: BuildOperatorSummary(selected, execution, comparison, jobsAttempted, jobsExecuted),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            LatestSuccessAvailable: latestSuccess is not null,
            LatestSuccessPath: LatestSuccessPath,
            ResultPath: resultPath,
            HistoryPath: HistoryPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        WriteHistory(report, selected, execution, comparison);
        return report;
    }

    public MutationValidationExecutorReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MutationValidationExecutorReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private MutationValidationJobPlan? SelectJob(IReadOnlyList<MutationValidationJobPlan> readyJobs)
    {
        if (!string.IsNullOrWhiteSpace(_targetJobId))
        {
            var requested = readyJobs.FirstOrDefault(job => job.ValidationJobId.Equals(_targetJobId, StringComparison.OrdinalIgnoreCase));
            if (requested is not null && IsSupportedJob(requested))
            {
                return requested;
            }

            return null;
        }

        return readyJobs
            .Where(IsSupportedJob)
            .OrderBy(job => PriorityRank(job.Priority))
            .ThenBy(job => MutationTypeRank(job.MutationType))
            .ThenBy(job => job.ValidationJobId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsSupportedJob(MutationValidationJobPlan job)
        => job.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)
            && job.Timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase)
            && job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase)
            && job.MutationType is "session_filter_sharpen" or "range_regime_enforce";

    private MutationValidationExecutionResult ExecuteSelected(
        MutationValidationJobPlan selected,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        List<string> warnings)
    {
        var executionId = $"mutation_validation_execution_{NormalizeId(selected.ValidationJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var supportIssues = GetSupportIssues(selected);
        if (supportIssues.Count > 0)
        {
            warnings.AddRange(supportIssues);
            return new MutationValidationExecutionResult(
                ExecutionId: executionId,
                MutationValidationJobId: selected.ValidationJobId,
                MutationId: selected.MutationId,
                StrategyPattern: selected.StrategyPattern,
                Asset: selected.Asset,
                Timeframe: selected.Timeframe,
                MutationType: selected.MutationType,
                ExecutionSupported: false,
                Status: "unsupported",
                TradesSimulated: null,
                WinRate: null,
                ProfitFactor: null,
                MaxDrawdown: null,
                Expectancy: null,
                RMultipleAvg: null,
                QualityClass: "unsupported",
                CertificationReady: false,
                CostSpreadModelUsed: false,
                Warnings: supportIssues,
                Errors: supportIssues,
                RequiresHumanReview: true,
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        if (!TryLoadCandles(selected.Asset, selected.Timeframe, out var candles, out var datasetWarnings, out var datasetErrors))
        {
            var errors = datasetErrors.Count > 0 ? datasetErrors : ["dataset_missing"];
            warnings.AddRange(datasetWarnings);
            warnings.AddRange(errors);
            return new MutationValidationExecutionResult(
                ExecutionId: executionId,
                MutationValidationJobId: selected.ValidationJobId,
                MutationId: selected.MutationId,
                StrategyPattern: selected.StrategyPattern,
                Asset: selected.Asset,
                Timeframe: selected.Timeframe,
                MutationType: selected.MutationType,
                ExecutionSupported: false,
                Status: "failed",
                TradesSimulated: null,
                WinRate: null,
                ProfitFactor: null,
                MaxDrawdown: null,
                Expectancy: null,
                RMultipleAvg: null,
                QualityClass: "unknown",
                CertificationReady: false,
                CostSpreadModelUsed: false,
                Warnings: datasetWarnings.Count > 0 ? datasetWarnings : ["dataset_missing"],
                Errors: errors,
                RequiresHumanReview: true,
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        var maxRuns = _maxRunsOverride is null or <= 0 ? Math.Max(1, selected.MaxRuns) : _maxRunsOverride.Value;
        var simulated = RunHistoricalMutationBacktest(selected, candles, maxRuns, datasetWarnings);
        if (simulated.Warnings.Count > 0)
        {
            warnings.AddRange(simulated.Warnings);
        }

        return simulated with
        {
            ExecutionId = executionId,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static MutationValidationComparison BuildComparison(
        MutationValidationJobPlan selected,
        MutationValidationExecutionResult execution,
        StrategyBacktestExecutorResultArtifact? latestSuccess)
    {
        if (latestSuccess is null || execution.TradesSimulated is null)
        {
            return new MutationValidationComparison(
                BaselineBacktestJobId: latestSuccess?.Job.BacktestJobId ?? "-",
                BaselineTradesSimulated: latestSuccess?.Execution.TradesSimulated ?? 0,
                BaselineWinRate: latestSuccess?.Execution.WinRate ?? 0,
                BaselineProfitFactor: latestSuccess?.Execution.ProfitFactor ?? 0,
                BaselineMaxDrawdown: latestSuccess?.Execution.MaxDrawdown ?? 0,
                BaselineExpectancy: latestSuccess?.Execution.Expectancy ?? 0,
                BaselineQualityClass: "unknown",
                BaselineCertificationReady: false,
                MutationTradesSimulated: execution.TradesSimulated ?? 0,
                MutationWinRate: execution.WinRate ?? 0,
                MutationProfitFactor: execution.ProfitFactor ?? 0,
                MutationMaxDrawdown: execution.MaxDrawdown ?? 0,
                MutationExpectancy: execution.Expectancy ?? 0,
                MutationQualityClass: execution.QualityClass,
                MutationCertificationReady: execution.CertificationReady,
                WinRateDelta: 0,
                ProfitFactorDelta: 0,
                MaxDrawdownDelta: 0,
                ExpectancyDelta: 0,
                Outcome: execution.ExecutionSupported ? "inconclusive" : "failed");
        }

        var baselineTrades = latestSuccess.Execution.TradesSimulated ?? 0;
        var baselineWinRate = latestSuccess.Execution.WinRate ?? 0;
        var baselineProfitFactor = latestSuccess.Execution.ProfitFactor ?? 0;
        var baselineMaxDrawdown = latestSuccess.Execution.MaxDrawdown ?? 0;
        var baselineExpectancy = latestSuccess.Execution.Expectancy ?? 0;
        var baselineQualityClass = ClassifyQuality(baselineTrades);
        var mutationTrades = execution.TradesSimulated ?? 0;
        var mutationWinRate = execution.WinRate ?? 0;
        var mutationProfitFactor = execution.ProfitFactor ?? 0;
        var mutationMaxDrawdown = execution.MaxDrawdown ?? 0;
        var mutationExpectancy = execution.Expectancy ?? 0;
        var mutationQualityClass = execution.QualityClass;
        var winRateDelta = Math.Round(mutationWinRate - baselineWinRate, 4);
        var profitFactorDelta = Math.Round(mutationProfitFactor - baselineProfitFactor, 4);
        var maxDrawdownDelta = Math.Round(mutationMaxDrawdown - baselineMaxDrawdown, 4);
        var expectancyDelta = Math.Round(mutationExpectancy - baselineExpectancy, 4);
        var outcome = DetermineOutcome(latestSuccess, execution);

        return new MutationValidationComparison(
            BaselineBacktestJobId: latestSuccess.Job.BacktestJobId,
            BaselineTradesSimulated: baselineTrades,
            BaselineWinRate: baselineWinRate,
            BaselineProfitFactor: baselineProfitFactor,
            BaselineMaxDrawdown: baselineMaxDrawdown,
            BaselineExpectancy: baselineExpectancy,
            BaselineQualityClass: baselineQualityClass,
            BaselineCertificationReady: latestSuccess.Execution.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || latestSuccess.Execution.Status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase),
            MutationTradesSimulated: mutationTrades,
            MutationWinRate: mutationWinRate,
            MutationProfitFactor: mutationProfitFactor,
            MutationMaxDrawdown: mutationMaxDrawdown,
            MutationExpectancy: mutationExpectancy,
            MutationQualityClass: mutationQualityClass,
            MutationCertificationReady: execution.CertificationReady,
            WinRateDelta: winRateDelta,
            ProfitFactorDelta: profitFactorDelta,
            MaxDrawdownDelta: maxDrawdownDelta,
            ExpectancyDelta: expectancyDelta,
            Outcome: outcome);
    }

    private static string DetermineOutcome(StrategyBacktestExecutorResultArtifact latestSuccess, MutationValidationExecutionResult execution)
    {
        if (!execution.ExecutionSupported)
        {
            return "unsupported";
        }

        if (execution.Status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase))
        {
            return "inconclusive";
        }

        var trades = execution.TradesSimulated ?? 0;
        if (trades < 30)
        {
            return "inconclusive";
        }

        var baselineTrades = latestSuccess.Execution.TradesSimulated ?? 0;
        var baselineWinRate = latestSuccess.Execution.WinRate ?? 0;
        var baselineProfitFactor = latestSuccess.Execution.ProfitFactor ?? 0;
        var baselineMaxDrawdown = latestSuccess.Execution.MaxDrawdown ?? 0;
        var baselineExpectancy = latestSuccess.Execution.Expectancy ?? 0;

        var betterProfitFactor = (execution.ProfitFactor ?? double.NegativeInfinity) > baselineProfitFactor;
        var betterExpectancy = (execution.Expectancy ?? double.NegativeInfinity) > baselineExpectancy;
        var betterDrawdown = (execution.MaxDrawdown ?? double.NegativeInfinity) >= baselineMaxDrawdown;
        var betterWinRate = (execution.WinRate ?? double.NegativeInfinity) >= baselineWinRate;

        var worseProfitFactor = (execution.ProfitFactor ?? double.PositiveInfinity) < baselineProfitFactor;
        var worseExpectancy = (execution.Expectancy ?? double.PositiveInfinity) < baselineExpectancy;
        var worseDrawdown = (execution.MaxDrawdown ?? double.PositiveInfinity) < baselineMaxDrawdown;
        var worseWinRate = (execution.WinRate ?? double.PositiveInfinity) < baselineWinRate;

        if (trades >= baselineTrades && betterProfitFactor && betterExpectancy && betterDrawdown && betterWinRate)
        {
            return "improved";
        }

        if (worseProfitFactor || worseExpectancy || worseDrawdown || worseWinRate)
        {
            return "worse";
        }

        return "inconclusive";
    }

    private static MutationValidationExecutionResult RunHistoricalMutationBacktest(
        MutationValidationJobPlan job,
        IReadOnlyList<MarketDataCandle> candles,
        int maxRuns,
        IReadOnlyList<string> datasetWarnings)
    {
        var executionId = $"mutation_validation_execution_{NormalizeId(job.ValidationJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        const int period = 20;
        const double deviation = 2.0;
        var trades = new List<TradeOutcome>();
        var equityCurve = new List<double>();
        var equity = 0.0;

        for (var index = period; index < candles.Count && trades.Count < maxRuns; index++)
        {
            var window = candles.Skip(index - period).Take(period).Select(candle => candle.Close).ToArray();
            var mean = window.Average();
            var variance = window.Select(value => Math.Pow(value - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            if (stdDev <= 0)
            {
                continue;
            }

            var upper = mean + deviation * stdDev;
            var lower = mean - deviation * stdDev;
            var bandWidth = upper - lower;
            var current = candles[index];
            var previous = candles[index - 1];

            if (!MutationFilterAllows(job.MutationType, candles, index, lower, upper, mean, bandWidth))
            {
                continue;
            }

            var longSignal = current.Low <= lower && current.Close > lower && current.Close >= previous.Close;
            var shortSignal = current.High >= upper && current.Close < upper && current.Close <= previous.Close;
            if (!longSignal && !shortSignal)
            {
                continue;
            }

            var direction = longSignal ? "long" : "short";
            var entry = current.Close;
            var stopDistance = Math.Max(bandWidth * 0.5, 0.4);
            var stop = direction == "long" ? entry - stopDistance : entry + stopDistance;
            var target = direction == "long" ? entry + stopDistance : entry - stopDistance;

            var result = ResolveTradeOutcome(candles, index, direction, entry, stop, target, equity);
            trades.Add(result);
            equity = result.EquityAfterTrade;
            equityCurve.Add(equity);
        }

        if (trades.Count == 0)
        {
            return new MutationValidationExecutionResult(
                ExecutionId: executionId,
                MutationValidationJobId: job.ValidationJobId,
                MutationId: job.MutationId,
                StrategyPattern: job.StrategyPattern,
                Asset: job.Asset,
                Timeframe: job.Timeframe,
                MutationType: job.MutationType,
                ExecutionSupported: true,
                Status: "completed_inconclusive",
                TradesSimulated: 0,
                WinRate: null,
                ProfitFactor: null,
                MaxDrawdown: null,
                Expectancy: null,
                RMultipleAvg: null,
                QualityClass: "insufficient_sample",
                CertificationReady: false,
                CostSpreadModelUsed: job.CostSpreadModelRequired,
                Warnings: datasetWarnings.Count > 0 ? datasetWarnings.Concat(["no_trades_generated"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : ["no_trades_generated"],
                Errors: ["no_trades_generated"],
                RequiresHumanReview: true,
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        var wins = trades.Count(trade => trade.NetR > 0);
        var grossProfit = trades.Where(trade => trade.NetR > 0).Sum(trade => trade.NetR);
        var grossLoss = Math.Abs(trades.Where(trade => trade.NetR < 0).Sum(trade => trade.NetR));
        var totalNet = trades.Sum(trade => trade.NetR);
        var expectancy = totalNet / trades.Count;
        var rMultipleAvg = trades.Average(trade => trade.NetR);
        var maxDrawdown = CalculateMaxDrawdown(equityCurve);
        var profitFactor = grossLoss <= 0 ? grossProfit : grossProfit / grossLoss;
        var qualityClass = ClassifyQuality(trades.Count);

        return new MutationValidationExecutionResult(
            ExecutionId: executionId,
            MutationValidationJobId: job.ValidationJobId,
            MutationId: job.MutationId,
            StrategyPattern: job.StrategyPattern,
            Asset: job.Asset,
            Timeframe: job.Timeframe,
            MutationType: job.MutationType,
            ExecutionSupported: true,
            Status: qualityClass == "insufficient_sample" ? "completed_inconclusive" : "completed_" + DetermineOutcomeLabel(trades.Count, profitFactor, expectancy, maxDrawdown),
            TradesSimulated: trades.Count,
            WinRate: Math.Round((double)wins / trades.Count, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(expectancy, 4),
            RMultipleAvg: Math.Round(rMultipleAvg, 4),
            QualityClass: qualityClass,
            CertificationReady: false,
            CostSpreadModelUsed: job.CostSpreadModelRequired,
            Warnings: datasetWarnings,
            Errors: [],
            RequiresHumanReview: true,
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static bool MutationFilterAllows(
        string mutationType,
        IReadOnlyList<MarketDataCandle> candles,
        int index,
        double lower,
        double upper,
        double mean,
        double bandWidth)
    {
        return mutationType switch
        {
            "session_filter_sharpen" => IsPreferredSession(candles[index].TimestampUtc),
            "range_regime_enforce" => IsRangeRegime(candles, index, bandWidth, mean),
            _ => false,
        };
    }

    private static bool IsPreferredSession(DateTimeOffset timestampUtc)
    {
        var hour = timestampUtc.UtcDateTime.Hour;
        return hour is >= 7 and < 10 or >= 13 and < 17;
    }

    private static bool IsRangeRegime(IReadOnlyList<MarketDataCandle> candles, int index, double bandWidth, double mean)
    {
        var lookback = Math.Min(60, index);
        if (lookback < 20)
        {
            return false;
        }

        var widths = new List<double>();
        for (var cursor = Math.Max(20, index - lookback); cursor < index; cursor++)
        {
            var window = candles.Skip(cursor - 20).Take(20).Select(candle => candle.Close).ToArray();
            if (window.Length == 0)
            {
                continue;
            }

            var windowMean = window.Average();
            var variance = window.Select(value => Math.Pow(value - windowMean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            if (stdDev > 0)
            {
                widths.Add((windowMean + 2 * stdDev) - (windowMean - 2 * stdDev));
            }
        }

        if (widths.Count == 0)
        {
            return false;
        }

        var avgWidth = widths.Average();
        return bandWidth <= avgWidth * 0.92 && bandWidth / Math.Max(mean, 1e-9) <= 0.01;
    }

    private static TradeOutcome ResolveTradeOutcome(
        IReadOnlyList<MarketDataCandle> candles,
        int entryIndex,
        string direction,
        double entry,
        double stop,
        double target,
        double equityBeforeTrade)
    {
        var closeIndex = Math.Min(entryIndex + 12, candles.Count - 1);
        var grossR = 0.0;
        var exitReason = "expired";
        var exitTime = candles[closeIndex].TimestampUtc;
        for (var index = entryIndex + 1; index <= closeIndex; index++)
        {
            var candle = candles[index];
            var stopHit = direction == "long" ? candle.Low <= stop : candle.High >= stop;
            var targetHit = direction == "long" ? candle.High >= target : candle.Low <= target;
            if (stopHit && targetHit)
            {
                grossR = -1.0;
                exitReason = "sl_hit_intracandle_ambiguous";
                exitTime = candle.TimestampUtc;
                break;
            }

            if (stopHit)
            {
                grossR = -1.0;
                exitReason = "sl_hit";
                exitTime = candle.TimestampUtc;
                break;
            }

            if (targetHit)
            {
                grossR = 1.0;
                exitReason = "tp_hit";
                exitTime = candle.TimestampUtc;
                break;
            }
        }

        if (exitReason == "expired")
        {
            var exitClose = candles[closeIndex].Close;
            grossR = direction == "long"
                ? (exitClose - entry) / Math.Abs(entry - stop)
                : (entry - exitClose) / Math.Abs(entry - stop);
            grossR = Math.Clamp(grossR, -1.0, 1.0);
        }

        var session = DetermineSession(candles[entryIndex].TimestampUtc);
        var spreadCost = 0.04;
        var commission = 0.02;
        var slippage = session == "london_new_york_overlap" ? 0.02 : 0.04;
        var net = Math.Round(grossR - spreadCost - commission - slippage, 4);
        var equityAfterTrade = Math.Round(equityBeforeTrade + net, 4);
        return new TradeOutcome(net, equityAfterTrade, exitReason, exitTime);
    }

    private static string DetermineSession(DateTimeOffset timestampUtc)
    {
        var hour = timestampUtc.UtcDateTime.Hour;
        if (hour is >= 7 and < 10)
        {
            return "london";
        }

        if (hour is >= 13 and < 17)
        {
            return "london_new_york_overlap";
        }

        if (hour is >= 13 and < 21)
        {
            return "new_york";
        }

        return "other";
    }

    private static double CalculateMaxDrawdown(IReadOnlyList<double> equityCurve)
    {
        var peak = double.NegativeInfinity;
        var maxDrawdown = 0.0;
        foreach (var value in equityCurve)
        {
            peak = Math.Max(peak, value);
            if (peak <= 0)
            {
                continue;
            }

            var drawdown = value - peak;
            maxDrawdown = Math.Min(maxDrawdown, drawdown);
        }
        return maxDrawdown;
    }

    private static string ClassifyQuality(int trades)
        => trades < 30 ? "insufficient_sample"
            : trades <= 100 ? "low_confidence"
            : trades <= 300 ? "medium_confidence"
            : "high_confidence";

    private static string DetermineOutcomeLabel(int trades, double profitFactor, double expectancy, double maxDrawdown)
    {
        if (trades < 30)
        {
            return "inconclusive";
        }

        if (profitFactor > 1.0 && expectancy > 0.0 && maxDrawdown > -5.0)
        {
            return "improved";
        }

        if (profitFactor < 1.0 || expectancy < 0.0 || maxDrawdown < -10.0)
        {
            return "worse";
        }

        return "inconclusive";
    }

    private static IReadOnlyList<string> GetSupportIssues(MutationValidationJobPlan job)
    {
        var issues = new List<string>();
        if (!job.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) || !job.Timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("unsupported_asset_or_timeframe");
        }

        if (!job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("unsupported_strategy_pattern");
        }

        if (job.MutationType is not ("session_filter_sharpen" or "range_regime_enforce"))
        {
            issues.Add("unsupported_mutation_type");
        }

        if (job.RequiredDataset.Contains("historical_data", StringComparison.OrdinalIgnoreCase) == false)
        {
            issues.Add("dataset_missing");
        }

        if (job.MaxRuns <= 0)
        {
            issues.Add("invalid_parameters");
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool TryLoadCandles(
        string asset,
        string timeframe,
        out IReadOnlyList<MarketDataCandle> candles,
        out List<string> warnings,
        out List<string> errors)
    {
        warnings = [];
        errors = [];
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", asset.ToUpperInvariant(), timeframe.ToUpperInvariant());
        if (!Directory.Exists(directory))
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        var files = Directory.EnumerateFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToList();
        if (files.Count == 0)
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        var candleMap = new Dictionary<DateTimeOffset, MarketDataCandle>();
        var invalidRows = 0;
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MarketDataCandle? candle;
                try
                {
                    candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
                }
                catch (JsonException)
                {
                    invalidRows++;
                    continue;
                }

                if (candle is null || candle.High < candle.Low || candle.Open <= 0 || candle.High <= 0 || candle.Low <= 0 || candle.Close <= 0)
                {
                    invalidRows++;
                    continue;
                }

                candleMap[candle.TimestampUtc] = candle;
            }
        }

        if (candleMap.Count == 0)
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        if (invalidRows > 0)
        {
            warnings.Add("dataset_rows_filtered");
        }

        candles = candleMap.Values.OrderBy(candle => candle.TimestampUtc).ToList();
        return true;
    }

    private string WriteResultArtifact(
        MutationValidationJobPlan selected,
        MutationValidationExecutionResult execution,
        MutationValidationComparison comparison)
    {
        var artifact = new MutationValidationExecutionArtifact(
            ReportRole: "result",
            Job: selected,
            Execution: execution,
            Comparison: comparison,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        var path = Path.Combine(ResultsRoot, $"mutation_validation_result_{NormalizeId(selected.ValidationJobId)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(artifact, JsonDefaults.WriteOptions));
        return path;
    }

    private void WriteArtifacts(MutationValidationExecutorReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private void WriteHistory(
        MutationValidationExecutorReport report,
        MutationValidationJobPlan? selected,
        MutationValidationExecutionResult? execution,
        MutationValidationComparison? comparison)
    {
        var entry = new MutationValidationRunHistoryEntry(
            AttemptedAtUtc: report.UpdatedAtUtc,
            MutationValidationJobId: selected?.ValidationJobId ?? "-",
            MutationId: selected?.MutationId ?? "-",
            StrategyPattern: selected?.StrategyPattern ?? "-",
            Asset: selected?.Asset ?? "-",
            Timeframe: selected?.Timeframe ?? "-",
            MutationType: selected?.MutationType ?? "-",
            ExecutionSupported: execution?.ExecutionSupported ?? false,
            Status: execution?.Status ?? "not_attempted",
            ComparisonOutcome: comparison?.Outcome ?? "unknown",
            Successful: execution is not null && execution.ExecutionSupported && execution.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase),
            Source: execution is not null && execution.ExecutionSupported && execution.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase) ? "result" : "last_run",
            Warnings: execution?.Warnings ?? report.Warnings,
            Errors: execution?.Errors ?? []);

        var line = JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine;
        File.AppendAllText(HistoryPath, line);
    }

    private static IReadOnlyList<string> BuildStatusDistribution(int attempted, int executed, int skipped, MutationValidationExecutionResult? execution)
    {
        var distribution = new List<string>
        {
            $"attempted:{attempted}",
            $"executed:{executed}",
            $"skipped:{skipped}",
        };

        if (execution is not null)
        {
            distribution.Add($"status:{execution.Status}");
        }

        return distribution;
    }

    private static string BuildOperatorSummary(
        MutationValidationJobPlan? selected,
        MutationValidationExecutionResult? execution,
        MutationValidationComparison? comparison,
        int attempted,
        int executed)
    {
        if (attempted == 0 || selected is null)
        {
            return "0 Mutation-Job geprüft. Kein unterstützter Job verfügbar. Frank nötig: nein. Keine Broker-Aktionen.";
        }

        if (execution is null || !execution.ExecutionSupported)
        {
            return $"1 Mutation-Job geprüft. 0/1 ausgeführt. Job nicht unterstützbar. Frank nötig: nein. Keine Broker-Aktionen.";
        }

        var outcome = comparison?.Outcome ?? "inconclusive";
        var trades = execution.TradesSimulated?.ToString() ?? "0";
        return $"1 Mutation-Job geprüft. {executed}/1 ausgeführt. Ergebnis: {outcome}. Trades={trades}. Frank nötig: nein. Keine Broker-Aktionen.";
    }

    private static string BuildMarkdown(MutationValidationExecutorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mutation Validation Execution");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Jobs loaded: {report.JobsLoaded}");
        sb.AppendLine($"- Ready jobs found: {report.ReadyJobsFound}");
        sb.AppendLine($"- Jobs attempted: {report.JobsAttempted}");
        sb.AppendLine($"- Jobs executed: {report.JobsExecuted}");
        sb.AppendLine($"- Jobs skipped: {report.JobsSkipped}");
        sb.AppendLine($"- Latest success available: {report.LatestSuccessAvailable}");
        sb.AppendLine($"- Latest success path: {report.LatestSuccessPath}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);

        if (report.SelectedJob is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Selected Job");
            sb.AppendLine($"- {report.SelectedJob.ValidationJobId}");
            sb.AppendLine($"- {report.SelectedJob.StrategyPattern} · {report.SelectedJob.Asset} {report.SelectedJob.Timeframe}");
            sb.AppendLine($"- mutation_type: {report.SelectedJob.MutationType}");
            sb.AppendLine($"- priority: {report.SelectedJob.Priority}");
            sb.AppendLine($"- readiness: {report.SelectedJob.ReadinessStatus}");
        }

        if (report.Execution is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Execution");
            sb.AppendLine($"- supported: {report.Execution.ExecutionSupported}");
            sb.AppendLine($"- status: {report.Execution.Status}");
            sb.AppendLine($"- quality_class: {report.Execution.QualityClass}");
            sb.AppendLine($"- certification_ready: {report.Execution.CertificationReady}");
            sb.AppendLine($"- trades_simulated: {report.Execution.TradesSimulated ?? 0}");
            sb.AppendLine($"- win_rate: {report.Execution.WinRate?.ToString("0.####") ?? "-"}");
            sb.AppendLine($"- profit_factor: {report.Execution.ProfitFactor?.ToString("0.####") ?? "-"}");
            sb.AppendLine($"- max_drawdown: {report.Execution.MaxDrawdown?.ToString("0.####") ?? "-"}");
            sb.AppendLine($"- expectancy: {report.Execution.Expectancy?.ToString("0.####") ?? "-"}");
            sb.AppendLine($"- r_multiple_avg: {report.Execution.RMultipleAvg?.ToString("0.####") ?? "-"}");
        }

        if (report.Comparison is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Comparison");
            sb.AppendLine($"- outcome: {report.Comparison.Outcome}");
            sb.AppendLine($"- baseline_backtest_job_id: {report.Comparison.BaselineBacktestJobId}");
            sb.AppendLine($"- baseline_profit_factor: {report.Comparison.BaselineProfitFactor:0.####}");
            sb.AppendLine($"- mutation_profit_factor: {report.Comparison.MutationProfitFactor:0.####}");
            sb.AppendLine($"- baseline_expectancy: {report.Comparison.BaselineExpectancy:0.####}");
            sb.AppendLine($"- mutation_expectancy: {report.Comparison.MutationExpectancy:0.####}");
            sb.AppendLine($"- baseline_max_drawdown: {report.Comparison.BaselineMaxDrawdown:0.####}");
            sb.AppendLine($"- mutation_max_drawdown: {report.Comparison.MutationMaxDrawdown:0.####}");
            sb.AppendLine($"- baseline_win_rate: {report.Comparison.BaselineWinRate:0.####}");
            sb.AppendLine($"- mutation_win_rate: {report.Comparison.MutationWinRate:0.####}");
        }

        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine(report.SafetySummary);
        return sb.ToString();
    }

    private static int PriorityRank(string priority)
        => priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 0
            : priority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private static int MutationTypeRank(string mutationType)
        => mutationType.Equals("session_filter_sharpen", StringComparison.OrdinalIgnoreCase) ? 0
            : mutationType.Equals("range_regime_enforce", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }

    private sealed record TradeOutcome(double NetR, double EquityAfterTrade, string ExitReason, DateTimeOffset ExitTimeUtc);
}
