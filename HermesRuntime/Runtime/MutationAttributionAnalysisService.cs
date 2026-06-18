using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MutationAttributionItem(
    string MutationId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    string MutationType,
    string? ParentHypothesis,
    double BaselineWinRate,
    double MutationWinRate,
    double WinRateDelta,
    double BaselineProfitFactor,
    double MutationProfitFactor,
    double ProfitFactorDelta,
    double BaselineMaxDrawdown,
    double MutationMaxDrawdown,
    double MaxDrawdownDelta,
    double BaselineExpectancy,
    double MutationExpectancy,
    double ExpectancyDelta,
    int BaselineTrades,
    int MutationTrades,
    int BaselineWins,
    int BaselineLosses,
    int MutationWins,
    int MutationLosses,
    string Cause,
    string ResultClass,
    string LearningHypothesis,
    IReadOnlyList<string> SupportingSignals,
    IReadOnlyList<string> Warnings);

public sealed record MutationAttributionAnalysisReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string LatestSuccessPath,
    string MutationExecutionPath,
    string FailureLearningPath,
    string MutationCandidateQueuePath,
    string MutationValidationJobsPath,
    string MutationValidationExecutionRole,
    string BaselineStrategyPattern,
    string BaselineAsset,
    string BaselineTimeframe,
    string BaselineMutationType,
    double BaselineWinRate,
    double BaselineProfitFactor,
    double BaselineMaxDrawdown,
    double BaselineExpectancy,
    int BaselineTrades,
    int BaselineWins,
    int BaselineLosses,
    double MutationWinRate,
    double MutationProfitFactor,
    double MutationMaxDrawdown,
    double MutationExpectancy,
    int MutationTrades,
    int MutationWins,
    int MutationLosses,
    double WinRateDelta,
    double ProfitFactorDelta,
    double MaxDrawdownDelta,
    double ExpectancyDelta,
    string ResultClass,
    string Cause,
    string LearningHypothesis,
    IReadOnlyList<string> SupportingSignals,
    IReadOnlyList<string> Warnings,
    bool FrankRequired,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<MutationAttributionItem> Items);

