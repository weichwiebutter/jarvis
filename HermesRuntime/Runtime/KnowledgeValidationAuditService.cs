using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeValidationAuditDomain(
    string Domain,
    int OpenPlans,
    int OpenQueueItems,
    int OpenKnowledgeItems,
    int OldestOpenValidationAgeDays);

public sealed record KnowledgeValidationAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string ValidationCompletionLabel,
    double ValidationCompletionPercent,
    int OpenValidations,
    int CriticalKnowledgeGaps,
    int QueueItemsOpen,
    int QueueItemsProcessed,
    bool ValidationQueueExists,
    bool ValidationQueueFilled,
    bool ValidationQueueProcessed,
    int OldestOpenValidationAgeDays,
    IReadOnlyList<string> AffectedDomains,
    IReadOnlyList<KnowledgeValidationAuditDomain> DomainBreakdown,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string PlansPath,
    string QueuePath,
    string StatusPath,
    string EvidencePath);

public sealed class KnowledgeValidationAuditService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string AuditPath => Path.Combine(Root, "knowledge_validation_audit.json");

    public string AuditMarkdownPath => Path.Combine(Root, "knowledge_validation_audit.md");

    public KnowledgeValidationAuditReport Run()
    {
        Directory.CreateDirectory(Root);
        var validation = new KnowledgeValidationStrategy(_storagePaths).LoadStatus()
            ?? new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
        var plans = new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport()
            ?? new KnowledgeValidationStrategy(_storagePaths).GeneratePlans(50);
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var validationQueueItems = queue.Items
            .Where(item => item.RequestedBy.Equals("knowledge_validation_strategy", StringComparison.OrdinalIgnoreCase)
                || item.Notes.Any(note => note.StartsWith("validation_plan:", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var openQueueItems = validationQueueItems
            .Where(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var processedQueueItems = validationQueueItems.Count - openQueueItems.Count;
        var queueExists = File.Exists(new ResearchQueueService(_storagePaths).QueuePath);
        var queueFilled = validationQueueItems.Count > 0;
        var queueProcessed = processedQueueItems > 0;
        var openPlans = validation.ValidationPlansOpen;
        var completionPercent = plans.TotalPlans == 0
            ? 0
            : Math.Round(Math.Clamp(1 - (validation.KnowledgeItemsNeedingOos / (double)Math.Max(1, plans.TotalPlans)), 0, 1) * 100, 0);
        var affectedDomains = openQueueItems
            .Select(item => item.Domain)
            .Concat(plans.Plans.Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).Select(plan => plan.Domain))
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var domainBreakdown = affectedDomains
            .Select(domain =>
            {
                var domainPlans = plans.Plans.Where(plan => plan.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
                var domainOpenPlans = domainPlans.Count(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
                var domainQueue = openQueueItems.Count(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
                var domainOpenKnowledge = domainPlans.Select(plan => plan.KnowledgeItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var oldest = OpenAgeDays(domain, openQueueItems, domainPlans);
                return new KnowledgeValidationAuditDomain(domain, domainOpenPlans, domainQueue, domainOpenKnowledge, oldest);
            })
            .OrderByDescending(item => item.OpenPlans)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ToList();
        var criticalGaps = validation.KnowledgeItemsNeedingOos;
        var oldestOpenAgeDays = plans.Plans.Count == 0
            ? 0
            : plans.Plans
                .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
                .Select(plan => Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - plan.CreatedAtUtc).TotalDays)))
                .DefaultIfEmpty(0)
                .Max();
        var warnings = new List<string>();
        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            warnings.Add("knowledge_validation_queue_missing");
        }

        if (validation.KnowledgeItemsNeedingOos > 0)
        {
            warnings.Add("knowledge_items_need_oos_validation");
        }

        if (plans.OpenPlans > 0 && openQueueItems.Count == 0)
        {
            warnings.Add("hypotheses_without_validation_queue");
        }

        var report = new KnowledgeValidationAuditReport(
            ReportVersion: "knowledge_validation_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ValidationCompletionLabel: $"{completionPercent:0}% abgeschlossen",
            ValidationCompletionPercent: completionPercent,
            OpenValidations: openPlans,
            CriticalKnowledgeGaps: criticalGaps,
            QueueItemsOpen: validation.ValidationTasksPending,
            QueueItemsProcessed: processedQueueItems,
            ValidationQueueExists: queueExists,
            ValidationQueueFilled: queueFilled,
            ValidationQueueProcessed: queueProcessed,
            OldestOpenValidationAgeDays: oldestOpenAgeDays,
            AffectedDomains: affectedDomains,
            DomainBreakdown: domainBreakdown,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            PlansPath: new KnowledgeValidationStrategy(_storagePaths).PlansPath,
            QueuePath: new ResearchQueueService(_storagePaths).QueuePath,
            StatusPath: new KnowledgeValidationStrategy(_storagePaths).StatusPath,
            EvidencePath: new KnowledgeQualityEngine(_storagePaths).EvidencePath);

        File.WriteAllText(AuditPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(AuditMarkdownPath, BuildMarkdown(report));
        return report;
    }

    public KnowledgeValidationAuditReport? Load()
    {
        if (!File.Exists(AuditPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeValidationAuditReport>(
                File.ReadAllText(AuditPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static int OpenAgeDays(string domain, IReadOnlyList<ResearchQueueItem> openQueueItems, IReadOnlyList<KnowledgeValidationPlan> plans)
    {
        var oldestQueue = openQueueItems.Where(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(item => item.CreatedAtUtc).DefaultIfEmpty().Min();
        var oldestPlan = plans.Where(plan => plan.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).Select(plan => plan.CreatedAtUtc).DefaultIfEmpty().Min();
        var candidates = new[] { oldestQueue, oldestPlan }
            .Where(timestamp => timestamp != default)
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var oldest = candidates.Min();
        return Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - oldest).TotalDays));
    }

    private static string BuildMarkdown(KnowledgeValidationAuditReport report)
    {
        var lines = new List<string>
        {
            "# Knowledge Validation Audit",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Validierung: {report.ValidationCompletionLabel}",
            $"- Offene Validierungen: {report.OpenValidations}",
            $"- Kritische Wissenslücken: {report.CriticalKnowledgeGaps}",
            $"- Älteste offene Validierung: {report.OldestOpenValidationAgeDays} Tage",
            string.Empty,
            "## Domänen",
        };

        lines.AddRange(report.DomainBreakdown.Select(domain =>
            $"- {domain.Domain}: open_plans={domain.OpenPlans}, open_queue_items={domain.OpenQueueItems}, open_knowledge_items={domain.OpenKnowledgeItems}, oldest_age_days={domain.OldestOpenValidationAgeDays}"));
        lines.Add(string.Empty);
        lines.Add("## Warnungen");
        lines.AddRange(report.Warnings.Count == 0 ? ["- keine"] : report.Warnings.Select(warning => $"- {warning}"));
        return string.Join(Environment.NewLine, lines);
    }
}
