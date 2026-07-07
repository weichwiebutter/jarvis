using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace Hermes.Runtime;

public sealed record PaperPositionLifecycleItem(
    string SignalId,
    string Asset,
    string Timeframe,
    string Direction,
    string Status,
    string Entry,
    string Sl,
    string Tp,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string? PaperResult,
    string Reason);

public sealed record PaperPositionLifecycleReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int NoneCount,
    int PaperOpenCount,
    int PaperClosedTpCount,
    int PaperClosedSlCount,
    int PaperClosedExpiredCount,
    int PaperInvalidatedCount,
    IReadOnlyList<PaperPositionLifecycleItem> Positions,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperPositionLifecycleService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperPositionLifecycleService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_position_lifecycle");
    public string ReportPath => Path.Combine(Root, "paper_position_lifecycle.json");
    public string MarkdownPath => Path.Combine(Root, "paper_position_lifecycle.md");

    public PaperPositionLifecycleReport Run(PaperRuntimeStepReport? stepReport = null)
    {
        Directory.CreateDirectory(Root);
        stepReport ??= new PaperRuntimeStepService(_storagePaths, _runtimeRoot).LoadLatestReport();
        var warnings = new List<string>();

        if (!stepReport.RuntimeReady)
        {
            warnings.Add("paper_runtime_step_not_ready");
        }

        var positions = new List<PaperPositionLifecycleItem>();
        foreach (var signal in stepReport.SignalEvaluation.Signals)
        {
            var status = MapStatus(signal.SignalLifecycleStatus, signal.PaperDecision, signal.SignalStatus);
            var paperResult = MapPaperResult(status);
            positions.Add(new PaperPositionLifecycleItem(
                SignalId: signal.SignalId,
                Asset: signal.Asset,
                Timeframe: signal.Timeframe,
                Direction: signal.Direction,
                Status: status,
                Entry: signal.PaperDecision == "would_trigger" ? "signal_entry" : "n/a",
                Sl: signal.SignalStatus is "active" or "completed" ? "set" : "n/a",
                Tp: signal.SignalStatus is "active" or "completed" ? "set" : "n/a",
                OpenedAtUtc: signal.SignalStatus is "active" or "completed" ? stepReport.UpdatedAtUtc : null,
                ClosedAtUtc: signal.SignalStatus is "completed" or "expired" or "invalidated" ? stepReport.UpdatedAtUtc : null,
                PaperResult: paperResult,
                Reason: signal.Reason));
        }

        var report = new PaperPositionLifecycleReport(
            ReportVersion: "paper_position_lifecycle_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: stepReport.RuntimeReady ? "ready" : "partial",
            NoneCount: positions.Count(item => item.Status == "none"),
            PaperOpenCount: positions.Count(item => item.Status == "paper_open"),
            PaperClosedTpCount: positions.Count(item => item.Status == "paper_closed_tp"),
            PaperClosedSlCount: positions.Count(item => item.Status == "paper_closed_sl"),
            PaperClosedExpiredCount: positions.Count(item => item.Status == "paper_closed_expired"),
            PaperInvalidatedCount: positions.Count(item => item.Status == "paper_invalidated"),
            Positions: positions,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public PaperPositionLifecycleReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperPositionLifecycleReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    private static string MapStatus(string lifecycleStatus, string paperDecision, string signalStatus)
        => lifecycleStatus switch
        {
            "would_trigger" or "active" => "paper_open",
            "completed" when paperDecision.Contains("take_profit", StringComparison.OrdinalIgnoreCase) => "paper_closed_tp",
            "completed" when paperDecision.Contains("stop_loss", StringComparison.OrdinalIgnoreCase) => "paper_closed_sl",
            "completed" => "paper_closed_tp",
            "invalidated" => "paper_invalidated",
            "expired" => "paper_closed_expired",
            _ => "none",
        };

    private static string? MapPaperResult(string status)
        => status switch
        {
            "paper_open" => null,
            "paper_closed_tp" => "take_profit_hit",
            "paper_closed_sl" => "stop_loss_hit",
            "paper_closed_expired" => "expired",
            "paper_invalidated" => "invalidated",
            _ => null,
        };

    private void WriteReport(PaperPositionLifecycleReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperPositionLifecycleReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Position Lifecycle");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- none_count: {report.NoneCount}");
        sb.AppendLine($"- paper_open_count: {report.PaperOpenCount}");
        sb.AppendLine($"- paper_closed_tp_count: {report.PaperClosedTpCount}");
        sb.AppendLine($"- paper_closed_sl_count: {report.PaperClosedSlCount}");
        sb.AppendLine($"- paper_closed_expired_count: {report.PaperClosedExpiredCount}");
        sb.AppendLine($"- paper_invalidated_count: {report.PaperInvalidatedCount}");
        sb.AppendLine();
        sb.AppendLine("## Positions");
        foreach (var position in report.Positions)
        {
            sb.AppendLine($"- {position.SignalId}: status={position.Status}; asset={position.Asset}; timeframe={position.Timeframe}; direction={position.Direction}; entry={position.Entry}; sl={position.Sl}; tp={position.Tp}; result={position.PaperResult ?? "n/a"}; opened_at={position.OpenedAtUtc:O}; closed_at={(position.ClosedAtUtc.HasValue ? position.ClosedAtUtc.Value.ToString("O") : "n/a")}");
        }
        if (report.Positions.Count == 0)
        {
            sb.AppendLine("- none");
        }
        return sb.ToString();
    }
}
