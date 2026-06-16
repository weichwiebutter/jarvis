using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EvidenceImpactReviewEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Priority,
    double TrustBefore,
    double TrustAfter,
    double QualityBefore,
    double QualityAfter,
    double ValidationBefore,
    double ValidationAfter,
    double EvidenceScoreBefore,
    double EvidenceScoreAfter,
    string RecommendationBefore,
    string RecommendationAfter,
    string BlockingMetric,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> EvidenceTaskTypes,
    IReadOnlyList<string> EvidenceTaskStatuses,
    int EvidenceTaskCount,
    string MissingForApprove,
    string MissingForMoreEvidence,
    string MissingForReject,
    string OperatorSummary);

public sealed record EvidenceImpactAnalysisReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ReviewCount,
    int HighPriorityCount,
    int ChangedRecommendations,
    int UnchangedRecommendations,
    int RecommendedApprove,
    int RecommendedMoreEvidence,
    int RecommendedReject,
    IReadOnlyDictionary<string, int> BlockingMetricCounts,
    IReadOnlyList<EvidenceImpactReviewEntry> Reviews,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string BeforeReportPath,
    string AfterReportPath,
    string EvidenceTaskExecutionPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class EvidenceImpactAnalysisService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public EvidenceImpactAnalysisService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "evidence_impact_analysis");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "evidence_impact_analysis.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "evidence_impact_analysis.md");

    public EvidenceImpactAnalysisReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var beforeReport = new ReviewDecisionAssistantService(_storagePaths).Load();
        var afterQueue = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var afterEntries = afterQueue.Items
            .Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .Select(ReviewDecisionAssistantService.BuildEntry)
            .ToList();
        var afterByReviewId = afterEntries.ToDictionary(entry => entry.ReviewId, StringComparer.OrdinalIgnoreCase);
        var beforeEntries = beforeReport?.Entries ?? [];
        var beforeByReviewId = beforeEntries.ToDictionary(entry => entry.ReviewId, StringComparer.OrdinalIgnoreCase);
        var execution = new EvidenceTaskExecutionService(_storagePaths).Load();
        var executionByDomain = (execution?.ExecutedTasks ?? [])
            .Concat(execution?.SkippedTasks ?? [])
            .GroupBy(task => task.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var reviewIds = beforeByReviewId.Keys
            .Concat(afterByReviewId.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(id => PriorityRank(GetDomain(beforeByReviewId, afterByReviewId, id)))
            .ThenByDescending(id => GetTrust(afterByReviewId, beforeByReviewId, id))
            .ThenBy(id => GetDomain(beforeByReviewId, afterByReviewId, id), StringComparer.OrdinalIgnoreCase)
            .ThenBy(id => GetTitle(beforeByReviewId, afterByReviewId, id), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reviews = new List<EvidenceImpactReviewEntry>();
        foreach (var reviewId in reviewIds)
        {
            var before = beforeByReviewId.GetValueOrDefault(reviewId)
                ?? afterByReviewId.GetValueOrDefault(reviewId)
                ?? throw new InvalidOperationException($"Review entry missing for {reviewId}");
            var after = afterByReviewId.GetValueOrDefault(reviewId) ?? before;
            var tasks = executionByDomain.GetValueOrDefault(after.Domain) ?? [];
            reviews.Add(BuildEntry(before, after, tasks));
        }

        var reviewCount = reviews.Count;
        var highPriorityCount = reviews.Count(review => review.Priority.Equals("hoch", StringComparison.OrdinalIgnoreCase));
        var changed = reviews.Count(review => !review.RecommendationBefore.Equals(review.RecommendationAfter, StringComparison.OrdinalIgnoreCase));
        var recommendedApprove = reviews.Count(review => review.RecommendationAfter.Equals("approve", StringComparison.OrdinalIgnoreCase));
        var recommendedMoreEvidence = reviews.Count(review => review.RecommendationAfter.Equals("more_evidence", StringComparison.OrdinalIgnoreCase));
        var recommendedReject = reviews.Count(review => review.RecommendationAfter.Equals("reject", StringComparison.OrdinalIgnoreCase));
        var blockingMetricCounts = reviews
            .GroupBy(review => review.BlockingMetric, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var unchanged = reviewCount - changed;
        var operatorSummary = BuildOperatorSummary(reviews, execution);
        var report = new EvidenceImpactAnalysisReport(
            ReportVersion: "evidence_impact_analysis_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReviewCount: reviewCount,
            HighPriorityCount: highPriorityCount,
            ChangedRecommendations: changed,
            UnchangedRecommendations: unchanged,
            RecommendedApprove: recommendedApprove,
            RecommendedMoreEvidence: recommendedMoreEvidence,
            RecommendedReject: recommendedReject,
            BlockingMetricCounts: blockingMetricCounts,
            Reviews: reviews,
            Warnings: BuildWarnings(reviews, execution),
            OperatorSummary: operatorSummary,
            BeforeReportPath: beforeReport?.ReportPath ?? new ReviewDecisionAssistantService(_storagePaths).ReportPath,
            AfterReportPath: new ReviewDecisionAssistantService(_storagePaths).ReportPath,
            EvidenceTaskExecutionPath: execution?.ReportPath ?? new EvidenceTaskExecutionService(_storagePaths).ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        return report;
    }

    public EvidenceImpactAnalysisReport? Load()
    {
        var readablePath = ResolveReadableReportPath();
        _resolvedReportPath = readablePath;
        if (!File.Exists(readablePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceImpactAnalysisReport>(File.ReadAllText(readablePath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static EvidenceImpactReviewEntry BuildEntry(
        ReviewDecisionAssistantEntry before,
        ReviewDecisionAssistantEntry after,
        IReadOnlyList<EvidenceTaskExecutionEntry> tasks)
    {
        var blockingMetric = DetermineBlockingMetric(after);
        var blockingReasons = BuildBlockingReasons(after, blockingMetric);
        var missingApprove = BuildMissingApprove(after, blockingMetric, blockingReasons);
        var missingMoreEvidence = BuildMissingMoreEvidence(after, tasks);
        var missingReject = BuildMissingReject(after, blockingMetric, blockingReasons);
        return new EvidenceImpactReviewEntry(
            ReviewId: after.ReviewId,
            KnowledgeItemId: after.KnowledgeItemId,
            Title: after.Title,
            Domain: after.Domain,
            Priority: after.Priority,
            TrustBefore: before.TrustBefore,
            TrustAfter: after.TrustBefore,
            QualityBefore: before.EvidenceQuality,
            QualityAfter: after.EvidenceQuality,
            ValidationBefore: before.ValidationScore,
            ValidationAfter: after.ValidationScore,
            EvidenceScoreBefore: Math.Max(before.EvidenceQuality, before.ValidationScore),
            EvidenceScoreAfter: Math.Max(after.EvidenceQuality, after.ValidationScore),
            RecommendationBefore: before.RecommendationKey,
            RecommendationAfter: after.RecommendationKey,
            BlockingMetric: blockingMetric,
            BlockingReasons: blockingReasons,
            EvidenceTaskTypes: tasks.Select(task => task.ActionType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(task => task, StringComparer.OrdinalIgnoreCase).ToList(),
            EvidenceTaskStatuses: tasks.Select(task => task.Result).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(task => task, StringComparer.OrdinalIgnoreCase).ToList(),
            EvidenceTaskCount: tasks.Count,
            MissingForApprove: missingApprove,
            MissingForMoreEvidence: missingMoreEvidence,
            MissingForReject: missingReject,
            OperatorSummary: BuildReviewSummary(after, blockingReasons, tasks));
    }

    private static string DetermineBlockingMetric(ReviewDecisionAssistantEntry review)
    {
        if (review.RecommendationKey.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        var trustGap = Math.Max(0, 0.70 - review.TrustBefore);
        var qualityGap = Math.Max(0, 0.65 - review.EvidenceQuality);
        var validationGap = Math.Max(0, 0.65 - review.ValidationScore);
        var evidenceGap = Math.Max(0, 0.70 - Math.Max(review.EvidenceQuality, review.ValidationScore));

        var blockers = new List<(string Metric, double Gap)>
        {
            ("trust", trustGap),
            ("quality", qualityGap),
            ("validation", validationGap),
            ("evidence", evidenceGap)
        };

        if (review.RecommendationKey.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            return trustGap > 0.25 ? "trust" : qualityGap > 0.25 ? "quality" : validationGap > 0.25 ? "validation" : "hard_gate";
        }

        var blocking = blockers
            .Where(item => item.Gap > 0)
            .OrderByDescending(item => item.Gap)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(blocking.Metric) ? "none" : blocking.Metric;
    }

    private static IReadOnlyList<string> BuildBlockingReasons(ReviewDecisionAssistantEntry review, string blockingMetric)
    {
        var reasons = new List<string>();
        if (review.RecommendationKey.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Freigabe bereits plausibel.");
            return reasons;
        }

        if (review.TrustBefore < 0.70)
        {
            reasons.Add(review.TrustBefore < 0.45 ? "Vertrauen zu niedrig." : "Vertrauen noch nicht ausreichend.");
        }

        if (review.EvidenceQuality < 0.65)
        {
            reasons.Add(review.EvidenceQuality < 0.45 ? "Evidenzqualität zu schwach." : "Evidenzqualität noch mittel.");
        }

        if (review.ValidationScore < 0.65)
        {
            reasons.Add("Validierung noch nicht ausreichend.");
        }

        if (review.EvidenceQuality < 0.70 || review.ValidationScore < 0.70)
        {
            reasons.Add("OOS-/Forward-Bestätigung noch nicht stark genug.");
        }

        if (blockingMetric is "trust" or "quality" or "validation" or "evidence")
        {
            reasons.Add($"Aktuell blockiert durch {blockingMetric}.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Mehr Evidenz sinnvoll.");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildMissingApprove(ReviewDecisionAssistantEntry review, string blockingMetric, IReadOnlyList<string> blockingReasons)
    {
        var parts = new List<string>();
        if (review.TrustBefore < 0.70)
        {
            parts.Add($"Vertrauen bis 0.70 fehlt ({0.70 - review.TrustBefore:0.###}).");
        }

        if (review.EvidenceQuality < 0.65)
        {
            parts.Add($"Evidenzqualität bis 0.65 fehlt ({0.65 - review.EvidenceQuality:0.###}).");
        }

        if (review.ValidationScore < 0.65)
        {
            parts.Add($"Validierung bis 0.65 fehlt ({0.65 - review.ValidationScore:0.###}).");
        }

        if (review.EvidenceQuality < 0.70 || review.ValidationScore < 0.70)
        {
            parts.Add("OOS-/Forward-Bestätigung ausbauen.");
        }

        if (blockingReasons.Any(reason => reason.Contains("Widerspruch", StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add("Widerspruch auflösen.");
        }

        return parts.Count == 0 ? "Freigabe bereits plausibel." : string.Join(" ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildMissingMoreEvidence(ReviewDecisionAssistantEntry review, IReadOnlyList<EvidenceTaskExecutionEntry> tasks)
    {
        if (review.RecommendationKey.Equals("more_evidence", StringComparison.OrdinalIgnoreCase))
        {
            var executedTypes = tasks.Select(task => task.ActionType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return executedTypes.Count == 0
                ? "Hermes sammelt weitere Evidenz, Frank muss nichts tun."
                : $"Hermes sammelt weitere Evidenz. Ausgeführt: {string.Join(", ", executedTypes.Select(DescribeTaskType))}.";
        }

        return review.RecommendationKey.Equals("approve", StringComparison.OrdinalIgnoreCase)
            ? "Keine zusätzliche Evidenz nötig."
            : "Mehr Evidenz wurde überschritten; weitere Prüfung bleibt nötig.";
    }

    private static string BuildMissingReject(ReviewDecisionAssistantEntry review, string blockingMetric, IReadOnlyList<string> blockingReasons)
    {
        return review.RecommendationKey.Equals("reject", StringComparison.OrdinalIgnoreCase)
            ? "Ablehnung bereits empfohlen."
            : blockingMetric switch
            {
                "trust" => "Für Ablehnung müssten Vertrauen und Evidenz deutlich schwächer sein.",
                "quality" => "Für Ablehnung müsste die Evidenzqualität deutlich schwächer sein.",
                "validation" => "Für Ablehnung müsste die Validierung deutlich schwächer sein.",
                "hard_gate" => "Für Ablehnung wäre ein harter Widerspruch oder Gate-Verstoß nötig.",
                _ => "Ablehnung ist aktuell nicht das Ziel; weitere Evidenz ist der richtige Pfad."
            };
    }

    private static string BuildReviewSummary(ReviewDecisionAssistantEntry review, IReadOnlyList<string> blockingReasons, IReadOnlyList<EvidenceTaskExecutionEntry> tasks)
    {
        var recommendation = review.RecommendationLabel;
        var reason = review.RecommendationReason;
        var taskSummary = tasks.Count == 0
            ? "Keine spezifischen Evidence-Tasks auf dieses Review gemappt."
            : $"Evidence-Tasks: {tasks.Count} ({string.Join(", ", tasks.Select(task => DescribeTaskType(task.ActionType)).Distinct(StringComparer.OrdinalIgnoreCase))}).";
        return $"Aktuell: {recommendation}. Grund: {reason} {string.Join(" ", blockingReasons)} {taskSummary}".Trim();
    }

    private static string DescribeTaskType(string actionType) => actionType switch
    {
        "documentation_source_check" or "source_check" => "Quellen prüfen",
        "collect_evidence" => "Evidenz sammeln",
        "knowledge_item_validation" or "validate_knowledge_items" => "Wissen validieren",
        "trading_historical_oos_check" or "run_oos_validation" => "OOS prüfen",
        "trading_forward_observation_check" or "run_forward_validation" => "Forward prüfen",
        "evidence_quality_recheck" => "Evidenzqualität prüfen",
        "contradiction_check" => "Widersprüche prüfen",
        _ => actionType
    };

    private static int PriorityRank(string priority) => priority switch
    {
        "hoch" => 3,
        "mittel" => 2,
        _ => 1
    };

    private static string GetDomain(IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> before, IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> after, string reviewId)
        => after.TryGetValue(reviewId, out var afterEntry) ? afterEntry.Domain : before[reviewId].Domain;

    private static double GetTrust(IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> after, IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> before, string reviewId)
        => after.TryGetValue(reviewId, out var afterEntry) ? afterEntry.TrustBefore : before[reviewId].TrustBefore;

    private static string GetTitle(IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> before, IReadOnlyDictionary<string, ReviewDecisionAssistantEntry> after, string reviewId)
        => after.TryGetValue(reviewId, out var afterEntry) ? afterEntry.Title : before[reviewId].Title;

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<EvidenceImpactReviewEntry> reviews, EvidenceTaskExecutionReport? execution)
    {
        var warnings = new List<string>();
        if (reviews.Count == 0)
        {
            warnings.Add("evidence_impact_analysis_empty");
        }

        if ((execution?.TasksExecuted ?? 0) > 0 && reviews.All(review => review.RecommendationBefore == review.RecommendationAfter))
        {
            warnings.Add("evidence_execution_did_not_change_review_recommendations");
        }

        return warnings;
    }

    private static string BuildOperatorSummary(IReadOnlyList<EvidenceImpactReviewEntry> reviews, EvidenceTaskExecutionReport? execution)
    {
        var unchanged = reviews.Count(review => review.RecommendationBefore.Equals(review.RecommendationAfter, StringComparison.OrdinalIgnoreCase));
        var trading = reviews.Count(review => review.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase));
        var documentation = reviews.Count(review => review.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase));
        var validation = reviews.Count(review => review.BlockingMetric.Equals("validation", StringComparison.OrdinalIgnoreCase));
        var evidence = reviews.Count(review => review.BlockingMetric.Equals("evidence", StringComparison.OrdinalIgnoreCase));
        var trust = reviews.Count(review => review.BlockingMetric.Equals("trust", StringComparison.OrdinalIgnoreCase));
        var quality = reviews.Count(review => review.BlockingMetric.Equals("quality", StringComparison.OrdinalIgnoreCase));
        var taskSummary = execution is null
            ? "Der Evidenzlauf wurde noch nicht geladen."
            : $"Der Evidenzlauf hat {execution.TasksExecuted} Aufgaben ausgeführt, aber keine Review-Empfehlung verändert.";

        return string.Join(Environment.NewLine, new[]
        {
            $"{reviews.Count} Reviews blieben unverändert.",
            $"Hauptblocker: {validation} Validierung, {evidence} Evidenz, {trust} Vertrauen, {quality} Qualität.",
            $"Trading-Reviews: {trading}. Dokumentations-Reviews: {documentation}.",
            taskSummary,
            "Frank muss weiterhin nur die sichtbaren Reviews im Prüfzentrum entscheiden; Hermes liefert nur die Ursachenanalyse."
        });
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        var roots = new[]
        {
            Path.Combine(_storagePaths.Root, "reports", "evidence_impact_analysis"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".codex_artifacts", "reports", "evidence_impact_analysis"),
        };

        foreach (var root in roots)
        {
            try
            {
                Directory.CreateDirectory(root);
                return (Path.Combine(root, "evidence_impact_analysis.json"), Path.Combine(root, "evidence_impact_analysis.md"), root);
            }
            catch
            {
            }
        }

        var fallbackRoot = roots.Last();
        return (Path.Combine(fallbackRoot, "evidence_impact_analysis.json"), Path.Combine(fallbackRoot, "evidence_impact_analysis.md"), fallbackRoot);
    }

    private string ResolveReadableReportPath()
    {
        if (File.Exists(ReportPath))
        {
            return ReportPath;
        }

        var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_impact_analysis", "evidence_impact_analysis.json");
        return File.Exists(fallbackPath) ? fallbackPath : ReportPath;
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, EvidenceImpactAnalysisReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(reportPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_impact_analysis");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_impact_analysis.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_impact_analysis.md"), markdown);
        }
    }

    private static string BuildMarkdown(EvidenceImpactAnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Evidence Impact Analysis");
        sb.AppendLine();
        sb.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Review Count: {report.ReviewCount}");
        sb.AppendLine($"- High Priority: {report.HighPriorityCount}");
        sb.AppendLine($"- Unchanged Recommendations: {report.UnchangedRecommendations}");
        sb.AppendLine($"- Changed Recommendations: {report.ChangedRecommendations}");
        sb.AppendLine($"- Freigabe empfohlen: {report.RecommendedApprove}");
        sb.AppendLine($"- Mehr Evidenz empfohlen: {report.RecommendedMoreEvidence}");
        sb.AppendLine($"- Ablehnung empfohlen: {report.RecommendedReject}");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Blocker");
        foreach (var blocker in report.BlockingMetricCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {blocker.Key}: {blocker.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Reviews");
        foreach (var review in report.Reviews)
        {
            sb.AppendLine($"- {review.Title} ({review.Domain}, {review.RecommendationAfter})");
            sb.AppendLine($"  - Vorher: trust={review.TrustBefore:0.####}, quality={review.QualityBefore:0.####}, validation={review.ValidationBefore:0.####}, evidence={review.EvidenceScoreBefore:0.####}, recommendation={review.RecommendationBefore}");
            sb.AppendLine($"  - Nachher: trust={review.TrustAfter:0.####}, quality={review.QualityAfter:0.####}, validation={review.ValidationAfter:0.####}, evidence={review.EvidenceScoreAfter:0.####}, recommendation={review.RecommendationAfter}");
            sb.AppendLine($"  - Blocker: {review.BlockingMetric}");
            sb.AppendLine($"  - Fehlt für Freigabe: {review.MissingForApprove}");
            sb.AppendLine($"  - Fehlt für mehr Evidenz: {review.MissingForMoreEvidence}");
            sb.AppendLine($"  - Fehlt für Ablehnung: {review.MissingForReject}");
            sb.AppendLine($"  - Entscheidungshilfe: {review.OperatorSummary}");
        }

        return sb.ToString();
    }
}
