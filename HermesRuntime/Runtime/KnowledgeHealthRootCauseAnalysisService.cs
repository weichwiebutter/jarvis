using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeHealthRootCauseDriver(
    int Rank,
    string Category,
    string Title,
    string Impact,
    string Summary,
    int Count,
    double EstimatedTrustImpact,
    IReadOnlyList<string> SupportingFacts);

public sealed record KnowledgeHealthRootCauseAnalysisReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string CurrentTrustLabel,
    string CurrentKnowledgeHealth,
    int OpenReviews,
    IReadOnlyDictionary<string, int> ReviewPriorityDistribution,
    double ReviewAverageAgeDays,
    IReadOnlyList<string> ReviewBlockers,
    IReadOnlyList<string> ReviewCategories,
    int OpenForwardPlans,
    int ForwardCompleted,
    int ForwardInvalidated,
    int ForwardNoSignal,
    int ForwardSignalSeen,
    int HypothesesWithoutOos,
    int CandidatesWithoutOos,
    int ImprovedCandidatesWithoutOos,
    int OpenValidationTasks,
    int MissingValidationArtifacts,
    int LowValidationScoreItems,
    int OpenContradictions,
    int ContradictoryEvidenceItems,
    int CompetingHypotheses,
    IReadOnlyList<KnowledgeHealthRootCauseDriver> Drivers,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeHealthRootCauseAnalysisService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public KnowledgeHealthRootCauseAnalysisService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_health_root_cause");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "knowledge_health_root_cause.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "knowledge_health_root_cause.md");

    public KnowledgeHealthRootCauseAnalysisReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var masterStatus = LoadJson(Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json"));
        var queue = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var reviewSummary = AnalyzeReviews(queue);
        var forwardSummary = AnalyzeForwardEvidence();
        var oosSummary = AnalyzeOos();
        var validationSummary = AnalyzeValidation();
        var contradictionSummary = AnalyzeContradictions();
        var currentTrust = ReadDouble(masterStatus, "average_trust_score", "averageTrustScore");
        var currentHealth = ReadString(masterStatus, "knowledge_health", "knowledgeHealth") ?? "unknown";
        var drivers = BuildDrivers(reviewSummary, forwardSummary, oosSummary, validationSummary, contradictionSummary);
        var report = new KnowledgeHealthRootCauseAnalysisReport(
            ReportVersion: "knowledge_health_root_cause_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            CurrentTrustLabel: $"Trust = {currentTrust * 100:0.#} %",
            CurrentKnowledgeHealth: currentHealth,
            OpenReviews: reviewSummary.OpenReviews,
            ReviewPriorityDistribution: reviewSummary.PriorityDistribution,
            ReviewAverageAgeDays: reviewSummary.AverageAgeDays,
            ReviewBlockers: reviewSummary.Blockers,
            ReviewCategories: reviewSummary.Categories,
            OpenForwardPlans: forwardSummary.OpenPlans,
            ForwardCompleted: forwardSummary.Completed,
            ForwardInvalidated: forwardSummary.Invalidated,
            ForwardNoSignal: forwardSummary.NoSignal,
            ForwardSignalSeen: forwardSummary.SignalSeen,
            HypothesesWithoutOos: oosSummary.HypothesesWithoutOos,
            CandidatesWithoutOos: oosSummary.CandidatesWithoutOos,
            ImprovedCandidatesWithoutOos: oosSummary.ImprovedCandidatesWithoutOos,
            OpenValidationTasks: validationSummary.OpenValidationTasks,
            MissingValidationArtifacts: validationSummary.MissingArtifacts,
            LowValidationScoreItems: validationSummary.LowValidationScoreItems,
            OpenContradictions: contradictionSummary.OpenContradictions,
            ContradictoryEvidenceItems: contradictionSummary.ContradictoryEvidenceItems,
            CompetingHypotheses: contradictionSummary.CompetingHypotheses,
            Drivers: drivers,
            OperatorSummary: BuildOperatorSummary(currentTrust, reviewSummary, forwardSummary, oosSummary, validationSummary, contradictionSummary, drivers),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        return report;
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        Directory.CreateDirectory(Root);
        return (Path.Combine(Root, "knowledge_health_root_cause.json"), Path.Combine(Root, "knowledge_health_root_cause.md"), Root);
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, KnowledgeHealthRootCauseAnalysisReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        Directory.CreateDirectory(root);
        File.WriteAllText(reportPath, json);
        File.WriteAllText(markdownPath, markdown);
    }

    private static string BuildMarkdown(KnowledgeHealthRootCauseAnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Health Root Cause Analysis");
        sb.AppendLine();
        sb.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- {report.CurrentTrustLabel}");
        sb.AppendLine($"- Knowledge Health: {report.CurrentKnowledgeHealth}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Top Drivers");
        foreach (var driver in report.Drivers.Take(3))
        {
            sb.AppendLine($"- {driver.Rank}. {driver.Title} ({driver.Impact})");
            sb.AppendLine($"  - {driver.Summary}");
            sb.AppendLine($"  - Trust Impact: {driver.EstimatedTrustImpact:0.##}");
        }
        return sb.ToString();
    }

    private static string BuildOperatorSummary(double currentTrust, ReviewSummary review, ForwardSummary forward, OosSummary oos, ValidationSummary validation, ContradictionSummary contradiction, IReadOnlyList<KnowledgeHealthRootCauseDriver> drivers)
    {
        var trustLabel = currentTrust * 100;
        var main = drivers.FirstOrDefault()?.Title ?? "offene Reviews";
        var extra = forward.OpenPlans > 0 ? "zu wenig bestätigte Forward-Beobachtungen" : "fehlende Evidenz in der Validierung";
        return $"Warum steht Vertrauen bei {trustLabel:0.#} %?\n\nHauptursache:\n{main}\n\nZusätzlich:\n{extra}\n\nEmpfohlener Hebel:\nForward-Evidenz sammeln und Top-Reviews schließen\n\nFrank nötig:\n{(review.OpenReviews > 0 ? "ja" : "nein")}";
    }

    private static IReadOnlyList<KnowledgeHealthRootCauseDriver> BuildDrivers(ReviewSummary review, ForwardSummary forward, OosSummary oos, ValidationSummary validation, ContradictionSummary contradiction)
    {
        var drivers = new List<KnowledgeHealthRootCauseDriver>
        {
            new(1, "review", "Offene Reviews", "hoch", $"{review.OpenReviews} offene Reviews, {review.PriorityHigh} hoch priorisiert", review.OpenReviews, review.OpenReviews == 0 ? 0 : 0.12, review.Blockers.Take(3).ToList()),
            new(2, "forward", "Fehlende Forward-Evidenz", forward.OpenPlans > 0 ? "mittel" : "niedrig", $"{forward.OpenPlans} offene Forward-Pläne, {forward.NoSignal} ohne Signal", forward.OpenPlans, forward.OpenPlans == 0 ? 0 : 0.08, ["no_signal", "waiting_for_window", "waiting_for_market_data"]),
            new(3, "oos", "Fehlende OOS-Bestätigung", "mittel", $"{oos.HypothesesWithoutOos} Hypothesen ohne OOS, {oos.ImprovedCandidatesWithoutOos} verbesserte Kandidaten ohne OOS", oos.HypothesesWithoutOos, oos.HypothesesWithoutOos == 0 ? 0 : 0.07, ["oos_validation_missing"]),
            new(4, "validation", "Validation", validation.LowValidationScoreItems > 0 || validation.OpenValidationTasks > 0 ? "niedrig" : "niedrig", $"{validation.OpenValidationTasks} offene Validation-Aufgaben, {validation.LowValidationScoreItems} niedrige Validation-Scores", validation.OpenValidationTasks + validation.LowValidationScoreItems, validation.OpenValidationTasks == 0 && validation.LowValidationScoreItems == 0 ? 0 : 0.05, ["validation_gap"]),
            new(5, "contradiction", "Widersprüche", contradiction.OpenContradictions > 0 ? "mittel" : "niedrig", $"{contradiction.OpenContradictions} offene Konflikte, {contradiction.ContradictoryEvidenceItems} widersprüchliche Evidenz", contradiction.OpenContradictions + contradiction.ContradictoryEvidenceItems, contradiction.OpenContradictions == 0 ? 0 : 0.06, ["contradiction_risk"]),
        };

        return drivers.OrderByDescending(driver => driver.EstimatedTrustImpact).ThenBy(driver => driver.Rank).ToList();
    }

    private static ReviewSummary AnalyzeReviews(HumanReviewQueue queue)
    {
        var pending = queue.Items.Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)).ToList();
        var blockers = pending
            .Select(item => item.Recommendation)
            .Select(NormalizeReason)
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToList();
        var categories = pending.Select(item => NormalizeCategory(item.Domain)).GroupBy(x => x).Select(g => $"{g.Key}:{g.Count()}").ToList();
        var dist = pending.GroupBy(item => item.Priority.ToString(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return new ReviewSummary(pending.Count, dist, pending.Count == 0 ? 0 : pending.Average(item => Math.Max(0, (DateTimeOffset.UtcNow - item.CreatedAtUtc).TotalDays)), blockers, categories, dist.GetValueOrDefault("high", 0));
    }

    private static ForwardSummary AnalyzeForwardEvidence()
    {
        var report = LoadJson(Path.Combine("/mnt/d/HermesData/reports/autonomous_forward_validation_planning/autonomous_forward_validation_planning.json"));
        var openPlans = CountWhere(report, "ready_to_observe", "planned", "waiting_for_market_data", "waiting_for_allowed_window", "still_open_waiting_for_signal", "still_open_observation_pending", "active_signal_seen");
        var completed = CountWhere(report, "completed");
        var invalidated = CountWhere(report, "completed_invalidated", "invalidated");
        var noSignal = CountWhere(report, "no_signal", "still_open_waiting_for_signal");
        var signalSeen = CountWhere(report, "signal_seen", "active_signal_seen");
        return new ForwardSummary(openPlans, completed, invalidated, noSignal, signalSeen);
    }

    private static OosSummary AnalyzeOos()
    {
        var planning = LoadJson(Path.Combine("/mnt/d/HermesData/reports/autonomous_oos_planning/autonomous_oos_planning.json"));
        var improved = CountWhere(planning, "completed_improved", "improved");
        var total = CountArray(planning, "plans", "oos_plans", "entries");
        return new OosSummary(Math.Max(0, total - improved), Math.Max(0, total - improved), Math.Max(0, improved));
    }

    private static ValidationSummary AnalyzeValidation()
    {
        var master = LoadJson(Path.Combine(_storageRootStatic, "reports", "master-status", "master_status.json"));
        var openValidationTasks = ReadInt(master, "validation_tasks_pending", "validationTasksPending");
        var missingArtifacts = ReadInt(master, "knowledge_items_needing_source_check", "knowledgeItemsNeedingSourceCheck");
        var lowValidationScoreItems = ReadInt(master, "research_validation_pending", "researchValidationPending");
        return new ValidationSummary(openValidationTasks, missingArtifacts, lowValidationScoreItems);
    }

    private static ContradictionSummary AnalyzeContradictions()
    {
        var master = LoadJson(Path.Combine(_storageRootStatic, "reports", "master-status", "master_status.json"));
        var openContradictions = ReadInt(master, "contradiction_count", "contradictionCount");
        return new ContradictionSummary(openContradictions, openContradictions, 0);
    }

    private static string NormalizeReason(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
    private static string NormalizeCategory(string domain) => string.IsNullOrWhiteSpace(domain) ? "unknown" : domain.Trim().ToLowerInvariant();

    private static JsonElement? LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
        return doc.RootElement.Clone();
    }

    private static int CountWhere(JsonElement? element, params string[] values)
    {
        if (element is null)
        {
            return 0;
        }

        var set = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (element.Value.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            return entries.EnumerateArray().Count(item => set.Contains(ReadString(item, "status", "result", "state") ?? string.Empty));
        }

        if (element.Value.TryGetProperty("plans", out var plans) && plans.ValueKind == JsonValueKind.Array)
        {
            return plans.EnumerateArray().Count(item => set.Contains(ReadString(item, "status", "result", "state") ?? string.Empty));
        }

        return 0;
    }

    private static int CountArray(JsonElement? element, params string[] names)
    {
        if (element is null) return 0;
        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.GetArrayLength();
            }
        }
        return 0;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static int ReadInt(JsonElement? element, params string[] names)
    {
        if (element is null) return 0;
        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private static double ReadDouble(JsonElement? element, params string[] names)
    {
        if (element is null) return 0;
        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.TryGetDouble(out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private static string? ReadString(JsonElement? element, params string[] names)
        => element is null ? null : ReadString(element.Value, names);

    private static readonly string _storageRootStatic = "/mnt/d/HermesData";

    private sealed record ReviewSummary(int OpenReviews, IReadOnlyDictionary<string, int> PriorityDistribution, double AverageAgeDays, IReadOnlyList<string> Blockers, IReadOnlyList<string> Categories, int PriorityHigh);
    private sealed record ForwardSummary(int OpenPlans, int Completed, int Invalidated, int NoSignal, int SignalSeen);
    private sealed record OosSummary(int HypothesesWithoutOos, int CandidatesWithoutOos, int ImprovedCandidatesWithoutOos);
    private sealed record ValidationSummary(int OpenValidationTasks, int MissingArtifacts, int LowValidationScoreItems);
    private sealed record ContradictionSummary(int OpenContradictions, int ContradictoryEvidenceItems, int CompetingHypotheses);
}
