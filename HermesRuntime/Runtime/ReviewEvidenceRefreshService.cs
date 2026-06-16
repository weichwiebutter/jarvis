using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ReviewEvidenceRefreshEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Priority,
    double TrustBefore,
    double TrustAfter,
    double QualityBefore,
    double QualityAfter,
    double ValidationBefore,
    double ValidationAfter,
    double EvidenceBefore,
    double EvidenceAfter,
    string RecommendationBefore,
    string RecommendationAfter,
    string EvidenceSummaryBefore,
    string EvidenceSummaryAfter,
    IReadOnlyList<string> EvidenceRefsBefore,
    IReadOnlyList<string> EvidenceRefsAfter,
    IReadOnlyList<string> BlockingReasons,
    bool TrustImproved,
    bool QualityImproved,
    bool ValidationImproved,
    bool EvidenceImproved,
    bool RecommendationChanged,
    string RecommendationLabel,
    string FrankAction);

public sealed record ReviewEvidenceRefreshReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int PendingReviewsRead,
    int ReviewsUpdated,
    int ReviewsUnchanged,
    int TrustImprovedCount,
    int QualityImprovedCount,
    int ValidationImprovedCount,
    int EvidenceImprovedCount,
    int RecommendationChangedCount,
    int RecommendedApprove,
    int RecommendedMoreEvidence,
    int RecommendedReject,
    bool FrankActionRequired,
    IReadOnlyList<ReviewEvidenceRefreshEntry> Reviews,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string QueuePath,
    string EvidencePath,
    string QualityPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewEvidenceRefreshService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewEvidenceRefreshService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_evidence_refresh");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_evidence_refresh.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_evidence_refresh.md");

    public ReviewEvidenceRefreshReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var workflow = new HumanReviewWorkflow(_storagePaths);
        var queue = workflow.LoadOrCreateQueue();
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var evidence = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var evidenceByKnowledgeId = evidence.Reviews
            .GroupBy(review => review.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ReviewedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionIds = contradictions.Contradictions
            .Select(item => item.KnowledgeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var refreshed = new List<HumanReviewItem>();
        var entries = new List<ReviewEvidenceRefreshEntry>();
        foreach (var item in queue.Items)
        {
            if (!item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                refreshed.Add(item);
                continue;
            }

            var qualityItem = quality.Items.FirstOrDefault(candidate => candidate.KnowledgeId.Equals(item.KnowledgeItemId, StringComparison.OrdinalIgnoreCase));
            var evidenceRecord = evidenceByKnowledgeId.GetValueOrDefault(item.KnowledgeItemId);
            var updated = RefreshReview(item, qualityItem, evidenceRecord, contradictionIds.Contains(item.KnowledgeItemId));
            refreshed.Add(updated.Item);
            entries.Add(updated.Entry);
        }

        var refreshedQueue = queue with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Items = refreshed,
            PendingReviews = refreshed.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedReviews = refreshed.Count(item => item.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)),
            RejectedReviews = refreshed.Count(item => item.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            NeedsMoreEvidenceReviews = refreshed.Count(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase)),
            DeferredReviews = refreshed.Count(item => item.Status.Equals("deferred", StringComparison.OrdinalIgnoreCase)),
            Warnings = queue.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        workflow.PersistQueue(refreshedQueue);

        var report = new ReviewEvidenceRefreshReport(
            ReportVersion: "review_evidence_refresh_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PendingReviewsRead: queue.Items.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ReviewsUpdated: entries.Count,
            ReviewsUnchanged: Math.Max(0, queue.Items.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)) - entries.Count),
            TrustImprovedCount: entries.Count(item => item.TrustImproved),
            QualityImprovedCount: entries.Count(item => item.QualityImproved),
            ValidationImprovedCount: entries.Count(item => item.ValidationImproved),
            EvidenceImprovedCount: entries.Count(item => item.EvidenceImproved),
            RecommendationChangedCount: entries.Count(item => item.RecommendationChanged),
            RecommendedApprove: entries.Count(item => item.RecommendationAfter.Equals("approve", StringComparison.OrdinalIgnoreCase)),
            RecommendedMoreEvidence: entries.Count(item => item.RecommendationAfter.Equals("more_evidence", StringComparison.OrdinalIgnoreCase)),
            RecommendedReject: entries.Count(item => item.RecommendationAfter.Equals("reject", StringComparison.OrdinalIgnoreCase)),
            FrankActionRequired: refreshedQueue.PendingReviews > 0,
            Reviews: entries,
            Warnings: BuildWarnings(entries, refreshedQueue),
            OperatorSummary: BuildOperatorSummary(entries, refreshedQueue),
            QueuePath: workflow.QueuePath,
            EvidencePath: new HumanReviewEvidenceStore(_storagePaths).ReviewPath,
            QualityPath: quality.EvidencePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(reportPath, markdownPath, root, report);
        return report;
    }

    public ReviewEvidenceRefreshReport? Load()
    {
        var readablePath = ResolveReadableReportPath();
        _resolvedReportPath = readablePath;
        if (!File.Exists(readablePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReviewEvidenceRefreshReport>(File.ReadAllText(readablePath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static (HumanReviewItem Item, ReviewEvidenceRefreshEntry Entry) RefreshReview(
        HumanReviewItem item,
        KnowledgeQualityItem? qualityItem,
        HumanReviewEvidence? evidenceRecord,
        bool hasContradiction)
    {
        var before = ExtractSignals(item.EvidenceSummary, item.TrustBefore);
        var afterTrust = qualityItem?.TrustScore ?? before.Trust;
        var afterQuality = qualityItem?.QualityScore ?? before.Quality;
        var afterValidation = qualityItem?.ValidationScore ?? before.Validation;
        var afterEvidence = qualityItem?.EvidenceScore ?? before.Evidence;
        var afterSummary = BuildEvidenceSummary(qualityItem, evidenceRecord, hasContradiction);
        var afterRecommendation = DetermineRecommendation(afterTrust, afterQuality, afterValidation, afterEvidence, hasContradiction);
        var evidenceRefsAfter = BuildEvidenceRefs(item, qualityItem, evidenceRecord, hasContradiction);
        var trustImproved = afterTrust > before.Trust;
        var qualityImproved = afterQuality > before.Quality;
        var validationImproved = afterValidation > before.Validation;
        var evidenceImproved = afterEvidence > before.Evidence;
        var recommendationChanged = !item.Recommendation.Equals(afterRecommendation, StringComparison.OrdinalIgnoreCase);
        var status = item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase) ? "pending" : item.Status;

        var updatedItem = item with
        {
            TrustBefore = afterTrust,
            EvidenceSummary = afterSummary,
            Recommendation = afterRecommendation,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            EvidenceRefs = evidenceRefsAfter,
            Status = status
        };

        var entry = new ReviewEvidenceRefreshEntry(
            ReviewId: item.ReviewId,
            KnowledgeItemId: item.KnowledgeItemId,
            Title: item.Title,
            Domain: item.Domain,
            Priority: item.Priority switch
            {
                HumanReviewPriority.high => "hoch",
                HumanReviewPriority.medium => "mittel",
                _ => "niedrig"
            },
            TrustBefore: before.Trust,
            TrustAfter: afterTrust,
            QualityBefore: before.Quality,
            QualityAfter: afterQuality,
            ValidationBefore: before.Validation,
            ValidationAfter: afterValidation,
            EvidenceBefore: before.Evidence,
            EvidenceAfter: afterEvidence,
            RecommendationBefore: item.Recommendation,
            RecommendationAfter: afterRecommendation,
            EvidenceSummaryBefore: item.EvidenceSummary,
            EvidenceSummaryAfter: afterSummary,
            EvidenceRefsBefore: item.EvidenceRefs.ToList(),
            EvidenceRefsAfter: evidenceRefsAfter,
            BlockingReasons: BuildBlockingReasons(afterTrust, afterQuality, afterValidation, afterEvidence, hasContradiction),
            TrustImproved: trustImproved,
            QualityImproved: qualityImproved,
            ValidationImproved: validationImproved,
            EvidenceImproved: evidenceImproved,
            RecommendationChanged: recommendationChanged,
            RecommendationLabel: RecommendationLabel(afterRecommendation),
            FrankAction: afterRecommendation.Equals("approve", StringComparison.OrdinalIgnoreCase)
                ? "Prüfzentrum: Freigabe prüfen"
                : afterRecommendation.Equals("reject", StringComparison.OrdinalIgnoreCase)
                    ? "Prüfzentrum: Ablehnung prüfen"
                    : "Prüfzentrum: mehr Evidenz prüfen");

        return (updatedItem, entry);
    }

    private static (double Trust, double Quality, double Validation, double Evidence) ExtractSignals(string summary, double fallbackTrust)
    {
        double Read(string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(summary ?? string.Empty, $@"{key}=([0-9.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        var trust = Read("trust");
        var quality = Read("quality");
        var validation = Read("validation");
        var evidence = Read("evidence");
        return (
            Trust: trust > 0 ? trust : fallbackTrust,
            Quality: quality,
            Validation: validation,
            Evidence: evidence > 0 ? evidence : Math.Max(quality, validation));
    }

    private static string BuildEvidenceSummary(KnowledgeQualityItem? qualityItem, HumanReviewEvidence? evidenceRecord, bool hasContradiction)
    {
        var trust = qualityItem?.TrustScore ?? 0;
        var quality = qualityItem?.QualityScore ?? 0;
        var evidence = qualityItem?.EvidenceScore ?? 0;
        var validation = qualityItem?.ValidationScore ?? 0;
        var lifecycle = qualityItem?.LifecycleStatus ?? "unknown";
        var evidenceRefs = evidenceRecord is null ? 0 : 1;
        var contradiction = hasContradiction ? "; contradiction=true" : string.Empty;
        return $"trust={trust:0.####}; quality={quality:0.####}; evidence={evidence:0.####}; validation={validation:0.####}; lifecycle={lifecycle}; evidence_refs={evidenceRefs}{contradiction}";
    }

    private static string DetermineRecommendation(double trust, double quality, double validation, double evidence, bool hasContradiction)
    {
        if (hasContradiction || trust < 0.45 || quality < 0.45 || validation < 0.45)
        {
            return "reject";
        }

        if (trust >= 0.70 && quality >= 0.65 && validation >= 0.65)
        {
            return "approve";
        }

        if (trust >= 0.45 && trust < 0.70)
        {
            return "more_evidence";
        }

        if (validation < 0.65 || evidence < 0.70)
        {
            return "more_evidence";
        }

        return "more_evidence";
    }

    private static IReadOnlyList<string> BuildEvidenceRefs(HumanReviewItem item, KnowledgeQualityItem? qualityItem, HumanReviewEvidence? evidenceRecord, bool hasContradiction)
    {
        var refs = new List<string>();
        refs.AddRange(item.EvidenceRefs);
        if (qualityItem is not null)
        {
            refs.AddRange(qualityItem.EvidenceRefs.Select(reference => $"quality:{reference}"));
            if (qualityItem.LastValidatedUtc is not null)
            {
                refs.Add($"validation:last_validated:{qualityItem.LastValidatedUtc:O}");
            }
        }

        if (evidenceRecord is not null)
        {
            refs.Add($"human_review_review:{evidenceRecord.ReviewId}");
            refs.Add($"human_review_result:{evidenceRecord.Result}");
            refs.Add($"human_review_notes:{evidenceRecord.Notes}");
        }

        if (hasContradiction)
        {
            refs.Add("contradiction:detected");
        }

        return refs.Distinct(StringComparer.OrdinalIgnoreCase).Take(80).ToList();
    }

    private static IReadOnlyList<string> BuildBlockingReasons(double trust, double quality, double validation, double evidence, bool hasContradiction)
    {
        var reasons = new List<string>();
        if (hasContradiction)
        {
            reasons.Add("Widerspruch erkannt.");
        }

        if (trust < 0.70)
        {
            reasons.Add(trust < 0.45 ? "Vertrauen zu niedrig." : "Vertrauen noch nicht ausreichend.");
        }

        if (quality < 0.65)
        {
            reasons.Add(quality < 0.45 ? "Evidenzqualität zu schwach." : "Evidenzqualität noch mittel.");
        }

        if (validation < 0.65)
        {
            reasons.Add("Validierung noch nicht stark genug.");
        }

        if (evidence < 0.70)
        {
            reasons.Add("Weitere Evidenz sinnvoll.");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string RecommendationLabel(string recommendation) => recommendation switch
    {
        "approve" => "Freigabe empfohlen",
        "reject" => "Ablehnung empfohlen",
        _ => "Mehr Evidenz empfohlen"
    };

    private static string BuildOperatorSummary(IReadOnlyList<ReviewEvidenceRefreshEntry> reviews, HumanReviewQueue queue)
    {
        var updated = reviews.Count;
        var approve = reviews.Count(item => item.RecommendationAfter.Equals("approve", StringComparison.OrdinalIgnoreCase));
        var moreEvidence = reviews.Count(item => item.RecommendationAfter.Equals("more_evidence", StringComparison.OrdinalIgnoreCase));
        var reject = reviews.Count(item => item.RecommendationAfter.Equals("reject", StringComparison.OrdinalIgnoreCase));
        var improved = reviews.Count(item => item.TrustImproved || item.QualityImproved || item.ValidationImproved || item.EvidenceImproved);
        return string.Join(Environment.NewLine, new[]
        {
            $"Hermes hat {updated} Reviews mit neuer Evidenz aktualisiert.",
            $"{improved} Reviews zeigen bessere Entscheidungsgrundlagen.",
            $"{queue.PendingReviews} Reviews bleiben im Prüfzentrum offen.",
            $"Neue Empfehlungen: {approve} Freigabe, {moreEvidence} mehr Evidenz, {reject} Ablehnung.",
            queue.PendingReviews > 0 ? "Frank muss weiterhin selbst entscheiden." : "Frank muss aktuell nichts tun."
        });
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<ReviewEvidenceRefreshEntry> reviews, HumanReviewQueue queue)
    {
        var warnings = new List<string>();
        if (reviews.Count == 0)
        {
            warnings.Add("no_pending_reviews_refreshed");
        }

        if (queue.PendingReviews > 0 && reviews.All(review => !review.RecommendationChanged))
        {
            warnings.Add("refresh_did_not_change_recommendations");
        }

        return warnings;
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        var roots = new[]
        {
            Path.Combine(_storagePaths.Root, "reports", "review_evidence_refresh"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".codex_artifacts", "reports", "review_evidence_refresh"),
        };

        foreach (var root in roots)
        {
            try
            {
                Directory.CreateDirectory(root);
                return (Path.Combine(root, "review_evidence_refresh.json"), Path.Combine(root, "review_evidence_refresh.md"), root);
            }
            catch
            {
            }
        }

        var fallbackRoot = roots.Last();
        return (Path.Combine(fallbackRoot, "review_evidence_refresh.json"), Path.Combine(fallbackRoot, "review_evidence_refresh.md"), fallbackRoot);
    }

    private string ResolveReadableReportPath()
    {
        if (File.Exists(ReportPath))
        {
            return ReportPath;
        }

        var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_evidence_refresh", "review_evidence_refresh.json");
        return File.Exists(fallbackPath) ? fallbackPath : ReportPath;
    }

    private static void WriteReport(string reportPath, string markdownPath, string root, ReviewEvidenceRefreshReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_evidence_refresh");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_evidence_refresh.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_evidence_refresh.md"), markdown);
        }
    }

    private static string BuildMarkdown(ReviewEvidenceRefreshReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Review Evidence Refresh");
        sb.AppendLine();
        sb.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Pending Reviews gelesen: {report.PendingReviewsRead}");
        sb.AppendLine($"- Reviews aktualisiert: {report.ReviewsUpdated}");
        sb.AppendLine($"- Reviews unverändert: {report.ReviewsUnchanged}");
        sb.AppendLine($"- Vertrauen verbessert: {report.TrustImprovedCount}");
        sb.AppendLine($"- Qualität verbessert: {report.QualityImprovedCount}");
        sb.AppendLine($"- Validierung verbessert: {report.ValidationImprovedCount}");
        sb.AppendLine($"- Evidenz verbessert: {report.EvidenceImprovedCount}");
        sb.AppendLine($"- Empfehlung geändert: {report.RecommendationChangedCount}");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Reviews");
        foreach (var review in report.Reviews)
        {
            sb.AppendLine($"- {review.Title} ({review.Domain}, {review.RecommendationAfter})");
            sb.AppendLine($"  - Vorher: trust={review.TrustBefore:0.####}, quality={review.QualityBefore:0.####}, validation={review.ValidationBefore:0.####}, evidence={review.EvidenceBefore:0.####}, recommendation={review.RecommendationBefore}");
            sb.AppendLine($"  - Nachher: trust={review.TrustAfter:0.####}, quality={review.QualityAfter:0.####}, validation={review.ValidationAfter:0.####}, evidence={review.EvidenceAfter:0.####}, recommendation={review.RecommendationAfter}");
            sb.AppendLine($"  - Frank-Aktion: {review.FrankAction}");
            sb.AppendLine($"  - Warum: {string.Join(" ", review.BlockingReasons)}");
        }

        return sb.ToString();
    }
}
