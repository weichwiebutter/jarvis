using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RuntimeStabilityWindowSummary(
    string Window,
    int TotalEntries,
    double ArbeitetPercent,
    double WartetPercent,
    double FrankNoetigPercent,
    double FehlerPercent,
    int StatusChanges,
    string LongestErrorPhase,
    string LongestWaitPhase,
    int FrankEscalations);

public sealed record RuntimeStabilityAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    RuntimeStabilityWindowSummary Last24Hours,
    RuntimeStabilityWindowSummary Last7Days,
    RuntimeStabilityWindowSummary? Last30Days,
    string OperatorSummary,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class RuntimeStabilityAuditService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public RuntimeStabilityAuditService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "runtime_stability_audit");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "runtime_stability_audit.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "runtime_stability_audit.md");

    public RuntimeStabilityAuditReport Run()
    {
        Directory.CreateDirectory(Root);

        var historyService = new RuntimeHealthHistoryService(_storagePaths, _runtimeRoot);
        var entries = historyService.LoadEntries();
        if (entries.Count == 0)
        {
            entries = [historyService.AppendFromSummary()];
        }

        var now = entries.MaxBy(entry => entry.TimestampUtc)?.TimestampUtc ?? DateTimeOffset.UtcNow;
        var summary24h = BuildWindowSummary(entries.Where(entry => entry.TimestampUtc >= now.AddHours(-24)).ToList(), "24h");
        var summary7d = BuildWindowSummary(entries.Where(entry => entry.TimestampUtc >= now.AddDays(-7)).ToList(), "7d");
        var summary30d = entries.Any(entry => entry.TimestampUtc >= now.AddDays(-30))
            ? BuildWindowSummary(entries.Where(entry => entry.TimestampUtc >= now.AddDays(-30)).ToList(), "30d")
            : null;

        var report = new RuntimeStabilityAuditReport(
            ReportVersion: "runtime_stability_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Last24Hours: summary24h,
            Last7Days: summary7d,
            Last30Days: summary30d,
            OperatorSummary: BuildOperatorSummary(summary7d),
            SourceReports: BuildSourceReports(),
            Warnings: [],
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public RuntimeStabilityAuditReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RuntimeStabilityAuditReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static RuntimeStabilityWindowSummary BuildWindowSummary(IReadOnlyList<RuntimeHealthHistoryEntry> entries, string window)
    {
        if (entries.Count == 0)
        {
            return new RuntimeStabilityWindowSummary(window, 0, 0, 0, 0, 0, 0, "0h 0m", "0h 0m", 0);
        }

        var sorted = entries.OrderBy(entry => entry.TimestampUtc).ToList();
        var total = sorted.Count;
        var arbeite = sorted.Count(entry => entry.MainStatus == "arbeitet");
        var warte = sorted.Count(entry => entry.MainStatus == "wartet");
        var frank = sorted.Count(entry => entry.MainStatus == "frank_noetig");
        var fehler = sorted.Count(entry => entry.MainStatus == "fehler");
        var transitions = CountStatusChanges(sorted);
        var longestError = LongestPhase(sorted, "fehler");
        var longestWait = LongestPhase(sorted, "wartet");
        var frankEscalations = CountEscalations(sorted);

        return new RuntimeStabilityWindowSummary(
            Window: window,
            TotalEntries: total,
            ArbeitetPercent: Percent(arbeite, total),
            WartetPercent: Percent(warte, total),
            FrankNoetigPercent: Percent(frank, total),
            FehlerPercent: Percent(fehler, total),
            StatusChanges: transitions,
            LongestErrorPhase: longestError,
            LongestWaitPhase: longestWait,
            FrankEscalations: frankEscalations);
    }

    private static int CountStatusChanges(IReadOnlyList<RuntimeHealthHistoryEntry> entries)
    {
        if (entries.Count <= 1)
        {
            return 0;
        }

        var changes = 0;
        for (var index = 1; index < entries.Count; index++)
        {
            if (!string.Equals(entries[index - 1].MainStatus, entries[index].MainStatus, StringComparison.OrdinalIgnoreCase))
            {
                changes++;
            }
        }

        return changes;
    }

    private static string LongestPhase(IReadOnlyList<RuntimeHealthHistoryEntry> entries, string status)
    {
        if (entries.Count == 0)
        {
            return "0h 0m";
        }

        var best = TimeSpan.Zero;
        var current = TimeSpan.Zero;
        var previous = entries[0];
        for (var index = 1; index < entries.Count; index++)
        {
            var currentEntry = entries[index];
            var delta = currentEntry.TimestampUtc - previous.TimestampUtc;
            if (string.Equals(previous.MainStatus, status, StringComparison.OrdinalIgnoreCase))
            {
                current += delta;
                if (current > best)
                {
                    best = current;
                }
            }
            else
            {
                current = TimeSpan.Zero;
            }

            previous = currentEntry;
        }

        return $"{(int)best.TotalHours}h {best.Minutes}m";
    }

    private static int CountEscalations(IReadOnlyList<RuntimeHealthHistoryEntry> entries)
    {
        var count = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].MainStatus != "frank_noetig")
            {
                continue;
            }

            if (index == 0 || entries[index - 1].MainStatus != "frank_noetig")
            {
                count++;
            }
        }

        return count;
    }

    private static double Percent(int part, int total)
        => total == 0 ? 0 : Math.Round(part * 100.0 / total, 2);

    private static string BuildOperatorSummary(RuntimeStabilityWindowSummary summary7d)
        => summary7d.TotalEntries == 0
            ? "Hermes hat noch keine Stabilitäts-Historie."
            : $"Hermes lief in den letzten 7 Tagen zu {summary7d.ArbeitetPercent + summary7d.WartetPercent:0.##} % stabil. {summary7d.FrankEscalations} Frank-Eskalation(en). Keine kritischen Fehler.";

    private static IReadOnlyList<string> BuildSourceReports() =>
    [
        "/mnt/d/HermesData/reports/runtime_health_history/runtime_health_history.jsonl",
        "/mnt/d/HermesData/reports/runtime_health_summary/runtime_health_summary.json",
        "/mnt/d/HermesData/reports/master-status/master_status.json"
    ];

    private void WriteArtifacts(RuntimeStabilityAuditReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(RuntimeStabilityAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Runtime Stability Audit");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        AppendWindow(sb, report.Last24Hours);
        AppendWindow(sb, report.Last7Days);
        if (report.Last30Days is not null)
        {
            AppendWindow(sb, report.Last30Days);
        }
        return sb.ToString();
    }

    private static void AppendWindow(StringBuilder sb, RuntimeStabilityWindowSummary summary)
    {
        sb.AppendLine($"## {summary.Window}");
        sb.AppendLine($"- arbeitet: {summary.ArbeitetPercent:0.##}%");
        sb.AppendLine($"- wartet: {summary.WartetPercent:0.##}%");
        sb.AppendLine($"- frank_noetig: {summary.FrankNoetigPercent:0.##}%");
        sb.AppendLine($"- fehler: {summary.FehlerPercent:0.##}%");
        sb.AppendLine($"- Statuswechsel: {summary.StatusChanges}");
        sb.AppendLine($"- längste Fehlerphase: {summary.LongestErrorPhase}");
        sb.AppendLine($"- längste Wartephase: {summary.LongestWaitPhase}");
        sb.AppendLine($"- Frank-Eskalationen: {summary.FrankEscalations}");
        sb.AppendLine();
    }
}
