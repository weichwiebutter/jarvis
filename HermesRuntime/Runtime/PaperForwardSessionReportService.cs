using System.Globalization;
using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;

namespace Hermes.Runtime;

public sealed record PaperForwardSessionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    DateTimeOffset? SessionStart,
    DateTimeOffset? LastTimerSeen,
    int TimerTicks,
    int SignalCount,
    int OpenPositions,
    int ClosedTrades,
    decimal NetR,
    string LastDecision,
    string SafetyStatus,
    string BrokerAction,
    string? PaperRuntimeStepReportPath,
    string? PaperTradeSummaryReportPath,
    string? PaperTradeHistoryReportPath,
    string? PaperStateSnapshotPath,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperForwardSessionReportService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperForwardSessionReportService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_forward_session");
    public string ReportPath => Path.Combine(Root, "paper_forward_session_report.json");
    public string MarkdownPath => Path.Combine(Root, "paper_forward_session_report.md");

    public PaperForwardSessionReport Run()
    {
        Directory.CreateDirectory(Root);

        var warnings = new List<string>();
        var stepService = new PaperRuntimeStepService(_storagePaths, _runtimeRoot);
        var stepReport = stepService.LoadLatestReport();
        var summaryService = new PaperTradeSummaryService(_storagePaths, _runtimeRoot);
        var summaryReport = summaryService.LoadLatestReport() ?? summaryService.Run();
        var historyService = new PaperTradeHistoryService(_storagePaths, _runtimeRoot);
        var historyReport = historyService.LoadLatestReport() ?? historyService.Run();
        var snapshotPath = ResolveSnapshotPath(stepReport, historyReport, warnings);

        var (sessionStart, lastTimerSeen, timerTicks) = LoadTimerTimeline(stepReport, warnings);
        var report = new PaperForwardSessionReport(
            ReportVersion: "paper_forward_session_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: DetermineStatus(stepReport, summaryReport, historyReport),
            SessionStart: sessionStart,
            LastTimerSeen: lastTimerSeen,
            TimerTicks: timerTicks,
            SignalCount: stepReport.EvaluatedSignals,
            OpenPositions: stepReport.ActiveSignals,
            ClosedTrades: historyReport.ClosedTradeCount,
            NetR: summaryReport.NetR,
            LastDecision: stepReport.RuntimeStepResult?.PaperDecision ?? stepReport.PaperDecisionSummary,
            SafetyStatus: BuildSafetyStatus(stepReport, summaryReport),
            BrokerAction: stepReport.RuntimeStepResult?.BrokerAction ?? "none",
            PaperRuntimeStepReportPath: stepService.ReportPath,
            PaperTradeSummaryReportPath: summaryService.ReportPath,
            PaperTradeHistoryReportPath: historyService.ReportPath,
            PaperStateSnapshotPath: snapshotPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public PaperForwardSessionReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaperForwardSessionReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private string? ResolveSnapshotPath(PaperRuntimeStepReport stepReport, PaperTradeHistoryReport historyReport, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(stepReport.PaperStateSnapshotPath))
        {
            return stepReport.PaperStateSnapshotPath;
        }

        if (!string.IsNullOrWhiteSpace(historyReport.PaperStateSnapshotPath))
        {
            warnings.Add("snapshot_path_fallback_from_history");
            return historyReport.PaperStateSnapshotPath;
        }

        warnings.Add("paper_state_snapshot_missing");
        return null;
    }

    private static (DateTimeOffset? sessionStart, DateTimeOffset? lastTimerSeen, int timerTicks) LoadTimerTimeline(PaperRuntimeStepReport stepReport, List<string> warnings)
    {
        var logFiles = new List<string>();

        if (!string.IsNullOrWhiteSpace(stepReport.LogsPath))
        {
            logFiles.Add(Path.Combine(stepReport.LogsPath, "paper_runtime_step_log.jsonl"));
        }

        var ctraderTempRoot = "/tmp/ctrader-paper-bot-cloud-logs";
        var ctraderTempLog = Path.Combine(ctraderTempRoot, "paper_runtime_step_log.jsonl");
        if (!logFiles.Any(path => string.Equals(path, ctraderTempLog, StringComparison.OrdinalIgnoreCase)))
        {
            logFiles.Add(ctraderTempLog);
        }

        var timestamps = new List<DateTimeOffset>();
        var usedFallback = false;
        var foundAnyLog = false;
        try
        {
            foreach (var logFile in logFiles)
            {
                if (!File.Exists(logFile))
                {
                    continue;
                }

                foundAnyLog = true;
                if (string.Equals(logFile, ctraderTempLog, StringComparison.OrdinalIgnoreCase))
                {
                    usedFallback = true;
                }

                foreach (var line in File.ReadLines(logFile))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    using var document = JsonDocument.Parse(line);
                    if (!document.RootElement.TryGetProperty("entry_type", out var entryType)
                        || !string.Equals(entryType.GetString(), "timer", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (document.RootElement.TryGetProperty("timestamp_utc", out var timestamp) && DateTimeOffset.TryParse(timestamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                    {
                        timestamps.Add(parsed);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"timer_log_read_failed:{ex.GetType().Name}");
            return (null, null, 0);
        }

        if (timestamps.Count == 0)
        {
            warnings.Add(foundAnyLog ? "timer_log_no_timer_entries" : "timer_log_missing");
            return (null, null, 0);
        }

        if (usedFallback)
        {
            warnings.Add("used_ctrader_temp_log_path");
        }

        timestamps.Sort();
        return (timestamps.First(), timestamps.Last(), timestamps.Count);
    }

    private static string DetermineStatus(PaperRuntimeStepReport stepReport, PaperTradeSummaryReport summaryReport, PaperTradeHistoryReport historyReport)
    {
        if (stepReport.RuntimeReady && summaryReport.BrokerActionNone)
        {
            return "ready";
        }

        return "partial";
    }

    private static string BuildSafetyStatus(PaperRuntimeStepReport stepReport, PaperTradeSummaryReport summaryReport)
    {
        var runtimeReady = stepReport.RuntimeReady;
        var brokerActionNone = summaryReport.BrokerActionNone;
        return runtimeReady && brokerActionNone
            ? "safe"
            : "partial";
    }

    private void WriteReport(PaperForwardSessionReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperForwardSessionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Forward Session Report");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- session_start: {report.SessionStart?.ToString("O") ?? "-"}");
        sb.AppendLine($"- last_timer_seen: {report.LastTimerSeen?.ToString("O") ?? "-"}");
        sb.AppendLine($"- timer_ticks: {report.TimerTicks}");
        sb.AppendLine($"- signal_count: {report.SignalCount}");
        sb.AppendLine($"- open_positions: {report.OpenPositions}");
        sb.AppendLine($"- closed_trades: {report.ClosedTrades}");
        sb.AppendLine($"- net_r: {report.NetR:0.####}");
        sb.AppendLine($"- last_decision: {report.LastDecision}");
        sb.AppendLine($"- safety_status: {report.SafetyStatus}");
        sb.AppendLine($"- broker_action: {report.BrokerAction}");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath ?? "-"}");
        sb.AppendLine($"- paper_trade_summary_report_path: {report.PaperTradeSummaryReportPath ?? "-"}");
        sb.AppendLine($"- paper_trade_history_report_path: {report.PaperTradeHistoryReportPath ?? "-"}");
        sb.AppendLine($"- paper_state_snapshot_path: {report.PaperStateSnapshotPath ?? "-"}");

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
