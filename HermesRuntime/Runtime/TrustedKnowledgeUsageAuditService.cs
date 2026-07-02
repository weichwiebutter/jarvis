using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TrustedKnowledgeUsageAuditEntry(
    string Command,
    string AnalysisLabel,
    bool TrustedKnowledgeContextUsed,
    bool TopicInferred,
    string? Topic,
    double? Confidence,
    IReadOnlyList<string> TrustedKnowledgeIds,
    IReadOnlyList<string> MissingTopicFields,
    IReadOnlyList<string> TopicSourceFields,
    string CurrentState,
    IReadOnlyList<string> Notes);

public sealed record TrustedKnowledgeUsageAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TrustedKnowledgeUsageAuditEntry> Entries,
    int CommandsWithTrustedKnowledgeContext,
    int CommandsWithoutInferredTopic,
    IReadOnlyList<string> UsedTopics,
    IReadOnlyList<string> UsedKnowledgeIds,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool ReadOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class TrustedKnowledgeUsageAuditService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TrustedKnowledgeUsageAuditService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trusted_knowledge_usage_audit");

    public string ReportPath => Path.Combine(Root, "trusted_knowledge_usage_audit_report.json");

    public string MarkdownPath => Path.Combine(Root, "trusted_knowledge_usage_audit_report.md");

    public TrustedKnowledgeUsageAuditReport Run()
    {
        Directory.CreateDirectory(Root);

        var entries = new List<TrustedKnowledgeUsageAuditEntry>
        {
            BuildSignalsEntry(),
            BuildTradingResearchSynthesizerEntry(),
            BuildStrategyValidationReadinessEntry(),
            BuildReviewDecisionAssistantEntry()
        };

        var usedTopics = entries
            .Where(entry => entry.TopicInferred && !string.IsNullOrWhiteSpace(entry.Topic))
            .Select(entry => entry.Topic!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var usedKnowledgeIds = entries
            .SelectMany(entry => entry.TrustedKnowledgeIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new TrustedKnowledgeUsageAuditReport(
            ReportVersion: "trusted_knowledge_usage_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Entries: entries,
            CommandsWithTrustedKnowledgeContext: entries.Count(entry => entry.TrustedKnowledgeContextUsed),
            CommandsWithoutInferredTopic: entries.Count(entry => entry.TrustedKnowledgeContextUsed && !entry.TopicInferred),
            UsedTopics: usedTopics,
            UsedKnowledgeIds: usedKnowledgeIds,
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

    public TrustedKnowledgeUsageAuditReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrustedKnowledgeUsageAuditReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private TrustedKnowledgeUsageAuditEntry BuildSignalsEntry()
    {
        var files = FindExportFiles("signals").ToList();
        if (files.Count == 0)
        {
            return BuildEntry(
                command: "signals",
                analysisLabel: "signal explanation",
                topicInferred: false,
                topic: null,
                confidence: null,
                trustedKnowledgeIds: [],
                missingTopicFields: ["signal_export_file"],
                topicSourceFields: ["signal_type", "reason_codes", "direction", "symbol", "setup_name/pattern_name"],
                currentState: "no_signal_export_available",
                notes: ["No signal export file found to infer topic from."],
                knownValues: []);
        }

        var file = files[^1];
        string? inferredTopic = null;
        var knowledgeIds = new List<string>();
        var notes = new List<string>();
        var sourceFields = new List<string>();
        var missing = new List<string>();

        foreach (var line in ReadRecentJsonlLines(file, 4))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                continue;
            }

            var values = new[]
            {
                GetString(root, "signal_type", "signalType"),
                string.Join(" ", GetStringArray(root, "reason_codes", "reasonCodes")),
                GetString(root, "direction"),
                GetString(root, "symbol"),
                GetString(root, "setup_name", "setupName", "pattern_name", "patternName")
            };

            sourceFields.AddRange(values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!));
            inferredTopic ??= ExtractTopic(values);
            if (inferredTopic is not null)
            {
                notes.Add("Topic inferred from latest signal export row.");
                break;
            }
        }

        if (inferredTopic is null)
        {
            missing.AddRange(["signal_type", "reason_codes", "direction", "symbol", "setup_name/pattern_name"]);
            notes.Add("Latest signal export rows did not provide enough topic cues.");
        }
        else
        {
            var reasoning = new KnowledgeReasoningService(_storagePaths).Run(inferredTopic);
            knowledgeIds = reasoning.UsedKnowledgeIds.ToList();
            return BuildEntry(
                command: "signals",
                analysisLabel: "signal explanation",
                topicInferred: true,
                topic: inferredTopic,
                confidence: reasoning.Confidence,
                trustedKnowledgeIds: knowledgeIds,
                missingTopicFields: [],
                topicSourceFields: ["signal_type", "reason_codes", "direction", "symbol", "setup_name/pattern_name"],
                currentState: "topic_inferred",
                notes: notes.Concat(reasoning.OpenUncertainties).ToList(),
                knownValues: []);
        }

        return BuildEntry(
            command: "signals",
            analysisLabel: "signal explanation",
            topicInferred: false,
            topic: null,
            confidence: null,
            trustedKnowledgeIds: knowledgeIds,
            missingTopicFields: missing,
            topicSourceFields: ["signal_type", "reason_codes", "direction", "symbol", "setup_name/pattern_name"],
            currentState: "topic_not_inferred",
            notes: notes,
            knownValues: []);
    }

    private TrustedKnowledgeUsageAuditEntry BuildTradingResearchSynthesizerEntry()
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
                command: "trading-research-synthesizer",
                analysisLabel: "trading research synthesizer",
                topicInferred: false,
                topic: null,
                confidence: null,
                trustedKnowledgeIds: [],
                missingTopicFields: ["pattern_name", "hypothesis_title", "hypothesis_text", "internal_sources", "external_sources"],
                topicSourceFields: ["pattern_name", "hypothesis_title", "hypothesis_text", "internal_sources", "external_sources"],
                currentState: "topic_not_inferred",
                notes: ["Could not infer a topic from the latest synthesizer report."],
                knownValues: []);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            command: "trading-research-synthesizer",
            analysisLabel: "trading research synthesizer",
            topicInferred: true,
            topic: topic,
            confidence: reasoning.Confidence,
            trustedKnowledgeIds: reasoning.UsedKnowledgeIds.ToList(),
            missingTopicFields: [],
            topicSourceFields: ["pattern_name", "hypothesis_title", "hypothesis_text", "internal_sources", "external_sources"],
            currentState: "topic_inferred",
            notes: reasoning.OpenUncertainties.ToList(),
            knownValues: []);
    }

    private TrustedKnowledgeUsageAuditEntry BuildStrategyValidationReadinessEntry()
    {
        var strategyService = new StrategyValidationReadinessAnalyzerService(_storagePaths, _runtimeRoot);
        var report = strategyService.Load() ?? strategyService.Run();
        var plan = report.Items.FirstOrDefault();
        var topic = ExtractTopic(
            plan?.ValidationPlanId,
            plan?.StrategyPattern,
            plan?.Asset,
            plan?.Timeframe,
            string.Join(" ", plan?.MissingRequirements ?? []));

        if (topic is null)
        {
            return BuildEntry(
                command: "strategy-validation-readiness-analyzer",
                analysisLabel: "strategy validation",
                topicInferred: false,
                topic: null,
                confidence: null,
                trustedKnowledgeIds: [],
                missingTopicFields: ["validation_plan_id", "strategy_pattern", "asset", "timeframe", "missing_requirements"],
                topicSourceFields: ["validation_plan_id", "strategy_pattern", "asset", "timeframe", "missing_requirements"],
                currentState: "topic_not_inferred",
                notes: ["Could not infer a trusted knowledge topic from the first validation plan."],
                knownValues: []);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            command: "strategy-validation-readiness-analyzer",
            analysisLabel: "strategy validation",
            topicInferred: true,
            topic: topic,
            confidence: reasoning.Confidence,
            trustedKnowledgeIds: reasoning.UsedKnowledgeIds.ToList(),
            missingTopicFields: [],
            topicSourceFields: ["validation_plan_id", "strategy_pattern", "asset", "timeframe", "missing_requirements"],
            currentState: "topic_inferred",
            notes: reasoning.OpenUncertainties.ToList(),
            knownValues: []);
    }

    private TrustedKnowledgeUsageAuditEntry BuildReviewDecisionAssistantEntry()
    {
        var report = new ReviewDecisionAssistantService(_storagePaths).Load()
            ?? new ReviewDecisionAssistantService(_storagePaths).Run();

        var entry = report.Entries.FirstOrDefault();
        var topic = ExtractTopic(
            entry?.KnowledgeItemId,
            entry?.Title,
            entry?.Domain,
            entry?.RecommendationLabel,
            entry?.WhyNow,
            entry?.NextStep);

        if (topic is null)
        {
            return BuildEntry(
                command: "review-decision-assistant",
                analysisLabel: "review decision assistant",
                topicInferred: false,
                topic: null,
                confidence: null,
                trustedKnowledgeIds: [],
                missingTopicFields: entry is null ? ["review_entries"] : ["knowledge_item_id", "title", "domain", "recommendation_label", "why_now", "next_step"],
                topicSourceFields: ["knowledge_item_id", "title", "domain", "recommendation_label", "why_now", "next_step"],
                currentState: entry is null ? "no_reviews_available" : "topic_not_inferred",
                notes: entry is null
                    ? ["No review entries available in the latest review decision report."]
                    : ["The latest review entry did not provide enough topic cues."],
                knownValues: []);
        }

        var reasoning = new KnowledgeReasoningService(_storagePaths).Run(topic);
        return BuildEntry(
            command: "review-decision-assistant",
            analysisLabel: "review decision assistant",
            topicInferred: true,
            topic: topic,
            confidence: reasoning.Confidence,
            trustedKnowledgeIds: reasoning.UsedKnowledgeIds.ToList(),
            missingTopicFields: [],
            topicSourceFields: ["knowledge_item_id", "title", "domain", "recommendation_label", "why_now", "next_step"],
            currentState: "topic_inferred",
            notes: reasoning.OpenUncertainties.ToList(),
            knownValues: []);
    }

    private static TrustedKnowledgeUsageAuditEntry BuildEntry(
        string command,
        string analysisLabel,
        bool topicInferred,
        string? topic,
        double? confidence,
        IReadOnlyList<string> trustedKnowledgeIds,
        IReadOnlyList<string> missingTopicFields,
        IReadOnlyList<string> topicSourceFields,
        string currentState,
        IReadOnlyList<string> notes,
        IReadOnlyList<string> knownValues)
    {
        _ = knownValues;
        return new TrustedKnowledgeUsageAuditEntry(
            Command: command,
            AnalysisLabel: analysisLabel,
            TrustedKnowledgeContextUsed: true,
            TopicInferred: topicInferred,
            Topic: topic,
            Confidence: confidence,
            TrustedKnowledgeIds: trustedKnowledgeIds,
            MissingTopicFields: missingTopicFields,
            TopicSourceFields: topicSourceFields,
            CurrentState: currentState,
            Notes: notes);
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<TrustedKnowledgeUsageAuditEntry> entries)
    {
        var warnings = new List<string>();
        if (entries.Any(entry => entry.TrustedKnowledgeContextUsed && !entry.TopicInferred))
        {
            warnings.Add("some_commands_could_not_infer_a_topic_from_current_data");
        }

        if (!entries.Any(entry => entry.TopicInferred))
        {
            warnings.Add("no_trusted_knowledge_topics_inferred");
        }

        return warnings;
    }

    private string? ExtractTopic(params string?[] values)
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

        var ranked = candidates
            .Select(candidate => new
            {
                candidate.Topic,
                Score = candidate.Weight + candidate.Aliases.Sum(alias => normalized.Contains(alias, StringComparison.OrdinalIgnoreCase) ? 25 : 0)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Topic, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(candidate => candidate.Score > 0);

        return ranked?.Topic;
    }

    private void WriteReport(TrustedKnowledgeUsageAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(TrustedKnowledgeUsageAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Trusted Knowledge Usage Audit");
        builder.AppendLine();
        builder.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- commands_with_trusted_knowledge_context: {report.CommandsWithTrustedKnowledgeContext}");
        builder.AppendLine($"- commands_without_inferred_topic: {report.CommandsWithoutInferredTopic}");
        builder.AppendLine($"- used_topics: {string.Join(", ", report.UsedTopics)}");
        builder.AppendLine($"- used_knowledge_ids: {string.Join(", ", report.UsedKnowledgeIds)}");
        builder.AppendLine();

        foreach (var entry in report.Entries)
        {
            builder.AppendLine($"## {entry.Command}");
            builder.AppendLine($"- analysis_label: {entry.AnalysisLabel}");
            builder.AppendLine($"- trusted_knowledge_context_used: {entry.TrustedKnowledgeContextUsed}");
            builder.AppendLine($"- topic_inferred: {entry.TopicInferred}");
            builder.AppendLine($"- topic: {entry.Topic ?? "-"}");
            builder.AppendLine($"- confidence: {(entry.Confidence is null ? "-" : entry.Confidence.Value.ToString("0.###", CultureInfo.InvariantCulture))}");
            builder.AppendLine($"- trusted_knowledge_ids: {string.Join(", ", entry.TrustedKnowledgeIds)}");
            builder.AppendLine($"- missing_topic_fields: {string.Join(", ", entry.MissingTopicFields)}");
            builder.AppendLine($"- source_fields: {string.Join(", ", entry.TopicSourceFields)}");
            builder.AppendLine($"- current_state: {entry.CurrentState}");
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

    private IEnumerable<string> FindExportFiles(string category)
    {
        var exportRoot = Path.Combine(_storagePaths.Root, "exports");
        if (!Directory.Exists(exportRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(exportRoot, $"*{category}*.jsonl", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ReadRecentJsonlLines(string path, int limit)
    {
        var lines = File.ReadAllLines(path);
        return lines.TakeLast(Math.Max(1, limit));
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
}
