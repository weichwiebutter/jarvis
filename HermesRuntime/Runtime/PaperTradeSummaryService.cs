using System;
using System.Linq;
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
    int PaperClosedCount,
    decimal GrossProfitR,
    decimal GrossLossR,
    decimal NetR,
    decimal AverageRMultiple,
    bool BrokerActionNone,
    bool PaperOnly,
    string PaperRuntimeStepReportPath,
    string PaperPositionLifecycleReportPath,
    string PaperStateSnapshotPath,
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
        var snapshotSummary = LoadSnapshotSummary(stepReport.PaperStateSnapshotPath);

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
            PaperClosedCount: snapshotSummary.ClosedCount,
            GrossProfitR: snapshotSummary.GrossProfitR,
            GrossLossR: snapshotSummary.GrossLossR,
            NetR: snapshotSummary.NetR,
            AverageRMultiple: snapshotSummary.AverageRMultiple,
            BrokerActionNone: string.Equals(stepReport.RuntimeStepResult.BrokerAction, "none", StringComparison.OrdinalIgnoreCase),
            PaperOnly: true,
            PaperRuntimeStepReportPath: stepService.ReportPath,
            PaperPositionLifecycleReportPath: lifecycleService.ReportPath,
            PaperStateSnapshotPath: stepReport.PaperStateSnapshotPath,
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
        sb.AppendLine($"- paper_closed_count: {report.PaperClosedCount}");
        sb.AppendLine($"- gross_profit_r: {report.GrossProfitR:0.####}");
        sb.AppendLine($"- gross_loss_r: {report.GrossLossR:0.####}");
        sb.AppendLine($"- net_r: {report.NetR:0.####}");
        sb.AppendLine($"- average_r_multiple: {report.AverageRMultiple:0.####}");
        sb.AppendLine($"- broker_action: {(report.BrokerActionNone ? "none" : "not_none")}");
        sb.AppendLine($"- paper_only: {report.PaperOnly.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath}");
        sb.AppendLine($"- paper_position_lifecycle_report_path: {report.PaperPositionLifecycleReportPath}");
        sb.AppendLine($"- paper_state_snapshot_path: {report.PaperStateSnapshotPath}");

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

    private SnapshotSummary LoadSnapshotSummary(string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return SnapshotSummary.Empty;
        }

        try
        {
            var restore = new HermesPaperBot.Services.PaperStateStore(snapshotPath).Load();
            var positions = restore.PaperPortfolioState?.ClosedTrades ?? [];
            var closedPositions = positions.Where(position => position.ClosedAtUtc.HasValue || position.Lifecycle != HermesPaperBot.Models.PaperTradeLifecycle.Open).ToArray();
            if (closedPositions.Length == 0)
            {
                return SnapshotSummary.Empty;
            }

            var closedR = closedPositions.Select(position => position.RMultiple == 0m
                ? ComputeRMultiple(position)
                : position.RMultiple).ToArray();
            var grossProfit = closedR.Where(r => r > 0m).Sum();
            var grossLoss = Math.Abs(closedR.Where(r => r < 0m).Sum());
            var net = closedR.Sum();
            var average = closedR.Length == 0 ? 0m : Math.Round(closedR.Average(), 4);

            return new SnapshotSummary(closedPositions.Length, Math.Round(grossProfit, 4), Math.Round(grossLoss, 4), Math.Round(net, 4), average);
        }
        catch
        {
            return SnapshotSummary.Empty;
        }
    }

    private static decimal ComputeRMultiple(HermesPaperBot.Models.PaperPosition position)
    {
        var risk = Math.Max(Math.Abs(position.EntryPrice - position.StopLossPrice), 0.0001m);
        return string.Equals(position.Direction, "short", StringComparison.OrdinalIgnoreCase)
            ? (position.EntryPrice - position.ExitPrice) / risk
            : (position.ExitPrice - position.EntryPrice) / risk;
    }

    private sealed record SnapshotSummary(int ClosedCount, decimal GrossProfitR, decimal GrossLossR, decimal NetR, decimal AverageRMultiple)
    {
        public static SnapshotSummary Empty { get; } = new(0, 0m, 0m, 0m, 0m);
    }
}
