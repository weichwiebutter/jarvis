using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationQueueRefillTask(
    string PlanId,
    string KnowledgeItemId,
    string Domain,
    string Status,
    IReadOnlyList<string> RequiredTaskIds,
    IReadOnlyList<string> Notes);

public sealed record ValidationQueueRefillReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int OpenPlans,
    int PlansWithQueuedTasks,
    int PlansSkipped,
    int TasksCreated,
    IReadOnlyList<string> Domains,
    IReadOnlyList<ValidationQueueRefillTask> CreatedTasks,
    IReadOnlyList<string> SkippedPlans,
    string NextAction,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string PlansPath,
    string QueuePath,
    string ReportPath,
    string MarkdownPath);

public sealed class ValidationQueueRefillService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ValidationQueueRefillService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "validation_queue_refill");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "validation_queue_refill.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "validation_queue_refill.md");

    public ValidationQueueRefillReport Refill(int maxPlans = 100)
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var strategy = new KnowledgeValidationStrategy(_storagePaths);
        var statusBefore = strategy.LoadStatus() ?? strategy.BuildStatus();
        var plans = strategy.LoadPlanReport() ?? strategy.GeneratePlans(Math.Clamp(maxPlans, 1, 500));
        var queueBefore = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var pendingBefore = queueBefore.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));

        ResearchQueue queueAfter;
        if (statusBefore.ValidationPlansOpen > 0)
        {
            strategy.ValidateKnowledge(Math.Clamp(maxPlans, 1, 500));
            queueAfter = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        }
        else
        {
            queueAfter = queueBefore;
        }
        var pendingAfter = queueAfter.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));

        var createdCount = Math.Max(0, pendingAfter - pendingBefore);
        var openPlans = Math.Max(statusBefore.ValidationPlansOpen, plans.OpenPlans);
        var domains = plans.Plans
            .Select(plan => plan.Domain)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var created = plans.Plans
            .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(plan => plan.Priority)
            .ThenBy(plan => plan.Domain, StringComparer.Ordinal)
            .ThenBy(plan => plan.KnowledgeItemId, StringComparer.Ordinal)
            .Take(createdCount)
            .Select(plan => new ValidationQueueRefillTask(
                PlanId: plan.PlanId,
                KnowledgeItemId: plan.KnowledgeItemId,
                Domain: plan.Domain,
                Status: "queued",
                RequiredTaskIds: plan.RequiredTasks.Select(task => task.TaskId).ToList(),
                Notes: plan.MissingEvidence))
            .ToList();
        var skipped = Math.Max(0, Math.Max(plans.OpenPlans, statusBefore.ValidationPlansOpen) - created.Count);

        var refill = new ValidationQueueRefillReport(
            ReportVersion: "validation_queue_refill_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            OpenPlans: openPlans,
            PlansWithQueuedTasks: pendingAfter,
            PlansSkipped: skipped,
            TasksCreated: createdCount,
            Domains: domains,
            CreatedTasks: created,
            SkippedPlans: created.Count == 0 ? plans.Plans.Select(plan => plan.PlanId).Take(20).ToList() : [],
            NextAction: createdCount == 0 ? "Keine neuen Tasks nötig; bestehende Validation Queue beobachten." : "Validation Tasks ausführen.",
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            PlansPath: strategy.PlansPath,
            QueuePath: new ResearchQueueService(_storagePaths).QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteTextWithFallback(reportPath, markdownPath, root, refill);
        return refill;
    }

    public ValidationQueueRefillReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationQueueRefillReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string BuildMarkdown(ValidationQueueRefillReport report)
    {
        var lines = new List<string>
        {
            "# Validation Queue Refill",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Offene Pläne: {report.OpenPlans}",
            $"- Pläne mit Queue-Arbeit: {report.PlansWithQueuedTasks}",
            $"- Neue Tasks: {report.TasksCreated}",
            $"- Übersprungene Pläne: {report.PlansSkipped}",
            $"- Domänen: {string.Join(", ", report.Domains)}",
            string.Empty,
            "## Nächste Aktion",
            $"- {report.NextAction}",
            string.Empty,
            "## Erzeugte Aufgaben",
        };

        lines.AddRange(report.CreatedTasks.Count == 0
            ? ["- keine"]
            : report.CreatedTasks.Select(task => $"- {task.Domain}: {task.PlanId} -> {string.Join(", ", task.RequiredTaskIds)}"));
        lines.Add(string.Empty);
        lines.Add("## Übersprungene Pläne");
        lines.AddRange(report.SkippedPlans.Count == 0 ? ["- keine"] : report.SkippedPlans.Select(planId => $"- {planId}"));
        return string.Join(Environment.NewLine, lines);
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        var primaryRoot = Root;
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return (Path.Combine(primaryRoot, "validation_queue_refill.json"), Path.Combine(primaryRoot, "validation_queue_refill.md"), primaryRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "validation_queue_refill");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "validation_queue_refill.json"), Path.Combine(fallbackRoot, "validation_queue_refill.md"), fallbackRoot);
        }
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, ValidationQueueRefillReport refill)
    {
        var json = JsonSerializer.Serialize(refill, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(refill);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(reportPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "validation_queue_refill");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "validation_queue_refill.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "validation_queue_refill.md"), markdown);
        }
    }
}
