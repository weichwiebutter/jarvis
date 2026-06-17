using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationBacklogPriorityArea(
    string AreaId,
    string AreaTitle,
    int ItemCount,
    string Priority,
    string Status,
    string NextAction,
    string Reason,
    bool AutomaticallyAllowed,
    bool FrankRequired,
    bool RequiresHumanReview,
    bool SafeToExecute,
    string WindowHint);

public sealed class ValidationBacklogPriorityEngineService
{
    private readonly StoragePaths _storagePaths;

    public ValidationBacklogPriorityEngineService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<ValidationBacklogPriorityArea> BuildAreas(
        ValidationBacklogAnalyzerReport analyzer,
        KnowledgeTrustImprovementPlanReport? trustPlan = null)
    {
        trustPlan ??= new KnowledgeTrustImprovementPlannerService(_storagePaths).Load()
            ?? new KnowledgeTrustImprovementPlannerService(_storagePaths).Run();

        var areas = new List<ValidationBacklogPriorityArea>
        {
            BuildArea(
                "gather_more_evidence",
                "Evidenz sammeln",
                GetCount(trustPlan.BlockerCounts, "trust_score_too_low") + GetCount(trustPlan.BlockerCounts, "quality_score_too_low"),
                "high",
                "Mehr Evidenz sammeln",
                "Hermes sammelt weitere Evidenz.",
                true,
                false,
                false,
                true,
                "Arbeitsfenster"),
            BuildArea(
                "source_expansion",
                "Quellen erweitern",
                GetCount(trustPlan.BlockerCounts, "insufficient_sources") + analyzer.DocumentationValidationPending,
                "high",
                "Quellen erweitern",
                "Hermes erweitert oder prüft zulässige Quellen.",
                true,
                false,
                false,
                true,
                "Arbeitsfenster"),
            BuildArea(
                "schedule_revalidation",
                "Re-Validierung",
                analyzer.ValidationPlansOpen + analyzer.ProcessValidationPending + analyzer.ResearchValidationPending + analyzer.SoftwareValidationPending,
                "high",
                "Re-Validierung planen",
                "Hermes plant und priorisiert Validierungsläufe.",
                true,
                false,
                false,
                true,
                "Nightly"),
            BuildArea(
                "contradiction_analysis",
                "Widersprüche prüfen",
                GetCount(trustPlan.BlockerCounts, "active_contradiction"),
                "high",
                "Widersprüche prüfen",
                "Hermes analysiert Widersprüche; Auflösung bleibt im Prüfzentrum.",
                true,
                true,
                true,
                true,
                "Arbeitsfenster"),
            BuildArea(
                "systempflege",
                "Systempflege",
                analyzer.CleanupCandidates > 0 ? 1 : 0,
                analyzer.CleanupCandidates > 50000 ? "medium" : "low",
                "Cleanup-Plan aktualisieren",
                "Hermes aktualisiert den Cleanup-Plan.",
                true,
                false,
                false,
                true,
                "bei Bedarf"),
        };

        return areas
            .OrderBy(area => PriorityRank(area.Priority))
            .ThenByDescending(area => area.ItemCount)
            .ThenBy(area => area.AreaId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ValidationBacklogPriorityArea BuildArea(
        string areaId,
        string areaTitle,
        int itemCount,
        string priority,
        string nextAction,
        string reason,
        bool automaticallyAllowed,
        bool frankRequired,
        bool requiresHumanReview,
        bool safeToExecute,
        string windowHint)
    {
        var status = itemCount <= 0
            ? "leer"
            : windowHint.Equals("Nightly", StringComparison.OrdinalIgnoreCase)
                ? "wartet auf Nightly"
                : windowHint.Equals("Arbeitsfenster", StringComparison.OrdinalIgnoreCase)
                    ? "bereit"
                    : "geplant";

        return new ValidationBacklogPriorityArea(
            AreaId: areaId,
            AreaTitle: areaTitle,
            ItemCount: Math.Max(0, itemCount),
            Priority: priority,
            Status: status,
            NextAction: nextAction,
            Reason: reason,
            AutomaticallyAllowed: automaticallyAllowed,
            FrankRequired: frankRequired,
            RequiresHumanReview: requiresHumanReview,
            SafeToExecute: safeToExecute,
            WindowHint: windowHint);
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string key)
    {
        return counts.TryGetValue(key, out var value) ? value : 0;
    }

    private static int PriorityRank(string priority) =>
        priority.ToLowerInvariant() switch
        {
            "high" => 0,
            "medium" => 1,
            _ => 2,
        };
}
