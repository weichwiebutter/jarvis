using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ApprovedChartAnnotationRegistryItem(
    string Asset,
    string SetupId,
    bool Approved,
    string Reviewer,
    DateTimeOffset ReviewTimestampUtc,
    string Comment,
    bool PromotedToEmbedded,
    string DecisionId);

public sealed record ApprovedChartAnnotationRegistryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ApprovedCount,
    int TotalCount,
    IReadOnlyList<ApprovedChartAnnotationRegistryItem> Items,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    string AuditTrailPath);

public sealed class ApprovedChartAnnotationRegistryService
{
    private readonly StoragePaths _storagePaths;

    public ApprovedChartAnnotationRegistryService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "approved_chart_annotations");
    public string ReportPath => Path.Combine(Root, "approved_chart_annotations.json");
    public string MarkdownPath => Path.Combine(Root, "approved_chart_annotations.md");
    public string AuditTrailPath => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_review_decisions", "chart_annotation_review_decisions.jsonl");

    public ApprovedChartAnnotationRegistryReport Run()
    {
        var warnings = new List<string>();
        var items = LoadApprovedItems(out var loadWarnings)
            .OrderByDescending(item => item.ReviewTimestampUtc)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();
        warnings.AddRange(loadWarnings);

        var report = new ApprovedChartAnnotationRegistryReport(
            ReportVersion: "approved_chart_annotation_registry_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count > 0 ? "ready" : "empty",
            ApprovedCount: items.Count,
            TotalCount: items.Count,
            Items: items,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            AuditTrailPath: AuditTrailPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public ApprovedChartAnnotationRegistryReport LoadLatestReport()
        => Run();

    private IReadOnlyList<ApprovedChartAnnotationRegistryItem> LoadApprovedItems(out List<string> warnings)
    {
        warnings = [];
        var items = new List<ApprovedChartAnnotationRegistryItem>();

        if (!File.Exists(AuditTrailPath))
        {
            return items;
        }

        foreach (var line in File.ReadAllLines(AuditTrailPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ChartAnnotationReviewDecisionEntry>(line, JsonDefaults.SnapshotReadOptions);
                if (entry is null || !entry.Approved)
                {
                    continue;
                }

                items.Add(new ApprovedChartAnnotationRegistryItem(
                    Asset: entry.Asset,
                    SetupId: entry.SetupId,
                    Approved: true,
                    Reviewer: entry.Reviewer,
                    ReviewTimestampUtc: entry.ReviewTimestampUtc,
                    Comment: entry.Comment,
                    PromotedToEmbedded: entry.PromotedToEmbedded,
                    DecisionId: entry.DecisionId));
            }
            catch (JsonException)
            {
                warnings.Add("audit_trail_line_parse_failed");
            }
        }

        return items;
    }

    private static string BuildMarkdown(ApprovedChartAnnotationRegistryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Approved Chart Annotation Registry");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- approved_count: {report.ApprovedCount}");
        sb.AppendLine($"- total_count: {report.TotalCount}");
        sb.AppendLine($"- audit_trail_path: {report.AuditTrailPath}");
        sb.AppendLine();

        foreach (var item in report.Items)
        {
            sb.AppendLine($"## {item.Asset} / {item.SetupId}");
            sb.AppendLine($"- approved: {item.Approved.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- reviewer: {item.Reviewer}");
            sb.AppendLine($"- review_timestamp: {item.ReviewTimestampUtc:O}");
            sb.AppendLine($"- comment: {item.Comment}");
            sb.AppendLine($"- promoted_to_embedded: {item.PromotedToEmbedded.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- decision_id: {item.DecisionId}");
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
