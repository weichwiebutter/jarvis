using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeTrustPromotionCandidate(
    string KnowledgeId,
    string Domain,
    string Title,
    string CanonicalStatus,
    string TrustClass,
    string CurrentStatus,
    string RecommendedStatus,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    int SourceCount,
    int SourceTypeCount,
    int ValidationEvidenceCount,
    DateTimeOffset? LastValidatedUtc,
    DateTimeOffset? LatestValidationExecutionUtc,
    string ValidationReadiness,
    IReadOnlyList<string> SatisfiedConditions,
    IReadOnlyList<string> MissingEvidenceCategories,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> CanonicalBlockers,
    string PromotionOutcome,
    bool EligibleForPromotion,
    bool HumanReviewRequired);

public sealed record KnowledgeTrustPromotionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string LastSuccessfulStage,
    int TotalItems,
    int EligibleForPromotion,
    int PromotedToTrusted,
    int BlockedByEvidence,
    int BlockedByContradiction,
    int BlockedByScore,
    IReadOnlyDictionary<string, int> TopBlockers,
    IReadOnlyList<KnowledgeTrustPromotionCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    string RecommendedNextAction,
    string QualityPath,
    string KnowledgeEvidencePath,
    string SourceConfirmationsPath,
    string EvidenceGraphPath,
    string ValidationPlansPath,
    string ValidationStatusPath,
    string ValidationExecutionLogPath,
    int ValidationPlansOpen,
    int ValidationTasksPending,
    int ValidationTrustedCandidateCount,
    int ValidationItemsNeedingSourceCheck,
    int ValidationItemsNeedingOos,
    string ValidationRoutingHealth,
    string ContradictionsPath,
    string ReportPath,
    string MarkdownPath,
    string PromotionLogPath,
    IReadOnlyList<string> StageTrace,
    IReadOnlyList<string> AffectedItems,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool DryRun,
    int AppliedCount);

public sealed record KnowledgeTrustPromotionThresholds(
    double MinimumTrustScore,
    double MinimumQualityScore,
    double MinimumValidationScore,
    TimeSpan FreshValidationWindow)
{
    public static KnowledgeTrustPromotionThresholds Default => new(
        MinimumTrustScore: 0.64,
        MinimumQualityScore: 0.64,
        MinimumValidationScore: 0.6,
        FreshValidationWindow: TimeSpan.FromDays(180));
}

public sealed record PromotionStageTraceEntry(
    string Stage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMs,
    bool Completed,
    string? Error = null);

public sealed class PromotionApplyProgress
{
    private readonly List<PromotionStageTraceEntry> _stageTrace = new();
    private readonly List<string> _affectedItems = new();

    public IReadOnlyList<PromotionStageTraceEntry> StageTrace => _stageTrace;
    public IReadOnlyList<string> AffectedItems => _affectedItems;
    public string LastSuccessfulStage { get; private set; } = "load_inputs";
    public string? CurrentStage { get; private set; }

    public void Stage(string stage)
    {
        CurrentStage = stage;
        var now = DateTimeOffset.UtcNow;
        _stageTrace.Add(new PromotionStageTraceEntry(stage, now, now, 0, false));
    }

    public void CompleteStage(string stage, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, string? error = null)
    {
        CurrentStage = stage;
        if (error is null)
        {
            LastSuccessfulStage = stage;
        }

        var duration = Math.Max(0, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds);
        _stageTrace.Add(new PromotionStageTraceEntry(stage, startedAtUtc, completedAtUtc, duration, error is null, error));
    }

    public void AddAffectedItems(IEnumerable<string> knowledgeIds)
    {
        foreach (var knowledgeId in knowledgeIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!_affectedItems.Contains(knowledgeId, StringComparer.OrdinalIgnoreCase))
            {
                _affectedItems.Add(knowledgeId);
            }
        }
    }

    public void MarkTimeout(string stage)
    {
        CurrentStage = stage;
    }
}

public sealed class KnowledgeTrustPromotionPipelineService
{
    private static readonly IReadOnlySet<string> BlockingMissingEvidence =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

