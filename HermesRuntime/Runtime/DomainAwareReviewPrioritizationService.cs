using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record DomainAwareReviewPrioritizationEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string ClassifiedDomain,
    string Priority,
    double ReviewActionScore,
    double ConfidenceScore,
    string ConfidenceClass,
    string ConfidenceGainScore,
    string ConfidenceGainClass,
    string NextEvidenceStep,
    string StrongestBlockers,
    string ReprioritizationScore,
    string ReprioritizationClass,
    string FrankSignal,
    string OperatorNote);

public sealed record DomainAwareReviewPrioritizationGroup(
    string Domain,
    int Count,
    IReadOnlyList<DomainAwareReviewPrioritizationEntry> Reviews);

public sealed record DomainAwareReviewPrioritizationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalReviews,
    int TradingReviews,
    int KnowledgeReviews,
    int RuntimeReviews,
    int DocumentationReviews,
    int ProcessReviews,
    int UnknownReviews,
    IReadOnlyList<DomainAwareReviewPrioritizationGroup> TopTradingDecisions,
    IReadOnlyList<DomainAwareReviewPrioritizationGroup> TopKnowledgeReviews,
    IReadOnlyList<DomainAwareReviewPrioritizationGroup> TopRuntimeReviews,
    IReadOnlyList<DomainAwareReviewPrioritizationEntry> DocumentationLater,
    IReadOnlyList<DomainAwareReviewPrioritizationEntry> LowPriorityOther,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class DomainAwareReviewPrioritizationService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public DomainAwareReviewPrioritizationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "domain_aware_review_prioritization");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "domain_aware_review_prioritization.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "domain_aware_review_prioritization.md");

    public DomainAwareReviewPrioritizationReport Run()
    {
        Directory.CreateDirectory(Root);

        var assistant = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant", "review_decision_assistant.json"));
        var confidence = LoadJson(Path.Combine(_storagePaths.Root, "reports", "knowledge_confidence_engine", "knowledge_confidence_engine.json"));
        var entries = LoadReviewEntries(assistant)
            .Select(review => BuildEntry(review, confidence))
            .ToList();

        var grouped = entries
            .GroupBy(entry => entry.ClassifiedDomain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DomainAwareReviewPrioritizationGroup(
                Domain: group.Key,
                Count: group.Count(),
                Reviews: group.OrderByDescending(entry => ReprioritizationRank(entry))
                    .ThenByDescending(entry => entry.ReviewActionScore)
                    .ThenByDescending(entry => entry.ConfidenceScore)
                    .ThenBy(entry => entry.Title, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        var trading = grouped.Where(group => group.Domain == "trading").ToList();
        var knowledge = grouped.Where(group => group.Domain == "knowledge").ToList();
        var runtime = grouped.Where(group => group.Domain == "runtime_schema").Concat(grouped.Where(group => group.Domain == "runtime")).ToList();
        var documentation = entries.Where(entry => entry.ClassifiedDomain == "documentation").OrderByDescending(ReprioritizationRank).ToList();
        var other = entries.Where(entry => entry.ClassifiedDomain is "process" or "unknown").OrderByDescending(ReprioritizationRank).ToList();

        var report = new DomainAwareReviewPrioritizationReport(
            ReportVersion: "domain_aware_review_prioritization_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviews: entries.Count,
            TradingReviews: entries.Count(entry => entry.ClassifiedDomain == "trading"),
            KnowledgeReviews: entries.Count(entry => entry.ClassifiedDomain == "knowledge"),
            RuntimeReviews: entries.Count(entry => entry.ClassifiedDomain is "runtime_schema" or "runtime"),
            DocumentationReviews: entries.Count(entry => entry.ClassifiedDomain == "documentation"),
            ProcessReviews: entries.Count(entry => entry.ClassifiedDomain == "process"),
            UnknownReviews: entries.Count(entry => entry.ClassifiedDomain == "unknown"),
            TopTradingDecisions: BuildTopGroups(entries.Where(entry => entry.ClassifiedDomain == "trading"), "trading", 3),
            TopKnowledgeReviews: BuildTopGroups(entries.Where(entry => entry.ClassifiedDomain == "knowledge"), "knowledge", 3),
            TopRuntimeReviews: BuildTopGroups(entries.Where(entry => entry.ClassifiedDomain is "runtime_schema" or "runtime"), "runtime_schema", 3),
            DocumentationLater: documentation,
            LowPriorityOther: other,
            OperatorSummary: BuildOperatorSummary(entries),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteArtifacts(report);
        return report;
    }

    private DomainAwareReviewPrioritizationEntry BuildEntry(ReviewDecisionAssistantEntry review, JsonElement? confidence)
    {
        var domain = ClassifyDomain(review);
        var confidenceHit = MatchConfidence(review, confidence);
        var gain = EstimateGain(review, confidenceHit);
        var confidenceScore = Math.Round(confidenceHit.ConfidenceScore, 1);
        var gainClass = gain >= 15 ? "A" : gain >= 8 ? "B" : "C";
        var reprioritizationScore = domain == "trading"
            ? Math.Round((review.ReviewActionScore * 0.55) + (gain * 2.5) + PriorityBoost(review.Priority), 1)
            : domain == "knowledge"
                ? Math.Round((review.ReviewActionScore * 0.35) + (gain * 1.5) + PriorityBoost(review.Priority), 1)
                : domain == "runtime_schema" || domain == "documentation"
                    ? Math.Round((review.ReviewActionScore * 0.15) + PriorityBoost(review.Priority), 1)
                    : Math.Round((review.ReviewActionScore * 0.2) + PriorityBoost(review.Priority), 1);
        var reprioritizationClass = domain == "trading" && reprioritizationScore >= 70 ? "A"
            : reprioritizationScore >= 45 ? "B" : "C";
        var frankSignal = domain == "trading"
            ? (review.RecommendationKey == "approve" ? "rot" : review.RecommendationKey == "reject" ? "rot" : "gelb")
            : domain == "knowledge"
                ? "gelb"
                : "grün";
        var operatorNote = domain == "trading"
            ? "Trading-Entscheidung bleibt in der Trading-Liste."
            : domain == "runtime_schema" || domain == "documentation"
                ? "Separat eingeordnet, verdrängt keine Trading-Entscheidung."
                : "Sekundär priorisiert.";

        return new DomainAwareReviewPrioritizationEntry(
            ReviewId: review.ReviewId,
            KnowledgeItemId: review.KnowledgeItemId,
            Title: review.Title,
            Domain: review.Domain,
            ClassifiedDomain: domain,
            Priority: review.Priority,
            ReviewActionScore: review.ReviewActionScore,
            ConfidenceScore: confidenceScore,
            ConfidenceClass: confidenceHit.ConfidenceClass,
            ConfidenceGainScore: $"+{gain:0.#}%",
            ConfidenceGainClass: gainClass,
            NextEvidenceStep: confidenceHit.NextEvidenceStep,
            StrongestBlockers: string.Join(" · ", confidenceHit.StrongestBlockers),
            ReprioritizationScore: reprioritizationScore.ToString("0.#"),
            ReprioritizationClass: reprioritizationClass,
            FrankSignal: frankSignal,
            OperatorNote: operatorNote);
    }

    private static string ClassifyDomain(ReviewDecisionAssistantEntry review)
    {
        var text = $"{review.Title} {review.Domain} {review.KnowledgeItemId} {review.RecommendationReason} {review.WhyNow} {string.Join(' ', review.MissingEvidence)}".ToLowerInvariant();

        if (review.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return "trading";
        }

        if (review.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        {
            return review.Title.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && (text.Contains("runtime") || text.Contains("schema") || text.Contains("event"))
                ? "runtime_schema"
                : "documentation";
        }

        if (review.Domain.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            return "process";
        }

        if (text.Contains("runtime") || text.Contains("schema") || text.Contains("event"))
        {
            return "runtime_schema";
        }

        if (text.Contains("process") || text.Contains("scheduler") || text.Contains("storage") || text.Contains("cleanup"))
        {
            return "process";
        }

        if (text.Contains("trading") || text.Contains("strategy") || text.Contains("setup") || text.Contains("signal") || text.Contains("asset") || text.Contains("timeframe") || text.Contains("breakout") || text.Contains("pullback") || text.Contains("engulfing"))
        {
            return "trading";
        }

        if (text.Contains("hypothesis") || text.Contains("knowledge") || text.Contains("evidence") || text.Contains("confidence"))
        {
            return "knowledge";
        }

        return "unknown";
    }

    private static ConfidenceMatch MatchConfidence(ReviewDecisionAssistantEntry review, JsonElement? confidence)
    {
        var hypotheses = ReadArray(confidence, "hypotheses")
            .Select(entry => new ConfidenceMatch(
                HypothesisId: ReadString(entry, "hypothesis_id", "HypothesisId") ?? "unknown",
                Title: ReadString(entry, "title", "Title") ?? "unknown",
                ConfidenceScore: ReadDouble(entry, "confidence_score", "ConfidenceScore"),
                ConfidenceClass: ReadString(entry, "confidence_class", "ConfidenceClass") ?? "very_low",
                StrongestBlockers: ReadStringList(entry, "strongest_blockers", "StrongestBlockers"),
                NextEvidenceStep: ReadString(entry, "next_evidence_step", "NextEvidenceStep") ?? "nächste Evidenz prüfen"))
            .ToList();

        var normalized = $"{review.Title} {review.Domain} {review.KnowledgeItemId}".ToLowerInvariant();
        var match = hypotheses
            .OrderByDescending(candidate => ScoreMatch(normalized, candidate))
            .ThenByDescending(candidate => candidate.ConfidenceScore)
            .FirstOrDefault();

        return match ?? new ConfidenceMatch("unmatched", review.Title, 0, "very_low", ["keine Confidence-Hypothese zugeordnet"], "nächste Evidenz prüfen");
    }

    private static double EstimateGain(ReviewDecisionAssistantEntry review, ConfidenceMatch confidence)
    {
        var gain = 6.0;
        var nextStep = confidence.NextEvidenceStep.ToLowerInvariant();
        if (nextStep.Contains("forward")) gain += 16;
        if (nextStep.Contains("oos")) gain += 12;
        if (nextStep.Contains("validation")) gain += 8;
        if (review.RecommendationKey == "more_evidence") gain += 4;
        if (review.RecommendationKey == "approve") gain -= 3;
        if (review.RecommendationKey == "reject") gain -= 2;
        return Math.Max(0, Math.Min(30, gain));
    }

    private static IReadOnlyList<DomainAwareReviewPrioritizationGroup> BuildTopGroups(IEnumerable<DomainAwareReviewPrioritizationEntry> entries, string domain, int take)
    {
        var list = entries
            .OrderByDescending(entry => ReprioritizationRank(entry))
            .ThenByDescending(entry => entry.ReviewActionScore)
            .ThenByDescending(entry => entry.ConfidenceScore)
            .Take(take)
            .ToList();

        return list.Count == 0
            ? []
            : [new DomainAwareReviewPrioritizationGroup(domain, list.Count, list)];
    }

    private static string BuildOperatorSummary(IReadOnlyList<DomainAwareReviewPrioritizationEntry> entries)
    {
        var trading = entries.Count(entry => entry.ClassifiedDomain == "trading");
        var runtime = entries.Count(entry => entry.ClassifiedDomain is "runtime_schema" or "documentation");
        var message = trading > 0
            ? $"Frank hat {trading} relevante Trading-Entscheidungen."
            : "Frank hat aktuell keine Trading-Entscheidungen.";

        return runtime > 0
            ? $"{message}\n{runtime} Runtime-/Dokumentationsreview(s) wurden separat eingeordnet.\nKeine Dokumentation verdrängt Trading-Entscheidungen.\nFrank muss aktuell nichts freigeben."
            : $"{message}\nKeine Dokumentation verdrängt Trading-Entscheidungen.\nFrank muss aktuell nichts freigeben.";
    }

    private void WriteArtifacts(DomainAwareReviewPrioritizationReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;
        Directory.CreateDirectory(root);
        File.WriteAllText(reportPath, json);
        File.WriteAllText(markdownPath, markdown);
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        return (Path.Combine(Root, "domain_aware_review_prioritization.json"), Path.Combine(Root, "domain_aware_review_prioritization.md"), Root);
    }

    private static string BuildMarkdown(DomainAwareReviewPrioritizationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Domain-Aware Review Prioritization");
        builder.AppendLine();
        builder.AppendLine(report.OperatorSummary);
        builder.AppendLine();
        builder.AppendLine($"- Trading: {report.TradingReviews}");
        builder.AppendLine($"- Knowledge: {report.KnowledgeReviews}");
        builder.AppendLine($"- Runtime: {report.RuntimeReviews}");
        builder.AppendLine($"- Documentation: {report.DocumentationReviews}");
        builder.AppendLine($"- Process: {report.ProcessReviews}");
        builder.AppendLine($"- Unknown: {report.UnknownReviews}");
        return builder.ToString();
    }

    private static JsonElement? LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
        return doc.RootElement.Clone();
    }

    private static IReadOnlyList<ReviewDecisionAssistantEntry> LoadReviewEntries(JsonElement? reviewAssistant)
    {
        var items = new List<ReviewDecisionAssistantEntry>();
        foreach (var entry in ReadArray(reviewAssistant, "entries"))
        {
            items.Add(new ReviewDecisionAssistantEntry(
                ReviewId: ReadString(entry, "review_id", "ReviewId") ?? "unknown",
                KnowledgeItemId: ReadString(entry, "knowledge_item_id", "KnowledgeItemId") ?? "unknown",
                Title: ReadString(entry, "title", "Title") ?? "unknown",
                Domain: ReadString(entry, "domain", "Domain") ?? "unknown",
                Priority: ReadString(entry, "priority", "Priority") ?? "niedrig",
                ReviewActionScore: ReadDouble(entry, "review_action_score", "ReviewActionScore"),
                ReviewActionBand: ReadString(entry, "review_action_band", "ReviewActionBand") ?? "C",
                TrustBefore: ReadDouble(entry, "trust_before", "TrustBefore"),
                EvidenceQuality: ReadDouble(entry, "evidence_quality", "EvidenceQuality"),
                ValidationScore: ReadDouble(entry, "validation_score", "ValidationScore"),
                RiskScore: ReadDouble(entry, "risk_score", "RiskScore"),
                TradingRisk: ReadString(entry, "trading_risk", "TradingRisk") ?? "mittel",
                RecommendationKey: ReadString(entry, "recommendation_key", "RecommendationKey") ?? "more_evidence",
                RecommendationLabel: ReadString(entry, "recommendation_label", "RecommendationLabel") ?? "Mehr Evidenz empfohlen",
                RecommendationClass: ReadString(entry, "recommendation_class", "RecommendationClass") ?? "Unsicher",
                RecommendationReason: ReadString(entry, "recommendation_reason", "RecommendationReason") ?? "",
                WhyNow: ReadString(entry, "why_now", "WhyNow") ?? "",
                NextStep: ReadString(entry, "next_step", "NextStep") ?? "",
                MissingEvidence: ReadStringList(entry, "missing_evidence", "MissingEvidence"),
                FrankAction: ReadString(entry, "frank_action", "FrankAction") ?? "",
                RequiresHumanReview: ReadBool(entry, "requires_human_review", "RequiresHumanReview")));
        }

        return items;
    }

    private static int ScoreMatch(string normalizedReview, ConfidenceMatch candidate)
    {
        var score = 0;
        if (normalizedReview.Contains(candidate.Title.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 5;
        if (normalizedReview.Contains(candidate.HypothesisId.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 2;
        return score;
    }

    private static double ReprioritizationRank(DomainAwareReviewPrioritizationEntry entry)
        => entry.ReprioritizationClass switch
        {
            "A" => 3,
            "B" => 2,
            _ => 1
        };

    private static double PriorityBoost(string priority)
        => priority.Equals("hoch", StringComparison.OrdinalIgnoreCase) ? 10 : priority.Equals("mittel", StringComparison.OrdinalIgnoreCase) ? 6 : 2;

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement? element, string name)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!TryGetProperty(element.Value, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray().ToList();
    }

    private static string? ReadString(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (TryGetProperty(element.Value, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static double ReadDouble(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (TryGetProperty(element.Value, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                {
                    return number;
                }
                if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in names)
        {
            if (TryGetProperty(element.Value, name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            }
        }

        return [];
    }

    private static bool ReadBool(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (TryGetProperty(element.Value, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    return value.GetBoolean();
                }
                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private sealed record ConfidenceMatch(
        string HypothesisId,
        string Title,
        double ConfidenceScore,
        string ConfidenceClass,
        IReadOnlyList<string> StrongestBlockers,
        string NextEvidenceStep);

    private static string ClassifyConfidence(string nextStep)
        => nextStep.Contains("forward", StringComparison.OrdinalIgnoreCase) ? "forward" : "analysis";
}
