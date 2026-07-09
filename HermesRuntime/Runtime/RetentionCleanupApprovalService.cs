using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RetentionCleanupApprovalRecord(
    string ReviewId,
    bool Approved,
    string Reviewer,
    DateTimeOffset ApprovedAtUtc,
    string Comment,
    int CandidateCount,
    long EstimatedReclaimableBytes);

public sealed class RetentionCleanupApprovalService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedAuditPath;

    public RetentionCleanupApprovalService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, ".codex_artifacts", "reports", "retention_cleanup_review");

    public string ReviewPath => Path.Combine(Root, "retention_cleanup_review.json");

    public string AuditPath => _resolvedAuditPath ?? Path.Combine(Root, "retention_cleanup_approvals.jsonl");

    public RetentionCleanupApprovalRecord Approve(string reviewer, string comment)
    {
        Directory.CreateDirectory(Root);

        var review = LoadLatestReview();
        var approval = new RetentionCleanupApprovalRecord(
            ReviewId: review.ReviewId,
            Approved: true,
            Reviewer: reviewer,
            ApprovedAtUtc: DateTimeOffset.UtcNow,
            Comment: comment,
            CandidateCount: review.CandidateCount,
            EstimatedReclaimableBytes: review.EstimatedReclaimableBytes);

        WriteAudit(approval);
        return approval;
    }

    private RetentionCleanupReviewReport LoadLatestReview()
    {
        if (!File.Exists(ReviewPath))
        {
            throw new FileNotFoundException("Retention cleanup review not found", ReviewPath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(ReviewPath));
        var root = doc.RootElement;
        return new RetentionCleanupReviewReport(
            ReviewId: ReadString(root, "review_id") ?? $"retention_review_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            GeneratedAtUtc: ReadDateTime(root, "generated_at_utc") ?? DateTimeOffset.UtcNow,
            CandidateCount: ReadInt(root, "candidate_count"),
            EstimatedReclaimableFiles: ReadInt(root, "estimated_reclaimable_files"),
            EstimatedReclaimableBytes: ReadLong(root, "estimated_reclaimable_bytes"),
            ProtectedCount: ReadInt(root, "protected_count"),
            SampleCandidates: Array.Empty<RetentionCleanupReviewEntry>(),
            RequiresApproval: ReadBool(root, "requires_approval"),
            Approved: ReadBool(root, "approved"),
            ReviewPath: ReviewPath,
            MarkdownPath: Path.Combine(Root, "retention_cleanup_review.md"),
            NoDeletionPerformed: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private void WriteAudit(RetentionCleanupApprovalRecord approval)
    {
        var json = JsonSerializer.Serialize(approval, JsonDefaults.WriteOptions);
        File.AppendAllText(AuditPath, json + Environment.NewLine, Encoding.UTF8);
        _resolvedAuditPath = AuditPath;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static long ReadLong(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0L;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0L;
    }

    private static bool ReadBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static DateTimeOffset? ReadDateTime(JsonElement root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
