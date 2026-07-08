using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PaperForwardEvaluationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TimerTicks,
    int SignalCount,
    int BrokerActionNoneCount,
    string SafetyStatus,
    int OpenPositions,
    int ClosedTrades,
    decimal NetR,
    string LastDecision,
    int ExpiredSignalCount,
    int InvalidatedSignalCount,
    int MissingRiskBoundsCount,
    string ForwardRunStatus,
    string RecommendedNextAction,
    string PaperRuntimeStepReportPath,
    string PaperTradeSummaryReportPath,
    string PaperForwardSessionReportPath,
    string PaperSignalExplainReportPath,
    string PaperSignalEvaluationReportPath,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperForwardEvaluationService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperForwardEvaluationService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_forward_evaluation");
    public string ReportPath => Path.Combine(Root, "paper_forward_evaluation.json");
    public string MarkdownPath => Path.Combine(Root, "paper_forward_evaluation.md");

    public PaperForwardEvaluationReport Run()
    {
        Directory.CreateDirectory(Root);

        var stepService = new PaperRuntimeStepService(_storagePaths, _runtimeRoot);
        var summaryService = new PaperTradeSummaryService(_storagePaths, _runtimeRoot);
        var forwardService = new PaperForwardSessionReportService(_storagePaths, _runtimeRoot);
        var explainService = new PaperSignalExplainService(_storagePaths, _runtimeRoot);
        var evaluationService = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot);

        var stepReport = stepService.LoadLatestReport();
        var summaryReport = summaryService.LoadLatestReport();
        var forwardReport = forwardService.LoadLatestReport();
        var explainReport = explainService.LoadLatestReport();
        var evaluationReport = evaluationService.LoadLatestReport();

        var warnings = new List<string>();
        if (stepReport is null)
        {
            warnings.Add("paper_runtime_step_report_missing");
        }

        if (evaluationReport is null)
        {
            warnings.Add("paper_signal_evaluation_report_missing");
        }

        var runtimeReady = stepReport?.RuntimeReady ?? false;
        var brokerActionNone = stepReport?.BrokerActionNone ?? summaryReport?.BrokerActionNone ?? false;
        var safetyStatus = forwardReport?.SafetyStatus ?? "unknown";

        var expiredSignalCount = evaluationReport?.ExpiredSignals ?? explainReport?.Signals.Count(signal => signal.LifecycleState.Equals("expired", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var invalidatedSignalCount = evaluationReport?.InvalidatedSignals ?? explainReport?.Signals.Count(signal => signal.LifecycleState.Equals("invalidated", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var missingRiskBoundsCount = explainReport?.Signals.Count(signal => signal.ConfidenceBlockers.Any(blocker =>
            blocker.Contains("missing_stop_loss", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("missing_take_profit", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("missing_confidence_baseline", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("missing_direction", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("missing_setup_id", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("missing_setup_name", StringComparison.OrdinalIgnoreCase)) ) ?? 0;

        var forwardRunStatus = DetermineForwardRunStatus(
            runtimeReady,
            brokerActionNone,
            safetyStatus,
            forwardReport?.TimerTicks ?? 0,
            forwardReport?.OpenPositions ?? 0,
            forwardReport?.ClosedTrades ?? 0,
            forwardReport?.NetR ?? 0m,
            expiredSignalCount,
            invalidatedSignalCount,
            missingRiskBoundsCount);

        var report = new PaperForwardEvaluationReport(
            ReportVersion: "paper_forward_evaluation_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: warnings.Count == 0 ? "ready" : "partial",
            TimerTicks: forwardReport?.TimerTicks ?? 0,
            SignalCount: forwardReport?.SignalCount ?? 0,
            BrokerActionNoneCount: brokerActionNone ? 1 : 0,
            SafetyStatus: safetyStatus,
            OpenPositions: forwardReport?.OpenPositions ?? 0,
            ClosedTrades: forwardReport?.ClosedTrades ?? 0,
            NetR: forwardReport?.NetR ?? 0m,
            LastDecision: forwardReport?.LastDecision ?? "unknown",
            ExpiredSignalCount: expiredSignalCount,
            InvalidatedSignalCount: invalidatedSignalCount,
            MissingRiskBoundsCount: missingRiskBoundsCount,
            ForwardRunStatus: forwardRunStatus,
            RecommendedNextAction: DetermineNextAction(forwardRunStatus, warnings),
            PaperRuntimeStepReportPath: stepService.ReportPath,
            PaperTradeSummaryReportPath: summaryService.ReportPath,
            PaperForwardSessionReportPath: forwardService.ReportPath,
            PaperSignalExplainReportPath: explainService.ReportPath,
            PaperSignalEvaluationReportPath: evaluationService.ReportPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public PaperForwardEvaluationReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaperForwardEvaluationReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string DetermineForwardRunStatus(
        bool runtimeReady,
        bool brokerActionNone,
        string safetyStatus,
        int timerTicks,
        int openPositions,
        int closedTrades,
        decimal netR,
        int expiredSignalCount,
        int invalidatedSignalCount,
        int missingRiskBoundsCount)
    {
        if (!runtimeReady || !brokerActionNone || !safetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase))
        {
            return "blocked";
        }

        if (timerTicks <= 0 || openPositions < 0 || closedTrades < 0)
        {
            return "warning";
        }

        if (invalidatedSignalCount > 0 || missingRiskBoundsCount > 0 || expiredSignalCount > 0 || netR < 0m)
        {
            return "warning";
        }

        return "green";
    }

    private static string DetermineNextAction(string forwardRunStatus, IReadOnlyList<string> warnings)
        => forwardRunStatus switch
        {
            "green" => "continue forward run and review the next session report",
            "warning" => "inspect expired, invalidated and risk-bound signals before continuing",
            _ => warnings.Count > 0
                ? "fix paper runtime or report readiness issues before the next forward run"
                : "review paper runtime safety and timer health",
        };

    private static string BuildMarkdown(PaperForwardEvaluationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Forward Evaluation Checklist");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- timer_ticks: {report.TimerTicks}");
        sb.AppendLine($"- signal_count: {report.SignalCount}");
        sb.AppendLine($"- broker_action_none_count: {report.BrokerActionNoneCount}");
        sb.AppendLine($"- safety_status: {report.SafetyStatus}");
        sb.AppendLine($"- open_positions: {report.OpenPositions}");
        sb.AppendLine($"- closed_trades: {report.ClosedTrades}");
        sb.AppendLine($"- net_r: {report.NetR:0.####}");
        sb.AppendLine($"- last_decision: {report.LastDecision}");
        sb.AppendLine($"- expired_signal_count: {report.ExpiredSignalCount}");
        sb.AppendLine($"- invalidated_signal_count: {report.InvalidatedSignalCount}");
        sb.AppendLine($"- missing_risk_bounds_count: {report.MissingRiskBoundsCount}");
        sb.AppendLine($"- forward_run_status: {report.ForwardRunStatus}");
        sb.AppendLine($"- recommended_next_action: {report.RecommendedNextAction}");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath}");
        sb.AppendLine($"- paper_trade_summary_report_path: {report.PaperTradeSummaryReportPath}");
        sb.AppendLine($"- paper_forward_session_report_path: {report.PaperForwardSessionReportPath}");
        sb.AppendLine($"- paper_signal_explain_report_path: {report.PaperSignalExplainReportPath}");
        sb.AppendLine($"- paper_signal_evaluation_report_path: {report.PaperSignalEvaluationReportPath}");

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
