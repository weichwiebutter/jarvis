using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TrustedKnowledgeReviewCandidate(
    string KnowledgeId,
    string Domain,
    string Title,
    double TrustScore,
    double QualityScore,
    double EvidenceScore,
    int EvidenceCount,
    int SourceCount,
    DateTimeOffset? LastValidatedUtc,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> BlockingReasons,
    bool RequiresHumanReview,
    string ReviewStatus);

public sealed record TrustedKnowledgeReviewGateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalKnowledgeItems,
    int TrustedItemsCount,
    int EligibleForTrustedReview,
    int BlockedItems,
    IReadOnlyDictionary<string, int> RejectionReasons,
    IReadOnlyList<TrustedKnowledgeReviewCandidate> TopCandidates,
    IReadOnlyList<string> Warnings,
    bool RequiresHumanReview,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    string QualityPath,
    string ContradictionsPath,
    string ReviewQueuePath,
    string ReviewGatePath,
    string MarkdownPath);

public sealed class TrustedKnowledgeReviewGateService
{
    private readonly StoragePaths _storagePaths;

    public TrustedKnowledgeReviewGateService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trusted_knowledge_review_gate");

    public string GatePath => Path.Combine(Root, "trusted_knowledge_review_gate.json");

    public string MarkdownPath => Path.Combine(Root, "trusted_knowledge_review_gate.md");

    public TrustedKnowledgeReviewGateReport Run()
    {
        Directory.CreateDirectory(Root);

        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionsByKnowledgeId = contradictions.Contradictions
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var humanReview = new HumanReviewWorkflow(_storagePaths).BuildSummary();
        var humanReviewQueue = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var reviewByKnowledgeId = humanReviewQueue.Items
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var candidates = new List<TrustedKnowledgeReviewCandidate>();
        var rejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in qualityReport.Items)
        {
            var catalogItem = catalogById.GetValueOrDefault(item.KnowledgeId);
            if (catalogItem is null)
            {
                AddReject("missing_catalog_entry");
                continue;
            }

            var candidate = EvaluateCandidate(item, catalogItem, contradictionsByKnowledgeId, reviewByKnowledgeId, out var accepted, out var blockers, out var reasons);
            foreach (var blocker in blockers)
            {
                AddReject(blocker);
            }

            if (accepted)
            {
                candidates.Add(candidate);
            }
            else if (reasons.Count > 0)
            {
                foreach (var reason in reasons)
                {
                    AddReject(reason);
                }
            }
        }

        var eligible = candidates
            .OrderByDescending(item => item.TrustScore)
            .ThenByDescending(item => item.QualityScore)
            .ThenByDescending(item => item.EvidenceScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .Take(25)
            .ToList();

        var trustedCount = qualityReport.Items.Count(item =>
            catalogById.TryGetValue(item.KnowledgeId, out var catalogItem)
            && catalogItem.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase));

