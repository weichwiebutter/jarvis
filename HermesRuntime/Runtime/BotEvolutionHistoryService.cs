using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotEvolutionHistoryEntry(
    string ExportId,
    DateTimeOffset TimestampUtc,
    decimal EvolutionScore,
    decimal? PreviousScore,
    decimal? ImprovementDelta,
    string Recommendation,
    string ConfidenceLevel,
    string? EmbeddedChecksum,
    string? SignalPackageVersion);

public sealed record BotEvolutionHistoryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int EntryCount,
    decimal? BestScore,
    decimal? WorstScore,
    decimal? AverageScore,
    decimal? BiggestImprovement,
    decimal? BiggestRegression,
    string Trend,
    IReadOnlyList<BotEvolutionHistoryEntry> Entries,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class BotEvolutionHistoryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotEvolutionHistoryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_evolution_history");
    public string ReportPath => Path.Combine(Root, "bot_evolution_history.json");
    public string MarkdownPath => Path.Combine(Root, "bot_evolution_history.md");

    private string BaselinePath => Path.Combine(_storagePaths.Root, "reports", "bot_evolution_baseline", "bot_evolution_baseline.json");

    public BotEvolutionHistoryReport Run()
    {
        Directory.CreateDirectory(Root);

        var entries = LoadEntries();
        var scores = entries.Select(item => item.EvolutionScore).ToList();
        var deltas = entries.Select(item => item.ImprovementDelta).Where(value => value.HasValue).Select(value => value!.Value).ToList();
        decimal? bestScore = scores.Count == 0 ? (decimal?)null : scores.Max();
        decimal? worstScore = scores.Count == 0 ? (decimal?)null : scores.Min();
        decimal? averageScore = scores.Count == 0 ? (decimal?)null : Math.Round(scores.Average(), 1);
        decimal? biggestImprovement = deltas.Count == 0 ? (decimal?)null : deltas.Max();
        decimal? biggestRegression = deltas.Count == 0 ? (decimal?)null : deltas.Min();
        var trend = DetermineTrend(deltas);

        var report = new BotEvolutionHistoryReport(
            ReportVersion: "bot_evolution_history_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: "ready",
            EntryCount: entries.Count,
            BestScore: bestScore.HasValue ? Math.Round(bestScore.Value, 1) : null,
            WorstScore: worstScore.HasValue ? Math.Round(worstScore.Value, 1) : null,
            AverageScore: averageScore,
            BiggestImprovement: biggestImprovement.HasValue ? Math.Round(biggestImprovement.Value, 1) : (decimal?)null,
            BiggestRegression: biggestRegression.HasValue ? Math.Round(biggestRegression.Value, 1) : (decimal?)null,
            Trend: trend,
            Entries: entries.OrderByDescending(item => item.TimestampUtc).ToList(),
            Warnings: [],
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public BotEvolutionHistoryReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotEvolutionHistoryReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public BotEvolutionHistoryEntry AppendFromCurrentBaseline(string? notes = null)
    {
        Directory.CreateDirectory(Root);

        var baselineService = new BotEvolutionBaselineService(_storagePaths, _runtimeRoot);
        var baseline = baselineService.LoadLatest() ?? baselineService.Save(notes);
        var history = LoadEntries().ToList();

        var entry = new BotEvolutionHistoryEntry(
            ExportId: baseline.ExportId,
            TimestampUtc: baseline.SavedAtUtc,
            EvolutionScore: baseline.Score,
            PreviousScore: null,
            ImprovementDelta: null,
            Recommendation: "baseline_saved",
            ConfidenceLevel: "baseline",
            EmbeddedChecksum: baseline.EmbeddedChecksum,
            SignalPackageVersion: baseline.SignalPackageVersion);

        if (!history.Any(existing => string.Equals(existing.ExportId, entry.ExportId, StringComparison.OrdinalIgnoreCase)
            && existing.TimestampUtc == entry.TimestampUtc
            && existing.EvolutionScore == entry.EvolutionScore))
        {
            history.Add(entry);
        }

        SaveEntries(history);
        Run();
        return entry;
    }

    private IReadOnlyList<BotEvolutionHistoryEntry> LoadEntries()
    {
        var entries = new List<BotEvolutionHistoryEntry>();

        if (File.Exists(BaselinePath))
        {
            try
            {
                var baseline = JsonSerializer.Deserialize<BotEvolutionBaselineReport>(File.ReadAllText(BaselinePath), JsonDefaults.SnapshotReadOptions);
                if (baseline is not null)
                {
                    entries.Add(new BotEvolutionHistoryEntry(
                        ExportId: baseline.ExportId,
                        TimestampUtc: baseline.SavedAtUtc,
                        EvolutionScore: baseline.Score,
                        PreviousScore: null,
                        ImprovementDelta: null,
                        Recommendation: "baseline_saved",
                        ConfidenceLevel: "baseline",
                        EmbeddedChecksum: baseline.EmbeddedChecksum,
                        SignalPackageVersion: baseline.SignalPackageVersion));
                }
            }
            catch
            {
                // ignore malformed baseline
            }
        }

        if (File.Exists(ReportPath))
        {
            try
            {
                var report = JsonSerializer.Deserialize<BotEvolutionHistoryReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
                if (report?.Entries is not null)
                {
                    entries.AddRange(report.Entries);
                }
            }
            catch
            {
                // ignore malformed history report
            }
        }

        return entries
            .GroupBy(item => new { item.ExportId, item.TimestampUtc, item.EvolutionScore })
            .Select(group => group.First())
            .ToList();
    }

    private void SaveEntries(IReadOnlyList<BotEvolutionHistoryEntry> entries)
    {
        var report = new BotEvolutionHistoryReport(
            ReportVersion: "bot_evolution_history_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: "ready",
            EntryCount: entries.Count,
            BestScore: entries.Count == 0 ? null : Math.Round(entries.Max(item => item.EvolutionScore), 1),
            WorstScore: entries.Count == 0 ? null : Math.Round(entries.Min(item => item.EvolutionScore), 1),
            AverageScore: entries.Count == 0 ? null : Math.Round(entries.Average(item => item.EvolutionScore), 1),
            BiggestImprovement: entries.Count == 0 ? (decimal?)null : entries.Where(item => item.ImprovementDelta.HasValue).Select(item => item.ImprovementDelta!.Value).DefaultIfEmpty().Max(),
            BiggestRegression: entries.Count == 0 ? (decimal?)null : entries.Where(item => item.ImprovementDelta.HasValue).Select(item => item.ImprovementDelta!.Value).DefaultIfEmpty().Min(),
            Trend: DetermineTrend(entries.Where(item => item.ImprovementDelta.HasValue).Select(item => item.ImprovementDelta!.Value).ToList()),
            Entries: entries.OrderByDescending(item => item.TimestampUtc).ToList(),
            Warnings: [],
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string DetermineTrend(IReadOnlyList<decimal> deltas)
    {
        if (deltas.Count == 0)
        {
            return "stable";
        }

        var positive = deltas.Count(delta => delta > 0m);
        var negative = deltas.Count(delta => delta < 0m);
        if (positive > negative)
        {
            return "improving";
        }

        if (negative > positive)
        {
            return "declining";
        }

        return "stable";
    }

    private static string BuildMarkdown(BotEvolutionHistoryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bot Evolution History");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- entry_count: {report.EntryCount}");
        sb.AppendLine($"- best_score: {report.BestScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- worst_score: {report.WorstScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- average_score: {report.AverageScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- biggest_improvement: {report.BiggestImprovement?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- biggest_regression: {report.BiggestRegression?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- trend: {report.Trend}");
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in report.Entries)
        {
            sb.AppendLine($"- export_id: {entry.ExportId}");
            sb.AppendLine($"  - timestamp: {entry.TimestampUtc:O}");
            sb.AppendLine($"  - evolution_score: {entry.EvolutionScore:0.0}");
            sb.AppendLine($"  - previous_score: {entry.PreviousScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
            sb.AppendLine($"  - improvement_delta: {entry.ImprovementDelta?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
            sb.AppendLine($"  - recommendation: {entry.Recommendation}");
            sb.AppendLine($"  - confidence_level: {entry.ConfidenceLevel}");
            sb.AppendLine($"  - embedded_checksum: {entry.EmbeddedChecksum ?? "-"}");
            sb.AppendLine($"  - signal_package_version: {entry.SignalPackageVersion ?? "-"}");
        }

        return sb.ToString();
    }
}
