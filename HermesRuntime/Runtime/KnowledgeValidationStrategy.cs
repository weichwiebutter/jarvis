using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeValidationRequirement(
    string RequirementId,
    string KnowledgeItemId,
    string RequirementType,
    string Status,
    string Reason,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> EvidenceRefs,
    string RequiredTaskType,
    string MappedInternalTaskType,
    double Priority,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record KnowledgeValidationTask(
    string TaskId,
    string KnowledgeItemId,
    string TaskType,
    string Domain,
    string RequirementType,
    string Status,
    double Priority,
    string ExpectedEvidence,
    string MappedInternalTaskType,
    IReadOnlyList<string> SourceRefs,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record KnowledgeValidationPlan(
    string PlanId,
    string KnowledgeItemId,
    string Domain,
    string Title,
    string CurrentStatus,
    string TargetStatus,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<KnowledgeValidationRequirement> Requirements,
    IReadOnlyList<KnowledgeValidationTask> RequiredTasks,
    double Priority,
    double ExpectedQualityDelta,
    string RelatedGoalId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    IReadOnlyList<string>? SkippedByRouterReasons = null);

public sealed record KnowledgeValidationPlanReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalPlans,
    int OpenPlans,
    int TrustedCandidateCount,
    int KnowledgeItemsNeedingOos,
    int KnowledgeItemsNeedingSourceCheck,
    IReadOnlyList<KnowledgeValidationPlan> Plans,
    IReadOnlyList<string> MostCommonMissingEvidence,
    IReadOnlyList<string> Warnings,
    string RequirementsPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record KnowledgeValidationRequirementsReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalRequirements,
    IReadOnlyList<KnowledgeValidationRequirement> Requirements,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record KnowledgeValidationStatus(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    int ValidationPlansOpen,
    int ValidationTasksPending,
    int TrustedCandidateCount,
    int KnowledgeItemsNeedingOos,
    int KnowledgeItemsNeedingSourceCheck,
    int QueueValidationTasks,
    string PlansPath,
    string RequirementsPath,
    string ResearchQueuePath,
    IReadOnlyList<string> MostCommonMissingEvidence,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    int InvalidValidationTasks = 0,
    int ValidationTasksCleaned = 0,
    string ValidationRoutingHealth = "unknown");

public sealed class KnowledgeValidationStrategy
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationStrategy(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string PlansPath => Path.Combine(Root, "validation_plans.json");

    public string RequirementsPath => Path.Combine(Root, "validation_requirements.json");

    public string StatusPath => Path.Combine(Root, "knowledge_validation_status.json");

    public KnowledgeValidationPlanReport GeneratePlans(int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 500);
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var catalog = new KnowledgeCatalog(_storagePaths)
            .LoadOrCreateItems()
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var goalState = new GoalProgressTracker(_storagePaths).LoadOrCreateState();
        var walkForward = new WalkForwardValidationService(_storagePaths).LoadReport();
        var costStress = new CostStressTestService(_storagePaths).LoadReport();
        var monteCarlo = new MonteCarloSimulationService(_storagePaths).LoadReport();
        var oosAvailable = walkForward?.Assessments.Any(item => item.OosAvailable) == true;
        var existingPlans = LoadPlanReport()?.Plans ?? [];
        var existingById = existingPlans.ToDictionary(plan => plan.PlanId, StringComparer.OrdinalIgnoreCase);

        var candidates = quality.Items
            .Where(item => item.LifecycleStatus is "untested" or "experimental" or "promising" or "rejected"
                || item.QualityScore < 0.72
                || item.ValidationScore < 0.68)
            .OrderByDescending(item => 1 - item.QualityScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        var plans = candidates
            .Select(item => BuildPlan(
                item,
                catalog.GetValueOrDefault(item.KnowledgeId),
                goalState.Goals,
                oosAvailable,
                costStress is not null,
                monteCarlo is not null,
                now,
                existingById))
            .OrderByDescending(plan => plan.Priority)
            .ThenBy(plan => plan.KnowledgeItemId, StringComparer.Ordinal)
            .ToList();
        var requirements = plans.SelectMany(plan => plan.Requirements).ToList();
        var report = new KnowledgeValidationPlanReport(
            ReportVersion: "knowledge_validation_plans_v1",
            UpdatedAtUtc: now,
            TotalPlans: plans.Count,
            OpenPlans: plans.Count(IsOpenPlan),
            TrustedCandidateCount: plans.Count(plan => plan.TargetStatus.Equals("trusted_candidate", StringComparison.OrdinalIgnoreCase)),
            KnowledgeItemsNeedingOos: plans.Count(plan => plan.Requirements.Any(requirement =>
                requirement.RequirementType is "out_of_sample_test" or "walkforward_test")),
            KnowledgeItemsNeedingSourceCheck: plans.Count(plan => plan.Requirements.Any(requirement =>
                requirement.RequirementType is "source_verification" or "cross_source_confirmation")),
            Plans: plans,
            MostCommonMissingEvidence: CommonMissingEvidence(plans),
            Warnings: plans.Count == 0 ? ["no_weak_knowledge_items_selected_for_validation"] : [],
            RequirementsPath: RequirementsPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var requirementsReport = new KnowledgeValidationRequirementsReport(
            ReportVersion: "knowledge_validation_requirements_v1",
            UpdatedAtUtc: now,
            TotalRequirements: requirements.Count,
            Requirements: requirements,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(RequirementsPath, JsonSerializer.Serialize(requirementsReport, JsonDefaults.WriteOptions));
        File.WriteAllText(PlansPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        WriteStatus(report, new ResearchQueueService(_storagePaths).LoadOrCreateQueue());
        return report;
    }

    public KnowledgeValidationStatus ValidateKnowledge(int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 500);
        var report = LoadPlanReport();
        if (report is null || report.OpenPlans == 0)
        {
            report = GeneratePlans(maxItems);
        }

        var selected = report.Plans
            .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(plan => plan.Priority)
            .Take(maxItems)
            .ToList();
        var queue = new ResearchQueueService(_storagePaths).EnqueueValidationPlans(selected, maxItems * 4);
        return WriteStatus(report, queue);
    }

    public KnowledgeValidationPlanReport? LoadPlanReport()
    {
        if (!File.Exists(PlansPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeValidationPlanReport>(
                File.ReadAllText(PlansPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public KnowledgeValidationStatus BuildStatus()
    {
        var report = LoadPlanReport() ?? GeneratePlans(50);
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        return WriteStatus(report, queue);
    }

    public KnowledgeValidationStatus? LoadStatus()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeValidationStatus>(
                File.ReadAllText(StatusPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public KnowledgeValidationPlan? FindPlan(string knowledgeItemId)
    {
        var report = LoadPlanReport() ?? GeneratePlans(50);
        return report.Plans.FirstOrDefault(plan =>
            plan.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)
            || plan.PlanId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase));
    }

    private KnowledgeValidationPlan BuildPlan(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem? catalogItem,
        IReadOnlyList<HermesGoal> goals,
        bool oosAvailable,
        bool costStressAvailable,
        bool monteCarloAvailable,
        DateTimeOffset now,
        IReadOnlyDictionary<string, KnowledgeValidationPlan> existingPlans)
    {
        var goal = RelatedGoalFor(qualityItem, goals);
        var planId = StableId("validation_plan", qualityItem.KnowledgeId);
        var existing = existingPlans.GetValueOrDefault(planId);
        var existingRequirements = existing?.Requirements.ToDictionary(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeValidationRequirement>(StringComparer.OrdinalIgnoreCase);
        var existingTasks = existing?.RequiredTasks.ToDictionary(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeValidationTask>(StringComparer.OrdinalIgnoreCase);
        var router = new DomainValidationRouter(_storagePaths);
        var rawRequirements = BuildRequirements(qualityItem, catalogItem, oosAvailable, costStressAvailable, monteCarloAvailable);
        var route = router.Route(qualityItem.Domain, rawRequirements);
        var routerReasons = route.SkippedByRouterReasons
            .Concat(RouterReplacementReasons(qualityItem))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var requirements = route.AllowedRequirements
            .Select(requirement =>
            {
                if (!existingRequirements.TryGetValue(requirement.RequirementId, out var prior))
                {
                    return requirement;
                }

                return requirement with
                {
                    Status = prior.Status,
                    EvidenceRefs = prior.EvidenceRefs
                        .Concat(requirement.EvidenceRefs)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(40)
                        .ToList()
                };
            })
            .ToList();
        var tasks = requirements
            .Select(requirement =>
            {
                var taskId = StableId("validation_task", qualityItem.KnowledgeId, requirement.RequirementType, requirement.RequiredTaskType);
                return new KnowledgeValidationTask(
                    TaskId: taskId,
                    KnowledgeItemId: qualityItem.KnowledgeId,
                    TaskType: requirement.RequiredTaskType,
                    Domain: qualityItem.Domain,
                    RequirementType: requirement.RequirementType,
                    Status: existingTasks.TryGetValue(taskId, out var priorTask) ? priorTask.Status : "planned",
                    Priority: requirement.Priority,
                    ExpectedEvidence: requirement.Reason,
                    MappedInternalTaskType: requirement.MappedInternalTaskType,
                    SourceRefs: [qualityItem.KnowledgeId, requirement.RequirementId],
                    NoTradingExecution: true,
                    HumanReviewRequired: true);
            })
            .GroupBy(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(task => task.Priority)
            .ToList();
        var missing = requirements.SelectMany(requirement => requirement.MissingEvidence).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var qualityGap = Math.Clamp(0.82 - qualityItem.QualityScore, 0, 1);
        var goalPriority = goal is null ? 0.35 : Math.Clamp(1 - ((goal.Priority - 1) / 100.0), 0.05, 1);
        var requirementWeight = Math.Clamp(requirements.Count / 8.0, 0, 1);
        var priority = Math.Round(Math.Clamp(qualityGap * 0.45 + goalPriority * 0.25 + requirementWeight * 0.2 - CostPenalty(qualityItem, requirements), 0, 1), 4);
        var expectedDelta = Math.Round(Math.Clamp(qualityGap * 0.55 + Math.Min(0.2, requirements.Count * 0.025), 0.03, 0.5), 4);
        return new KnowledgeValidationPlan(
            PlanId: planId,
            KnowledgeItemId: qualityItem.KnowledgeId,
            Domain: qualityItem.Domain,
            Title: qualityItem.Title,
            CurrentStatus: qualityItem.LifecycleStatus,
            TargetStatus: TargetStatusFor(qualityItem),
            MissingEvidence: missing,
            Requirements: requirements,
            RequiredTasks: tasks,
            Priority: priority,
            ExpectedQualityDelta: expectedDelta,
            RelatedGoalId: goal?.GoalId ?? "improve_knowledge_quality",
            CreatedAtUtc: existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc: now,
            Status: PlanStatusFor(requirements),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            SkippedByRouterReasons: routerReasons);
    }

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

    private IReadOnlyList<KnowledgeValidationRequirement> BuildRequirements(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem? catalogItem,
        bool oosAvailable,
        bool costStressAvailable,
        bool monteCarloAvailable)
    {
        var requirements = new List<KnowledgeValidationRequirement>();
        var sourceCount = catalogItem?.SourceIds.Count ?? 0;
        if (sourceCount == 0)
        {
            requirements.Add(Requirement(qualityItem, "source_verification", "Knowledge Item hat keine Quelle.", ["source_reference"], "collect_missing_evidence", "scan_knowledge_sources", 0.82));
        }
        else if (sourceCount < 2)
        {
            requirements.Add(Requirement(qualityItem, "cross_source_confirmation", "Nur eine Quelle vorhanden; Cross-Source-Bestaetigung fehlt.", ["second_independent_source"], "run_cross_source_check", "scan_knowledge_sources", 0.62));
        }

        if (qualityItem.EvidenceScore < 0.65)
        {
            requirements.Add(Requirement(qualityItem, "domain_review", "Evidence Score ist niedrig; strukturierte Domain-Pruefung erforderlich.", ["domain_review_evidence"], "run_domain_review", "generate_domain_insights", 0.58));
        }

        if (qualityItem.LastValidatedUtc is null || qualityItem.AgeScore < 0.65)
        {
            requirements.Add(Requirement(qualityItem, "stale_check", "Knowledge Item besitzt keinen aktuellen Validierungszeitpunkt.", ["fresh_validation_timestamp"], "run_domain_review", "generate_domain_insights", 0.42));
        }

        if (qualityItem.ValidationScore < 0.68)
        {
            var primaryRequirement = PrimaryValidationRequirementType(qualityItem.Domain);
            requirements.Add(Requirement(
                qualityItem,
                primaryRequirement,
                $"Validation Score reicht nicht fuer robust/trusted; Domain-Router plant {primaryRequirement}.",
                MissingEvidenceFor(primaryRequirement),
                RequiredTaskType(qualityItem.Domain, primaryRequirement),
                MappedInternalTask(qualityItem.Domain, primaryRequirement),
                0.72));
        }

        if (qualityItem.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            requirements.Add(Requirement(qualityItem, "out_of_sample_test", oosAvailable ? "OOS-Nachweis muss auf das Knowledge Item gemappt werden." : "OOS-Daten oder OOS-Report fehlen; nicht trusted setzen.", ["oos_validation_result"], "run_oos_validation", "run_walkforward_validation", oosAvailable ? 0.76 : 0.9));
            requirements.Add(Requirement(qualityItem, "walkforward_test", "Walk-Forward-Validierung erforderlich, bevor Trading-Wissen trusted wird.", ["walkforward_validation_result"], "run_oos_validation", "run_walkforward_validation", 0.82));
            requirements.Add(Requirement(qualityItem, "cost_stress_test", costStressAvailable ? "Cost-Stress muss dem Knowledge Item zugeordnet werden." : "Cost-Stress Report fehlt.", ["cost_stress_result"], "validate_knowledge_item", "cost-stress-report", costStressAvailable ? 0.68 : 0.78));
            requirements.Add(Requirement(qualityItem, "monte_carlo_test", monteCarloAvailable ? "Monte-Carlo Ergebnis muss dem Knowledge Item zugeordnet werden." : "Monte-Carlo Report fehlt.", ["monte_carlo_result"], "validate_knowledge_item", "monte-carlo-report", monteCarloAvailable ? 0.68 : 0.78));
        }

        return requirements
            .GroupBy(requirement => requirement.RequirementType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(requirement => requirement.Priority).First())
            .OrderByDescending(requirement => requirement.Priority)
            .ToList();
    }

    private static string PrimaryValidationRequirementType(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => "historical_test",
            "documentation" => "consistency_check",
            "software" => "static_analysis",
            "process" => "process_owner_review_stub",
            "research" => "reproducibility_check",
            _ => "domain_review"
        };

    private static IReadOnlyList<string> MissingEvidenceFor(string requirementType) =>
        requirementType switch
        {
            "historical_test" => ["historical_validation_result"],
            "consistency_check" => ["consistency_review_result"],
            "reference_check" => ["reference_check_result"],
            "static_analysis" => ["static_analysis_result"],
            "test_presence_check" => ["test_presence_result"],
            "build_reference_check" => ["build_reference_result"],
            "process_owner_review_stub" => ["process_owner_review_result"],
            "citation_check" => ["citation_check_result"],
            "reproducibility_check" => ["reproducibility_check_result"],
            _ => ["domain_review_evidence"]
        };

    private static IReadOnlyList<string> RouterReplacementReasons(KnowledgeQualityItem item)
    {
        if (item.ValidationScore >= 0.68 || item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var replacement = PrimaryValidationRequirementType(item.Domain);
        return [$"skipped_by_router:{item.Domain}:historical_test:replaced_by_{replacement}"];
    }

    private static KnowledgeValidationRequirement Requirement(
        KnowledgeQualityItem item,
        string type,
        string reason,
        IReadOnlyList<string> missingEvidence,
        string requiredTaskType,
        string mappedInternalTaskType,
        double priority) =>
        new(
            RequirementId: StableId("validation_requirement", item.KnowledgeId, type),
            KnowledgeItemId: item.KnowledgeId,
            RequirementType: type,
            Status: "missing",
            Reason: reason,
            MissingEvidence: missingEvidence,
            EvidenceRefs: item.EvidenceRefs.Take(12).ToList(),
            RequiredTaskType: requiredTaskType,
            MappedInternalTaskType: mappedInternalTaskType,
            Priority: Math.Round(Math.Clamp(priority, 0, 1), 4),
            NoTradingExecution: true,
            HumanReviewRequired: true);

    private KnowledgeValidationStatus WriteStatus(KnowledgeValidationPlanReport report, ResearchQueue queue)
    {
        var validationQueueItems = queue.Items
            .Where(item => item.RequestedBy.Equals("knowledge_validation_strategy", StringComparison.OrdinalIgnoreCase)
                || item.Notes.Any(note => note.StartsWith("validation_plan:", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var routingStatus = new DomainValidationRouter(_storagePaths).BuildStatus();
        var status = new KnowledgeValidationStatus(
            StatusVersion: "knowledge_validation_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ValidationPlansOpen: report.OpenPlans,
            ValidationTasksPending: validationQueueItems.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)),
            TrustedCandidateCount: report.TrustedCandidateCount,
            KnowledgeItemsNeedingOos: report.KnowledgeItemsNeedingOos,
            KnowledgeItemsNeedingSourceCheck: report.KnowledgeItemsNeedingSourceCheck,
            QueueValidationTasks: validationQueueItems.Count,
            PlansPath: PlansPath,
            RequirementsPath: RequirementsPath,
            ResearchQueuePath: new ResearchQueueService(_storagePaths).QueuePath,
            MostCommonMissingEvidence: report.MostCommonMissingEvidence,
            Warnings: report.Warnings,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            InvalidValidationTasks: routingStatus.InvalidValidationTasks,
            ValidationTasksCleaned: routingStatus.ValidationTasksCleaned,
            ValidationRoutingHealth: routingStatus.ValidationRoutingHealth);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    private static bool IsOpenPlan(KnowledgeValidationPlan plan) =>
        plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase);

    private static HermesGoal? RelatedGoalFor(KnowledgeQualityItem item, IReadOnlyList<HermesGoal> goals)
    {
        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return goals.FirstOrDefault(goal => goal.GoalId.Equals("improve_trading_robustness", StringComparison.OrdinalIgnoreCase))
                ?? goals.FirstOrDefault(goal => goal.GoalId.Equals("improve_knowledge_quality", StringComparison.OrdinalIgnoreCase));
        }

        return goals.FirstOrDefault(goal => goal.GoalId.Equals("improve_knowledge_quality", StringComparison.OrdinalIgnoreCase))
            ?? goals.FirstOrDefault(goal => goal.GoalId.Equals("reduce_low_confidence_knowledge", StringComparison.OrdinalIgnoreCase));
    }

    private static string TargetStatusFor(KnowledgeQualityItem item)
    {
        if (item.QualityScore >= 0.68 && item.ValidationScore >= 0.55)
        {
            return "trusted_candidate";
        }

        return item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            ? "promising_after_oos_cost_monte_carlo"
            : "promising_after_source_and_domain_review";
    }

    private static string MappedInternalTask(string domain, string requirementType)
    {
        var capability = new DomainValidationRouter().CapabilityFor(domain, requirementType);
        if (capability is not null)
        {
            return capability.DefaultMappedInternalTaskType;
        }

        if (!domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            return "generate_domain_insights";
        }

        return requirementType switch
        {
            "historical_test" => "run_strategy_research",
            "out_of_sample_test" => "run_walkforward_validation",
            "walkforward_test" => "run_walkforward_validation",
            "cost_stress_test" => "cost-stress-report",
            "monte_carlo_test" => "monte-carlo-report",
            "source_verification" => "scan_knowledge_sources",
            _ => "generate_cognitive_insights"
        };
    }

    private static string RequiredTaskType(string domain, string requirementType)
    {
        var capability = new DomainValidationRouter().CapabilityFor(domain, requirementType);
        return capability?.DefaultTaskType ?? "run_domain_review";
    }

    private static double CostPenalty(KnowledgeQualityItem item, IReadOnlyList<KnowledgeValidationRequirement> requirements)
    {
        var heavy = requirements.Count(requirement =>
            requirement.RequirementType is "out_of_sample_test" or "walkforward_test" or "monte_carlo_test" or "cost_stress_test");
        var domainPenalty = item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase) ? 0.04 : 0.01;
        return Math.Clamp(heavy * 0.025 + domainPenalty, 0, 0.2);
    }

    private static IReadOnlyList<string> CommonMissingEvidence(IReadOnlyList<KnowledgeValidationPlan> plans) =>
        plans
            .SelectMany(plan => plan.MissingEvidence)
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToList();

    private static string StableId(string prefix, params string[] values)
    {
        var input = string.Join("|", values);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..12];
        return $"{prefix}_{hash}";
    }
}
