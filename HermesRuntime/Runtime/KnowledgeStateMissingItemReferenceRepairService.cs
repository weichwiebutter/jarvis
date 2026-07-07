using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeStateMissingItemReferenceRepairItem(
    string KnowledgeItemId,
    string Title,
    string Domain,
    string CurrentPlanStatus,
    string CurrentCatalogStatus,
    string RepairStatus,
    DateTimeOffset? LastValidatedUtc,
    int SourceCount,
    IReadOnlyList<string> Warnings);

public sealed record KnowledgeStateMissingItemReferenceRepairReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedValidationPlans,
    int TargetItems,
    int RepairedItems,
    int SkippedItems,
    IReadOnlyList<KnowledgeStateMissingItemReferenceRepairItem> Items,
    IReadOnlyList<string> Warnings,
    string ValidationPlansPath,
    string CatalogPath,
    string QualityPath,
    string EvidencePath,
    string SourceConfirmationsPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool DryRun,
    bool Applied);

public sealed class KnowledgeStateMissingItemReferenceRepairService
{
    private static readonly IReadOnlySet<string> DefaultTargetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "software:code_module_QueueManager",
        "software:code_module_StorageCleanupSafetyAuditService"
    };

    private readonly StoragePaths _storagePaths;

    public KnowledgeStateMissingItemReferenceRepairService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_missing_item_reference_repair");

    public string ReportPath => Path.Combine(Root, "knowledge_state_missing_item_reference_repair_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_state_missing_item_reference_repair_report.md");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string EvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");

    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");

    public KnowledgeStateMissingItemReferenceRepairReport Run(bool apply, bool dryRun, IReadOnlyCollection<string>? targetIds = null)
    {
        Directory.CreateDirectory(Root);

        var updatedAt = DateTimeOffset.UtcNow;
        var targets = (targetIds is { Count: > 0 } ? targetIds : DefaultTargetIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var planReport = LoadJson<KnowledgeValidationPlanReport>(ValidationPlansPath) ?? new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport() ?? new KnowledgeValidationStrategy(_storagePaths).GeneratePlans(50);
        var planById = planReport.Plans.ToDictionary(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);
        var catalog = new KnowledgeCatalog(_storagePaths).LoadItems().ToList();
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadReport();
        var evidenceReport = LoadJson<KnowledgeEvidenceReport>(EvidencePath);
        var confirmationReport = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var executions = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var latestExecutionById = executions
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var qualityById = qualityReport?.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidenceReport?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmationReport?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);

        var repairedItems = new List<KnowledgeStateMissingItemReferenceRepairItem>();
        var catalogChanged = false;
        var qualityChanged = false;
        var evidenceChanged = false;
        var confirmationChanged = false;

        foreach (var knowledgeId in targets)
        {
            if (!planById.TryGetValue(knowledgeId, out var plan))
            {
                repairedItems.Add(new KnowledgeStateMissingItemReferenceRepairItem(
                    KnowledgeItemId: knowledgeId,
                    Title: "missing_plan",
                    Domain: "unknown",
                    CurrentPlanStatus: "missing",
                    CurrentCatalogStatus: "missing",
                    RepairStatus: "skipped_no_validation_plan",
                    LastValidatedUtc: null,
                    SourceCount: 0,
                    Warnings: ["validation_plan_missing"]));
                continue;
            }

            var lastValidatedUtc = latestExecutionById.GetValueOrDefault(knowledgeId)?.CompletedAtUtc;
            var sourceCount = DetermineSourceCount(plan, evidenceById.GetValueOrDefault(knowledgeId), confirmationById.GetValueOrDefault(knowledgeId));
            var title = NormalizeTitle(plan.Title, knowledgeId);
            var domain = NormalizeDomain(plan.Domain);
            var currentCatalogStatus = catalogById.TryGetValue(knowledgeId, out var catalogItem) ? catalogItem.ValidationStatus : "missing";
            var repairWarnings = new List<string>();

            if (catalogItem is null)
            {
                catalog.Add(new KnowledgeCatalogItem(
                    Id: knowledgeId,
                    Domain: domain,
                    Title: title,
                    DescriptionShort: plan.Title,
                    SourceIds: [],
                    Confidence: 0.18,
                    ValidationStatus: "needs_review",
                    Tags: ExtractTags(plan),
                    LastValidatedUtc: lastValidatedUtc,
                    RelatedItems: []));
                catalogChanged = true;
            }
            else if (catalogItem.Domain.Equals("unknown", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(catalogItem.Title))
            {
                var index = catalog.FindIndex(item => item.Id.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    catalog[index] = catalog[index] with
                    {
                        Domain = domain,
                        Title = title,
                        DescriptionShort = string.IsNullOrWhiteSpace(catalog[index].DescriptionShort) ? plan.Title : catalog[index].DescriptionShort,
                        LastValidatedUtc = lastValidatedUtc
                    };
                    catalogChanged = true;
                }
            }

            if (!qualityById.TryGetValue(knowledgeId, out var qualityItem))
            {
                var newQuality = new KnowledgeQualityItem(
                    KnowledgeId: knowledgeId,
                    Domain: domain,
                    Title: title,
                    LifecycleStatus: plan.CurrentStatus.Equals("experimental", StringComparison.OrdinalIgnoreCase) ? "experimental" : "needs_review",
                    RetentionState: "active",
                    TrustScore: 0,
                    EvidenceScore: 0,
                    ReuseScore: 0,
                    ValidationScore: 0,
                    AgeScore: 0,
                    QualityScore: 0,
                    EvidenceRefs: [],
                    SupportingGoals: [],
                    SupportingOutcomes: [],
                    Reasons: ["reconstructed_missing_item_reference"],
                    LastValidatedUtc: lastValidatedUtc);
                qualityById[knowledgeId] = newQuality;
                qualityChanged = true;
            }
            else if (qualityItem.Title.Equals("unknown", StringComparison.OrdinalIgnoreCase) || qualityItem.Domain.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                qualityById[knowledgeId] = qualityItem with
                {
                    Domain = domain,
                    Title = title,
                    LastValidatedUtc = lastValidatedUtc
                };
                qualityChanged = true;
            }

            if (!evidenceById.ContainsKey(knowledgeId))
            {
                evidenceById[knowledgeId] = new KnowledgeEvidenceEntry(
                    KnowledgeId: knowledgeId,
                    Domain: domain,
                    SourceIds: [],
                    SourceEvidenceRefs: [],
                    ValidationEvidenceRefs: latestExecutionById.TryGetValue(knowledgeId, out var latestExecution)
                        ? [$"validation:{latestExecution.ExecutionId}:{latestExecution.OutcomeStatus}"]
                        : [],
                    OutcomeRefs: [],
                    GoalRefs: [],
                    QueueRefs: [],
                    RelatedItems: [],
                    UpdatedAtUtc: updatedAt,
                    HumanReviewRequired: true);
                evidenceChanged = true;
            }

            if (!confirmationById.TryGetValue(knowledgeId, out var confirmation))
            {
                confirmationById[knowledgeId] = new ConfirmationResult(
                    KnowledgeId: knowledgeId,
                    Domain: domain,
                    ConfirmationLevel: sourceCount >= 2 ? "validated" : "single_source",
                    ConfirmationScore: sourceCount >= 2 ? 0.52 : 0.18,
                    SourceCount: sourceCount,
                    SourceTypeCount: 0,
                    SourceTimeBucketCount: 0,
                    ValidationEvidenceCount: latestExecutionById.ContainsKey(knowledgeId) ? 1 : 0,
                    HumanApproved: false,
                    EvidenceRefs: latestExecutionById.TryGetValue(knowledgeId, out var execution)
                        ? [$"validation:{execution.ExecutionId}:{execution.OutcomeStatus}"]
                        : [],
                    Warnings: sourceCount >= 2 ? [] : ["source_metadata_missing"],
                    CandidateSourceCount: 0,
                    IndependentSourceCandidateCount: 0,
                    PolicyApprovedSourceCount: 0,
                    ReviewStatus: sourceCount >= 2 ? "candidate_second_source" : "awaiting_human_review",
                    CandidateSources: []);
                confirmationChanged = true;
            }
            else if (confirmation.SourceCount == 0 && sourceCount > 0)
            {
                confirmationById[knowledgeId] = confirmation with
                {
                    SourceCount = sourceCount,
                    ReviewStatus = sourceCount >= 2 ? "candidate_second_source" : confirmation.ReviewStatus,
                    EvidenceRefs = latestExecutionById.TryGetValue(knowledgeId, out var execution)
                        ? confirmation.EvidenceRefs.Concat([$"validation:{execution.ExecutionId}:{execution.OutcomeStatus}"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                        : confirmation.EvidenceRefs
                };
                confirmationChanged = true;
            }

            repairedItems.Add(new KnowledgeStateMissingItemReferenceRepairItem(
                KnowledgeItemId: knowledgeId,
                Title: title,
                Domain: domain,
                CurrentPlanStatus: plan.CurrentStatus,
                CurrentCatalogStatus: currentCatalogStatus,
                RepairStatus: "reconstructed",
                LastValidatedUtc: lastValidatedUtc,
                SourceCount: sourceCount,
                Warnings: repairWarnings));
        }

        var report = new KnowledgeStateMissingItemReferenceRepairReport(
            ReportVersion: "knowledge_state_missing_item_reference_repair_v1",
            UpdatedAtUtc: updatedAt,
            Status: apply && !dryRun ? "applied" : "dry_run_ready",
            LoadedValidationPlans: planReport.Plans.Count,
            TargetItems: targets.Count,
            RepairedItems: repairedItems.Count(item => item.RepairStatus.Equals("reconstructed", StringComparison.OrdinalIgnoreCase)),
            SkippedItems: repairedItems.Count(item => item.RepairStatus.StartsWith("skipped", StringComparison.OrdinalIgnoreCase)),
            Items: repairedItems,
            Warnings: BuildWarnings(repairedItems),
            ValidationPlansPath: ValidationPlansPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            EvidencePath: EvidencePath,
            SourceConfirmationsPath: SourceConfirmationsPath,
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
            if (catalogChanged)
            {
                File.WriteAllText(CatalogPath, JsonSerializer.Serialize(catalog, JsonDefaults.WriteOptions));
            }

            if (qualityChanged)
            {
                var updatedQuality = qualityReport is null
                    ? new KnowledgeQualityReport(
                        ReportVersion: "knowledge_quality_v2",
                        UpdatedAtUtc: updatedAt,
                        TotalKnowledgeItems: qualityById.Count,
                        TrustedKnowledge: qualityById.Values.Count(item => item.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase)),
                        WeakKnowledge: qualityById.Values.Count(item => item.LifecycleStatus.Equals("experimental", StringComparison.OrdinalIgnoreCase) || item.LifecycleStatus.Equals("promising", StringComparison.OrdinalIgnoreCase) || item.LifecycleStatus.Equals("needs_review", StringComparison.OrdinalIgnoreCase)),
                        DeprecatedKnowledge: qualityById.Values.Count(item => item.LifecycleStatus.Equals("deprecated", StringComparison.OrdinalIgnoreCase)),
                        AverageQualityScore: qualityById.Values.Count == 0 ? 0 : Math.Round(qualityById.Values.Average(item => item.QualityScore), 4),
                        AverageTrustScore: qualityById.Values.Count == 0 ? 0 : Math.Round(qualityById.Values.Average(item => item.TrustScore), 4),
                        KnowledgeHealth: "critical",
                        KnowledgeTrend: "stable",
                        Items: qualityById.Values.OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList(),
                        Warnings: [],
                        EvidencePath: Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json"),
                        NoTradingExecution: true,
                        NoBrokerAction: true,
                        NoAutoTrading: true,
                        HumanReviewRequired: true)
                    : qualityReport with { Items = qualityById.Values.OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList() };
                File.WriteAllText(QualityPath, JsonSerializer.Serialize(updatedQuality, JsonDefaults.WriteOptions));
            }

            if (evidenceChanged)
            {
                var updatedEvidence = evidenceReport is null
                    ? new KnowledgeEvidenceReport(
                        ReportVersion: "knowledge_evidence_v1",
                        UpdatedAtUtc: updatedAt,
                        Evidence: evidenceById.Values.OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList(),
                        NoTradingExecution: true,
                        NoBrokerAction: true,
                        NoAutoTrading: true,
                        HumanReviewRequired: true)
                    : evidenceReport with { Evidence = evidenceById.Values.OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList() };
                File.WriteAllText(EvidencePath, JsonSerializer.Serialize(updatedEvidence, JsonDefaults.WriteOptions));
            }

            if (confirmationChanged)
            {
                var updatedConfirmations = confirmationReport is null
                    ? new SourceConfirmationReport(
                        ReportVersion: "source_confirmation_v2",
                        UpdatedAtUtc: updatedAt,
                        ItemsAnalyzed: confirmationById.Count,
                        ConfirmationDistribution: confirmationById.Values
                            .GroupBy(item => item.ConfirmationLevel, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                        Results: confirmationById.Values.OrderByDescending(item => item.ConfirmationScore).ThenBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList(),
                        Warnings: [],
                        NoTradingExecution: true,
                        NoBrokerAction: true,
                        NoAutoTrading: true,
                        HumanReviewRequired: true)
                    : confirmationReport with
                    {
                        ItemsAnalyzed = confirmationById.Count,
                        ConfirmationDistribution = confirmationById.Values
                            .GroupBy(item => item.ConfirmationLevel, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                        Results = confirmationById.Values.OrderByDescending(item => item.ConfirmationScore).ThenBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.KnowledgeId, StringComparer.Ordinal).ToList()
                    };
                File.WriteAllText(SourceConfirmationsPath, JsonSerializer.Serialize(updatedConfirmations, JsonDefaults.WriteOptions));
            }

            _ = new KnowledgeStateConsistencyService(_storagePaths, Directory.GetCurrentDirectory()).Run(apply: false, dryRun: true);
        }

        WriteReport(report);
        return report;
    }

    public KnowledgeStateMissingItemReferenceRepairReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeStateMissingItemReferenceRepairReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static int DetermineSourceCount(KnowledgeValidationPlan plan, KnowledgeEvidenceEntry? evidence, ConfirmationResult? confirmation)
    {
        if (confirmation is not null)
        {
            return confirmation.SourceCount;
        }

        if (evidence is not null)
        {
            var sourceCount = evidence.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (sourceCount > 0)
            {
                return sourceCount;
            }
        }

        return plan.Requirements.Any(requirement => requirement.EvidenceRefs.Any(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase))) ? 1 : 0;
    }

    private static IReadOnlyList<string> ExtractTags(KnowledgeValidationPlan plan) =>
        plan.Requirements
            .Select(requirement => requirement.RequirementType)
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

    private static string NormalizeTitle(string title, string knowledgeId)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var suffix = knowledgeId.Split(':').LastOrDefault() ?? knowledgeId;
        return suffix.Replace('_', ' ');
    }

    private static string NormalizeDomain(string domain) =>
        string.IsNullOrWhiteSpace(domain) ? "software" : domain;

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<KnowledgeStateMissingItemReferenceRepairItem> items)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("no_missing_item_references_selected");
        }

        if (items.Any(item => item.RepairStatus.StartsWith("skipped", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("some_missing_item_reference_repairs_were_skipped");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void WriteReport(KnowledgeStateMissingItemReferenceRepairReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeStateMissingItemReferenceRepairReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge State Missing Item Reference Repair");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- loaded_validation_plans: {report.LoadedValidationPlans}");
        sb.AppendLine($"- target_items: {report.TargetItems}");
        sb.AppendLine($"- repaired_items: {report.RepairedItems}");
        sb.AppendLine($"- skipped_items: {report.SkippedItems}");
        sb.AppendLine();
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.KnowledgeItemId} / {item.Title}");
            sb.AppendLine($"- domain: {item.Domain}");
            sb.AppendLine($"- current_plan_status: {item.CurrentPlanStatus}");
            sb.AppendLine($"- current_catalog_status: {item.CurrentCatalogStatus}");
            sb.AppendLine($"- repair_status: {item.RepairStatus}");
            sb.AppendLine($"- last_validated_utc: {item.LastValidatedUtc?.ToString("O") ?? "-"}");
            sb.AppendLine($"- source_count: {item.SourceCount}");
            sb.AppendLine($"- warnings: {string.Join(", ", item.Warnings)}");
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