    private static readonly IReadOnlySet<string> NonCriticalMissingEvidence =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "noncritical_missing_evidence",
            "reproducibility_hint_missing",
            "reference_metadata_missing",
            "open_assumptions_present",
            "related_items_unresolved",
            "tags_missing",
            "description_missing"
        };

    private readonly StoragePaths _storagePaths;
    private readonly KnowledgeTrustPromotionThresholds _thresholds;

    public KnowledgeTrustPromotionPipelineService(
        StoragePaths storagePaths,
        KnowledgeTrustPromotionThresholds? thresholds = null)
    {
        _storagePaths = storagePaths;
        _thresholds = thresholds ?? KnowledgeTrustPromotionThresholds.Default;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_promotion");

    public string ReportPath => Path.Combine(Root, "knowledge_trust_promotion_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_trust_promotion_report.md");

    public string PromotionLogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_trust_promotion_log.jsonl");

    public KnowledgeTrustPromotionReport Run(bool apply = false, int? maxSeconds = null, bool skipRefresh = false)
    {
        Directory.CreateDirectory(Root);

        if (apply && maxSeconds is not null)
        {
            var timeoutSeconds = Math.Max(5, maxSeconds.Value);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var progress = new PromotionApplyProgress();
            var task = Task.Run(() => RunInternal(apply: true, cts.Token, progress, skipRefresh), cts.Token);

            if (task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                return task.Result;
            }

            cts.Cancel();
            var timeoutReport = BuildTimeoutReport(progress, "blocked_promotion_apply_timeout");
            WriteReport(timeoutReport);
            return timeoutReport;
        }

        return RunInternal(apply, CancellationToken.None, new PromotionApplyProgress(), skipRefresh);
    }

    public KnowledgeTrustPromotionReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeTrustPromotionReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private KnowledgeTrustPromotionCandidate BuildCandidate(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem? catalogItem,
        KnowledgeEvidenceEntry? evidenceEntry,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? validationPlan,
        KnowledgeValidationExecutionResult? latestValidation,
        IReadOnlyList<ContradictionRecord>? contradictions,
        HumanReviewEvidence? latestReview,
        KnowledgeCanonicalStateItem? canonicalState,
        DateTimeOffset now)
    {
        var isInternalKnowledge = IsInternalKnowledge(qualityItem.Domain, qualityItem.KnowledgeId, catalogItem);
        var internalValidation = LoadInternalValidationItem(qualityItem.KnowledgeId);
        var canonicalStatus = canonicalState?.CanonicalStatus ?? qualityItem.LifecycleStatus;
        var trustClass = canonicalState?.TrustClass ?? (qualityItem.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
            ? "external_trusted"
            : qualityItem.LifecycleStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
                ? "internal_trusted"
                : qualityItem.LifecycleStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase)
                    ? "internal_trusted"
                    : "candidate");
        var internalValidationPassing = isInternalKnowledge
            && internalValidation is not null
            && internalValidation.BuildSucceeded
            && internalValidation.FileExists
            && internalValidation.CliCommandExists
            && internalValidation.ReportOrConfigExists
            && (internalValidation.ValidationStatusAfter.Equals("validated", StringComparison.OrdinalIgnoreCase)
                || internalValidation.EvidenceWritten
                || internalValidation.ValidationStatusAfter.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase)
                || internalValidation.ValidationStatusAfter.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase));

        var sourceCount = canonicalState?.SourceCount
            ?? confirmation?.SourceCount
            ?? evidenceEntry?.SourceIds.Count
            ?? catalogItem?.SourceIds.Count
            ?? 0;
        var sourceTypeCount = confirmation?.SourceTypeCount ?? 0;
        var validationEvidenceCount = confirmation?.ValidationEvidenceCount ?? evidenceEntry?.ValidationEvidenceRefs.Count ?? 0;
        var lastValidatedUtc = canonicalState is not null && canonicalState.HasFreshValidation
            ? latestValidation?.CompletedAtUtc
            ?? qualityItem.LastValidatedUtc
            ?? catalogItem?.LastValidatedUtc
            : latestValidation?.CompletedAtUtc
                ?? qualityItem.LastValidatedUtc
                ?? catalogItem?.LastValidatedUtc;
        var freshValidation = canonicalState?.HasFreshValidation
            ?? (lastValidatedUtc is not null && now - lastValidatedUtc.Value <= _thresholds.FreshValidationWindow);

        var satisfied = new List<string>();
        var missing = new List<string>();
        var blockers = new List<string>();
        var contradictionsCount = contradictions?.Count ?? 0;

        if (canonicalState is not null)
        {
            if (canonicalState.HasTwoIndependentSources)
            {
                satisfied.Add("two_independent_sources_available");
                blockers.Remove("second_independent_source_missing");
                blockers.Remove("source_metadata_missing");
            }
            else if (sourceCount == 0)
            {
                missing.Add("source_metadata_missing");
                blockers.Add("source_metadata_missing");
            }
            else
            {
                missing.Add("second_independent_source_missing");
                blockers.Add("second_independent_source_missing");
            }

            if (canonicalState.HasPolicyApprovedSecondSource)
            {
                satisfied.Add("policy_approved_second_source");
            }

            if (canonicalState.HasFreshValidation)
            {
                satisfied.Add("fresh_validation_timestamp_available");
                blockers.Remove("fresh_validation_timestamp_missing");
            }
            else
            {
                missing.Add("fresh_validation_timestamp_missing");
                blockers.Add("fresh_validation_timestamp_missing");
            }

            if (canonicalState.HasBlockingContradiction)
            {
                blockers.Add("blocking_contradiction");
            }
        }

        if (isInternalKnowledge && internalValidationPassing)
        {
            satisfied.Add("internal_validation_evidence_present");
            satisfied.Add("implementation_verified");
            satisfied.Add("internal_trusted");
        }
        else
        {
            if (qualityItem.TrustScore >= _thresholds.MinimumTrustScore)
            {
                satisfied.Add("trust_score_sufficient");
            }
            else
            {
                blockers.Add("trust_score_too_low");
            }

            if (qualityItem.QualityScore >= _thresholds.MinimumQualityScore)
            {
                satisfied.Add("quality_score_sufficient");
            }
            else
            {
                blockers.Add("quality_score_too_low");
            }

            if (qualityItem.ValidationScore >= _thresholds.MinimumValidationScore)
            {
                satisfied.Add("validation_score_sufficient");
            }
            else
            {
                blockers.Add("validation_score_too_low");
            }

            if (sourceCount >= 2)
            {
                satisfied.Add("two_independent_sources_available");
            }
            else if (sourceCount == 0)
            {
                missing.Add("source_metadata_missing");
                blockers.Add("source_metadata_missing");
            }
            else
            {
                missing.Add("second_independent_source_missing");
                blockers.Add("second_independent_source_missing");
            }
        }

            if (freshValidation)
            {
                satisfied.Add("fresh_validation_timestamp_available");
            }
            else
            {
            missing.Add("fresh_validation_timestamp_missing");
            blockers.Add("fresh_validation_timestamp_missing");
        }

        var policyApprovedSecondSource = HasPolicyApprovedSecondSource(confirmation);

        foreach (var missingEvidence in validationPlan?.MissingEvidence ?? [])
        {
            if (string.IsNullOrWhiteSpace(missingEvidence))
            {
                continue;
            }

            if (missingEvidence.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
                && ((qualityItem.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
                        || qualityItem.LifecycleStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
                        || qualityItem.LifecycleStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase))
                    || (canonicalState?.HasTwoIndependentSources == true)
                    || (sourceCount >= 2 && policyApprovedSecondSource)
                    || (isInternalKnowledge && internalValidationPassing)))
            {
                continue;
            }

            if (missingEvidence.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                && ((qualityItem.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
                        || qualityItem.LifecycleStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
                        || qualityItem.LifecycleStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase))
                    || (canonicalState?.HasFreshValidation == true)
                    || (isInternalKnowledge && internalValidationPassing)))
            {
                continue;
            }

            if (IsBlockingMissingEvidence(missingEvidence))
            {
                blockers.Add(missingEvidence);
                missing.Add(missingEvidence);
                continue;
            }

            if (NonCriticalMissingEvidence.Contains(missingEvidence))
            {
                missing.Add(missingEvidence);
            }
        }

        if (contradictionsCount == 0)
        {
            satisfied.Add("no_blocking_contradiction");
        }
        else
        {
            blockers.Add("blocking_contradiction");
        }

        var validationReadiness = ValidationReadiness(latestValidation, validationPlan, missing);
        if (!validationReadiness.Equals("passed", StringComparison.OrdinalIgnoreCase)
            && !validationReadiness.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase))
        {
            if (!(isInternalKnowledge && internalValidationPassing))
            {
                blockers.Add("domain_validation_not_passed");
            }
        }
        else
        {
            satisfied.Add(validationReadiness);
        }

        if (!isInternalKnowledge && latestReview is not null && latestReview.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase))
        {
            if (policyApprovedSecondSource)
            {
                satisfied.Add("policy_approved_second_source");
            }
            else
            {
                missing.Add("human_review_pending");
            }
        }

        if (isInternalKnowledge && internalValidationPassing)
        {
            blockers.Remove("trust_score_too_low");
            blockers.Remove("quality_score_too_low");
            blockers.Remove("validation_score_too_low");
            blockers.Remove("second_independent_source_missing");
            blockers.Remove("source_metadata_missing");
            blockers.Remove("domain_validation_not_passed");
        }

        var alreadyTrusted = qualityItem.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
            || qualityItem.LifecycleStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
            || qualityItem.LifecycleStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase);
        var trustedLike = alreadyTrusted
            || canonicalStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
            || canonicalStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
            || canonicalStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase);

        if (trustedLike)
        {
            missing.RemoveAll(value =>
                value.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
                || value.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                || value.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase));

            blockers.RemoveAll(value =>
                value.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)
                || value.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                || value.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase));

            if (canonicalStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
                || canonicalStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase))
            {
                blockers.RemoveAll(value =>
                    value.Equals("trust_score_too_low", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase));
            }
        }

        var eligible = !trustedLike && blockers.Count == 0 && (!isInternalKnowledge || internalValidationPassing);
        var recommendedStatus = trustedLike
            ? qualityItem.LifecycleStatus
            : eligible
                ? (isInternalKnowledge && internalValidationPassing ? "internal_trusted" : "trusted")
                : qualityItem.LifecycleStatus;
        var promotionOutcome = trustedLike
            ? qualityItem.LifecycleStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase)
                ? "already_implementation_verified"
                : qualityItem.LifecycleStatus.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)
                    ? "already_internal_trusted"
                    : "already_trusted"
            : eligible
                ? (isInternalKnowledge && internalValidationPassing ? "eligible_for_internal_promotion" : "eligible_for_promotion")
            : contradictionsCount > 0
                ? "blocked_by_contradiction"
                : blockers.Any(blocker => blocker is "source_metadata_missing" or "second_independent_source_missing" or "fresh_validation_timestamp_missing" or "domain_validation_not_passed")
                    ? "blocked_by_evidence"
                    : "blocked_by_score";

        return new KnowledgeTrustPromotionCandidate(
            KnowledgeId: qualityItem.KnowledgeId,
            Domain: qualityItem.Domain,
            Title: qualityItem.Title,
            CanonicalStatus: canonicalStatus,
            TrustClass: trustClass,
            CurrentStatus: qualityItem.LifecycleStatus,
            RecommendedStatus: recommendedStatus,
            TrustScore: qualityItem.TrustScore,
            QualityScore: qualityItem.QualityScore,
            ValidationScore: qualityItem.ValidationScore,
            SourceCount: sourceCount,
            SourceTypeCount: sourceTypeCount,
            ValidationEvidenceCount: validationEvidenceCount,
            LastValidatedUtc: lastValidatedUtc,
            LatestValidationExecutionUtc: latestValidation?.CompletedAtUtc,
            ValidationReadiness: validationReadiness,
            SatisfiedConditions: satisfied.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MissingEvidenceCategories: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CanonicalBlockers: canonicalState?.CanonicalBlockers ?? Array.Empty<string>(),
            PromotionOutcome: promotionOutcome,
            EligibleForPromotion: eligible,
            HumanReviewRequired: trustedLike
                ? false
                : isInternalKnowledge && internalValidationPassing
                    ? false
                    : (latestReview is null && !policyApprovedSecondSource)
                    || latestReview?.Result.Equals("rejected", StringComparison.OrdinalIgnoreCase) == true
                    || (latestReview?.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase) == true && !policyApprovedSecondSource));
    }

    private KnowledgeTrustPromotionReport RunInternal(bool apply, CancellationToken cancellationToken, PromotionApplyProgress progress, bool skipRefresh)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var stageStart = DateTimeOffset.UtcNow;
        progress.Stage("load_inputs");

        var qualityReport = new KnowledgeQualityEngine(_storagePaths).Run();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var evidenceReport = LoadKnowledgeEvidence();
        var sourceConfirmationReport = LoadSourceConfirmations();
        var sourceConfirmationById = sourceConfirmationReport.Results
            .ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidenceReport.Evidence
            .ToDictionary(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var canonicalStateById = new KnowledgeCanonicalStateService(_storagePaths).BuildFromQualityItems(qualityReport.Items)
            .Items.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);

        var validationStrategy = new KnowledgeValidationStrategy(_storagePaths);
        var validationPlanReport = validationStrategy.LoadPlanReport() ?? validationStrategy.GeneratePlans(50);
        var validationStatus = validationStrategy.BuildStatus();
        var validationExecutor = new KnowledgeValidationExecutor(_storagePaths);
        var validationExecutions = validationExecutor.LoadResults(5000);
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
        var humanReview = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var humanReviewById = humanReview.Reviews
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ReviewedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        progress.CompleteStage("load_inputs", stageStart, DateTimeOffset.UtcNow);

        stageStart = DateTimeOffset.UtcNow;
        progress.Stage("compute_candidates");
        var candidates = qualityReport.Items
            .OrderByDescending(item => item.TrustScore)
            .ThenByDescending(item => item.QualityScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .Select(item => BuildCandidate(
                item,
                catalogById.GetValueOrDefault(item.KnowledgeId),
                evidenceById.GetValueOrDefault(item.KnowledgeId),
                sourceConfirmationById.GetValueOrDefault(item.KnowledgeId),
                validationPlanReport.Plans.FirstOrDefault(plan => plan.KnowledgeItemId.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase)),
                latestValidationById.GetValueOrDefault(item.KnowledgeId),
                contradictionsById.GetValueOrDefault(item.KnowledgeId),
                humanReviewById.GetValueOrDefault(item.KnowledgeId),
                canonicalStateById.GetValueOrDefault(item.KnowledgeId),
                updatedAt))
            .ToList();
        progress.CompleteStage("compute_candidates", stageStart, DateTimeOffset.UtcNow);

        var eligible = candidates.Where(candidate => candidate.EligibleForPromotion).ToList();
        progress.AddAffectedItems(eligible.Select(candidate => candidate.KnowledgeId));
        var promoted = 0;
        if (apply && eligible.Count > 0)
        {
            stageStart = DateTimeOffset.UtcNow;
            progress.Stage("apply_promotions");
            promoted = ApplyTrustedPromotions(eligible, catalog, updatedAt, cancellationToken, progress, skipRefresh);
            progress.CompleteStage("apply_promotions", stageStart, DateTimeOffset.UtcNow);
        }
        else
        {
            progress.CompleteStage("apply_promotions", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, apply ? null : "dry_run_skipped");
        }

        var blockedByEvidence = candidates.Count(candidate => candidate.Blockers.Any(blocker =>
            BlockingMissingEvidence.Contains(blocker) || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase)));
        var blockedByContradiction = candidates.Count(candidate => candidate.Blockers.Any(blocker =>
            blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)));
        var blockedByScore = candidates.Count(candidate => candidate.Blockers.Any(blocker =>
            blocker.Equals("trust_score_too_low", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)
            || blocker.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase)));

        var topBlockers = candidates
            .SelectMany(candidate => candidate.Blockers.Concat(candidate.MissingEvidenceCategories))
            .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>();
        if (qualityReport.TotalKnowledgeItems == 0)
        {
            warnings.Add("knowledge_catalog_empty");
        }
        if (eligible.Count == 0)
        {
            warnings.Add("no_items_eligible_for_trusted_promotion");
        }
        if (validationStatus.ValidationPlansOpen > 0)
        {
            warnings.Add($"validation_plans_open:{validationStatus.ValidationPlansOpen}");
        }

        stageStart = DateTimeOffset.UtcNow;
        progress.Stage("write_report");
        var report = new KnowledgeTrustPromotionReport(
            ReportVersion: "knowledge_trust_promotion_report_v1",
            UpdatedAtUtc: updatedAt,
            Status: apply ? (promoted > 0 ? "applied" : "apply_completed_no_changes") : "dry_run_completed",
            LastSuccessfulStage: progress.LastSuccessfulStage,
            TotalItems: qualityReport.TotalKnowledgeItems,
            EligibleForPromotion: eligible.Count,
            PromotedToTrusted: promoted,
            BlockedByEvidence: blockedByEvidence,
            BlockedByContradiction: blockedByContradiction,
            BlockedByScore: blockedByScore,
            TopBlockers: topBlockers,
            Candidates: candidates,
            Warnings: warnings,
            RecommendedNextAction: RecommendedNextAction(eligible, topBlockers, validationStatus),
            QualityPath: new KnowledgeQualityEngine(_storagePaths).QualityPath,
            KnowledgeEvidencePath: new KnowledgeQualityEngine(_storagePaths).EvidencePath,
            SourceConfirmationsPath: new SourceConfirmationEngine(_storagePaths).ReportPath,
            EvidenceGraphPath: new EvidenceGraphBuilder(_storagePaths).GraphPath,
            ValidationPlansPath: validationStrategy.PlansPath,
            ValidationStatusPath: validationStrategy.StatusPath,
            ValidationExecutionLogPath: validationExecutor.ExecutionLogPath,
            ValidationPlansOpen: validationStatus.ValidationPlansOpen,
            ValidationTasksPending: validationStatus.ValidationTasksPending,
            ValidationTrustedCandidateCount: validationStatus.TrustedCandidateCount,
            ValidationItemsNeedingSourceCheck: validationStatus.KnowledgeItemsNeedingSourceCheck,
            ValidationItemsNeedingOos: validationStatus.KnowledgeItemsNeedingOos,
            ValidationRoutingHealth: validationStatus.ValidationRoutingHealth,
            ContradictionsPath: new ContradictionDetector(_storagePaths).ContradictionsPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            PromotionLogPath: PromotionLogPath,
            StageTrace: progress.StageTrace.Select(entry => $"{entry.Stage}:{(entry.Completed ? "done" : "open")}:{entry.DurationMs}ms{(string.IsNullOrWhiteSpace(entry.Error) ? string.Empty : $":{entry.Error}")}").ToList(),
            AffectedItems: progress.AffectedItems,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            DryRun: !apply,
            AppliedCount: promoted);

        WriteReport(report);
        progress.CompleteStage("write_report", stageStart, DateTimeOffset.UtcNow);
        return report;
    }

    private int ApplyTrustedPromotions(
        IReadOnlyList<KnowledgeTrustPromotionCandidate> candidates,
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        PromotionApplyProgress progress,
        bool skipRefresh)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        var byId = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var updated = new List<KnowledgeCatalogItem>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byId.TryGetValue(candidate.KnowledgeId, out var item))
            {
                continue;
            }

            var targetStatus = candidate.RecommendedStatus;
            if (string.IsNullOrWhiteSpace(targetStatus))
            {
                targetStatus = "trusted";
            }

            var updatedItem = item with
            {
                ValidationStatus = targetStatus,
                LastValidatedUtc = now
            };
            byId[candidate.KnowledgeId] = updatedItem;
            updated.Add(updatedItem);
            AppendPromotionLog(candidate, now, targetStatus);
        }

        if (updated.Count > 0)
        {
            progress.Stage("persist_catalog");
            var serialized = JsonSerializer.Serialize(
                byId.Values
                    .OrderBy(item => item.Domain, StringComparer.Ordinal)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToList(),
                JsonDefaults.WriteOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(new KnowledgeCatalog(_storagePaths).CatalogPath)!);
            File.WriteAllText(new KnowledgeCatalog(_storagePaths).CatalogPath, serialized);
            progress.CompleteStage("persist_catalog", now, DateTimeOffset.UtcNow);

            if (!skipRefresh)
            {
                progress.Stage("persist_quality");
                var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
                _ = qualityEngine.LoadReport() ?? qualityEngine.Run();
                _ = new TrustedKnowledgeReviewGateService(_storagePaths).Load() ?? new TrustedKnowledgeReviewGateService(_storagePaths).Run();
                _ = new KnowledgePromotionEngine(_storagePaths).BuildStatus();
                _ = new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
                progress.CompleteStage("persist_quality", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            }
            else
            {
                progress.CompleteStage("persist_quality", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "refresh_skipped");
            }
        }

        return updated.Count;
    }

    private void AppendPromotionLog(KnowledgeTrustPromotionCandidate candidate, DateTimeOffset now, string action)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PromotionLogPath)!);
        var entry = new
        {
            timestamp_utc = now,
            action,
            candidate.KnowledgeId,
            candidate.Domain,
            candidate.Title,
            candidate.CurrentStatus,
            candidate.RecommendedStatus,
            candidate.TrustScore,
            candidate.QualityScore,
            candidate.ValidationScore,
            candidate.SourceCount,
            candidate.SourceTypeCount,
            candidate.ValidationEvidenceCount,
            candidate.LastValidatedUtc,
            candidate.ValidationReadiness,
            candidate.Blockers,
            candidate.MissingEvidenceCategories
        };
        File.AppendAllText(PromotionLogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private KnowledgeEvidenceReport LoadKnowledgeEvidence()
    {
        var path = new KnowledgeQualityEngine(_storagePaths).EvidencePath;
        if (!File.Exists(path))
        {
            return new KnowledgeEvidenceReport(
                ReportVersion: "knowledge_evidence_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Evidence: [],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions)
                ?? new KnowledgeEvidenceReport(
                    ReportVersion: "knowledge_evidence_v1",
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    Evidence: [],
                    NoTradingExecution: true,
                    NoBrokerAction: true,
                    NoAutoTrading: true,
                    HumanReviewRequired: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new KnowledgeEvidenceReport(
                ReportVersion: "knowledge_evidence_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Evidence: [],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }
    }

    private SourceConfirmationReport LoadSourceConfirmations()
    {
        var path = new SourceConfirmationEngine(_storagePaths).ReportPath;
        if (!File.Exists(path))
        {
            return new SourceConfirmationReport(
                ReportVersion: "source_confirmation_v2",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ItemsAnalyzed: 0,
                ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Results: [],
                Warnings: ["source_confirmation_missing"],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }

        try
        {
            return JsonSerializer.Deserialize<SourceConfirmationReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions)
                ?? new SourceConfirmationReport(
                    ReportVersion: "source_confirmation_v2",
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    ItemsAnalyzed: 0,
                    ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    Results: [],
                    Warnings: ["source_confirmation_missing"],
                    NoTradingExecution: true,
                    NoBrokerAction: true,
                    NoAutoTrading: true,
                    HumanReviewRequired: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new SourceConfirmationReport(
                ReportVersion: "source_confirmation_v2",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ItemsAnalyzed: 0,
                ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Results: [],
                Warnings: ["source_confirmation_missing"],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }
    }

    private static string ValidationReadiness(
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? validationPlan,
        IReadOnlyList<string> missing)
    {
        if (latestValidation is null)
        {
            return "validation_missing";
        }

        if (latestValidation.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (latestValidation.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
        {
            var blockingWarnings = latestValidation.Warnings.Where(IsBlockingMissingEvidence).ToList();
            var nonCriticalWarnings = latestValidation.Warnings.Where(value => NonCriticalMissingEvidence.Contains(value)).ToList();
            if (blockingWarnings.Count == 0 && missing.All(item => !IsBlockingMissingEvidence(item)))
            {
                return nonCriticalWarnings.Count > 0 || missing.Any(item => NonCriticalMissingEvidence.Contains(item))
                    ? "completed_with_missing_noncritical_evidence"
                    : "needs_more_data";
            }

            return blockingWarnings.Count > 0
                ? "blocked_waiting_for_evidence"
                : "needs_more_data";
        }

        if (latestValidation.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            return "validation_failed";
        }

        if (validationPlan is not null && validationPlan.Status.Equals("ready_for_quality_review", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        return "blocked";
    }

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

    private static bool HasPolicyApprovedSecondSource(ConfirmationResult? confirmation) =>
        confirmation is not null && (
            confirmation.ReviewStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)
            || confirmation.PolicyApprovedSourceCount > 0
            || confirmation.CandidateSources?.Any(candidate =>
                candidate.AutoApprovedByPolicy
                || candidate.PolicyReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase)
                || candidate.SourceStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)) == true);

    private static bool IsInternalKnowledge(string domain, string knowledgeId, KnowledgeCatalogItem? catalogItem)
    {
        if (domain.Equals("software", StringComparison.OrdinalIgnoreCase)
            || domain.Equals("documentation", StringComparison.OrdinalIgnoreCase)
            || domain.Equals("process", StringComparison.OrdinalIgnoreCase)
            || domain.Equals("research", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (knowledgeId.StartsWith("software:", StringComparison.OrdinalIgnoreCase)
            || knowledgeId.StartsWith("documentation:", StringComparison.OrdinalIgnoreCase)
            || knowledgeId.StartsWith("process:", StringComparison.OrdinalIgnoreCase)
            || knowledgeId.StartsWith("research:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return catalogItem?.Title.Contains(".cs", StringComparison.OrdinalIgnoreCase) == true
            || catalogItem?.Title.Contains(".md", StringComparison.OrdinalIgnoreCase) == true
            || catalogItem?.Title.Contains("architecture", StringComparison.OrdinalIgnoreCase) == true
            || catalogItem?.Title.Contains("roadmap", StringComparison.OrdinalIgnoreCase) == true;
    }

    private InternalKnowledgeValidationItem? LoadInternalValidationItem(string knowledgeId)
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "internal_knowledge_validation", "internal_knowledge_validation_report.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var report = JsonSerializer.Deserialize<InternalKnowledgeValidationReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
            return report?.Items.FirstOrDefault(item => item.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string RecommendedNextAction(
        IReadOnlyList<KnowledgeTrustPromotionCandidate> eligible,
        IReadOnlyDictionary<string, int> topBlockers,
        KnowledgeValidationStatus validationStatus)
    {
        if (eligible.Count > 0)
        {
            return "mindestens ein Wissenstext ist trusted-ready. `knowledge-trust-promote --apply` kann die Freigabe umsetzen.";
        }

        if (topBlockers.Count == 0)
        {
            return "keine trusted Kandidaten vorhanden; fehlende Evidenz und Validierung erneut erzeugen.";
        }

        var topBlocker = topBlockers.First();
        if (topBlocker.Key.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
        {
            return $"Alle Kandidaten sind noch single_source. Weitere unabhängige Quelle ergänzen; validation_plans_open={validationStatus.ValidationPlansOpen}.";
        }

        if (topBlocker.Key.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase))
        {
            return "Aktuelle Validierungszeitstempel nachziehen und trusted-Prüfung erneut ausführen.";
        }

        if (topBlocker.Key.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase))
        {
            return "Widersprüche zuerst auflösen, dann trusted-Promotion erneut prüfen.";
        }

        if (topBlocker.Key.Equals("trust_score_too_low", StringComparison.OrdinalIgnoreCase)
            || topBlocker.Key.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)
            || topBlocker.Key.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase))
        {
            return "Score-basierte Kandidaten reifen lassen und weitere Evidenz sammeln.";
        }

        return $"Top Blocker: {topBlocker.Key}; trusted promotion erneut prüfen.";
    }

    private KnowledgeTrustPromotionReport BuildTimeoutReport(PromotionApplyProgress progress, string status)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadReport();
        var validationStatus = new KnowledgeValidationStrategy(_storagePaths).BuildStatus();

        return new KnowledgeTrustPromotionReport(
            ReportVersion: "knowledge_trust_promotion_report_v1",
            UpdatedAtUtc: updatedAt,
            Status: status,
            LastSuccessfulStage: progress.LastSuccessfulStage,
            TotalItems: qualityReport.TotalKnowledgeItems,
            EligibleForPromotion: 0,
            PromotedToTrusted: 0,
            BlockedByEvidence: 0,
            BlockedByContradiction: 0,
            BlockedByScore: 0,
            TopBlockers: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [status] = 1
            },
            Candidates: [],
            Warnings: [status],
            RecommendedNextAction: "retry_promotion_apply_or_run_consistency_repair",
            QualityPath: new KnowledgeQualityEngine(_storagePaths).QualityPath,
            KnowledgeEvidencePath: new KnowledgeQualityEngine(_storagePaths).EvidencePath,
            SourceConfirmationsPath: new SourceConfirmationEngine(_storagePaths).ReportPath,
            EvidenceGraphPath: new EvidenceGraphBuilder(_storagePaths).GraphPath,
            ValidationPlansPath: new KnowledgeValidationStrategy(_storagePaths).PlansPath,
            ValidationStatusPath: new KnowledgeValidationStrategy(_storagePaths).StatusPath,
            ValidationExecutionLogPath: new KnowledgeValidationExecutor(_storagePaths).ExecutionLogPath,
            ValidationPlansOpen: validationStatus.ValidationPlansOpen,
            ValidationTasksPending: validationStatus.ValidationTasksPending,
            ValidationTrustedCandidateCount: validationStatus.TrustedCandidateCount,
            ValidationItemsNeedingSourceCheck: validationStatus.KnowledgeItemsNeedingSourceCheck,
            ValidationItemsNeedingOos: validationStatus.KnowledgeItemsNeedingOos,
            ValidationRoutingHealth: validationStatus.ValidationRoutingHealth,
            ContradictionsPath: new ContradictionDetector(_storagePaths).ContradictionsPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            PromotionLogPath: PromotionLogPath,
            StageTrace: progress.StageTrace.Select(entry => $"{entry.Stage}:{(entry.Completed ? "done" : "open")}:{entry.DurationMs}ms{(string.IsNullOrWhiteSpace(entry.Error) ? string.Empty : $":{entry.Error}")}").ToList(),
            AffectedItems: progress.AffectedItems,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            DryRun: false,
            AppliedCount: 0);
    }

    private void WriteReport(KnowledgeTrustPromotionReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "knowledge_trust_promotion");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_trust_promotion_report.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "knowledge_trust_promotion_report.md"), markdown);
        }
    }

    private static string BuildMarkdown(KnowledgeTrustPromotionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Trust Promotion Report");
        sb.AppendLine();
        sb.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Total Items: {report.TotalItems}");
        sb.AppendLine($"- Eligible for Promotion: {report.EligibleForPromotion}");
        sb.AppendLine($"- Promoted to Trusted: {report.PromotedToTrusted}");
        sb.AppendLine($"- Blocked by Evidence: {report.BlockedByEvidence}");
        sb.AppendLine($"- Blocked by Contradiction: {report.BlockedByContradiction}");
        sb.AppendLine($"- Blocked by Score: {report.BlockedByScore}");
        sb.AppendLine($"- Dry Run: {report.DryRun.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Validation Plans Open: {report.ValidationPlansOpen}");
        sb.AppendLine($"- Validation Tasks Pending: {report.ValidationTasksPending}");
        sb.AppendLine($"- Validation Trusted Candidate Count: {report.ValidationTrustedCandidateCount}");
        sb.AppendLine($"- Validation Needs Source Check: {report.ValidationItemsNeedingSourceCheck}");
        sb.AppendLine($"- Validation Needs OOS: {report.ValidationItemsNeedingOos}");
        sb.AppendLine($"- Validation Routing Health: {report.ValidationRoutingHealth}");
        sb.AppendLine();
        sb.AppendLine("## Top Blockers");
        if (report.TopBlockers.Count == 0)
        {
            sb.AppendLine("- keine");
        }
        else
        {
            foreach (var blocker in report.TopBlockers)
            {
                sb.AppendLine($"- {blocker.Key}: {blocker.Value}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Top Candidates");
        foreach (var candidate in report.Candidates.Take(20))
        {
            sb.AppendLine($"- {candidate.KnowledgeId} [{candidate.Domain}] {candidate.CurrentStatus} -> {candidate.RecommendedStatus}");
            sb.AppendLine($"  - canonical_status={candidate.CanonicalStatus} trust_class={candidate.TrustClass}");
            sb.AppendLine($"  - trust={candidate.TrustScore:0.###} quality={candidate.QualityScore:0.###} validation={candidate.ValidationScore:0.###}");
            sb.AppendLine($"  - sources={candidate.SourceCount} validationEvidence={candidate.ValidationEvidenceCount} readiness={candidate.ValidationReadiness}");
            sb.AppendLine($"  - blockers={string.Join(", ", candidate.Blockers)}");
            sb.AppendLine($"  - missing={string.Join(", ", candidate.MissingEvidenceCategories)}");
            if (candidate.CanonicalBlockers.Count > 0)
            {
                sb.AppendLine($"  - canonical_blockers={string.Join(", ", candidate.CanonicalBlockers)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Recommended Next Action");
        sb.AppendLine(report.RecommendedNextAction);
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine("- no_trading_execution=true");
        sb.AppendLine("- no_broker_action=true");
        sb.AppendLine("- no_auto_trading=true");
        sb.AppendLine("- research_only=true");
        sb.AppendLine("- human_review_required=true");
        return sb.ToString();
    }
}
