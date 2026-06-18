using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record ConfidenceDrivenReviewPriorityEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Priority,
    double CurrentReviewScore,
    string ReviewActionBand,
    string HypothesisId,
    string HypothesisTitle,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    double ConfidenceScore,
    string ConfidenceClass,
    IReadOnlyList<string> StrongestBlockers,
    string NextEvidenceStep,
    double CurrentConfidenceScore,
    double ExpectedConfidenceScore,
    double ConfidenceGainScore,
    string ConfidenceGainClass,
    IReadOnlyList<string> StrongestPositiveDrivers,
    string ReprioritizationClass);

public sealed record ConfidenceDrivenReviewPrioritizationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ReviewsEvaluated,
    int HypothesesMatched,
    IReadOnlyList<ConfidenceDrivenReviewPriorityEntry> Entries,
    ConfidenceDrivenReviewPriorityEntry? TopLever,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ConfidenceDrivenReviewPrioritizationService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ConfidenceDrivenReviewPrioritizationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "confidence_review_prioritization");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "confidence_review_prioritization.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "confidence_review_prioritization.md");

    public ConfidenceDrivenReviewPrioritizationReport Run()
    {
        Directory.CreateDirectory(Root);

        var confidence = LoadJson(Path.Combine(_storagePaths.Root, "reports", "knowledge_confidence_engine", "knowledge_confidence_engine.json"));
        var reviewAssistant = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant", "review_decision_assistant.json"));
        var reviewAudit = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_prioritization_audit", "review_prioritization_audit.json"));

        var hypotheses = LoadConfidenceHypotheses(confidence);
        var reviews = LoadReviewEntries(reviewAssistant);
        var entries = reviews
            .Select(review => BuildEntry(review, hypotheses))
            .OrderByDescending(entry => entry.ConfidenceGainScore)
            .ThenByDescending(entry => entry.CurrentReviewScore)
            .ThenByDescending(entry => PriorityRank(entry.Priority))
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

        var top = entries.FirstOrDefault();
        var report = new ConfidenceDrivenReviewPrioritizationReport(
            ReportVersion: "confidence_review_prioritization_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReviewsEvaluated: entries.Count,
            HypothesesMatched: entries.Count(entry => !string.Equals(entry.HypothesisId, "unmatched", StringComparison.OrdinalIgnoreCase)),
            Entries: entries,
            TopLever: top,
            OperatorSummary: BuildOperatorSummary(top),
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

    private ConfidenceDrivenReviewPriorityEntry BuildEntry(ReviewDecisionAssistantEntry review, IReadOnlyList<ConfidenceHypothesis> hypotheses)
    {
        var hypothesis = MatchHypothesis(review, hypotheses);
        var confidenceGain = EstimateConfidenceGain(review, hypothesis);
        var delta = Math.Max(0, confidenceGain.Expected - confidenceGain.Current);
        var gainClass = delta >= 15 ? "A" : delta >= 8 ? "B" : "C";
        var reprioritizationClass = delta >= 15 ? "A" : delta >= 8 ? "B" : "C";
        var baseScore = (review.ReviewActionScore * 0.35) + (delta * 2.2) + PriorityWeight(review.Priority) * 10 + (review.TradingRisk == "hoch" ? 10 : review.TradingRisk == "mittel" ? 5 : 2) + AgeWeight(review);
        var currentScore = Math.Round(Math.Clamp(baseScore, 0, 100), 1);

        return new ConfidenceDrivenReviewPriorityEntry(
            ReviewId: review.ReviewId,
            KnowledgeItemId: review.KnowledgeItemId,
            Title: review.Title,
            Domain: review.Domain,
            Priority: review.Priority,
            CurrentReviewScore: currentScore,
            ReviewActionBand: review.ReviewActionBand,
            HypothesisId: hypothesis.HypothesisId,
            HypothesisTitle: hypothesis.Title,
            Asset: hypothesis.Asset,
            Timeframe: hypothesis.Timeframe,
            StrategyPattern: hypothesis.StrategyPattern,
            ConfidenceScore: hypothesis.ConfidenceScore,
            ConfidenceClass: hypothesis.ConfidenceClass,
            StrongestBlockers: hypothesis.StrongestBlockers,
            NextEvidenceStep: hypothesis.NextEvidenceStep,
            CurrentConfidenceScore: confidenceGain.Current,
            ExpectedConfidenceScore: confidenceGain.Expected,
            ConfidenceGainScore: delta,
            ConfidenceGainClass: gainClass,
            StrongestPositiveDrivers: hypothesis.StrongestPositiveDrivers,
            ReprioritizationClass: reprioritizationClass);
    }

    private static ConfidenceHypothesis MatchHypothesis(ReviewDecisionAssistantEntry review, IReadOnlyList<ConfidenceHypothesis> hypotheses)
    {
        var normalized = $"{review.Title} {review.Domain} {review.KnowledgeItemId}".ToLowerInvariant();
        var match = hypotheses
            .OrderByDescending(hypothesis => MatchScore(normalized, hypothesis))
            .ThenByDescending(hypothesis => hypothesis.ConfidenceScore)
            .FirstOrDefault();

        return match ?? new ConfidenceHypothesis("unmatched", review.Title, review.Domain, "unknown", "unknown", review.Title, 0, "very_low", [], ["keine Confidence-Hypothese zugeordnet"], "nächste Evidenz prüfen", false, false);
    }

    private static double MatchScore(string normalizedReview, ConfidenceHypothesis hypothesis)
    {
        var score = 0.0;
        if (normalizedReview.Contains(hypothesis.Title.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 3;
        if (normalizedReview.Contains(hypothesis.StrategyPattern.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 2;
        if (!string.IsNullOrWhiteSpace(hypothesis.Asset) && normalizedReview.Contains(hypothesis.Asset.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 2;
        if (!string.IsNullOrWhiteSpace(hypothesis.Timeframe) && normalizedReview.Contains(hypothesis.Timeframe.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) score += 1;
        return score;
    }

    private static ConfidenceGainEstimate EstimateConfidenceGain(ReviewDecisionAssistantEntry review, ConfidenceHypothesis hypothesis)
    {
        var current = Math.Round(hypothesis.ConfidenceScore, 1);
        var boost = 0.0;
        var step = hypothesis.NextEvidenceStep.ToLowerInvariant();
        if (step.Contains("forward")) boost += 16;
        if (step.Contains("oos")) boost += 12;
        if (step.Contains("validation")) boost += 8;
        if (review.RecommendationKey == "more_evidence") boost += 4;
        if (review.MissingEvidence.Any(item => item.Contains("Contradiction", StringComparison.OrdinalIgnoreCase))) boost += 2;
        var expected = Math.Round(Math.Min(100, current + boost), 1);
        return new ConfidenceGainEstimate(current, expected);
    }

    private static string BuildOperatorSummary(ConfidenceDrivenReviewPriorityEntry? top)
    {
        if (top is null)
        {
            return "Keine Reviews mit Confidence-Gewinn bewertbar. Frank muss nichts freigeben.";
        }

        return $"Die {top.HypothesisTitle} besitzt aktuell {top.CurrentConfidenceScore:0.#}% Confidence.\n\nEin erfolgreicher {top.NextEvidenceStep} würde die Confidence voraussichtlich auf {top.ExpectedConfidenceScore:0.#}% erhöhen.\n\nDies ist aktuell der größte Wissenshebel.\n\nFrank muss aktuell nichts freigeben.";
    }

    private static IReadOnlyList<ConfidenceHypothesis> LoadConfidenceHypotheses(JsonElement? confidenceReport)
    {
        var list = new List<ConfidenceHypothesis>();
        var entries = ReadArray(confidenceReport, "hypotheses");
        foreach (var entry in entries)
        {
            list.Add(new ConfidenceHypothesis(
                HypothesisId: ReadString(entry, "hypothesis_id", "HypothesisId") ?? "unknown",
                Title: ReadString(entry, "title", "Title") ?? "unknown",
                Domain: ReadString(entry, "domain", "Domain") ?? "unknown",
                Asset: ReadString(entry, "asset", "Asset") ?? "unknown",
                Timeframe: ReadString(entry, "timeframe", "Timeframe") ?? "unknown",
                StrategyPattern: ReadString(entry, "strategy_pattern", "StrategyPattern") ?? ReadString(entry, "title", "Title") ?? "unknown",
                ConfidenceScore: ReadDouble(entry, "confidence_score", "ConfidenceScore"),
                ConfidenceClass: ReadString(entry, "confidence_class", "ConfidenceClass") ?? "unknown",
                StrongestPositiveDrivers: ReadStringList(entry, "strongest_positive_drivers", "StrongestPositiveDrivers"),
                StrongestBlockers: ReadStringList(entry, "strongest_blockers", "StrongestBlockers"),
                NextEvidenceStep: ReadString(entry, "next_evidence_step", "NextEvidenceStep") ?? "unknown",
                FrankRequired: ReadBool(entry, "frank_required", "FrankRequired"),
                MayPromote: ReadBool(entry, "may_promote", "MayPromote")));
        }

        return list;
    }

    private static IReadOnlyList<ReviewDecisionAssistantEntry> LoadReviewEntries(JsonElement? reviewAssistant)
    {
        var list = new List<ReviewDecisionAssistantEntry>();
        foreach (var entry in ReadArray(reviewAssistant, "entries"))
        {
            list.Add(new ReviewDecisionAssistantEntry(
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

        return list;
    }

    private static double PriorityWeight(string priority)
        => priority.Equals("hoch", StringComparison.OrdinalIgnoreCase) ? 1 : priority.Equals("mittel", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.4;

    private static int PriorityRank(string priority)
        => priority.Equals("hoch", StringComparison.OrdinalIgnoreCase) ? 3 : priority.Equals("mittel", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static double AgeWeight(ReviewDecisionAssistantEntry review)
        => Math.Clamp((DateTimeOffset.UtcNow - DateTimeOffset.UtcNow.AddDays(-1)).TotalHours / 24.0, 0, 1);

    private static JsonElement? LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
        return doc.RootElement.Clone();
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement? element, string name)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return element.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array
            ? prop.EnumerateArray().ToList()
            : [];
    }

    private static string? ReadString(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
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
            if (element.Value.TryGetProperty(name, out var prop) && prop.TryGetDouble(out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static bool ReadBool(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return prop.GetBoolean();
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            }
        }

        return [];
    }

    private void WriteArtifacts(ConfidenceDrivenReviewPrioritizationReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
    }

    private static string BuildMarkdown(ConfidenceDrivenReviewPrioritizationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Confidence Driven Review Prioritization");
        sb.AppendLine();
        sb.AppendLine($"- Evaluated Reviews: {report.ReviewsEvaluated}");
        sb.AppendLine($"- Hypotheses matched: {report.HypothesesMatched}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        foreach (var item in report.Entries.Take(10))
        {
            sb.AppendLine($"- {item.Title} | {item.ReprioritizationClass} | gain={item.ConfidenceGainScore:0.#}");
            sb.AppendLine($"  - Hypothesis: {item.HypothesisTitle} ({item.ConfidenceScore:0.#}%)");
            sb.AppendLine($"  - Expected: {item.ExpectedConfidenceScore:0.#}%");
            sb.AppendLine($"  - Next step: {item.NextEvidenceStep}");
        }
        return sb.ToString();
    }

    private sealed record ConfidenceHypothesis(
        string HypothesisId,
        string Title,
        string Domain,
        string Asset,
        string Timeframe,
        string StrategyPattern,
        double ConfidenceScore,
        string ConfidenceClass,
        IReadOnlyList<string> StrongestPositiveDrivers,
        IReadOnlyList<string> StrongestBlockers,
        string NextEvidenceStep,
        bool FrankRequired,
        bool MayPromote);

    private sealed record ConfidenceGainEstimate(double Current, double Expected);
}
