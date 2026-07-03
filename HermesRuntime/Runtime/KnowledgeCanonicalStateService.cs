using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeCanonicalStateItem(
    string KnowledgeItemId,
    string Domain,
    string Title,
    string CanonicalStatus,
    string TrustClass,
    int SourceCount,
    int IndependentSourceCount,
    int PolicyApprovedSourceCount,
    bool HasTwoIndependentSources,
    bool HasPolicyApprovedSecondSource,
    string ValidationStatus,
    string ValidationReadiness,
    bool HasFreshValidation,
    bool HumanReviewRequired,
    bool HasBlockingContradiction,
    IReadOnlyList<string> CanonicalBlockers);

public sealed record KnowledgeCanonicalStateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalItems,
    int TrustedKnowledgeExternal,
    int TrustedKnowledgeInternal,
    int ImplementationVerifiedKnowledge,
    int TrustedKnowledgeTotal,
    int PromisingKnowledge,
    int WeakKnowledge,
    int NeedsMoreDataKnowledge,
    int RejectedKnowledge,
    IReadOnlyDictionary<string, int> CanonicalStatusCounts,
    IReadOnlyDictionary<string, int> TrustClassCounts,
    IReadOnlyList<KnowledgeCanonicalStateItem> Items,
    IReadOnlyList<string> Warnings,
    string CatalogPath,
    string QualityPath,
    string EvidencePath,
    string SourceConfirmationsPath,
    string ValidationPlansPath,
    string ValidationStatusPath,
    string ValidationExecutionLogPath,
    string InternalValidationPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeCanonicalStateService
{
    private static readonly IReadOnlySet<string> InternalDomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "documentation",
            "software",
            "process",
            "research"
        };

    private readonly StoragePaths _storagePaths;

    public KnowledgeCanonicalStateService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_canonical_state");

    public string ReportPath => Path.Combine(Root, "knowledge_canonical_state_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_canonical_state_report.md");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string EvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");

    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string ValidationStatusPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_validation_status.json");

    public string ValidationExecutionLogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_execution.jsonl");

    public string InternalValidationPath => Path.Combine(_storagePaths.Root, "reports", "internal_knowledge_validation", "internal_knowledge_validation_report.json");

    public KnowledgeCanonicalStateReport Run()
    {
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadReport()
            ?? new KnowledgeQualityEngine(_storagePaths).Run();
        return BuildFromQualityItems(quality.Items);
    }

    public KnowledgeCanonicalStateReport BuildFromQualityItems(IReadOnlyList<KnowledgeQualityItem> qualityItems)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var evidence = LoadJson<KnowledgeEvidenceReport>(EvidencePath);
        var evidenceById = evidence?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
        var confirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var confirmationById = confirmations?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var plans = LoadJson<KnowledgeValidationPlanReport>(ValidationPlansPath);
        var planById = plans?.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeValidationPlan>(StringComparer.OrdinalIgnoreCase);
        var validationStatus = LoadJson<KnowledgeValidationStatus>(ValidationStatusPath);
        var validationExecutions = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var latestValidationById = validationExecutions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.CompletedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
        var internalValidation = LoadJson<InternalKnowledgeValidationReport>(InternalValidationPath);
        var internalById = internalValidation?.Items.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, InternalKnowledgeValidationItem>(StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionsById = contradictions.Contradictions
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var reviews = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var latestReviewById = reviews.Reviews
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ReviewedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var items = qualityItems
            .Select(item => BuildItem(
                item,
                catalogById.GetValueOrDefault(item.KnowledgeId),
                confirmationById.GetValueOrDefault(item.KnowledgeId),
                planById.GetValueOrDefault(item.KnowledgeId),
                latestValidationById.GetValueOrDefault(item.KnowledgeId),
                internalById.GetValueOrDefault(item.KnowledgeId),
                contradictionsById.GetValueOrDefault(item.KnowledgeId),
                latestReviewById.GetValueOrDefault(item.KnowledgeId),
                now))
            .ToList();

        var canonicalStatusCounts = items
            .GroupBy(item => item.CanonicalStatus, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var trustClassCounts = items
            .GroupBy(item => item.TrustClass, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var report = new KnowledgeCanonicalStateReport(
            ReportVersion: "knowledge_canonical_state_v1",
            UpdatedAtUtc: now,
            TotalItems: items.Count,
            TrustedKnowledgeExternal: items.Count(item => item.TrustClass.Equals("external_trusted", StringComparison.OrdinalIgnoreCase)),
            TrustedKnowledgeInternal: items.Count(item => item.TrustClass.Equals("internal_trusted", StringComparison.OrdinalIgnoreCase)),
            ImplementationVerifiedKnowledge: items.Count(item => item.CanonicalStatus.Equals("implementation_verified", StringComparison.OrdinalIgnoreCase)),
            TrustedKnowledgeTotal: items.Count(item => item.CanonicalStatus is "trusted" or "internal_trusted" or "implementation_verified"),
            PromisingKnowledge: items.Count(item => item.CanonicalStatus.Equals("promising", StringComparison.OrdinalIgnoreCase)),
            WeakKnowledge: items.Count(item => item.CanonicalStatus.Equals("weak", StringComparison.OrdinalIgnoreCase)),
            NeedsMoreDataKnowledge: items.Count(item => item.CanonicalStatus.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase)),
            RejectedKnowledge: items.Count(item => item.CanonicalStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            CanonicalStatusCounts: canonicalStatusCounts,
            TrustClassCounts: trustClassCounts,
            Items: items,
            Warnings: BuildWarnings(items, qualityItems.Count),
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            EvidencePath: EvidencePath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            ValidationPlansPath: ValidationPlansPath,
            ValidationStatusPath: ValidationStatusPath,
            ValidationExecutionLogPath: ValidationExecutionLogPath,
            InternalValidationPath: InternalValidationPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        return report;
    }

    public KnowledgeCanonicalStateReport? LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeCanonicalStateReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public KnowledgeCanonicalStateReport LoadOrCreateReport() => LoadStatus() ?? Run();

    private static KnowledgeCanonicalStateItem BuildItem(
        KnowledgeQualityItem qualityItem,
        KnowledgeCatalogItem? catalogItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeValidationExecutionResult? latestValidation,
        InternalKnowledgeValidationItem? internalValidation,
        IReadOnlyList<ContradictionRecord>? contradictions,
        HumanReviewEvidence? latestReview,
        DateTimeOffset now)
    {
        var domain = qualityItem.Domain;
        var title = catalogItem?.Title ?? qualityItem.Title;
        var isInternal = IsInternalKnowledge(domain, qualityItem.KnowledgeId, catalogItem);
        var internalPass = IsInternalValidationPassing(internalValidation);
        var sourceCount = confirmation is null
            ? Math.Max(qualityItem.EvidenceRefs.Count(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase)), catalogItem?.SourceIds.Count ?? 0)
            : SourceConfirmationEngine.CanonicalSourceCount(catalogItem ?? new KnowledgeCatalogItem(
                qualityItem.KnowledgeId,
                domain,
                title,
                string.Empty,
                [],
                0,
                qualityItem.LifecycleStatus,
                [],
                qualityItem.LastValidatedUtc,
                []), confirmation);
        var independentSourceCount = Math.Max(0, sourceCount);
        var policyApprovedSourceCount = confirmation?.PolicyApprovedSourceCount ?? 0;
        var hasTwoIndependentSources = sourceCount >= 2;
        var hasPolicyApprovedSecondSource = policyApprovedSourceCount > 0
            || confirmation?.ReviewStatus.Equals("policy_approved_second_source", StringComparison.OrdinalIgnoreCase) == true;
        var validationStatus = latestValidation?.Status
            ?? plan?.Status
            ?? qualityItem.LifecycleStatus;
        var validationReadiness = DetermineValidationReadiness(latestValidation, plan, isInternal && internalPass);
        var hasFreshValidation = DetermineFreshValidation(qualityItem.LastValidatedUtc, latestValidation, isInternal && internalPass, internalValidation, now);
        var humanReviewRequired = isInternal && internalPass
            ? false
            : latestReview is null
                || latestReview.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase)
                || latestReview.Result.Equals("rejected", StringComparison.OrdinalIgnoreCase);
        var hasBlockingContradiction = contradictions is not null && contradictions.Count > 0;

        var canonicalBlockers = BuildCanonicalBlockers(
            qualityItem,
            sourceCount,
            hasTwoIndependentSources,
            hasPolicyApprovedSecondSource,
            validationReadiness,
            hasFreshValidation,
            humanReviewRequired,
            hasBlockingContradiction,
            isInternal,
            internalPass,
            contradictions);

        var canonicalStatus = DetermineCanonicalStatus(qualityItem, canonicalBlockers, isInternal, internalPass, validationReadiness, hasFreshValidation, hasBlockingContradiction);
        var trustClass = DetermineTrustClass(canonicalStatus);

        return new KnowledgeCanonicalStateItem(
            KnowledgeItemId: qualityItem.KnowledgeId,
            Domain: domain,
            Title: title,
            CanonicalStatus: canonicalStatus,
            TrustClass: trustClass,
            SourceCount: sourceCount,
            IndependentSourceCount: independentSourceCount,
            PolicyApprovedSourceCount: policyApprovedSourceCount,
            HasTwoIndependentSources: hasTwoIndependentSources,
            HasPolicyApprovedSecondSource: hasPolicyApprovedSecondSource,
            ValidationStatus: validationStatus,
            ValidationReadiness: validationReadiness,
            HasFreshValidation: hasFreshValidation,
            HumanReviewRequired: humanReviewRequired,
            HasBlockingContradiction: hasBlockingContradiction,
            CanonicalBlockers: canonicalBlockers);
    }

    private static IReadOnlyList<string> BuildCanonicalBlockers(
        KnowledgeQualityItem qualityItem,
        int sourceCount,
        bool hasTwoIndependentSources,
        bool hasPolicyApprovedSecondSource,
        string validationReadiness,
        bool hasFreshValidation,
        bool humanReviewRequired,
        bool hasBlockingContradiction,
        bool isInternal,
        bool internalPass,
        IReadOnlyList<ContradictionRecord>? contradictions)
    {
        var blockers = new List<string>();

        if (isInternal && internalPass)
        {
            if (hasBlockingContradiction)
            {
                blockers.Add("blocking_contradiction");
            }

            if (humanReviewRequired)
            {
                blockers.Add("human_review_required");
            }

            return blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (qualityItem.TrustScore < 0.64)
        {
            blockers.Add("trust_score_too_low");
        }

        if (qualityItem.QualityScore < 0.64)
        {
            blockers.Add("quality_score_too_low");
        }

        if (qualityItem.ValidationScore < 0.6)
        {
            blockers.Add("validation_score_too_low");
        }

        if (!hasTwoIndependentSources)
        {
            blockers.Add(sourceCount <= 0 ? "source_metadata_missing" : "second_independent_source_missing");
        }

        if (!hasFreshValidation)
        {
            blockers.Add("fresh_validation_timestamp_missing");
        }

        if (!validationReadiness.Equals("passed", StringComparison.OrdinalIgnoreCase)
            && !validationReadiness.Equals("completed_with_missing_noncritical_evidence", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("domain_validation_not_passed");
        }

        if (!hasPolicyApprovedSecondSource && blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add("policy_approved_second_source_missing");
        }

        if (hasBlockingContradiction || (contradictions?.Count ?? 0) > 0)
        {
            blockers.Add("blocking_contradiction");
        }

        if (humanReviewRequired)
        {
            blockers.Add("human_review_pending");
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string DetermineCanonicalStatus(
        KnowledgeQualityItem qualityItem,
        IReadOnlyList<string> canonicalBlockers,
        bool isInternal,
        bool internalPass,
        string validationReadiness,
        bool hasFreshValidation,
        bool hasBlockingContradiction)
    {
        if (isInternal && internalPass && !hasBlockingContradiction)
        {
            return qualityItem.Domain.Equals("software", StringComparison.OrdinalIgnoreCase)
                ? "implementation_verified"
                : "internal_trusted";
        }

        if (canonicalBlockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "rejected";
        }

        if (qualityItem.TrustScore < 0.38 || qualityItem.QualityScore < 0.38)
        {
            return "weak";
        }

        if (qualityItem.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)
            || (qualityItem.TrustScore >= 0.64 && qualityItem.QualityScore >= 0.64 && qualityItem.ValidationScore >= 0.6 && hasFreshValidation && validationReadiness is "passed" or "completed_with_missing_noncritical_evidence"))
        {
            return "trusted";
        }

        if (qualityItem.LifecycleStatus.Equals("promising", StringComparison.OrdinalIgnoreCase)
            || qualityItem.ValidationScore >= 0.45)
        {
            return "promising";
        }

        return "needs_more_data";
    }

    private static string DetermineTrustClass(string canonicalStatus) =>
        canonicalStatus switch
        {
            "trusted" => "external_trusted",
            "internal_trusted" => "internal_trusted",
            "implementation_verified" => "internal_trusted",
            "promising" or "needs_more_data" => "candidate",
            "weak" => "weak",
            "rejected" => "rejected",
            _ => "candidate"
        };

    private static string DetermineValidationReadiness(
        KnowledgeValidationExecutionResult? latestValidation,
        KnowledgeValidationPlan? plan,
        bool internalPass)
    {
        if (internalPass)
        {
            return "passed";
        }

        if (latestValidation is null)
        {
            return plan is null ? "validation_missing" : "blocked_waiting_for_evidence";
        }

        if (latestValidation.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (latestValidation.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
        {
            return latestValidation.Warnings.Any(warning => warning.Contains("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
                ? "blocked_waiting_for_evidence"
                : "needs_more_data";
        }

        return latestValidation.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            ? "validation_failed"
            : "blocked";
    }

    private static bool DetermineFreshValidation(
        DateTimeOffset? lastValidatedUtc,
        KnowledgeValidationExecutionResult? latestValidation,
        bool internalPass,
        InternalKnowledgeValidationItem? internalValidation,
        DateTimeOffset now)
    {
        if (internalPass)
        {
            return internalValidation?.ValidationStatusAfter.Equals("validated", StringComparison.OrdinalIgnoreCase) == true
                || internalValidation?.BuildSucceeded == true;
        }

        var validatedUtc = latestValidation?.CompletedAtUtc ?? lastValidatedUtc;
        return validatedUtc is not null && now - validatedUtc.Value <= TimeSpan.FromDays(180);
    }

    private static bool IsInternalKnowledge(string domain, string knowledgeId, KnowledgeCatalogItem? catalogItem) =>
        InternalDomains.Contains(domain)
        || knowledgeId.StartsWith("software:", StringComparison.OrdinalIgnoreCase)
        || knowledgeId.StartsWith("documentation:", StringComparison.OrdinalIgnoreCase)
        || knowledgeId.StartsWith("process:", StringComparison.OrdinalIgnoreCase)
        || knowledgeId.StartsWith("research:", StringComparison.OrdinalIgnoreCase)
        || catalogItem?.Title.Contains(".cs", StringComparison.OrdinalIgnoreCase) == true
        || catalogItem?.Title.Contains(".md", StringComparison.OrdinalIgnoreCase) == true
        || catalogItem?.Title.Contains("architecture", StringComparison.OrdinalIgnoreCase) == true
        || catalogItem?.Title.Contains("roadmap", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInternalValidationPassing(InternalKnowledgeValidationItem? item)
    {
        if (item is null)
        {
            return false;
        }

        if (!item.BuildSucceeded || !item.FileExists || !item.CliCommandExists || !item.ReportOrConfigExists)
        {
            return false;
        }

        if (item.ValidationStatusAfter.Equals("validated", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item.EvidenceWritten && item.BuildSucceeded && item.FileExists;
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<KnowledgeCanonicalStateItem> items, int totalItems)
    {
        var warnings = new List<string>();
        if (totalItems == 0)
        {
            warnings.Add("knowledge_catalog_empty");
        }

        if (items.Any(item => item.CanonicalStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("canonical_rejections_present");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static T? LoadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void WriteReport(KnowledgeCanonicalStateReport report)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        var markdown = BuildMarkdown(report);
        File.WriteAllText(MarkdownPath, markdown);
    }

    private static string BuildMarkdown(KnowledgeCanonicalStateReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Canonical State");
        sb.AppendLine();
        sb.AppendLine($"- trusted_external: {report.TrustedKnowledgeExternal}");
        sb.AppendLine($"- trusted_internal: {report.TrustedKnowledgeInternal}");
        sb.AppendLine($"- implementation_verified: {report.ImplementationVerifiedKnowledge}");
        sb.AppendLine($"- trusted_total: {report.TrustedKnowledgeTotal}");
        sb.AppendLine($"- promising: {report.PromisingKnowledge}");
        sb.AppendLine($"- weak: {report.WeakKnowledge}");
        sb.AppendLine($"- needs_more_data: {report.NeedsMoreDataKnowledge}");
        sb.AppendLine($"- rejected: {report.RejectedKnowledge}");
        sb.AppendLine();
        foreach (var item in report.Items.OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeItemId, StringComparer.Ordinal).Take(120))
        {
            sb.AppendLine($"## {item.KnowledgeItemId}");
            sb.AppendLine($"- canonical_status: {item.CanonicalStatus}");
            sb.AppendLine($"- trust_class: {item.TrustClass}");
            sb.AppendLine($"- source_count: {item.SourceCount}");
            sb.AppendLine($"- independent_source_count: {item.IndependentSourceCount}");
            sb.AppendLine($"- policy_approved_source_count: {item.PolicyApprovedSourceCount}");
            sb.AppendLine($"- validation_readiness: {item.ValidationReadiness}");
            sb.AppendLine($"- blockers: {string.Join(", ", item.CanonicalBlockers)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
