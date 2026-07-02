using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record KnowledgeReasoningSupport(
    string KnowledgeId,
    string Title,
    string Domain,
    string ValidationStatus,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    double MatchScore,
    IReadOnlyList<string> MatchedTerms,
    IReadOnlyList<string> SourceIds,
    string MatchMode,
    string Reason);

public sealed record KnowledgeReasoningConflict(
    string KnowledgeId,
    string Title,
    string Domain,
    string ValidationStatus,
    double MatchScore,
    string Reason);

public sealed record KnowledgeReasoningReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Topic,
    string Status,
    double Confidence,
    IReadOnlyList<KnowledgeReasoningSupport> MatchedKnowledge,
    IReadOnlyList<KnowledgeReasoningSupport> CandidateSupport,
    IReadOnlyList<string> ReasoningSteps,
    IReadOnlyList<string> SupportingSources,
    IReadOnlyList<KnowledgeReasoningConflict> ConflictingKnowledge,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> UsedKnowledgeIds,
    IReadOnlyList<string> OpenUncertainties,
    IReadOnlyList<string> Warnings,
    string KnowledgeCatalogPath,
    string KnowledgeQualityPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeReasoningService
{
    private static readonly IReadOnlyDictionary<string, string> OppositeTerms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["bullish"] = "bearish",
        ["bearish"] = "bullish",
        ["long"] = "short",
        ["short"] = "long",
        ["buy"] = "sell",
        ["sell"] = "buy",
        ["top"] = "bottom",
        ["bottom"] = "top",
        ["support"] = "resistance",
        ["resistance"] = "support"
    };

    private readonly StoragePaths _storagePaths;

    public KnowledgeReasoningService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_reasoning");

    public string ReportPath => Path.Combine(Root, "knowledge_reasoning_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_reasoning_report.md");

    public KnowledgeReasoningReport Run(string topic)
    {
        Directory.CreateDirectory(Root);

        var normalizedTopic = Normalize(topic);
        var topicTokens = Tokenize(normalizedTopic).ToList();
        var now = DateTimeOffset.UtcNow;
        var catalogPath = Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");
        var qualityPath = Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

        var catalog = new KnowledgeCatalog(_storagePaths).LoadItems();
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadReport();
        var qualityById = qualityReport?.Items
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>();
        if (catalog.Count == 0)
        {
            warnings.Add("knowledge_catalog_missing_or_empty");
        }

        var trustedItems = catalog
            .Where(item => item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .Select(item => BuildSupport(item, qualityById.GetValueOrDefault(item.Id), normalizedTopic, topicTokens, trustedOnly: true))
            .Where(item => item.MatchScore >= 0.25 || item.MatchedTerms.Count > 0)
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.TrustScore)
            .ThenByDescending(item => item.QualityScore)
            .ThenBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var candidateSupport = catalog
            .Where(item => !item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .Select(item => BuildSupport(item, qualityById.GetValueOrDefault(item.Id), normalizedTopic, topicTokens, trustedOnly: false))
            .Where(item => item.MatchScore >= 0.28)
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.TrustScore)
            .ThenByDescending(item => item.QualityScore)
            .ThenBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var supportingSources = trustedItems
            .SelectMany(item => item.SourceIds.Select(sourceId => $"{item.KnowledgeId}:{sourceId}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var conflicts = BuildConflicts(normalizedTopic, trustedItems, catalog, qualityById);
        var confidence = BuildConfidence(trustedItems, candidateSupport);
        var reasoningSteps = BuildReasoningSteps(topic, trustedItems, candidateSupport, conflicts, qualityReport);
        var recommendations = BuildRecommendations(topic, trustedItems, candidateSupport, conflicts, confidence);
        var openUncertainties = BuildOpenUncertainties(trustedItems, candidateSupport, conflicts, qualityReport);
        var usedKnowledgeIds = trustedItems.Select(item => item.KnowledgeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var status = trustedItems.Count > 0
            ? "trusted_reasoning_ready"
            : candidateSupport.Count > 0
                ? "candidate_support_only"
                : "no_trusted_knowledge_match";

        var report = new KnowledgeReasoningReport(
            ReportVersion: "knowledge_reasoning_v1",
            UpdatedAtUtc: now,
            Topic: topic,
            Status: status,
            Confidence: confidence,
            MatchedKnowledge: trustedItems,
            CandidateSupport: candidateSupport,
            ReasoningSteps: reasoningSteps,
            SupportingSources: supportingSources,
            ConflictingKnowledge: conflicts,
            Recommendations: recommendations,
            UsedKnowledgeIds: usedKnowledgeIds,
            OpenUncertainties: openUncertainties,
            Warnings: warnings,
            KnowledgeCatalogPath: catalogPath,
            KnowledgeQualityPath: qualityPath,
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

    public KnowledgeReasoningReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeReasoningReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static KnowledgeReasoningSupport BuildSupport(
        KnowledgeCatalogItem item,
        KnowledgeQualityItem? qualityItem,
        string normalizedTopic,
        IReadOnlyList<string> topicTokens,
        bool trustedOnly)
    {
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title;
        var trustScore = qualityItem?.TrustScore ?? item.Confidence;
        var qualityScore = qualityItem?.QualityScore ?? Math.Round(Math.Clamp(item.Confidence * 0.9, 0, 1), 4);
        var validationScore = qualityItem?.ValidationScore ?? Math.Round(Math.Clamp(item.Confidence * 0.82, 0, 1), 4);
        var itemText = Normalize($"{item.Id} {item.Title} {item.DescriptionShort} {string.Join(' ', item.Tags)} {string.Join(' ', item.RelatedItems)}");
        var itemTokens = Tokenize(itemText).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedTerms = topicTokens.Where(token => itemTokens.Contains(token)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var titleBonus = ContainsAllTokens(Normalize(item.Title), topicTokens) ? 1.0 : 0.0;
        var idBonus = ContainsAllTokens(Normalize(item.Id), topicTokens) ? 1.0 : 0.0;
        var exactPhraseBonus = itemText.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        var tokenCoverage = topicTokens.Count == 0 ? 0 : matchedTerms.Count / (double)topicTokens.Count;
        var score = Math.Clamp(
            tokenCoverage * 0.46
            + titleBonus * 0.18
            + idBonus * 0.1
            + exactPhraseBonus * 0.12
            + trustScore * 0.08
            + qualityScore * 0.06,
            0,
            1);

        if (trustedOnly && score > 0)
        {
            score = Math.Clamp(score + 0.05, 0, 1);
        }

        var reason = trustedOnly
            ? "trusted_knowledge_used_for_reasoning"
            : "candidate_support_only_not_used_automatically";

        if (matchedTerms.Count > 0)
        {
            reason = $"{reason}; matched_terms={string.Join(',', matchedTerms)}";
        }

        return new KnowledgeReasoningSupport(
            KnowledgeId: item.Id,
            Title: title,
            Domain: item.Domain,
            ValidationStatus: item.ValidationStatus,
            TrustScore: Math.Round(Math.Clamp(trustScore, 0, 1), 4),
            QualityScore: Math.Round(Math.Clamp(qualityScore, 0, 1), 4),
            ValidationScore: Math.Round(Math.Clamp(validationScore, 0, 1), 4),
            MatchScore: Math.Round(score, 4),
            MatchedTerms: matchedTerms,
            SourceIds: item.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MatchMode: trustedOnly ? "trusted_auto_use" : "candidate_support",
            Reason: reason);
    }

    private static IReadOnlyList<KnowledgeReasoningConflict> BuildConflicts(
        string normalizedTopic,
        IReadOnlyList<KnowledgeReasoningSupport> trustedItems,
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        IReadOnlyDictionary<string, KnowledgeQualityItem> qualityById)
    {
        var trustedIds = trustedItems
            .Select(item => item.KnowledgeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<KnowledgeReasoningConflict>();
        var topicTokens = Tokenize(normalizedTopic).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var oppositeTokens = topicTokens
            .Where(token => OppositeTerms.ContainsKey(token))
            .Select(token => OppositeTerms[token])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in catalog.Where(item => item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)))
        {
            if (trustedIds.Contains(item.Id))
            {
                continue;
            }

            var quality = qualityById.GetValueOrDefault(item.Id);
            var itemText = Normalize($"{item.Id} {item.Title} {item.DescriptionShort} {string.Join(' ', item.Tags)}");
            var itemTokens = Tokenize(itemText).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var overlap = topicTokens.Intersect(itemTokens, StringComparer.OrdinalIgnoreCase).Count();
            var oppositeOverlap = oppositeTokens.Count == 0
                ? 0
                : oppositeTokens.Intersect(itemTokens, StringComparer.OrdinalIgnoreCase).Count();

            if (oppositeOverlap == 0 && overlap == 0)
            {
                continue;
            }

            var matchScore = Math.Round(Math.Clamp(
                overlap * 0.15
                + oppositeOverlap * 0.22
                + (quality?.QualityScore ?? item.Confidence) * 0.08,
                0,
                1), 4);

            if (matchScore < 0.2)
            {
                continue;
            }

            var reason = oppositeOverlap > 0
                ? $"possible_opposite_knowledge_for:{string.Join(',', oppositeTokens)}"
                : "trusted_knowledge_with_related_terms";

            conflicts.Add(new KnowledgeReasoningConflict(
                KnowledgeId: item.Id,
                Title: item.Title,
                Domain: item.Domain,
                ValidationStatus: item.ValidationStatus,
                MatchScore: matchScore,
                Reason: reason));
        }

        return conflicts
            .DistinctBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.MatchScore)
            .ThenBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static double BuildConfidence(
        IReadOnlyList<KnowledgeReasoningSupport> trustedItems,
        IReadOnlyList<KnowledgeReasoningSupport> candidateSupport)
    {
        if (trustedItems.Count == 0)
        {
            if (candidateSupport.Count == 0)
            {
                return 0;
            }

            return Math.Round(Math.Clamp(candidateSupport.Max(item => item.MatchScore) * 0.45, 0, 1), 4);
        }

        var top = trustedItems.Max(item => item.MatchScore);
        var averageScores = trustedItems.Average(item => (item.TrustScore + item.QualityScore + item.ValidationScore) / 3.0);
        var sourceCoverage = Math.Min(0.12, trustedItems.Sum(item => item.SourceIds.Count) * 0.012);
        var multiMatchBonus = Math.Min(0.08, trustedItems.Count * 0.03);
        var confidence = top * 0.58 + averageScores * 0.27 + sourceCoverage + multiMatchBonus;
        if (candidateSupport.Count > 0)
        {
            confidence += Math.Min(0.04, candidateSupport.Count * 0.01);
        }

        return Math.Round(Math.Clamp(confidence, 0, 1), 4);
    }

    private static IReadOnlyList<string> BuildReasoningSteps(
        string topic,
        IReadOnlyList<KnowledgeReasoningSupport> trustedItems,
        IReadOnlyList<KnowledgeReasoningSupport> candidateSupport,
        IReadOnlyList<KnowledgeReasoningConflict> conflicts,
        KnowledgeQualityReport? qualityReport)
    {
        var steps = new List<string>
        {
            $"loaded knowledge_catalog.json and knowledge_quality.json for topic '{topic}'",
            $"filtered trusted knowledge items: {trustedItems.Count}",
            $"kept candidate_support items separately: {candidateSupport.Count}",
            $"identified conflicting_knowledge entries: {conflicts.Count}",
            qualityReport is null
                ? "knowledge_quality.json missing; trust/quality fallback values used from catalog"
                : $"knowledge_quality.json available with trusted_knowledge={qualityReport.TrustedKnowledge}, weak_knowledge={qualityReport.WeakKnowledge}"
        };

        if (trustedItems.Count > 0)
        {
            steps.Add($"used trusted knowledge ids: {string.Join(", ", trustedItems.Select(item => item.KnowledgeId))}");
        }
        else if (candidateSupport.Count > 0)
        {
            steps.Add("no trusted knowledge matched automatically; candidate_support listed for manual review");
        }
        else
        {
            steps.Add("no knowledge item matched the topic strongly enough for automatic use");
        }

        return steps;
    }

    private static IReadOnlyList<string> BuildRecommendations(
        string topic,
        IReadOnlyList<KnowledgeReasoningSupport> trustedItems,
        IReadOnlyList<KnowledgeReasoningSupport> candidateSupport,
        IReadOnlyList<KnowledgeReasoningConflict> conflicts,
        double confidence)
    {
        var recommendations = new List<string>();

        if (trustedItems.Count > 0)
        {
            recommendations.Add($"Use trusted knowledge IDs: {string.Join(", ", trustedItems.Select(item => item.KnowledgeId))}");
        }
        else
        {
            recommendations.Add($"No trusted automatic match found for topic '{topic}'.");
        }

        if (candidateSupport.Count > 0)
        {
            recommendations.Add($"Candidate support only: {string.Join(", ", candidateSupport.Select(item => item.KnowledgeId))}");
        }

        if (conflicts.Count > 0)
        {
            recommendations.Add($"Review conflicting knowledge: {string.Join(", ", conflicts.Select(item => item.KnowledgeId))}");
        }

        if (confidence < 0.6)
        {
            recommendations.Add("Confidence is limited; use trusted knowledge only and keep the conclusion conservative.");
        }

        recommendations.Add("Do not promote or write knowledge state from reasoning output; this layer is read-only.");
        return recommendations;
    }

    private static IReadOnlyList<string> BuildOpenUncertainties(
        IReadOnlyList<KnowledgeReasoningSupport> trustedItems,
        IReadOnlyList<KnowledgeReasoningSupport> candidateSupport,
        IReadOnlyList<KnowledgeReasoningConflict> conflicts,
        KnowledgeQualityReport? qualityReport)
    {
        var uncertainties = new List<string>();

        if (trustedItems.Count == 0)
        {
            uncertainties.Add("no_trusted_knowledge_match");
        }

        if (candidateSupport.Count > 0)
        {
            uncertainties.Add("candidate_support_available_not_used_automatically");
        }

        if (conflicts.Count > 0)
        {
            uncertainties.Add("conflicting_knowledge_present");
        }

        if (qualityReport is null)
        {
            uncertainties.Add("knowledge_quality_report_missing");
        }

        return uncertainties.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsAllTokens(string text, IReadOnlyList<string> tokens) =>
        tokens.Count > 0 && tokens.All(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private static IReadOnlyList<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void WriteReport(KnowledgeReasoningReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeReasoningReport report)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine("# Knowledge Reasoning Report");
        markdown.AppendLine();
        markdown.AppendLine($"- Topic: {report.Topic}");
        markdown.AppendLine($"- Status: {report.Status}");
        markdown.AppendLine($"- Confidence: {report.Confidence:0.###}");
        markdown.AppendLine($"- Updated At UTC: {report.UpdatedAtUtc:O}");
        markdown.AppendLine($"- Used Knowledge IDs: {(report.UsedKnowledgeIds.Count == 0 ? "-" : string.Join(", ", report.UsedKnowledgeIds))}");
        markdown.AppendLine();

        markdown.AppendLine("## Matched Knowledge");
        if (report.MatchedKnowledge.Count == 0)
        {
            markdown.AppendLine("- None");
        }
        else
        {
            foreach (var item in report.MatchedKnowledge)
            {
                markdown.AppendLine($"- {item.KnowledgeId} | {item.Title} | score={item.MatchScore:0.###} | trust={item.TrustScore:0.###} | quality={item.QualityScore:0.###}");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Candidate Support");
        if (report.CandidateSupport.Count == 0)
        {
            markdown.AppendLine("- None");
        }
        else
        {
            foreach (var item in report.CandidateSupport)
            {
                markdown.AppendLine($"- {item.KnowledgeId} | {item.Title} | score={item.MatchScore:0.###} | trust={item.TrustScore:0.###} | quality={item.QualityScore:0.###}");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Supporting Sources");
        if (report.SupportingSources.Count == 0)
        {
            markdown.AppendLine("- None");
        }
        else
        {
            foreach (var source in report.SupportingSources)
            {
                markdown.AppendLine($"- {source}");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Conflicting Knowledge");
        if (report.ConflictingKnowledge.Count == 0)
        {
            markdown.AppendLine("- None");
        }
        else
        {
            foreach (var item in report.ConflictingKnowledge)
            {
                markdown.AppendLine($"- {item.KnowledgeId} | {item.Title} | score={item.MatchScore:0.###} | {item.Reason}");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Reasoning Steps");
        foreach (var step in report.ReasoningSteps)
        {
            markdown.AppendLine($"- {step}");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Recommendations");
        foreach (var recommendation in report.Recommendations)
        {
            markdown.AppendLine($"- {recommendation}");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Open Uncertainties");
        if (report.OpenUncertainties.Count == 0)
        {
            markdown.AppendLine("- None");
        }
        else
        {
            foreach (var uncertainty in report.OpenUncertainties)
            {
                markdown.AppendLine($"- {uncertainty}");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Safety");
        markdown.AppendLine("- research_only=true");
        markdown.AppendLine("- no_trading_execution=true");
        markdown.AppendLine("- no_broker_action=true");
        markdown.AppendLine("- no_auto_trading=true");
        markdown.AppendLine("- human_review_required=true");

        return markdown.ToString();
    }
}
