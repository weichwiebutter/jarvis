using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeTrustImprovementAction(
    string ActionId,
    string Blocker,
    string Title,
    string Domain,
    string Priority,
    string Reason,
    string SuggestedAction,
    bool AutoFixable,
    bool RequiresHumanReview,
    string Status);

public sealed record KnowledgeTrustImprovementItem(
    string KnowledgeId,
    string Domain,
    string Title,
    IReadOnlyList<string> Blockers,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    IReadOnlyList<string> PlannedActions,
    string Priority,
    bool AutoFixable,
    bool RequiresHumanReview);

public sealed record KnowledgeTrustImprovementPlanReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalBlockedItems,
    IReadOnlyDictionary<string, int> BlockerCounts,
    IReadOnlyList<KnowledgeTrustImprovementAction> PlannedActions,
    string EstimatedEffort,
    int AutoFixableCount,
    int HumanReviewCount,
    IReadOnlyList<KnowledgeTrustImprovementItem> TopPriorityItems,
    string NextRecommendedAction,
    bool RequiresHumanReview,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    string ReviewGatePath,
    string ReviewQueuePath,
    string ImprovementQueuePath,
    string ReportPath,
    string MarkdownPath);

public sealed class KnowledgeTrustImprovementPlannerService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeTrustImprovementPlannerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_improvement_plan");

    public string ReportPath => Path.Combine(Root, "knowledge_trust_improvement_plan.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_trust_improvement_plan.md");

    public KnowledgeTrustImprovementPlanReport Run()
    {
        Directory.CreateDirectory(Root);
        var gateService = new TrustedKnowledgeReviewGateService(_storagePaths);
        var gate = gateService.Load() ?? gateService.Run();
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionsByKnowledgeId = contradictions.Contradictions
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var humanReviewQueue = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var reviewByKnowledgeId = humanReviewQueue.Items
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var blockerCounts = gate.RejectionReasons
            .OrderByDescending(pair => pair.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var plannedActions = new List<KnowledgeTrustImprovementAction>();
        var topItems = new List<KnowledgeTrustImprovementItem>();
        var autoFixableCount = 0;
        var humanReviewCount = 0;

        foreach (var qualityItem in quality.Items)
        {
            if (!catalogById.TryGetValue(qualityItem.KnowledgeId, out var catalogItem))
            {
                continue;
            }

            var blockers = BuildBlockers(qualityItem, catalogItem, contradictionsByKnowledgeId, reviewByKnowledgeId);
            if (blockers.Count == 0)
            {
                continue;
            }

            var candidate = new TrustedKnowledgeReviewCandidate(
                KnowledgeId: qualityItem.KnowledgeId,
                Domain: qualityItem.Domain,
                Title: catalogItem.Title,
                TrustScore: qualityItem.TrustScore,
                QualityScore: qualityItem.QualityScore,
                EvidenceScore: qualityItem.EvidenceScore,
                EvidenceCount: qualityItem.EvidenceRefs.Count,
                SourceCount: catalogItem.SourceIds.Count,
                LastValidatedUtc: qualityItem.LastValidatedUtc,
                Reasons: blockers,
                BlockingReasons: blockers,
                RequiresHumanReview: blockers.Contains("pending_human_review", StringComparer.OrdinalIgnoreCase),
                ReviewStatus: reviewByKnowledgeId.TryGetValue(qualityItem.KnowledgeId, out var review) ? review.Status : "none");

            var actions = MapPlannedActions(candidate, blockers);
            var item = new KnowledgeTrustImprovementItem(
                KnowledgeId: candidate.KnowledgeId,
                Domain: candidate.Domain,
                Title: candidate.Title,
                Blockers: blockers,
                TrustScore: candidate.TrustScore,
                QualityScore: candidate.QualityScore,
                ValidationScore: quality.Items.FirstOrDefault(item => item.KnowledgeId.Equals(candidate.KnowledgeId, StringComparison.OrdinalIgnoreCase))?.ValidationScore ?? 0,
                PlannedActions: actions.Select(action => action.ActionId).ToList(),
                Priority: blockerPriority(blockers),
                AutoFixable: actions.Any(action => action.AutoFixable),
                RequiresHumanReview: actions.Any(action => action.RequiresHumanReview));

            topItems.Add(item);
            plannedActions.AddRange(actions);
            if (item.AutoFixable)
            {
                autoFixableCount++;
            }

            if (item.RequiresHumanReview)
            {
                humanReviewCount++;
            }
        }

        var effort = EstimateEffort(blockerCounts, plannedActions.Count);
        var nextAction = plannedActions.Count == 0
            ? "Keine Aktion erforderlich. Hermes arbeitet weiter an Validierung."
            : plannedActions.OrderBy(action => PriorityRank(action.Priority)).First().SuggestedAction;

        var report = new KnowledgeTrustImprovementPlanReport(
            ReportVersion: "knowledge_trust_improvement_plan_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalBlockedItems: gate.BlockedItems,
            BlockerCounts: blockerCounts,
            PlannedActions: plannedActions
                .GroupBy(action => action.ActionId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(action => PriorityRank(action.Priority))
                .ThenBy(action => action.ActionId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EstimatedEffort: effort,
            AutoFixableCount: autoFixableCount,
            HumanReviewCount: humanReviewCount,
            TopPriorityItems: topItems
                .OrderByDescending(item => PriorityRank(item.Priority))
                .ThenByDescending(item => item.TrustScore)
                .ThenByDescending(item => item.QualityScore)
                .Take(15)
                .ToList(),
            NextRecommendedAction: nextAction,
            RequiresHumanReview: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            ReviewGatePath: new TrustedKnowledgeReviewGateService(_storagePaths).GatePath,
            ReviewQueuePath: gate.ReviewQueuePath,
            ImprovementQueuePath: new AutonomousImprovementQueueService(_storagePaths).QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        TryWriteReportCopies(report);
        return report;
    }

    public KnowledgeTrustImprovementPlanReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeTrustImprovementPlanReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void TryWriteReportCopies(KnowledgeTrustImprovementPlanReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);

        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "knowledge_trust_improvement_plan");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_trust_improvement_plan.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_trust_improvement_plan.md"), markdown);
        }
    }

    private static List<string> BuildBlockers(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem catalogItem,
        IReadOnlyDictionary<string, List<ContradictionRecord>> contradictionsByKnowledgeId,
        IReadOnlyDictionary<string, HumanReviewItem> reviewByKnowledgeId)
    {
        var blockers = new List<string>();
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

        if (qualityItem.EvidenceRefs.Count < 3)
        {
            blockers.Add("insufficient_evidence");
        }

        if (catalogItem.SourceIds.Count < 2)
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

        return blockers;
    }

    private static IReadOnlyList<KnowledgeTrustImprovementAction> MapPlannedActions(
        TrustedKnowledgeReviewCandidate candidate,
        IReadOnlyList<string> blockers)
    {
        var actions = new List<KnowledgeTrustImprovementAction>();

        foreach (var blocker in blockers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var action = blocker switch
            {
                "trust_score_too_low" => NewAction("gather_more_evidence", blocker, candidate, "Mehr Evidenz sammeln", "Sammle zusätzliche Evidenz für den Kandidaten.", true, false, "planned"),
                "quality_score_too_low" => NewAction("source_expansion", blocker, candidate, "Qualitätsprüfung und bessere Quellen suchen", "Suche bessere Quellen und erhöhe die Qualitätsbasis.", true, false, "planned"),
                "insufficient_sources" => NewAction("source_expansion", blocker, candidate, "Zusätzliche Quellen identifizieren", "Identifiziere neue unabhängige Quellen.", true, false, "planned"),
                "validation_score_too_low" => NewAction("schedule_revalidation", blocker, candidate, "Re-Validierung planen", "Plane eine erneute Validierung.", true, false, "planned"),
                "not_recently_validated" => NewAction("schedule_revalidation", blocker, candidate, "Re-Validation Schedule erzeugen", "Erzeuge einen Re-Validation-Plan.", true, false, "planned"),
                "active_contradiction" => NewAction("contradiction_analysis", blocker, candidate, "Widerspruchsanalyse erzeugen", "Analysiere aktive Widersprüche und leite Gegenprüfungen ab.", true, false, "planned"),
                "pending_human_review" => NewAction("review_preparation", blocker, candidate, "Review vorbereiten", "Bereite eine menschliche Prüfung vor, ohne freizugeben.", true, false, "planned"),
                "not_yet_trusted_or_robust" => NewAction("gather_more_evidence", blocker, candidate, "Weitere Robustheits- oder Evidenzarbeit planen", "Arbeite an Robustheit, Evidenz und Validierung weiter.", true, false, "planned"),
                _ => null
            };

            if (action is not null)
            {
                actions.Add(action);
            }
        }

        return actions;
    }

    private static KnowledgeTrustImprovementAction NewAction(
        string actionId,
        string blocker,
        TrustedKnowledgeReviewCandidate candidate,
        string title,
        string suggestedAction,
        bool autoFixable,
        bool requiresHumanReview,
        string status)
    {
        return new KnowledgeTrustImprovementAction(
            ActionId: $"{actionId}_{candidate.KnowledgeId}",
            Blocker: blocker,
            Title: title,
            Domain: candidate.Domain,
            Priority: blocker is "active_contradiction" or "pending_human_review" ? "high" : "medium",
            Reason: blocker,
            SuggestedAction: suggestedAction,
            AutoFixable: autoFixable,
            RequiresHumanReview: requiresHumanReview,
            Status: status);
    }

    private static string blockerPriority(IReadOnlyList<string> blockers)
    {
        return blockers.Any(blocker => blocker is "active_contradiction" or "pending_human_review")
            ? "high"
            : "medium";
    }

    private static int PriorityRank(string priority) => StringComparer.OrdinalIgnoreCase.Equals(priority, "high")
        ? 0
        : StringComparer.OrdinalIgnoreCase.Equals(priority, "medium")
            ? 1
            : 2;

    private static string EstimateEffort(IReadOnlyDictionary<string, int> blockerCounts, int actionCount)
    {
        var effortUnits = actionCount
            + blockerCounts.GetValueOrDefault("trust_score_too_low", 0)
            + blockerCounts.GetValueOrDefault("quality_score_too_low", 0)
            + blockerCounts.GetValueOrDefault("insufficient_sources", 0)
            + blockerCounts.GetValueOrDefault("validation_score_too_low", 0)
            + blockerCounts.GetValueOrDefault("not_recently_validated", 0)
            + blockerCounts.GetValueOrDefault("active_contradiction", 0) * 2;

        return effortUnits switch
        {
            <= 10 => "niedrig",
            <= 30 => "mittel",
            _ => "hoch"
        };
    }

    private static string BuildMarkdown(KnowledgeTrustImprovementPlanReport report)
    {
        var lines = new List<string>
        {
            "# Knowledge Trust Improvement Plan",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Total Blocked Items: {report.TotalBlockedItems}",
            $"- Planned Actions: {report.PlannedActions.Count}",
            $"- Estimated Effort: {report.EstimatedEffort}",
            $"- Auto Fixable: {report.AutoFixableCount}",
            $"- Human Review Count: {report.HumanReviewCount}",
            string.Empty,
            "## Blocker Counts",
        };

        lines.AddRange(report.BlockerCounts.Count == 0
            ? ["- keine"]
            : report.BlockerCounts.Select(entry => $"- {entry.Key}: {entry.Value}"));

        lines.Add(string.Empty);
        lines.Add("## Planned Actions");
        lines.AddRange(report.PlannedActions.Count == 0
            ? ["- keine"]
            : report.PlannedActions.Select(action => $"- {action.Title} [{action.Blocker}] -> {action.SuggestedAction}"));

        return string.Join(Environment.NewLine, lines);
    }
}
