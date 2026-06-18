using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ReviewActionPlanEntry(
    string ReviewId,
    string Title,
    string Domain,
    string CurrentRecommendation,
    double ConfidenceScore,
    string ConfidenceClass,
    IReadOnlyList<string> MissingEvidence,
    string NextEvidenceStep,
    bool CanHermesActAutonomously,
    string AutonomousCommand,
    bool FrankRequired,
    string ActionStatus);

public sealed record ReviewActionPlanReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ActionPlans,
    int HermesCanContinue,
    int WaitingForSignal,
    int WaitingForOos,
    int WaitingForForward,
    int FrankDecisionRequired,
    int Blocked,
    int NoSafeAction,
    IReadOnlyList<ReviewActionPlanEntry> Entries,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewActionPlanService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewActionPlanService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_action_plan");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_action_plan.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_action_plan.md");

    public ReviewActionPlanReport Run()
    {
        Directory.CreateDirectory(Root);

        var domainAware = LoadJson(Path.Combine(_storagePaths.Root, "reports", "domain_aware_review_prioritization", "domain_aware_review_prioritization.json"));
        var confidence = LoadJson(Path.Combine(_storagePaths.Root, "reports", "confidence_review_prioritization", "confidence_review_prioritization.json"));
        var confidenceEngine = LoadJson(Path.Combine(_storagePaths.Root, "reports", "knowledge_confidence_engine", "knowledge_confidence_engine.json"));
        var assistant = LoadJson(Path.Combine(_storagePaths.Root, "reports", "review_decision_assistant", "review_decision_assistant.json"));
        var loop = LoadJson(Path.Combine(_storagePaths.Root, "reports", "autonomous_research_loop", "autonomous_research_loop.json"));
        var forwardSync = LoadJson(Path.Combine(_storagePaths.Root, "reports", "autonomous_forward_observation_sync", "autonomous_forward_observation_sync.json"));
        var oosPlanning = LoadJson(Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_planning", "autonomous_oos_planning.json"));
        var runtimeHealth = LoadJson(Path.Combine(_storagePaths.Root, "reports", "runtime_health_summary", "runtime_health_summary.json"));

        var actionEntries = LoadTopTradingEntries(domainAware)
            .Select(entry => BuildEntry(entry, confidence, confidenceEngine, assistant, loop, forwardSync, oosPlanning, runtimeHealth))
            .ToList();

        var report = new ReviewActionPlanReport(
            ReportVersion: "review_action_plan_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ActionPlans: actionEntries.Count,
            HermesCanContinue: actionEntries.Count(entry => entry.CanHermesActAutonomously),
            WaitingForSignal: actionEntries.Count(entry => entry.ActionStatus == "waiting_for_signal"),
            WaitingForOos: actionEntries.Count(entry => entry.ActionStatus == "waiting_for_oos"),
            WaitingForForward: actionEntries.Count(entry => entry.ActionStatus == "waiting_for_forward"),
            FrankDecisionRequired: actionEntries.Count(entry => entry.FrankRequired),
            Blocked: actionEntries.Count(entry => entry.ActionStatus == "blocked"),
            NoSafeAction: actionEntries.Count(entry => entry.ActionStatus == "no_safe_action"),
            Entries: actionEntries,
            OperatorSummary: BuildOperatorSummary(actionEntries),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            SourceReports: new[]
            {
                "/reports/domain-aware-review-prioritization",
                "/reports/confidence-review-prioritization",
                "/reports/knowledge-confidence-engine",
                "/reports/review-decision-assistant",
                "/reports/autonomous-research-loop-status",
                "/reports/autonomous-forward-observation-sync",
                "/reports/autonomous-oos-planning",
                "/reports/runtime-health-summary",
            },
            Warnings: [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteArtifacts(report);
        return report;
    }

    private static ReviewActionPlanEntry BuildEntry(JsonElement review, JsonElement? confidencePrioritization, JsonElement? confidenceEngine, JsonElement? assistant, JsonElement? loop, JsonElement? forwardSync, JsonElement? oosPlanning, JsonElement? runtimeHealth)
    {
        var reviewId = ReadString(review, "review_id", "ReviewId") ?? "unknown";
        var title = ReadString(review, "title", "Title") ?? "unknown";
        var domain = ReadString(review, "classified_domain", "ClassifiedDomain") ?? ReadString(review, "domain", "Domain") ?? "unknown";
        var recommendation = ReadString(review, "recommendation", "Recommendation") ?? ReadString(review, "recommendation_label", "RecommendationLabel") ?? "Mehr Evidenz empfohlen";
        var confidenceScore = ReadDouble(review, "confidence_score", "ConfidenceScore");
        var confidenceClass = ReadString(review, "confidence_class", "ConfidenceClass") ?? "low";
        var nextStep = ReadString(review, "next_evidence_step", "NextEvidenceStep") ?? "nächste Evidenz prüfen";
        var missingEvidence = ResolveMissingEvidence(reviewId, title, nextStep, assistant);
        var canAct = CanHermesAct(nextStep, domain, confidencePrioritization, confidenceEngine, assistant, loop, forwardSync, oosPlanning, runtimeHealth);
        var command = ResolveAutonomousCommand(nextStep, canAct, loop);
        var status = ResolveActionStatus(nextStep, canAct, confidenceScore, recommendation);
        var frankRequired = status is "frank_decision_required" or "blocked";

        return new ReviewActionPlanEntry(
            ReviewId: reviewId,
            Title: title,
            Domain: domain,
            CurrentRecommendation: recommendation,
            ConfidenceScore: confidenceScore,
            ConfidenceClass: confidenceClass,
            MissingEvidence: missingEvidence,
            NextEvidenceStep: nextStep,
            CanHermesActAutonomously: canAct,
            AutonomousCommand: command,
            FrankRequired: frankRequired,
            ActionStatus: status);
    }

    private static string ResolveActionStatus(string nextStep, bool canAct, double confidenceScore, string recommendation)
    {
        var step = nextStep.ToLowerInvariant();
        if (!canAct)
        {
            if (step.Contains("oos")) return "waiting_for_oos";
            if (step.Contains("forward")) return "waiting_for_forward";
            if (step.Contains("signal")) return "waiting_for_signal";
            if (step.Contains("validation")) return "waiting_for_forward";
            return "no_safe_action";
        }

        if (recommendation.Contains("Ablehnung", StringComparison.OrdinalIgnoreCase) || confidenceScore < 30)
        {
            return "frank_decision_required";
        }

        return "hermes_can_continue";
    }

    private static bool CanHermesAct(string nextStep, string domain, JsonElement? domainAware, JsonElement? confidenceEngine, JsonElement? assistant, JsonElement? loop, JsonElement? forwardSync, JsonElement? oosPlanning, JsonElement? runtimeHealth)
    {
        var step = nextStep.ToLowerInvariant();
        var openForwardPlans = ReadInt(loop, "open_forward_plans", "OpenForwardPlans");
        var openOosPlans = ReadInt(loop, "open_oos_plans", "OpenOosPlans");
        var forwardStatus = ReadString(forwardSync, "last_status", "LastStatus", "last_forward_status", "LastForwardStatus") ?? string.Empty;
        var mainStatus = ReadString(runtimeHealth, "main_status", "MainStatus") ?? "unknown";
        var safe = !mainStatus.Equals("fehler", StringComparison.OrdinalIgnoreCase);

        if (!safe)
        {
            return false;
        }

        if (step.Contains("forward"))
        {
            return true;
        }

        if (step.Contains("oos"))
        {
            return true;
        }

        if (step.Contains("signal"))
        {
            return true;
        }

        return domain == "trading" && openForwardPlans >= 0 && openOosPlans >= 0;
    }

    private static string ResolveAutonomousCommand(string nextStep, bool canAct, JsonElement? loop)
    {
        if (canAct)
        {
            return "autonomous-research-loop-step";
        }

        var step = nextStep.ToLowerInvariant();
        if (step.Contains("forward")) return "autonomous-forward-observation-gate";
        if (step.Contains("oos")) return "autonomous-oos-execution-gate";
        if (step.Contains("signal")) return "autonomous-forward-observation-gate";
        return "autonomous-research-loop-step";
    }

    private static IReadOnlyList<string> ResolveMissingEvidence(string reviewId, string title, string nextStep, JsonElement? assistant)
    {
        foreach (var entry in ReadArray(assistant, "entries", "Entries"))
        {
            var candidateId = ReadString(entry, "review_id", "ReviewId") ?? string.Empty;
            var candidateTitle = ReadString(entry, "title", "Title") ?? string.Empty;
            if (candidateId.Equals(reviewId, StringComparison.OrdinalIgnoreCase) || candidateTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                var list = ReadStringList(entry, "missing_evidence", "MissingEvidence");
                return list.Count > 0 ? list : ["Forward-Bestätigung", "OOS Validation"];
            }
        }

        var step = nextStep.ToLowerInvariant();
        return step.Contains("forward")
            ? ["Forward-Bestätigung"]
            : step.Contains("oos")
                ? ["OOS Validation"]
                : ["weitere Evidenz"];
    }

    private static IReadOnlyList<JsonElement> LoadTopTradingEntries(JsonElement? domainAware)
    {
        var result = new List<JsonElement>();
        foreach (var group in ReadArray(domainAware, "top_trading_decisions", "TopTradingDecisions"))
        {
            result.AddRange(ReadArray(group, "reviews", "Reviews"));
        }

        return result
            .Where(review => string.Equals(ReadString(review, "classified_domain", "ClassifiedDomain") ?? ReadString(review, "domain", "Domain"), "trading", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ReadString(review, "domain", "Domain"), "trading", StringComparison.OrdinalIgnoreCase)
                || (ReadString(review, "knowledge_item_id", "KnowledgeItemId") ?? string.Empty).StartsWith("trading:", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
    }

    private static string BuildOperatorSummary(IReadOnlyList<ReviewActionPlanEntry> entries)
    {
        var canAct = entries.Count(entry => entry.CanHermesActAutonomously);
        var frank = entries.Count(entry => entry.FrankRequired);
        return frank > 0
            ? $"Frank muss aktuell nicht entscheiden. Hermes kann bei {canAct} Trading-Reviews selbst weitere Evidenz sammeln. Nächster sicherer Schritt: autonomous-research-loop-step."
            : $"Frank muss aktuell nicht entscheiden. Hermes kann bei {canAct} Trading-Reviews selbst weitere Evidenz sammeln. Nächster sicherer Schritt: autonomous-research-loop-step.";
    }

    private void WriteArtifacts(ReviewActionPlanReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(ReviewActionPlanReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Review Action Plan");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        foreach (var entry in report.Entries)
        {
            sb.AppendLine($"## {entry.Title}");
            sb.AppendLine($"- Empfehlung: {entry.CurrentRecommendation}");
            sb.AppendLine($"- Fehlt: {string.Join(", ", entry.MissingEvidence)}");
            sb.AppendLine($"- Nächster Schritt: {entry.NextEvidenceStep}");
            sb.AppendLine($"- Hermes kann selbst handeln: {entry.CanHermesActAutonomously.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- Frank nötig: {entry.FrankRequired.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- Autonomer Command: {entry.AutonomousCommand}");
        }
        return sb.ToString();
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

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().ToList();
            }
        }

        return [];
    }

    private static string? ReadString(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
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
            if (element.Value.TryGetProperty(name, out var value))
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
            if (element.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
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
            if (element.Value.TryGetProperty(name, out var value))
            {
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
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

    private static int ReadInt(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (element.Value.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }
}
