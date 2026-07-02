using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeEvidenceAcquisitionSnapshot(
    int TrustedKnowledge,
    int ContradictionCount,
    int ValidationPlansOpen,
    int KnowledgeItemsNeedingSourceCheck,
    double AverageTrustScore,
    double AverageQualityScore);

public sealed record KnowledgeEvidenceAcquisitionPlan(
    string KnowledgeItemId,
    string Title,
    string Domain,
    string CurrentStatus,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    int SourceCount,
    IReadOnlyList<string> Blockers,
    string SelectedStrategy,
    IReadOnlyList<string> RecommendedExistingCommands,
    string ExpectedEffect,
    int Priority);

public sealed record KnowledgeEvidenceAcquisitionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedIssues,
    int SelectedItems,
    int SkippedTrueContradictions,
    int SkippedHumanReviewRequired,
    IReadOnlyList<string> SelectedDomains,
    IReadOnlyDictionary<string, int> TopBlockers,
    IReadOnlyList<KnowledgeEvidenceAcquisitionPlan> AcquisitionPlans,
    IReadOnlyList<string> CommandsExecuted,
    KnowledgeEvidenceAcquisitionSnapshot Before,
    KnowledgeEvidenceAcquisitionSnapshot After,
    IReadOnlyList<string> Warnings,
    string DiagnosticsPath,
    string CatalogPath,
    string QualityPath,
    string SourceConfirmationsPath,
    string ValidationPlansPath,
    string TrustedSourceCatalogPath,
    string KnownArticleSeedCatalogPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool Executed,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeEvidenceAcquisitionService
{
    private static readonly IReadOnlySet<string> EvidenceBlockers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "second_independent_source_missing",
        "trust_score_too_low",
        "quality_score_too_low",
        "validation_score_too_low",
        "domain_validation_not_passed"
    };

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public KnowledgeEvidenceAcquisitionService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_acquisition");

    public string ReportPath => Path.Combine(Root, "knowledge_evidence_acquisition_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_evidence_acquisition_report.md");

    public string DiagnosticsPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_repair_diagnostics", "knowledge_state_repair_diagnostics_report.json");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string TrustedSourceCatalogPath => Path.Combine(_runtimeRoot, "config", "trusted_source_catalog.json");

    public string KnownArticleSeedCatalogPath => Path.Combine(_runtimeRoot, "config", "known_article_seed_catalog.json");

    public KnowledgeEvidenceAcquisitionReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(maxItems: 10, execute: false);
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceAcquisitionReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(maxItems: 10, execute: false);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(maxItems: 10, execute: false);
        }
    }

    public KnowledgeEvidenceAcquisitionReport Run(int maxItems, bool execute)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var diagnosticsService = new KnowledgeStateRepairDiagnosticsService(_storagePaths);
        var diagnostics = diagnosticsService.LoadLatestReport() ?? diagnosticsService.Run();

        var catalog = LoadJson<List<KnowledgeCatalogItem>>(CatalogPath) ?? new KnowledgeCatalog(_storagePaths).LoadOrCreateItems().ToList();
        var quality = LoadJson<KnowledgeQualityReport>(QualityPath) ?? new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var confirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath) ?? new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
        var validationPlans = LoadJson<KnowledgeValidationPlanReport>(ValidationPlansPath) ?? new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport() ?? new KnowledgeValidationStrategy(_storagePaths).GeneratePlans(50);
        var trustedCatalog = new TrustedSourceCatalogService(_storagePaths, _runtimeRoot).LoadCatalog();
        var knownSeedCatalog = new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot).LoadSeeds();

        var qualityById = quality.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmations.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var planById = validationPlans.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var selected = diagnostics.Items
            .Where(item => item.AutoRepairable)
            .Where(item => !item.Blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
            .Where(item => !item.Blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)))
            .Where(item => !item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Blockers.Any(blocker => EvidenceBlockers.Contains(blocker)))
            .Select(item => BuildPlan(item, qualityById.GetValueOrDefault(item.KnowledgeItemId), confirmationById.GetValueOrDefault(item.KnowledgeItemId), planById.GetValueOrDefault(item.KnowledgeItemId), catalogById.GetValueOrDefault(item.KnowledgeItemId), trustedCatalog, knownSeedCatalog))
            .OrderByDescending(plan => plan.Priority)
            .ThenByDescending(plan => plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(plan => plan.TrustScore)
            .ThenByDescending(plan => plan.QualityScore)
            .ThenByDescending(plan => plan.ValidationScore)
            .ThenBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxItems))
            .ToList();

        var skippedTrueContradictions = diagnostics.Items.Count(item => item.Blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)));
        var skippedHumanReviewRequired = diagnostics.Items.Count(item => item.Blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)));
        var topBlockers = selected
            .SelectMany(plan => plan.Blockers)
            .GroupBy(blocker => blocker, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var selectedDomains = selected
            .Select(plan => plan.Domain)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var before = BuildSnapshot(quality, LoadMasterSnapshot());
        var commandsExecuted = new List<string>();
        var warnings = new List<string>();
        var executed = false;

        if (selected.Count == 0)
        {
            warnings.Add("no_evidence_acquisition_candidates");
        }

        if (execute && selected.Count > 0)
        {
            executed = true;
            var knownArticleSeedCatalogService = new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot);
            var webResearchImportService = new WebResearchSourceImportService(_storagePaths);
            var evidenceMatcherService = new KnowledgeEvidenceSemanticMatcherService(_storagePaths);
            var resolverService = new IndependentSourceResolverService(_storagePaths);
            var autoReviewService = new AutoSourceReviewPolicyService(_storagePaths, _runtimeRoot);
            var validationSyncService = new KnowledgeValidationStateSyncService(_storagePaths);
            var promotionService = new KnowledgeTrustPromotionPipelineService(_storagePaths);
            var masterStatusWriter = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot));
            var qualityEngine = new KnowledgeQualityEngine(_storagePaths);

            _ = knownArticleSeedCatalogService.Run(Math.Max(1, maxItems), dryRun: false, maxFetchSeconds: 60);
            commandsExecuted.Add("known-article-seed-fetch --max-items N --apply --max-fetch-seconds 60");

            _ = webResearchImportService.Run(apply: true);
            commandsExecuted.Add("web-research-import --apply");

            _ = evidenceMatcherService.Run(apply: true);
            commandsExecuted.Add("knowledge-evidence-match --apply");

            _ = resolverService.Run(apply: true);
            commandsExecuted.Add("independent-source-resolver --apply");

            _ = autoReviewService.Run(apply: true);
            commandsExecuted.Add("auto-source-review --apply");

            _ = validationSyncService.Run(apply: true, dryRun: false);
            commandsExecuted.Add("knowledge-validation-state-sync --apply");

            _ = promotionService.Run(apply: true, maxSeconds: 60, skipRefresh: true);
            commandsExecuted.Add("knowledge-trust-promote --apply --skip-refresh");

            var refreshedQuality = qualityEngine.LoadReport() ?? qualityEngine.Run();
            _ = masterStatusWriter.WriteKnowledgeOnlySnapshot(refreshedQuality);
            commandsExecuted.Add("master-status-refresh --knowledge-only --max-seconds 60");
        }

        var afterQuality = new KnowledgeQualityEngine(_storagePaths).LoadReport() ?? new KnowledgeQualityEngine(_storagePaths).Run();
        var after = BuildSnapshot(afterQuality, LoadMasterSnapshot());

        var report = new KnowledgeEvidenceAcquisitionReport(
            ReportVersion: "knowledge_evidence_acquisition_v1",
            UpdatedAtUtc: now,
            Status: execute ? (selected.Count == 0 ? "no_candidates" : "executed") : "dry_run_ready",
            LoadedIssues: diagnostics.TotalIssues,
            SelectedItems: selected.Count,
            SkippedTrueContradictions: skippedTrueContradictions,
            SkippedHumanReviewRequired: skippedHumanReviewRequired,
            SelectedDomains: selectedDomains,
            TopBlockers: topBlockers,
            AcquisitionPlans: selected,
            CommandsExecuted: commandsExecuted,
            Before: before,
            After: after,
            Warnings: warnings,
            DiagnosticsPath: DiagnosticsPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            ValidationPlansPath: ValidationPlansPath,
            TrustedSourceCatalogPath: TrustedSourceCatalogPath,
            KnownArticleSeedCatalogPath: KnownArticleSeedCatalogPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: !execute,
            Executed: executed,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static KnowledgeEvidenceAcquisitionPlan BuildPlan(
        KnowledgeStateRepairDiagnosticItem diagnostic,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? validationPlan,
        KnowledgeCatalogItem? catalogItem,
        IReadOnlyList<TrustedSourceCatalogEntry> trustedCatalog,
        IReadOnlyList<KnownArticleSeedDefinition> seedCatalog)
    {
        var blockers = diagnostic.Blockers
            .Where(blocker => EvidenceBlockers.Contains(blocker) || blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var strategy = blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
            ? "collect_second_independent_source"
            : blockers.Any(blocker => blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase))
                ? "complete_validation_evidence"
                : "improve_evidence_scores";

        var commands = new List<string>();
        if (strategy == "collect_second_independent_source")
        {
            commands.Add("known-article-seed-fetch --max-items N --apply --max-fetch-seconds 60");
            commands.Add("web-research-import --apply");
        }
        else
        {
            commands.Add("knowledge-evidence-match --apply");
            commands.Add("independent-source-resolver --apply");
            commands.Add("auto-source-review --apply");
        }

        if (blockers.Any(blocker => blocker.Equals("domain_validation_not_passed", StringComparison.OrdinalIgnoreCase) || blocker.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            commands.Add("knowledge-validation-state-sync --apply");
        }

        commands.Add("knowledge-trust-promote --apply --skip-refresh");

        var recommendedSeedCount = seedCatalog.Count(seed =>
            seed.Allowed
            && seed.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase));
        var recommendedCatalogDomains = trustedCatalog
            .Where(entry => entry.Allowed)
            .Select(entry => entry.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var expectedEffect = blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase))
            ? "increase_source_count_and_enable_policy_review"
            : blockers.Any(blocker => blocker.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase) || blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase))
                ? "increase_validation_and_quality_scores"
                : "reduce_evidence_blockers";

        return new KnowledgeEvidenceAcquisitionPlan(
            KnowledgeItemId: diagnostic.KnowledgeItemId,
            Title: diagnostic.Title,
            Domain: catalogItem?.Domain ?? quality?.Domain ?? "unknown",
            CurrentStatus: diagnostic.CurrentStatus,
            TrustScore: diagnostic.TrustScore,
            QualityScore: diagnostic.QualityScore,
            ValidationScore: quality?.ValidationScore ?? 0,
            SourceCount: diagnostic.SourceCount,
            Blockers: blockers,
            SelectedStrategy: strategy,
            RecommendedExistingCommands: commands,
            ExpectedEffect: $"{expectedEffect}; seeds={recommendedSeedCount}; catalog_domains={string.Join(',', recommendedCatalogDomains)}",
            Priority: BuildPriority(diagnostic, quality, confirmation, validationPlan));
    }

    private static int BuildPriority(
        KnowledgeStateRepairDiagnosticItem diagnostic,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? validationPlan)
    {
        var priority = 0;
        if ((quality?.Domain ?? string.Empty).Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            priority += 100;
        }

        if (diagnostic.Blockers.Any(blocker => blocker.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)))
        {
            priority += 40;
        }

        if (diagnostic.Blockers.Any(blocker => blocker.Equals("validation_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            priority += 20;
        }

        if (diagnostic.Blockers.Any(blocker => blocker.Equals("quality_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            priority += 18;
        }

        if (diagnostic.Blockers.Any(blocker => blocker.Equals("trust_score_too_low", StringComparison.OrdinalIgnoreCase)))
        {
            priority += 16;
        }

        if (validationPlan is not null)
        {
            priority += 10;
        }

        priority += (int)Math.Round((quality?.TrustScore ?? diagnostic.TrustScore) * 10);
        priority += (int)Math.Round((quality?.QualityScore ?? diagnostic.QualityScore) * 10);
        priority += confirmation?.SourceCount is > 0 ? 5 : 0;
        return priority;
    }

    private KnowledgeEvidenceAcquisitionSnapshot BuildSnapshot(KnowledgeQualityReport quality, MasterStatusSnapshot? master)
    {
        var snapshot = master ?? new MasterStatusService(_storagePaths, _runtimeRoot).BuildSnapshot();
        return new KnowledgeEvidenceAcquisitionSnapshot(
            TrustedKnowledge: snapshot.TrustedKnowledge,
            ContradictionCount: snapshot.ContradictionCount,
            ValidationPlansOpen: snapshot.ValidationPlansOpen,
            KnowledgeItemsNeedingSourceCheck: snapshot.KnowledgeItemsNeedingSourceCheck,
            AverageTrustScore: quality.AverageTrustScore,
            AverageQualityScore: quality.AverageQualityScore);
    }

    private MasterStatusSnapshot? LoadMasterSnapshot()
    {
        var writer = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot));
        return writer.LoadSnapshot();
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

    private static string BuildMarkdown(KnowledgeEvidenceAcquisitionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hermes Knowledge Evidence Acquisition");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- loaded_issues: {report.LoadedIssues}");
        sb.AppendLine($"- selected_items: {report.SelectedItems}");
        sb.AppendLine($"- skipped_true_contradictions: {report.SkippedTrueContradictions}");
        sb.AppendLine($"- skipped_human_review_required: {report.SkippedHumanReviewRequired}");
        sb.AppendLine($"- dry_run: {report.DryRun}");
        sb.AppendLine($"- executed: {report.Executed}");
        sb.AppendLine();
        sb.AppendLine("## Before");
        WriteSnapshot(sb, report.Before);
        sb.AppendLine();
        sb.AppendLine("## After");
        WriteSnapshot(sb, report.After);
        sb.AppendLine();
        sb.AppendLine("## Plans");
        foreach (var plan in report.AcquisitionPlans.Take(25))
        {
            sb.AppendLine($"- {plan.KnowledgeItemId} | {plan.Domain} | {plan.SelectedStrategy} | source_count={plan.SourceCount} | trust={plan.TrustScore:0.###} | quality={plan.QualityScore:0.###}");
        }

        return sb.ToString();
    }

    private static void WriteSnapshot(StringBuilder sb, KnowledgeEvidenceAcquisitionSnapshot snapshot)
    {
        sb.AppendLine($"- trusted_knowledge: {snapshot.TrustedKnowledge}");
        sb.AppendLine($"- contradiction_count: {snapshot.ContradictionCount}");
        sb.AppendLine($"- validation_plans_open: {snapshot.ValidationPlansOpen}");
        sb.AppendLine($"- knowledge_items_needing_source_check: {snapshot.KnowledgeItemsNeedingSourceCheck}");
        sb.AppendLine($"- average_trust_score: {snapshot.AverageTrustScore:0.###}");
        sb.AppendLine($"- average_quality_score: {snapshot.AverageQualityScore:0.###}");
    }
}
