using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MutationValidationJobPlan(
    string ValidationJobId,
    string MutationId,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string MutationType,
    string RequiredDataset,
    bool BacktestRequired,
    bool OosRequired,
    bool WalkForwardRequired,
    bool MonteCarloRequired,
    bool CostSpreadModelRequired,
    int MaxRuns,
    string ReadinessStatus,
    IReadOnlyList<string> Blockers,
    string Priority);

public sealed record MutationValidationJobPlannerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int MutationsAnalyzed,
    int JobsPrepared,
    int ReadyToExecuteCount,
    int WaitingForDataCount,
    int WaitingForEngineSupportCount,
    int WaitingForSpecificationCount,
    int BlockedCount,
    IReadOnlyList<MutationValidationJobPlan> Jobs,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string NextSafeStep,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class MutationValidationJobPlannerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public MutationValidationJobPlannerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "mutation_validation_jobs");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "mutation_validation_jobs.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "mutation_validation_jobs.md");

    public MutationValidationJobPlannerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MutationValidationJobPlannerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public MutationValidationJobPlannerReport Run()
    {
        Directory.CreateDirectory(Root);

        var mutationQueue = new MutationCandidateQueueService(_storagePaths).Load()
            ?? new MutationCandidateQueueService(_storagePaths).Run();
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var failureLearning = new StrategyBacktestFailureLearningService(_storagePaths).Load();
        var qualityAudit = new StrategyBacktestQualityAuditService(_storagePaths).Load();
        var readiness = new StrategyValidationReadinessAnalyzerService(_storagePaths, _runtimeRoot).Load();

        var jobs = BuildJobs(mutationQueue, latestSuccess, failureLearning, qualityAudit, readiness);
        var report = new MutationValidationJobPlannerReport(
            ReportVersion: "mutation_validation_job_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MutationsAnalyzed: mutationQueue.QueueSize,
            JobsPrepared: jobs.Count,
            ReadyToExecuteCount: jobs.Count(job => job.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase)),
            WaitingForDataCount: jobs.Count(job => job.ReadinessStatus.Equals("waiting_for_data", StringComparison.OrdinalIgnoreCase)),
            WaitingForEngineSupportCount: jobs.Count(job => job.ReadinessStatus.Equals("waiting_for_engine_support", StringComparison.OrdinalIgnoreCase)),
            WaitingForSpecificationCount: jobs.Count(job => job.ReadinessStatus.Equals("waiting_for_specification", StringComparison.OrdinalIgnoreCase)),
            BlockedCount: jobs.Count(job => job.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            Jobs: jobs,
            SourceReports: BuildSourceReports(mutationQueue, latestSuccess, failureLearning, qualityAudit, readiness),
            Warnings: BuildWarnings(mutationQueue, latestSuccess, failureLearning, qualityAudit, readiness),
            OperatorSummary: BuildOperatorSummary(jobs),
            NextSafeStep: BuildNextSafeStep(jobs),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    private static IReadOnlyList<MutationValidationJobPlan> BuildJobs(
        MutationCandidateQueueReport mutationQueue,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyValidationReadinessAnalyzerReport? readiness)
    {
        var supportedPattern = latestSuccess?.Job.StrategyPattern ?? "-";
        var supportedAsset = latestSuccess?.Job.Asset ?? "-";
        var supportedTimeframe = latestSuccess?.Job.Timeframe ?? "-";
        var supportedDataset = latestSuccess is not null ? $"historical_data:{supportedAsset}:{supportedTimeframe}" : "-";
        var maxRuns = latestSuccess is not null ? 50 : 0;

        return mutationQueue.QueueItems
            .OrderBy(item => PriorityRank(item.Priority))
            .ThenBy(item => item.MutationType, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var blockers = new List<string>();
                var readinessStatus = DetermineReadinessStatus(item, supportedPattern, supportedAsset, supportedTimeframe, supportedDataset, latestSuccess, failureLearning, qualityAudit, readiness, blockers);

                return new MutationValidationJobPlan(
                    ValidationJobId: $"mutation_validation_{item.MutationId}".Replace(' ', '_'),
                    MutationId: item.MutationId,
                    Asset: item.Asset,
                    Timeframe: item.Timeframe,
                    StrategyPattern: item.StrategyPattern,
                    MutationType: item.MutationType,
                    RequiredDataset: supportedDataset,
                    BacktestRequired: true,
                    OosRequired: true,
                    WalkForwardRequired: true,
                    MonteCarloRequired: true,
                    CostSpreadModelRequired: true,
                    MaxRuns: maxRuns,
                    ReadinessStatus: readinessStatus,
                    Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    Priority: item.Priority);
            })
            .ToList();
    }

    private static string DetermineReadinessStatus(
        MutationCandidateQueueItem item,
        string supportedPattern,
        string supportedAsset,
        string supportedTimeframe,
        string requiredDataset,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyValidationReadinessAnalyzerReport? readiness,
        List<string> blockers)
    {
        var hasDataset = latestSuccess is not null && item.Asset.Equals(supportedAsset, StringComparison.OrdinalIgnoreCase) && item.Timeframe.Equals(supportedTimeframe, StringComparison.OrdinalIgnoreCase);
        var supportedByEngine = item.Asset.Equals(supportedAsset, StringComparison.OrdinalIgnoreCase)
            && item.Timeframe.Equals(supportedTimeframe, StringComparison.OrdinalIgnoreCase)
            && item.StrategyPattern.Equals(supportedPattern, StringComparison.OrdinalIgnoreCase)
            && item.MutationType is "session_filter_sharpen" or "volatility_filter_add" or "trend_filter_add" or "range_regime_enforce" or "entry_zone_narrow" or "invalidate_earlier" or "timeframe_alternative" or "parameter_range_refine";
        var concrete = !string.IsNullOrWhiteSpace(item.MutationType) && !string.IsNullOrWhiteSpace(item.Reason);
        var safetyOk = true;

        if (!hasDataset)
        {
            blockers.Add("waiting_for_data");
        }

        if (!supportedByEngine)
        {
            blockers.Add("waiting_for_engine_support");
        }

        if (!concrete)
        {
            blockers.Add("waiting_for_specification");
        }

        if (!safetyOk)
        {
            blockers.Add("blocked");
        }

        return blockers.Count switch
        {
            0 => "ready_to_execute",
            _ when blockers.Contains("waiting_for_data") => "waiting_for_data",
            _ when blockers.Contains("waiting_for_engine_support") => "waiting_for_engine_support",
            _ when blockers.Contains("waiting_for_specification") => "waiting_for_specification",
            _ => "blocked"
        };
    }

    private static IReadOnlyList<string> BuildSourceReports(
        MutationCandidateQueueReport mutationQueue,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyValidationReadinessAnalyzerReport? readiness)
    {
        var sources = new List<string> { mutationQueue.ReportPath };
        if (latestSuccess is not null)
        {
            sources.Add("/mnt/d/HermesData/reports/strategy_backtest_execution/strategy_backtest_latest_success.json");
        }

        if (failureLearning is not null)
        {
            sources.Add("/mnt/d/HermesData/reports/strategy_backtest_failure_learning/strategy_backtest_failure_learning.json");
        }

        if (qualityAudit is not null)
        {
            sources.Add("/mnt/d/HermesData/reports/strategy_backtest_quality/strategy_backtest_quality_audit.json");
        }

        if (readiness is not null)
        {
            sources.Add("/mnt/d/HermesData/reports/strategy_validation_readiness/strategy_validation_readiness_analyzer.json");
        }

        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildWarnings(
        MutationCandidateQueueReport mutationQueue,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyValidationReadinessAnalyzerReport? readiness)
    {
        var warnings = new List<string>();
        if (latestSuccess is null)
        {
            warnings.Add("no_successful_backtest_found");
        }

        if (failureLearning is null)
        {
            warnings.Add("failure_learning_report_missing");
        }

        if (qualityAudit is null)
        {
            warnings.Add("quality_audit_missing");
        }

        if (readiness is null)
        {
            warnings.Add("strategy_validation_readiness_missing");
        }

        if (mutationQueue.QueueSize == 0)
        {
            warnings.Add("no_mutation_candidates_found");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildOperatorSummary(IReadOnlyList<MutationValidationJobPlan> jobs)
    {
        var ready = jobs.Count(job => job.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase));
        var waiting = jobs.Count(job => job.ReadinessStatus.StartsWith("waiting_", StringComparison.OrdinalIgnoreCase));
        var blocked = jobs.Count(job => job.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase));
        return $"{jobs.Count} Mutationen analysiert. {ready} Jobs bereit. {waiting} warten. {blocked} blockiert. Frank nötig: nein.";
    }

    private static string BuildNextSafeStep(IReadOnlyList<MutationValidationJobPlan> jobs)
    {
        var ready = jobs.FirstOrDefault(job => job.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase));
        return ready is not null
            ? $"Nächster sicherer Schritt: {ready.ValidationJobId} für kontrollierte Validation vormerken."
            : "Nächster sicherer Schritt: Datensatz- und Engine-Support für die ersten Mutation Jobs schließen.";
    }

    private static int PriorityRank(string priority)
        => priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 0
            : priority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private void WriteArtifacts(MutationValidationJobPlannerReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(MutationValidationJobPlannerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mutation Validation Job Planner");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Mutations analyzed: {report.MutationsAnalyzed}");
        sb.AppendLine($"- Jobs prepared: {report.JobsPrepared}");
        sb.AppendLine($"- Ready to execute: {report.ReadyToExecuteCount}");
        sb.AppendLine($"- Waiting for data: {report.WaitingForDataCount}");
        sb.AppendLine($"- Waiting for engine support: {report.WaitingForEngineSupportCount}");
        sb.AppendLine($"- Waiting for specification: {report.WaitingForSpecificationCount}");
        sb.AppendLine($"- Blocked: {report.BlockedCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Jobs");
        foreach (var job in report.Jobs)
        {
            sb.AppendLine($"- {job.ValidationJobId} [{job.Priority}] {job.ReadinessStatus}");
            sb.AppendLine($"  - Mutation: {job.MutationType}");
            sb.AppendLine($"  - Reasonable blockers: {string.Join(", ", job.Blockers)}");
        }
        return sb.ToString();
    }
}
