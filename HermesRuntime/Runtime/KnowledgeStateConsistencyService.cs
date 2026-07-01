using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeStateConsistencyItem(
    string KnowledgeId,
    string Domain,
    string Title,
    int SourceCountBefore,
    int SourceCountExpected,
    bool SourceCountMismatch,
    bool TrustedStatusMismatch,
    bool TimestampMismatch,
    bool BlockerMismatch,
    bool MissingItemIdMismatch,
    string CatalogValidationStatus,
    string QualityLifecycleStatus,
    string SourceConfirmationStatus,
    string ValidationPlanStatus,
    string PromotionStatus,
    DateTimeOffset? LastValidatedUtc,
    DateTimeOffset? LatestValidationExecutionUtc,
    double ValidationScore,
    double TrustScore,
    double QualityScore,
    bool PolicyApprovedSecondSource,
    bool HasValidationExecutions,
    IReadOnlyList<string> CurrentBlockers,
    IReadOnlyList<string> ExpectedBlockers,
    IReadOnlyList<string> Warnings,
    string RecommendedNextAction);

public sealed record KnowledgeStateConsistencyReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedCatalogItems,
    int LoadedQualityItems,
    int LoadedEvidenceItems,
    int LoadedSourceConfirmationItems,
    int LoadedValidationStatusItems,
    int LoadedValidationPlans,
    int LoadedPromotionEntries,
    int LoadedMasterStatusSnapshots,
    int SourceCountMismatches,
    int TrustedStatusMismatches,
    int TimestampMismatches,
    int BlockerMismatches,
    int MissingItemIdMismatches,
    int RepairedItems,
    IReadOnlyList<KnowledgeStateConsistencyItem> Items,
    IReadOnlyList<string> RemainingIssues,
    IReadOnlyList<string> Warnings,
    string CatalogPath,
    string QualityPath,
    string EvidencePath,
    string SourceConfirmationsPath,
    string ValidationStatusPath,
    string ValidationPlansPath,
    string PromotionReportPath,
    string PromotionLogPath,
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

public sealed record KnowledgeTrustPromotionLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    string KnowledgeId,
    string Domain,
    string Title,
    string CurrentStatus,
    string RecommendedStatus,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    int SourceCount,
    int SourceTypeCount,
    int ValidationEvidenceCount,
    DateTimeOffset? LastValidatedUtc,
    string ValidationReadiness,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> MissingEvidenceCategories);

