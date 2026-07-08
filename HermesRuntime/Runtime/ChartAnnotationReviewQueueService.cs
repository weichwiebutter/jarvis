using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ChartAnnotationReviewQueueItem(
    string Asset,
    string SetupId,
    double ConfidenceBaseline,
    string Status,
    bool RequiresHumanReview,
    bool CanGenerateAnnotation,
    IReadOnlyList<string> MissingFields,
    string ReviewDecision,
    bool Approved,
    bool PromotedToEmbedded,
    string SourcePath);

public sealed record ChartAnnotationReviewQueueReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TotalCandidates,
    int ReadyForReview,
    int NeedsPriceReview,
    int ApprovedCount,
    int PendingCount,
    IReadOnlyList<ChartAnnotationReviewQueueItem> Items,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class ChartAnnotationReviewQueueService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ChartAnnotationReviewQueueService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_review_queue");
    public string ReportPath => Path.Combine(Root, "chart_annotation_review_queue.json");
    public string MarkdownPath => Path.Combine(Root, "chart_annotation_review_queue.md");

    public ChartAnnotationReviewQueueReport LoadLatestReport()
    {
        return Run();
    }

    public ChartAnnotationReviewQueueReport Run()
    {
        var warnings = new List<string>();
        var items = LoadReviewArtifacts(warnings)
            .OrderByDescending(item => item.CanGenerateAnnotation)
            .ThenByDescending(item => item.ConfidenceBaseline)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new ChartAnnotationReviewQueueReport(
            ReportVersion: "chart_annotation_review_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count > 0 ? "ready" : "empty",
            TotalCandidates: items.Count,
            ReadyForReview: items.Count(item => item.Status == "ready_for_review"),
            NeedsPriceReview: items.Count(item => item.Status == "needs_price_review"),
            ApprovedCount: items.Count(item => item.Approved),
            PendingCount: items.Count(item => item.ReviewDecision.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            Items: items,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private IReadOnlyList<ChartAnnotationReviewQueueItem> LoadReviewArtifacts(List<string> warnings)
    {
        var artifacts = new List<ChartAnnotationReviewQueueItem>();
        foreach (var path in EnumerateArtifactPaths())
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var artifactType = ReadString(root, "artifact_type");
                var source = ReadString(root, "source");
                if (!artifactType.Contains("chart_annotation_review_artifact", StringComparison.OrdinalIgnoreCase) &&
                    !source.Contains("embedded_spec_review", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = ReadString(root, "asset");
                var setupId = ReadString(root, "setup_id");
                var confidenceBaseline = ReadAnyDouble(root, "confidence_baseline");
                var status = ReadString(root, "status");
                var requiresHumanReview = ReadBool(root, "requires_human_review", defaultValue: true);
                var approved = ReadBool(root, "approved", defaultValue: false);
                var promotedToEmbedded = ReadBool(root, "promoted_to_embedded", defaultValue: false);
                var canGenerateAnnotation =
                    !status.Equals("needs_price_review", StringComparison.OrdinalIgnoreCase)
                    && HasAnyPriceSet(root, "entry", "proposed_entry")
                    && HasAnyPriceSet(root, "sl", "proposed_sl")
                    && HasAnyPriceSet(root, "tp1", "proposed_tp1")
                    && HasAnyPriceSet(root, "invalidation", "invalidation_level")
                    && ReadAnyDouble(root, "risk_reward") > 0;
                var missingFields = CollectMissingFields(root);

                artifacts.Add(new ChartAnnotationReviewQueueItem(
                    Asset: asset,
                    SetupId: setupId,
                    ConfidenceBaseline: confidenceBaseline,
                    Status: status,
                    RequiresHumanReview: requiresHumanReview,
                    CanGenerateAnnotation: canGenerateAnnotation,
                    MissingFields: missingFields,
                    ReviewDecision: "pending",
                    Approved: approved,
                    PromotedToEmbedded: promotedToEmbedded,
                    SourcePath: path));
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                warnings.Add($"chart_annotation_review_artifact_read_failed:{Path.GetFileName(path)}:{ex.GetType().Name}");
            }
        }

        return artifacts;
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

        var reportRoot = Path.Combine(_storagePaths.Root, "reports", "chart_annotation_review_queue");
        if (Directory.Exists(reportRoot))
        {
            foreach (var path in Directory.EnumerateFiles(reportRoot, "*chart_annotation_review_artifact.json", SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }
        }
    }

    private static IReadOnlyList<string> CollectMissingFields(JsonElement root)
    {
        var fields = new List<string>();
        if (!HasAnyPriceSet(root, "entry", "proposed_entry")) fields.Add("entry");
        if (!HasAnyPriceSet(root, "sl", "proposed_sl")) fields.Add("sl");
        if (!HasAnyPriceSet(root, "tp1", "proposed_tp1")) fields.Add("tp1");
        if (!HasAnyPriceSet(root, "invalidation", "invalidation_level")) fields.Add("invalidation");
        if (ReadAnyDouble(root, "risk_reward") <= 0) fields.Add("risk_reward");
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

    private static string BuildMarkdown(ChartAnnotationReviewQueueReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Chart Annotation Review Queue");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- total_candidates: {report.TotalCandidates}");
        sb.AppendLine($"- ready_for_review: {report.ReadyForReview}");
        sb.AppendLine($"- needs_price_review: {report.NeedsPriceReview}");
        sb.AppendLine($"- approved_count: {report.ApprovedCount}");
        sb.AppendLine($"- pending_count: {report.PendingCount}");
        sb.AppendLine();

        foreach (var item in report.Items)
        {
            sb.AppendLine($"## {item.Asset} / {item.SetupId}");
            sb.AppendLine($"- asset: {item.Asset}");
            sb.AppendLine($"- setup_id: {item.SetupId}");
            sb.AppendLine($"- confidence_baseline: {item.ConfidenceBaseline:0.####}");
            sb.AppendLine($"- status: {item.Status}");
            sb.AppendLine($"- requires_human_review: {item.RequiresHumanReview.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- can_generate_annotation: {item.CanGenerateAnnotation.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- missing_fields: {string.Join(", ", item.MissingFields)}");
            sb.AppendLine($"- review_decision: {item.ReviewDecision}");
            sb.AppendLine($"- approved: {item.Approved.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- promoted_to_embedded: {item.PromotedToEmbedded.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- source_path: {item.SourcePath}");
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
