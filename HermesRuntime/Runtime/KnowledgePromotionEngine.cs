namespace Hermes.Runtime;

public sealed record TrustThresholds(
    double WeakToPromisingTrust,
    double WeakToPromisingQuality,
    int WeakToPromisingEvidenceCount,
    double PromisingToRobustTrust,
    double PromisingToRobustQuality,
    int PromisingToRobustSourceCount,
    double RobustToTrustedTrust,
    double RobustToTrustedQuality,
    bool RobustToTrustedRequiresHumanReview)
{
    public static TrustThresholds Default => new(
        WeakToPromisingTrust: 0.55,
        WeakToPromisingQuality: 0.55,
        WeakToPromisingEvidenceCount: 3,
        PromisingToRobustTrust: 0.70,
        PromisingToRobustQuality: 0.70,
        PromisingToRobustSourceCount: 2,
        RobustToTrustedTrust: 0.85,
        RobustToTrustedQuality: 0.85,
        RobustToTrustedRequiresHumanReview: true);
}

public sealed record PromotionRule(
    string FromStatus,
    string ToStatus,
    IReadOnlyList<string> RequiredConditions,
    IReadOnlyList<string> BlockingConditions);

public sealed record DemotionRule(
    string FromStatus,
    string ToStatus,
    IReadOnlyList<string> Triggers);

public sealed record PromotionDecision(
    string KnowledgeId,
    string CurrentStatus,
    string RecommendedStatus,
    string DecisionReason,
    IReadOnlyList<string> SatisfiedConditions,
    IReadOnlyList<string> UnsatisfiedConditions,
    IReadOnlyList<string> Blockers,
    double CurrentTrustScore,
    double CurrentQualityScore,
    double ExpectedTrustDelta,
    bool HumanReviewRequired,
    string DecisionType);

public sealed record TrustedCandidateReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<PromotionDecision> Candidates,
    int TotalCandidates,
    int ReadyForPromotion,
    int AwaitingHumanReview,
    int BlockedCandidates,
    IReadOnlyList<string> TopBlockers,
    string CandidatesPath);

public sealed record PromotionStatusReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int WeakKnowledge,
    int PromisingKnowledge,
    int RobustKnowledge,
    int TrustedKnowledge,
    int DeprecatedKnowledge,
    int RejectedKnowledge,
    string PromotionHealth,
    IReadOnlyList<string> PromotionBlockers,
    IReadOnlyList<PromotionDecision> RecentPromotions,
    IReadOnlyList<PromotionDecision> RecentDemotions,
    TrustedCandidateReport TrustedCandidates,
    string PromotionLogPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading);

public sealed class KnowledgePromotionEngine
{
    private readonly StoragePaths _storagePaths;
    private readonly TrustThresholds _thresholds;

