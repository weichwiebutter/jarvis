using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotEvolutionBaselineReport(
    string ReportVersion,
    DateTimeOffset SavedAtUtc,
    decimal Score,
    string ExportId,
    string? EmbeddedChecksum,
    string? SignalPackageVersion,
    string Notes,
    string SourceReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class BotEvolutionBaselineService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotEvolutionBaselineService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_evolution_baseline");
    public string ReportPath => Path.Combine(Root, "bot_evolution_baseline.json");
    public string MarkdownPath => Path.Combine(Root, "bot_evolution_baseline.md");

    public BotEvolutionBaselineReport Save(string? notes = null)
    {
        Directory.CreateDirectory(Root);

        var scoreService = new BotEvolutionScoreService(_storagePaths, _runtimeRoot);
        var scoreReport = scoreService.LoadLatestReport() ?? scoreService.Run();
        var botVersionService = new BotVersionRecommendationMonitorService(_storagePaths, _runtimeRoot);
        var versionReport = botVersionService.Run();

        var report = new BotEvolutionBaselineReport(
            ReportVersion: "bot_evolution_baseline_v1",
            SavedAtUtc: DateTimeOffset.UtcNow,
            Score: scoreReport.EvolutionScore,
            ExportId: versionReport.CurrentExportId,
            EmbeddedChecksum: versionReport.CurrentEmbeddedChecksum,
            SignalPackageVersion: versionReport.CurrentSignalPackageVersion,
            Notes: string.IsNullOrWhiteSpace(notes) ? "baseline_saved_from_current_evolution_score" : notes.Trim(),
            SourceReportPath: scoreService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));

        new BotEvolutionHistoryService(_storagePaths, _runtimeRoot).AppendFromCurrentBaseline(report.Notes);
        return report;
    }

    public BotEvolutionBaselineReport? LoadLatest()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotEvolutionBaselineReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string BuildMarkdown(BotEvolutionBaselineReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bot Evolution Baseline");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- saved_at_utc: {report.SavedAtUtc:O}");
        sb.AppendLine($"- score: {report.Score:0.0}");
        sb.AppendLine($"- export_id: {report.ExportId}");
        sb.AppendLine($"- embedded_checksum: {report.EmbeddedChecksum ?? "-"}");
        sb.AppendLine($"- signal_package_version: {report.SignalPackageVersion ?? "-"}");
        sb.AppendLine($"- notes: {report.Notes}");
        sb.AppendLine($"- source_report_path: {report.SourceReportPath}");
        return sb.ToString();
    }
}
