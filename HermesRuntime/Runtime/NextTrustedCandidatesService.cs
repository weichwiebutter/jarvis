using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record NextTrustedCandidateItem(
    string KnowledgeId,
    string Domain,
    string Title,
    string CurrentStatus,
    string RecommendedStatus,
    string PromotionOutcome,
    bool EligibleForPromotion,
    int SourceCount,
    int PolicyApprovedSourceCount,
    IReadOnlyList<string> BestCandidateSources,
    IReadOnlyList<string> MissingEvidence,
    string ValidationPlanStatus,
    IReadOnlyList<string> Contradictions,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    string NextAction,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record NextTrustedCandidatesReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TotalItems,
    IReadOnlyList<NextTrustedCandidateItem> Items,
    IReadOnlyDictionary<string, int> NextActions,
    IReadOnlyDictionary<string, int> BlockerCounts,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string KnowledgeQualityPath,
    string KnowledgeEvidencePath,
    string ValidationPlansPath,
    string PromotionReportPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class NextTrustedCandidatesService
{
    private static readonly IReadOnlyList<string> TargetKnowledgeIds =
    [
        "trading:double_top",
        "trading:double_bottom",
        "trading:breakout",
        "trading:inside_bar",
        "trading:gap_trading",
        "trading:daytrading",
        "trading:pin_bar",
        "trading:pullback"
    ];

    private readonly StoragePaths _storagePaths;

    public NextTrustedCandidatesService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "next_trusted_candidates");

    public string ReportPath => Path.Combine(Root, "next_trusted_candidates_report.json");

    public string MarkdownPath => Path.Combine(Root, "next_trusted_candidates_report.md");

    public NextTrustedCandidatesReport Run()
    {
        Directory.CreateDirectory(Root);

        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var evidence = LoadEvidenceReport();
        var confirmations = new SourceConfirmationEngine(_storagePaths).LoadReport();
        var validationPlans = new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport();
        var promotion = new KnowledgeTrustPromotionPipelineService(_storagePaths).Load()
            ?? new KnowledgeTrustPromotionPipelineService(_storagePaths).Run(apply: false);
        var promotionById = promotion.Candidates
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var qualityById = quality.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidence?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmations?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var planById = validationPlans?.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeValidationPlan>(StringComparer.OrdinalIgnoreCase);

        var items = new List<NextTrustedCandidateItem>();
        var warnings = new List<string>();
        foreach (var knowledgeId in TargetKnowledgeIds)
        {
            var promotionCandidate = promotionById.GetValueOrDefault(knowledgeId);
            if (promotionCandidate is null)
            {
                warnings.Add($"promotion_candidate_missing:{knowledgeId}");
                continue;
            }

            var qualityItem = qualityById.GetValueOrDefault(knowledgeId);
            var evidenceItem = evidenceById.GetValueOrDefault(knowledgeId);
            var confirmation = confirmationById.GetValueOrDefault(knowledgeId);
            var plan = planById.GetValueOrDefault(knowledgeId);
            var bestCandidateSources = BuildBestCandidateSources(confirmation);
            var blockers = promotionCandidate.Blockers.ToList();
            var missingEvidence = promotionCandidate.MissingEvidenceCategories.ToList();
            var contradictions = ExtractContradictions(qualityItem, evidenceItem, promotionCandidate);
            var nextAction = DetermineNextAction(promotionCandidate, confirmation, plan, qualityItem, contradictions);

            items.Add(new NextTrustedCandidateItem(
                KnowledgeId: knowledgeId,
                Domain: promotionCandidate.Domain,
                Title: promotionCandidate.Title,
                CurrentStatus: promotionCandidate.CurrentStatus,
                RecommendedStatus: promotionCandidate.RecommendedStatus,
                PromotionOutcome: promotionCandidate.PromotionOutcome,
                EligibleForPromotion: promotionCandidate.EligibleForPromotion,
                SourceCount: promotionCandidate.SourceCount,
                PolicyApprovedSourceCount: confirmation?.PolicyApprovedSourceCount ?? 0,
                BestCandidateSources: bestCandidateSources,
                MissingEvidence: missingEvidence,
                ValidationPlanStatus: plan?.Status ?? "missing",
                Contradictions: contradictions,
                TrustScore: promotionCandidate.TrustScore,
                QualityScore: promotionCandidate.QualityScore,
                ValidationScore: promotionCandidate.ValidationScore,
                NextAction: nextAction,
                Blockers: blockers,
                Warnings: BuildWarnings(knowledgeId, promotionCandidate, confirmation, plan, qualityItem, evidenceItem)));
        }

        var report = new NextTrustedCandidatesReport(
            ReportVersion: "next_trusted_candidates_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: items.Count == 0 ? "empty" : "ready",
            TotalItems: items.Count,
            Items: items,
            NextActions: items
                .GroupBy(item => item.NextAction, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            BlockerCounts: items
                .SelectMany(item => item.Blockers)
                .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            Warnings: warnings,
            SourceConfirmationsPath: Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json"),
            KnowledgeQualityPath: Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json"),
            KnowledgeEvidencePath: Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json"),
            ValidationPlansPath: Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json"),
            PromotionReportPath: Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_promotion", "knowledge_trust_promotion_report.json"),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        return report;
    }

    public NextTrustedCandidatesReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NextTrustedCandidatesReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> BuildBestCandidateSources(ConfirmationResult? confirmation)
    {
        if (confirmation?.CandidateSources is null || confirmation.CandidateSources.Count == 0)
        {
            return [];
        }

        return confirmation.CandidateSources
            .OrderByDescending(candidate => candidate.AutoApprovedByPolicy)
            .ThenByDescending(candidate => candidate.SemanticMatchScore)
            .ThenByDescending(candidate => candidate.IndependenceScore)
            .ThenBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(candidate =>
            {
                var flags = new List<string>();
                if (candidate.AutoApprovedByPolicy)
                {
                    flags.Add("policy_approved");
                }
                if (!string.IsNullOrWhiteSpace(candidate.SourceStatus))
                {
                    flags.Add(candidate.SourceStatus);
                }
                return $"{candidate.Domain} | {candidate.Url} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | {string.Join(", ", flags.Distinct(StringComparer.OrdinalIgnoreCase))}";
            })
            .ToList();
    }

    private KnowledgeEvidenceReport? LoadEvidenceReport()
    {
        var path = Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractContradictions(KnowledgeQualityItem? qualityItem, KnowledgeEvidenceEntry? evidenceItem, KnowledgeTrustPromotionCandidate promotionCandidate)
    {
        var contradictions = new List<string>();

        if (promotionCandidate.Blockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            contradictions.Add("blocking_contradiction");
        }

        if (qualityItem is not null && qualityItem.Reasons.Any(reason => reason.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            contradictions.Add("quality_contradiction_signal");
        }

        if (evidenceItem is not null && evidenceItem.OutcomeRefs.Any(reference => reference.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            contradictions.Add("evidence_contradiction_signal");
        }

        return contradictions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string DetermineNextAction(
        KnowledgeTrustPromotionCandidate candidate,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeQualityItem? qualityItem,
        IReadOnlyList<string> contradictions)
    {
        if (contradictions.Any(item => item.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "resolve_contradiction";
        }

        var policyApprovedSourceCount = confirmation?.PolicyApprovedSourceCount ?? 0;

        if (candidate.SourceCount < 2 || policyApprovedSourceCount == 0)
        {
            return "add_second_source_seed";
        }

        if (candidate.Blockers.Any(blocker => blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase))
            || plan is null
            || string.IsNullOrWhiteSpace(plan.Status))
        {
            return "create_validation_plan";
        }

        if (candidate.Blockers.Any(blocker =>
                blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                || blocker.Equals("fresh_validation_timestamp", StringComparison.OrdinalIgnoreCase))
            && candidate.ValidationScore > 0)
        {
            return "run_validation_state_sync";
        }

        if (qualityItem is not null && qualityItem.QualityScore < 0.64
            || candidate.Blockers.Any(blocker => blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            return "improve_quality_score";
        }

        if (candidate.Blockers.Any(blocker =>
                blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)
                || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase)
                || blocker.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase))
            || confirmation?.CandidateSources is null
            || confirmation.CandidateSources.Count == 0)
        {
            return "await_external_evidence";
        }

        return "await_external_evidence";
    }

    private static IReadOnlyList<string> BuildWarnings(
        string knowledgeId,
        KnowledgeTrustPromotionCandidate promotionCandidate,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeQualityItem? qualityItem,
        KnowledgeEvidenceEntry? evidenceItem)
    {
        var warnings = new List<string>();

        if (confirmation is null)
        {
            warnings.Add("source_confirmation_missing");
        }

        if (plan is null)
        {
            warnings.Add("validation_plan_missing");
        }

        if (qualityItem is null)
        {
            warnings.Add("quality_missing");
        }

        if (evidenceItem is null)
        {
            warnings.Add("evidence_missing");
        }

        if (promotionCandidate.Blockers.Count == 0 && !promotionCandidate.EligibleForPromotion)
        {
            warnings.Add("not_eligible_but_unblocked");
        }

        if (string.IsNullOrWhiteSpace(knowledgeId))
        {
            warnings.Add("knowledge_id_missing");
        }

        return warnings;
    }

    private void WriteReport(NextTrustedCandidatesReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(NextTrustedCandidatesReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Next Trusted Candidates");
        sb.AppendLine();
        sb.AppendLine($"- Updated: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Total Items: {report.TotalItems}");
        sb.AppendLine($"- Research Only: {report.ResearchOnly}");
        sb.AppendLine($"- No Trading Execution: {report.NoTradingExecution}");
        sb.AppendLine($"- No Broker Action: {report.NoBrokerAction}");
        sb.AppendLine($"- No Auto Trading: {report.NoAutoTrading}");
        sb.AppendLine($"- Human Review Required: {report.HumanReviewRequired}");
        sb.AppendLine();
        sb.AppendLine("## Next Actions");
        foreach (var group in report.NextActions.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {group.Key}: {group.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Items");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.Title} / {item.KnowledgeId}");
            sb.AppendLine($"- Current Status: {item.CurrentStatus}");
            sb.AppendLine($"- Recommended Status: {item.RecommendedStatus}");
            sb.AppendLine($"- Promotion Outcome: {item.PromotionOutcome}");
            sb.AppendLine($"- Eligible For Promotion: {item.EligibleForPromotion}");
            sb.AppendLine($"- Source Count: {item.SourceCount}");
            sb.AppendLine($"- Policy Approved Source Count: {item.PolicyApprovedSourceCount}");
            sb.AppendLine($"- Trust Score: {item.TrustScore:0.###}");
            sb.AppendLine($"- Quality Score: {item.QualityScore:0.###}");
            sb.AppendLine($"- Validation Score: {item.ValidationScore:0.###}");
            sb.AppendLine($"- Validation Plan Status: {item.ValidationPlanStatus}");
            sb.AppendLine($"- Next Action: {item.NextAction}");
            if (item.BestCandidateSources.Count > 0)
            {
                sb.AppendLine("- Best Candidate Sources:");
                foreach (var source in item.BestCandidateSources)
                {
                    sb.AppendLine($"  - {source}");
                }
            }
            if (item.MissingEvidence.Count > 0)
            {
                sb.AppendLine("- Missing Evidence:");
                foreach (var missing in item.MissingEvidence)
                {
                    sb.AppendLine($"  - {missing}");
                }
            }
            if (item.Contradictions.Count > 0)
            {
                sb.AppendLine("- Contradictions:");
                foreach (var contradiction in item.Contradictions)
                {
                    sb.AppendLine($"  - {contradiction}");
                }
            }
            if (item.Blockers.Count > 0)
            {
                sb.AppendLine("- Blockers:");
                foreach (var blocker in item.Blockers)
                {
                    sb.AppendLine($"  - {blocker}");
                }
            }
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