    public KnowledgePromotionEngine(StoragePaths storagePaths, TrustThresholds? thresholds = null)
    {
        _storagePaths = storagePaths;
        _thresholds = thresholds ?? TrustThresholds.Default;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");
    public string PromotionLogPath => Path.Combine(Root, "promotion_log.jsonl");
    public string PromotionStatusPath => Path.Combine(Root, "promotion_status.json");
    public string TrustedCandidatesPath => Path.Combine(Root, "trusted_candidates.json");

    public PromotionStatusReport BuildStatus()
    {
        Directory.CreateDirectory(Root);
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var humanReview = new HumanReviewWorkflow(_storagePaths).BuildSummary();
        var promotionLog = LoadPromotionLog();

        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var statusCounts = qualityReport.Items
            .GroupBy(item => NormalizeStatus(catalogById.GetValueOrDefault(item.KnowledgeId)?.ValidationStatus ?? "weak"))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var weak = statusCounts.GetValueOrDefault("weak", 0) + statusCounts.GetValueOrDefault("needs_more_data", 0) + statusCounts.GetValueOrDefault("untested", 0);
        var promising = statusCounts.GetValueOrDefault("promising", 0) + statusCounts.GetValueOrDefault("experimental", 0);
        var robust = statusCounts.GetValueOrDefault("robust", 0);
        var trusted = statusCounts.GetValueOrDefault("trusted", 0);
        var deprecated = statusCounts.GetValueOrDefault("deprecated", 0);
        var rejected = statusCounts.GetValueOrDefault("rejected", 0) + statusCounts.GetValueOrDefault("overfit_suspected", 0);

        var trustedCandidates = BuildTrustedCandidates(qualityReport, humanReview);
        var blockers = trustedCandidates.TopBlockers
            .Concat(qualityReport.Items.Where(item => item.QualityScore < 0.45).Take(5).Select(item => $"low_quality:{item.KnowledgeId}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        var health = trusted >= 10 && weak < promising
            ? "healthy"
            : trusted >= 5 && robust >= 10
                ? "growing"
                : promising > weak
                    ? "improving"
                    : "needs_attention";

        var recentPromotions = promotionLog
            .Where(decision => decision.DecisionType.Equals("promotion", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(decision => decision.CurrentTrustScore)
            .Take(10)
            .ToList();

        var recentDemotions = promotionLog
            .Where(decision => decision.DecisionType.Equals("demotion", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(decision => decision.CurrentTrustScore)
            .Take(10)
            .ToList();

        var status = new PromotionStatusReport(
            ReportVersion: "promotion_status_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            WeakKnowledge: weak,
            PromisingKnowledge: promising,
            RobustKnowledge: robust,
            TrustedKnowledge: trusted,
            DeprecatedKnowledge: deprecated,
            RejectedKnowledge: rejected,
            PromotionHealth: health,
            PromotionBlockers: blockers,
            RecentPromotions: recentPromotions,
            RecentDemotions: recentDemotions,
            TrustedCandidates: trustedCandidates,
            PromotionLogPath: PromotionLogPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true);

        File.WriteAllText(PromotionStatusPath, System.Text.Json.JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    public TrustedCandidateReport BuildTrustedCandidates(KnowledgeQualityReport? qualityReport = null, HumanReviewSummary? humanReview = null)
    {
        Directory.CreateDirectory(Root);
        qualityReport ??= new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        humanReview ??= new HumanReviewWorkflow(_storagePaths).BuildSummary();

        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var candidates = qualityReport.Items
            .Where(item => item.QualityScore >= 0.68 || item.TrustScore >= 0.68)
            .Where(item => !NormalizeStatus(catalogById.GetValueOrDefault(item.KnowledgeId)?.ValidationStatus ?? "weak").Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .Select(item => EvaluatePromotion(item, catalogById.GetValueOrDefault(item.KnowledgeId), humanReview))
            .Where(decision => !decision.RecommendedStatus.Equals(decision.CurrentStatus, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(decision => decision.CurrentTrustScore)
            .ThenByDescending(decision => decision.CurrentQualityScore)
            .Take(50)
            .ToList();

        var readyForPromotion = candidates.Count(c => c.Blockers.Count == 0 && !c.HumanReviewRequired);
        var awaitingReview = candidates.Count(c => c.HumanReviewRequired && c.Blockers.Count == 0);
        var blocked = candidates.Count(c => c.Blockers.Count > 0);

        var topBlockers = candidates
            .SelectMany(c => c.Blockers)
            .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Take(10)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToList();

        var report = new TrustedCandidateReport(
            ReportVersion: "trusted_candidates_v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Candidates: candidates,
            TotalCandidates: candidates.Count,
            ReadyForPromotion: readyForPromotion,
            AwaitingHumanReview: awaitingReview,
            BlockedCandidates: blocked,
            TopBlockers: topBlockers,
            CandidatesPath: TrustedCandidatesPath);

        File.WriteAllText(TrustedCandidatesPath, System.Text.Json.JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public PromotionDecision EvaluatePromotion(KnowledgeQualityItem qualityItem, KnowledgeCatalogItem? catalogItem, HumanReviewSummary humanReview)
    {
        var currentStatus = NormalizeStatus(catalogItem?.ValidationStatus ?? "weak");
        var satisfied = new List<string>();
        var unsatisfied = new List<string>();
        var blockers = new List<string>();

        var evidenceCount = qualityItem.EvidenceRefs.Count;
        var sourceCount = catalogItem?.SourceIds.Count ?? 0;
        var reviewEvidence = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var hasHumanReview = reviewEvidence.Reviews.Any(review =>
            review.KnowledgeId.Equals(qualityItem.KnowledgeId, StringComparison.OrdinalIgnoreCase)
            && review.Result.Equals("approved", StringComparison.OrdinalIgnoreCase));

        var hasCriticalWarnings = TrustClassification(qualityItem.TrustScore).Equals("unreliable", StringComparison.OrdinalIgnoreCase)
            || qualityItem.LifecycleStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase);

        var recommendedStatus = currentStatus;
        var decisionReason = "no_promotion_needed";
        var humanReviewRequired = false;
        var expectedTrustDelta = 0.0;

        if (currentStatus.Equals("weak", StringComparison.OrdinalIgnoreCase) || currentStatus.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase) || currentStatus.Equals("untested", StringComparison.OrdinalIgnoreCase))
        {
            Check(qualityItem.TrustScore >= _thresholds.WeakToPromisingTrust, "trust_score_sufficient", "trust_score_insufficient");
            Check(qualityItem.QualityScore >= _thresholds.WeakToPromisingQuality, "quality_score_sufficient", "quality_score_insufficient");
            Check(evidenceCount >= _thresholds.WeakToPromisingEvidenceCount, "evidence_count_sufficient", "evidence_count_insufficient");
            Check(!hasCriticalWarnings, "no_critical_warnings", "critical_warnings_present");

            if (unsatisfied.Count == 0 && blockers.Count == 0)
            {
                recommendedStatus = "promising";
                decisionReason = "weak_to_promising_criteria_met";
                expectedTrustDelta = 0.08;
            }
        }
        else if (currentStatus.Equals("promising", StringComparison.OrdinalIgnoreCase) || currentStatus.Equals("experimental", StringComparison.OrdinalIgnoreCase))
        {
            Check(qualityItem.TrustScore >= _thresholds.PromisingToRobustTrust, "trust_score_sufficient", "trust_score_insufficient");
            Check(qualityItem.QualityScore >= _thresholds.PromisingToRobustQuality, "quality_score_sufficient", "quality_score_insufficient");
            Check(sourceCount >= _thresholds.PromisingToRobustSourceCount, "source_count_sufficient", "source_count_insufficient");
            Check(qualityItem.ValidationScore >= 0.68, "validation_successful", "validation_insufficient");
            Check(!hasCriticalWarnings, "no_blockers", "blockers_present");

            if (unsatisfied.Count == 0 && blockers.Count == 0)
            {
                recommendedStatus = "robust";
                decisionReason = "promising_to_robust_criteria_met";
                expectedTrustDelta = 0.12;
            }
        }
        else if (currentStatus.Equals("robust", StringComparison.OrdinalIgnoreCase))
        {
            Check(qualityItem.TrustScore >= _thresholds.RobustToTrustedTrust, "trust_score_sufficient", "trust_score_insufficient");
            Check(qualityItem.QualityScore >= _thresholds.RobustToTrustedQuality, "quality_score_sufficient", "quality_score_insufficient");
            Check(EvidenceClassification(qualityItem.EvidenceScore).Equals("strong", StringComparison.OrdinalIgnoreCase), "strong_evidence", "evidence_not_strong");
            Check(hasHumanReview, "human_review_approved", "human_review_required");
            Check(TrustClassification(qualityItem.TrustScore).Equals("trusted", StringComparison.OrdinalIgnoreCase) || TrustClassification(qualityItem.TrustScore).Equals("reliable", StringComparison.OrdinalIgnoreCase), "trust_classification_reliable", "trust_classification_insufficient");

            if (unsatisfied.Count == 0 && blockers.Count == 0)
            {
                recommendedStatus = "trusted";
                decisionReason = "robust_to_trusted_criteria_met";
                expectedTrustDelta = 0.15;
            }
            else if (unsatisfied.Any(u => u.Contains("human_review")))
            {
                humanReviewRequired = true;
                recommendedStatus = "robust";
                decisionReason = "awaiting_human_review";
            }
        }

        return new PromotionDecision(
            KnowledgeId: qualityItem.KnowledgeId,
            CurrentStatus: currentStatus,
            RecommendedStatus: recommendedStatus,
            DecisionReason: decisionReason,
            SatisfiedConditions: satisfied,
            UnsatisfiedConditions: unsatisfied,
            Blockers: blockers,
            CurrentTrustScore: qualityItem.TrustScore,
            CurrentQualityScore: qualityItem.QualityScore,
            ExpectedTrustDelta: expectedTrustDelta,
            HumanReviewRequired: humanReviewRequired,
            DecisionType: recommendedStatus != currentStatus ? "promotion" : "no_change");

        void Check(bool condition, string satisfiedRef, string unsatisfiedRef)
        {
            if (condition)
            {
                satisfied.Add(satisfiedRef);
            }
            else
            {
                unsatisfied.Add(unsatisfiedRef);
                if (unsatisfiedRef.Contains("critical") || unsatisfiedRef.Contains("blocker"))
                {
                    blockers.Add(unsatisfiedRef);
                }
            }
        }
    }

    public void ApplyPromotions(IReadOnlyList<PromotionDecision> decisions, bool dryRun = false)
    {
        Directory.CreateDirectory(Root);
        var catalog = new KnowledgeCatalog(_storagePaths);
        var items = catalog.LoadOrCreateItems().ToList();
        var updated = new List<KnowledgeCatalogItem>();

        foreach (var decision in decisions.Where(d => !d.RecommendedStatus.Equals(d.CurrentStatus, StringComparison.OrdinalIgnoreCase)))
        {
            var item = items.FirstOrDefault(i => i.Id.Equals(decision.KnowledgeId, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                continue;
            }

            if (decision.Blockers.Count > 0)
            {
                LogPromotion(decision with { DecisionType = "blocked" });
                continue;
            }

            if (decision.HumanReviewRequired && !decision.SatisfiedConditions.Contains("human_review_approved"))
            {
                LogPromotion(decision with { DecisionType = "awaiting_human_review" });
                continue;
            }

            if (!dryRun)
            {
                var updatedItem = item with { ValidationStatus = decision.RecommendedStatus, LastValidatedUtc = DateTimeOffset.UtcNow };
                updated.Add(updatedItem);
                items[items.FindIndex(i => i.Id.Equals(decision.KnowledgeId, StringComparison.OrdinalIgnoreCase))] = updatedItem;
            }

            LogPromotion(decision);
        }

        if (!dryRun && updated.Count > 0)
        {
            File.WriteAllText(catalog.CatalogPath, System.Text.Json.JsonSerializer.Serialize(items, JsonDefaults.WriteOptions));
        }
    }

    private void LogPromotion(PromotionDecision decision)
    {
        Directory.CreateDirectory(Root);
        var logEntry = System.Text.Json.JsonSerializer.Serialize(decision, JsonDefaults.WriteOptions);
        File.AppendAllText(PromotionLogPath, logEntry + Environment.NewLine);
    }

    private IReadOnlyList<PromotionDecision> LoadPromotionLog()
    {
        if (!File.Exists(PromotionLogPath))
        {
            return Array.Empty<PromotionDecision>();
        }

        return File.ReadAllLines(PromotionLogPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<PromotionDecision>(line, JsonDefaults.ReadOptions);
                }
                catch
                {
                    return null;
                }
            })
            .Where(decision => decision is not null)
            .Cast<PromotionDecision>()
            .ToList();
    }

    private static string NormalizeStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "needs_more_data" => "weak",
            "untested" => "weak",
            "experimental" => "promising",
            "overfit_suspected" => "rejected",
            _ => status.ToLowerInvariant()
        };

    public static string TrustClassification(double trustScore) =>
        trustScore >= 0.85 ? "trusted" :
        trustScore >= 0.68 ? "reliable" :
        trustScore >= 0.48 ? "moderate" :
        "unreliable";

    public static string EvidenceClassification(double evidenceScore) =>
        evidenceScore >= 0.75 ? "strong" :
        evidenceScore >= 0.48 ? "moderate" :
        "weak";
}
