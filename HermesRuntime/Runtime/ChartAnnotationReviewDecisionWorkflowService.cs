using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ChartAnnotationReviewDecisionEntry(
    string DecisionId,
    DateTimeOffset ReviewTimestampUtc,
    string Asset,
    string SetupId,
    string Decision,
    string Reviewer,
    string Comment,
    bool Approved,
    bool PromotedToEmbedded,
    string ArtifactPath);

public sealed record ChartAnnotationReviewDecisionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int DecisionsTotal,
    int ApprovedCount,
    int RejectedCount,
    int PendingCount,
    IReadOnlyList<ChartAnnotationReviewDecisionEntry> Decisions,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    string AuditTrailPath);

public sealed class ChartAnnotationReviewDecisionWorkflowService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ChartAnnotationReviewDecisionWorkflowService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_review_decisions");
    public string ReportPath => Path.Combine(Root, "chart_annotation_review_decisions.json");
    public string MarkdownPath => Path.Combine(Root, "chart_annotation_review_decisions.md");
    public string AuditTrailPath => Path.Combine(Root, "chart_annotation_review_decisions.jsonl");

    public ChartAnnotationReviewDecisionReport Run()
    {
        var decisions = LoadAuditTrail(out var warnings)
            .OrderByDescending(item => item.ReviewTimestampUtc)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new ChartAnnotationReviewDecisionReport(
            ReportVersion: "chart_annotation_review_decisions_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: decisions.Count > 0 ? "ready" : "empty",
            DecisionsTotal: decisions.Count,
            ApprovedCount: decisions.Count(item => item.Approved),
            RejectedCount: decisions.Count(item => item.Decision.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            PendingCount: decisions.Count(item => item.Decision.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            Decisions: decisions,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            AuditTrailPath: AuditTrailPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public ChartAnnotationReviewDecisionReport Decide(
        string asset,
        string setupId,
        string decision,
        string reviewer,
        string comment)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            throw new ArgumentException("asset is required", nameof(asset));
        }

        if (string.IsNullOrWhiteSpace(setupId))
        {
            throw new ArgumentException("setupId is required", nameof(setupId));
        }

        var normalizedDecision = NormalizeDecision(decision);
        if (normalizedDecision is null)
        {
            throw new ArgumentException("decision must be approve or reject", nameof(decision));
        }

        var artifactPath = FindArtifactPath(asset, setupId);
        if (artifactPath is null)
        {
            throw new InvalidOperationException($"No chart annotation review artifact found for {asset} / {setupId}");
        }

        Directory.CreateDirectory(Root);
        var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath)).RootElement.Clone();
        var updated = UpdateArtifact(artifact, normalizedDecision, reviewer, comment);
        File.WriteAllText(artifactPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));

        var entry = new ChartAnnotationReviewDecisionEntry(
            DecisionId: $"chart_annotation_review_decision_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            ReviewTimestampUtc: DateTimeOffset.UtcNow,
            Asset: asset,
            SetupId: setupId,
            Decision: normalizedDecision,
            Reviewer: reviewer,
            Comment: comment,
            Approved: normalizedDecision == "approved",
            PromotedToEmbedded: false,
            ArtifactPath: artifactPath);

        File.AppendAllText(AuditTrailPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
        return Run();
    }

    private IReadOnlyList<ChartAnnotationReviewDecisionEntry> LoadAuditTrail(out List<string> warnings)
    {
        warnings = [];
        if (!File.Exists(AuditTrailPath))
        {
            return [];
        }

        var entries = new List<ChartAnnotationReviewDecisionEntry>();
        foreach (var line in File.ReadAllLines(AuditTrailPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ChartAnnotationReviewDecisionEntry>(line, JsonDefaults.SnapshotReadOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                warnings.Add("audit_trail_line_parse_failed");
            }
        }

        return entries;
    }

    private string? FindArtifactPath(string asset, string setupId)
    {
        var docRoot = Path.Combine(_runtimeRoot, "docs", "trading");
        if (!Directory.Exists(docRoot))
        {
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
            catch
            {
                // ignore broken artifacts here; they will be surfaced in the report loader if needed
            }
        }

        return null;
    }

    private static JsonElement UpdateArtifact(JsonElement artifact, string decision, string reviewer, string comment)
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
            ["status"] = decision == "approved" ? "ready_for_review" : "rejected",
            ["approved"] = decision == "approved",
            ["review_decision"] = decision,
            ["reviewer"] = reviewer,
            ["comment"] = comment,
            ["review_timestamp"] = now,
            ["promoted_to_embedded"] = false,
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
    {
        if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        return null;
    }

    private static string? NormalizeDecision(string decision)
        => decision.Trim().ToLowerInvariant() switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            _ => null
        };

    private static string BuildMarkdown(ChartAnnotationReviewDecisionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Chart Annotation Review Decisions");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- decisions_total: {report.DecisionsTotal}");
        sb.AppendLine($"- approved_count: {report.ApprovedCount}");
        sb.AppendLine($"- rejected_count: {report.RejectedCount}");
        sb.AppendLine($"- pending_count: {report.PendingCount}");
        sb.AppendLine($"- audit_trail_path: {report.AuditTrailPath}");
        sb.AppendLine();

        foreach (var item in report.Decisions)
        {
            sb.AppendLine($"## {item.Asset} / {item.SetupId}");
            sb.AppendLine($"- decision_id: {item.DecisionId}");
            sb.AppendLine($"- review_timestamp_utc: {item.ReviewTimestampUtc:O}");
            sb.AppendLine($"- decision: {item.Decision}");
            sb.AppendLine($"- reviewer: {item.Reviewer}");
            sb.AppendLine($"- comment: {item.Comment}");
            sb.AppendLine($"- approved: {item.Approved.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- promoted_to_embedded: {item.PromotedToEmbedded.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- artifact_path: {item.ArtifactPath}");
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
