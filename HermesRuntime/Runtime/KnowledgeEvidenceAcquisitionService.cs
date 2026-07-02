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
    int Priority,
    IReadOnlyList<string> PublisherGroups,
    KnowledgeEvidenceAcquisitionTrace? Trace);

public sealed record KnowledgeEvidenceAcquisitionTrace(
    string KnowledgeItemId,
    string Title,
    string Domain,
    int SourceCountBefore,
    IReadOnlyList<string> PublisherGroupsBefore,
    IReadOnlyList<string> SeedDefinitions,
    IReadOnlyList<string> SeedRequests,
    IReadOnlyList<string> ImportedCandidates,
    IReadOnlyList<string> SemanticMatches,
    IReadOnlyList<string> ResolverMatches,
    IReadOnlyList<string> PolicyApprovedCandidates,
    string SeedFetchStatus,
    string ImportStatus,
    string SemanticMatchStatus,
    string ResolverStatus,
    string PolicyStatus,
    string FirstFailedStage,
    string FailureReason,
    string NoSourceGainReason,
    int SourceCountAfter,
    IReadOnlyList<string> PublisherGroupsAfter);

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

        var selectedDiagnostics = diagnostics.Items
            .Where(item => item.AutoRepairable)
            .Where(item => !item.Blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)))
            .Where(item => !item.Blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)))
            .Where(item => !item.ValidationStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Blockers.Any(blocker => EvidenceBlockers.Contains(blocker)))
            .ToList();

        var skippedTrueContradictions = diagnostics.Items.Count(item => item.Blockers.Any(blocker => blocker.Equals("blocking_contradiction", StringComparison.OrdinalIgnoreCase)));
        var skippedHumanReviewRequired = diagnostics.Items.Count(item => item.Blockers.Any(blocker => blocker.Equals("human_review_pending", StringComparison.OrdinalIgnoreCase)));

        KnownArticleSeedStatusReport? seedReport = null;
        WebResearchImportReport? importReport = null;
        KnowledgeEvidenceSemanticMatcherReport? matcherReport = null;
        IndependentSourceResolverReport? resolverReport = null;
        AutoSourceReviewReport? autoReviewReport = null;

        seedReport = LoadJson<KnownArticleSeedStatusReport>(Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog", "known_article_seed_report.json"));
        importReport = LoadJson<WebResearchImportReport>(Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_report.json"));
        matcherReport = LoadJson<KnowledgeEvidenceSemanticMatcherReport>(Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json"));
        resolverReport = LoadJson<IndependentSourceResolverReport>(Path.Combine(_storagePaths.Root, "reports", "independent_source_resolver", "independent_source_resolver_report.json"));
        autoReviewReport = LoadJson<AutoSourceReviewReport>(Path.Combine(_storagePaths.Root, "reports", "auto_source_review", "auto_source_review_report.json"));

        var before = BuildSnapshot(quality, LoadMasterSnapshot());
        var commandsExecuted = new List<string>();
        var warnings = new List<string>();
        var executed = false;

        if (selectedDiagnostics.Count == 0)
        {
            warnings.Add("no_evidence_acquisition_candidates");
        }

        if (execute && selectedDiagnostics.Count > 0)
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

            seedReport = LoadJson<KnownArticleSeedStatusReport>(Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog", "known_article_seed_report.json"));
            importReport = LoadJson<WebResearchImportReport>(Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_report.json"));
            matcherReport = LoadJson<KnowledgeEvidenceSemanticMatcherReport>(Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json"));
            resolverReport = LoadJson<IndependentSourceResolverReport>(Path.Combine(_storagePaths.Root, "reports", "independent_source_resolver", "independent_source_resolver_report.json"));
            autoReviewReport = LoadJson<AutoSourceReviewReport>(Path.Combine(_storagePaths.Root, "reports", "auto_source_review", "auto_source_review_report.json"));
            confirmationById.Clear();
            var refreshedConfirmations = LoadJson<SourceConfirmationReport>(SourceConfirmationsPath) ?? new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
            foreach (var item in refreshedConfirmations.Results)
            {
                confirmationById[item.KnowledgeId] = item;
            }
        }

        var afterQuality = new KnowledgeQualityEngine(_storagePaths).LoadReport() ?? new KnowledgeQualityEngine(_storagePaths).Run();
        var after = BuildSnapshot(afterQuality, LoadMasterSnapshot());

        seedReport ??= LoadJson<KnownArticleSeedStatusReport>(Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog", "known_article_seed_report.json"));
        importReport ??= LoadJson<WebResearchImportReport>(Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_report.json"));
        matcherReport ??= LoadJson<KnowledgeEvidenceSemanticMatcherReport>(Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json"));
        resolverReport ??= LoadJson<IndependentSourceResolverReport>(Path.Combine(_storagePaths.Root, "reports", "independent_source_resolver", "independent_source_resolver_report.json"));
        autoReviewReport ??= LoadJson<AutoSourceReviewReport>(Path.Combine(_storagePaths.Root, "reports", "auto_source_review", "auto_source_review_report.json"));

        var selected = selectedDiagnostics
            .Select(item => BuildPlan(
                item,
                qualityById.GetValueOrDefault(item.KnowledgeItemId),
                confirmationById.GetValueOrDefault(item.KnowledgeItemId),
                planById.GetValueOrDefault(item.KnowledgeItemId),
                catalogById.GetValueOrDefault(item.KnowledgeItemId),
                trustedCatalog,
                knownSeedCatalog,
                seedReport,
                importReport,
                matcherReport,
                resolverReport,
                autoReviewReport))
            .OrderByDescending(plan => plan.Priority)
            .ThenByDescending(plan => plan.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(plan => plan.TrustScore)
            .ThenByDescending(plan => plan.QualityScore)
            .ThenByDescending(plan => plan.ValidationScore)
            .ThenBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxItems))
            .ToList();

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

    private KnowledgeEvidenceAcquisitionPlan BuildPlan(
        KnowledgeStateRepairDiagnosticItem diagnostic,
        KnowledgeQualityItem? quality,
        ConfirmationResult? confirmation,
        KnowledgeValidationPlan? validationPlan,
        KnowledgeCatalogItem? catalogItem,
        IReadOnlyList<TrustedSourceCatalogEntry> trustedCatalog,
        IReadOnlyList<KnownArticleSeedDefinition> seedCatalog,
        KnownArticleSeedStatusReport? seedReport,
        WebResearchImportReport? importReport,
        KnowledgeEvidenceSemanticMatcherReport? matcherReport,
        IndependentSourceResolverReport? resolverReport,
        AutoSourceReviewReport? autoReviewReport)
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
        var publisherGroups = BuildPublisherGroups(catalogItem, confirmation, trustedCatalog);
        var trace = BuildTrace(
            diagnostic,
            catalogItem,
            confirmation,
            seedCatalog,
            seedReport,
            importReport,
            matcherReport,
            resolverReport,
            autoReviewReport,
            publisherGroups,
            catalogItem?.Domain ?? quality?.Domain ?? diagnostic.CurrentStatus ?? "unknown");
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
            Priority: BuildPriority(diagnostic, quality, confirmation, validationPlan),
            PublisherGroups: publisherGroups,
            Trace: trace);
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

    private IReadOnlyList<string> BuildPublisherGroups(KnowledgeCatalogItem? catalogItem, ConfirmationResult? confirmation, IReadOnlyList<TrustedSourceCatalogEntry> trustedCatalog)
    {
        var resolver = new PublisherGroupResolverService(_storagePaths, _runtimeRoot);
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (catalogItem is not null)
        {
            var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources()
                .ToDictionary(source => source.SourceId, source => source, StringComparer.OrdinalIgnoreCase);

            foreach (var sourceId in catalogItem.SourceIds)
            {
                if (sources.TryGetValue(sourceId, out var source))
                {
                    var group = resolver.Resolve(source.Domain);
                    if (!string.IsNullOrWhiteSpace(group))
                    {
                        groups.Add(group);
                    }
                }
            }
        }

        foreach (var candidate in confirmation?.CandidateSources ?? [])
        {
            var group = resolver.Resolve(candidate.Domain);
            if (!string.IsNullOrWhiteSpace(group))
            {
                groups.Add(group);
            }
        }

        foreach (var source in trustedCatalog.Where(entry => entry.Allowed))
        {
            if (!string.IsNullOrWhiteSpace(source.Domain) && string.Equals(catalogItem?.Domain, source.Domain, StringComparison.OrdinalIgnoreCase))
            {
                var group = resolver.Resolve(source.Domain);
                if (!string.IsNullOrWhiteSpace(group))
                {
                    groups.Add(group);
                }
            }
        }

        return groups.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private KnowledgeEvidenceAcquisitionTrace BuildTrace(
        KnowledgeStateRepairDiagnosticItem diagnostic,
        KnowledgeCatalogItem? catalogItem,
        ConfirmationResult? confirmation,
        IReadOnlyList<KnownArticleSeedDefinition> seedCatalog,
        KnownArticleSeedStatusReport? seedReport,
        WebResearchImportReport? importReport,
        KnowledgeEvidenceSemanticMatcherReport? matcherReport,
        IndependentSourceResolverReport? resolverReport,
        AutoSourceReviewReport? autoReviewReport,
        IReadOnlyList<string> publisherGroups,
        string domain)
    {
        var selectedSeeds = seedCatalog
            .Where(seed => seed.Allowed && seed.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var seedDefinitions = selectedSeeds.Select(seed => seed.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var seedRequests = seedReport?.Requests
            .Where(request => request.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Select(request => $"{request.Url}|{request.Status}")
            .ToList() ?? [];
        var importedCandidates = importReport?.Accepted
            .Where(candidate => candidate.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Url)
            .ToList() ?? [];
        var semanticMatches = matcherReport?.Accepted
            .Where(candidate => candidate.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Url)
            .ToList() ?? [];
        var resolverMatches = resolverReport?.Accepted
            .Where(candidate => candidate.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Url)
            .ToList() ?? [];
        var policyApprovedCandidates = autoReviewReport?.AutoApproved
            .Where(candidate => candidate.KnowledgeItemId.Equals(diagnostic.KnowledgeItemId, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Url)
            .ToList() ?? [];

        var seedFetchStatus = DetermineSeedFetchStatus(diagnostic.KnowledgeItemId, seedDefinitions, seedReport, seedRequests);
        var importStatus = DetermineImportStatus(diagnostic.KnowledgeItemId, importedCandidates, importReport);
        var semanticStatus = DetermineSemanticStatus(diagnostic.KnowledgeItemId, semanticMatches, matcherReport);
        var resolverStatus = DetermineResolverStatus(diagnostic.KnowledgeItemId, resolverMatches, resolverReport);
        var policyStatus = DeterminePolicyStatus(diagnostic.KnowledgeItemId, policyApprovedCandidates, autoReviewReport);
        var firstFailedStage = DetermineFirstFailedStage(seedFetchStatus, importStatus, semanticStatus, resolverStatus, policyStatus);
        var failureReason = BuildFailureReason(firstFailedStage, seedFetchStatus, importStatus, semanticStatus, resolverStatus, policyStatus, importedCandidates, semanticMatches, resolverMatches, policyApprovedCandidates, confirmation, diagnostic);
        var noSourceGainReason = BuildNoSourceGainReason(firstFailedStage, failureReason, diagnostic.SourceCount, confirmation?.SourceCount ?? diagnostic.SourceCount, policyApprovedCandidates.Count);

        return new KnowledgeEvidenceAcquisitionTrace(
            KnowledgeItemId: diagnostic.KnowledgeItemId,
            Title: diagnostic.Title,
            Domain: domain,
            SourceCountBefore: diagnostic.SourceCount,
            PublisherGroupsBefore: publisherGroups,
            SeedDefinitions: seedDefinitions,
            SeedRequests: seedRequests,
            ImportedCandidates: importedCandidates,
            SemanticMatches: semanticMatches,
            ResolverMatches: resolverMatches,
            PolicyApprovedCandidates: policyApprovedCandidates,
            SeedFetchStatus: seedFetchStatus,
            ImportStatus: importStatus,
            SemanticMatchStatus: semanticStatus,
            ResolverStatus: resolverStatus,
            PolicyStatus: policyStatus,
            FirstFailedStage: firstFailedStage,
            FailureReason: failureReason,
            NoSourceGainReason: noSourceGainReason,
            SourceCountAfter: confirmation?.SourceCount ?? diagnostic.SourceCount,
            PublisherGroupsAfter: publisherGroups);
    }

    private static string DetermineSeedFetchStatus(string knowledgeItemId, IReadOnlyList<string> seedDefinitions, KnownArticleSeedStatusReport? seedReport, IReadOnlyList<string> seedRequests)
    {
        if (seedDefinitions.Count == 0)
        {
            return "no_seed_available";
        }

        if (seedReport is null || seedRequests.Count == 0)
        {
            return "no_seed_available";
        }

        if (seedRequests.Any(entry => entry.Contains("blocked_seed_fetch_timeout", StringComparison.OrdinalIgnoreCase)))
        {
            return "seed_fetch_failed";
        }

        if (seedRequests.Any(entry => entry.Contains("no_html_content", StringComparison.OrdinalIgnoreCase) || entry.Contains("fetch_failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "seed_fetch_failed";
        }

        return seedRequests.Count > 0 ? "ok" : "no_seed_available";
    }

    private static string DetermineImportStatus(string knowledgeItemId, IReadOnlyList<string> importedCandidates, WebResearchImportReport? importReport)
    {
        if (importedCandidates.Count > 0)
        {
            return "ok";
        }

        if (importReport is null)
        {
            return "no_candidate_imported";
        }

        var rejected = importReport.Rejected.Where(candidate => candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (rejected.Any(candidate => !string.IsNullOrWhiteSpace(candidate.RejectionReason) && candidate.RejectionReason.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
        {
            return "duplicate_source";
        }

        return importReport.ImportCandidates > 0 ? "no_candidate_imported" : "no_candidate_imported";
    }

    private static string DetermineSemanticStatus(string knowledgeItemId, IReadOnlyList<string> semanticMatches, KnowledgeEvidenceSemanticMatcherReport? matcherReport)
    {
        if (semanticMatches.Count > 0)
        {
            return "ok";
        }

        if (matcherReport is null)
        {
            return "semantic_match_failed";
        }

        var relevant = matcherReport.Candidates.Where(candidate => candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)).ToList();
        return relevant.Count == 0 ? "semantic_match_failed" : "semantic_match_failed";
    }

    private static string DetermineResolverStatus(string knowledgeItemId, IReadOnlyList<string> resolverMatches, IndependentSourceResolverReport? resolverReport)
    {
        if (resolverMatches.Count > 0)
        {
            return "ok";
        }

        if (resolverReport is null)
        {
            return "resolver_rejected";
        }

        var relevant = resolverReport.Candidates.Where(candidate => candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (relevant.Any(candidate => candidate.RejectionReason?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "duplicate_source";
        }

        return relevant.Count > 0 ? "resolver_rejected" : "resolver_rejected";
    }

    private static string DeterminePolicyStatus(string knowledgeItemId, IReadOnlyList<string> policyApprovedCandidates, AutoSourceReviewReport? autoReviewReport)
    {
        if (policyApprovedCandidates.Count > 0)
        {
            return "ok";
        }

        if (autoReviewReport is null)
        {
            return "policy_rejected";
        }

        var relevant = autoReviewReport.Candidates.Where(candidate => candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)).ToList();
        return relevant.Count > 0 ? "policy_rejected" : "policy_rejected";
    }

    private static string DetermineFirstFailedStage(string seedFetchStatus, string importStatus, string semanticStatus, string resolverStatus, string policyStatus)
    {
        if (!seedFetchStatus.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return seedFetchStatus;
        }

        if (!importStatus.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return importStatus;
        }

        if (!semanticStatus.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return semanticStatus;
        }

        if (!resolverStatus.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return resolverStatus;
        }

        if (!policyStatus.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return policyStatus;
        }

        return "no_candidate_imported";
    }

    private static string BuildFailureReason(
        string firstFailedStage,
        string seedFetchStatus,
        string importStatus,
        string semanticStatus,
        string resolverStatus,
        string policyStatus,
        IReadOnlyList<string> importedCandidates,
        IReadOnlyList<string> semanticMatches,
        IReadOnlyList<string> resolverMatches,
        IReadOnlyList<string> policyApprovedCandidates,
        ConfirmationResult? confirmation,
        KnowledgeStateRepairDiagnosticItem diagnostic)
    {
        return firstFailedStage switch
        {
            "no_seed_available" => "no enabled seed definition or request found for this knowledge item",
            "seed_fetch_failed" => $"seed fetch did not yield usable content; status={seedFetchStatus}",
            "duplicate_source" => "seed/import candidate duplicated an existing source or publisher group",
            "semantic_match_failed" => "no semantically relevant candidate was accepted by the matcher",
            "resolver_rejected" => "semantic match did not survive independent-source resolution",
            "policy_rejected" => "resolver output did not pass auto source review policy",
            "no_candidate_imported" when importedCandidates.Count == 0 && semanticMatches.Count == 0 && resolverMatches.Count == 0 && policyApprovedCandidates.Count == 0 => "pipeline produced no imported candidate for this item",
            "no_candidate_imported" => "a candidate existed but did not become a canonical source confirmation entry",
            _ => $"no actionable source gain recorded; confirmation_source_count={confirmation?.SourceCount ?? diagnostic.SourceCount}"
        };
    }

    private static string BuildNoSourceGainReason(
        string firstFailedStage,
        string failureReason,
        int sourceCountBefore,
        int sourceCountAfter,
        int policyApprovedCount)
    {
        if (sourceCountAfter > sourceCountBefore)
        {
            return "source_count_increased";
        }

        if (policyApprovedCount > 0 && sourceCountAfter == sourceCountBefore)
        {
            return "policy_approved_candidate_not_materialized_into_source_count";
        }

        return $"no_source_gain_after_stage={firstFailedStage}; {failureReason}";
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
            if (plan.Trace is not null)
            {
                sb.AppendLine($"  - first_failed_stage: {plan.Trace.FirstFailedStage}");
                sb.AppendLine($"  - failure_reason: {plan.Trace.FailureReason}");
                sb.AppendLine($"  - no_source_gain_reason: {plan.Trace.NoSourceGainReason}");
                sb.AppendLine($"  - publisher_groups: {(plan.PublisherGroups.Count == 0 ? "-" : string.Join(", ", plan.PublisherGroups))}");
                sb.AppendLine($"  - source_count_before_after: {plan.Trace.SourceCountBefore} -> {plan.Trace.SourceCountAfter}");
                sb.AppendLine($"  - seed_fetch_status: {plan.Trace.SeedFetchStatus}");
                sb.AppendLine($"  - import_status: {plan.Trace.ImportStatus}");
                sb.AppendLine($"  - semantic_match_status: {plan.Trace.SemanticMatchStatus}");
                sb.AppendLine($"  - resolver_status: {plan.Trace.ResolverStatus}");
                sb.AppendLine($"  - policy_status: {plan.Trace.PolicyStatus}");
            }
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
