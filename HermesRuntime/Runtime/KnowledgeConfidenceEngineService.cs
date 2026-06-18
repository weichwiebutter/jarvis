using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeConfidenceHypothesisResult(
    string HypothesisId,
    string Title,
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

public sealed record KnowledgeConfidenceEngineReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int EvaluatedHypotheses,
    KnowledgeConfidenceHypothesisResult? TopCandidate,
    IReadOnlyList<KnowledgeConfidenceHypothesisResult> Hypotheses,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeConfidenceEngineService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public KnowledgeConfidenceEngineService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_confidence_engine");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "knowledge_confidence_engine.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "knowledge_confidence_engine.md");

    public KnowledgeConfidenceEngineReport Run()
    {
        Directory.CreateDirectory(Root);
        var rootCause = LoadJson(Path.Combine(_storagePaths.Root, "reports", "knowledge_health_root_cause", "knowledge_health_root_cause.json"));
        var reviewAssistant = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant", "review_decision_assistant.json"));
        var reviewAudit = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_prioritization_audit", "review_prioritization_audit.json"));
        var backtestQuality = LoadJson(Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_quality", "strategy_backtest_quality_audit.json"));
        var failureLearning = LoadJson(Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_failure_learning", "strategy_backtest_failure_learning.json"));
        var attributionAnalysis = LoadJson(Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis", "mutation_attribution_analysis.json"));
        var attributionFeedback = LoadJson(Path.Combine(_storagePaths.Root, "reports", "attribution_hypothesis_feedback", "attribution_hypothesis_feedback.json"));
        var oosExecution = LoadJson(Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_execution_gate", "autonomous_oos_execution_gate.json"));
        var forwardSync = LoadJson(Path.Combine(_storagePaths.Root, "reports", "autonomous_forward_observation_sync", "autonomous_forward_observation_sync.json"));
        var runtimeHealth = LoadJson(Path.Combine(_storagePaths.Root, "reports", "runtime_health_summary", "runtime_health_summary.json"));
        var hypotheses = LoadHypotheses();

        var results = hypotheses
            .Select(hypothesis => BuildResult(hypothesis, rootCause, reviewAssistant, reviewAudit, backtestQuality, failureLearning, attributionAnalysis, attributionFeedback, oosExecution, forwardSync, runtimeHealth))
            .OrderByDescending(item => item.ConfidenceScore)
            .ThenBy(item => item.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = results.FirstOrDefault();
        var report = new KnowledgeConfidenceEngineReport(
            ReportVersion: "knowledge_confidence_engine_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            EvaluatedHypotheses: results.Count,
            TopCandidate: top,
            Hypotheses: results,
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

    private KnowledgeConfidenceHypothesisResult BuildResult(
        CognitiveHypothesis hypothesis,
        JsonElement? rootCause,
        JsonElement? reviewAssistant,
        JsonElement? reviewAudit,
        JsonElement? backtestQuality,
        JsonElement? failureLearning,
        JsonElement? attributionAnalysis,
        JsonElement? attributionFeedback,
        JsonElement? oosExecution,
        JsonElement? forwardSync,
        JsonElement? runtimeHealth)
    {
        var backtest = AnalyzeBacktest(hypothesis, backtestQuality, failureLearning);
        var oos = AnalyzeOos(hypothesis, oosExecution);
        var forward = AnalyzeForward(hypothesis, forwardSync);
        var attribution = AnalyzeAttribution(hypothesis, attributionAnalysis, attributionFeedback);
        var evidence = AnalyzeEvidence(hypothesis, rootCause, runtimeHealth);
        var review = AnalyzeReviews(hypothesis, reviewAssistant, reviewAudit);

        var rawScore = (backtest.Score * 0.28) + (oos.Score * 0.16) + (forward.Score * 0.16) + (attribution.Score * 0.16) + (evidence.Score * 0.12) + (review.Score * 0.12);
        var confidenceScore = Math.Round(Math.Clamp(rawScore, 0, 1) * 100, 1);
        var confidenceClass = Classify(confidenceScore, oos.Status, forward.Status, review.PendingReviews);
        var positives = new List<string>();
        positives.AddRange(backtest.PositiveDrivers);
        positives.AddRange(oos.PositiveDrivers);
        positives.AddRange(attribution.PositiveDrivers);
        var blockers = new List<string>();
        blockers.AddRange(backtest.Blockers);
        blockers.AddRange(oos.Blockers);
        blockers.AddRange(forward.Blockers);
        blockers.AddRange(review.Blockers);
        blockers.AddRange(evidence.Blockers);

        return new KnowledgeConfidenceHypothesisResult(
            HypothesisId: hypothesis.HypothesisId,
            Title: hypothesis.Title,
            Asset: ResolveAsset(hypothesis, attributionFeedback),
            Timeframe: ResolveTimeframe(hypothesis, attributionFeedback),
            StrategyPattern: ResolveStrategyPattern(hypothesis, attributionFeedback),
            ConfidenceScore: confidenceScore,
            ConfidenceClass: confidenceClass,
            StrongestPositiveDrivers: positives.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(),
            StrongestBlockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(),
            NextEvidenceStep: RecommendNextStep(oos.Status, forward.Status, review.PendingReviews, evidence.ValidationScore),
            FrankRequired: review.PendingReviews > 0 && review.HighPriorityReviews > 0,
            MayPromote: false);
    }

    private static string Classify(double score, string oosStatus, string forwardStatus, int pendingReviews)
    {
        if (score >= 85 && oosStatus == "improved" && (forwardStatus is "confirmed" or "signal_seen"))
        {
            return "validated";
        }

        if (score >= 70)
        {
            return "high";
        }

        if (score >= 50)
        {
            return "medium";
        }

        if (score >= 30)
        {
            return "low";
        }

        return "very_low";
    }

    private static string RecommendNextStep(string oosStatus, string forwardStatus, int pendingReviews, double validationScore)
        => forwardStatus is "missing" or "no_signal" or "waiting_for_allowed_window" or "waiting_for_market_data"
            ? "Forward-Evidenz sammeln"
            : oosStatus is "missing" or "inconclusive"
                ? "OOS-Daten nachziehen"
                : pendingReviews > 0
                    ? "Top-Reviews schließen"
                    : validationScore < 0.55
                        ? "Validation-Artefakte nachziehen"
                        : "Nächste Evidenzschicht prüfen";

    private static BacktestAnalysis AnalyzeBacktest(CognitiveHypothesis hypothesis, JsonElement? qualityReport, JsonElement? failureLearning)
    {
        var item = FindBacktestEntry(qualityReport, hypothesis);
        var trades = ReadInt(item, "trades_simulated", "TradesSimulated");
        var pf = ReadDouble(item, "profit_factor", "ProfitFactor", "mutation_profit_factor");
        var expectancy = ReadDouble(item, "expectancy", "Expectancy", "mutation_expectancy");
        var drawdown = ReadDouble(item, "max_drawdown", "MaxDrawdown", "mutation_max_drawdown");
        var qualityClass = ReadString(item, "quality_class", "QualityClass") ?? "missing";

        var score = 0.0;
        score += NormalizeTrades(trades) * 0.3;
        score += NormalizeProfitFactor(pf) * 0.25;
        score += NormalizeExpectancy(expectancy) * 0.2;
        score += NormalizeDrawdown(drawdown) * 0.15;
        score += QualityClassScore(qualityClass) * 0.1;

        var blockers = new List<string>();
        if (trades < 30) blockers.Add($"nur {trades} Trades");
        if (pf < 1.0) blockers.Add($"Profit Factor {pf:0.###}");
        if (expectancy <= 0) blockers.Add($"Expectancy {expectancy:0.###}");
        if (drawdown < -5) blockers.Add($"Drawdown {drawdown:0.###}");
        if (qualityClass.Contains("low", StringComparison.OrdinalIgnoreCase) || qualityClass.Contains("insufficient", StringComparison.OrdinalIgnoreCase)) blockers.Add($"Quality {qualityClass}");
        var positives = new List<string>();
        if (trades >= 50) positives.Add($"Sample Size {trades}");
        if (pf > 0.8) positives.Add($"PF {pf:0.###}");
        if (expectancy > -0.1) positives.Add($"Expectancy {expectancy:0.###}");
        return new BacktestAnalysis(score, positives, blockers);
    }

    private static OosAnalysis AnalyzeOos(CognitiveHypothesis hypothesis, JsonElement? oosExecution)
    {
        var status = ReadString(ReadObject(oosExecution, "result"), "outcome", "Outcome") ?? ReadString(oosExecution, "outcome", "Outcome") ?? "missing";
        var score = status switch
        {
            "improved" => 1.0,
            "worse" => 0.15,
            "inconclusive" => 0.45,
            _ => 0.0
        };
        var blockers = new List<string>();
        if (status == "missing") blockers.Add("OOS fehlt");
        else if (status == "worse") blockers.Add("OOS verschlechtert");
        else if (status == "inconclusive") blockers.Add("OOS inconclusive");
        var positives = new List<string>();
        if (status == "improved") positives.Add("OOS verbessert");
        return new OosAnalysis(status, score, positives, blockers);
    }

    private static ForwardAnalysis AnalyzeForward(CognitiveHypothesis hypothesis, JsonElement? forwardSync)
    {
        var items = ReadArray(forwardSync, "items");
        var item = items.FirstOrDefault(element => string.Equals(ReadString(element, "hypothesis_id", "HypothesisId"), hypothesis.HypothesisId, StringComparison.OrdinalIgnoreCase));
        var status = ReadString(item, "synced_status", "SyncedStatus") ?? ReadString(item, "observation_status", "ObservationStatus") ?? "missing";
        var score = status switch
        {
            "confirmed" => 1.0,
            "signal_seen" => 0.7,
            "invalidated" => 0.1,
            "no_signal" => 0.35,
            "still_open_waiting_for_signal" => 0.3,
            _ => 0.0
        };
        var blockers = new List<string>();
        if (status is "missing" or "no_signal" or "still_open_waiting_for_signal")
        {
            blockers.Add("fehlende Forward-Bestätigung");
        }
        else if (status == "invalidated")
        {
            blockers.Add("invalidiert");
        }
        var positives = new List<string>();
        if (status is "confirmed" or "signal_seen")
        {
            positives.Add("Forward-Evidenz vorhanden");
        }
        return new ForwardAnalysis(status, score, positives, blockers);
    }

    private static AttributionAnalysis AnalyzeAttribution(CognitiveHypothesis hypothesis, JsonElement? attributionAnalysis, JsonElement? attributionFeedback)
    {
        var score = 0.0;
        var positives = new List<string>();
        var blockers = new List<string>();
        var feedbackHypothesis = ReadObject(attributionFeedback, "hypothesis");
        var feedbackId = ReadString(feedbackHypothesis, "hypothesis_id", "HypothesisId");
        if (!string.IsNullOrWhiteSpace(feedbackId) && feedbackId.Equals(hypothesis.HypothesisId, StringComparison.OrdinalIgnoreCase))
        {
            score = 1.0;
            positives.Add("Attribution bestätigt");
        }
        else if (ReadArray(attributionAnalysis, "items").Any())
        {
            score = 0.6;
            positives.Add("Attribution vorhanden");
        }
        else
        {
            blockers.Add("Attribution fehlt");
        }

        return new AttributionAnalysis(score, positives, blockers);
    }

    private static EvidenceAnalysis AnalyzeEvidence(CognitiveHypothesis hypothesis, JsonElement? rootCause, JsonElement? runtimeHealth)
    {
        var trust = ReadDouble(rootCause, "current_trust_value", "currentTrustValue", "current_trust", "average_trust_score", "averageTrustScore");
        var validationScore = ReadDouble(rootCause, "validation_score", "validationScore", "validation_coverage", "validationCoverage");
        var contradictions = ReadInt(rootCause, "open_contradictions", "openContradictions", "contradiction_count", "contradictionCount");
        var score = 1.0;
        if (trust > 0)
        {
            score *= Math.Clamp(trust, 0, 1);
        }
        if (validationScore > 0)
        {
            score *= Math.Clamp(validationScore, 0, 1);
        }
        score *= Math.Max(0.1, 1 - (contradictions * 0.08));
        var blockers = new List<string>();
        if (contradictions > 0) blockers.Add($"{contradictions} Widersprüche");
        if (validationScore > 0 && validationScore < 0.6) blockers.Add($"Validation {validationScore:0.###}");
        return new EvidenceAnalysis(score, validationScore, blockers);
    }

    private static ReviewAnalysis AnalyzeReviews(CognitiveHypothesis hypothesis, JsonElement? reviewAssistant, JsonElement? reviewAudit)
    {
        var pending = ReadInt(reviewAssistant, "review_count", "ReviewCount", "pending_reviews", "PendingReviews");
        var high = ReadInt(reviewAssistant, "high_priority_count", "HighPriorityCount", "high_priority_reviews", "HighPriorityReviews");
        var recommendation = ReadString(ReadArray(reviewAssistant, "entries").FirstOrDefault(), "recommendation_label", "RecommendationLabel") ?? ReadString(ReadArray(reviewAssistant, "entries").FirstOrDefault(), "recommendation", "Recommendation") ?? "missing";
        var missingEvidence = ReadArray(reviewAssistant, "entries").FirstOrDefault();
        var score = Math.Clamp(1 - (pending / 25.0), 0, 1) * 0.6 + Math.Clamp(1 - (high / 10.0), 0, 1) * 0.4;
        var blockers = new List<string>();
        if (pending > 0) blockers.Add($"{pending} offene Reviews");
        if (high > 0) blockers.Add($"{high} High Priority");
        if (!string.Equals(recommendation, "Freigabe empfohlen", StringComparison.OrdinalIgnoreCase)) blockers.Add("Freigabe nicht empfohlen");
        return new ReviewAnalysis(pending, high, score, blockers);
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

    private static IReadOnlyList<CognitiveHypothesis> LoadHypotheses()
    {
        var path = Path.Combine(_storageRootStatic, "cognitive_core", "insights", "hypotheses.json");
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<CognitiveHypothesis>>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static JsonElement? FindBacktestEntry(JsonElement? report, CognitiveHypothesis hypothesis)
    {
        if (report is null) return null;
        if (report.Value.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                var pattern = ReadString(entry, "strategy_pattern", "StrategyPattern");
                if (string.Equals(pattern, hypothesis.Title, StringComparison.OrdinalIgnoreCase)
                    || hypothesis.SourceItemIds.Any(source => !string.IsNullOrWhiteSpace(source) && pattern is not null && source.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return entry;
                }
            }
            return entries.EnumerateArray().FirstOrDefault();
        }
        return report;
    }

    private static JsonElement? ReadObject(JsonElement? element, string property)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        return element.Value.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : null;
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement? element, string property)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return [];
        if (element.Value.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            return nested.EnumerateArray().ToList();
        }
        return [];
    }

    private static string? ReadString(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static int ReadInt(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return 0;
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
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return 0;
        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var prop) && prop.TryGetDouble(out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private static double NormalizeTrades(int trades) => Math.Clamp(trades / 300.0, 0, 1);
    private static double NormalizeProfitFactor(double value) => value <= 0 ? 0 : Math.Clamp(value / 2.0, 0, 1);
    private static double NormalizeExpectancy(double value) => value <= 0 ? 0.1 : Math.Clamp(value / 0.1, 0, 1);
    private static double NormalizeDrawdown(double value) => value >= 0 ? 1 : Math.Clamp(1 - Math.Abs(value) / 20.0, 0, 1);
    private static double QualityClassScore(string qualityClass) => qualityClass switch { "completed" => 1, "low_confidence" => 0.5, "insufficient_sample" => 0.1, _ => 0.3 };

    private static string BuildOperatorSummary(KnowledgeConfidenceHypothesisResult? top)
        => top is null
            ? "Keine Hypothese bewertbar. Frank muss nichts freigeben."
            : $"Hermes bewertet die {top.Title} aktuell mit {top.ConfidenceScore:0.#} % Confidence.\nPositive Treiber: {string.Join(", ", top.StrongestPositiveDrivers)}.\nBlocker: {string.Join(", ", top.StrongestBlockers)}.\nNächster Schritt: {top.NextEvidenceStep}.\nFrank muss aktuell nichts freigeben.";

    private static string ResolveAsset(CognitiveHypothesis hypothesis, JsonElement? attributionFeedback)
        => ReadString(ReadObject(attributionFeedback, "hypothesis"), "asset", "Asset") ?? "unknown";

    private static string ResolveTimeframe(CognitiveHypothesis hypothesis, JsonElement? attributionFeedback)
        => ReadString(ReadObject(attributionFeedback, "hypothesis"), "timeframe", "Timeframe") ?? "unknown";

    private static string ResolveStrategyPattern(CognitiveHypothesis hypothesis, JsonElement? attributionFeedback)
        => ReadString(ReadObject(attributionFeedback, "hypothesis"), "strategy_pattern", "StrategyPattern")
            ?? hypothesis.Title;

    private void WriteArtifacts(KnowledgeConfidenceEngineReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(KnowledgeConfidenceEngineReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Confidence Engine");
        sb.AppendLine();
        sb.AppendLine($"- Evaluated Hypotheses: {report.EvaluatedHypotheses}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        foreach (var hypothesis in report.Hypotheses.Take(10))
        {
            sb.AppendLine($"- {hypothesis.Title} | {hypothesis.ConfidenceClass} | {hypothesis.ConfidenceScore:0.#}");
            sb.AppendLine($"  - Positives: {string.Join(", ", hypothesis.StrongestPositiveDrivers)}");
            sb.AppendLine($"  - Blockers: {string.Join(", ", hypothesis.StrongestBlockers)}");
            sb.AppendLine($"  - Next: {hypothesis.NextEvidenceStep}");
        }
        return sb.ToString();
    }

    private static readonly string _storageRootStatic = "/mnt/d/HermesData";

    private sealed record BacktestAnalysis(double Score, IReadOnlyList<string> PositiveDrivers, IReadOnlyList<string> Blockers);
    private sealed record OosAnalysis(string Status, double Score, IReadOnlyList<string> PositiveDrivers, IReadOnlyList<string> Blockers);
    private sealed record ForwardAnalysis(string Status, double Score, IReadOnlyList<string> PositiveDrivers, IReadOnlyList<string> Blockers);
    private sealed record AttributionAnalysis(double Score, IReadOnlyList<string> PositiveDrivers, IReadOnlyList<string> Blockers);
    private sealed record EvidenceAnalysis(double Score, double ValidationScore, IReadOnlyList<string> Blockers);
    private sealed record ReviewAnalysis(int PendingReviews, int HighPriorityReviews, double Score, IReadOnlyList<string> Blockers);
}
