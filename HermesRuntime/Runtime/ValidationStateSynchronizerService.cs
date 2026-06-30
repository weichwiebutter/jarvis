using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationStateSynchronizerItem(
    string KnowledgeItemId,
    string Domain,
    string Title,
    double ValidationScoreBefore,
    double ValidationScoreAfter,
    double TrustScoreBefore,
    double TrustScoreAfter,
    double QualityScoreBefore,
    double QualityScoreAfter,
    DateTimeOffset? LastValidatedUtcBefore,
    DateTimeOffset? LastValidatedUtcAfter,
    string ValidationPlanStatusBefore,
    string ValidationPlanStatusAfter,
    string DomainValidationStatusBefore,
    string DomainValidationStatusAfter,
    IReadOnlyList<string> RemainingBlockersBefore,
    IReadOnlyList<string> RemainingBlockersAfter,
    IReadOnlyList<string> RemovedBlockers,
    IReadOnlyList<string> Warnings,
    bool HasValidationExecutions,
    bool HasPolicyApprovedSecondSource,
    bool Synchronized,
    string RecommendedNextAction);

public sealed record ValidationStateSynchronizerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int SynchronizedItems,
    int TimestampFixed,
    int DomainValidationFixed,
    int ValidationPlanFixed,
    int HumanReviewReclassified,
    int RemainingBlockers,
    IReadOnlyDictionary<string, int> RemainingBlockersByType,
    IReadOnlyList<ValidationStateSynchronizerItem> Items,
    IReadOnlyList<string> Warnings,
    string QualityPath,
    string EvidencePath,
    string ValidationPlansPath,
    string ValidationStatusPath,
    string ValidationExecutionLogPath,
    string SourceConfirmationsPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool DryRun,
    bool Applied);

