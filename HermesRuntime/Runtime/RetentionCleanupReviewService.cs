using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RetentionCleanupReviewEntry(
    string Path,
    string RetentionClass,
    string Reason,
    double AgeDays,
    string? ProtectedReason);

public sealed record RetentionCleanupReviewReport(
    string ReviewId,
    DateTimeOffset GeneratedAtUtc,
    int CandidateCount,
    int EstimatedReclaimableFiles,
    long EstimatedReclaimableBytes,
    int ProtectedCount,
    IReadOnlyList<RetentionCleanupReviewEntry> SampleCandidates,
    bool RequiresApproval,
    bool Approved,
    string ReviewPath,
    string MarkdownPath,
    bool NoDeletionPerformed,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class RetentionCleanupReviewService
{
    private const int SampleLimit = 50;

    private readonly StoragePaths _storagePaths;
    private readonly RetentionCleanupPreviewService _previewService;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public RetentionCleanupReviewService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _previewService = new RetentionCleanupPreviewService(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, ".codex_artifacts", "reports", "retention_cleanup_review");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "retention_cleanup_review.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "retention_cleanup_review.md");

    public RetentionCleanupReviewReport Run()
    {
        Directory.CreateDirectory(Root);

        var preview = _previewService.Run(full: false);
        var sampleCandidates = preview.CandidatePaths
            .Take(SampleLimit)
            .Select(item => new RetentionCleanupReviewEntry(
                Path: item.Path,
                RetentionClass: item.RetentionClass,
                Reason: item.Reason,
                AgeDays: item.AgeDays,
                ProtectedReason: item.ProtectedReason))
            .ToList();

        var protectedCount = preview.ProtectedPaths.Count;
        var review = new RetentionCleanupReviewReport(
            ReviewId: $"retention_review_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            CandidateCount: preview.DeletableCount,
            EstimatedReclaimableFiles: preview.EstimatedReclaimableFiles,
            EstimatedReclaimableBytes: preview.EstimatedReclaimableBytes,
            ProtectedCount: protectedCount,
            SampleCandidates: sampleCandidates,
            RequiresApproval: true,
            Approved: false,
            ReviewPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoDeletionPerformed: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(review);
        return review;
    }

    private void WriteReport(RetentionCleanupReviewReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, BuildMarkdown(report));
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "retention_cleanup_review");
            Directory.CreateDirectory(fallbackRoot);
            _resolvedReportPath = Path.Combine(fallbackRoot, "retention_cleanup_review.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "retention_cleanup_review.md");
            File.WriteAllText(_resolvedReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
            File.WriteAllText(_resolvedMarkdownPath, BuildMarkdown(report));
        }
    }

    private static string BuildMarkdown(RetentionCleanupReviewReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Retention Cleanup Review");
        sb.AppendLine();
        sb.AppendLine($"- review_id: {report.ReviewId}");
        sb.AppendLine($"- generated_at_utc: {report.GeneratedAtUtc:O}");
        sb.AppendLine($"- candidate_count: {report.CandidateCount}");
        sb.AppendLine($"- estimated_reclaimable_files: {report.EstimatedReclaimableFiles}");
        sb.AppendLine($"- estimated_reclaimable_bytes: {report.EstimatedReclaimableBytes}");
        sb.AppendLine($"- protected_count: {report.ProtectedCount}");
        sb.AppendLine($"- requires_approval: {report.RequiresApproval.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- approved: {report.Approved.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Sample Candidates");
        foreach (var candidate in report.SampleCandidates)
        {
            sb.AppendLine($"- {candidate.Path} | retention={candidate.RetentionClass} | age_days={candidate.AgeDays:0.##} | reason={candidate.Reason} | protected_reason={candidate.ProtectedReason ?? "-"}");
        }
        sb.AppendLine();
        sb.AppendLine("Safety: no deletion performed; no trading execution; no broker action; no auto trading; human review required.");
        return sb.ToString();
    }
}
