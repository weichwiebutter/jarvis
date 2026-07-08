using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ChartAnnotationPromotionAuditEntry(
    string PromotionId,
    string Asset,
    string SetupId,
    string Reviewer,
    DateTimeOffset PromotedAtUtc,
    bool Approved,
    bool Promotable,
    bool PromotedToEmbedded,
    string SourceArtifactPath,
    string EmbeddedPackagePath,
    string EmbeddedPackageGeneratorPath,
    string Result,
    string Comment);

public sealed record ChartAnnotationPromotionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string Asset,
    string SetupId,
    bool Approved,
    bool Promotable,
    bool PromotedToEmbedded,
    string Reviewer,
    string Comment,
    string SourceArtifactPath,
    string PromotionAuditTrailPath,
    string EmbeddedPackageJsonPath,
    string EmbeddedPackageGeneratorPath,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class ChartAnnotationPromotionService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ChartAnnotationPromotionService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_promotions");
    public string ReportPath => Path.Combine(Root, "chart_annotation_promotions.json");
    public string MarkdownPath => Path.Combine(Root, "chart_annotation_promotions.md");
    public string AuditTrailPath => Path.Combine(Root, "chart_annotation_promotions.jsonl");

    public ChartAnnotationPromotionReport Promote(string asset, string setupId, string reviewer)
    {
        if (string.IsNullOrWhiteSpace(asset)) throw new ArgumentException("asset is required", nameof(asset));
        if (string.IsNullOrWhiteSpace(setupId)) throw new ArgumentException("setupId is required", nameof(setupId));
        if (string.IsNullOrWhiteSpace(reviewer)) throw new ArgumentException("reviewer is required", nameof(reviewer));

        var promotionPlan = new ApprovedChartAnnotationPromotionPlanService(_storagePaths, _runtimeRoot).Run();
        var item = promotionPlan.Items.FirstOrDefault(entry => entry.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase) && entry.SetupId.Equals(setupId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            throw new InvalidOperationException($"No approved chart annotation found for {asset} / {setupId}");
        }

        var warnings = new List<string>();
        if (!item.Approved)
        {
            throw new InvalidOperationException($"Chart annotation {asset} / {setupId} is not approved");
        }

        if (!item.IsPromotable)
        {
            throw new InvalidOperationException($"Chart annotation {asset} / {setupId} is not promotable: {string.Join(", ", item.MissingFields)}");
        }

        var artifactPath = ResolveArtifactPath(asset, setupId, out var artifactWarnings);
        warnings.AddRange(artifactWarnings);
        if (artifactPath is null)
        {
            throw new InvalidOperationException($"No chart annotation review artifact found for {asset} / {setupId}");
        }

        var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath)).RootElement.Clone();
        var promotedArtifact = UpdatePromotedArtifact(artifact, reviewer);
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(promotedArtifact, JsonDefaults.WriteOptions));

        var embeddedGenerator = new CloudEmbeddedReleasePackageGeneratorService(_storagePaths, _runtimeRoot);
        var embeddedResult = embeddedGenerator.Generate();

        Directory.CreateDirectory(Root);
        var auditEntry = new ChartAnnotationPromotionAuditEntry(
            PromotionId: $"chart_annotation_promotion_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            Asset: asset,
            SetupId: setupId,
            Reviewer: reviewer,
            PromotedAtUtc: DateTimeOffset.UtcNow,
            Approved: true,
            Promotable: true,
            PromotedToEmbedded: true,
            SourceArtifactPath: artifactPath,
            EmbeddedPackagePath: embeddedGenerator.OutputJsonPath,
            EmbeddedPackageGeneratorPath: embeddedGenerator.OutputSourcePath,
            Result: embeddedResult.Success ? "promoted_and_regenerated" : $"promoted_but_regeneration_failed:{embeddedResult.Status}",
            Comment: item.Comment);
        File.AppendAllText(AuditTrailPath, JsonSerializer.Serialize(auditEntry, JsonDefaults.WriteOptions) + Environment.NewLine);

        var report = new ChartAnnotationPromotionReport(
            ReportVersion: "chart_annotation_promotion_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: embeddedResult.Success ? "promoted" : "promoted_with_regeneration_warning",
            Asset: asset,
            SetupId: setupId,
            Approved: true,
            Promotable: true,
            PromotedToEmbedded: true,
            Reviewer: reviewer,
            Comment: item.Comment,
            SourceArtifactPath: artifactPath,
            PromotionAuditTrailPath: AuditTrailPath,
            EmbeddedPackageJsonPath: embeddedGenerator.OutputJsonPath,
            EmbeddedPackageGeneratorPath: embeddedGenerator.OutputSourcePath,
            Warnings: warnings.Concat(embeddedResult.Success ? [] : [$"embedded_package_regeneration_failed:{embeddedResult.Status}:{embeddedResult.Reason}"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public ChartAnnotationPromotionReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return BuildEmptyReport();
        }

        try
        {
            var report = JsonSerializer.Deserialize<ChartAnnotationPromotionReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? BuildEmptyReport();
        }
        catch
        {
            return BuildEmptyReport();
        }
    }

    private ChartAnnotationPromotionReport BuildEmptyReport() => new(
        ReportVersion: "chart_annotation_promotion_v1",
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Status: "empty",
        Asset: string.Empty,
        SetupId: string.Empty,
        Approved: false,
        Promotable: false,
        PromotedToEmbedded: false,
        Reviewer: string.Empty,
        Comment: string.Empty,
        SourceArtifactPath: string.Empty,
        PromotionAuditTrailPath: AuditTrailPath,
        EmbeddedPackageJsonPath: string.Empty,
        EmbeddedPackageGeneratorPath: string.Empty,
        Warnings: [],
        ReportPath: ReportPath,
        MarkdownPath: MarkdownPath);

    private static JsonElement UpdatePromotedArtifact(JsonElement artifact, string reviewer)
    {
        var now = DateTimeOffset.UtcNow;
        using var doc = JsonDocument.Parse(artifact.GetRawText());
        var root = doc.RootElement;
        var updated = new Dictionary<string, object?>
        {
            ["artifact_type"] = root.TryGetProperty("artifact_type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : "chart_annotation_review_artifact",
            ["asset"] = root.TryGetProperty("asset", out var assetElement) && assetElement.ValueKind == JsonValueKind.String ? assetElement.GetString() : string.Empty,
            ["setup_id"] = root.TryGetProperty("setup_id", out var setupIdElement) && setupIdElement.ValueKind == JsonValueKind.String ? setupIdElement.GetString() : string.Empty,
            ["confidence_baseline"] = root.TryGetProperty("confidence_baseline", out var confidenceElement) && confidenceElement.TryGetDouble(out var confidenceBaseline) ? confidenceBaseline : 0d,
            ["source"] = root.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.String ? sourceElement.GetString() : "embedded_spec_review",
            ["requires_human_review"] = true,
            ["status"] = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String ? statusElement.GetString() : "ready_for_review",
            ["approved"] = root.TryGetProperty("approved", out var approvedElement) && (approvedElement.ValueKind == JsonValueKind.True || approvedElement.ValueKind == JsonValueKind.False) ? approvedElement.GetBoolean() : true,
            ["review_decision"] = root.TryGetProperty("review_decision", out var decisionElement) && decisionElement.ValueKind == JsonValueKind.String ? decisionElement.GetString() : "approved",
            ["reviewer"] = reviewer,
            ["comment"] = root.TryGetProperty("comment", out var commentElement) && commentElement.ValueKind == JsonValueKind.String ? commentElement.GetString() : string.Empty,
            ["review_timestamp"] = now,
            ["promoted_to_embedded"] = true,
            ["entry"] = ReadNullableDouble(root, "entry"),
            ["sl"] = ReadNullableDouble(root, "sl"),
            ["tp1"] = ReadNullableDouble(root, "tp1"),
            ["tp2"] = ReadNullableDouble(root, "tp2"),
            ["invalidation"] = ReadNullableDouble(root, "invalidation"),
            ["risk_reward"] = ReadNullableDouble(root, "risk_reward"),
        };

        return JsonSerializer.SerializeToElement(updated, JsonDefaults.WriteOptions);
    }

    private static double? ReadNullableDouble(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : null;

    private string? ResolveArtifactPath(string asset, string setupId, out List<string> warnings)
    {
        warnings = [];
        var docRoot = Path.Combine(_runtimeRoot, "docs", "trading");
        if (!Directory.Exists(docRoot))
        {
            warnings.Add("docs_trading_root_missing");
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(docRoot, "*chart_annotation_review_artifact.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var artifactAsset = root.TryGetProperty("asset", out var assetElement) && assetElement.ValueKind == JsonValueKind.String ? assetElement.GetString() ?? string.Empty : string.Empty;
                var artifactSetup = root.TryGetProperty("setup_id", out var setupElement) && setupElement.ValueKind == JsonValueKind.String ? setupElement.GetString() ?? string.Empty : string.Empty;
                if (artifactAsset.Equals(asset, StringComparison.OrdinalIgnoreCase) && artifactSetup.Equals(setupId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
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

    private void WriteReport(ChartAnnotationPromotionReport report)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(ChartAnnotationPromotionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Chart Annotation Promotion");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- asset: {report.Asset}");
        sb.AppendLine($"- setup_id: {report.SetupId}");
        sb.AppendLine($"- approved: {report.Approved.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- promotable: {report.Promotable.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- promoted_to_embedded: {report.PromotedToEmbedded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- reviewer: {report.Reviewer}");
        sb.AppendLine($"- comment: {report.Comment}");
        sb.AppendLine($"- source_artifact_path: {report.SourceArtifactPath}");
        sb.AppendLine($"- promotion_audit_trail_path: {report.PromotionAuditTrailPath}");
        sb.AppendLine($"- embedded_package_json_path: {report.EmbeddedPackageJsonPath}");
        sb.AppendLine($"- embedded_package_generator_path: {report.EmbeddedPackageGeneratorPath}");
        sb.AppendLine();
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
