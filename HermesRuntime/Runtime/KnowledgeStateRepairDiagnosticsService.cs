using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeStateRepairDiagnosticItem(
    string KnowledgeItemId,
    string Title,
    string MismatchType,
    string CurrentStatus,
    string ValidationStatus,
    double TrustScore,
    double QualityScore,
    int SourceCount,
    bool AutoRepairable,
    string RecommendedAction,
    string ExpectedEffect,
    string Severity,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    string? SourceConfirmationStatus,
    string? ValidationPlanStatus,
    string? MasterStatusHint);

public sealed record KnowledgeStateRepairDiagnosticsReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TotalIssues,
    int AutoRepairableCount,
    int HumanReviewRequiredCount,
    IReadOnlyDictionary<string, int> TopMismatchTypes,
    string NextBestRepairAction,
    IReadOnlyList<KnowledgeStateRepairDiagnosticItem> Items,
    IReadOnlyList<string> Warnings,
    string ConsistencyReportPath,
    string CatalogPath,
    string QualityPath,
    string EvidencePath,
    string SourceConfirmationsPath,
    string ValidationPlansPath,
    string MasterStatusPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeStateRepairDiagnosticsService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeStateRepairDiagnosticsService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_repair_diagnostics");
    public string ReportPath => Path.Combine(Root, "knowledge_state_repair_diagnostics_report.json");
    public string MarkdownPath => Path.Combine(Root, "knowledge_state_repair_diagnostics_report.md");

    public string ConsistencyReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_consistency", "knowledge_state_consistency_report.json");
    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");
    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");
    public string EvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");
    public string MasterStatusPath => Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json");

    public KnowledgeStateRepairDiagnosticsReport Run()
    {
        Directory.CreateDirectory(Root);

        var consistency = LoadJson<KnowledgeStateConsistencyReport>(ConsistencyReportPath) ?? new KnowledgeStateConsistencyService(_storagePaths).Run(apply: false, dryRun: true);
        var catalog = LoadJson<List<KnowledgeCatalogItem>>(CatalogPath) ?? [];
        var quality = LoadJson<KnowledgeQualityReport>(QualityPath);
        var evidence = LoadJson<KnowledgeEvidenceReport>(EvidencePath);
        var confirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath);
        var validationPlans = LoadJson<KnowledgeValidationPlanReport>(ValidationPlansPath);
        var master = LoadJson<MasterStatusSnapshot>(MasterStatusPath);

        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var qualityById = quality?.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmations?.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var planById = validationPlans?.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, KnowledgeValidationPlan>(StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidence?.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, KnowledgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);

        var sourceCountMap = confirmationById.ToDictionary(pair => pair.Key, pair => SourceConfirmationEngine.CanonicalSourceCount(catalogById.GetValueOrDefault(pair.Key), pair.Value), StringComparer.OrdinalIgnoreCase);

        var diagnostics = consistency.Items
            .Where(item => item.SourceCountMismatch || item.TrustedStatusMismatch || item.TimestampMismatch || item.BlockerMismatch || item.MissingItemIdMismatch)
            .Select(item => BuildDiagnosticItem(item, catalogById, qualityById, confirmationById, planById, evidenceById, sourceCountMap, master))
            .ToList();

        var topMismatchTypes = diagnostics
            .Select(diagnostic => diagnostic.MismatchType)
            .GroupBy(type => type, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var autoRepairableCount = diagnostics.Count(item => item.AutoRepairable);
        var humanReviewRequiredCount = diagnostics.Count(item => !item.AutoRepairable || item.RecommendedAction.Equals("human_review_required", StringComparison.OrdinalIgnoreCase));
        var nextBestRepairAction = diagnostics
            .OrderByDescending(item => item.AutoRepairable)
            .ThenByDescending(item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.MismatchType, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.RecommendedAction)
            .FirstOrDefault() ?? "no_safe_auto_repair";

        var report = new KnowledgeStateRepairDiagnosticsReport(
            ReportVersion: "knowledge_state_repair_diagnostics_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: diagnostics.Count == 0 ? "clean" : "diagnosed",
            TotalIssues: diagnostics.Count,
            AutoRepairableCount: autoRepairableCount,
            HumanReviewRequiredCount: humanReviewRequiredCount,
            TopMismatchTypes: topMismatchTypes,
            NextBestRepairAction: nextBestRepairAction,
            Items: diagnostics,
            Warnings: BuildWarnings(diagnostics),
            ConsistencyReportPath: ConsistencyReportPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            EvidencePath: EvidencePath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            ValidationPlansPath: ValidationPlansPath,
            MasterStatusPath: MasterStatusPath,
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

    public KnowledgeStateRepairDiagnosticsReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeStateRepairDiagnosticsReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static KnowledgeStateRepairDiagnosticItem BuildDiagnosticItem(
        KnowledgeStateConsistencyItem item,
        IReadOnlyDictionary<string, KnowledgeCatalogItem> catalogById,
        IReadOnlyDictionary<string, KnowledgeQualityItem> qualityById,
        IReadOnlyDictionary<string, ConfirmationResult> confirmationById,
        IReadOnlyDictionary<string, KnowledgeValidationPlan> planById,
        IReadOnlyDictionary<string, KnowledgeEvidenceEntry> evidenceById,
        IReadOnlyDictionary<string, int> sourceCountMap,
        MasterStatusSnapshot? master)
    {
        var catalogItem = catalogById.GetValueOrDefault(item.KnowledgeId);
        var qualityItem = qualityById.GetValueOrDefault(item.KnowledgeId);
        var confirmation = confirmationById.GetValueOrDefault(item.KnowledgeId);
        var plan = planById.GetValueOrDefault(item.KnowledgeId);
        var evidenceItem = evidenceById.GetValueOrDefault(item.KnowledgeId);

        var mismatchType = DetermineMismatchType(item, qualityItem, confirmation, plan, evidenceItem);
        var sourceCount = sourceCountMap.GetValueOrDefault(item.KnowledgeId, item.SourceCountExpected);
        var blockers = BuildBlockers(item, qualityItem, confirmation, plan, evidenceItem, master);
        var autoRepairable = DetermineAutoRepairable(mismatchType, item, blockers, qualityItem, confirmation, plan, evidenceItem);
        var recommendedAction = DetermineRecommendedAction(mismatchType, item, blockers, autoRepairable, qualityItem, confirmation, plan, evidenceItem);
        var expectedEffect = DetermineExpectedEffect(mismatchType, item, blockers, autoRepairable);
        var severity = DetermineSeverity(item, qualityItem, confirmation, plan, evidenceItem, mismatchType);

        return new KnowledgeStateRepairDiagnosticItem(
            KnowledgeItemId: item.KnowledgeId,
            Title: item.Title,
            MismatchType: mismatchType,
            CurrentStatus: item.CatalogValidationStatus,
            ValidationStatus: qualityItem?.LifecycleStatus ?? catalogItem?.ValidationStatus ?? "unknown",
            TrustScore: qualityItem?.TrustScore ?? 0,
            QualityScore: qualityItem?.QualityScore ?? 0,
            SourceCount: sourceCount,
            AutoRepairable: autoRepairable,
            RecommendedAction: recommendedAction,
            ExpectedEffect: expectedEffect,
            Severity: severity,
            Blockers: blockers,
            Warnings: BuildWarnings(item, qualityItem, confirmation, plan, evidenceItem),
            SourceConfirmationStatus: confirmation?.ReviewStatus,
            ValidationPlanStatus: plan?.Status,
            MasterStatusHint: master?.KnowledgeHealth);
    }

    private static string DetermineMismatchType(
        KnowledgeStateConsistencyItem item,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem)
    {
        if (item.MissingItemIdMismatch)
        {
            return "missing_item_id_mismatch";
        }

        if (item.TimestampMismatch)
        {
            return "timestamp_mismatch";
        }

        if (item.BlockerMismatch)
        {
            if (item.CurrentBlockers.Any(blocker => blocker.Contains("validation_plan_missing", StringComparison.OrdinalIgnoreCase))
                || plan is null)
            {
                return "validation_plan_missing";
            }

            if (item.CurrentBlockers.Any(blocker => blocker.Contains("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)))
            {
                return "stale_blocker";
            }

            if (item.CurrentBlockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
            {
                return "true_contradiction";
            }

            return "blocker_mismatch";
        }

        if (qualityItem is null || confirmation is null || evidenceItem is null)
        {
            return "missing_item_id_mismatch";
        }

        if (item.CurrentBlockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            return "true_contradiction";
        }

        return "unknown";
    }

    private static IReadOnlyList<string> BuildBlockers(
        KnowledgeStateConsistencyItem item,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem,
        MasterStatusSnapshot? master)
    {
        var blockers = new List<string>(item.ExpectedBlockers);
        if (qualityItem is null)
        {
            blockers.Add("missing_quality_item");
        }

        if (confirmation is null)
        {
            blockers.Add("missing_source_confirmation");
        }

        if (plan is null)
        {
            blockers.Add("validation_plan_missing");
        }

        if (evidenceItem is null)
        {
            blockers.Add("missing_evidence_item");
        }

        if (master is not null && master.ContradictionCount > 0 && item.CurrentBlockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add("blocking_contradiction");
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool DetermineAutoRepairable(
        string mismatchType,
        KnowledgeStateConsistencyItem item,
        IReadOnlyList<string> blockers,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem)
    {
        return mismatchType switch
        {
            "timestamp_mismatch" => true,
            "blocker_mismatch" => true,
            "missing_item_id_mismatch" => true,
            "validation_plan_missing" => plan is not null,
            "stale_blocker" => qualityItem is not null || confirmation is not null,
            "true_contradiction" => false,
            _ => blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)) && qualityItem?.LastValidatedUtc is not null
        };
    }

    private static string DetermineRecommendedAction(
        string mismatchType,
        KnowledgeStateConsistencyItem item,
        IReadOnlyList<string> blockers,
        bool autoRepairable,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem)
    {
        if (!autoRepairable)
        {
            if (mismatchType.Equals("true_contradiction", StringComparison.OrdinalIgnoreCase))
            {
                return "human_review_required";
            }

            return "no_safe_auto_repair";
        }

        return mismatchType switch
        {
            "timestamp_mismatch" => "refresh_timestamp",
            "missing_item_id_mismatch" => "rebuild_missing_item_reference",
            "validation_plan_missing" => "create_validation_plan",
            "stale_blocker" => "remove_stale_blocker",
            "blocker_mismatch" => "run_validation_state_sync",
            _ => blockers.Any(blocker => blocker.Equals("fresh_validation_timestamp_missing", StringComparison.OrdinalIgnoreCase)) ? "refresh_timestamp" : "run_validation_state_sync"
        };
    }

    private static string DetermineExpectedEffect(string mismatchType, KnowledgeStateConsistencyItem item, IReadOnlyList<string> blockers, bool autoRepairable)
    {
        if (!autoRepairable)
        {
            return "no_direct_metric_change";
        }

        return mismatchType switch
        {
            "timestamp_mismatch" => "unblock_validation",
            "validation_plan_missing" => "unblock_validation",
            "stale_blocker" => "reduce_contradiction_count",
            "missing_item_id_mismatch" => "improve_trust_score",
            "blocker_mismatch" => "reduce_contradiction_count",
            _ => blockers.Any(blocker => blocker.Contains("validation", StringComparison.OrdinalIgnoreCase)) ? "unblock_validation" : "no_direct_metric_change"
        };
    }

    private static string DetermineSeverity(
        KnowledgeStateConsistencyItem item,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem,
        string mismatchType)
    {
        if (mismatchType.Equals("true_contradiction", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        if (item.SourceCountExpected >= 2 && item.CurrentBlockers.Any(blocker => blocker.Contains("source", StringComparison.OrdinalIgnoreCase)))
        {
            return "medium";
        }

        if (qualityItem?.ValidationScore < 0.6 || qualityItem?.TrustScore < 0.64)
        {
            return "medium";
        }

        return mismatchType is "timestamp_mismatch" or "validation_plan_missing" ? "medium" : "low";
    }

    private static IReadOnlyList<string> BuildWarnings(
        KnowledgeStateConsistencyItem item,
        KnowledgeQualityItem? qualityItem,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceItem)
    {
        var warnings = new List<string>();
        if (qualityItem is null)
        {
            warnings.Add("missing_quality_item");
        }

        if (confirmation is null)
        {
            warnings.Add("missing_source_confirmation");
        }

        if (plan is null)
        {
            warnings.Add("missing_validation_plan");
        }

        if (evidenceItem is null)
        {
            warnings.Add("missing_evidence_item");
        }

        if (item.SourceCountMismatch)
        {
            warnings.Add("source_count_mismatch");
        }

        if (item.TimestampMismatch)
        {
            warnings.Add("timestamp_mismatch");
        }

        if (item.BlockerMismatch)
        {
            warnings.Add("blocker_mismatch");
        }

        if (item.MissingItemIdMismatch)
        {
            warnings.Add("missing_item_id_mismatch");
        }

        if (item.CurrentBlockers.Any(blocker => blocker.Contains("contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("contradiction_present");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<KnowledgeStateRepairDiagnosticItem> items)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("no_state_repair_issues_detected");
        }

        if (items.Any(item => item.MismatchType.Equals("true_contradiction", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("human_review_required_for_true_contradictions");
        }

        return warnings;
    }

    private static void WriteReport(KnowledgeStateRepairDiagnosticsReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(report.ReportPath, json);
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeStateRepairDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge State Repair Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- total_issues: {report.TotalIssues}");
        sb.AppendLine($"- auto_repairable_count: {report.AutoRepairableCount}");
        sb.AppendLine($"- human_review_required_count: {report.HumanReviewRequiredCount}");
        sb.AppendLine($"- next_best_repair_action: {report.NextBestRepairAction}");
        sb.AppendLine();
        sb.AppendLine("## Top Mismatch Types");
        foreach (var item in report.TopMismatchTypes)
        {
            sb.AppendLine($"- {item.Key}: {item.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Issues");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.KnowledgeItemId} / {item.Title}");
            sb.AppendLine($"- mismatch_type: {item.MismatchType}");
            sb.AppendLine($"- current_status: {item.CurrentStatus}");
            sb.AppendLine($"- validation_status: {item.ValidationStatus}");
            sb.AppendLine($"- trust_score: {item.TrustScore:0.###}");
            sb.AppendLine($"- quality_score: {item.QualityScore:0.###}");
            sb.AppendLine($"- source_count: {item.SourceCount}");
            sb.AppendLine($"- auto_repairable: {item.AutoRepairable}");
            sb.AppendLine($"- recommended_action: {item.RecommendedAction}");
            sb.AppendLine($"- expected_effect: {item.ExpectedEffect}");
            sb.AppendLine($"- severity: {item.Severity}");
            sb.AppendLine($"- blockers: {string.Join(", ", item.Blockers)}");
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
