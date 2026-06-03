using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeValidationExecutionResult(
    string ExecutionId,
    string QueueItemId,
    string TaskId,
    string PlanId,
    string RequirementId,
    string KnowledgeItemId,
    string Domain,
    string RequirementType,
    string Status,
    string OutcomeStatus,
    string EvidenceSummary,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeValidationEvidenceWriter
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationEvidenceWriter(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string EvidencePath => new KnowledgeQualityEngine(_storagePaths).EvidencePath;

    public void MergeExecutionEvidence(IReadOnlyList<KnowledgeValidationExecutionResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        var report = LoadEvidenceReport()
            ?? new KnowledgeEvidenceReport(
                ReportVersion: "knowledge_evidence_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Evidence: [],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        var byId = report.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in results.GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase))
        {
            var effectiveResults = group
                .Where(result => !result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
                    && !result.OutcomeStatus.Equals("validation_type_not_supported_for_domain", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (effectiveResults.Count == 0)
            {
                continue;
            }

            var existing = byId.GetValueOrDefault(group.Key);
            var first = effectiveResults.First();
            var validationRefs = effectiveResults
                .Select(result => $"validation:{result.ExecutionId}:{result.OutcomeStatus}")
                .Concat(effectiveResults.SelectMany(result => result.EvidenceRefs))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(80)
                .ToList();
            byId[group.Key] = existing is null
                ? new KnowledgeEvidenceEntry(
                    KnowledgeId: group.Key,
                    Domain: first.Domain,
                    SourceIds: [],
                    SourceEvidenceRefs: [],
                    ValidationEvidenceRefs: validationRefs,
                    OutcomeRefs: [],
                    GoalRefs: [],
                    QueueRefs: effectiveResults.Select(result => $"queue:{result.QueueItemId}:processed").Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    RelatedItems: [],
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    HumanReviewRequired: true)
                : existing with
                {
                    ValidationEvidenceRefs = existing.ValidationEvidenceRefs
                        .Concat(validationRefs)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(120)
                        .ToList(),
                    QueueRefs = existing.QueueRefs
                        .Concat(effectiveResults.Select(result => $"queue:{result.QueueItemId}:processed"))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(80)
                        .ToList(),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
        }

        var updated = report with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Evidence = byId.Values
                .OrderBy(item => item.Domain, StringComparer.Ordinal)
                .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
                .ToList()
        };
        File.WriteAllText(EvidencePath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
    }

    private KnowledgeEvidenceReport? LoadEvidenceReport()
    {
        if (!File.Exists(EvidencePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                File.ReadAllText(EvidencePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}

public sealed class KnowledgeValidationExecutor
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationExecutor(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ExecutionLogPath => Path.Combine(Root, "validation_execution.jsonl");

    public IReadOnlyList<KnowledgeValidationExecutionResult> Execute(int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 200);
        Directory.CreateDirectory(Root);
        var strategy = new KnowledgeValidationStrategy(_storagePaths);
        var report = strategy.LoadPlanReport() ?? strategy.GeneratePlans(Math.Max(maxItems, 50));
        var queueService = new ResearchQueueService(_storagePaths);
        var queue = queueService.LoadOrCreateQueue();
        var plansById = report.Plans.ToDictionary(plan => plan.PlanId, StringComparer.OrdinalIgnoreCase);
        var openItems = queue.Items
            .Where(IsOpenValidationQueueItem)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(maxItems)
            .ToList();
        var results = new List<KnowledgeValidationExecutionResult>();

        foreach (var item in openItems)
        {
            var result = ExecuteQueueItem(item, plansById);
            results.Add(result);
            File.AppendAllText(ExecutionLogPath, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions) + Environment.NewLine);
            queueService.MarkValidationTaskExecution(
                result.TaskId,
                result.Status,
                result.OutcomeStatus,
                result.EvidenceRefs,
                result.Warnings);
        }

        UpdatePlanReport(report, results);
        new KnowledgeQualityEngine(_storagePaths).Run();
        new KnowledgeValidationEvidenceWriter(_storagePaths).MergeExecutionEvidence(results);
        new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
        new GoalProgressTracker(_storagePaths).Update();
        new CognitiveCoreService(_storagePaths).BuildStatus();
        new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        return results;
    }

    public IReadOnlyList<KnowledgeValidationExecutionResult> ExecuteDomain(string domain, int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 200);
        Directory.CreateDirectory(Root);
        var normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "documentation" : domain.Trim().ToLowerInvariant();
        var strategy = new KnowledgeValidationStrategy(_storagePaths);
        var report = strategy.LoadPlanReport() ?? strategy.GeneratePlans(Math.Max(maxItems, 50));
        var router = new DomainValidationRouter(_storagePaths);
        var catalog = new KnowledgeCatalog(_storagePaths);
        var results = new List<KnowledgeValidationExecutionResult>();
        var selected = report.Plans
            .Where(plan => plan.Domain.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase))
            .SelectMany(plan => plan.Requirements
                .Where(requirement => !requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
                .Where(requirement => router.IsAllowed(plan.Domain, requirement.RequirementType))
                .Select(requirement => new
                {
                    Plan = plan,
                    Requirement = requirement,
                    Task = plan.RequiredTasks.FirstOrDefault(task =>
                        task.RequirementType.Equals(requirement.RequirementType, StringComparison.OrdinalIgnoreCase))
                }))
            .Where(item => item.Task is not null)
            .OrderByDescending(item => item.Requirement.Priority)
            .ThenBy(item => item.Plan.KnowledgeItemId, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        foreach (var item in selected)
        {
            var queueItem = new ResearchQueueItem(
                QueueItemId: $"domain_validation_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
                Domain: item.Plan.Domain,
                Queue: "validation",
                Type: item.Requirement.RequiredTaskType,
                Priority: ResearchPriority.High,
                Status: "open",
                SourceRefs: [item.Plan.KnowledgeItemId, item.Plan.PlanId, item.Requirement.RequirementId],
                RequestedBy: "domain_knowledge_validation",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: null,
                Notes:
                [
                    $"validation_plan:{item.Plan.PlanId}",
                    $"validation_task:{item.Task!.TaskId}",
                    $"knowledge_item:{item.Plan.KnowledgeItemId}",
                    $"requirement:{item.Requirement.RequirementType}",
                    $"domain_validation:{normalizedDomain}"
                ],
                NoTradingExecution: true,
                HumanReviewRequired: true);
            var result = ExecuteRequirement(queueItem, item.Plan, item.Requirement, item.Task!);
            results.Add(result);
            File.AppendAllText(ExecutionLogPath, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions) + Environment.NewLine);
        }

        UpdatePlanReport(report, results);
        new KnowledgeValidationEvidenceWriter(_storagePaths).MergeExecutionEvidence(results);
        new KnowledgeQualityEngine(_storagePaths).Run();
        new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
        new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        new GoalProgressTracker(_storagePaths).Update();
        new CognitiveCoreService(_storagePaths).BuildStatus();
        new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        return results;
    }

    public IReadOnlyList<KnowledgeValidationExecutionResult> LoadResults(int limit = 200)
    {
        if (!File.Exists(ExecutionLogPath))
        {
            return [];
        }

        var results = new List<KnowledgeValidationExecutionResult>();
        foreach (var line in File.ReadLines(ExecutionLogPath).Reverse())
        {
            if (results.Count >= Math.Clamp(limit, 1, 10000))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var result = JsonSerializer.Deserialize<KnowledgeValidationExecutionResult>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
            catch (JsonException)
            {
                // Append-only execution logs should keep later valid lines usable.
            }
        }

        return results;
    }

    private KnowledgeValidationExecutionResult ExecuteQueueItem(
        ResearchQueueItem item,
        IReadOnlyDictionary<string, KnowledgeValidationPlan> plansById)
    {
        var started = DateTimeOffset.UtcNow;
        var taskId = NoteValue(item, "validation_task") ?? item.QueueItemId;
        var planId = NoteValue(item, "validation_plan") ?? item.SourceRefs.FirstOrDefault(reference => reference.StartsWith("validation_plan_", StringComparison.OrdinalIgnoreCase)) ?? "";
        var knowledgeItemId = NoteValue(item, "knowledge_item")
            ?? item.SourceRefs.FirstOrDefault(reference => reference.Contains(':'))
            ?? "";
        var requirementType = NoteValue(item, "requirement") ?? "domain_review";
        var plan = !string.IsNullOrWhiteSpace(planId) && plansById.TryGetValue(planId, out var matchedPlan)
            ? matchedPlan
            : plansById.Values.FirstOrDefault(candidate => candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase));
        var requirement = plan?.Requirements.FirstOrDefault(candidate =>
            candidate.RequirementType.Equals(requirementType, StringComparison.OrdinalIgnoreCase));
        var task = plan?.RequiredTasks.FirstOrDefault(candidate =>
            candidate.TaskId.Equals(taskId, StringComparison.OrdinalIgnoreCase)
            || candidate.RequirementType.Equals(requirementType, StringComparison.OrdinalIgnoreCase));

        if (plan is null || requirement is null || task is null)
        {
            return BuildResult(
                item,
                taskId,
                planId,
                "",
                knowledgeItemId,
                item.Domain,
                requirementType,
                "skipped",
                "validation_plan_or_requirement_missing",
                "Validation queue item could not be matched to an existing validation plan.",
                [],
                [],
                ["validation_plan_or_requirement_missing"],
                started);
        }

        return ExecuteRequirement(item, plan, requirement, task, started);
    }

    private KnowledgeValidationExecutionResult ExecuteRequirement(
        ResearchQueueItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement,
        KnowledgeValidationTask task,
        DateTimeOffset? startedAtUtc = null)
    {
        var started = startedAtUtc ?? DateTimeOffset.UtcNow;
        var router = new DomainValidationRouter(_storagePaths);
        if (!router.IsAllowed(plan.Domain, requirement.RequirementType))
        {
            return BuildResult(
                item,
                task.TaskId,
                plan.PlanId,
                requirement.RequirementId,
                plan.KnowledgeItemId,
                plan.Domain,
                requirement.RequirementType,
                "skipped",
                "validation_type_not_supported_for_domain",
                $"Requirement '{requirement.RequirementType}' is not supported for domain '{plan.Domain}' and was skipped by DomainValidationRouter.",
                [],
                [],
                [$"validation_type_not_supported_for_domain:{plan.Domain}:{requirement.RequirementType}"],
                started);
        }

        return requirement.RequirementType switch
        {
            "source_verification" => ExecuteSourceVerification(item, plan, requirement, task, started),
            "cross_source_confirmation" => ExecuteCrossSourceConfirmation(item, plan, requirement, task, started),
            "historical_test" => ExecuteHistoricalTest(item, plan, requirement, task, started),
            "out_of_sample_test" => ExecuteWalkForwardLikeTest(item, plan, requirement, task, started, "out_of_sample_test"),
            "walkforward_test" => ExecuteWalkForwardLikeTest(item, plan, requirement, task, started, "walkforward_test"),
            "cost_stress_test" => ExecuteCostStressTest(item, plan, requirement, task, started),
            "monte_carlo_test" => ExecuteMonteCarloTest(item, plan, requirement, task, started),
            "domain_review" => ExecuteDomainReview(item, plan, requirement, task, started),
            "stale_check" => ExecuteStaleCheck(item, plan, requirement, task, started),
            "consistency_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "reference_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "static_analysis" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "test_presence_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "build_reference_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "process_owner_review_stub" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "citation_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            "reproducibility_check" => ExecuteDomainSpecificCheck(item, plan, requirement, task, started),
            _ => BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "skipped", "unsupported_requirement_type", "Unsupported validation requirement type.", [], [], [$"unsupported_requirement_type:{requirement.RequirementType}"], started)
        };
    }

    private KnowledgeValidationExecutionResult ExecuteSourceVerification(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var adapter = AdapterFor(plan.Domain);
        if (!plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            && adapter?.Supports(requirement.RequirementType) == true)
        {
            return ExecuteDomainSpecificCheck(item, plan, requirement, task, started);
        }

        var catalogItem = new KnowledgeCatalog(_storagePaths).FindById(plan.KnowledgeItemId);
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var matched = catalogItem?.SourceIds
            .Where(sourceId => sources.Any(source => source.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)))
            .Select(sourceId => $"source:{sourceId}")
            .ToList() ?? [];
        return matched.Count > 0
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "evidence_confirmed", $"Source verification completed; matched_sources={matched.Count}.", matched, [new KnowledgeSourceRegistry(_storagePaths).SourcesPath], [], started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "source_missing", "No matching source metadata is available; knowledge cannot become trusted.", [], [new KnowledgeSourceRegistry(_storagePaths).SourcesPath], ["source_metadata_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteCrossSourceConfirmation(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var catalogItem = new KnowledgeCatalog(_storagePaths).FindById(plan.KnowledgeItemId);
        var sourceRefs = catalogItem?.SourceIds.Select(sourceId => $"source:{sourceId}").Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        return sourceRefs.Count >= 2
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "cross_source_confirmed", $"Cross-source confirmation available; source_count={sourceRefs.Count}.", sourceRefs, [new KnowledgeCatalog(_storagePaths).CatalogPath], [], started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "second_source_missing", "Only one or no source is available; cross-source confirmation remains open.", sourceRefs, [new KnowledgeCatalog(_storagePaths).CatalogPath], ["second_independent_source_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteHistoricalTest(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        if (!plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "historical_test_not_supported_for_domain_yet", "Non-trading historical validation is not implemented yet; domain review remains required.", [], [new KnowledgeCatalog(_storagePaths).CatalogPath], ["non_trading_historical_test_not_available"], started);
        }

        var service = new StrategyResearchService(_storagePaths);
        var memory = service.LoadOrCreateMemory();
        return memory.VariantsTested > 0
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "historical_report_available", $"Strategy research memory available; variants_tested={memory.VariantsTested}.", [$"validation_report:{service.MemoryPath}", $"strategy_variants_tested:{memory.VariantsTested}"], [service.MemoryPath], memory.Warnings, started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "historical_report_missing", "No strategy research memory exists; do not trust trading knowledge.", [], [service.MemoryPath], ["strategy_research_memory_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteWalkForwardLikeTest(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started, string mode)
    {
        if (!plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "walkforward_not_supported_for_domain", "Walk-forward/OOS validation is currently only mapped for trading knowledge.", [], [], ["non_trading_walkforward_not_available"], started);
        }

        var service = new WalkForwardValidationService(_storagePaths);
        var report = service.LoadReport();
        if (report is null || report.Assessments.Count == 0)
        {
            return BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", $"{mode}_report_missing", "Walk-forward/OOS report is missing; item remains needs_more_data.", [], [service.WalkForwardPath], ["walkforward_report_missing"], started);
        }

        var oosCount = report.Assessments.Count(assessment => assessment.OosAvailable);
        return oosCount > 0
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", $"{mode}_report_available", $"Walk-forward report available; strategies={report.StrategiesEvaluated}; oos_available={oosCount}.", [$"validation_report:{service.WalkForwardPath}", $"oos_available:{oosCount}"], [service.WalkForwardPath, service.WalkForwardSummaryPath], [], started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "oos_data_missing", "Walk-forward report exists but has no OOS evidence; item remains needs_more_data.", [$"validation_report:{service.WalkForwardPath}"], [service.WalkForwardPath], ["oos_data_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteCostStressTest(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var service = new CostStressTestService(_storagePaths);
        var report = service.LoadReport();
        return report is not null && report.StrategiesEvaluated > 0
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "cost_stress_report_available", $"Cost stress report available; evaluated={report.StrategiesEvaluated}; stress_survivors={report.SurvivesStressCost}.", [$"validation_report:{service.ReportPath}", $"cost_stress_evaluated:{report.StrategiesEvaluated}"], [service.ReportPath], [], started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "cost_stress_report_missing", "Cost stress report missing; item remains needs_more_data.", [], [service.ReportPath], ["cost_stress_report_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteMonteCarloTest(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var service = new MonteCarloSimulationService(_storagePaths);
        var report = service.LoadReport();
        return report is not null && report.StrategiesEvaluated > 0
            ? BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "monte_carlo_report_available", $"Monte-Carlo report available; evaluated={report.StrategiesEvaluated}; passed={report.Passed}.", [$"validation_report:{service.ReportPath}", $"monte_carlo_evaluated:{report.StrategiesEvaluated}"], [service.ReportPath], [], started)
            : BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "needs_more_data", "monte_carlo_report_missing", "Monte-Carlo report missing; item remains needs_more_data.", [], [service.ReportPath], ["monte_carlo_report_missing"], started);
    }

    private KnowledgeValidationExecutionResult ExecuteDomainReview(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var adapter = AdapterFor(plan.Domain);
        if (!plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            && adapter?.Supports(requirement.RequirementType) == true)
        {
            return ExecuteDomainSpecificCheck(item, plan, requirement, task, started);
        }

        var service = new DomainCognitiveService(_storagePaths);
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);
        return BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, "completed", "structured_domain_review_stub", $"Structured domain review evidence written; active_domains={status.ActiveDomains.Count}; insights={insights.Insights.Count}.", [$"domain_review:{plan.Domain}", $"domain_insights:{service.DomainInsightsPath}"], [service.DomainStatusPath, service.DomainInsightsPath], [], started);
    }

    private KnowledgeValidationExecutionResult ExecuteDomainSpecificCheck(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var catalogItem = new KnowledgeCatalog(_storagePaths).FindById(plan.KnowledgeItemId);
        var adapter = AdapterFor(plan.Domain);
        if (catalogItem is null || adapter is null || !adapter.Supports(requirement.RequirementType))
        {
            return BuildResult(
                item,
                task.TaskId,
                plan.PlanId,
                requirement.RequirementId,
                plan.KnowledgeItemId,
                plan.Domain,
                requirement.RequirementType,
                "needs_more_data",
                "domain_validation_metadata_missing",
                "Domain-specific validation requires a catalog item and supported adapter.",
                [],
                [new KnowledgeCatalog(_storagePaths).CatalogPath],
                ["domain_validation_metadata_missing"],
                started);
        }

        var result = adapter.Validate(catalogItem, plan, requirement);
        var status = result.ValidationStatus.Equals("validated", StringComparison.OrdinalIgnoreCase)
            ? "completed"
            : result.Recommendation.Equals("reject", StringComparison.OrdinalIgnoreCase)
                ? "failed"
                : "needs_more_data";
        return BuildResult(
            item,
            task.TaskId,
            plan.PlanId,
            requirement.RequirementId,
            plan.KnowledgeItemId,
            plan.Domain,
            requirement.RequirementType,
            status,
            $"domain_validation_{requirement.RequirementType}_{result.ValidationStatus}",
            result.Summary,
            result.EvidenceRefs,
            result.OutputPaths,
            result.Warnings.Concat(result.MissingEvidence.Select(missing => $"missing_evidence:{missing}")).ToList(),
            started);
    }

    private KnowledgeValidationExecutionResult ExecuteStaleCheck(ResearchQueueItem item, KnowledgeValidationPlan plan, KnowledgeValidationRequirement requirement, KnowledgeValidationTask task, DateTimeOffset started)
    {
        var adapter = AdapterFor(plan.Domain);
        if (!plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            && adapter?.Supports(requirement.RequirementType) == true)
        {
            return ExecuteDomainSpecificCheck(item, plan, requirement, task, started);
        }

        var catalogItem = new KnowledgeCatalog(_storagePaths).FindById(plan.KnowledgeItemId);
        var outcome = catalogItem?.LastValidatedUtc is null
            ? "last_validated_missing"
            : DateTimeOffset.UtcNow - catalogItem.LastValidatedUtc.Value > TimeSpan.FromDays(180)
                ? "stale_validation_detected"
                : "fresh_validation_timestamp_available";
        var status = outcome == "fresh_validation_timestamp_available" ? "completed" : "needs_more_data";
        var warnings = status == "completed" ? Array.Empty<string>() : [$"{outcome}_requires_validation_refresh"];
        return BuildResult(item, task.TaskId, plan.PlanId, requirement.RequirementId, plan.KnowledgeItemId, plan.Domain, requirement.RequirementType, status, outcome, $"Stale check completed; outcome={outcome}.", [$"stale_check:{outcome}"], [new KnowledgeCatalog(_storagePaths).CatalogPath], warnings, started);
    }

    private IDomainKnowledgeValidationAdapter? AdapterFor(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "documentation" => new DocumentationValidationAdapter(_storagePaths),
            "software" => new SoftwareValidationAdapter(_storagePaths),
            "process" => new ProcessValidationAdapter(_storagePaths),
            "research" => new ResearchValidationAdapter(_storagePaths),
            _ => null
        };

    private void UpdatePlanReport(KnowledgeValidationPlanReport report, IReadOnlyList<KnowledgeValidationExecutionResult> results)
    {
        if (results.Count == 0)
        {
            new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
            return;
        }

        var byRequirement = results
            .GroupBy(result => $"{result.PlanId}|{result.RequirementId}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var byTask = results
            .GroupBy(result => $"{result.PlanId}|{result.TaskId}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var plans = report.Plans
            .Select(plan =>
            {
                var requirements = plan.Requirements
                    .Select(requirement =>
                    {
                        if (!byRequirement.TryGetValue($"{plan.PlanId}|{requirement.RequirementId}", out var result))
                        {
                            return requirement;
                        }

                        return requirement with
                        {
                            Status = RequirementStatusFor(result),
                            EvidenceRefs = requirement.EvidenceRefs
                                .Concat(result.EvidenceRefs)
                                .Concat([$"validation:{result.ExecutionId}:{result.OutcomeStatus}"])
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(40)
                                .ToList()
                        };
                    })
                    .ToList();
                var tasks = plan.RequiredTasks
                    .Select(task =>
                    {
                        if (!byTask.TryGetValue($"{plan.PlanId}|{task.TaskId}", out var result))
                        {
                            return task;
                        }

                        return task with { Status = result.Status };
                    })
                    .ToList();
                var status = PlanStatusFor(requirements);
                return plan with
                {
                    Requirements = requirements,
                    RequiredTasks = tasks,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Status = status
                };
            })
            .ToList();
        var updated = report with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            OpenPlans = plans.Count(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
                || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)),
            TrustedCandidateCount = plans.Count(plan => plan.TargetStatus.Equals("trusted_candidate", StringComparison.OrdinalIgnoreCase)
                && plan.Requirements.All(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))),
            KnowledgeItemsNeedingOos = plans.Count(plan => plan.Requirements.Any(requirement =>
                (requirement.RequirementType is "out_of_sample_test" or "walkforward_test")
                && !requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))),
            KnowledgeItemsNeedingSourceCheck = plans.Count(plan => plan.Requirements.Any(requirement =>
                (requirement.RequirementType is "source_verification" or "cross_source_confirmation")
                && !requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))),
            Plans = plans,
            MostCommonMissingEvidence = plans
                .Where(plan => !plan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase))
                .SelectMany(plan => plan.MissingEvidence)
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(group => $"{group.Key}:{group.Count()}")
                .ToList()
        };
        File.WriteAllText(new KnowledgeValidationStrategy(_storagePaths).PlansPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        var requirementsReport = new KnowledgeValidationRequirementsReport(
            ReportVersion: "knowledge_validation_requirements_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalRequirements: plans.SelectMany(plan => plan.Requirements).Count(),
            Requirements: plans.SelectMany(plan => plan.Requirements).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(new KnowledgeValidationStrategy(_storagePaths).RequirementsPath, JsonSerializer.Serialize(requirementsReport, JsonDefaults.WriteOptions));
    }

    private static string RequirementStatusFor(KnowledgeValidationExecutionResult result) =>
        result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            ? "satisfied"
            : result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)
                ? "needs_more_data"
                : result.Status;

    private static string PlanStatusFor(IReadOnlyList<KnowledgeValidationRequirement> requirements)
    {
        if (requirements.Count == 0 || requirements.All(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase)))
        {
            return "ready_for_quality_review";
        }

        if (requirements.Any(requirement => requirement.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)))
        {
            return "needs_more_data";
        }

        return requirements.Any(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
            ? "in_progress"
            : "open";
    }

    private static bool IsOpenValidationQueueItem(ResearchQueueItem item) =>
        item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
        && (item.RequestedBy.Equals("knowledge_validation_strategy", StringComparison.OrdinalIgnoreCase)
            || item.Notes.Any(note => note.StartsWith("validation_task:", StringComparison.OrdinalIgnoreCase)));

    private static string? NoteValue(ResearchQueueItem item, string key)
    {
        var prefix = $"{key}:";
        return item.Notes
            .FirstOrDefault(note => note.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ?[prefix.Length..];
    }

    private static KnowledgeValidationExecutionResult BuildResult(
        ResearchQueueItem item,
        string taskId,
        string planId,
        string requirementId,
        string knowledgeItemId,
        string domain,
        string requirementType,
        string status,
        string outcomeStatus,
        string evidenceSummary,
        IReadOnlyList<string> evidenceRefs,
        IReadOnlyList<string> outputPaths,
        IReadOnlyList<string> warnings,
        DateTimeOffset started) =>
        new(
            ExecutionId: $"validation_execution_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            QueueItemId: item.QueueItemId,
            TaskId: taskId,
            PlanId: planId,
            RequirementId: requirementId,
            KnowledgeItemId: knowledgeItemId,
            Domain: domain,
            RequirementType: requirementType,
            Status: status,
            OutcomeStatus: outcomeStatus,
            EvidenceSummary: evidenceSummary,
            EvidenceRefs: evidenceRefs
                .Concat([$"queue:{item.QueueItemId}:processed", $"requirement:{requirementType}:{outcomeStatus}"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(60)
                .ToList(),
            OutputPaths: outputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            StartedAtUtc: started,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
}
