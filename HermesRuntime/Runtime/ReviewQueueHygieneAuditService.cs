using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record ReviewQueueHygieneCandidate(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Status,
    string Category,
    IReadOnlyList<string> Reasons,
    string SuggestedAction,
    bool SafeAutoClose,
    string SafeAutoCloseStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string EvidenceSignature);

public sealed record ReviewQueueHygieneAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalReviews,
    int AutoCloseCandidates,
    int MergeCandidates,
    int StaleReviews,
    int LowValueReviews,
    int DuplicateReviews,
    int PotentialReduction,
    int PotentialQueueSizeAfterCleanup,
    IReadOnlyList<ReviewQueueHygieneCandidate> Candidates,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewQueueHygieneAuditService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewQueueHygieneAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_queue_hygiene");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_queue_hygiene.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_queue_hygiene.md");

    public ReviewQueueHygieneAuditReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var workflow = new HumanReviewWorkflow(_storagePaths);
        var queue = workflow.LoadOrCreateQueue();
        var pending = queue.Items
            .Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var candidates = BuildCandidates(pending);
        var autoCloseCandidates = candidates.Count(candidate => candidate.SafeAutoClose);
        var mergeCandidates = candidates.Count(candidate => candidate.Category == "MERGE_CANDIDATE");
        var staleReviews = candidates.Count(candidate => candidate.Category == "STALE_REVIEW");
        var lowValueReviews = candidates.Count(candidate => candidate.Category == "LOW_VALUE_REVIEW");
        var duplicateReviews = candidates.Count(candidate => candidate.Category == "DUPLICATE_REVIEW");
        var reduction = CalculatePotentialReduction(candidates);
        var report = new ReviewQueueHygieneAuditReport(
            ReportVersion: "review_queue_hygiene_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviews: pending.Count,
            AutoCloseCandidates: autoCloseCandidates,
            MergeCandidates: mergeCandidates,
            StaleReviews: staleReviews,
            LowValueReviews: lowValueReviews,
            DuplicateReviews: duplicateReviews,
            PotentialReduction: reduction,
            PotentialQueueSizeAfterCleanup: Math.Max(0, pending.Count - reduction),
            Candidates: candidates,
            OperatorSummary: BuildOperatorSummary(pending.Count, reduction),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: queue.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        return report;
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        try
        {
            Directory.CreateDirectory(Root);
            return (Path.Combine(Root, "review_queue_hygiene.json"), Path.Combine(Root, "review_queue_hygiene.md"), Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_queue_hygiene");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "review_queue_hygiene.json"), Path.Combine(fallbackRoot, "review_queue_hygiene.md"), fallbackRoot);
        }
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, ReviewQueueHygieneAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(reportPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_queue_hygiene");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_queue_hygiene.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_queue_hygiene.md"), markdown);
        }
    }

    private static string BuildMarkdown(ReviewQueueHygieneAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Review Queue Hygiene Audit");
        builder.AppendLine();
        builder.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- Reviews gesamt: {report.TotalReviews}");
        builder.AppendLine($"- Auto-Close Kandidaten: {report.AutoCloseCandidates}");
        builder.AppendLine($"- Merge Kandidaten: {report.MergeCandidates}");
        builder.AppendLine($"- Veraltete Reviews: {report.StaleReviews}");
        builder.AppendLine($"- Low Value Reviews: {report.LowValueReviews}");
        builder.AppendLine($"- Duplikate: {report.DuplicateReviews}");
        builder.AppendLine($"- Potenzielle Reduktion: {report.PotentialReduction}");
        builder.AppendLine($"- Potenzielle Queue-Größe: {report.PotentialQueueSizeAfterCleanup}");
        builder.AppendLine();
        builder.AppendLine("## Operator Summary");
        builder.AppendLine(report.OperatorSummary);
        builder.AppendLine();
        builder.AppendLine("## Kandidaten");
        foreach (var candidate in report.Candidates.Take(20))
        {
            builder.AppendLine($"- {candidate.Category} | {candidate.Domain} | {candidate.Title} | {candidate.SafeAutoCloseStatus}");
            builder.AppendLine($"  - Knowledge Item: {candidate.KnowledgeItemId}");
            builder.AppendLine($"  - Begründung: {string.Join("; ", candidate.Reasons)}");
            builder.AppendLine($"  - Empfehlung: {candidate.SuggestedAction}");
        }

        return builder.ToString();
    }

    private static List<ReviewQueueHygieneCandidate> BuildCandidates(IReadOnlyList<HumanReviewItem> pending)
    {
        var byKnowledge = pending.GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase).ToList();
        var duplicateKnowledgeIds = byKnowledge
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.ReviewId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedTitleGroups = pending
            .GroupBy(item => NormalizeKey(item.Title) + "|" + NormalizeKey(item.Domain) + "|" + NormalizeKey(item.EvidenceSummary), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var similarGroups = pending
            .GroupBy(item => NormalizeKey(item.Title) + "|" + NormalizeKey(item.Domain), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        return pending
            .Select(item =>
            {
                var reasons = new List<string>();
                var categories = new List<string>();
                var ageDays = Math.Max(0, (DateTimeOffset.UtcNow - item.CreatedAtUtc).TotalDays);
                var evidenceSignature = BuildEvidenceSignature(item);
                var isDocumentation = IsDocumentationReview(item);
                var isLowValue = IsLowValueReview(item, ageDays, isDocumentation);
                var isStale = ageDays >= 21 || (item.UpdatedAtUtc is not null && (DateTimeOffset.UtcNow - item.UpdatedAtUtc.Value).TotalDays >= 14);
                var isDuplicate = duplicateKnowledgeIds.Contains(item.ReviewId)
                    || normalizedTitleGroups.Any(group => group.Count() > 1 && group.Any(groupItem => groupItem.ReviewId == item.ReviewId));
                var isMergeCandidate = similarGroups.Any(group => group.Count() > 1 && group.Any(groupItem => groupItem.ReviewId == item.ReviewId));
                var obsolete = IsTechnicallyObsolete(item);
                var safeAutoClose = (isDocumentation || isDuplicate || obsolete) && !HasOpenValidation(item);
                var openValidations = HasOpenValidation(item);
                var openHumanDecision = false;

                if (isDuplicate)
                {
                    categories.Add("DUPLICATE_REVIEW");
                    reasons.Add("Identischer Wissensträger oder Evidenzstand");
                }

                if (isMergeCandidate && !categories.Contains("DUPLICATE_REVIEW"))
                {
                    categories.Add("MERGE_CANDIDATE");
                    reasons.Add("Nahezu identische Titel oder Evidenz");
                }

                if (isStale)
                {
                    categories.Add("STALE_REVIEW");
                    reasons.Add($"Seit {ageDays:0} Tagen unverändert");
                }

                if (isLowValue)
                {
                    categories.Add("LOW_VALUE_REVIEW");
                    reasons.Add("Geringer operativer Nutzen");
                }

                if (obsolete)
                {
                    categories.Add("AUTO_ARCHIVE_CANDIDATE");
                    reasons.Add("Technisch obsolet");
                }

                if (categories.Count == 0)
                {
                    categories.Add("LOW_VALUE_REVIEW");
                    reasons.Add("Keine klare Priorität oder Folgeaktion");
                }

                if (safeAutoClose && !openValidations && !openHumanDecision)
                {
                    reasons.Add("Sicher schließbar ohne Trading-Bezug");
                }

                var category = categories.Contains("AUTO_ARCHIVE_CANDIDATE")
                    ? "AUTO_ARCHIVE_CANDIDATE"
                    : categories.Contains("DUPLICATE_REVIEW")
                        ? "DUPLICATE_REVIEW"
                        : categories.Contains("MERGE_CANDIDATE")
                            ? "MERGE_CANDIDATE"
                            : categories.Contains("STALE_REVIEW")
                                ? "STALE_REVIEW"
                                : "LOW_VALUE_REVIEW";

                return new ReviewQueueHygieneCandidate(
                    ReviewId: item.ReviewId,
                    KnowledgeItemId: item.KnowledgeItemId,
                    Title: item.Title,
                    Domain: item.Domain,
                    Status: item.Status,
                    Category: category,
                    Reasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    SuggestedAction: category switch
                    {
                        "AUTO_ARCHIVE_CANDIDATE" => "Auto-Archivierung vorschlagen",
                        "DUPLICATE_REVIEW" => "Duplikat mit Original zusammenführen",
                        "MERGE_CANDIDATE" => "Zusammenführung prüfen",
                        "STALE_REVIEW" => "Veraltetes Review aufräumen",
                        _ => "Queue-Hygiene prüfen"
                    },
                    SafeAutoClose: safeAutoClose && !openValidations && !openHumanDecision,
                    SafeAutoCloseStatus: safeAutoClose && !openValidations && !openHumanDecision ? "auto_closed_safe" : "open",
                    CreatedAtUtc: item.CreatedAtUtc,
                    UpdatedAtUtc: item.UpdatedAtUtc,
                    EvidenceSignature: evidenceSignature);
            })
            .OrderByDescending(candidate => candidate.SafeAutoClose)
            .ThenByDescending(candidate => candidate.Category == "DUPLICATE_REVIEW")
            .ThenByDescending(candidate => candidate.Category == "MERGE_CANDIDATE")
            .ThenByDescending(candidate => candidate.Category == "STALE_REVIEW")
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .ToList();
    }

    private static int CalculatePotentialReduction(IReadOnlyList<ReviewQueueHygieneCandidate> candidates)
    {
        var removable = candidates
            .Where(candidate => candidate.SafeAutoClose)
            .Select(candidate => candidate.ReviewId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return removable.Count;
    }

    private static bool HasOpenValidation(HumanReviewItem item)
        => item.EvidenceRefs.Any(reference => Regex.IsMatch(reference, "validation|forward|oos", RegexOptions.IgnoreCase));

    private static bool IsDocumentationReview(HumanReviewItem item)
        => NormalizeKey(item.Domain) == "documentation"
            || NormalizeKey(item.Title).Contains("doc")
            || NormalizeKey(item.Reason).Contains("documentation");

    private static bool IsLowValueReview(HumanReviewItem item, double ageDays, bool isDocumentation)
        => isDocumentation && ageDays >= 7
            || item.TrustBefore < 0.6
            || item.Recommendation.Contains("more_evidence", StringComparison.OrdinalIgnoreCase);

    private static bool IsTechnicallyObsolete(HumanReviewItem item)
        => Regex.IsMatch($"{item.Title} {item.Reason} {item.EvidenceSummary}", "obsolete|deprecated|legacy|superseded", RegexOptions.IgnoreCase);

    private static string BuildEvidenceSignature(HumanReviewItem item)
        => NormalizeKey(item.EvidenceSummary);

    private static string BuildOperatorSummary(int totalReviews, int potentialReduction)
        => $"Hermes kann voraussichtlich {potentialReduction} Reviews ohne Risiko aus der aktiven Queue entfernen. Aktuell: {totalReviews}.";

    private static string NormalizeKey(string value)
        => Regex.Replace(value.ToLowerInvariant().Trim(), "\\s+", " ");
}