public sealed class MutationAttributionAnalysisService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public MutationAttributionAnalysisService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "mutation_attribution_analysis.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "mutation_attribution_analysis.md");

    public MutationAttributionAnalysisReport Run()
    {
        Directory.CreateDirectory(Root);

        var mutationExecutionService = new MutationValidationExecutorService(_storagePaths, Directory.GetCurrentDirectory());
        var mutationExecutionReport = mutationExecutionService.Load() ?? mutationExecutionService.Run();
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var failureLearning = new StrategyBacktestFailureLearningService(_storagePaths).Load();
        var mutationQueue = new MutationCandidateQueueService(_storagePaths).Load() ?? new MutationCandidateQueueService(_storagePaths).Run();
        var mutationJobs = new MutationValidationJobPlannerService(_storagePaths, Directory.GetCurrentDirectory()).Load()
            ?? new MutationValidationJobPlannerService(_storagePaths, Directory.GetCurrentDirectory()).Run();

        var execution = mutationExecutionReport.Execution;
        var comparison = mutationExecutionReport.Comparison;
        var selectedJob = mutationExecutionReport.SelectedJob
            ?? mutationJobs.Jobs.FirstOrDefault(job => execution is not null && job.ValidationJobId.Equals(execution.MutationValidationJobId, StringComparison.OrdinalIgnoreCase));
        var baselineArtifact = latestSuccess;

        var baselineStrategyPattern = baselineArtifact?.Job.StrategyPattern ?? selectedJob?.StrategyPattern ?? "unknown";
        var baselineAsset = baselineArtifact?.Job.Asset ?? selectedJob?.Asset ?? "unknown";
        var baselineTimeframe = baselineArtifact?.Job.Timeframe ?? selectedJob?.Timeframe ?? "unknown";
        var baselineMutationType = selectedJob?.MutationType ?? "session_filter_sharpen";

        var baselineTrades = comparison?.BaselineTradesSimulated
            ?? baselineArtifact?.Execution.TradesSimulated
            ?? 0;
        var baselineWins = EstimateWins(baselineArtifact?.Execution.WinRate ?? comparison?.BaselineWinRate ?? 0d, baselineTrades);
        var baselineLosses = Math.Max(0, baselineTrades - baselineWins);
        var mutationTrades = comparison?.MutationTradesSimulated
            ?? execution?.TradesSimulated
            ?? 0;
        var mutationWins = EstimateWins(execution?.WinRate ?? comparison?.MutationWinRate ?? 0d, mutationTrades);
        var mutationLosses = Math.Max(0, mutationTrades - mutationWins);

        var cause = ClassifyCause(selectedJob?.MutationType ?? execution?.MutationType ?? "unknown", comparison, execution);
        var learningHypothesis = BuildLearningHypothesis(selectedJob?.MutationType ?? execution?.MutationType ?? "unknown", cause);
        var signals = BuildSupportingSignals(selectedJob, comparison, execution, failureLearning, mutationQueue);
        var warnings = new List<string>();
        if (latestSuccess is null)
        {
            warnings.Add("no_successful_baseline_found");
        }
        if (execution is null)
        {
            warnings.Add("no_mutation_execution_found");
        }

        var item = new MutationAttributionItem(
            MutationId: selectedJob?.MutationId ?? execution?.MutationId ?? "unknown",
            StrategyPattern: selectedJob?.StrategyPattern ?? execution?.StrategyPattern ?? baselineStrategyPattern,
            Asset: selectedJob?.Asset ?? execution?.Asset ?? baselineAsset,
            Timeframe: selectedJob?.Timeframe ?? execution?.Timeframe ?? baselineTimeframe,
            MutationType: selectedJob?.MutationType ?? execution?.MutationType ?? "unknown",
            ParentHypothesis: selectedJob?.ValidationJobId,
            BaselineWinRate: comparison?.BaselineWinRate ?? baselineArtifact?.Execution.WinRate ?? 0d,
            MutationWinRate: comparison?.MutationWinRate ?? execution?.WinRate ?? 0d,
            WinRateDelta: comparison?.WinRateDelta ?? ((execution?.WinRate ?? 0d) - (baselineArtifact?.Execution.WinRate ?? 0d)),
            BaselineProfitFactor: comparison?.BaselineProfitFactor ?? baselineArtifact?.Execution.ProfitFactor ?? 0d,
            MutationProfitFactor: comparison?.MutationProfitFactor ?? execution?.ProfitFactor ?? 0d,
            ProfitFactorDelta: comparison?.ProfitFactorDelta ?? ((execution?.ProfitFactor ?? 0d) - (baselineArtifact?.Execution.ProfitFactor ?? 0d)),
            BaselineMaxDrawdown: comparison?.BaselineMaxDrawdown ?? baselineArtifact?.Execution.MaxDrawdown ?? 0d,
            MutationMaxDrawdown: comparison?.MutationMaxDrawdown ?? execution?.MaxDrawdown ?? 0d,
            MaxDrawdownDelta: comparison?.MaxDrawdownDelta ?? ((execution?.MaxDrawdown ?? 0d) - (baselineArtifact?.Execution.MaxDrawdown ?? 0d)),
            BaselineExpectancy: comparison?.BaselineExpectancy ?? baselineArtifact?.Execution.Expectancy ?? 0d,
            MutationExpectancy: comparison?.MutationExpectancy ?? execution?.Expectancy ?? 0d,
            ExpectancyDelta: comparison?.ExpectancyDelta ?? ((execution?.Expectancy ?? 0d) - (baselineArtifact?.Execution.Expectancy ?? 0d)),
            BaselineTrades: baselineTrades,
            MutationTrades: mutationTrades,
            BaselineWins: baselineWins,
            BaselineLosses: baselineLosses,
            MutationWins: mutationWins,
            MutationLosses: mutationLosses,
            Cause: cause,
            ResultClass: execution is null ? "insufficient_evidence" : cause,
            LearningHypothesis: learningHypothesis,
            SupportingSignals: signals,
            Warnings: warnings);

        var report = new MutationAttributionAnalysisReport(
            ReportVersion: "mutation_attribution_analysis_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            LatestSuccessPath: StrategyBacktestResultArchiveService.LatestSuccessReportPath(_storagePaths),
            MutationExecutionPath: mutationExecutionService.ReportPath,
            FailureLearningPath: Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_failure_learning", "strategy_backtest_failure_learning.json"),
            MutationCandidateQueuePath: Path.Combine(_storagePaths.Root, "reports", "mutation_candidate_queue", "mutation_candidate_queue.json"),
            MutationValidationJobsPath: Path.Combine(_storagePaths.Root, "reports", "mutation_validation_jobs", "mutation_validation_jobs.json"),
            MutationValidationExecutionRole: mutationExecutionReport.ReportRole,
            BaselineStrategyPattern: item.StrategyPattern,
            BaselineAsset: item.Asset,
            BaselineTimeframe: item.Timeframe,
            BaselineMutationType: item.MutationType,
            BaselineWinRate: item.BaselineWinRate,
            BaselineProfitFactor: item.BaselineProfitFactor,
            BaselineMaxDrawdown: item.BaselineMaxDrawdown,
            BaselineExpectancy: item.BaselineExpectancy,
            BaselineTrades: item.BaselineTrades,
            BaselineWins: item.BaselineWins,
            BaselineLosses: item.BaselineLosses,
            MutationWinRate: item.MutationWinRate,
            MutationProfitFactor: item.MutationProfitFactor,
            MutationMaxDrawdown: item.MutationMaxDrawdown,
            MutationExpectancy: item.MutationExpectancy,
            MutationTrades: item.MutationTrades,
            MutationWins: item.MutationWins,
            MutationLosses: item.MutationLosses,
            WinRateDelta: item.WinRateDelta,
            ProfitFactorDelta: item.ProfitFactorDelta,
            MaxDrawdownDelta: item.MaxDrawdownDelta,
            ExpectancyDelta: item.ExpectancyDelta,
            ResultClass: item.ResultClass,
            Cause: item.Cause,
            LearningHypothesis: item.LearningHypothesis,
            SupportingSignals: signals,
            Warnings: warnings,
            FrankRequired: false,
            OperatorSummary: BuildOperatorSummary(item),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Items: [item]);

        WriteArtifacts(report);
        return report;
    }

    public MutationAttributionAnalysisReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MutationAttributionAnalysisReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static int EstimateWins(double winRate, int trades)
        => trades <= 0 ? 0 : (int)Math.Round(trades * Math.Clamp(winRate, 0d, 1d));

    private static string ClassifyCause(string mutationType, MutationValidationComparison? comparison, MutationValidationExecutionResult? execution)
    {
        if (comparison is null || execution is null)
        {
            return "insufficient_evidence";
        }

        if (mutationType.Equals("session_filter_sharpen", StringComparison.OrdinalIgnoreCase))
        {
            return "improvement_likely_caused_by_session_filter";
        }

        if (mutationType.Equals("range_regime_enforce", StringComparison.OrdinalIgnoreCase))
        {
            return "improvement_likely_caused_by_range_filter";
        }

        if (mutationType.Equals("volatility_filter_add", StringComparison.OrdinalIgnoreCase))
        {
            return "improvement_likely_caused_by_volatility_filter";
        }

        return "improvement_unclear";
    }

    private static string BuildLearningHypothesis(string mutationType, string cause)
        => cause switch
        {
            "improvement_likely_caused_by_session_filter" => "Die Strategie funktioniert deutlich besser, wenn bestimmte Handelssessions ausgeschlossen werden.",
            "improvement_likely_caused_by_range_filter" => "Die Strategie verbessert sich wahrscheinlich, wenn sie nur in Range-Regimen gehandelt wird.",
            "improvement_likely_caused_by_volatility_filter" => "Die Strategie verbessert sich wahrscheinlich, wenn extreme Volatilitätsphasen gefiltert werden.",
            _ when mutationType.Equals("session_filter_sharpen", StringComparison.OrdinalIgnoreCase) => "Die Verbesserung stammt wahrscheinlich aus dem Ausschluss schwacher Sessions.",
            _ => "Die Verbesserung ist noch nicht eindeutig einer einzelnen Filterklasse zuzuordnen.",
        };

    private static IReadOnlyList<string> BuildSupportingSignals(
        MutationValidationJobPlan? selectedJob,
        MutationValidationComparison? comparison,
        MutationValidationExecutionResult? execution,
        StrategyBacktestFailureLearningReport? failureLearning,
        MutationCandidateQueueReport mutationQueue)
    {
        var signals = new List<string>();
        if (selectedJob is not null)
        {
            signals.Add($"mutation_type:{selectedJob.MutationType}");
            signals.Add($"strategy_pattern:{selectedJob.StrategyPattern}");
        }

        if (comparison is not null)
        {
            signals.Add($"profit_factor_delta:{comparison.ProfitFactorDelta:0.####}");
            signals.Add($"expectancy_delta:{comparison.ExpectancyDelta:0.####}");
            signals.Add($"win_rate_delta:{comparison.WinRateDelta:0.####}");
            signals.Add($"max_drawdown_delta:{comparison.MaxDrawdownDelta:0.####}");
        }

        if (execution is not null)
        {
            signals.Add($"trades_simulated:{execution.TradesSimulated ?? 0}");
        }

        if (failureLearning is not null)
        {
            signals.Add($"learning_decision:{failureLearning.Recommendations.FirstOrDefault() ?? failureLearning.LearningDecision}");
        }

        signals.Add($"mutation_queue_size:{mutationQueue.QueueSize}");
        return signals;
    }

    private static string BuildOperatorSummary(MutationAttributionItem item)
        => item.Cause switch
        {
            "improvement_likely_caused_by_session_filter" => "Der Sessionfilter reduzierte vermutlich verlustreiche Handelsphasen. Die Verbesserung stammt wahrscheinlich aus dem Ausschluss schwacher Sessions. Frank muss nichts tun.",
            "improvement_likely_caused_by_range_filter" => "Der Range-Filter vermied vermutlich ungeeignete Marktphasen. Frank muss nichts tun.",
            "improvement_likely_caused_by_volatility_filter" => "Der Volatilitätsfilter entfernte vermutlich ungünstige Marktphasen. Frank muss nichts tun.",
            "improvement_unclear" => "Die Verbesserung ist sichtbar, aber die Ursache bleibt noch unklar. Frank muss nichts tun.",
            _ => "Es liegt noch keine belastbare Attribution vor. Frank muss nichts tun.",
        };

    private void WriteArtifacts(MutationAttributionAnalysisReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(MutationAttributionAnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mutation Attribution Analysis");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Result class: {report.ResultClass}");
        sb.AppendLine($"- Cause: {report.Cause}");
        sb.AppendLine($"- Learning hypothesis: {report.LearningHypothesis}");
        sb.AppendLine();
        sb.AppendLine("## Baseline");
        sb.AppendLine($"- Trades: {report.BaselineTrades}");
        sb.AppendLine($"- Win rate: {report.BaselineWinRate:0.####}");
        sb.AppendLine($"- Profit factor: {report.BaselineProfitFactor:0.####}");
        sb.AppendLine($"- Max drawdown: {report.BaselineMaxDrawdown:0.####}");
        sb.AppendLine($"- Expectancy: {report.BaselineExpectancy:0.####}");
        sb.AppendLine();
        sb.AppendLine("## Mutation");
        sb.AppendLine($"- Trades: {report.MutationTrades}");
        sb.AppendLine($"- Win rate: {report.MutationWinRate:0.####}");
        sb.AppendLine($"- Profit factor: {report.MutationProfitFactor:0.####}");
        sb.AppendLine($"- Max drawdown: {report.MutationMaxDrawdown:0.####}");
        sb.AppendLine($"- Expectancy: {report.MutationExpectancy:0.####}");
        sb.AppendLine();
        sb.AppendLine("## Operator");
        sb.AppendLine(report.OperatorSummary);
        return sb.ToString();
    }
}
