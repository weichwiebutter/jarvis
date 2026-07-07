using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PaperTradeSummaryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int EvaluatedSignals,
    int WouldTriggerSignals,
    int PaperOpenCount,
    int PaperClosedTpCount,
    int PaperClosedSlCount,
    int PaperInvalidatedCount,
    bool BrokerActionNone,
    bool PaperOnly,
    string PaperRuntimeStepReportPath,
    string PaperPositionLifecycleReportPath,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings);

public sealed class PaperTradeSummaryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperTradeSummaryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_trade_summary");
    public string ReportPath => Path.Combine(Root, "paper_trade_summary.json");
    public string MarkdownPath => Path.Combine(Root, "paper_trade_summary.md");

    public PaperTradeSummaryReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperTradeSummaryReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    public PaperTradeSummaryReport Run()
    {
        Directory.CreateDirectory(Root);

        var stepService = new PaperRuntimeStepService(_storagePaths, _runtimeRoot);
        var lifecycleService = new PaperPositionLifecycleService(_storagePaths, _runtimeRoot);
        var stepReport = stepService.LoadLatestReport();
        var lifecycleReport = lifecycleService.LoadLatestReport();

        var warnings = new List<string>();
        if (!stepReport.RuntimeReady)
        {
            warnings.Add("paper_runtime_step_not_ready");
        }

        if (!string.Equals(stepReport.RuntimeStepResult.BrokerAction, "none", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("broker_action_not_none");
        }

        var summary = new PaperTradeSummaryReport(
            ReportVersion: "paper_trade_summary_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: stepReport.RuntimeReady ? "ready" : "partial",
            EvaluatedSignals: stepReport.EvaluatedSignals,
            WouldTriggerSignals: stepReport.WouldTriggerSignals,
            PaperOpenCount: lifecycleReport.PaperOpenCount,
            PaperClosedTpCount: lifecycleReport.PaperClosedTpCount,
            PaperClosedSlCount: lifecycleReport.PaperClosedSlCount,
            PaperInvalidatedCount: lifecycleReport.PaperInvalidatedCount,
            BrokerActionNone: string.Equals(stepReport.RuntimeStepResult.BrokerAction, "none", StringComparison.OrdinalIgnoreCase),
            PaperOnly: true,
            PaperRuntimeStepReportPath: stepService.ReportPath,
            PaperPositionLifecycleReportPath: lifecycleService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        WriteReport(summary);
        return summary;
    }

    private void WriteReport(PaperTradeSummaryReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperTradeSummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Trade Summary");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- evaluated_signals: {report.EvaluatedSignals}");
        sb.AppendLine($"- would_trigger_signals: {report.WouldTriggerSignals}");
        sb.AppendLine($"- paper_open_count: {report.PaperOpenCount}");
        sb.AppendLine($"- paper_closed_tp_count: {report.PaperClosedTpCount}");
        sb.AppendLine($"- paper_closed_sl_count: {report.PaperClosedSlCount}");
        sb.AppendLine($"- paper_invalidated_count: {report.PaperInvalidatedCount}");
        sb.AppendLine($"- broker_action: {(report.BrokerActionNone ? "none" : "not_none")}");
        sb.AppendLine($"- paper_only: {report.PaperOnly.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath}");
        sb.AppendLine($"- paper_position_lifecycle_report_path: {report.PaperPositionLifecycleReportPath}");

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