        var report = new TrustedKnowledgeReviewGateReport(
            ReportVersion: "trusted_knowledge_review_gate_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalKnowledgeItems: qualityReport.TotalKnowledgeItems,
            TrustedItemsCount: trustedCount,
            EligibleForTrustedReview: eligible.Count,
            BlockedItems: Math.Max(0, qualityReport.TotalKnowledgeItems - eligible.Count),
            RejectionReasons: rejectionReasons.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            TopCandidates: eligible,
            Warnings: eligible.Count == 0 ? ["no_trusted_review_candidates"] : [],
            RequiresHumanReview: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            QualityPath: new KnowledgeQualityEngine(_storagePaths).QualityPath,
            ContradictionsPath: new ContradictionDetector(_storagePaths).ContradictionsPath,
            ReviewQueuePath: humanReview.QueuePath,
            ReviewGatePath: GatePath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(GatePath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;

        void AddReject(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            rejectionReasons[reason] = rejectionReasons.TryGetValue(reason, out var count) ? count + 1 : 1;
        }
    }

    public TrustedKnowledgeReviewGateReport? Load()
    {
        if (!File.Exists(GatePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrustedKnowledgeReviewGateReport>(
                File.ReadAllText(GatePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static TrustedKnowledgeReviewCandidate EvaluateCandidate(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem catalogItem,
        IReadOnlyDictionary<string, List<ContradictionRecord>> contradictionsByKnowledgeId,
        IReadOnlyDictionary<string, HumanReviewItem> reviewByKnowledgeId,
        out bool accepted,
        out List<string> blockers,
        out List<string> reasons)
    {
        blockers = [];
        reasons = [];
        accepted = false;

        var evidenceCount = qualityItem.EvidenceRefs.Count;
        var sourceCount = catalogItem.SourceIds.Count;
        var contradictionCount = contradictionsByKnowledgeId.GetValueOrDefault(qualityItem.KnowledgeId)?.Count ?? 0;
        var reviewStatus = reviewByKnowledgeId.TryGetValue(qualityItem.KnowledgeId, out var review)
            ? review.Status
            : "none";

        if (!catalogItem.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
            && !catalogItem.ValidationStatus.Equals("robust", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("not_yet_trusted_or_robust");
        }

        if (qualityItem.TrustScore < 0.82)
        {
            blockers.Add("trust_score_too_low");
        }

        if (qualityItem.QualityScore < 0.82)
        {
            blockers.Add("quality_score_too_low");
        }

        if (evidenceCount < 3)
        {
            blockers.Add("insufficient_evidence");
        }

        if (sourceCount < 2)
        {
            blockers.Add("insufficient_sources");
        }

        if (contradictionCount > 0)
        {
            blockers.Add("active_contradiction");
        }

        if (reviewStatus is "pending" or "deferred" or "needs_more_evidence")
        {
            blockers.Add("pending_human_review");
        }

        if (qualityItem.ValidationScore < 0.68)
        {
            blockers.Add("validation_score_too_low");
        }

        if (qualityItem.LastValidatedUtc is null)
        {
            blockers.Add("not_recently_validated");
        }

        if (blockers.Count == 0)
        {
            accepted = true;
        }
        else
        {
            reasons.AddRange(blockers.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        return new TrustedKnowledgeReviewCandidate(
            KnowledgeId: qualityItem.KnowledgeId,
            Domain: qualityItem.Domain,
            Title: catalogItem.Title,
            TrustScore: qualityItem.TrustScore,
            QualityScore: qualityItem.QualityScore,
            EvidenceScore: qualityItem.EvidenceScore,
            EvidenceCount: evidenceCount,
            SourceCount: sourceCount,
            LastValidatedUtc: qualityItem.LastValidatedUtc,
            Reasons: reasons,
            BlockingReasons: blockers,
            RequiresHumanReview: true,
            ReviewStatus: reviewStatus);
    }

    private static string BuildMarkdown(TrustedKnowledgeReviewGateReport report)
    {
        var lines = new List<string>
        {
            "# Trusted Knowledge Review Gate",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Total Knowledge Items: {report.TotalKnowledgeItems}",
            $"- Trusted Items: {report.TrustedItemsCount}",
            $"- Eligible for Trusted Review: {report.EligibleForTrustedReview}",
            $"- Blocked Items: {report.BlockedItems}",
            string.Empty,
            "## Top Candidates",
        };

        lines.AddRange(report.TopCandidates.Count == 0
            ? ["- keine"]
            : report.TopCandidates.Select(candidate => $"- {candidate.KnowledgeId} [{candidate.Domain}] trust={candidate.TrustScore:0.###} quality={candidate.QualityScore:0.###}"));

        lines.Add(string.Empty);
        lines.Add("## Blocker");
        lines.AddRange(report.RejectionReasons.Count == 0
            ? ["- keine"]
            : report.RejectionReasons.Select(entry => $"- {entry.Key}: {entry.Value}"));

        return string.Join(Environment.NewLine, lines);
    }
}
