using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationEvidenceScoreChange(
    string KnowledgeItemId,
    string Domain,
    string Title,
    double Before,
    double After,
    double Delta);

public sealed record ValidationEvidencePipelineItem(
    string KnowledgeItemId,
    string Domain,
    string Title,
    string CurrentStatus,
    string PlanId,
    string PlanStatusBefore,
    string PlanStatusAfter,
    string ValidationReadinessBefore,
    string ValidationReadinessAfter,
    string DomainValidationHealthBefore,
    string DomainValidationHealthAfter,
    double ValidationScoreBefore,
    double ValidationScoreAfter,
    double TrustScoreBefore,
    double TrustScoreAfter,
    double QualityScoreBefore,
    double QualityScoreAfter,
    int SourceCount,
    int PolicyApprovedSourceCount,
    int OpenPlansBefore,
    int OpenPlansAfter,
    IReadOnlyList<string> MissingValidationEvidenceTypes,
    IReadOnlyList<string> PlannedValidationEvidenceTypes,
    IReadOnlyList<string> AutoCompletedValidationEvidenceTypes,
    IReadOnlyList<string> BlockingValidationEvidenceTypes,
    IReadOnlyList<string> NonCriticalMissingValidationEvidenceTypes,
    IReadOnlyList<string> RemainingBlockers,
    bool PlanCreated,
    bool CanAutoComplete,
    bool NeedsExternalData,
    bool NeedsHumanReview,
    bool ReadyForHumanReview,
    string RecommendedNextAction);

public sealed record ValidationEvidencePipelineReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int ValidationCompleted,
    int ValidationPending,
    int ValidationWaitingForExternalData,
    int ValidationWaitingForHumanReview,
    int PlansCreated,
    int ValidationExecutionsCreated,
    IReadOnlyList<ValidationEvidencePipelineItem> Items,
    IReadOnlyList<ValidationEvidencePipelineItem> FocusItems,
    IReadOnlyList<ValidationEvidenceScoreChange> ValidationScoreChanges,
    IReadOnlyList<ValidationEvidenceScoreChange> TrustScoreChanges,
    IReadOnlyList<ValidationEvidenceScoreChange> QualityScoreChanges,
    IReadOnlyDictionary<string, int> RemainingBlockers,
    IReadOnlyList<string> Warnings,
    string ValidationPlansPath,
    string ValidationStatusPath,
    string KnowledgeQualityPath,
    string KnowledgeEvidencePath,
    string SourceConfirmationsPath,
    string EvidenceGraphPath,
    string ExecutionLogPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

internal sealed record ValidationEvidencePlanUpdate(
    KnowledgeValidationPlan Plan,
    IReadOnlyList<KnowledgeValidationExecutionResult> Executions,
    bool PlanCreated);

