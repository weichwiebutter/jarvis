using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PaperSignalExplainItem(
    string SignalId,
    decimal Confidence,
    decimal ConfidenceThreshold,
    bool SessionAllowed,
    bool SpreadAllowed,
    string Direction,
    bool EntryConditionMet,
    bool StopLossReady,
    bool TakeProfitReady,
    string DecisionReason,
    string LifecycleState);

public sealed record PaperSignalExplainReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ExplainedSignals,
    IReadOnlyList<PaperSignalExplainItem> Signals,
    IReadOnlyList<string> Warnings,
    string SignalEvaluationReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperSignalExplainService
{
    private const decimal ConfidenceThreshold = 0.60m;

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperSignalExplainService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_signal_explain");
    public string ReportPath => Path.Combine(Root, "paper_signal_explain.json");
    public string MarkdownPath => Path.Combine(Root, "paper_signal_explain.md");

    public PaperSignalExplainReport Run()
    {
        Directory.CreateDirectory(Root);

        var signalEvaluationService = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot);
        var signalReport = signalEvaluationService.LoadLatestReport();
        var warnings = new List<string>();
        if (signalReport is null)
        {
            warnings.Add("signal_evaluation_report_missing");
            var emptyReport = new PaperSignalExplainReport(
                ReportVersion: "paper_signal_explain_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "partial",
                ExplainedSignals: 0,
                Signals: [],
                Warnings: warnings,
                SignalEvaluationReportPath: signalEvaluationService.ReportPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath);
            WriteReport(emptyReport);
            return emptyReport;
        }

        var items = signalReport.Signals.Select(explain => BuildItem(explain)).ToList();
        var report = new PaperSignalExplainReport(
            ReportVersion: "paper_signal_explain_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count > 0 ? "ready" : "partial",
            ExplainedSignals: items.Count,
            Signals: items,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SignalEvaluationReportPath: signalEvaluationService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public PaperSignalExplainReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaperSignalExplainReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static PaperSignalExplainItem BuildItem(PaperSignalEvaluationItem item)
    {
        var confidence = item.ConfidenceBaseline;
        var confidenceThreshold = ConfidenceThreshold;
        var entryConditionMet = string.Equals(item.SignalStatus, "active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.SignalLifecycleStatus, "would_trigger", StringComparison.OrdinalIgnoreCase);
        var stopLossReady = item.Warnings.All(warning => !warning.Contains("stop_loss_missing", StringComparison.OrdinalIgnoreCase));
        var takeProfitReady = item.Warnings.All(warning => !warning.Contains("take_profit_missing", StringComparison.OrdinalIgnoreCase));

        var decisionReason = DetermineDecisionReason(item, confidence, confidenceThreshold, entryConditionMet);
        return new PaperSignalExplainItem(
            SignalId: item.SignalId,
            Confidence: confidence,
            ConfidenceThreshold: confidenceThreshold,
            SessionAllowed: item.SessionAllowed,
            SpreadAllowed: item.SpreadAllowed,
            Direction: item.Direction,
            EntryConditionMet: entryConditionMet,
            StopLossReady: stopLossReady,
            TakeProfitReady: takeProfitReady,
            DecisionReason: decisionReason,
            LifecycleState: item.SignalLifecycleStatus);
    }

    private static string DetermineDecisionReason(PaperSignalEvaluationItem item, decimal confidence, decimal confidenceThreshold, bool entryConditionMet)
    {
        if (item.SignalInvalidated)
        {
            return item.Warnings.FirstOrDefault(warning => warning.Contains("paper_entry_disabled", StringComparison.OrdinalIgnoreCase))
                ?? "invalidated";
        }

        if (item.SignalExpired)
        {
            return "expired";
        }

        if (!item.SessionAllowed)
        {
            return item.Warnings.FirstOrDefault(warning => warning.Contains("session", StringComparison.OrdinalIgnoreCase))
                ?? "skipped_session";
        }

        if (!item.SpreadAllowed)
        {
            return item.Warnings.FirstOrDefault(warning => warning.Contains("spread", StringComparison.OrdinalIgnoreCase))
                ?? "skipped_spread";
        }

        if (confidence < confidenceThreshold)
        {
            return "would_wait_low_confidence";
        }

        if (!entryConditionMet)
        {
            return "waiting";
        }

        return "would_trigger";
    }

    private void WriteReport(PaperSignalExplainReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperSignalExplainReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Signal Explainability");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- explained_signals: {report.ExplainedSignals}");
        sb.AppendLine($"- signal_evaluation_report_path: {report.SignalEvaluationReportPath}");
        sb.AppendLine();
        sb.AppendLine("## Signals");
        foreach (var signal in report.Signals)
        {
            sb.AppendLine($"- {signal.SignalId}: lifecycle={signal.LifecycleState}; decision_reason={signal.DecisionReason}; confidence={signal.Confidence:0.###}; threshold={signal.ConfidenceThreshold:0.###}; session_allowed={signal.SessionAllowed}; spread_allowed={signal.SpreadAllowed}; direction={signal.Direction}; entry_condition_met={signal.EntryConditionMet}; stop_loss_ready={signal.StopLossReady}; take_profit_ready={signal.TakeProfitReady}");
        }

        if (report.Signals.Count == 0)
        {
            sb.AppendLine("- none");
        }

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
