using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeValidationStateSyncItem(
    string KnowledgeItemId,
    string Domain,
    string Title,
    string MismatchType,
    string ValidationPlanStatusBefore,
    string ValidationPlanStatusAfter,
    string ValidationStatusBefore,
    string ValidationStatusAfter,
    string DomainValidationStatusBefore,
    string DomainValidationStatusAfter,
    DateTimeOffset? LastValidatedUtcBefore,
    DateTimeOffset? LastValidatedUtcAfter,
    int SourceCount,
    IReadOnlyList<string> BlockersBefore,
    IReadOnlyList<string> BlockersAfter,
    IReadOnlyList<string> RemovedBlockers,
    bool PlanCreated,
    bool PlanSynchronized,
    bool BlockersNormalized,
    bool SkippedTrueContradiction,
    bool SkippedHumanReviewRequired,
    string RecommendedNextAction,
    IReadOnlyList<string> Warnings);

public sealed record KnowledgeValidationStateSyncReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedIssues,
    int SelectedIssues,
    int PlansCreated,
    int PlansSynchronized,
    int BlockersRemoved,
    int BlockersNormalized,
    int SkippedTrueContradictions,
    int SkippedHumanReviewRequired,
    int BeforeIssueCount,
    int AfterIssueCount,
    IReadOnlyDictionary<string, int> BeforeIssueCountsByType,
    IReadOnlyDictionary<string, int> AfterIssueCountsByType,
    IReadOnlyList<KnowledgeValidationStateSyncItem> Items,
    IReadOnlyList<string> Warnings,
    string DiagnosticsPath,
    string CatalogPath,
    string QualityPath,
    string EvidencePath,
    string ValidationPlansPath,
    string ValidationStatusPath,
    string SourceConfirmationsPath,
    string MasterStatusPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool DryRun,
    bool Applied);

