using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ApprovedChartAnnotationPromotionPlanItem(
    string Asset,
    string SetupId,
    bool Approved,
    string Reviewer,
    DateTimeOffset ReviewTimestampUtc,
    string Comment,
    bool PromotedToEmbedded,
    IReadOnlyList<string> MissingFields,
    bool IsPromotable,
    string TargetEmbeddedAction);

public sealed record ApprovedChartAnnotationPromotionPlanReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ApprovedCount,
    int PromotableCount,
    int AlreadyPromotedCount,
    IReadOnlyList<ApprovedChartAnnotationPromotionPlanItem> Items,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    string AuditTrailPath);

public sealed class ApprovedChartAnnotationPromotionPlanService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ApprovedChartAnnotationPromotionPlanService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "approved_chart_annotation_promotion_plan");
    public string ReportPath => Path.Combine(Root, "approved_chart_annotation_promotion_plan.json");
    public string MarkdownPath => Path.Combine(Root, "approved_chart_annotation_promotion_plan.md");
    public string AuditTrailPath => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_review_decisions", "chart_annotation_review_decisions.jsonl");

    public ApprovedChartAnnotationPromotionPlanReport Run()
    {
        var warnings = new List<string>();
        var items = LoadApprovedItems(out var loadWarnings)
            .OrderByDescending(item => item.ReviewTimestampUtc)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();
        warnings.AddRange(loadWarnings);

        var report = new ApprovedChartAnnotationPromotionPlanReport(
            ReportVersion: "approved_chart_annotation_promotion_plan_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count > 0 ? "ready" : "empty",
            ApprovedCount: items.Count,
            PromotableCount: items.Count(item => item.IsPromotable),
            AlreadyPromotedCount: items.Count(item => item.PromotedToEmbedded),
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

    public ApprovedChartAnnotationPromotionPlanReport LoadLatestReport() => Run();

    private IReadOnlyList<ApprovedChartAnnotationPromotionPlanItem> LoadApprovedItems(out List<string> warnings)
    {
        warnings = [];
        var items = new List<ApprovedChartAnnotationPromotionPlanItem>();

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

                var artifact = LoadArtifact(entry.Asset, entry.SetupId, out var artifactWarnings);
                warnings.AddRange(artifactWarnings);
                var missingFields = CollectMissingFields(artifact);
                var promotedToEmbedded = artifact is not null && ReadBool(artifact.Value, "promoted_to_embedded", defaultValue: false);
                var isPromotable = missingFields.Count == 0 && !promotedToEmbedded;

                items.Add(new ApprovedChartAnnotationPromotionPlanItem(
                    Asset: entry.Asset,
                    SetupId: entry.SetupId,
                    Approved: true,
                    Reviewer: entry.Reviewer,
                    ReviewTimestampUtc: entry.ReviewTimestampUtc,
                    Comment: entry.Comment,
                    PromotedToEmbedded: promotedToEmbedded,
                    MissingFields: missingFields,
                    IsPromotable: isPromotable,
                    TargetEmbeddedAction: promotedToEmbedded
                        ? "already_promoted"
                        : (missingFields.Count == 0 ? "ready_for_embedded_promotion" : "needs_review")));
            }
            catch (JsonException)
            {
                warnings.Add("audit_trail_line_parse_failed");
            }
        }

        return items;
    }

    private JsonElement? LoadArtifact(string asset, string setupId, out List<string> warnings)
    {
        warnings = [];
        foreach (var path in EnumerateArtifactPaths())
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var artifactAsset = ReadString(root, "asset");
                var artifactSetup = ReadString(root, "setup_id");
                if (artifactAsset.Equals(asset, StringComparison.OrdinalIgnoreCase) && artifactSetup.Equals(setupId, StringComparison.OrdinalIgnoreCase))
                {
                    return root.Clone();
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                warnings.Add($"chart_annotation_review_artifact_read_failed:{Path.GetFileName(path)}:{ex.GetType().Name}");
            }
        }

        warnings.Add($"chart_annotation_review_artifact_missing:{asset}:{setupId}");
        return null;
    }

    private IEnumerable<string> EnumerateArtifactPaths()
    {
        var docRoot = Path.Combine(_runtimeRoot, "docs", "trading");
        if (Directory.Exists(docRoot))
        {
            foreach (var path in Directory.EnumerateFiles(docRoot, "*chart_annotation_review_artifact.json", SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }
        }
    }

    private static IReadOnlyList<string> CollectMissingFields(JsonElement? root)
    {
        if (root is null)
        {
            return ["artifact_missing"];
        }

        var value = root.Value;
        var fields = new List<string>();
        if (!HasAnyPriceSet(value, "entry", "proposed_entry")) fields.Add("entry");
        if (!HasAnyPriceSet(value, "sl", "proposed_sl")) fields.Add("sl");
        if (!HasAnyPriceSet(value, "tp1", "proposed_tp1")) fields.Add("tp1");
        if (!HasAnyPriceSet(value, "invalidation", "invalidation_level")) fields.Add("invalidation");
        if (ReadAnyDouble(value, "risk_reward") <= 0) fields.Add("risk_reward");
        return fields;
    }

    private static bool HasAnyPriceSet(JsonElement root, params string[] propertyNames)
        => propertyNames.Any(propertyName => TryGetDouble(root, propertyName, out var value) && value > 0);

    private static string ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue)
        => root.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : defaultValue;

    private static bool TryGetDouble(JsonElement root, string propertyName, out double value)
    {
        value = 0d;
        return root.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.Number
               && property.TryGetDouble(out value);
    }

    private static double ReadAnyDouble(JsonElement root, string propertyName)
        => TryGetDouble(root, propertyName, out var value) ? value : 0d;

    private static string BuildMarkdown(ApprovedChartAnnotationPromotionPlanReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Approved Chart Annotation Promotion Plan");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- approved_count: {report.ApprovedCount}");
        sb.AppendLine($"- promotable_count: {report.PromotableCount}");
        sb.AppendLine($"- already_promoted_count: {report.AlreadyPromotedCount}");
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
            sb.AppendLine($"- missing_fields: {string.Join(", ", item.MissingFields)}");
            sb.AppendLine($"- is_promotable: {item.IsPromotable.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- target_embedded_action: {item.TargetEmbeddedAction}");
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
