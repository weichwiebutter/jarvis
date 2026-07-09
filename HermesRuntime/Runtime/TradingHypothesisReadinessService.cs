using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TradingHypothesisReadinessItem(
    string HypothesisId,
    string Status,
    int CurrentSampleSize,
    int RequiredSampleSize,
    string Readiness,
    string NextRequiredData);

public sealed record TradingHypothesisReadinessReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TotalHypotheses,
    int ReadyForValidationCount,
    int InsufficientDataCount,
    IReadOnlyList<TradingHypothesisReadinessItem> Items,
    IReadOnlyList<string> Warnings,
    string TradingHypothesesReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class TradingHypothesisReadinessService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TradingHypothesisReadinessService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trading_hypothesis_readiness");
    public string ReportPath => Path.Combine(Root, "trading_hypothesis_readiness_report.json");
    public string MarkdownPath => Path.Combine(Root, "trading_hypothesis_readiness_report.md");

    public TradingHypothesisReadinessReport Run()
    {
        Directory.CreateDirectory(Root);

        var hypothesisService = new TradingHypothesisService(_storagePaths, _runtimeRoot);
        var hypothesisReport = hypothesisService.LoadLatestReport() ?? hypothesisService.Run();

        var items = hypothesisReport.Hypotheses
            .Select(BuildItem)
            .OrderByDescending(item => item.Readiness.Equals("ready_for_validation", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.CurrentSampleSize)
            .ThenBy(item => item.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new TradingHypothesisReadinessReport(
            ReportVersion: "trading_hypothesis_readiness_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count > 0 ? "ready" : "empty",
            TotalHypotheses: items.Count,
            ReadyForValidationCount: items.Count(item => item.Readiness.Equals("ready_for_validation", StringComparison.OrdinalIgnoreCase)),
            InsufficientDataCount: items.Count(item => item.Readiness.Equals("insufficient_data", StringComparison.OrdinalIgnoreCase)),
            Items: items,
            Warnings: BuildWarnings(hypothesisReport),
            TradingHypothesesReportPath: hypothesisService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public TradingHypothesisReadinessReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TradingHypothesisReadinessReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static TradingHypothesisReadinessItem BuildItem(TradingHypothesis hypothesis)
    {
        var ready = hypothesis.CurrentSampleSize >= hypothesis.RequiredSampleSize;
        var readiness = ready ? "ready_for_validation" : "insufficient_data";
        var nextRequiredData = ready
            ? BuildValidationNextAction(hypothesis)
            : BuildDataCollectionNextAction(hypothesis);

        return new TradingHypothesisReadinessItem(
            HypothesisId: hypothesis.HypothesisId,
            Status: hypothesis.Status,
            CurrentSampleSize: hypothesis.CurrentSampleSize,
            RequiredSampleSize: hypothesis.RequiredSampleSize,
            Readiness: readiness,
            NextRequiredData: nextRequiredData);
    }

    private static string BuildDataCollectionNextAction(TradingHypothesis hypothesis)
    {
        var missing = Math.Max(0, hypothesis.RequiredSampleSize - hypothesis.CurrentSampleSize);
        if (missing <= 0)
        {
            return "validation_ready";
        }

        var parts = new List<string> { $"collect {missing} more samples" };
        if (hypothesis.RequiredManualActions.Count > 0)
        {
            parts.AddRange(hypothesis.RequiredManualActions);
        }

        return string.Join("; ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildValidationNextAction(TradingHypothesis hypothesis)
    {
        if (hypothesis.ValidationRequired)
        {
            return "run_validation_state_sync; review supporting evidence";
        }

        return "review the hypothesis and consider it for manual validation";
    }

    private static IReadOnlyList<string> BuildWarnings(TradingHypothesisReport hypothesisReport)
    {
        var warnings = new List<string>();
        if (hypothesisReport.Hypotheses.Count == 0)
        {
            warnings.Add("trading_hypotheses_report_empty");
        }

        return warnings;
    }

    private static string BuildMarkdown(TradingHypothesisReadinessReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trading Hypothesis Readiness");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- total_hypotheses: {report.TotalHypotheses}");
        sb.AppendLine($"- ready_for_validation_count: {report.ReadyForValidationCount}");
        sb.AppendLine($"- insufficient_data_count: {report.InsufficientDataCount}");
        sb.AppendLine();
        sb.AppendLine("## Sources");
        sb.AppendLine($"- trading_hypotheses_report_path: {report.TradingHypothesesReportPath}");

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Readiness");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"- hypothesis_id: {item.HypothesisId}");
            sb.AppendLine($"  - status: {item.Status}");
            sb.AppendLine($"  - current_sample_size: {item.CurrentSampleSize}");
            sb.AppendLine($"  - required_sample_size: {item.RequiredSampleSize}");
            sb.AppendLine($"  - readiness: {item.Readiness}");
            sb.AppendLine($"  - next_required_data: {item.NextRequiredData}");
        }

        return sb.ToString();
    }
}
