using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TrustedKnowledgeImpactEntry(
    string Command,
    string AnalysisLabel,
    bool TrustedKnowledgeUsed,
    bool TopicInferred,
    string? Topic,
    double? Confidence,
    string SupportedRecommendation,
    IReadOnlyList<string> TrustedKnowledgeIds,
    IReadOnlyList<string> CandidateSupportNotUsed,
    IReadOnlyList<string> ReducedUncertainties,
    IReadOnlyList<string> MissingTrustedKnowledge,
    IReadOnlyList<string> Notes);

public sealed record TrustedKnowledgeImpactReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TrustedKnowledgeImpactEntry> Entries,
    IReadOnlyList<string> CommandsWithTrustImpact,
    IReadOnlyList<string> CommandsWithoutTopic,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> TrustedKnowledgeIds,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool ReadOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class TrustedKnowledgeImpactService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TrustedKnowledgeImpactService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trusted_knowledge_impact");

    public string ReportPath => Path.Combine(Root, "trusted_knowledge_impact_report.json");

    public string MarkdownPath => Path.Combine(Root, "trusted_knowledge_impact_report.md");

    public TrustedKnowledgeImpactReport Run()
    {
        Directory.CreateDirectory(Root);

        var entries = new List<TrustedKnowledgeImpactEntry>
        {
            BuildSignalsEntry(),
            BuildTradingResearchSynthesizerEntry(),
            BuildStrategyValidationReadinessEntry(),
            BuildReviewDecisionAssistantEntry()
        };

        var report = new TrustedKnowledgeImpactReport(
            ReportVersion: "trusted_knowledge_impact_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Entries: entries,
            CommandsWithTrustImpact: entries.Where(entry => entry.TrustedKnowledgeUsed).Select(entry => entry.Command).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            CommandsWithoutTopic: entries.Where(entry => !entry.TopicInferred).Select(entry => entry.Command).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            Topics: entries.Where(entry => entry.TopicInferred && !string.IsNullOrWhiteSpace(entry.Topic)).Select(entry => entry.Topic!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            TrustedKnowledgeIds: entries.SelectMany(entry => entry.TrustedKnowledgeIds).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: BuildWarnings(entries),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ReadOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        return report;
    }

    public TrustedKnowledgeImpactReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrustedKnowledgeImpactReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private TrustedKnowledgeImpactEntry BuildSignalsEntry()
    {
        var files = FindLatestExportFile("signals");
        if (files is null)
        {
            return BuildEntry(
                "signals",
                "signal explanation",
                false,
                false,
                null,
                null,
                "signal explanation supported by trusted knowledge",
                [],
                ["candidate_support_available_not_used_automatically"],
                ["topic not inferred from current signal export"],
                ["signal setup name", "pattern name", "asset", "reason codes"],
                ["no signal export available"]);
        }

        var topic = InferTopicFromSignals(files);
        if (topic is null)
        {
            return BuildEntry(
                "signals",
                "signal explanation",
                false,
                false,
                null,
                null,
                "signal explanation not supported by trusted knowledge",
                [],
                ["candidate_support_available_not_used_automatically"],
                ["topic could not be inferred from current signal export"],
                ["signal setup name", "pattern name", "asset", "reason codes"],
                ["topic not inferred from current signal export"]);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            "signals",
            "signal explanation",
            true,
            true,
            topic,
            reasoning.Confidence,
            "signal explanation supported by trusted knowledge",
            reasoning.UsedKnowledgeIds.ToList(),
            reasoning.CandidateSupport.Select(item => item.KnowledgeId).ToList(),
            reasoning.OpenUncertainties.ToList(),
            topic is null ? ["signal setup name", "pattern name", "asset", "reason codes"] : [],
            reasoning.Recommendations.ToList());
    }

    private TrustedKnowledgeImpactEntry BuildTradingResearchSynthesizerEntry()
    {
        var report = new TradingResearchSynthesizerService(_storagePaths, _runtimeRoot).Load()
            ?? new TradingResearchSynthesizerService(_storagePaths, _runtimeRoot).Run();
        var comparison = report.Comparisons.FirstOrDefault();
        var hypothesis = report.Hypotheses.FirstOrDefault();
        var topic = ExtractTopic(
            comparison?.PatternName,
            hypothesis?.Title,
            hypothesis?.Hypothesis,
            string.Join(" ", report.InternalSources.Take(8)),
            string.Join(" ", report.ExternalSources.Take(8)));

        if (topic is null)
        {
            return BuildEntry(
                "trading-research-synthesizer",
                "trading research synthesizer",
                false,
                false,
                null,
                null,
                "research synthesis not supported by trusted knowledge",
                [],
                ["candidate_support_available_not_used_automatically"],
                ["topic could not be inferred from the latest synthesizer report"],
                ["pattern name", "hypothesis title", "internal sources", "external sources"],
                ["topic not inferred from synthesizer report"]);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            "trading-research-synthesizer",
            "trading research synthesizer",
            true,
            true,
            topic,
            reasoning.Confidence,
            "research synthesis supported by trusted knowledge",
            reasoning.UsedKnowledgeIds.ToList(),
            reasoning.CandidateSupport.Select(item => item.KnowledgeId).ToList(),
            reasoning.OpenUncertainties.ToList(),
            [],
            reasoning.Recommendations.ToList());
    }

    private TrustedKnowledgeImpactEntry BuildStrategyValidationReadinessEntry()
    {
        var service = new StrategyValidationReadinessAnalyzerService(_storagePaths, _runtimeRoot);
        var report = service.Load() ?? service.Run();
        var plan = report.Items.FirstOrDefault();
        var topic = ExtractTopic(plan?.ValidationPlanId, plan?.StrategyPattern, plan?.Asset, plan?.Timeframe, string.Join(" ", plan?.MissingRequirements ?? []));

        if (topic is null)
        {
            return BuildEntry(
                "strategy-validation-readiness-analyzer",
                "strategy validation readiness",
                false,
                false,
                null,
                null,
                "validation readiness not supported by trusted knowledge",
                [],
                ["candidate_support_available_not_used_automatically"],
                ["topic could not be inferred from the latest validation readiness report"],
                ["validation plan id", "strategy pattern", "asset", "timeframe", "missing requirements"],
                ["topic not inferred from validation readiness report"]);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            "strategy-validation-readiness-analyzer",
            "strategy validation readiness",
            true,
            true,
            topic,
            reasoning.Confidence,
            "validation readiness supported by trusted knowledge",
            reasoning.UsedKnowledgeIds.ToList(),
            reasoning.CandidateSupport.Select(item => item.KnowledgeId).ToList(),
            reasoning.OpenUncertainties.ToList(),
            [],
            reasoning.Recommendations.ToList());
    }

    private TrustedKnowledgeImpactEntry BuildReviewDecisionAssistantEntry()
    {
        var report = new ReviewDecisionAssistantService(_storagePaths).Load()
            ?? new ReviewDecisionAssistantService(_storagePaths).Run();
        var entry = report.Entries.FirstOrDefault();
        var topic = ExtractTopic(entry?.KnowledgeItemId, entry?.Title, entry?.Domain, entry?.RecommendationLabel, entry?.WhyNow, entry?.NextStep);

        if (topic is null)
        {
            return BuildEntry(
                "review-decision-assistant",
                "review decision assistant",
                false,
                false,
                null,
                null,
                "review recommendation currently driven by review queue state rather than trusted knowledge",
                [],
                [],
                ["no review entries available; trusted knowledge context not inferred"],
                ["knowledge item id", "title", "domain", "recommendation label", "why now", "next step"],
                ["no review entries available in current report"]);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            "review-decision-assistant",
            "review decision assistant",
            true,
            true,
            topic,
            reasoning.Confidence,
            "review recommendation supported by trusted knowledge",
            reasoning.UsedKnowledgeIds.ToList(),
            reasoning.CandidateSupport.Select(item => item.KnowledgeId).ToList(),
            reasoning.OpenUncertainties.ToList(),
            [],
            reasoning.Recommendations.ToList());
    }

    private static TrustedKnowledgeImpactEntry BuildEntry(
        string command,
        string analysisLabel,
        bool trustedKnowledgeUsed,
        bool topicInferred,
        string? topic,
        double? confidence,
        string supportedRecommendation,
        IReadOnlyList<string> trustedKnowledgeIds,
        IReadOnlyList<string> candidateSupportNotUsed,
        IReadOnlyList<string> reducedUncertainties,
        IReadOnlyList<string> missingTrustedKnowledge,
        IReadOnlyList<string> notes)
    {
        return new TrustedKnowledgeImpactEntry(
            Command: command,
            AnalysisLabel: analysisLabel,
            TrustedKnowledgeUsed: trustedKnowledgeUsed,
            TopicInferred: topicInferred,
            Topic: topic,
            Confidence: confidence,
            SupportedRecommendation: supportedRecommendation,
            TrustedKnowledgeIds: trustedKnowledgeIds,
            CandidateSupportNotUsed: candidateSupportNotUsed,
            ReducedUncertainties: reducedUncertainties,
            MissingTrustedKnowledge: missingTrustedKnowledge,
            Notes: notes);
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<TrustedKnowledgeImpactEntry> entries)
    {
        var warnings = new List<string>();
        if (entries.Any(entry => !entry.TopicInferred))
        {
            warnings.Add("some_commands_lack_inferable_topics");
        }

        if (entries.Any(entry => entry.CandidateSupportNotUsed.Count > 0))
        {
            warnings.Add("candidate_support_was_intentionally_not_used_automatically");
        }

        return warnings;
    }

    private string? InferTopicFromSignals(string path)
    {
        foreach (var line in File.ReadLines(path).TakeLast(4))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                continue;
            }

            var topic = ExtractTopic(
                GetString(root, "signal_type", "signalType"),
                string.Join(" ", GetStringArray(root, "reason_codes", "reasonCodes")),
                GetString(root, "direction"),
                GetString(root, "symbol"),
                GetString(root, "setup_name", "setupName", "pattern_name", "patternName"));
            if (topic is not null)
            {
                return topic;
            }
        }

        return null;
    }

    private static string? FindLatestExportFile(string category)
    {
        var exportRoot = Path.Combine("/mnt/d/HermesData", "exports");
        if (!Directory.Exists(exportRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(exportRoot, $"*{category}*.jsonl", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault();
    }

    private static string? ExtractTopic(params string?[] values)
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

    private static bool TryParseJsonLine(string line, out JsonElement root)
    {
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(line, JsonDefaults.SnapshotReadOptions);
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetStringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yield return text;
                        }
                    }
                }

                yield break;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        yield return part;
                    }
                }
            }
        }
    }

    private void WriteReport(TrustedKnowledgeImpactReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(TrustedKnowledgeImpactReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Trusted Knowledge Impact Report");
        builder.AppendLine();
        builder.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- commands_with_trust_impact: {string.Join(", ", report.CommandsWithTrustImpact)}");
        builder.AppendLine($"- commands_without_topic: {string.Join(", ", report.CommandsWithoutTopic)}");
        builder.AppendLine($"- topics: {string.Join(", ", report.Topics)}");
        builder.AppendLine($"- trusted_knowledge_ids: {string.Join(", ", report.TrustedKnowledgeIds)}");
        builder.AppendLine();

        foreach (var entry in report.Entries)
        {
            builder.AppendLine($"## {entry.Command}");
            builder.AppendLine($"- analysis_label: {entry.AnalysisLabel}");
            builder.AppendLine($"- trusted_knowledge_used: {entry.TrustedKnowledgeUsed}");
            builder.AppendLine($"- topic_inferred: {entry.TopicInferred}");
            builder.AppendLine($"- topic: {entry.Topic ?? "-"}");
            builder.AppendLine($"- confidence: {(entry.Confidence is null ? "-" : entry.Confidence.Value.ToString("0.###", CultureInfo.InvariantCulture))}");
            builder.AppendLine($"- supported_recommendation: {entry.SupportedRecommendation}");
            builder.AppendLine($"- trusted_knowledge_ids: {string.Join(", ", entry.TrustedKnowledgeIds)}");
            builder.AppendLine($"- candidate_support_not_used: {string.Join(", ", entry.CandidateSupportNotUsed)}");
            builder.AppendLine($"- reduced_uncertainties: {string.Join(", ", entry.ReducedUncertainties)}");
            builder.AppendLine($"- missing_trusted_knowledge: {string.Join(", ", entry.MissingTrustedKnowledge)}");
            builder.AppendLine($"- notes: {string.Join(" · ", entry.Notes)}");
            builder.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }
}