public sealed class ValidationStateSynchronizerService
{
    private static readonly IReadOnlySet<string> StalePlanBlockers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fresh_validation_timestamp_missing",
        "fresh_validation_timestamp",
        "validation_plan_missing",
        "validation_plan_or_requirement_missing"
    };

    private readonly StoragePaths _storagePaths;

    public ValidationStateSynchronizerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "validation_state_sync");

    public string ReportPath => Path.Combine(Root, "validation_state_sync_report.json");

    public string MarkdownPath => Path.Combine(Root, "validation_state_sync_report.md");

    public ValidationStateSynchronizerReport Run(bool apply, bool dryRun)
    {
        Directory.CreateDirectory(Root);

        var updatedAt = DateTimeOffset.UtcNow;
        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var qualityReport = qualityEngine.LoadOrCreateReport();
        var validationStrategy = new KnowledgeValidationStrategy(_storagePaths);
        var validationPlanReport = validationStrategy.LoadPlanReport() ?? validationStrategy.GeneratePlans(50);
        var validationStatusBefore = validationStrategy.LoadStatus() ?? validationStrategy.BuildStatus();
        var validationExecutor = new KnowledgeValidationExecutor(_storagePaths);
        var validationExecutions = validationExecutor.LoadResults(5000);
        var sourceConfirmations = LoadSourceConfirmations(_storagePaths);
        var humanReviewReport = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var qualityById = qualityReport.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var planById = validationPlanReport.Plans.ToDictionary(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);
        var humanReviewById = humanReviewReport.Reviews
            .Where(review => !string.IsNullOrWhiteSpace(review.KnowledgeId))
            .GroupBy(review => review.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(review => review.ReviewedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
        var latestExecutionById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.CompletedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
        var executionsById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);
        var confirmationById = sourceConfirmations.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var contradictionsById = contradictions.Contradictions
            .Where(item => !string.IsNullOrWhiteSpace(item.KnowledgeId))
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var candidateIds = qualityReport.Items
            .Where(item => ShouldSynchronize(
                item,
                planById.GetValueOrDefault(item.KnowledgeId),
                confirmationById.GetValueOrDefault(item.KnowledgeId),
                latestExecutionById.GetValueOrDefault(item.KnowledgeId),
                contradictionsById.GetValueOrDefault(item.KnowledgeId) ?? []))
            .Select(item => item.KnowledgeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var synchronizedItems = new List<ValidationStateSynchronizerItem>();
        var mutatedQualityItems = qualityReport.Items.ToList();
        var mutatedPlans = validationPlanReport.Plans.ToList();

        foreach (var knowledgeId in candidateIds)
        {
            if (!qualityById.TryGetValue(knowledgeId, out var beforeQuality))
            {
                continue;
            }

            var confirmation = confirmationById.GetValueOrDefault(knowledgeId);
            var latestExecution = latestExecutionById.GetValueOrDefault(knowledgeId);
            var latestReview = humanReviewById.GetValueOrDefault(knowledgeId);
            var executions = executionsById.GetValueOrDefault(knowledgeId) ?? [];
            var contradictionsForItem = contradictionsById.GetValueOrDefault(knowledgeId) ?? [];
            var beforePlan = planById.GetValueOrDefault(knowledgeId);
            var planIndex = mutatedPlans.FindIndex(plan => plan.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
            var qualityIndex = mutatedQualityItems.FindIndex(item => item.KnowledgeId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));

            var updatedQuality = beforeQuality;
            var updatedPlan = beforePlan;
            var blockersBefore = ComputeBlockers(beforeQuality, beforePlan, confirmation, latestExecution, latestReview, contradictionsForItem);
            var hasPolicyApprovedSecondSource = HasPolicyApprovedSecondSource(confirmation);
            var validationExecutionPresent = latestExecution is not null;
            var freshTimestamp = latestExecution?.CompletedAtUtc ?? beforeQuality.LastValidatedUtc;
            var removedBlockers = new List<string>();

            if (validationExecutionPresent && freshTimestamp is not null && beforeQuality.LastValidatedUtc != freshTimestamp)
            {
                updatedQuality = updatedQuality with { LastValidatedUtc = freshTimestamp };
            }

            if (beforePlan is not null)
            {
                var filteredMissingEvidence = FilterMissingEvidence(beforePlan.MissingEvidence, validationExecutionPresent, hasPolicyApprovedSecondSource);
                if (filteredMissingEvidence.Count != beforePlan.MissingEvidence.Count
                    || !beforePlan.Status.Equals(PlanStatusFor(filteredMissingEvidence, beforePlan.Requirements, validationExecutionPresent), StringComparison.OrdinalIgnoreCase))
                {
                    updatedPlan = beforePlan with
                    {
                        MissingEvidence = filteredMissingEvidence,
                        Status = PlanStatusFor(filteredMissingEvidence, beforePlan.Requirements, validationExecutionPresent),
                        UpdatedAtUtc = updatedAt
                    };
                }
                else if (validationExecutionPresent || hasPolicyApprovedSecondSource)
                {
                    updatedPlan = beforePlan with { UpdatedAtUtc = updatedAt };
                }
            }

            var blockersAfter = ComputeBlockers(updatedQuality, updatedPlan, confirmation, latestExecution, latestReview, contradictionsForItem);

            removedBlockers.AddRange(blockersBefore.Where(blocker => !blockersAfter.Contains(blocker, StringComparer.OrdinalIgnoreCase)));
            var synchronized = removedBlockers.Count > 0
                || !Nullable.Equals(beforeQuality.LastValidatedUtc, updatedQuality.LastValidatedUtc)
                || !string.Equals(beforePlan?.Status, updatedPlan?.Status, StringComparison.OrdinalIgnoreCase)
                || !SequenceEqualIgnoreCase(beforePlan?.MissingEvidence ?? [], updatedPlan?.MissingEvidence ?? []);

            if (synchronized && qualityIndex >= 0)
            {
                mutatedQualityItems[qualityIndex] = updatedQuality;
            }

            if (synchronized && planIndex >= 0 && updatedPlan is not null)
            {
                mutatedPlans[planIndex] = updatedPlan;
            }

            var domainValidationBefore = DomainValidationStatusBefore(blockersBefore);
            var domainValidationAfter = DomainValidationStatusAfter(blockersAfter, hasPolicyApprovedSecondSource);
            var validationPlanStatusBefore = beforePlan?.Status ?? "validation_plan_missing";
            var validationPlanStatusAfter = updatedPlan?.Status ?? validationPlanStatusBefore;

            if (synchronized)
            {
                removedBlockers = removedBlockers
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            synchronizedItems.Add(new ValidationStateSynchronizerItem(
                KnowledgeItemId: knowledgeId,
                Domain: beforeQuality.Domain,
                Title: beforeQuality.Title,
                ValidationScoreBefore: beforeQuality.ValidationScore,
                ValidationScoreAfter: updatedQuality.ValidationScore,
                TrustScoreBefore: beforeQuality.TrustScore,
                TrustScoreAfter: updatedQuality.TrustScore,
                QualityScoreBefore: beforeQuality.QualityScore,
                QualityScoreAfter: updatedQuality.QualityScore,
                LastValidatedUtcBefore: beforeQuality.LastValidatedUtc,
                LastValidatedUtcAfter: updatedQuality.LastValidatedUtc,
                ValidationPlanStatusBefore: validationPlanStatusBefore,
                ValidationPlanStatusAfter: validationPlanStatusAfter,
                DomainValidationStatusBefore: domainValidationBefore,
                DomainValidationStatusAfter: domainValidationAfter,
                RemainingBlockersBefore: blockersBefore,
                RemainingBlockersAfter: blockersAfter,
                RemovedBlockers: removedBlockers,
                Warnings: BuildWarnings(blockersAfter, confirmation, latestExecution, latestReview, beforePlan),
                HasValidationExecutions: validationExecutionPresent,
                HasPolicyApprovedSecondSource: hasPolicyApprovedSecondSource,
                Synchronized: synchronized,
                RecommendedNextAction: RecommendedNextAction(blockersAfter, hasPolicyApprovedSecondSource)));
        }

        var timestampFixed = synchronizedItems.Count(item =>
            !Nullable.Equals(item.LastValidatedUtcBefore, item.LastValidatedUtcAfter)
            && item.LastValidatedUtcAfter is not null);
        var domainValidationFixed = synchronizedItems.Count(item =>
            !item.DomainValidationStatusBefore.Equals(item.DomainValidationStatusAfter, StringComparison.OrdinalIgnoreCase));
        var validationPlanFixed = synchronizedItems.Count(item =>
            item.RemovedBlockers.Any(blocker => StalePlanBlockers.Contains(blocker))
            || !SequenceEqualIgnoreCase(item.RemainingBlockersBefore, item.RemainingBlockersAfter));
        var humanReviewReclassified = synchronizedItems.Count(item =>
            item.HasPolicyApprovedSecondSource
            && item.DomainValidationStatusAfter.Equals("passed_with_policy_review", StringComparison.OrdinalIgnoreCase));

        var remainingBlockersByType = synchronizedItems
            .SelectMany(item => item.RemainingBlockersAfter)
            .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var report = new ValidationStateSynchronizerReport(
            ReportVersion: "validation_state_sync_v1",
            UpdatedAtUtc: updatedAt,
            Status: dryRun ? "dry_run_ready" : "applied",
            LoadedItems: qualityReport.Items.Count,
            SynchronizedItems: synchronizedItems.Count(item => item.Synchronized),
            TimestampFixed: timestampFixed,
            DomainValidationFixed: domainValidationFixed,
            ValidationPlanFixed: validationPlanFixed,
            HumanReviewReclassified: humanReviewReclassified,
            RemainingBlockers: remainingBlockersByType.Values.Sum(),
            RemainingBlockersByType: remainingBlockersByType,
            Items: synchronizedItems,
            Warnings: BuildWarnings(reportable: synchronizedItems),
            QualityPath: qualityEngine.QualityPath,
            EvidencePath: qualityEngine.EvidencePath,
            ValidationPlansPath: validationStrategy.PlansPath,
            ValidationStatusPath: validationStrategy.StatusPath,
            ValidationExecutionLogPath: validationExecutor.ExecutionLogPath,
            SourceConfirmationsPath: sourceConfirmationsPath(_storagePaths),
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            DryRun: dryRun || !apply,
            Applied: apply && !dryRun);

        if (apply && !dryRun)
        {
            WriteQualityReport(qualityEngine.QualityPath, qualityReport with
            {
                UpdatedAtUtc = updatedAt,
                Items = mutatedQualityItems
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

        WriteReport(report);
        return report;
    }

    public ValidationStateSynchronizerReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationStateSynchronizerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
    }

    private static string sourceConfirmationsPath(StoragePaths storagePaths) =>
        Path.Combine(storagePaths.Root, "cognitive_core", "source_confirmations.json");

    private static bool ShouldSynchronize(
        KnowledgeQualityItem item,
        KnowledgeValidationPlan? plan,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestValidation,
        IReadOnlyList<ContradictionRecord> contradictions)
    {
        if (item.ValidationScore < 0.60 || item.TrustScore < 0.64 || item.QualityScore < 0.64)
        {
            return false;
        }

        if ((confirmation?.SourceCount ?? 0) < 2
            || latestValidation is null
            || latestValidation.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
            || latestValidation.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || latestValidation.OutcomeStatus.Equals("validation_type_not_supported_for_domain", StringComparison.OrdinalIgnoreCase)
            || contradictions.Any(IsBlockingContradiction))
        {
            return false;
        }

        return plan is not null;
    }

    private static bool IsBlockingContradiction(ContradictionRecord record) =>
        record.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)
        || record.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> FilterMissingEvidence(
        IReadOnlyList<string> missingEvidence,
        bool validationExecutionPresent,
        bool policyApprovedSecondSource)
    {
        var filtered = missingEvidence
            .Where(entry => !StalePlanBlockers.Contains(entry))
            .Where(entry => !(entry.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase) && policyApprovedSecondSource))
            .ToList();

        if (validationExecutionPresent)
        {
            filtered.RemoveAll(entry => entry.Equals("validation_result_missing", StringComparison.OrdinalIgnoreCase));
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

        if (validationExecutionPresent && missingEvidence.All(entry => !StalePlanBlockers.Contains(entry)))
        {
            return "completed_with_missing_noncritical_evidence";
        }

        if (requirements.Any(requirement => requirement.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)))
        {
            return "needs_more_data";
        }

        return requirements.Any(requirement => requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase))
            ? "in_progress"
            : "open";
    }

    private static string DomainValidationStatusBefore(IReadOnlyList<string> blockers)
    {
        if (blockers.Any(entry => entry.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)))
        {
            return "passed";
        }

        return blockers.Any(IsBlockingBlocker) ? "blocked_waiting_for_evidence" : "passed";
    }

    private static string DomainValidationStatusAfter(IReadOnlyList<string> blockers, bool policyApprovedSecondSource)
    {
        if (blockers.Any(IsBlockingBlocker))
        {
            return "blocked_waiting_for_evidence";
        }

        return policyApprovedSecondSource ? "passed_with_policy_review" : "passed";
    }

    private static string RecommendedNextAction(IReadOnlyList<string> blockers, bool policyApprovedSecondSource)
    {
        if (blockers.Count == 0)
        {
            return policyApprovedSecondSource ? "run_trust_promotion_review" : "validation_state_synchronized";
        }

        if (blockers.Any(blocker => blocker.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("trust_score_too_low", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            return "strengthen_validation_or_evidence";
        }

        if (blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "refresh_validation_plan_and_timestamp";
        }

        if (blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("source_metadata_missing", StringComparison.OrdinalIgnoreCase)))
        {
            return "collect_second_independent_source";
        }

        if (blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)))
        {
            return "await_human_review";
        }

        if (blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "resolve_contradiction";
        }

        return "review_remaining_blockers";
    }

    private static bool IsOpenPlan(KnowledgeValidationPlan plan) =>
        plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
        || plan.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockingBlocker(string blocker) =>
        blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("source_metadata_missing", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)
        || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<ValidationStateSynchronizerItem> reportable) =>
        reportable.Count == 0
            ? ["validation_state_sync_no_candidates"]
            : [];

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<string> blockers,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestValidation,
        HumanReviewEvidence? latestReview,
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
        if (latestReview is not null && latestReview.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("human_review_needs_review");
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

    private static bool HasPolicyApprovedSecondSource(ConfirmationResult? confirmation) =>
        confirmation is not null && (
            confirmation.ReviewStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)
            || confirmation.PolicyApprovedSourceCount > 0
            || confirmation.CandidateSources?.Any(candidate =>
                candidate.AutoApprovedByPolicy
                || candidate.PolicyReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase)
                || candidate.SourceStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)) == true);

    private static IReadOnlyList<string> ComputeBlockers(
        KnowledgeQualityItem quality,
        KnowledgeValidationPlan? plan,
        ConfirmationResult? confirmation,
        KnowledgeValidationExecutionResult? latestValidation,
        HumanReviewEvidence? latestReview,
        IReadOnlyList<ContradictionRecord> contradictions)
    {
        var blockers = new List<string>();

        if (quality.ValidationScore < 0.60)
        {
            blockers.Add("validation_score_too_low");
        }
        if (quality.TrustScore < 0.64)
        {
            blockers.Add("trust_score_too_low");
        }
        if (quality.QualityScore < 0.64)
        {
            blockers.Add("quality_score_too_low");
        }

        var sourceCount = confirmation?.SourceCount ?? 0;
        if (sourceCount < 2)
        {
            blockers.Add(sourceCount == 0 ? "source_metadata_missing" : "second_independent_source_missing");
        }

        if (latestValidation is null || quality.LastValidatedUtc is null)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (contradictions.Any(IsBlockingContradiction))
        {
            blockers.Add("blocking_contradiction");
        }

        if (plan is null)
        {
            blockers.Add("validation_plan_missing");
        }
        else
        {
            foreach (var entry in plan.MissingEvidence)
            {
                if (StalePlanBlockers.Contains(entry))
                {
                    blockers.Add(entry);
                }
            }
        }

        if (latestValidation is null)
        {
            blockers.Add("domain_validation_not_passed");
        }

        if (latestReview?.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase) == true
            && !HasPolicyApprovedSecondSource(confirmation))
        {
            blockers.Add("human_review_pending");
        }

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SequenceEqualIgnoreCase(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);

    private static SourceConfirmationReport LoadSourceConfirmations(StoragePaths storagePaths) =>
        LoadJson<SourceConfirmationReport>(Path.Combine(storagePaths.Root, "cognitive_core", "source_confirmations.json"))
        ?? new SourceConfirmationReport(
            ReportVersion: "source_confirmation_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ItemsAnalyzed: 0,
            ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            Results: [],
            Warnings: ["source_confirmations_unavailable"],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

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

    private void WriteReport(ValidationStateSynchronizerReport report)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(ValidationStateSynchronizerReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Validation State Synchronizer");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Loaded Items: {report.LoadedItems}");
        sb.AppendLine($"- Synchronized Items: {report.SynchronizedItems}");
        sb.AppendLine($"- Timestamp Fixed: {report.TimestampFixed}");
        sb.AppendLine($"- Domain Validation Fixed: {report.DomainValidationFixed}");
        sb.AppendLine($"- Validation Plan Fixed: {report.ValidationPlanFixed}");
        sb.AppendLine($"- Human Review Reclassified: {report.HumanReviewReclassified}");
        sb.AppendLine($"- Remaining Blockers: {report.RemainingBlockers}");
        sb.AppendLine();
        sb.AppendLine("## Remaining Blockers");
        foreach (var entry in report.RemainingBlockersByType.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {entry.Key}: {entry.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Targeted Items");
        foreach (var item in report.Items.Where(item => item.Synchronized || item.KnowledgeItemId.Equals("trading:bearish_engulfing", StringComparison.OrdinalIgnoreCase) || item.KnowledgeItemId.Equals("trading:liquidity_sweep", StringComparison.OrdinalIgnoreCase) || item.KnowledgeItemId.Equals("trading:inside_bar", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine($"### {item.Title} / {item.KnowledgeItemId}");
            sb.AppendLine($"- Domain Validation: {item.DomainValidationStatusBefore} -> {item.DomainValidationStatusAfter}");
            sb.AppendLine($"- Validation Plan: {item.ValidationPlanStatusBefore} -> {item.ValidationPlanStatusAfter}");
            sb.AppendLine($"- Last Validated: {item.LastValidatedUtcBefore?.ToString("O") ?? "-"} -> {item.LastValidatedUtcAfter?.ToString("O") ?? "-"}");
            sb.AppendLine($"- Validation Score: {item.ValidationScoreBefore:0.###} -> {item.ValidationScoreAfter:0.###}");
            sb.AppendLine($"- Trust Score: {item.TrustScoreBefore:0.###} -> {item.TrustScoreAfter:0.###}");
            sb.AppendLine($"- Quality Score: {item.QualityScoreBefore:0.###} -> {item.QualityScoreAfter:0.###}");
            sb.AppendLine($"- Remaining Blockers: {string.Join(", ", item.RemainingBlockersAfter)}");
            sb.AppendLine($"- Recommended Next Action: {item.RecommendedNextAction}");
            sb.AppendLine();
        }
        return sb.ToString();
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
}
