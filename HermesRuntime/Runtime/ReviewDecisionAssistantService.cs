using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ReviewDecisionAssistantEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Priority,
    double TrustBefore,
    double EvidenceQuality,
    double ValidationScore,
    string TradingRisk,
    string RecommendationKey,
    string RecommendationLabel,
    string RecommendationReason,
    string FrankAction,
    bool RequiresHumanReview);

public sealed record ReviewDecisionAssistantReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ReviewCount,
    int HighPriorityCount,
    int RecommendedApprove,
    int RecommendedMoreEvidence,
    int RecommendedReject,
    IReadOnlyList<ReviewDecisionAssistantEntry> Entries,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewDecisionAssistantService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewDecisionAssistantService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_decision_assistant.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_decision_assistant.md");

    public ReviewDecisionAssistantReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var queue = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var pending = queue.Items
            .Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .Select(BuildEntry)
            .ToList();

        var report = new ReviewDecisionAssistantReport(
            ReportVersion: "review_decision_assistant_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReviewCount: pending.Count,
            HighPriorityCount: pending.Count(entry => entry.Priority.Equals("hoch", StringComparison.OrdinalIgnoreCase)),
            RecommendedApprove: pending.Count(entry => entry.RecommendationKey == "approve"),
            RecommendedMoreEvidence: pending.Count(entry => entry.RecommendationKey == "more_evidence"),
            RecommendedReject: pending.Count(entry => entry.RecommendationKey == "reject"),
            Entries: pending
                .OrderByDescending(entry => PriorityRank(entry.Priority))
                .ThenByDescending(entry => entry.TrustBefore)
                .ThenBy(entry => entry.Domain, StringComparer.Ordinal)
                .ThenBy(entry => entry.Title, StringComparer.Ordinal)
                .ToList(),
            OperatorSummary: BuildOperatorSummary(pending),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: queue.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        return report;
    }

    public ReviewDecisionAssistantReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReviewDecisionAssistantReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public static ReviewDecisionAssistantEntry BuildEntry(HumanReviewItem item)
    {
        var trust = item.TrustBefore;
        var evidenceQuality = ReviewEvidenceQuality(item);
        var validationScore = ReviewMetric(item.EvidenceSummary, "validation");
        var tradingRisk = ReviewRisk(trust, evidenceQuality, validationScore);
        var recommendation = DetermineRecommendation(item, trust, evidenceQuality, validationScore);
        var recommendationLabel = recommendation switch
        {
            "approve" => "Freigabe empfohlen",
            "more_evidence" => "Mehr Evidenz empfohlen",
            _ => "Ablehnung empfohlen"
        };

        return new ReviewDecisionAssistantEntry(
            ReviewId: item.ReviewId,
            KnowledgeItemId: item.KnowledgeItemId,
            Title: item.Title,
            Domain: item.Domain,
            Priority: NormalizePriority(item.Domain, item.Priority),
            TrustBefore: trust,
            EvidenceQuality: evidenceQuality,
            ValidationScore: validationScore,
            TradingRisk: tradingRisk,
            RecommendationKey: recommendation,
            RecommendationLabel: recommendationLabel,
            RecommendationReason: BuildReason(item, trust, evidenceQuality, validationScore, recommendation),
            FrankAction: recommendation == "approve" ? "Prüfzentrum: Freigabe prüfen" : recommendation == "more_evidence" ? "Prüfzentrum: mehr Evidenz prüfen" : "Prüfzentrum: Ablehnung prüfen",
            RequiresHumanReview: true);
    }

    private static string BuildOperatorSummary(IReadOnlyList<ReviewDecisionAssistantEntry> entries)
    {
        var approve = entries.Count(entry => entry.RecommendationKey == "approve");
        var evidence = entries.Count(entry => entry.RecommendationKey == "more_evidence");
        var reject = entries.Count(entry => entry.RecommendationKey == "reject");
        var highPriority = entries.Count(entry => NormalizeGroupDomain(entry.Domain) == "trading");

        return string.Join(Environment.NewLine, new[]
        {
            $"🔴 {highPriority} wichtige Entscheidungen",
            $"🟡 {evidence} Reviews brauchen mehr Evidenz",
            $"🟢 {approve} Freigaben plausibel",
            $"⚫ {reject} Ablehnungen empfohlen",
            "",
            "Frank muss weiterhin selbst entscheiden. Hermes liefert nur die Empfehlung."
        });
    }

    private static string DetermineRecommendation(HumanReviewItem item, double trust, double evidenceQuality, double validationScore)
    {
        var reason = item.Reason.ToLowerInvariant();
        var evidenceSummary = item.EvidenceSummary.ToLowerInvariant();
        var hasCriticalWarning = reason.Contains("contradict") || reason.Contains("widerspruch") || evidenceSummary.Contains("contradict") || evidenceSummary.Contains("widerspruch");

        if (hasCriticalWarning || trust < 0.45 || evidenceQuality < 0.45 || validationScore < 0.45)
        {
            return "reject";
        }

        if (trust >= 0.70 && evidenceQuality >= 0.65 && validationScore >= 0.65 && !hasCriticalWarning)
        {
            return "approve";
        }

        if (trust >= 0.45 && trust < 0.70)
        {
            return "more_evidence";
        }

        if (validationScore < 0.65 || evidenceQuality < 0.70)
        {
            return "more_evidence";
        }

        return "more_evidence";
    }

    private static string BuildReason(HumanReviewItem item, double trust, double evidenceQuality, double validationScore, string recommendation)
    {
        var trustText = trust >= 0.70 ? "Vertrauen ausreichend" : trust >= 0.45 ? "Vertrauen mittel" : "Vertrauen zu niedrig";
        var evidenceText = evidenceQuality >= 0.65 ? "Evidenzqualität ausreichend" : evidenceQuality >= 0.45 ? "Evidenzqualität mittel" : "Evidenzqualität zu schwach";
        var validationText = validationScore >= 0.65 ? "Validierung vollständig genug" : "Validierung noch nicht stark genug";
        var riskText = ReviewRisk(trust, evidenceQuality, validationScore) switch
        {
            "niedrig" => "Trading-Risiko niedrig",
            "mittel" => "Trading-Risiko mittel",
            _ => "Trading-Risiko hoch"
        };

        var baseText = recommendation switch
        {
            "approve" => "Freigabe plausibel.",
            "reject" => "Ablehnung empfohlen.",
            _ => "Mehr Evidenz sinnvoll."
        };

        return $"{baseText} {trustText}. {evidenceText}. {validationText}. {riskText}.";
    }

    private static string NormalizePriority(HumanReviewPriority priority) =>
        priority switch
        {
            HumanReviewPriority.high => "hoch",
            HumanReviewPriority.medium => "mittel",
            _ => "niedrig"
        };

    private static string NormalizePriority(string domain, HumanReviewPriority priority) =>
        NormalizeGroupDomain(domain) switch
        {
            "trading" => "hoch",
            "research" => "mittel",
            "software" => "mittel",
            "process" => "niedrig",
            "documentation" => "niedrig",
            _ => NormalizePriority(priority)
        };

    private static string NormalizeGroupDomain(string domain)
    {
        var normalized = (domain ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "trading" => "trading",
            "documentation" => "documentation",
            "research" => "research",
            "software" => "software",
            "process" => "process",
            _ => normalized
        };
    }

    private static double ReviewEvidenceQuality(HumanReviewItem item)
    {
        var quality = ReviewMetric(item.EvidenceSummary, "quality");
        var evidence = ReviewMetric(item.EvidenceSummary, "evidence");
        var validation = ReviewMetric(item.EvidenceSummary, "validation");
        var values = new[] { quality, evidence, validation }.Where(value => value > 0).ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static double ReviewMetric(string summary, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(summary ?? string.Empty, $@"{key}=([0-9.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string ReviewRisk(double trust, double evidenceQuality, double validationScore)
    {
        if (trust < 0.45 || evidenceQuality < 0.45 || validationScore < 0.45)
        {
            return "hoch";
        }

        if (trust < 0.65 || evidenceQuality < 0.62 || validationScore < 0.55)
        {
            return "mittel";
        }

        return "niedrig";
    }

    private static int PriorityRank(string priority) =>
        priority switch
        {
            "hoch" => 3,
            "mittel" => 2,
            _ => 1
        };

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string fallbackRoot, ReviewDecisionAssistantReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? fallbackRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath) ?? fallbackRoot);

        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(reportPath, json, Encoding.UTF8);
        File.WriteAllText(markdownPath, BuildMarkdown(report), Encoding.UTF8);
    }

    private static string BuildMarkdown(ReviewDecisionAssistantReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Review Decision Assistant");
        sb.AppendLine();
        sb.AppendLine($"- Pending Reviews: {report.ReviewCount}");
        sb.AppendLine($"- High Priority: {report.HighPriorityCount}");
        sb.AppendLine($"- Freigabe empfohlen: {report.RecommendedApprove}");
        sb.AppendLine($"- Mehr Evidenz empfohlen: {report.RecommendedMoreEvidence}");
        sb.AppendLine($"- Ablehnung empfohlen: {report.RecommendedReject}");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        foreach (var entry in report.Entries.Take(20))
        {
            sb.AppendLine($"- {entry.Title} ({entry.Domain}, {entry.RecommendationLabel})");
            sb.AppendLine($"  - Warum: {entry.RecommendationReason}");
        }

        return sb.ToString();
    }

    private (string reportPath, string markdownPath, string root) ResolveOutputPaths()
    {
        var roots = new[]
        {
            Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".codex_artifacts", "reports", "review_decision_assistant"),
        };

        foreach (var root in roots)
        {
            try
            {
                Directory.CreateDirectory(root);
                var reportPath = Path.Combine(root, "review_decision_assistant.json");
                var markdownPath = Path.Combine(root, "review_decision_assistant.md");
                return (reportPath, markdownPath, root);
            }
            catch
            {
            }
        }

        var fallbackRoot = roots.Last();
        return (Path.Combine(fallbackRoot, "review_decision_assistant.json"), Path.Combine(fallbackRoot, "review_decision_assistant.md"), fallbackRoot);
    }
}