public sealed class KnowledgeStateConsistencyService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public KnowledgeStateConsistencyService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_consistency");

    public string ReportPath => Path.Combine(Root, "knowledge_state_consistency_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_state_consistency_report.md");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string EvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");

    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");

    public string ValidationStatusPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_validation_status.json");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string PromotionReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_promotion", "knowledge_trust_promotion_report.json");

    public string PromotionLogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_trust_promotion_log.jsonl");

    public string MasterStatusPath => Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json");

    public KnowledgeStateConsistencyReport Run(bool apply = false, bool dryRun = true)
    {
        Directory.CreateDirectory(Root);
        var updatedAt = DateTimeOffset.UtcNow;

        var catalog = LoadJson<List<KnowledgeCatalogItem>>(CatalogPath) ?? [];
        var quality = LoadJson<KnowledgeQualityReport>(QualityPath);
        var evidence = LoadJson<KnowledgeEvidenceReport>(EvidencePath);
        var confirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var validationStatus = LoadJson<KnowledgeValidationStatus>(ValidationStatusPath);
        var validationPlans = LoadJson<KnowledgeValidationPlanReport>(ValidationPlansPath);
        var promotionReport = LoadJson<KnowledgeTrustPromotionReport>(PromotionReportPath) ?? new KnowledgeTrustPromotionPipelineService(_storagePaths).Run(apply: false);
        var masterSnapshots = LoadMasterSnapshots();
        var promotionLog = LoadPromotionLog();
        var latestPromotionById = promotionLog
            .Where(entry => !string.IsNullOrWhiteSpace(entry.KnowledgeId))
            .GroupBy(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.TimestampUtc).First(), StringComparer.OrdinalIgnoreCase);

        var qualityById = quality?.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidence?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmations?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var planById = validationPlans?.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeValidationPlan>(StringComparer.OrdinalIgnoreCase);
        var validationById = LoadValidationExecutions()
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var validationExecutionsById = LoadValidationExecutions()
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var syncReport = LoadValidationStateSyncReport();
        var syncById = syncReport?.Items.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ValidationStateSynchronizerItem>(StringComparer.OrdinalIgnoreCase);
        var promotionById = promotionReport.Candidates
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var allIds = catalog.Select(item => item.Id)
            .Concat(qualityById.Keys)
            .Concat(evidenceById.Keys)
            .Concat(confirmationById.Keys)
            .Concat(planById.Keys)
            .Concat(promotionById.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var items = new List<KnowledgeStateConsistencyItem>();
        foreach (var knowledgeId in allIds)
        {
            var catalogItem = catalog.FirstOrDefault(item => item.Id.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
            var qualityItem = qualityById.GetValueOrDefault(knowledgeId);
            var evidenceItem = evidenceById.GetValueOrDefault(knowledgeId);
            var confirmation = confirmationById.GetValueOrDefault(knowledgeId);
            var plan = planById.GetValueOrDefault(knowledgeId);
            var latestValidation = validationById.GetValueOrDefault(knowledgeId);
            var executions = validationExecutionsById.GetValueOrDefault(knowledgeId) ?? [];
            var promotionItem = promotionById.GetValueOrDefault(knowledgeId);
            var syncItem = syncById.GetValueOrDefault(knowledgeId);
            var promotionLogEntry = latestPromotionById.GetValueOrDefault(knowledgeId);

            var sourceCountExpected = ExpectedSourceCount(catalogItem, confirmation, promotionLogEntry);
            var sourceCountBefore = confirmation?.SourceCount ?? 0;
            var sourceCountMismatch = sourceCountBefore != sourceCountExpected;

            var trustedStatusExpected = ExpectedTrustedStatus(catalogItem, qualityItem, promotionLogEntry);
            var trustedStatusActual = catalogItem?.ValidationStatus ?? qualityItem?.LifecycleStatus ?? "unknown";
            var trustedStatusMismatch = !trustedStatusActual.Equals(trustedStatusExpected, StringComparison.OrdinalIgnoreCase);

            var timestampMismatch = HasTimestampMismatch(catalogItem, qualityItem, latestValidation, promotionLogEntry);
            var expectedBlockers = ExpectedBlockers(qualityItem, confirmation, plan, latestValidation, syncItem, promotionItem, promotionLogEntry);
            var currentBlockers = (promotionItem?.Blockers ?? [])
                .Concat(promotionItem?.MissingEvidenceCategories ?? [])
                .Concat(syncItem?.RemainingBlockersAfter ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var blockerMismatch = !SequenceEqualIgnoreCase(currentBlockers, expectedBlockers);
            var missingItemIdMismatch = catalogItem is null || qualityItem is null || evidenceItem is null || confirmation is null || plan is null;
            var policyApprovedSecondSource = HasPolicyApprovedSecondSource(confirmation, promotionLogEntry);
            var hasValidationExecutions = executions.Count > 0 || latestValidation is not null;
            var validatedUtc = latestValidation?.CompletedAtUtc ?? qualityItem?.LastValidatedUtc ?? catalogItem?.LastValidatedUtc;
            var recommendedNextAction = RecommendedNextAction(sourceCountMismatch, trustedStatusMismatch, timestampMismatch, blockerMismatch, policyApprovedSecondSource, latestValidation, validationStatus);

            items.Add(new KnowledgeStateConsistencyItem(
                KnowledgeId: knowledgeId,
                Domain: catalogItem?.Domain ?? qualityItem?.Domain ?? evidenceItem?.Domain ?? promotionItem?.Domain ?? syncItem?.Domain ?? "unknown",
                Title: catalogItem?.Title ?? qualityItem?.Title ?? promotionItem?.Title ?? syncItem?.Title ?? "unknown",
                SourceCountBefore: sourceCountBefore,
                SourceCountExpected: sourceCountExpected,
                SourceCountMismatch: sourceCountMismatch,
                TrustedStatusMismatch: trustedStatusMismatch,
                TimestampMismatch: timestampMismatch,
                BlockerMismatch: blockerMismatch,
                MissingItemIdMismatch: missingItemIdMismatch,
                CatalogValidationStatus: catalogItem?.ValidationStatus ?? "missing",
                QualityLifecycleStatus: qualityItem?.LifecycleStatus ?? "missing",
                SourceConfirmationStatus: confirmation?.ReviewStatus ?? "missing",
                ValidationPlanStatus: plan?.Status ?? "missing",
                PromotionStatus: promotionItem?.RecommendedStatus ?? "unknown",
                LastValidatedUtc: validatedUtc,
                LatestValidationExecutionUtc: latestValidation?.CompletedAtUtc,
                ValidationScore: qualityItem?.ValidationScore ?? 0,
                TrustScore: qualityItem?.TrustScore ?? 0,
                QualityScore: qualityItem?.QualityScore ?? 0,
                PolicyApprovedSecondSource: policyApprovedSecondSource,
                HasValidationExecutions: hasValidationExecutions,
                CurrentBlockers: currentBlockers,
                ExpectedBlockers: expectedBlockers,
                Warnings: BuildFlagWarnings(sourceCountMismatch, trustedStatusMismatch, timestampMismatch, blockerMismatch, missingItemIdMismatch, policyApprovedSecondSource, validationStatus),
                RecommendedNextAction: recommendedNextAction));
        }

        var report = new KnowledgeStateConsistencyReport(
            ReportVersion: "knowledge_state_consistency_v1",
            UpdatedAtUtc: updatedAt,
            Status: apply && !dryRun ? "applied" : "dry_run_ready",
            LoadedCatalogItems: catalog.Count,
            LoadedQualityItems: quality?.Items.Count ?? 0,
            LoadedEvidenceItems: evidence?.Evidence.Count ?? 0,
            LoadedSourceConfirmationItems: confirmations?.Results.Count ?? 0,
            LoadedValidationStatusItems: validationStatus is null ? 0 : 1,
            LoadedValidationPlans: validationPlans?.Plans.Count ?? 0,
            LoadedPromotionEntries: promotionReport.Candidates.Count,
            LoadedMasterStatusSnapshots: masterSnapshots.Count,
            SourceCountMismatches: items.Count(item => item.SourceCountMismatch),
            TrustedStatusMismatches: items.Count(item => item.TrustedStatusMismatch),
            TimestampMismatches: items.Count(item => item.TimestampMismatch),
            BlockerMismatches: items.Count(item => item.BlockerMismatch),
            MissingItemIdMismatches: items.Count(item => item.MissingItemIdMismatch),
            RepairedItems: 0,
            Items: items,
            RemainingIssues: BuildRemainingIssues(items),
            Warnings: BuildReportWarnings(items),
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            EvidencePath: EvidencePath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            ValidationStatusPath: ValidationStatusPath,
            ValidationPlansPath: ValidationPlansPath,
            PromotionReportPath: PromotionReportPath,
            PromotionLogPath: PromotionLogPath,
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

        if (apply && !dryRun)
        {
            var repaired = RepairConsistency(items);
            report = report with
            {
                RepairedItems = repaired,
                Status = "applied"
            };
        }

        WriteReport(report);
        return report;
    }

    public KnowledgeStateConsistencyReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeStateConsistencyReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
    }

    private int RepairConsistency(IReadOnlyList<KnowledgeStateConsistencyItem> items)
    {
        var repaired = 0;

        if (items.Any(item => item.TrustedStatusMismatch || item.SourceCountMismatch))
        {
            repaired += RepairTrustedCatalogAndSourceCounts(items);
        }

        if (items.Any(item => item.TimestampMismatch || item.BlockerMismatch || item.SourceCountMismatch))
        {
            repaired += RepairValidationAndMasterState();
        }

        return repaired;
    }

    private int RepairTrustedCatalogAndSourceCounts(IReadOnlyList<KnowledgeStateConsistencyItem> items)
    {
        var repaired = 0;
        var promotionLog = LoadPromotionLog()
            .Where(entry => entry.Action.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.TimestampUtc).First(), StringComparer.OrdinalIgnoreCase);

        var catalog = LoadJson<List<KnowledgeCatalogItem>>(CatalogPath) ?? [];
        var catalogChanged = false;
        var confirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var confirmationChanged = false;

        for (var index = 0; index < catalog.Count; index++)
        {
            var item = catalog[index];
            if (!promotionLog.TryGetValue(item.Id, out var promotion))
            {
                continue;
            }

            if (!item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
                || !Nullable.Equals(item.LastValidatedUtc, promotion.TimestampUtc))
            {
                catalog[index] = item with
                {
                    ValidationStatus = "trusted",
                    LastValidatedUtc = promotion.TimestampUtc
                };
                catalogChanged = true;
                repaired++;
            }

            if (confirmations is null)
            {
                continue;
            }

            var results = confirmations.Results.ToList();
            var confirmationIndex = results.FindIndex(result => result.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (confirmationIndex < 0)
            {
                continue;
            }

            var confirmation = results[confirmationIndex];
            var expectedSourceCount = Math.Max(confirmation.SourceCount, promotion.SourceCount);
            var approvedCount = Math.Max(confirmation.PolicyApprovedSourceCount, expectedSourceCount - item.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            var reviewStatus = expectedSourceCount >= 2 ? "policy_approved_second_source" : confirmation.ReviewStatus;
            if (confirmation.SourceCount != expectedSourceCount
                || confirmation.PolicyApprovedSourceCount != approvedCount
                || !confirmation.ReviewStatus.Equals(reviewStatus, StringComparison.OrdinalIgnoreCase))
            {
                results[confirmationIndex] = confirmation with
                {
                    SourceCount = expectedSourceCount,
                    PolicyApprovedSourceCount = approvedCount,
                    ReviewStatus = reviewStatus,
                    Warnings = confirmation.Warnings
                        .Where(warning => !warning.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
                        .ToList()
                };
                confirmationChanged = true;
                repaired++;
            }

            confirmations = confirmations with { Results = results };
        }

        if (catalogChanged)
        {
            File.WriteAllText(CatalogPath, JsonSerializer.Serialize(catalog, JsonDefaults.WriteOptions));
        }

        if (confirmationChanged && confirmations is not null)
        {
            File.WriteAllText(SourceConfirmationsPath, JsonSerializer.Serialize(confirmations, JsonDefaults.WriteOptions));
        }

        return repaired;
    }

    private int RepairValidationAndMasterState()
    {
        var repaired = 0;
        _ = new SourceConfirmationEngine(_storagePaths).Build();
        _ = new ValidationStateSynchronizerService(_storagePaths).Run(apply: true, dryRun: false);
        _ = new KnowledgeQualityEngine(_storagePaths).Run();
        _ = new KnowledgeTrustPromotionPipelineService(_storagePaths).Run(apply: false);
        _ = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot)).WriteSnapshot();
        repaired++;
        return repaired;
    }

    private static int ExpectedSourceCount(
        KnowledgeCatalogItem? catalogItem,
        ConfirmationResult? confirmation,
        KnowledgeTrustPromotionLogEntry? promotionLogEntry)
    {
        var catalogCount = catalogItem?.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0;
        var confirmationCount = confirmation?.SourceCount ?? catalogCount;
        var approvedSources = confirmation?.CandidateSources?.Count(candidate =>
            candidate.AutoApprovedByPolicy
            || candidate.PolicyReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase)
            || candidate.SourceStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var promotionCount = promotionLogEntry?.SourceCount ?? 0;
        return Math.Max(confirmationCount, Math.Max(catalogCount + approvedSources, promotionCount));
    }

    private static string ExpectedTrustedStatus(
        KnowledgeCatalogItem? catalogItem,
        KnowledgeQualityItem? qualityItem,
        KnowledgeTrustPromotionLogEntry? promotionLogEntry)
    {
        if (catalogItem?.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase) == true
            || qualityItem?.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase) == true
            || promotionLogEntry?.Action.Equals("trusted", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "trusted";
        }

        return catalogItem?.ValidationStatus ?? qualityItem?.LifecycleStatus ?? "unknown";
    }

    private static bool HasTimestampMismatch(
        KnowledgeCatalogItem? catalogItem,
        KnowledgeQualityItem? qualityItem,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeTrustPromotionLogEntry? promotionLogEntry)
    {
        var catalogValidated = catalogItem?.LastValidatedUtc;
        var qualityValidated = qualityItem?.LastValidatedUtc;
        var validationValidated = latestValidation?.CompletedAtUtc ?? promotionLogEntry?.TimestampUtc;

        if (validationValidated is null)
        {
            return false;
        }

        if (catalogValidated is null && qualityValidated is null)
        {
            return true;
        }

        return (catalogValidated is not null && Math.Abs((validationValidated.Value - catalogValidated.Value).TotalMinutes) > 5)
            || (qualityValidated is not null && Math.Abs((validationValidated.Value - qualityValidated.Value).TotalMinutes) > 5);
    }

    private static IReadOnlyList<string> ExpectedBlockers(
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeValidationExecutionResult? latestValidation,
        ValidationStateSynchronizerItem? syncItem,
        KnowledgeTrustPromotionCandidate? promotionItem,
        KnowledgeTrustPromotionLogEntry? promotionLogEntry)
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

        var sourceCount = confirmation?.SourceCount ?? promotionLogEntry?.SourceCount ?? 0;
        if (sourceCount < 2)
        {
            blockers.Add(sourceCount == 0 ? "source_metadata_missing" : "second_independent_source_missing");
        }

        var hasValidationExecution = latestValidation is not null;
        var latestValidatedUtc = latestValidation?.CompletedAtUtc ?? qualityItem?.LastValidatedUtc ?? promotionLogEntry?.TimestampUtc;
        if (!hasValidationExecution || latestValidatedUtc is null)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (plan is null)
        {
            blockers.Add("validation_plan_missing");
        }
        else if (plan.MissingEvidence.Any(entry =>
            entry.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
            || entry.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
            || entry.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add("validation_plan_missing");
        }

        if (syncItem is not null)
        {
            blockers.AddRange(syncItem.RemainingBlockersAfter
                .Where(blocker =>
                    blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                    || blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
                    || blocker.Equals("validation_plan_or_requirement_missing", StringComparison.OrdinalIgnoreCase)
                    || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase)));
        }

        if (promotionItem is not null)
        {
            blockers.AddRange(promotionItem.Blockers);
            blockers.AddRange(promotionItem.MissingEvidenceCategories);
        }

        if (promotionLogEntry is not null && promotionLogEntry.Action.Equals("trusted", StringComparison.OrdinalIgnoreCase))
        {
            blockers.RemoveAll(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)
                || blocker.Equals("validation_plan_missing", StringComparison.OrdinalIgnoreCase)
                || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase));
        }

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SequenceEqualIgnoreCase(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);

    private static bool HasPolicyApprovedSecondSource(ConfirmationResult? confirmation, KnowledgeTrustPromotionLogEntry? promotionLogEntry) =>
        confirmation is not null && (
            confirmation.ReviewStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)
            || confirmation.PolicyApprovedSourceCount > 0
            || confirmation.CandidateSources?.Any(candidate =>
                candidate.AutoApprovedByPolicy
                || candidate.PolicyReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase)
                || candidate.SourceStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase)) == true
            || (promotionLogEntry is not null && promotionLogEntry.SourceCount >= 2));

    private static IReadOnlyList<string> BuildFlagWarnings(params bool[] flags)
    {
        var warnings = new List<string>();
        if (flags.Length > 0 && flags[0])
        {
            warnings.Add("source_count_mismatch");
        }
        if (flags.Length > 1 && flags[1])
        {
            warnings.Add("trusted_status_mismatch");
        }
        if (flags.Length > 2 && flags[2])
        {
            warnings.Add("timestamp_mismatch");
        }
        if (flags.Length > 3 && flags[3])
        {
            warnings.Add("blocker_mismatch");
        }
        if (flags.Length > 4 && flags[4])
        {
            warnings.Add("missing_item_id_mismatch");
        }
        if (flags.Length > 5 && flags[5])
        {
            warnings.Add("policy_approved_second_source_present");
        }
        if (flags.Length > 6 && flags[6])
        {
            warnings.Add("validation_status_missing");
        }
        return warnings;
    }

    private static IReadOnlyList<string> BuildFlagWarnings(
        bool sourceCountMismatch,
        bool trustedStatusMismatch,
        bool timestampMismatch,
        bool blockerMismatch,
        bool missingItemIdMismatch,
        bool policyApprovedSecondSource,
        KnowledgeValidationStatus? validationStatus)
    {
        var warnings = BuildFlagWarnings(
            sourceCountMismatch,
            trustedStatusMismatch,
            timestampMismatch,
            blockerMismatch,
            missingItemIdMismatch,
            policyApprovedSecondSource,
            validationStatus is null);

        if (validationStatus is not null && validationStatus.ValidationRoutingHealth.Equals("invalid", StringComparison.OrdinalIgnoreCase))
        {
            warnings = warnings.Concat(["validation_routing_invalid"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return warnings;
    }

    private static IReadOnlyList<string> BuildReportWarnings(IReadOnlyList<KnowledgeStateConsistencyItem> items)
    {
        var warnings = new List<string>();
        if (items.Any(item => item.SourceCountMismatch))
        {
            warnings.Add("source_count_mismatch");
        }
        if (items.Any(item => item.TrustedStatusMismatch))
        {
            warnings.Add("trusted_status_mismatch");
        }
        if (items.Any(item => item.TimestampMismatch))
        {
            warnings.Add("timestamp_mismatch");
        }
        if (items.Any(item => item.BlockerMismatch))
        {
            warnings.Add("blocker_mismatch");
        }
        if (items.Any(item => item.MissingItemIdMismatch))
        {
            warnings.Add("missing_item_id_mismatch");
        }
        return warnings;
    }

    private static IReadOnlyList<string> BuildRemainingIssues(IReadOnlyList<KnowledgeStateConsistencyItem> items) =>
        items.Where(item => item.SourceCountMismatch || item.TrustedStatusMismatch || item.TimestampMismatch || item.BlockerMismatch || item.MissingItemIdMismatch)
            .Select(item => $"{item.KnowledgeId}:{item.RecommendedNextAction}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string RecommendedNextAction(
        bool sourceCountMismatch,
        bool trustedStatusMismatch,
        bool timestampMismatch,
        bool blockerMismatch,
        bool policyApprovedSecondSource,
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationStatus? validationStatus)
    {
        if (trustedStatusMismatch)
        {
            return "restore_trusted_catalog_state";
        }

        if (sourceCountMismatch)
        {
            return "rebuild_source_confirmations";
        }

        if (timestampMismatch || blockerMismatch)
        {
            return "run_validation_state_synchronizer";
        }

        if (policyApprovedSecondSource && latestValidation is not null && validationStatus?.ValidationPlansOpen > 0)
        {
            return "refresh_validation_state";
        }

        return "no_action_required";
    }

    private IReadOnlyList<ReviewStatusConsistencySnapshot> LoadMasterSnapshots()
    {
        var candidates = new List<(string Source, string Path)>
        {
            ("master-status", MasterStatusPath),
            ("master-status-local", Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "master-status", "master_status.json")),
        };

        var snapshots = new List<ReviewStatusConsistencySnapshot>();
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate.Path))
            {
                continue;
            }

            try
            {
                var raw = JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(candidate.Path), JsonDefaults.SnapshotReadOptions);
                if (raw is null)
                {
                    continue;
                }

                snapshots.Add(new ReviewStatusConsistencySnapshot(
                    Source: candidate.Source,
                    Path: candidate.Path,
                    LastUpdatedUtc: raw.LastUpdatedUtc,
                    PendingReviews: raw.PendingReviews,
                    NeedsMoreEvidenceReviews: raw.NeedsMoreEvidenceReviews,
                    TopReviewPriorities: raw.TopReviewPriorities));
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                snapshots.Add(new ReviewStatusConsistencySnapshot(
                    Source: candidate.Source,
                    Path: candidate.Path,
                    LastUpdatedUtc: DateTimeOffset.MinValue,
                    PendingReviews: -1,
                    NeedsMoreEvidenceReviews: -1,
                    TopReviewPriorities: []));
            }
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.LastUpdatedUtc)
            .ToList();
    }

    private IReadOnlyList<KnowledgeTrustPromotionLogEntry> LoadPromotionLog()
    {
        if (!File.Exists(PromotionLogPath))
        {
            return [];
        }

        var entries = new List<KnowledgeTrustPromotionLogEntry>();
        foreach (var line in File.ReadAllLines(PromotionLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<KnowledgeTrustPromotionLogEntry>(line, JsonDefaults.SnapshotReadOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // ignore malformed log entries
            }
        }

        return entries;
    }

    private IReadOnlyList<KnowledgeValidationExecutionResult> LoadValidationExecutions()
    {
        var path = Path.Combine(_storagePaths.Root, "cognitive_core", "validation_execution.jsonl");
        if (!File.Exists(path))
        {
            return [];
        }

        var results = new List<KnowledgeValidationExecutionResult>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var result = JsonSerializer.Deserialize<KnowledgeValidationExecutionResult>(line, JsonDefaults.SnapshotReadOptions);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
            catch (JsonException)
            {
                // ignore malformed entries
            }
        }

        return results;
    }

    private ValidationStateSynchronizerReport? LoadValidationStateSyncReport()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "validation_state_sync", "validation_state_sync_report.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationStateSynchronizerReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
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

    private void WriteReport(KnowledgeStateConsistencyReport report)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeStateConsistencyReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge State Consistency");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Loaded Catalog Items: {report.LoadedCatalogItems}");
        sb.AppendLine($"- Loaded Quality Items: {report.LoadedQualityItems}");
        sb.AppendLine($"- Loaded Evidence Items: {report.LoadedEvidenceItems}");
        sb.AppendLine($"- Loaded Source Confirmations: {report.LoadedSourceConfirmationItems}");
        sb.AppendLine($"- Loaded Validation Status: {report.LoadedValidationStatusItems}");
        sb.AppendLine($"- Loaded Validation Plans: {report.LoadedValidationPlans}");
        sb.AppendLine($"- Loaded Promotion Entries: {report.LoadedPromotionEntries}");
        sb.AppendLine($"- Loaded Master Status Snapshots: {report.LoadedMasterStatusSnapshots}");
        sb.AppendLine($"- Source Count Mismatches: {report.SourceCountMismatches}");
        sb.AppendLine($"- Trusted Status Mismatches: {report.TrustedStatusMismatches}");
        sb.AppendLine($"- Timestamp Mismatches: {report.TimestampMismatches}");
        sb.AppendLine($"- Blocker Mismatches: {report.BlockerMismatches}");
        sb.AppendLine($"- Missing Item ID Mismatches: {report.MissingItemIdMismatches}");
        sb.AppendLine($"- Repaired Items: {report.RepairedItems}");
        sb.AppendLine($"- Dry Run: {report.DryRun}");
        sb.AppendLine($"- Applied: {report.Applied}");
        sb.AppendLine();
        sb.AppendLine("## Remaining Issues");
        foreach (var issue in report.RemainingIssues)
        {
            sb.AppendLine($"- {issue}");
        }
        if (report.RemainingIssues.Count == 0)
        {
            sb.AppendLine("- none");
        }

        sb.AppendLine();
        sb.AppendLine("## Targeted Items");
        foreach (var id in new[] { "trading:bearish_engulfing", "trading:liquidity_sweep", "trading:inside_bar" })
        {
            var item = report.Items.FirstOrDefault(entry => entry.KnowledgeId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                sb.AppendLine($"- {id}: not found");
                continue;
            }

            sb.AppendLine($"### {item.Title} / {item.KnowledgeId}");
            sb.AppendLine($"- Source Count: {item.SourceCountBefore} -> {item.SourceCountExpected}");
            sb.AppendLine($"- Catalog Validation Status: {item.CatalogValidationStatus}");
            sb.AppendLine($"- Quality Lifecycle Status: {item.QualityLifecycleStatus}");
            sb.AppendLine($"- Source Confirmation Status: {item.SourceConfirmationStatus}");
            sb.AppendLine($"- Validation Plan Status: {item.ValidationPlanStatus}");
            sb.AppendLine($"- Promotion Status: {item.PromotionStatus}");
            sb.AppendLine($"- Validation Score: {item.ValidationScore:0.###}");
            sb.AppendLine($"- Trust Score: {item.TrustScore:0.###}");
            sb.AppendLine($"- Quality Score: {item.QualityScore:0.###}");
            sb.AppendLine($"- Current Blockers: {string.Join(", ", item.CurrentBlockers)}");
            sb.AppendLine($"- Expected Blockers: {string.Join(", ", item.ExpectedBlockers)}");
            sb.AppendLine($"- Recommended Next Action: {item.RecommendedNextAction}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
