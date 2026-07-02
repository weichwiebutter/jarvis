using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousKnowledgeAdvancementPlan(
    string KnowledgeId,
    string Title,
    string CurrentStatus,
    string RootCause,
    string NextAction,
    IReadOnlyList<string> FollowedBy,
    string OperatorRequired,
    int SourceCount,
    int PolicyApprovedSourceCount,
    double ValidationScore,
    double TrustScore,
    double QualityScore,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Reasons,
    double ImpactScore);

public sealed record AutonomousKnowledgeAdvancementReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int CandidateSupportItems,
    int PrioritizedItems,
    int PlansCreated,
    IReadOnlyList<AutonomousKnowledgeAdvancementPlan> Plans,
    IReadOnlyList<string> UsedKnowledgeIds,
    IReadOnlyList<string> UsedTopics,
    IReadOnlyList<string> Warnings,
    string RootCauseSummary,
    string ReportPath,
    string MarkdownPath,
    bool ReadOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool Executed);

public sealed class AutonomousKnowledgeAdvancementEngineService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public AutonomousKnowledgeAdvancementEngineService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_knowledge_advancement");
    public string ReportPath => Path.Combine(Root, "autonomous_knowledge_advancement_report.json");
    public string MarkdownPath => Path.Combine(Root, "autonomous_knowledge_advancement_report.md");

    public AutonomousKnowledgeAdvancementReport Run(int maxItems = 12, bool execute = false)
    {
        Directory.CreateDirectory(Root);

        var knowledgeItems = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var qualityById = qualityReport.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var confirmations = new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
        var confirmationById = confirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var validationState = new ValidationStateSynchronizerService(_storagePaths).LoadStatus();
        var promotionReport = new KnowledgeTrustPromotionPipelineService(_storagePaths).Load();

        var candidatePlans = knowledgeItems
            .Select(item => BuildPlan(item, qualityById.GetValueOrDefault(item.Id), confirmationById.GetValueOrDefault(item.Id), validationState, promotionReport))
            .Where(plan => plan is not null)
            .Cast<AutonomousKnowledgeAdvancementPlan>()
            .OrderByDescending(plan => plan.ImpactScore)
            .ThenByDescending(plan => plan.PolicyApprovedSourceCount)
            .ThenByDescending(plan => plan.TrustScore)
            .ThenByDescending(plan => plan.QualityScore)
            .ThenByDescending(plan => plan.ValidationScore)
            .ThenBy(plan => plan.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxItems))
            .ToList();

        if (execute)
        {
            ExecuteExistingServices(maxItems);
        }

        var usedKnowledgeIds = candidatePlans.Select(plan => plan.KnowledgeId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var usedTopics = candidatePlans.Select(plan => InferTopic(plan.KnowledgeId, plan.Title)).Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!;

        var report = new AutonomousKnowledgeAdvancementReport(
            ReportVersion: "autonomous_knowledge_advancement_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: execute ? "executed" : "planned",
            LoadedItems: knowledgeItems.Count,
            CandidateSupportItems: candidatePlans.Count,
            PrioritizedItems: candidatePlans.Count,
            PlansCreated: candidatePlans.Count,
            Plans: candidatePlans,
            UsedKnowledgeIds: usedKnowledgeIds,
            UsedTopics: usedTopics,
            Warnings: BuildWarnings(candidatePlans),
            RootCauseSummary: BuildRootCauseSummary(candidatePlans),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ReadOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Executed: execute);

        WriteReport(report);
        return report;
    }

    public AutonomousKnowledgeAdvancementReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousKnowledgeAdvancementReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private AutonomousKnowledgeAdvancementPlan? BuildPlan(
        KnowledgeCatalogItem item,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        ValidationStateSynchronizerReport? validationState,
        KnowledgeTrustPromotionReport? promotionReport)
    {
        var sourceCount = SourceConfirmationEngine.CanonicalSourceCount(item, confirmation);
        var policyApprovedSourceCount = SourceConfirmationEngine.ApprovedSourceCount(confirmation);
        var trustScore = quality?.TrustScore ?? 0;
        var qualityScore = quality?.QualityScore ?? 0;
        var validationScore = quality?.ValidationScore ?? 0;
        var blockers = BuildBlockers(item, quality, confirmation, validationState, promotionReport, sourceCount, policyApprovedSourceCount);
        var rootCause = DetermineRootCause(blockers, sourceCount, policyApprovedSourceCount, quality, confirmation);
        if (rootCause is null)
        {
            return null;
        }

        var nextAction = DetermineNextAction(rootCause);
        var followedBy = BuildFollowedBy(nextAction);
        var impact = ComputeImpact(sourceCount, policyApprovedSourceCount, trustScore, qualityScore, validationScore, blockers);
        return new AutonomousKnowledgeAdvancementPlan(
            KnowledgeId: item.Id,
            Title: item.Title,
            CurrentStatus: item.ValidationStatus,
            RootCause: rootCause,
            NextAction: nextAction,
            FollowedBy: followedBy,
            OperatorRequired: "no",
            SourceCount: sourceCount,
            PolicyApprovedSourceCount: policyApprovedSourceCount,
            ValidationScore: Math.Round(Math.Clamp(validationScore, 0, 1), 4),
            TrustScore: Math.Round(Math.Clamp(trustScore, 0, 1), 4),
            QualityScore: Math.Round(Math.Clamp(qualityScore, 0, 1), 4),
            Blockers: blockers,
            Reasons: BuildReasons(item, quality, confirmation, sourceCount, policyApprovedSourceCount, rootCause),
            ImpactScore: impact);
    }

    private static IReadOnlyList<string> BuildBlockers(
        KnowledgeCatalogItem item,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        ValidationStateSynchronizerReport? validationState,
        KnowledgeTrustPromotionReport? promotionReport,
        int sourceCount,
        int policyApprovedSourceCount)
    {
        var blockers = new List<string>();
        if (sourceCount < 2 || policyApprovedSourceCount == 0)
        {
            blockers.Add(sourceCount <= 1 ? "second_independent_source_missing" : "policy_approved_second_source_missing");
        }

        if (quality is null)
        {
            blockers.Add("quality_missing");
            return blockers;
        }

        var reasons = quality.Reasons ?? [];
        if (reasons.Any(reason => reason.Contains("validation_plan_missing", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add("validation_plan_missing");
        }

        if (quality.ValidationScore < 0.6 && !blockers.Contains("validation_plan_missing", StringComparer.OrdinalIgnoreCase))
        {
            blockers.Add("quality_score_too_low");
        }

        var hasFreshValidation = quality.LastValidatedUtc.HasValue || reasons.Any(reason => reason.Contains("confirmation:validated", StringComparison.OrdinalIgnoreCase));
        if (!hasFreshValidation && sourceCount >= 2)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (reasons.Any(reason => reason.Contains("contradiction", StringComparison.OrdinalIgnoreCase))
            || promotionReport?.Candidates.Any(candidate => candidate.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase) && candidate.Blockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase))) == true)
        {
            blockers.Add("blocking_contradiction");
        }

        if (validationState is not null)
        {
            var validationItem = validationState.Items.FirstOrDefault(entry => entry.KnowledgeItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (validationItem is not null && validationItem.RemainingBlockersAfter.Count > 0)
            {
                foreach (var blocker in validationItem.RemainingBlockersAfter)
                {
                    if (!blockers.Contains(blocker, StringComparer.OrdinalIgnoreCase))
                    {
                        blockers.Add(blocker);
                    }
                }
            }
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? DetermineRootCause(
        IReadOnlyList<string> blockers,
        int sourceCount,
        int policyApprovedSourceCount,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation)
    {
        if (blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "blocking_contradiction";
        }

        if (sourceCount < 2 || policyApprovedSourceCount == 0 || blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "second_independent_source_missing";
        }

        if (blockers.Any(blocker => blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "validation_plan_missing";
        }

        var hasExecution = quality?.EvidenceRefs?.Any(refId => refId.StartsWith("validation:", StringComparison.OrdinalIgnoreCase)) == true;
        if (hasExecution && (quality?.LastValidatedUtc is null || blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase))))
        {
            return "fresh_validation_timestamp_missing";
        }

        if ((quality?.QualityScore ?? 0) < 0.64 || blockers.Any(blocker => blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            return "quality_score_too_low";
        }

        if (confirmation?.ReviewStatus?.Equals("candidate_second_source", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "awaiting_source_policy";
        }

        return "awaiting_external_evidence";
    }

    private static string DetermineNextAction(string rootCause) => rootCause switch
    {
        "second_independent_source_missing" => "known-article-seed-fetch",
        "validation_plan_missing" => "validation-evidence --apply",
        "fresh_validation_timestamp_missing" => "validation-state-sync --apply",
        "blocking_contradiction" => "knowledge-state-consistency-check",
        "quality_score_too_low" => "knowledge-state-consistency-repair --apply",
        _ => "await_external_evidence"
    };

    private static IReadOnlyList<string> BuildFollowedBy(string nextAction)
    {
        if (nextAction == "known-article-seed-fetch")
        {
            return
            [
                "web-research-import",
                "knowledge-evidence-match",
                "independent-source-resolver",
                "auto-source-review",
                "validation-state-sync",
                "knowledge-trust-promote"
            ];
        }

        if (nextAction == "validation-evidence --apply")
        {
            return
            [
                "validation-state-sync",
                "knowledge-trust-promote"
            ];
        }

        if (nextAction == "validation-state-sync --apply")
        {
            return ["knowledge-trust-promote"];
        }

        if (nextAction == "knowledge-state-consistency-check")
        {
            return
            [
                "knowledge-state-consistency-repair",
                "validation-state-sync",
                "knowledge-trust-promote"
            ];
        }

        if (nextAction == "knowledge-state-consistency-repair --apply")
        {
            return
            [
                "validation-state-sync",
                "knowledge-trust-promote"
            ];
        }

        return
        [
            "validation-evidence",
            "validation-state-sync",
            "knowledge-trust-promote"
        ];
    }

    private static double ComputeImpact(
        int sourceCount,
        int policyApprovedSourceCount,
        double trustScore,
        double qualityScore,
        double validationScore,
        IReadOnlyList<string> blockers)
    {
        var sourceGap = sourceCount < 2 ? 1.0 : 0.0;
        var policyGap = policyApprovedSourceCount == 0 ? 1.0 : 0.0;
        var scoreGap = Math.Max(0, 1 - Math.Min(trustScore, Math.Min(qualityScore, validationScore)));
        var blockerWeight = blockers.Count switch
        {
            0 => 0,
            1 => 0.08,
            2 => 0.12,
            _ => 0.16
        };
        return Math.Round(Math.Clamp(sourceGap * 0.4 + policyGap * 0.2 + scoreGap * 0.3 + blockerWeight, 0, 1), 4);
    }

    private static IReadOnlyList<string> BuildReasons(
        KnowledgeCatalogItem item,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        int sourceCount,
        int policyApprovedSourceCount,
        string rootCause)
    {
        var reasons = new List<string>();
        reasons.Add($"current_status:{item.ValidationStatus}");
        reasons.Add($"source_count:{sourceCount}");
        reasons.Add($"policy_approved_source_count:{policyApprovedSourceCount}");
        reasons.Add($"root_cause:{rootCause}");
        if (quality is not null)
        {
            reasons.Add($"trust_score:{quality.TrustScore:0.###}");
            reasons.Add($"quality_score:{quality.QualityScore:0.###}");
            reasons.Add($"validation_score:{quality.ValidationScore:0.###}");
        }
        if (confirmation is not null)
        {
            reasons.Add($"review_status:{confirmation.ReviewStatus}");
        }
        return reasons;
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<AutonomousKnowledgeAdvancementPlan> plans)
    {
        var warnings = new List<string>();
        if (plans.Count == 0)
        {
            warnings.Add("no_candidate_support_items_detected");
        }
        if (plans.Any(plan => plan.RootCause.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("second_independent_source_missing");
        }
        return warnings;
    }

    private static string? InferTopic(params string?[] values)
    {
        var joined = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(joined))
        {
            return null;
        }

        var normalized = joined.Replace("_", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .ToLowerInvariant();

        var candidates = new (string Topic, string[] Aliases, int Weight)[]
        {
            ("bullish engulfing", ["bullish engulfing", "bullish", "engulfing"], 100),
            ("bearish engulfing", ["bearish engulfing", "bearish", "engulfing"], 100),
            ("double top", ["double top", "doubletop"], 96),
            ("double bottom", ["double bottom", "doublebottom"], 96),
            ("support resistance", ["support resistance", "support", "resistance"], 94),
            ("inside bar", ["inside bar", "insidebar"], 92),
            ("breakout", ["breakout", "break out", "break outs"], 90),
            ("gap trading", ["gap trading", "gap trade", "gap"], 88),
            ("daytrading", ["daytrading", "day trading", "intraday"], 86),
            ("pullback", ["pullback", "pull back"], 84),
            ("pin bar", ["pin bar", "pinbar"], 82),
            ("hammer", ["hammer"], 80),
            ("doji", ["doji"], 80),
            ("liquidity sweep", ["liquidity sweep", "sweep"], 78),
            ("mean reversion", ["mean reversion", "mean revert", "reversion"], 76)
        };

        return candidates
            .Select(candidate => new
            {
                candidate.Topic,
                Score = candidate.Weight + candidate.Aliases.Sum(alias => normalized.Contains(alias, StringComparison.OrdinalIgnoreCase) ? 25 : 0)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Topic, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(candidate => candidate.Score > 0)
            ?.Topic;
    }

    private static string BuildRootCauseSummary(IReadOnlyList<AutonomousKnowledgeAdvancementPlan> plans)
    {
        if (plans.Count == 0)
        {
            return "Keine Candidate-Support-Items gefunden.";
        }

        var top = plans.First();
        return $"Knowledge Item {top.KnowledgeId} ({top.Title}) ist aktuell {top.CurrentStatus}. Root Cause: {top.RootCause}. Nächster Schritt: {top.NextAction}. Followed By: {string.Join(", ", top.FollowedBy)}. Operator Required: {top.OperatorRequired}.";
    }

    private void ExecuteExistingServices(int maxItems)
    {
        _ = new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot).Run(maxItems, dryRun: false, maxFetchSeconds: 60);
        _ = new WebResearchSourceImportService(_storagePaths).Run(apply: true);
        _ = new KnowledgeEvidenceSemanticMatcherService(_storagePaths).Run(apply: true);
        _ = new IndependentSourceResolverService(_storagePaths).Run(apply: true);
        _ = new AutoSourceReviewPolicyService(_storagePaths, _runtimeRoot).Run(apply: true);
        _ = new ValidationStateSynchronizerService(_storagePaths).Run(apply: true, dryRun: false);
        _ = new KnowledgeTrustPromotionPipelineService(_storagePaths).Run(apply: true, skipRefresh: true);
    }

    private void WriteReport(AutonomousKnowledgeAdvancementReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(AutonomousKnowledgeAdvancementReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Knowledge Advancement Engine");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- loaded_items: {report.LoadedItems}");
        sb.AppendLine($"- candidate_support_items: {report.CandidateSupportItems}");
        sb.AppendLine($"- prioritized_items: {report.PrioritizedItems}");
        sb.AppendLine($"- plans_created: {report.PlansCreated}");
        sb.AppendLine($"- root_cause_summary: {report.RootCauseSummary}");
        sb.AppendLine();

        foreach (var plan in report.Plans)
        {
            sb.AppendLine($"## {plan.KnowledgeId} / {plan.Title}");
            sb.AppendLine($"- current_status: {plan.CurrentStatus}");
            sb.AppendLine($"- root_cause: {plan.RootCause}");
            sb.AppendLine($"- next_action: {plan.NextAction}");
            sb.AppendLine($"- followed_by: {string.Join(", ", plan.FollowedBy)}");
            sb.AppendLine($"- operator_required: {plan.OperatorRequired}");
            sb.AppendLine($"- source_count: {plan.SourceCount}");
            sb.AppendLine($"- policy_approved_source_count: {plan.PolicyApprovedSourceCount}");
            sb.AppendLine($"- validation_score: {plan.ValidationScore:0.###}");
            sb.AppendLine($"- trust_score: {plan.TrustScore:0.###}");
            sb.AppendLine($"- quality_score: {plan.QualityScore:0.###}");
            sb.AppendLine($"- impact_score: {plan.ImpactScore:0.###}");
            sb.AppendLine($"- blockers: {string.Join(", ", plan.Blockers)}");
            sb.AppendLine($"- reasons: {string.Join(", ", plan.Reasons)}");
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