public sealed class KnowledgeValidationStateSyncService
{
    private static readonly IReadOnlySet<string> SyncMismatchTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "validation_plan_missing",
        "blocker_mismatch"
    };

    private static readonly IReadOnlySet<string> StaleBlockers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fresh_validation_timestamp_missing",
        "fresh_validation_timestamp",
        "second_independent_source_missing",
        "source_metadata_missing",
        "validation_plan_missing",
        "validation_plan_or_requirement_missing"
    };

    private readonly StoragePaths _storagePaths;

    public KnowledgeValidationStateSyncService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_validation_state_sync");

    public string ReportPath => Path.Combine(Root, "knowledge_validation_state_sync_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_validation_state_sync_report.md");

    public string DiagnosticsPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_repair_diagnostics", "knowledge_state_repair_diagnostics_report.json");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string EvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string ValidationStatusPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_validation_status.json");

    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");

    public string MasterStatusPath => Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json");

    public KnowledgeValidationStateSyncReport Run(bool apply, bool dryRun)
    {
        Directory.CreateDirectory(Root);

        var updatedAt = DateTimeOffset.UtcNow;
        var diagnosticsService = new KnowledgeStateRepairDiagnosticsService(_storagePaths);
        var diagnosticsBefore = diagnosticsService.LoadLatestReport() ?? diagnosticsService.Run();
        var selectedIssues = diagnosticsBefore.Items
            .Where(item => SyncMismatchTypes.Contains(item.MismatchType) && item.AutoRepairable)
            .OrderByDescending(item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalog = new KnowledgeCatalog(_storagePaths).LoadItems().ToList();
        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var qualityReport = qualityEngine.LoadOrCreateReport();
        var evidenceReport = LoadJson<KnowledgeEvidenceReport>(EvidencePath);
        var validationStrategy = new KnowledgeValidationStrategy(_storagePaths);
        var validationPlanReport = validationStrategy.LoadPlanReport() ?? validationStrategy.GeneratePlans(50);
        var validationStatusBefore = validationStrategy.LoadStatus() ?? validationStrategy.BuildStatus();
        var sourceConfirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var validationExecutions = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);

        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var qualityById = qualityReport.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidenceReport?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
        var planById = validationPlanReport.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);
        var confirmationById = sourceConfirmations?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var latestExecutionById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var executionsById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var beforeIssues = diagnosticsBefore.Items.Count;
        var beforeCountsByType = diagnosticsBefore.Items
            .GroupBy(item => item.MismatchType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var items = new List<KnowledgeValidationStateSyncItem>();
        var mutatedQuality = qualityReport.Items.ToList();
        var mutatedPlans = validationPlanReport.Plans.ToList();
        var plansCreated = 0;
        var plansSynchronized = 0;
        var blockersRemoved = 0;
        var blockersNormalized = 0;
        var skippedTrueContradictions = 0;
        var skippedHumanReviewRequired = 0;

        foreach (var issue in selectedIssues)
        {
            var knowledgeId = issue.KnowledgeItemId;
            var warnings = new List<string>();
            var planCreated = false;
            var planSynchronized = false;
            var blockersNormalizedForItem = false;
            var removedBlockers = new List<string>();

            var catalogItem = catalogById.GetValueOrDefault(knowledgeId);
            var qualityItem = qualityById.GetValueOrDefault(knowledgeId);
            var planBefore = planById.GetValueOrDefault(knowledgeId);
            var confirmation = confirmationById.GetValueOrDefault(knowledgeId);
            var latestExecution = latestExecutionById.GetValueOrDefault(knowledgeId);
            var executions = executionsById.GetValueOrDefault(knowledgeId) ?? [];
            var evidence = evidenceById.GetValueOrDefault(knowledgeId);
            var currentTitle = catalogItem?.Title ?? qualityItem?.Title ?? issue.Title;
            var currentDomain = catalogItem?.Domain ?? qualityItem?.Domain ?? "unknown";
            var sourceCount = confirmation?.SourceCount ?? 0;

            if (issue.Blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
            {
                skippedTrueContradictions++;
                items.Add(BuildItem(
                    issue,
                    currentDomain,
                    currentTitle,
                    planBefore,
                    qualityItem,
                    confirmation,
                    latestExecution,
                    sourceCount,
                    qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                    qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                    issue.Blockers.Any(blocker => blocker.Contains("domain_validation", StringComparison.OrdinalIgnoreCase))
                        ? "blocked_waiting_for_evidence"
                        : "passed",
                    issue.Blockers.Any(blocker => blocker.Contains("domain_validation", StringComparison.OrdinalIgnoreCase))
                        ? "blocked_waiting_for_evidence"
                        : "passed",
                    [],
                    [],
                    [],
                    planCreated: false,
                    planSynchronized: false,
                    blockersNormalized: false,
                    skippedTrueContradiction: true,
                    skippedHumanReviewRequired: false,
                    recommendedNextAction: "human_review_required",
                    warnings: ["true_contradiction_skipped"]));
                continue;
            }

            if (issue.Blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)))
            {
                skippedHumanReviewRequired++;
                items.Add(BuildItem(
                    issue,
                    currentDomain,
                    currentTitle,
                    planBefore,
                    qualityItem,
                    confirmation,
                    latestExecution,
                    sourceCount,
                    qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                    qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                    issue.Blockers.Any(blocker => blocker.Contains("domain_validation", StringComparison.OrdinalIgnoreCase))
                        ? "blocked_waiting_for_evidence"
                        : "passed",
                    issue.Blockers.Any(blocker => blocker.Contains("domain_validation", StringComparison.OrdinalIgnoreCase))
                        ? "blocked_waiting_for_evidence"
                        : "passed",
                    [],
                    [],
                    [],
                    planCreated: false,
                    planSynchronized: false,
                    blockersNormalized: false,
                    skippedTrueContradiction: false,
                    skippedHumanReviewRequired: true,
                    recommendedNextAction: "await_human_review",
                    warnings: ["human_review_pending_skipped"]));
                continue;
            }

            var blockersBefore = ComputeBlockers(qualityItem, planBefore, confirmation, latestExecution, evidence, issue.Blockers);
            var validationExecutionPresent = latestExecution is not null;
            var hasPolicyApprovedSecondSource = HasPolicyApprovedSecondSource(confirmation);
            var lastValidatedUtc = latestExecution?.CompletedAtUtc ?? qualityItem?.LastValidatedUtc ?? catalogItem?.LastValidatedUtc;

            var updatedPlan = planBefore;
            if (updatedPlan is null)
            {
                updatedPlan = BuildMinimalPlan(issue, currentDomain, currentTitle, lastValidatedUtc, blockersBefore, validationExecutionPresent);
                planCreated = true;
                plansCreated++;
            }
            else
            {
                var updatedMissingEvidence = NormalizeMissingEvidence(updatedPlan.MissingEvidence, blockersBefore, validationExecutionPresent, hasPolicyApprovedSecondSource);
                var status = PlanStatusFor(updatedMissingEvidence, updatedPlan.Requirements, validationExecutionPresent);
                if (!SequenceEqualIgnoreCase(updatedPlan.MissingEvidence, updatedMissingEvidence)
                    || !updatedPlan.Status.Equals(status, StringComparison.OrdinalIgnoreCase)
                    || updatedPlan.UpdatedAtUtc < updatedAt)
                {
                    updatedPlan = updatedPlan with
                    {
                        MissingEvidence = updatedMissingEvidence,
                        Status = status,
                        UpdatedAtUtc = updatedAt
                    };
                    planSynchronized = true;
                    plansSynchronized++;
                }
            }

            var updatedQuality = qualityItem;
            if (updatedQuality is not null && lastValidatedUtc is not null && updatedQuality.LastValidatedUtc != lastValidatedUtc)
            {
                updatedQuality = updatedQuality with { LastValidatedUtc = lastValidatedUtc };
                if (qualityById.TryGetValue(knowledgeId, out var originalQuality))
                {
                    var qualityIndex = mutatedQuality.FindIndex(item => item.KnowledgeId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
                    if (qualityIndex >= 0)
                    {
                        mutatedQuality[qualityIndex] = updatedQuality;
                    }
                }
            }

            if (updatedPlan is not null)
            {
                var planIndex = mutatedPlans.FindIndex(item => item.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
                if (planIndex >= 0)
                {
                    mutatedPlans[planIndex] = updatedPlan;
                }
                else
                {
                    mutatedPlans.Add(updatedPlan);
                }
            }

            var blockersAfter = ComputeBlockers(
                updatedQuality,
                updatedPlan,
                confirmation,
                latestExecution,
                evidence,
                issue.Blockers);

            removedBlockers.AddRange(blockersBefore.Where(blocker => !blockersAfter.Contains(blocker, StringComparer.OrdinalIgnoreCase)));
            blockersRemoved += removedBlockers.Count;
            blockersNormalizedForItem = removedBlockers.Count > 0;
            if (blockersNormalizedForItem)
            {
                blockersNormalized++;
            }

            var domainValidationBefore = DomainValidationStatusBefore(blockersBefore);
            var domainValidationAfter = DomainValidationStatusAfter(blockersAfter, hasPolicyApprovedSecondSource);
            var validationPlanStatusBefore = planBefore?.Status ?? "validation_plan_missing";
            var validationPlanStatusAfter = updatedPlan?.Status ?? validationPlanStatusBefore;

            items.Add(BuildItem(
                issue,
                currentDomain,
                currentTitle,
                planBefore,
                qualityItem,
                confirmation,
                latestExecution,
                sourceCount,
                qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                updatedQuality?.LifecycleStatus ?? qualityItem?.LifecycleStatus ?? issue.ValidationStatus,
                domainValidationBefore,
                domainValidationAfter,
                blockersBefore,
                blockersAfter,
                removedBlockers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                planCreated,
                planSynchronized,
                blockersNormalizedForItem,
                skippedTrueContradiction: false,
                skippedHumanReviewRequired: false,
                recommendedNextAction: RecommendedNextAction(blockersAfter, hasPolicyApprovedSecondSource),
                warnings: BuildWarnings(blockersAfter, confirmation, latestExecution, updatedPlan)));
        }

        if (apply && !dryRun)
        {
            WriteQualityReport(qualityEngine.QualityPath, qualityReport with
            {
                UpdatedAtUtc = updatedAt,
                Items = mutatedQuality
                    .OrderByDescending(item => item.QualityScore)
                    .ThenBy(item => item.Domain, StringComparer.Ordinal)
                    .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
                    .ToList()
            });

            WriteValidationPlanReport(validationStrategy.PlansPath, validationPlanReport with
            {
                UpdatedAtUtc = updatedAt,
                Plans = mutatedPlans
                    .OrderByDescending(plan => plan.Priority)
                    .ThenBy(plan => plan.KnowledgeItemId, StringComparer.Ordinal)
                    .ToList(),
                OpenPlans = mutatedPlans.Count(IsOpenPlan),
                TrustedCandidateCount = mutatedPlans.Count(plan => plan.TargetStatus.Equals("trusted_candidate", StringComparison.OrdinalIgnoreCase)),
                KnowledgeItemsNeedingOos = mutatedPlans.Count(plan => plan.Requirements.Any(req => req.RequirementType is "out_of_sample_test" or "walkforward_test")),
                KnowledgeItemsNeedingSourceCheck = mutatedPlans.Count(plan => plan.Requirements.Any(req => req.RequirementType is "source_verification" or "cross_source_confirmation")),
                MostCommonMissingEvidence = mutatedPlans
                    .Where(plan => IsOpenPlan(plan))
                    .SelectMany(plan => plan.MissingEvidence)
                    .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .Select(group => $"{group.Key}:{group.Count()}")
                    .ToList()
            });

            _ = validationStrategy.BuildStatus();
            _ = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
            _ = new CognitiveCoreService(_storagePaths).BuildStatus();
        }

        var diagnosticsAfter = (apply && !dryRun ? diagnosticsService.Run() : diagnosticsBefore);
        var afterIssues = diagnosticsAfter.Items.Count(item =>
            SyncMismatchTypes.Contains(item.MismatchType) && item.AutoRepairable);
        var afterCountsByType = diagnosticsAfter.Items
            .GroupBy(item => item.MismatchType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var report = new KnowledgeValidationStateSyncReport(
            ReportVersion: "knowledge_validation_state_sync_v1",
            UpdatedAtUtc: updatedAt,
            Status: dryRun ? "dry_run_ready" : "applied",
            LoadedIssues: diagnosticsBefore.TotalIssues,
            SelectedIssues: selectedIssues.Count,
            PlansCreated: plansCreated,
            PlansSynchronized: plansSynchronized,
            BlockersRemoved: blockersRemoved,
            BlockersNormalized: blockersNormalized,
            SkippedTrueContradictions: skippedTrueContradictions,
            SkippedHumanReviewRequired: skippedHumanReviewRequired,
            BeforeIssueCount: beforeIssues,
            AfterIssueCount: afterIssues,
            BeforeIssueCountsByType: beforeCountsByType,
            AfterIssueCountsByType: afterCountsByType,
            Items: items,
            Warnings: BuildWarnings(items),
            DiagnosticsPath: DiagnosticsPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            EvidencePath: EvidencePath,
            ValidationPlansPath: ValidationPlansPath,
            ValidationStatusPath: ValidationStatusPath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            MasterStatusPath: MasterStatusPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            DryRun: dryRun || !apply,
            Applied: apply && !dryRun);

        WriteReport(report);
        return report;
    }

    public KnowledgeValidationStateSyncReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeValidationStateSyncReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static KnowledgeValidationStateSyncItem BuildItem(
        KnowledgeStateRepairDiagnosticItem issue,
        string domain,
        string title,
        KnowledgeValidationPlan? planBefore,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestExecution,
        int sourceCount,
        string validationStatusBefore,
        string validationStatusAfter,
        string domainValidationStatusBefore,
        string domainValidationStatusAfter,
        IReadOnlyList<string> blockersBefore,
        IReadOnlyList<string> blockersAfter,
        IReadOnlyList<string> removedBlockers,
        bool planCreated,
        bool planSynchronized,
        bool blockersNormalized,
        bool skippedTrueContradiction,
        bool skippedHumanReviewRequired,
        string recommendedNextAction,
        IReadOnlyList<string> warnings)
    {
        return new KnowledgeValidationStateSyncItem(
            KnowledgeItemId: issue.KnowledgeItemId,
            Domain: domain,
            Title: title,
            MismatchType: issue.MismatchType,
            ValidationPlanStatusBefore: planBefore?.Status ?? "validation_plan_missing",
            ValidationPlanStatusAfter: planBefore?.Status ?? "validation_plan_missing",
            ValidationStatusBefore: validationStatusBefore,
            ValidationStatusAfter: validationStatusAfter,
            DomainValidationStatusBefore: domainValidationStatusBefore,
            DomainValidationStatusAfter: domainValidationStatusAfter,
            LastValidatedUtcBefore: qualityItem?.LastValidatedUtc ?? latestExecution?.CompletedAtUtc,
            LastValidatedUtcAfter: qualityItem?.LastValidatedUtc ?? latestExecution?.CompletedAtUtc,
            SourceCount: sourceCount,
            BlockersBefore: blockersBefore,
            BlockersAfter: blockersAfter,
            RemovedBlockers: removedBlockers,
            PlanCreated: planCreated,
            PlanSynchronized: planSynchronized,
            BlockersNormalized: blockersNormalized,
            SkippedTrueContradiction: skippedTrueContradiction,
            SkippedHumanReviewRequired: skippedHumanReviewRequired,
            RecommendedNextAction: recommendedNextAction,
            Warnings: warnings);
    }

    private static KnowledgeValidationPlan BuildMinimalPlan(
        KnowledgeStateRepairDiagnosticItem issue,
        string domain,
        string title,
        DateTimeOffset? lastValidatedUtc,
        IReadOnlyList<string> blockers,
        bool validationExecutionPresent)
    {
        var now = DateTimeOffset.UtcNow;
        var missingEvidence = blockers
            .Where(blocker => StaleBlockers.Contains(blocker) || blocker.EndsWith("_missing", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingEvidence.Count == 0)
        {
            missingEvidence.Add("validation_plan_missing");
        }

        var requirements = missingEvidence.Select(blocker => new KnowledgeValidationRequirement(
            RequirementId: StableId("validation_requirement", issue.KnowledgeItemId, blocker),
            KnowledgeItemId: issue.KnowledgeItemId,
            RequirementType: blocker,
            Status: blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
                ? "needs_more_data"
                : "in_progress",
            Reason: $"repaired_from_{blocker}",
            MissingEvidence: [blocker],
            EvidenceRefs: [],
            RequiredTaskType: "validation_review",
            MappedInternalTaskType: "execute_validation_tasks",
            Priority: 0.5,
            NoTradingExecution: true,
            HumanReviewRequired: true)).ToList();

        var tasks = requirements.Select(requirement => new KnowledgeValidationTask(
            TaskId: StableId("validation_task", issue.KnowledgeItemId, requirement.RequirementId),
            KnowledgeItemId: issue.KnowledgeItemId,
            TaskType: requirement.RequiredTaskType,
            Domain: domain,
            RequirementType: requirement.RequirementType,
            Status: "pending",
            Priority: requirement.Priority,
            ExpectedEvidence: requirement.MissingEvidence.FirstOrDefault() ?? requirement.RequirementType,
            MappedInternalTaskType: requirement.MappedInternalTaskType,
            SourceRefs: [],
            NoTradingExecution: true,
            HumanReviewRequired: true)).ToList();

        return new KnowledgeValidationPlan(
            PlanId: StableId("validation_plan", issue.KnowledgeItemId),
            KnowledgeItemId: issue.KnowledgeItemId,
            Domain: domain,
            Title: title,
            CurrentStatus: issue.CurrentStatus,
            TargetStatus: "needs_review",
            MissingEvidence: missingEvidence,
            Requirements: requirements,
            RequiredTasks: tasks,
            Priority: 0.5,
            ExpectedQualityDelta: 0.05,
            RelatedGoalId: "knowledge_validation_state_sync",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Status: validationExecutionPresent ? "in_progress" : "needs_more_data",
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            SkippedByRouterReasons: []);
    }

    private static IReadOnlyList<string> NormalizeMissingEvidence(
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<string> blockers,
        bool validationExecutionPresent,
        bool hasPolicyApprovedSecondSource)
    {
        var filtered = missingEvidence
            .Where(entry => !entry.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.Equals("fresh_validation_timestamp", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.Equals("source_metadata_missing", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (validationExecutionPresent || blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)))
        {
            filtered.RemoveAll(entry => entry.Equals("validation_result_missing", StringComparison.OrdinalIgnoreCase));
        }

        if (hasPolicyApprovedSecondSource || blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)))
        {
            filtered.RemoveAll(entry => entry.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PlanStatusFor(
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<KnowledgeValidationRequirement> requirements,
        bool validationExecutionPresent)
    {
        if (requirements.All(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase)))
        {
            return "ready_for_quality_review";
        }

        if (validationExecutionPresent && missingEvidence.Count == 0)
        {
            return "completed_with_missing_noncritical_evidence";
        }

        if (requirements.Any(requirement => requirement.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)))
        {
            return "needs_more_data";
        }

        return "in_progress";
    }

    private static IReadOnlyList<string> ComputeBlockers(
        KnowledgeQualityItem? qualityItem,
        KnowledgeValidationPlan? plan,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeEvidenceEntry? evidence,
        IReadOnlyList<string> existingBlockers)
    {
        var blockers = new List<string>();

        if (qualityItem is not null)
        {
            if (qualityItem.ValidationScore < 0.60)
            {
                blockers.Add("validation_score_too_low");
            }

            if (qualityItem.TrustScore < 0.64)
            {
                blockers.Add("trust_score_too_low");
            }

            if (qualityItem.QualityScore < 0.64)
            {
                blockers.Add("quality_score_too_low");
            }
        }

        if ((confirmation?.SourceCount ?? 0) < 2)
        {
            blockers.Add((confirmation?.SourceCount ?? 0) == 0 ? "source_metadata_missing" : "second_independent_source_missing");
        }

        if (latestValidation is null || qualityItem?.LastValidatedUtc is null)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (plan is null)
        {
            blockers.Add("validation_plan_missing");
        }
        else
        {
            blockers.AddRange(plan.MissingEvidence.Where(entry => StaleBlockers.Contains(entry)));
        }

        if (latestValidation is null)
        {
            blockers.Add("domain_validation_not_passed");
        }

        if (confirmation?.ReviewStatus.Equals("trusted_ready", StringComparison.OrdinalIgnoreCase) == true
            && confirmation.PolicyApprovedSourceCount > 0)
        {
            blockers.RemoveAll(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase));
        }

        if (evidence is null)
        {
            blockers.Add("source_metadata_missing");
        }

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DomainValidationStatusBefore(IReadOnlyList<string> blockers)
    {
        if (blockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "blocked_waiting_for_evidence";
        }

        return blockers.Any(blocker => blocker.Contains("domain_validation", StringComparison.OrdinalIgnoreCase))
            ? "blocked_waiting_for_evidence"
            : "passed";
    }

    private static string DomainValidationStatusAfter(IReadOnlyList<string> blockers, bool policyApprovedSecondSource)
    {
        if (blockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "blocked_waiting_for_evidence";
        }

        return policyApprovedSecondSource ? "passed_with_policy_review" : "passed";
    }

    private static string RecommendedNextAction(IReadOnlyList<string> blockers, bool policyApprovedSecondSource)
    {
        if (blockers.Any(blocker => blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "run_validation_state_sync";
        }

        if (blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("fresh_validation_timestamp", StringComparison.OrdinalIgnoreCase)))
        {
            return "refresh_validation_timestamp";
        }

        if (blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("source_metadata_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "collect_second_independent_source";
        }

        if (blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "human_review_required";
        }

        return policyApprovedSecondSource ? "run_trust_promotion_review" : "validation_state_synchronized";
    }

    private static bool HasPolicyApprovedSecondSource(ConfirmationResult? confirmation) =>
        confirmation is not null && (
            confirmation.ReviewStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)
            || confirmation.PolicyApprovedSourceCount > 0
            || confirmation.CandidateSources?.Any(candidate =>
                candidate.AutoApprovedByPolicy
                || candidate.PolicyReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase)
                || candidate.SourceStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)) == true);

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<string> blockers,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan)
    {
        var warnings = new List<string>();

        if (confirmation is null)
        {
            warnings.Add("source_confirmation_missing");
        }

        if (latestValidation is null)
        {
            warnings.Add("validation_execution_missing");
        }

        if (plan is null)
        {
            warnings.Add("validation_plan_missing");
        }

        if (blockers.Count > 0)
        {
            warnings.AddRange(blockers.Select(blocker => $"remaining_blocker:{blocker}"));
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<KnowledgeValidationStateSyncItem> items)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("validation_state_sync_no_candidates");
        }

        if (items.Any(item => item.SkippedTrueContradiction))
        {
            warnings.Add("true_contradictions_skipped");
        }

        if (items.Any(item => item.SkippedHumanReviewRequired))
        {
            warnings.Add("human_review_required_skipped");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsOpenPlan(KnowledgeValidationPlan plan) =>
        plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase);

    private static bool SequenceEqualIgnoreCase(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);

    private static string StableId(params string[] parts)
    {
        var input = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(input))
        {
            return "validation_sync";
        }

        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return default;
        }
    }

    private static void WriteQualityReport(string path, KnowledgeQualityReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
    }

    private static void WriteValidationPlanReport(string path, KnowledgeValidationPlanReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
    }

    private void WriteReport(KnowledgeValidationStateSyncReport report)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeValidationStateSyncReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Validation State Sync");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Loaded Issues: {report.LoadedIssues}");
        sb.AppendLine($"- Selected Issues: {report.SelectedIssues}");
        sb.AppendLine($"- Plans Created: {report.PlansCreated}");
        sb.AppendLine($"- Plans Synchronized: {report.PlansSynchronized}");
        sb.AppendLine($"- Blockers Removed: {report.BlockersRemoved}");
        sb.AppendLine($"- Blockers Normalized: {report.BlockersNormalized}");
        sb.AppendLine($"- Skipped True Contradictions: {report.SkippedTrueContradictions}");
        sb.AppendLine($"- Skipped Human Review Required: {report.SkippedHumanReviewRequired}");
        sb.AppendLine($"- Before Issue Count: {report.BeforeIssueCount}");
        sb.AppendLine($"- After Issue Count: {report.AfterIssueCount}");
        sb.AppendLine();
        sb.AppendLine("## Items");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.Title} / {item.KnowledgeItemId}");
            sb.AppendLine($"- Mismatch Type: {item.MismatchType}");
            sb.AppendLine($"- Validation Plan: {item.ValidationPlanStatusBefore} -> {item.ValidationPlanStatusAfter}");
            sb.AppendLine($"- Validation Status: {item.ValidationStatusBefore} -> {item.ValidationStatusAfter}");
            sb.AppendLine($"- Domain Validation: {item.DomainValidationStatusBefore} -> {item.DomainValidationStatusAfter}");
            sb.AppendLine($"- Last Validated: {item.LastValidatedUtcBefore?.ToString("O") ?? "-"} -> {item.LastValidatedUtcAfter?.ToString("O") ?? "-"}");
            sb.AppendLine($"- Source Count: {item.SourceCount}");
            sb.AppendLine($"- Blockers Before: {string.Join(", ", item.BlockersBefore)}");
            sb.AppendLine($"- Blockers After: {string.Join(", ", item.BlockersAfter)}");
            sb.AppendLine($"- Removed Blockers: {string.Join(", ", item.RemovedBlockers)}");
            sb.AppendLine($"- Plan Created: {item.PlanCreated}");
            sb.AppendLine($"- Plan Synchronized: {item.PlanSynchronized}");
            sb.AppendLine($"- Blockers Normalized: {item.BlockersNormalized}");
            sb.AppendLine($"- Recommended Next Action: {item.RecommendedNextAction}");
            if (item.Warnings.Count > 0)
            {
                sb.AppendLine($"- Warnings: {string.Join(", ", item.Warnings)}");
            }
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
