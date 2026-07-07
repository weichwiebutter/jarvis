using System.Globalization;
using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace Hermes.Runtime;

public sealed record PaperTradeHistoryItem(
    string SignalId,
    string Asset,
    string Direction,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string Outcome,
    decimal ResultPoints,
    decimal RMultiple);

public sealed record PaperTradeHistoryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ClosedTradeCount,
    IReadOnlyList<PaperTradeHistoryItem> ClosedTrades,
    IReadOnlyList<string> Warnings,
    string? PaperStateSnapshotPath,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperTradeHistoryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperTradeHistoryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_trade_history");
    public string ReportPath => Path.Combine(Root, "paper_trade_history.json");
    public string MarkdownPath => Path.Combine(Root, "paper_trade_history.md");

    public PaperTradeHistoryReport Run()
    {
        Directory.CreateDirectory(Root);

        var warnings = new List<string>();
        var snapshotPath = ResolveSnapshotPath(warnings);
        var closedTrades = LoadClosedTrades(snapshotPath, warnings);

        var report = new PaperTradeHistoryReport(
            ReportVersion: "paper_trade_history_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: snapshotPath is null ? "partial" : "ready",
            ClosedTradeCount: closedTrades.Count,
            ClosedTrades: closedTrades,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PaperStateSnapshotPath: snapshotPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public PaperTradeHistoryReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaperTradeHistoryReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private string? ResolveSnapshotPath(List<string> warnings)
    {
        var stepReport = new PaperRuntimeStepService(_storagePaths, _runtimeRoot).LoadLatestReport();
        if (!string.IsNullOrWhiteSpace(stepReport.PaperStateSnapshotPath) && File.Exists(stepReport.PaperStateSnapshotPath))
        {
            return stepReport.PaperStateSnapshotPath;
        }

        if (!string.IsNullOrWhiteSpace(stepReport.PaperStateSnapshotPath))
        {
            warnings.Add("paper_state_snapshot_missing");
        }

        var latestSnapshot = Directory.Exists(_storagePaths.Snapshots)
            ? Directory.EnumerateFiles(_storagePaths.Snapshots, "paper_state_snapshot.json", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (latestSnapshot is not null)
        {
            return latestSnapshot.FullName;
        }

        warnings.Add("paper_state_snapshot_not_found");
        return null;
    }

    private static IReadOnlyList<PaperTradeHistoryItem> LoadClosedTrades(string? snapshotPath, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return [];
        }

        try
        {
            var restore = new PaperStateStore(snapshotPath).Load();
            var trades = restore.PaperPortfolioState?.ClosedTrades ?? [];
            return trades
                .Select(MapTrade)
                .OrderByDescending(item => item.ClosedAtUtc ?? item.OpenedAtUtc)
                .ThenBy(item => item.SignalId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            warnings.Add($"paper_state_snapshot_read_failed:{ex.GetType().Name}");
            return [];
        }
    }

    private static PaperTradeHistoryItem MapTrade(PaperPosition position)
    {
        var resultPoints = position.ResultPoints != 0m
            ? position.ResultPoints
            : string.Equals(position.Direction, "short", StringComparison.OrdinalIgnoreCase)
                ? position.EntryPrice - position.ExitPrice
                : position.ExitPrice - position.EntryPrice;

        var risk = Math.Max(Math.Abs(position.EntryPrice - position.StopLossPrice), 0.0001m);
        var rMultiple = position.RMultiple != 0m
            ? position.RMultiple
            : string.Equals(position.Direction, "short", StringComparison.OrdinalIgnoreCase)
                ? (position.EntryPrice - position.ExitPrice) / risk
                : (position.ExitPrice - position.EntryPrice) / risk;

        return new PaperTradeHistoryItem(
            SignalId: position.SignalId,
            Asset: position.Asset,
            Direction: position.Direction,
            EntryPrice: position.EntryPrice,
            ExitPrice: position.ExitPrice,
            StopLossPrice: position.StopLossPrice,
            TakeProfitPrice: position.TakeProfitPrice,
            OpenedAtUtc: position.OpenedAtUtc,
            ClosedAtUtc: position.ClosedAtUtc,
            Outcome: NormalizeOutcome(position.Outcome, position.ExitReason),
            ResultPoints: Math.Round(resultPoints, 4),
            RMultiple: Math.Round(rMultiple, 4));
    }

    private static string NormalizeOutcome(string outcome, PaperExitReason exitReason)
    {
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            return outcome;
        }

        return exitReason switch
        {
            PaperExitReason.TakeProfitHit => "tp",
            PaperExitReason.StopLossHit => "sl",
            PaperExitReason.Expired => "expired",
            PaperExitReason.Invalidated => "invalidated",
            _ => "unknown",
        };
    }

    private void WriteReport(PaperTradeHistoryReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperTradeHistoryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Trade History");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- closed_trade_count: {report.ClosedTradeCount}");
        sb.AppendLine($"- paper_state_snapshot_path: {report.PaperStateSnapshotPath ?? "-"}");

        if (report.ClosedTrades.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Closed Trades");
            foreach (var trade in report.ClosedTrades)
            {
                sb.AppendLine($"- signal_id: {trade.SignalId}");
                sb.AppendLine($"  - asset: {trade.Asset}");
                sb.AppendLine($"  - direction: {trade.Direction}");
                sb.AppendLine($"  - entry_price: {trade.EntryPrice.ToString("0.#####", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"  - exit_price: {trade.ExitPrice.ToString("0.#####", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"  - stop_loss_price: {trade.StopLossPrice.ToString("0.#####", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"  - take_profit_price: {trade.TakeProfitPrice.ToString("0.#####", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"  - opened_at: {trade.OpenedAtUtc:O}");
                sb.AppendLine($"  - closed_at: {trade.ClosedAtUtc?.ToString("O") ?? "-"}");
                sb.AppendLine($"  - outcome: {trade.Outcome}");
                sb.AppendLine($"  - result_points: {trade.ResultPoints:0.####}");
                sb.AppendLine($"  - r_multiple: {trade.RMultiple:0.####}");
            }
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
