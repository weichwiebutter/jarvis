using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeValidationAuditDomain(
    string Domain,
    int OpenPlans,
    int OpenQueueItems,
    int OpenKnowledgeItems,
    int OldestOpenValidationAgeDays);

public sealed record KnowledgeValidationAuditFinding(
    string Code,
    string Category,
    string Title,
    string Meaning,
    string Action,
    int Count,
    IReadOnlyList<string> Domains);

public sealed record KnowledgeValidationAuditTask(
    string Code,
    string Title,
    string Action,
    string Category,
    IReadOnlyList<string> Domains,
    int Count);

public sealed record KnowledgeValidationAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string ValidationCompletionLabel,
    double ValidationCompletionPercent,
    int TotalKnowledgeItems,
    int ValidatedKnowledgeItems,
    int KnowledgeItemsNeedingOosValidation,
    int KnowledgeItemsWithoutValidationQueue,
    int OpenValidations,
    int CriticalKnowledgeGaps,
    int QueueItemsOpen,
    int QueueItemsProcessed,
    bool ValidationQueueExists,
    bool ValidationQueueFilled,
    bool ValidationQueueProcessed,
    int OldestOpenValidationAgeDays,
    int HumanReviewPendingReviews,
    int HumanReviewNeedsMoreEvidenceReviews,
    int HumanReviewDeferredReviews,
    IReadOnlyList<string> HumanReviewNeedsMoreEvidenceDomains,
    IReadOnlyList<string> MissingAutomationJobs,
    IReadOnlyList<string> MissingQueues,
    IReadOnlyList<string> NextRecommendedCommands,
    string OperatorSummary,
    IReadOnlyList<string> AffectedDomains,
    IReadOnlyList<KnowledgeValidationAuditDomain> DomainBreakdown,
    IReadOnlyList<KnowledgeValidationAuditFinding> Findings,
    IReadOnlyList<KnowledgeValidationAuditTask> ImprovementTasks,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string PlansPath,
    string QueuePath,
    string StatusPath,
    string EvidencePath,
    string AuditPath,
    string MarkdownPath);