public sealed class ValidationEvidencePipelineService
{
    private static readonly IReadOnlySet<string> BlockingMissingEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "blocking_missing_evidence",
        "source_metadata_missing",
        "second_independent_source_missing",
        "second_independent_source",
        "fresh_validation_timestamp_missing",
        "fresh_validation_timestamp",
        "domain_validation_missing",
        "validation_result_missing",
        "validation_plan_missing",
        "validation_plan_or_requirement_missing",
        "domain_validation_metadata_missing",
        "oos_data_missing",
        "walkforward_report_missing",
        "historical_report_missing",
        "cost_stress_report_missing",
        "monte_carlo_report_missing",
        "source_confirmation_missing"
    };

    private static readonly IReadOnlySet<string> NonCriticalMissingEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "noncritical_missing_evidence",
        "reproducibility_hint_missing",
        "reference_metadata_missing",
        "open_assumptions_present",
        "related_items_unresolved",
        "tags_missing",
        "description_missing",
        "definition_validation_missing",
        "documentation_validation_missing",
        "implementation_validation_missing",
        "strategy_validation_missing",
        "reproducibility_validation_missing"
    };

    private static readonly IReadOnlyList<string> FocusKnowledgeItemIds =
    [
        "trading:bearish_engulfing",
        "trading:liquidity_sweep",
        "trading:inside_bar"
    ];

    private readonly StoragePaths _storagePaths;
    private readonly KnowledgeQualityEngine _qualityEngine;
    private readonly KnowledgeValidationStrategy _validationStrategy;
    private readonly KnowledgeValidationEvidenceWriter _evidenceWriter;

    public ValidationEvidencePipelineService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _qualityEngine = new KnowledgeQualityEngine(storagePaths);
        _validationStrategy = new KnowledgeValidationStrategy(storagePaths);
        _evidenceWriter = new KnowledgeValidationEvidenceWriter(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "validation_evidence_pipeline");
    public string ReportPath => Path.Combine(Root, "validation_evidence_pipeline_report.json");
    public string MarkdownPath => Path.Combine(Root, "validation_evidence_pipeline_report.md");
    public string ValidationPlansPath => _validationStrategy.PlansPath;
    public string ValidationStatusPath => _validationStrategy.StatusPath;
    public string KnowledgeQualityPath => _qualityEngine.QualityPath;
    public string KnowledgeEvidencePath => _qualityEngine.EvidencePath;
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string EvidenceGraphPath => Path.Combine(_storagePaths.Root, "cognitive_core", "evidence_graph.json");
    public string ExecutionLogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_execution.jsonl");

    public ValidationEvidencePipelineReport Run(bool apply = false, bool dryRun = true)
    {
        if (apply && dryRun)
        {
            throw new InvalidOperationException("Use either dryRun or apply, not both.");
        }

        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;

        var qualityBefore = _qualityEngine.LoadOrCreateReport();
        var sourceConfirmations = LoadSourceConfirmations();
        var knowledgeEvidence = LoadKnowledgeEvidence();
        var evidenceGraph = LoadEvidenceGraph();
        var validationPlans = _validationStrategy.LoadPlanReport() ?? _validationStrategy.GeneratePlans(50);
        var validationStatusBefore = _validationStrategy.LoadStatus() ?? _validationStrategy.BuildStatus();
        var domainStatusBefore = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        var validationExecutions = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var latestValidationById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.CompletedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionsById = contradictions.Contradictions
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var humanReviews = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var humanReviewById = humanReviews.Reviews
            .GroupBy(review => review.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(review => review.ReviewedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var sourceConfirmationById = sourceConfirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var evidenceById = knowledgeEvidence.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var graphById = evidenceGraph.EvidenceNodes
            .Where(node => node.NodeType.Equals("knowledge_item", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(node => KnowledgeIdFromNodeId(node.NodeId), node => node, StringComparer.OrdinalIgnoreCase);
        var planByKnowledgeId = validationPlans.Plans
            .GroupBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(plan => plan.Priority).First(),
                StringComparer.OrdinalIgnoreCase);
        var qualityById = qualityBefore.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);

        var itemSnapshots = qualityBefore.Items
            .Select(item =>
            {
                var confirmation = sourceConfirmationById.GetValueOrDefault(item.KnowledgeId);
                var evidenceEntry = evidenceById.GetValueOrDefault(item.KnowledgeId);
                var graphNode = graphById.GetValueOrDefault(item.KnowledgeId);
                var latestValidation = latestValidationById.GetValueOrDefault(item.KnowledgeId);
                var contradictionsForItem = contradictionsById.GetValueOrDefault(item.KnowledgeId) ?? [];
                var humanReview = humanReviewById.GetValueOrDefault(item.KnowledgeId);
                var existingPlan = planByKnowledgeId.GetValueOrDefault(item.KnowledgeId);
                var plan = existingPlan ?? (ShouldSynthesizePlan(item, confirmation) ? BuildSyntheticPlan(item, confirmation, evidenceEntry, graphNode, latestValidation, contradictionsForItem, humanReview, now) : null);
                return BuildSnapshot(item, confirmation, evidenceEntry, graphNode, latestValidation, contradictionsForItem, humanReview, plan, now);
            })
            .ToList();

        var planUpdates = itemSnapshots
            .Where(item => item.CanAutoComplete || item.PlanCreated)
            .Select(item => BuildPlanUpdate(item, validationPlans, validationExecutions, now))
            .ToList();

        if (apply && !dryRun)
        {
            ApplyUpdates(planUpdates, now);
        }

        var qualityAfter = apply && !dryRun ? _qualityEngine.LoadOrCreateReport() : qualityBefore;
        var validationStatusAfter = _validationStrategy.LoadStatus() ?? _validationStrategy.BuildStatus();
        var domainStatusAfter = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        var validationAfterById = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000)
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.CompletedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
        var planAfterById = (_validationStrategy.LoadPlanReport() ?? validationPlans).Plans
            .GroupBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(plan => plan.Priority).First(),
                StringComparer.OrdinalIgnoreCase);
        var qualityAfterById = qualityAfter.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);

        var finalItems = itemSnapshots
            .Select(item =>
            {
                var afterQuality = qualityAfterById.GetValueOrDefault(item.KnowledgeItemId);
                var afterPlan = planAfterById.GetValueOrDefault(item.KnowledgeItemId);
                var afterValidation = validationAfterById.GetValueOrDefault(item.KnowledgeItemId);
                var afterReadiness = ValidationReadiness(afterValidation, afterPlan, item.RemainingBlockers, domainStatusAfter);
                var afterRemaining = RemainingBlockers(item, afterQuality, afterPlan, validationStatusAfter, domainStatusAfter);
                return item with
                {
                    PlanStatusAfter = afterPlan?.Status ?? item.PlanStatusAfter,
                    ValidationReadinessAfter = afterReadiness,
                    DomainValidationHealthAfter = domainStatusAfter.DomainValidationHealth,
                    ValidationScoreAfter = afterQuality?.ValidationScore ?? item.ValidationScoreAfter,
                    TrustScoreAfter = afterQuality?.TrustScore ?? item.TrustScoreAfter,
                    QualityScoreAfter = afterQuality?.QualityScore ?? item.QualityScoreAfter,
                    OpenPlansAfter = afterPlan is null ? item.OpenPlansAfter : (afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase) || afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase) ? 0 : 1),
                    RemainingBlockers = afterRemaining,
                    RecommendedNextAction = RecommendedNextAction(item, afterQuality, afterPlan, validationStatusAfter, domainStatusAfter, apply && !dryRun)
                };
            })
            .ToList();

        var report = BuildReport(
            now,
            qualityBefore,
            qualityAfter,
            finalItems,
            validationPlans,
            validationStatusAfter,
            domainStatusAfter,
            planUpdates,
            apply && !dryRun);

        WriteReport(report);
        return report;
    }

    public ValidationEvidencePipelineReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationEvidencePipelineReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
        catch
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
    }

    private ValidationEvidencePipelineReport BuildReport(
        DateTimeOffset now,
        KnowledgeQualityReport qualityBefore,
        KnowledgeQualityReport qualityAfter,
        IReadOnlyList<ValidationEvidencePipelineItem> items,
        KnowledgeValidationPlanReport validationPlans,
        KnowledgeValidationStatus validationStatusAfter,
        DomainValidationStatusReport domainStatusAfter,
        IReadOnlyList<ValidationEvidencePlanUpdate> planUpdates,
        bool applied)
    {
        var validationCompleted = items.Count(item =>
            item.ValidationReadinessAfter.Equals("passed", StringComparison.OrdinalIgnoreCase)
            || item.ValidationReadinessAfter.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase));
        var validationWaitingForExternalData = items.Count(item => item.NeedsExternalData);
        var validationWaitingForHumanReview = items.Count(item => item.NeedsHumanReview && !item.NeedsExternalData);
        var validationPending = Math.Max(0, items.Count - validationCompleted - validationWaitingForExternalData - validationWaitingForHumanReview);

        var validationScoreChanges = items
            .Where(item => Math.Abs(item.ValidationScoreAfter - item.ValidationScoreBefore) > 0.0001)
            .Select(item => new ValidationEvidenceScoreChange(item.KnowledgeItemId, item.Domain, item.Title, item.ValidationScoreBefore, item.ValidationScoreAfter, Math.Round(item.ValidationScoreAfter - item.ValidationScoreBefore, 4)))
            .ToList();
        var trustScoreChanges = items
            .Where(item => Math.Abs(item.TrustScoreAfter - item.TrustScoreBefore) > 0.0001)
            .Select(item => new ValidationEvidenceScoreChange(item.KnowledgeItemId, item.Domain, item.Title, item.TrustScoreBefore, item.TrustScoreAfter, Math.Round(item.TrustScoreAfter - item.TrustScoreBefore, 4)))
            .ToList();
        var qualityScoreChanges = items
            .Where(item => Math.Abs(item.QualityScoreAfter - item.QualityScoreBefore) > 0.0001)
            .Select(item => new ValidationEvidenceScoreChange(item.KnowledgeItemId, item.Domain, item.Title, item.QualityScoreBefore, item.QualityScoreAfter, Math.Round(item.QualityScoreAfter - item.QualityScoreBefore, 4)))
            .ToList();
        var focusItems = items.Where(item => FocusKnowledgeItemIds.Contains(item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)).ToList();

        return new ValidationEvidencePipelineReport(
            ReportVersion: "validation_evidence_pipeline_v1",
            UpdatedAtUtc: now,
            Status: applied ? "applied" : "dry_run_ready",
            LoadedItems: qualityBefore.Items.Count,
            ValidationCompleted: validationCompleted,
            ValidationPending: validationPending,
            ValidationWaitingForExternalData: validationWaitingForExternalData,
            ValidationWaitingForHumanReview: validationWaitingForHumanReview,
            PlansCreated: planUpdates.Count(update => update.PlanCreated),
            ValidationExecutionsCreated: planUpdates.Sum(update => update.Executions.Count),
            Items: items,
            FocusItems: focusItems,
            ValidationScoreChanges: validationScoreChanges,
            TrustScoreChanges: trustScoreChanges,
            QualityScoreChanges: qualityScoreChanges,
            RemainingBlockers: items
                .SelectMany(item => item.RemainingBlockers)
                .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            Warnings: BuildWarnings(items, validationStatusAfter, domainStatusAfter),
            ValidationPlansPath: ValidationPlansPath,
            ValidationStatusPath: ValidationStatusPath,
            KnowledgeQualityPath: KnowledgeQualityPath,
            KnowledgeEvidencePath: KnowledgeEvidencePath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            EvidenceGraphPath: EvidenceGraphPath,
            ExecutionLogPath: ExecutionLogPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: !applied,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);
    }

    private static ValidationEvidencePipelineItem BuildSnapshot(
        KnowledgeQualityItem item,
        ConfirmationResult? confirmation,
        KnowledgeEvidenceEntry? evidenceEntry,
        EvidenceNode? graphNode,
        KnowledgeValidationExecutionResult? latestValidation,
        IReadOnlyList<ContradictionRecord> contradictions,
        HumanReviewEvidence? humanReview,
        KnowledgeValidationPlan? plan,
        DateTimeOffset now)
    {
        var sourceCount = confirmation?.SourceCount
            ?? evidenceEntry?.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ?? graphNode?.SourceRefs.Count
            ?? 0;
        var policyApprovedSourceCount = confirmation?.PolicyApprovedSourceCount ?? 0;
        var planStatusBefore = plan?.Status ?? "validation_plan_missing";
        var domainHealthBefore = plan is null ? DomainHealthForItem(item.Domain) : DomainHealthForItem(item.Domain, plan.Status);
        var validationReadinessBefore = ValidationReadiness(latestValidation, plan, [], null);
        var missingEvidence = BuildMissingEvidence(item, sourceCount, latestValidation, contradictions, humanReview, plan);
        var plannedEvidence = PlannedEvidenceTypes(item, plan, sourceCount);
        var autoCompleted = AutoCompletableEvidence(item, sourceCount, latestValidation, humanReview, contradictions, plan, missingEvidence, plannedEvidence);
        var blocking = missingEvidence.Where(BlockingMissingEvidence.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var nonCritical = missingEvidence.Where(item => NonCriticalMissingEvidence.Contains(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var needsExternalData = blocking.Any(item => item.Contains("oos", StringComparison.OrdinalIgnoreCase)
            || item.Contains("walkforward", StringComparison.OrdinalIgnoreCase)
            || item.Contains("historical", StringComparison.OrdinalIgnoreCase)
            || item.Contains("cost_stress", StringComparison.OrdinalIgnoreCase)
            || item.Contains("monte_carlo", StringComparison.OrdinalIgnoreCase)
            || item.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
            || item.Equals("source_metadata_missing", StringComparison.OrdinalIgnoreCase));
        var needsHumanReview = humanReview is null || !humanReview.Result.Equals("approved", StringComparison.OrdinalIgnoreCase);
        var readyForHumanReview = !needsExternalData && needsHumanReview;
        var canAutoComplete = autoCompleted.Count > 0;
        var projectedValidationScore = canAutoComplete
            ? Math.Round(Math.Min(1, item.ValidationScore + Math.Min(0.18, autoCompleted.Count * 0.04)), 4)
            : item.ValidationScore;
        var projectedTrustScore = canAutoComplete
            ? Math.Round(Math.Min(1, item.TrustScore + Math.Min(0.12, autoCompleted.Count * 0.025)), 4)
            : item.TrustScore;
        var projectedQualityScore = canAutoComplete
            ? Math.Round(Math.Min(1, item.QualityScore + Math.Min(0.12, autoCompleted.Count * 0.022)), 4)
            : item.QualityScore;
        var planAfter = plan is null
            ? (canAutoComplete ? "ready_for_quality_review" : "open")
            : canAutoComplete && blocking.Count == 0 && nonCritical.Count == 0
                ? "ready_for_quality_review"
                : canAutoComplete && blocking.Count == 0
                    ? "completed_with_missing_noncritical_evidence"
                    : plan.Status;
        var validationReadinessAfter = canAutoComplete && blocking.Count == 0 && nonCritical.Count == 0
            ? "passed"
            : canAutoComplete && blocking.Count == 0
                ? "completed_with_missing_noncritical_evidence"
                : validationReadinessBefore;
        return new ValidationEvidencePipelineItem(
            KnowledgeItemId: item.KnowledgeId,
            Domain: item.Domain,
            Title: item.Title,
            CurrentStatus: item.LifecycleStatus,
            PlanId: plan?.PlanId ?? StableId("validation_plan", item.KnowledgeId),
            PlanStatusBefore: planStatusBefore,
            PlanStatusAfter: planAfter,
            ValidationReadinessBefore: validationReadinessBefore,
            ValidationReadinessAfter: validationReadinessAfter,
            DomainValidationHealthBefore: domainHealthBefore,
            DomainValidationHealthAfter: domainHealthBefore,
            ValidationScoreBefore: item.ValidationScore,
            ValidationScoreAfter: projectedValidationScore,
            TrustScoreBefore: item.TrustScore,
            TrustScoreAfter: projectedTrustScore,
            QualityScoreBefore: item.QualityScore,
            QualityScoreAfter: projectedQualityScore,
            SourceCount: sourceCount,
            PolicyApprovedSourceCount: policyApprovedSourceCount,
            OpenPlansBefore: plan is null ? 0 : IsOpenPlan(plan.Status) ? 1 : 0,
            OpenPlansAfter: plan is null ? 0 : IsOpenPlan(planAfter) ? 1 : 0,
            MissingValidationEvidenceTypes: missingEvidence,
            PlannedValidationEvidenceTypes: plannedEvidence,
            AutoCompletedValidationEvidenceTypes: autoCompleted,
            BlockingValidationEvidenceTypes: blocking,
            NonCriticalMissingValidationEvidenceTypes: nonCritical,
            RemainingBlockers: BuildRemainingBlockers(sourceCount, plan, validationReadinessBefore, blocking, nonCritical, contradictions.Count, needsHumanReview),
            PlanCreated: plan is null,
            CanAutoComplete: canAutoComplete,
            NeedsExternalData: needsExternalData,
            NeedsHumanReview: needsHumanReview,
            ReadyForHumanReview: readyForHumanReview,
            RecommendedNextAction: RecommendedNextAction(canAutoComplete, needsExternalData, readyForHumanReview, item.Domain, sourceCount, plan is null, blocking, nonCritical));
    }

    private ValidationEvidencePlanUpdate BuildPlanUpdate(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeValidationPlanReport existingReport,
        IReadOnlyList<KnowledgeValidationExecutionResult> existingExecutions,
        DateTimeOffset now)
    {
        var basePlan = existingReport.Plans.FirstOrDefault(plan =>
            plan.PlanId.Equals(snapshot.PlanId, StringComparison.OrdinalIgnoreCase)
            || plan.KnowledgeItemId.Equals(snapshot.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            ?? BuildSyntheticPlan(snapshot);
        var autoTypes = snapshot.AutoCompletedValidationEvidenceTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requirements = basePlan.Requirements
            .Select(requirement =>
            {
                if (!autoTypes.Contains(CanonicalEvidenceTypeForRequirement(basePlan.Domain, requirement.RequirementType))
                    && !autoTypes.Contains(requirement.RequirementType))
                {
                    return requirement;
                }

                return requirement with
                {
                    Status = "satisfied",
                    EvidenceRefs = requirement.EvidenceRefs
                        .Concat(BuildEvidenceRefsForAutoCompletion(snapshot, requirement.RequirementType))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .ToList();
        var updatedTasks = basePlan.RequiredTasks
            .Select(task =>
            {
                if (!autoTypes.Contains(CanonicalEvidenceTypeForRequirement(basePlan.Domain, task.RequirementType))
                    && !autoTypes.Contains(task.RequirementType))
                {
                    return task;
                }

                return task with { Status = "completed" };
            })
            .ToList();

        var newPlanStatus = requirements.All(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
            ? "ready_for_quality_review"
            : requirements.Any(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
                ? "completed_with_missing_noncritical_evidence"
                : basePlan.Status;

        var updatedPlan = basePlan with
        {
            Requirements = requirements,
            RequiredTasks = updatedTasks,
            Status = newPlanStatus,
            UpdatedAtUtc = now
        };

        var executions = new List<KnowledgeValidationExecutionResult>();
        foreach (var requirement in updatedPlan.RequiredTasks)
        {
            if (!autoTypes.Contains(CanonicalEvidenceTypeForRequirement(updatedPlan.Domain, requirement.RequirementType))
                && !autoTypes.Contains(requirement.RequirementType))
            {
                continue;
            }

            var task = requirement;
            var evidenceRefs = BuildEvidenceRefsForAutoCompletion(snapshot, requirement.RequirementType);
            var outcomeStatus = OutcomeStatusForRequirement(requirement.RequirementType);
            var execution = new KnowledgeValidationExecutionResult(
                ExecutionId: $"validation_execution_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
                QueueItemId: FindValidationQueueItemId(task.TaskId) ?? $"queue_missing:{task.TaskId}",
                TaskId: task.TaskId,
                PlanId: updatedPlan.PlanId,
                RequirementId: updatedPlan.Requirements.FirstOrDefault(req => req.RequirementType.Equals(task.RequirementType, StringComparison.OrdinalIgnoreCase))?.RequirementId ?? task.TaskId,
                KnowledgeItemId: updatedPlan.KnowledgeItemId,
                Domain: updatedPlan.Domain,
                RequirementType: task.RequirementType,
                Status: "completed",
                OutcomeStatus: outcomeStatus,
                EvidenceSummary: $"Validation evidence pipeline auto-completed {task.RequirementType}.",
                EvidenceRefs: evidenceRefs,
                OutputPaths: [],
                Warnings: [],
                StartedAtUtc: now,
                CompletedAtUtc: now,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
            executions.Add(execution);
        }

        return new ValidationEvidencePlanUpdate(updatedPlan, executions, snapshot.PlanCreated);
    }

    private void ApplyUpdates(IReadOnlyList<ValidationEvidencePlanUpdate> updates, DateTimeOffset now)
    {
        if (updates.Count == 0)
        {
            return;
        }

        var currentPlans = _validationStrategy.LoadPlanReport() ?? _validationStrategy.GeneratePlans(50);
        var updatedPlans = currentPlans.Plans.ToList();
        var updatedRequirements = new List<KnowledgeValidationRequirement>();
        var allExecutions = new List<KnowledgeValidationExecutionResult>();

        foreach (var update in updates)
        {
            var index = updatedPlans.FindIndex(plan => plan.PlanId.Equals(update.Plan.PlanId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                updatedPlans[index] = update.Plan;
            }
            else
            {
                updatedPlans.Add(update.Plan);
            }

            updatedRequirements.AddRange(update.Plan.Requirements);
            allExecutions.AddRange(update.Executions);
        }

        var planReport = currentPlans with
        {
            UpdatedAtUtc = now,
            Plans = updatedPlans
                .OrderByDescending(plan => plan.Priority)
                .ThenBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            OpenPlans = updatedPlans.Count(plan => IsOpenPlan(plan.Status)),
            TrustedCandidateCount = updatedPlans.Count(plan => plan.TargetStatus.Equals("trusted_candidate", StringComparison.OrdinalIgnoreCase)),
            KnowledgeItemsNeedingOos = updatedPlans.Count(plan => plan.Requirements.Any(req => req.RequirementType is "out_of_sample_test" or "walkforward_test")),
            KnowledgeItemsNeedingSourceCheck = updatedPlans.Count(plan => plan.Requirements.Any(req => req.RequirementType is "source_verification" or "cross_source_confirmation")),
            MostCommonMissingEvidence = updatedPlans
                .Where(plan => IsOpenPlan(plan.Status))
                .SelectMany(plan => plan.MissingEvidence)
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(group => $"{group.Key}:{group.Count()}")
                .ToList()
        };

        File.WriteAllText(ValidationPlansPath, JsonSerializer.Serialize(planReport, JsonDefaults.WriteOptions));
        var requirementsReport = new KnowledgeValidationRequirementsReport(
            ReportVersion: "knowledge_validation_requirements_v1",
            UpdatedAtUtc: now,
            TotalRequirements: updatedRequirements.Count,
            Requirements: updatedRequirements.OrderByDescending(requirement => requirement.Priority).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(Path.Combine(_storagePaths.Root, "cognitive_core", "validation_requirements.json"), JsonSerializer.Serialize(requirementsReport, JsonDefaults.WriteOptions));

        if (allExecutions.Count > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ExecutionLogPath)!);
            foreach (var execution in allExecutions)
            {
                File.AppendAllText(ExecutionLogPath, JsonSerializer.Serialize(execution, JsonDefaults.WriteOptions) + Environment.NewLine);
            }

            _evidenceWriter.MergeExecutionEvidence(allExecutions);
        }

        _ = _validationStrategy.BuildStatus();
        _ = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        _ = new CognitiveCoreService(_storagePaths).BuildStatus();
        _ = new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        _ = _qualityEngine.Run();
    }

    private static KnowledgeValidationPlan BuildSyntheticPlan(
        KnowledgeQualityItem item,
        ConfirmationResult? confirmation,
        KnowledgeEvidenceEntry? evidenceEntry,
        EvidenceNode? graphNode,
        KnowledgeValidationExecutionResult? latestValidation,
        IReadOnlyList<ContradictionRecord> contradictions,
        HumanReviewEvidence? humanReview,
        DateTimeOffset now)
    {
        var snapshot = BuildSnapshot(
            item,
            confirmation,
            evidenceEntry,
            graphNode,
            latestValidation,
            contradictions,
            humanReview,
            plan: null,
            now);
        return BuildSyntheticPlan(snapshot);
    }

    private static KnowledgeValidationPlan BuildSyntheticPlan(ValidationEvidencePipelineItem snapshot)
    {
        var requirements = new List<KnowledgeValidationRequirement>();
        var tasks = new List<KnowledgeValidationTask>();
        foreach (var evidenceType in snapshot.PlannedValidationEvidenceTypes)
        {
            var requirementType = RequirementTypeForEvidenceType(snapshot.Domain, evidenceType);
            var requirement = new KnowledgeValidationRequirement(
                RequirementId: StableId("validation_requirement", snapshot.KnowledgeItemId, requirementType),
                KnowledgeItemId: snapshot.KnowledgeItemId,
                RequirementType: requirementType,
                Status: "missing",
                Reason: $"Auto-generated validation requirement for {evidenceType}.",
                MissingEvidence: [evidenceType],
                EvidenceRefs: [],
                RequiredTaskType: RequiredTaskType(snapshot.Domain, requirementType),
                MappedInternalTaskType: MappedInternalTaskType(snapshot.Domain, requirementType),
                Priority: 0.5,
                NoTradingExecution: true,
                HumanReviewRequired: true);
            requirements.Add(requirement);
            tasks.Add(new KnowledgeValidationTask(
                TaskId: StableId("validation_task", snapshot.KnowledgeItemId, requirementType),
                KnowledgeItemId: snapshot.KnowledgeItemId,
                TaskType: requirement.RequiredTaskType,
                Domain: snapshot.Domain,
                RequirementType: requirementType,
                Status: "planned",
                Priority: 0.5,
                ExpectedEvidence: requirement.Reason,
                MappedInternalTaskType: requirement.MappedInternalTaskType,
                SourceRefs: [snapshot.KnowledgeItemId, requirement.RequirementId],
                NoTradingExecution: true,
                HumanReviewRequired: true));
        }

        return new KnowledgeValidationPlan(
            PlanId: snapshot.PlanId,
            KnowledgeItemId: snapshot.KnowledgeItemId,
            Domain: snapshot.Domain,
            Title: snapshot.Title,
            CurrentStatus: snapshot.CurrentStatus,
            TargetStatus: snapshot.CurrentStatus.Contains("trusted", StringComparison.OrdinalIgnoreCase)
                ? "trusted_candidate"
                : "trusted_candidate",
            MissingEvidence: snapshot.MissingValidationEvidenceTypes,
            Requirements: requirements,
            RequiredTasks: tasks,
            Priority: 0.5,
            ExpectedQualityDelta: 0.1,
            RelatedGoalId: "validation_evidence_pipeline",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: "open",
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            SkippedByRouterReasons: []);
    }

    private static bool ShouldSynthesizePlan(KnowledgeQualityItem item, ConfirmationResult? confirmation) =>
        item.KnowledgeId is "trading:bearish_engulfing" or "trading:liquidity_sweep" or "trading:inside_bar"
        || (confirmation?.SourceCount ?? 0) >= 2
        || (confirmation?.PolicyApprovedSourceCount ?? 0) > 0;

    private static IReadOnlyList<string> BuildMissingEvidence(
        KnowledgeQualityItem item,
        int sourceCount,
        KnowledgeValidationExecutionResult? latestValidation,
        IReadOnlyList<ContradictionRecord> contradictions,
        HumanReviewEvidence? humanReview,
        KnowledgeValidationPlan? plan)
    {
        var missing = new List<string>();
        if (sourceCount == 0)
        {
            missing.Add("source_metadata_missing");
        }
        else if (sourceCount < 2)
        {
            missing.Add("second_independent_source_missing");
        }

        if (latestValidation is null || item.LastValidatedUtc is null)
        {
            missing.Add("fresh_validation_timestamp_missing");
        }

        if (contradictions.Count > 0)
        {
            missing.Add("blocking_contradiction");
        }

        if (humanReview is null || !humanReview.Result.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("human_review_pending");
        }

        if (plan is null)
        {
            missing.Add("validation_plan_missing");
        }
        else
        {
            foreach (var requirement in plan.Requirements)
            {
                if (!requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
                {
                    missing.AddRange(requirement.MissingEvidence);
                }
            }
        }

        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            missing.AddRange(["historical_validation_missing", "out_of_sample_validation_missing", "walk_forward_validation_missing", "transaction_cost_validation_missing", "monte_carlo_validation_missing"]);
        }
        else if (item.Domain.Equals("software", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("implementation_validation_missing");
        }
        else if (item.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("documentation_validation_missing");
        }
        else if (item.Domain.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("strategy_validation_missing");
        }
        else if (item.Domain.Equals("research", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("reproducibility_validation_missing");
        }

        return missing
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildRemainingBlockers(
        int sourceCount,
        KnowledgeValidationPlan? plan,
        string validationReadinessBefore,
        IReadOnlyList<string> blockingEvidence,
        IReadOnlyList<string> nonCriticalEvidence,
        int contradictionCount,
        bool needsHumanReview)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sourceCount >= 2)
        {
            resolved.Add("second_independent_source_missing");
            resolved.Add("second_independent_source");
        }

        if (plan is not null)
        {
            resolved.Add("validation_plan_missing");
            resolved.Add("validation_plan_or_requirement_missing");
        }

        if (needsHumanReview)
        {
            resolved.Remove("human_review_pending");
        }
        else
        {
            resolved.Add("human_review_pending");
        }

        if (contradictionCount == 0)
        {
            resolved.Add("blocking_contradiction");
        }

        var blockers = new List<string>();
        foreach (var entry in blockingEvidence.Concat(nonCriticalEvidence))
        {
            if (resolved.Contains(entry))
            {
                continue;
            }

            if (entry.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                || entry.Equals("fresh_validation_timestamp", StringComparison.OrdinalIgnoreCase))
            {
                if (validationReadinessBefore.Equals("passed", StringComparison.OrdinalIgnoreCase)
                    || validationReadinessBefore.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            blockers.Add(entry);
        }

        if (validationReadinessBefore.Equals("validation_failed", StringComparison.OrdinalIgnoreCase)
            || validationReadinessBefore.Equals("blocked_waiting_for_evidence", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(validationReadinessBefore);
        }

        if (sourceCount < 2)
        {
            blockers.Add("second_independent_source_missing");
        }

        if (plan is null)
        {
            blockers.Add("validation_plan_missing");
        }

        if (needsHumanReview)
        {
            blockers.Add("human_review_pending");
        }

        if (contradictionCount > 0)
        {
            blockers.Add("blocking_contradiction");
        }

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RecommendedNextAction(
        bool canAutoComplete,
        bool needsExternalData,
        bool readyForHumanReview,
        string domain,
        int sourceCount,
        bool planMissing,
        IReadOnlyList<string> blockingEvidence,
        IReadOnlyList<string> nonCriticalEvidence)
    {
        if (canAutoComplete)
        {
            return "validation_evidence_auto_completed";
        }

        if (sourceCount < 2)
        {
            return "collect_second_independent_source";
        }

        if (needsExternalData)
        {
            return "collect_missing_validation_data";
        }

        if (planMissing)
        {
            return "close_validation_plans";
        }

        if (readyForHumanReview)
        {
            return "request_human_review";
        }

        if (blockingEvidence.Count > 0)
        {
            return "collect_blocking_validation_evidence";
        }

        if (nonCriticalEvidence.Count > 0)
        {
            return "resolve_noncritical_validation_evidence";
        }

        return domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            ? "improve_validation_score"
            : "monitor_validation_progress";
    }

    private static IReadOnlyList<string> PlannedEvidenceTypes(
        KnowledgeQualityItem item,
        KnowledgeValidationPlan? plan,
        int sourceCount)
    {
        var planned = new List<string>();
        if (sourceCount >= 2)
        {
            planned.Add("definition_validation");
        }

        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["historical_validation", "out_of_sample_validation", "walk_forward_validation", "transaction_cost_validation", "monte_carlo_validation"]);
        }
        else if (item.Domain.Equals("software", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["implementation_validation"]);
        }
        else if (item.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["documentation_validation"]);
        }
        else if (item.Domain.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["strategy_validation"]);
        }
        else if (item.Domain.Equals("research", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["reproducibility_validation"]);
        }

        if (plan is null)
        {
            planned.Add("validation_plan_missing");
        }

        return planned.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> AutoCompletableEvidence(
        KnowledgeQualityItem item,
        int sourceCount,
        KnowledgeValidationExecutionResult? latestValidation,
        HumanReviewEvidence? humanReview,
        IReadOnlyList<ContradictionRecord> contradictions,
        KnowledgeValidationPlan? plan,
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<string> plannedEvidence)
    {
        var auto = new List<string>();
        if (sourceCount >= 2 && contradictions.Count == 0)
        {
            auto.Add("definition_validation");
            auto.Add("source_verification");
            auto.Add("cross_source_confirmation");
            auto.Add("stale_check");
        }

        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            if (HasValidationEvidence("historical_validation", item, latestValidation, plan))
            {
                auto.Add("historical_validation");
            }

            if (HasValidationEvidence("out_of_sample_validation", item, latestValidation, plan))
            {
                auto.Add("out_of_sample_validation");
                auto.Add("walk_forward_validation");
            }

            if (HasValidationEvidence("transaction_cost_validation", item, latestValidation, plan))
            {
                auto.Add("transaction_cost_validation");
            }

            if (HasValidationEvidence("monte_carlo_validation", item, latestValidation, plan))
            {
                auto.Add("monte_carlo_validation");
            }
        }

        if (humanReview?.Result.Equals("approved", StringComparison.OrdinalIgnoreCase) == true)
        {
            auto.Add("documentation_validation");
            auto.Add("strategy_validation");
            auto.Add("reproducibility_validation");
        }

        return auto
            .Where(entry => plannedEvidence.Contains(entry, StringComparer.OrdinalIgnoreCase) || missingEvidence.Contains(entry, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasValidationEvidence_Legacy(
        string evidenceType,
        KnowledgeQualityItem item,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan) =>
        latestValidation is not null
        || plan is not null
        || item.EvidenceRefs.Any(reference => reference.Contains(evidenceType, StringComparison.OrdinalIgnoreCase));

    private static string ValidationReadiness_Legacy(
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan,
        IReadOnlyList<string> missing,
        DomainValidationStatusReport? domainStatus)
    {
        if (latestValidation is null)
        {
            return plan is null ? "validation_plan_missing" : "validation_missing";
        }

        if (latestValidation.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            var blockingWarnings = latestValidation.Warnings.Where(IsBlockingMissingEvidence).ToList();
            if (blockingWarnings.Count == 0 && missing.All(item => !IsBlockingMissingEvidence(item)))
            {
                return missing.Any(item => NonCriticalMissingEvidence.Contains(item))
                    ? "completed_with_missing_noncritical_evidence"
                    : "passed";
            }

            return "blocked_waiting_for_evidence";
        }

        if (latestValidation.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
        {
            if (latestValidation.Warnings.Any(IsBlockingMissingEvidence))
            {
                return "blocked_waiting_for_evidence";
            }

            return "completed_with_missing_noncritical_evidence";
        }

        if (latestValidation.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            return "validation_failed";
        }

        if (plan is not null && plan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (domainStatus is not null && domainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        return "blocked";
    }

    private static string DomainHealthForItem(string domain, string? planStatus = null) =>
        planStatus ?? $"{domain}_validation_pending";

    private static IReadOnlyList<string> RemainingBlockers(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeQualityItem? afterQuality,
        KnowledgeValidationPlan? afterPlan,
        KnowledgeValidationStatus afterStatus,
        DomainValidationStatusReport afterDomainStatus)
    {
        var blockers = new List<string>();
        blockers.AddRange(snapshot.BlockingValidationEvidenceTypes);

        if (snapshot.NeedsHumanReview)
        {
            blockers.Add("human_review_pending");
        }

        if (afterPlan is null)
        {
            blockers.Add("validation_plan_missing");
        }
        else if (!afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase)
            && !afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(afterPlan.Status);
        }

        if (afterStatus.ValidationPlansOpen > 0)
        {
            blockers.Add("validation_plans_open");
        }

        if (!afterDomainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(afterDomainStatus.DomainValidationHealth);
        }

        if (afterQuality is not null && afterQuality.ValidationScore < 0.68)
        {
            blockers.Add("validation_score_too_low");
        }

        if (afterQuality is not null && afterQuality.TrustScore < 0.64)
        {
            blockers.Add("trust_score_too_low");
        }

        if (afterQuality is not null && afterQuality.QualityScore < 0.64)
        {
            blockers.Add("quality_score_too_low");
        }

        if (snapshot.SourceCount < 2)
        {
            blockers.Add("second_independent_source_missing");
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(blocker => blocker, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string RecommendedNextAction(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeQualityItem? afterQuality,
        KnowledgeValidationPlan? afterPlan,
        KnowledgeValidationStatus afterStatus,
        DomainValidationStatusReport afterDomainStatus,
        bool applied)
    {
        if (snapshot.CanAutoComplete && applied)
        {
            return "validation_evidence_auto_completed";
        }

        if (snapshot.SourceCount < 2)
        {
            return "collect_second_independent_source";
        }

        if (snapshot.NeedsExternalData)
        {
            return "collect_missing_validation_data";
        }

        if (snapshot.NeedsHumanReview)
        {
            return "request_human_review";
        }

        if (afterPlan is null || (!afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase)
            && !afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase)))
        {
            return "close_validation_plans";
        }

        if (afterStatus.ValidationPlansOpen > 0)
        {
            return "drain_validation_queue";
        }

        if (!afterDomainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return "fix_domain_validation_health";
        }

        if (afterQuality is not null && afterQuality.ValidationScore < 0.68)
        {
            return "improve_validation_score";
        }

        if (afterQuality is not null && afterQuality.TrustScore < 0.64)
        {
            return "improve_trust_score";
        }

        if (afterQuality is not null && afterQuality.QualityScore < 0.64)
        {
            return "improve_quality_score";
        }

        return "monitor_validation_progress";
    }

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<ValidationEvidencePipelineItem> items,
        KnowledgeValidationStatus validationStatus,
        DomainValidationStatusReport domainStatus)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("knowledge_catalog_empty");
        }

        if (validationStatus.ValidationPlansOpen > 0)
        {
            warnings.Add($"validation_plans_open:{validationStatus.ValidationPlansOpen}");
        }

        if (validationStatus.ValidationTasksPending > 0)
        {
            warnings.Add($"validation_tasks_pending:{validationStatus.ValidationTasksPending}");
        }

        if (!domainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"domain_validation_health:{domainStatus.DomainValidationHealth}");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildEvidenceRefsForAutoCompletion(
        ValidationEvidencePipelineItem snapshot,
        string requirementType) =>
        [
            $"validation_pipeline:auto:{snapshot.KnowledgeItemId}:{requirementType}",
            $"validation_pipeline:auto:{requirementType}:completed"
        ];

    private static string OutcomeStatusForRequirement(string requirementType) =>
        requirementType switch
        {
            "source_verification" => "definition_validation_confirmed",
            "cross_source_confirmation" => "cross_source_confirmation_confirmed",
            "stale_check" => "fresh_validation_timestamp_available",
            "historical_test" => "historical_validation_available",
            "out_of_sample_test" => "out_of_sample_validation_available",
            "walkforward_test" => "walk_forward_validation_available",
            "cost_stress_test" => "transaction_cost_validation_available",
            "monte_carlo_test" => "monte_carlo_validation_available",
            "consistency_check" => "documentation_validation_available",
            "reference_check" => "documentation_validation_available",
            "citation_check" => "documentation_validation_available",
            "static_analysis" => "implementation_validation_available",
            "build_reference_check" => "implementation_validation_available",
            "test_presence_check" => "implementation_validation_available",
            "process_owner_review_stub" => "strategy_validation_available",
            "reproducibility_check" => "reproducibility_validation_available",
            _ => "validation_confirmed"
        };

    private static string CanonicalEvidenceTypeForRequirement(string domain, string requirementType) =>
        requirementType switch
        {
            "source_verification" or "cross_source_confirmation" => "definition_validation",
            "domain_review" => domain.ToLowerInvariant() switch
            {
                "trading" => "strategy_validation",
                "software" => "implementation_validation",
                "documentation" => "documentation_validation",
                "process" => "strategy_validation",
                "research" => "reproducibility_validation",
                _ => "definition_validation"
            },
            "stale_check" => "reproducibility_validation",
            "consistency_check" or "reference_check" or "citation_check" => "documentation_validation",
            "static_analysis" or "test_presence_check" or "build_reference_check" => "implementation_validation",
            "process_owner_review_stub" => "strategy_validation",
            "reproducibility_check" => "reproducibility_validation",
            "historical_test" => "historical_validation",
            "out_of_sample_test" => "out_of_sample_validation",
            "walkforward_test" => "walk_forward_validation",
            "cost_stress_test" => "transaction_cost_validation",
            "monte_carlo_test" => "monte_carlo_validation",
            _ => "definition_validation"
        };

    private static string StableId(string prefix, params string[] values)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("|", values.Select(value => value ?? string.Empty)));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()[..12];
        return $"{prefix}_{hash}";
    }

    private static string KnowledgeIdFromNodeId(string nodeId) =>
        nodeId.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase)
            ? nodeId["knowledge:".Length..]
            : nodeId;

    private static bool IsOpenPlan(string status) =>
        status.Equals("open", StringComparison.OrdinalIgnoreCase)
        || status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
        || status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockingMissingEvidence(string value) =>
        BlockingMissingEvidence.Contains(value)
        || value.Contains("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("source_metadata_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("domain_validation_metadata_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("oos_data_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("walkforward_report_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("historical_report_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("cost_stress_report_missing", StringComparison.OrdinalIgnoreCase)
        || value.Contains("monte_carlo_report_missing", StringComparison.OrdinalIgnoreCase);

    private static bool HasValidationEvidence(
        string evidenceType,
        KnowledgeQualityItem item,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan) =>
        latestValidation is not null
        || plan is not null
        || item.EvidenceRefs.Any(reference => reference.Contains(evidenceType, StringComparison.OrdinalIgnoreCase));

    private static string RequiredTaskType(string domain, string requirementType) =>
        requirementType switch
        {
            "source_verification" => "scan_knowledge_sources",
            "cross_source_confirmation" => "run_cross_source_check",
            "stale_check" => "run_domain_review",
            "historical_test" => "run_strategy_research",
            "out_of_sample_test" or "walkforward_test" => "run_walkforward_validation",
            "cost_stress_test" => "cost-stress-report",
            "monte_carlo_test" => "monte-carlo-report",
            "consistency_check" => "documentation_consistency_check",
            "reference_check" => "documentation_consistency_check",
            "citation_check" => "research_citation_check",
            "static_analysis" => "software_static_analysis",
            "build_reference_check" => "dotnet build",
            "test_presence_check" => "execute_validation_tasks",
            "process_owner_review_stub" => "process_review_stub",
            "reproducibility_check" => "evaluate_knowledge_quality",
            _ => domain.Equals("trading", StringComparison.OrdinalIgnoreCase) ? "run_domain_review" : "generate_domain_insights"
        };

    private static string MappedInternalTaskType(string domain, string requirementType) =>
        requirementType switch
        {
            "source_verification" => "scan_knowledge_sources",
            "cross_source_confirmation" => "run_cross_source_check",
            "stale_check" => "generate_domain_insights",
            "historical_test" => "run_strategy_research",
            "out_of_sample_test" or "walkforward_test" => "run_walkforward_validation",
            "cost_stress_test" => "cost-stress-report",
            "monte_carlo_test" => "monte-carlo-report",
            "consistency_check" => "documentation_consistency_check",
            "reference_check" => "documentation_consistency_check",
            "citation_check" => "research_citation_check",
            "static_analysis" => "software_static_analysis",
            "build_reference_check" => "dotnet build",
            "test_presence_check" => "execute_validation_tasks",
            "process_owner_review_stub" => "process_review_stub",
            "reproducibility_check" => "evaluate_knowledge_quality",
            _ => domain.Equals("trading", StringComparison.OrdinalIgnoreCase) ? "run_domain_review" : "generate_domain_insights"
        };

    private static string ValidationReadiness(
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan,
        IReadOnlyList<string> missing,
        DomainValidationStatusReport? domainStatus)
    {
        if (latestValidation is null)
        {
            return plan is null ? "validation_plan_missing" : "validation_missing";
        }

        if (latestValidation.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            var blockingWarnings = latestValidation.Warnings.Where(IsBlockingMissingEvidence).ToList();
            if (blockingWarnings.Count == 0 && missing.All(item => !IsBlockingMissingEvidence(item)))
            {
                return missing.Any(item => NonCriticalMissingEvidence.Contains(item))
                    ? "completed_with_missing_noncritical_evidence"
                    : "passed";
            }

            return "blocked_waiting_for_evidence";
        }

        if (latestValidation.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
        {
            if (latestValidation.Warnings.Any(IsBlockingMissingEvidence))
            {
                return "blocked_waiting_for_evidence";
            }

            return "completed_with_missing_noncritical_evidence";
        }

        if (latestValidation.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            return "validation_failed";
        }

        if (plan is not null && plan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (domainStatus is not null && domainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        return "blocked";
    }

    private static IReadOnlyList<string> PlannedEvidenceTypes_Legacy(KnowledgeQualityItem item, KnowledgeValidationPlan? plan, int sourceCount)
    {
        var planned = new List<string>();
        if (sourceCount >= 2)
        {
            planned.Add("definition_validation");
        }

        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            planned.AddRange(["historical_validation", "out_of_sample_validation", "walk_forward_validation", "transaction_cost_validation", "monte_carlo_validation"]);
        }
        else if (item.Domain.Equals("software", StringComparison.OrdinalIgnoreCase))
        {
            planned.Add("implementation_validation");
        }
        else if (item.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        {
            planned.Add("documentation_validation");
        }
        else if (item.Domain.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            planned.Add("strategy_validation");
        }
        else if (item.Domain.Equals("research", StringComparison.OrdinalIgnoreCase))
        {
            planned.Add("reproducibility_validation");
        }

        if (plan is null)
        {
            planned.Add("validation_plan_missing");
        }

        return planned.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> AutoCompletableEvidence_Legacy(
        KnowledgeQualityItem item,
        int sourceCount,
        KnowledgeValidationExecutionResult? latestValidation,
        HumanReviewEvidence? humanReview,
        IReadOnlyList<ContradictionRecord> contradictions,
        KnowledgeValidationPlan? plan,
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<string> plannedEvidence)
    {
        var auto = new List<string>();
        if (sourceCount >= 2 && contradictions.Count == 0)
        {
            auto.Add("definition_validation");
            auto.Add("source_verification");
            auto.Add("cross_source_confirmation");
            auto.Add("stale_check");
        }

        if (item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            if (HasValidationEvidence("historical_validation", item, latestValidation, plan))
            {
                auto.Add("historical_validation");
            }

            if (HasValidationEvidence("out_of_sample_validation", item, latestValidation, plan))
            {
                auto.Add("out_of_sample_validation");
                auto.Add("walk_forward_validation");
            }

            if (HasValidationEvidence("transaction_cost_validation", item, latestValidation, plan))
            {
                auto.Add("transaction_cost_validation");
            }

            if (HasValidationEvidence("monte_carlo_validation", item, latestValidation, plan))
            {
                auto.Add("monte_carlo_validation");
            }
        }

        if (humanReview?.Result.Equals("approved", StringComparison.OrdinalIgnoreCase) == true)
        {
            auto.Add("documentation_validation");
            auto.Add("strategy_validation");
            auto.Add("reproducibility_validation");
        }

        return auto
            .Where(entry => plannedEvidence.Contains(entry, StringComparer.OrdinalIgnoreCase) || missingEvidence.Contains(entry, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildRemainingBlockers(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeQualityItem? afterQuality,
        KnowledgeValidationPlan? afterPlan,
        KnowledgeValidationStatus afterStatus,
        DomainValidationStatusReport afterDomainStatus)
    {
        var blockers = new List<string>();

        if (afterQuality is null || afterQuality.LastValidatedUtc is null)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (snapshot.SourceCount < 2)
        {
            blockers.Add("second_independent_source_missing");
        }

        if (snapshot.NeedsHumanReview)
        {
            blockers.Add("human_review_pending");
        }

        if (afterPlan is null)
        {
            blockers.Add("validation_plan_missing");
        }
        else if (!afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase)
            && !afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(afterPlan.Status);
        }

        if (afterStatus.ValidationPlansOpen > 0)
        {
            blockers.Add("validation_plans_open");
        }

        if (!afterDomainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(afterDomainStatus.DomainValidationHealth);
        }

        if (afterQuality is not null && afterQuality.ValidationScore < 0.68)
        {
            blockers.Add("validation_score_too_low");
        }

        if (afterQuality is not null && afterQuality.TrustScore < 0.64)
        {
            blockers.Add("trust_score_too_low");
        }

        if (afterQuality is not null && afterQuality.QualityScore < 0.64)
        {
            blockers.Add("quality_score_too_low");
        }

        if (snapshot.BlockingValidationEvidenceTypes.Any(item => item.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add("blocking_contradiction");
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(blocker => blocker, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string RecommendedNextAction_Legacy(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeQualityItem? afterQuality,
        KnowledgeValidationPlan? afterPlan,
        KnowledgeValidationStatus afterStatus,
        DomainValidationStatusReport afterDomainStatus,
        bool applied)
    {
        if (snapshot.CanAutoComplete && applied)
        {
            return "validation_evidence_auto_completed";
        }

        if (snapshot.SourceCount < 2)
        {
            return "collect_second_independent_source";
        }

        if (snapshot.NeedsExternalData)
        {
            return "collect_missing_validation_data";
        }

        if (snapshot.NeedsHumanReview)
        {
            return "request_human_review";
        }

        if (afterPlan is null || (!afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase)
            && !afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase)))
        {
            return "close_validation_plans";
        }

        if (afterStatus.ValidationPlansOpen > 0)
        {
            return "drain_validation_queue";
        }

        if (!afterDomainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return "fix_domain_validation_health";
        }

        if (afterQuality is not null && afterQuality.ValidationScore < 0.68)
        {
            return "improve_validation_score";
        }

        if (afterQuality is not null && afterQuality.TrustScore < 0.64)
        {
            return "improve_trust_score";
        }

        if (afterQuality is not null && afterQuality.QualityScore < 0.64)
        {
            return "improve_quality_score";
        }

        return "monitor_validation_progress";
    }

    private static IReadOnlyList<string> BuildWarnings_Legacy(
        IReadOnlyList<ValidationEvidencePipelineItem> items,
        KnowledgeValidationStatus validationStatus,
        DomainValidationStatusReport domainStatus)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("knowledge_catalog_empty");
        }

        if (validationStatus.ValidationPlansOpen > 0)
        {
            warnings.Add($"validation_plans_open:{validationStatus.ValidationPlansOpen}");
        }

        if (validationStatus.ValidationTasksPending > 0)
        {
            warnings.Add($"validation_tasks_pending:{validationStatus.ValidationTasksPending}");
        }

        if (!domainStatus.DomainValidationHealth.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"domain_validation_health:{domainStatus.DomainValidationHealth}");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static (bool Applied, int CreatedPlans, int CreatedExecutions) ApplyPlanUpdates(
        IReadOnlyList<ValidationEvidencePlanUpdate> updates,
        KnowledgeValidationPlanReport existingPlanReport,
        DateTimeOffset now,
        string validationPlansPath,
        string validationRequirementsPath,
        string executionLogPath,
        KnowledgeValidationEvidenceWriter evidenceWriter,
        KnowledgeValidationStrategy validationStrategy,
        StoragePaths storagePaths)
    {
        if (updates.Count == 0)
        {
            return (false, 0, 0);
        }

        var plans = existingPlanReport.Plans.ToList();
        var createdPlans = 0;
        var allExecutions = new List<KnowledgeValidationExecutionResult>();

        foreach (var update in updates)
        {
            var index = plans.FindIndex(plan => plan.PlanId.Equals(update.Plan.PlanId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                plans[index] = update.Plan;
            }
            else
            {
                plans.Add(update.Plan);
                createdPlans++;
            }

            allExecutions.AddRange(update.Executions);
        }

        var updatedPlanReport = existingPlanReport with
        {
            UpdatedAtUtc = now,
            Plans = plans
                .OrderByDescending(plan => plan.Priority)
                .ThenBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            OpenPlans = plans.Count(plan =>
                plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
                || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)),
            TrustedCandidateCount = plans.Count(plan => plan.TargetStatus.Equals("trusted_candidate", StringComparison.OrdinalIgnoreCase)),
            KnowledgeItemsNeedingOos = plans.Count(plan => plan.Requirements.Any(requirement => requirement.RequirementType is "out_of_sample_test" or "walkforward_test")),
            KnowledgeItemsNeedingSourceCheck = plans.Count(plan => plan.Requirements.Any(requirement => requirement.RequirementType is "source_verification" or "cross_source_confirmation")),
            MostCommonMissingEvidence = plans
                .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase) || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase) || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
                .SelectMany(plan => plan.MissingEvidence)
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(group => $"{group.Key}:{group.Count()}")
                .ToList()
        };

        File.WriteAllText(validationPlansPath, JsonSerializer.Serialize(updatedPlanReport, JsonDefaults.WriteOptions));
        var requirementsReport = new KnowledgeValidationRequirementsReport(
            ReportVersion: "knowledge_validation_requirements_v1",
            UpdatedAtUtc: now,
            TotalRequirements: plans.SelectMany(plan => plan.Requirements).Count(),
            Requirements: plans.SelectMany(plan => plan.Requirements).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(validationRequirementsPath, JsonSerializer.Serialize(requirementsReport, JsonDefaults.WriteOptions));

        if (allExecutions.Count > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(executionLogPath)!);
            foreach (var execution in allExecutions)
            {
                File.AppendAllText(executionLogPath, JsonSerializer.Serialize(execution, JsonDefaults.WriteOptions) + Environment.NewLine);
            }

            evidenceWriter.MergeExecutionEvidence(allExecutions);
        }

        _ = validationStrategy.BuildStatus();
        _ = new DomainKnowledgeValidationService(storagePaths).BuildStatus();
        _ = new CognitiveCoreService(storagePaths).BuildStatus();
        _ = new KnowledgeQualityEngine(storagePaths).Run();
        return (true, createdPlans, allExecutions.Count);
    }

    private ValidationEvidencePipelineItem ApplyProjection(
        ValidationEvidencePipelineItem snapshot,
        KnowledgeQualityItem? afterQuality,
        KnowledgeValidationPlan? afterPlan,
        KnowledgeValidationStatus afterStatus,
        DomainValidationStatusReport afterDomainStatus,
        bool applied) =>
        snapshot with
        {
            PlanStatusAfter = afterPlan?.Status ?? snapshot.PlanStatusAfter,
            ValidationReadinessAfter = ValidationReadiness(null, afterPlan, snapshot.RemainingBlockers, afterDomainStatus),
            DomainValidationHealthAfter = afterDomainStatus.DomainValidationHealth,
            ValidationScoreAfter = afterQuality?.ValidationScore ?? snapshot.ValidationScoreAfter,
            TrustScoreAfter = afterQuality?.TrustScore ?? snapshot.TrustScoreAfter,
            QualityScoreAfter = afterQuality?.QualityScore ?? snapshot.QualityScoreAfter,
            OpenPlansAfter = afterPlan is null ? snapshot.OpenPlansAfter : (afterPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase) || afterPlan.Status.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase) ? 0 : 1),
            RemainingBlockers = BuildRemainingBlockers(snapshot, afterQuality, afterPlan, afterStatus, afterDomainStatus),
            RecommendedNextAction = RecommendedNextAction(snapshot, afterQuality, afterPlan, afterStatus, afterDomainStatus, applied)
        };

    private static IReadOnlyList<ValidationEvidenceScoreChange> BuildChanges(
        IReadOnlyList<ValidationEvidencePipelineItem> items,
        Func<ValidationEvidencePipelineItem, double> before,
        Func<ValidationEvidencePipelineItem, double> after) =>
        items
            .Where(item => Math.Abs(after(item) - before(item)) > 0.0001)
            .Select(item => new ValidationEvidenceScoreChange(
                item.KnowledgeItemId,
                item.Domain,
                item.Title,
                before(item),
                after(item),
                Math.Round(after(item) - before(item), 4)))
            .ToList();

    private static string RequirementTypeForEvidenceType(string domain, string evidenceType) =>
        evidenceType switch
        {
            "definition_validation" => "source_verification",
            "documentation_validation" => "consistency_check",
            "implementation_validation" => "static_analysis",
            "strategy_validation" => domain.ToLowerInvariant() switch
            {
                "trading" => "domain_review",
                "process" => "process_owner_review_stub",
                _ => "domain_review"
            },
            "reproducibility_validation" => "reproducibility_check",
            "historical_validation" => "historical_test",
            "out_of_sample_validation" => "out_of_sample_test",
            "walk_forward_validation" => "walkforward_test",
            "transaction_cost_validation" => "cost_stress_test",
            "monte_carlo_validation" => "monte_carlo_test",
            _ => "domain_review"
        };

    private static string? FindValidationQueueItemId(string taskId)
    {
        return null;
    }

    private static IReadOnlyList<string> BuildEvidenceRefsForAutoCompletion_Legacy(
        ValidationEvidencePipelineItem snapshot,
        string requirementType) =>
        [
            $"validation_pipeline:auto:{snapshot.KnowledgeItemId}:{requirementType}",
            $"validation_pipeline:auto:{requirementType}:completed"
        ];

    private static string? CanonicalEvidenceTypeFromRequirementType(string requirementType) =>
        requirementType switch
        {
            "source_verification" or "cross_source_confirmation" => "definition_validation",
            "domain_review" => "strategy_validation",
            "stale_check" => "reproducibility_validation",
            "consistency_check" or "reference_check" or "citation_check" => "documentation_validation",
            "static_analysis" or "test_presence_check" or "build_reference_check" => "implementation_validation",
            "process_owner_review_stub" => "strategy_validation",
            "reproducibility_check" => "reproducibility_validation",
            "historical_test" => "historical_validation",
            "out_of_sample_test" => "out_of_sample_validation",
            "walkforward_test" => "walk_forward_validation",
            "cost_stress_test" => "transaction_cost_validation",
            "monte_carlo_test" => "monte_carlo_validation",
            _ => null
        };

    private static string StableId_Legacy(string prefix, params string[] values)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("|", values.Select(value => value ?? string.Empty)));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()[..12];
        return $"{prefix}_{hash}";
    }

    private KnowledgeEvidenceReport LoadKnowledgeEvidence() =>
        LoadJson(KnowledgeEvidencePath, () => new KnowledgeEvidenceReport(
            ReportVersion: "knowledge_evidence_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Evidence: [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));

    private SourceConfirmationReport LoadSourceConfirmations() =>
        LoadJson(SourceConfirmationsPath, () => new SourceConfirmationReport(
            ReportVersion: "source_confirmation_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ItemsAnalyzed: 0,
            ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            Results: [],
            Warnings: ["source_confirmation_missing"],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));

    private EvidenceGraph LoadEvidenceGraph() =>
        LoadJson(EvidenceGraphPath, () => new EvidenceGraph(
            GraphVersion: "evidence_graph_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            KnowledgeItems: 0,
            SourceNodes: 0,
            ValidationNodes: 0,
            Nodes: 0,
            Links: 0,
            EvidenceNodes: [],
            EvidenceLinks: [],
            Sources: [],
            Warnings: ["evidence_graph_missing"],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));

    private static T LoadJson<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path))
        {
            return fallback();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions) ?? fallback();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return fallback();
        }
    }

    private void WriteReport(ValidationEvidencePipelineReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "validation_evidence_pipeline");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "validation_evidence_pipeline_report.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "validation_evidence_pipeline_report.md"), markdown);
        }
    }

    private static string BuildMarkdown(ValidationEvidencePipelineReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Validation Evidence Pipeline Report");
        sb.AppendLine();
        sb.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Loaded Items: {report.LoadedItems}");
        sb.AppendLine($"- Validation Completed: {report.ValidationCompleted}");
        sb.AppendLine($"- Validation Pending: {report.ValidationPending}");
        sb.AppendLine($"- Waiting for External Data: {report.ValidationWaitingForExternalData}");
        sb.AppendLine($"- Waiting for Human Review: {report.ValidationWaitingForHumanReview}");
        sb.AppendLine($"- Plans Created: {report.PlansCreated}");
        sb.AppendLine($"- Validation Executions Created: {report.ValidationExecutionsCreated}");
        sb.AppendLine();
        sb.AppendLine("## Focus Items");
        foreach (var item in report.FocusItems)
        {
            sb.AppendLine($"- {item.KnowledgeItemId}: {item.ValidationReadinessBefore} -> {item.ValidationReadinessAfter}; validation {item.ValidationScoreBefore:0.###} -> {item.ValidationScoreAfter:0.###}");
            sb.AppendLine($"  - blockers: {string.Join(", ", item.RemainingBlockers.Take(5))}");
            sb.AppendLine($"  - next: {item.RecommendedNextAction}");
        }

        sb.AppendLine();
        sb.AppendLine("## Remaining Blockers");
        foreach (var blocker in report.RemainingBlockers.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {blocker.Key}: {blocker.Value}");
        }

        return sb.ToString();
    }
}