public sealed class KnowledgeValidationAuditService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string AuditRoot => Path.Combine(_storagePaths.Root, "reports", "knowledge_validation_audit");

    public string AuditPath => Path.Combine(AuditRoot, "knowledge_validation_audit.json");

    public string AuditMarkdownPath => Path.Combine(AuditRoot, "knowledge_validation_audit.md");

    public KnowledgeValidationAuditReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(AuditRoot);
        var humanReview = new HumanReviewWorkflow(_storagePaths).LoadOrCreateQueue();
        var validation = new KnowledgeValidationStrategy(_storagePaths).LoadStatus()
            ?? new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
        var plans = new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport()
            ?? new KnowledgeValidationStrategy(_storagePaths).GeneratePlans(50);
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
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
        var needsMoreEvidenceItems = humanReview.Items
            .Where(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var needsMoreEvidenceDomains = needsMoreEvidenceItems
            .Select(item => item.Domain)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingAutomationJobs = new List<string>();
        if (needsMoreEvidenceItems.Count > 0)
        {
            missingAutomationJobs.AddRange(["collect_evidence", "generate_validation_plans", "validate_knowledge_items", "execute_validation_tasks"]);
        }
        if (validation.KnowledgeItemsNeedingOos > 0)
        {
            missingAutomationJobs.Add("run_walkforward_validation");
        }
        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            missingAutomationJobs.AddRange(["validate_knowledge_items", "execute_validation_tasks"]);
        }
        var missingQueues = new List<string>();
        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            missingQueues.Add("validation_queue");
        }
        if (needsMoreEvidenceItems.Count > 0 && humanReview.PendingReviews == 0)
        {
            missingQueues.Add("evidence_queue");
        }
        var nextRecommendedCommands = new List<string>();
        if (needsMoreEvidenceItems.Count > 0)
        {
            nextRecommendedCommands.AddRange(["collect_evidence", "generate_validation_plans", "validate_knowledge_items", "execute_validation_tasks"]);
        }
        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            nextRecommendedCommands.AddRange(["validate_knowledge_items", "execute_validation_tasks"]);
        }
        if (validation.KnowledgeItemsNeedingOos > 0)
        {
            nextRecommendedCommands.Add("run_walkforward_validation");
        }
        var operatorSummary = humanReview.PendingReviews > 0
            ? "Frank muss im Prüfzentrum entscheiden."
            : needsMoreEvidenceItems.Count > 0
                ? "Hermes sammelt weitere Evidenz. Frank muss nichts tun."
                : "Keine Aktion erforderlich. Hermes arbeitet selbstständig weiter.";
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
        var validatedKnowledgeItems = quality.Items.Count(item =>
            item.ValidationScore >= 0.45
            || item.LastValidatedUtc is not null
            || item.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase));
        var knowledgeWithoutQueue = Math.Max(0, plans.OpenPlans - openQueueItems.Count);
        var oldestOpenAgeDays = plans.Plans.Count == 0
            ? 0
            : plans.Plans
                .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
                .Select(plan => Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - plan.CreatedAtUtc).TotalDays)))
                .DefaultIfEmpty(0)
                .Max();
        var affectedByDomain = quality.Items
            .Where(item => item.ValidationScore < 0.45 || item.LastValidatedUtc is null || !item.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var findings = BuildFindings(validation, plans, queue, quality, affectedByDomain, humanReview);
        var tasks = BuildTasks(findings, validation);
        var warnings = findings.Select(finding => finding.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var report = new KnowledgeValidationAuditReport(
            ReportVersion: "knowledge_validation_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ValidationCompletionLabel: $"{completionPercent:0}% abgeschlossen",
            ValidationCompletionPercent: completionPercent,
            TotalKnowledgeItems: quality.TotalKnowledgeItems,
            ValidatedKnowledgeItems: validatedKnowledgeItems,
            KnowledgeItemsNeedingOosValidation: criticalGaps,
            KnowledgeItemsWithoutValidationQueue: knowledgeWithoutQueue,
            OpenValidations: openPlans,
            CriticalKnowledgeGaps: criticalGaps,
            QueueItemsOpen: validation.ValidationTasksPending,
            QueueItemsProcessed: processedQueueItems,
            ValidationQueueExists: queueExists,
            ValidationQueueFilled: queueFilled,
            ValidationQueueProcessed: queueProcessed,
            OldestOpenValidationAgeDays: oldestOpenAgeDays,
            HumanReviewPendingReviews: humanReview.PendingReviews,
            HumanReviewNeedsMoreEvidenceReviews: humanReview.NeedsMoreEvidenceReviews,
            HumanReviewDeferredReviews: humanReview.DeferredReviews,
            HumanReviewNeedsMoreEvidenceDomains: needsMoreEvidenceDomains,
            MissingAutomationJobs: missingAutomationJobs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MissingQueues: missingQueues.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NextRecommendedCommands: nextRecommendedCommands.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: operatorSummary,
            AffectedDomains: affectedDomains,
            DomainBreakdown: domainBreakdown,
            Findings: findings,
            ImprovementTasks: tasks,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            PlansPath: new KnowledgeValidationStrategy(_storagePaths).PlansPath,
            QueuePath: new ResearchQueueService(_storagePaths).QueuePath,
            StatusPath: new KnowledgeValidationStrategy(_storagePaths).StatusPath,
            EvidencePath: new KnowledgeQualityEngine(_storagePaths).EvidencePath,
            AuditPath: AuditPath,
            MarkdownPath: AuditMarkdownPath);

        WriteAuditCopies(report);
        return report;
    }

    public KnowledgeValidationAuditReport? Load()
    {
        var candidates = new[]
        {
            AuditPath,
            Path.Combine(Root, "knowledge_validation_audit.json"),
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeValidationAuditReport>(
                File.ReadAllText(path),
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
            $"- Knowledge Items: {report.TotalKnowledgeItems}",
            $"- Validiert: {report.ValidatedKnowledgeItems}",
            $"- OOS nötig: {report.KnowledgeItemsNeedingOosValidation}",
            $"- Ohne Validation Queue: {report.KnowledgeItemsWithoutValidationQueue}",
            $"- Offene Validierungen: {report.OpenValidations}",
            $"- Kritische Wissenslücken: {report.CriticalKnowledgeGaps}",
            $"- Älteste offene Validierung: {report.OldestOpenValidationAgeDays} Tage",
            $"- Needs More Evidence: {report.HumanReviewNeedsMoreEvidenceReviews}",
            string.Empty,
            "## Domänen",
        };

        lines.AddRange(report.DomainBreakdown.Select(domain =>
            $"- {domain.Domain}: open_plans={domain.OpenPlans}, open_queue_items={domain.OpenQueueItems}, open_knowledge_items={domain.OpenKnowledgeItems}, oldest_age_days={domain.OldestOpenValidationAgeDays}"));
        lines.Add(string.Empty);
        lines.Add("## Warnungen");
        lines.AddRange(report.Warnings.Count == 0 ? ["- keine"] : report.Warnings.Select(warning => $"- {warning}"));
        lines.Add(string.Empty);
        lines.Add("## Evidenz- und Validierungsplan");
        lines.Add($"- Operator: {report.OperatorSummary}");
        lines.Add($"- Frank nötig: {(report.HumanReviewPendingReviews > 0 ? "ja" : "nein")}");
        lines.AddRange(report.MissingAutomationJobs.Count == 0 ? ["- fehlende Jobs: keine"] : report.MissingAutomationJobs.Select(job => $"- fehlender Job: {job}"));
        lines.AddRange(report.MissingQueues.Count == 0 ? ["- fehlende Queues: keine"] : report.MissingQueues.Select(queue => $"- fehlende Queue: {queue}"));
        lines.AddRange(report.NextRecommendedCommands.Count == 0 ? ["- nächste Commands: keine"] : report.NextRecommendedCommands.Select(command => $"- nächster Command: {command}"));
        lines.Add(string.Empty);
        lines.Add("## Maßnahmen");
        lines.AddRange(report.ImprovementTasks.Count == 0
            ? ["- keine"]
            : report.ImprovementTasks.Select(task => $"- {task.Title} [{task.Category}] ({string.Join(", ", task.Domains)})"));
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<KnowledgeValidationAuditFinding> BuildFindings(
        KnowledgeValidationStatus validation,
        KnowledgeValidationPlanReport plans,
        ResearchQueue queue,
        KnowledgeQualityReport quality,
        IReadOnlyDictionary<string, int> affectedByDomain,
        HumanReviewQueue humanReview)
    {
        var domains = affectedByDomain.Keys.OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase).ToList();
        var findings = new List<KnowledgeValidationAuditFinding>();

        if (validation.KnowledgeItemsNeedingOos > 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "oos_data_missing",
                Category: "needs_validation_run",
                Title: "OOS-Validierung planen",
                Meaning: "Ein Teil des Wissens hat noch keine ausreichende Out-of-Sample-Absicherung.",
                Action: "Weitere Walk-Forward-/OOS-Läufe einplanen.",
                Count: validation.KnowledgeItemsNeedingOos,
                Domains: domains));
        }

        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "knowledge_validation_queue_missing",
                Category: "configuration_missing",
                Title: "Validation Queue erzeugen oder reparieren",
                Meaning: "Es gibt offene Validierungspläne, aber keine passende Queue-Arbeit.",
                Action: "Knowledge Validation Queue prüfen und neu befüllen.",
                Count: validation.ValidationPlansOpen,
                Domains: domains));
        }

        if (plans.OpenPlans > 0 && queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)) == 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "hypotheses_without_validation_queue",
                Category: "needs_research",
                Title: "Hypothesen in Validierungswarteschlange überführen",
                Meaning: "Es existieren offene Hypothesen, aber keine offene Queue-Arbeit.",
                Action: "Hypothesen in die Validierungswarteschlange überführen.",
                Count: plans.OpenPlans,
                Domains: domains));
        }

        if (plans.OpenPlans > 0 && queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)) > 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "validation_queue_active",
                Category: "informational",
                Title: "Validierungswarteschlange aktiv",
                Meaning: "Hermes hat offene Validierungsarbeit in der Queue.",
                Action: "Keine direkte Aktion erforderlich.",
                Count: queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)),
                Domains: domains));
        }

        var needsMoreEvidenceItems = humanReview.Items
            .Where(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (needsMoreEvidenceItems.Count > 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "human_review_needs_more_evidence",
                Category: "informational",
                Title: "Hermes sammelt weitere Evidenz",
                Meaning: $"{needsMoreEvidenceItems.Count} Knowledge Items warten auf zusätzliche Evidenz und Validierung.",
                Action: "Keine Aktion für Frank. Hermes kann Validierungsläufe und Evidenzsammlung planen.",
                Count: needsMoreEvidenceItems.Count,
                Domains: needsMoreEvidenceItems
                    .Select(item => item.Domain)
                    .Where(domain => !string.IsNullOrWhiteSpace(domain))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
                    .ToList()));
        }

        if (quality.TrustedKnowledge == 0)
        {
            findings.Add(new KnowledgeValidationAuditFinding(
                Code: "no_trusted_knowledge",
                Category: "needs_human_review",
                Title: "Vertrauensstufe bleibt zurückhaltend",
                Meaning: "Es gibt noch kein ausreichend vertrauenswürdiges Wissensniveau für automatische Freigaben.",
                Action: "Frank muss nur prüfen, wenn eine Freigabe ansteht.",
                Count: quality.TotalKnowledgeItems,
                Domains: domains));
        }

        return findings
            .OrderByDescending(finding => finding.Count)
            .ThenBy(finding => finding.Code, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<KnowledgeValidationAuditTask> BuildTasks(
        IReadOnlyList<KnowledgeValidationAuditFinding> findings,
        KnowledgeValidationStatus validation)
    {
        var tasks = new List<KnowledgeValidationAuditTask>();

        foreach (var finding in findings)
        {
            var task = finding.Code switch
            {
                "oos_data_missing" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: finding.Title,
                    Action: finding.Action,
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                "knowledge_validation_queue_missing" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: finding.Title,
                    Action: finding.Action,
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                "hypotheses_without_validation_queue" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: finding.Title,
                    Action: finding.Action,
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                "validation_queue_active" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: finding.Title,
                    Action: finding.Action,
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                "human_review_needs_more_evidence" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: finding.Title,
                    Action: finding.Action,
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                "no_trusted_knowledge" => new KnowledgeValidationAuditTask(
                    Code: finding.Code,
                    Title: "Keine automatische Trusted-Promotion",
                    Action: "Keine automatische Trusted-Promotion ausführen. Nur in Review-/Validation-Pipeline bleiben.",
                    Category: finding.Category,
                    Domains: finding.Domains,
                    Count: finding.Count),
                _ => null
            };

            if (task is not null)
            {
                tasks.Add(task);
            }
        }

        if (validation.ValidationPlansOpen > 0 && validation.ValidationTasksPending == 0)
        {
            tasks.Add(new KnowledgeValidationAuditTask(
                Code: "validation_queue_missing",
                Title: "Validation Queue erzeugen oder reparieren",
                Action: "Queue befüllen oder Route korrigieren.",
                Category: "configuration_missing",
                Domains: findings.SelectMany(item => item.Domains).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Count: validation.ValidationPlansOpen));
        }

        return tasks
            .DistinctBy(task => task.Code, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private void WriteAuditCopies(KnowledgeValidationAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        TryWriteCopies(AuditRoot, AuditPath, AuditMarkdownPath, json, markdown);
        TryWriteCopies(Root, Path.Combine(Root, "knowledge_validation_audit.json"), Path.Combine(Root, "knowledge_validation_audit.md"), json, markdown);
    }

    private void TryWriteCopies(string root, string jsonPath, string markdownPath, string json, string markdown)
    {
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(jsonPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "knowledge_validation_audit");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_validation_audit.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_validation_audit.md"), markdown);
        }
    }
}
