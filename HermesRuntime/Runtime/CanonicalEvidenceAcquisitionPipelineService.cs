using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CanonicalEvidenceAcquisitionTrace(
    string KnowledgeItemId,
    string Title,
    string Domain,
    int SourceCountBefore,
    int SourceCountAfter,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    string Query,
    IReadOnlyList<string> RecommendedSourceDomains,
    IReadOnlyList<string> QueryTerms,
    IReadOnlyList<string> CatalogSourcesUsed,
    int RequestsExported,
    int PagesFetched,
    int CandidatesFound,
    int SemanticMatches,
    int IndependentSourcesFound,
    int PolicyApprovedSources,
    string ValidationSyncStatus,
    bool PromotionEligible,
    string NextAction,
    IReadOnlyList<string> BlockersBefore,
    IReadOnlyList<string> BlockersAfter,
    IReadOnlyList<string> Warnings);

public sealed record CanonicalEvidenceAcquisitionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int ConsideredItems,
    int TotalSecondSourceItems,
    int EvidenceCandidatesFound,
    int SemanticMatches,
    int IndependentSourcesFound,
    int PolicyApprovedSources,
    int SourceCountIncreasedItems,
    int RejectedLowRelevance,
    int RejectedSameDomain,
    int RejectedPolicy,
    int LoadedRequests,
    int ExportedSearchRequests,
    int AcceptedImportCandidates,
    int RejectedImportCandidates,
    int ValidationSynchronizedItems,
    int TrustedPromotionEligibleItems,
    IReadOnlyList<string> NextActions,
    IReadOnlyList<CanonicalEvidenceAcquisitionTrace> PerItemTrace,
    IReadOnlyList<string> PrioritizedKnowledgeItems,
    IReadOnlyDictionary<string, int> TopRejectionReasons,
    IReadOnlyList<string> Warnings,
    string MultiSourceEvidencePath,
    string WebResearchRequestsPath,
    string DirectDomainResearchPath,
    string ImportCandidatesPath,
    string SemanticMatcherPath,
    string IndependentResolverPath,
    string AutoSourceReviewPath,
    string SourceConfirmationsPath,
    string ValidationStateSyncPath,
    string TrustPromotionPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool Applied,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class CanonicalEvidenceAcquisitionPipelineService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public CanonicalEvidenceAcquisitionPipelineService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "canonical_evidence_acquisition");
    public string ReportPath => Path.Combine(Root, "canonical_evidence_acquisition_report.json");
    public string MarkdownPath => Path.Combine(Root, "canonical_evidence_acquisition_report.md");

    public CanonicalEvidenceAcquisitionReport Run(int maxItems, bool apply, bool dryRun)
    {
        if (apply && dryRun)
        {
            throw new InvalidOperationException("Use either dryRun or apply, not both.");
        }

        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;

        var sourceConfirmationEngine = new SourceConfirmationEngine(_storagePaths);
        var initialConfirmations = sourceConfirmationEngine.LoadReport() ?? sourceConfirmationEngine.Build();
        var initialConfirmationById = initialConfirmations.Results
            .ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);

        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var initialQuality = qualityEngine.LoadReport() ?? qualityEngine.Run();

        var multiSourceService = new MultiSourceEvidenceIngestionService(_storagePaths);
        var multiSourceReport = multiSourceService.Run(apply: apply, dryRun: dryRun || !apply);

        var collectorService = new ControlledWebResearchSourceCollectorService(_storagePaths);
        var collectorReport = collectorService.Run(apply: apply && !dryRun);

        var directDomainService = new DirectDomainResearchFetcherService(_storagePaths, _runtimeRoot);
        var directDomainReport = directDomainService.Run(maxItems: Math.Max(1, maxItems), dryRun: dryRun || !apply);

        var importService = new WebResearchSourceImportService(_storagePaths);
        var importReport = importService.Run(apply: apply && !dryRun);

        var matcherService = new KnowledgeEvidenceSemanticMatcherService(_storagePaths);
        var matcherReport = matcherService.Run(apply: apply && !dryRun);

        var resolverService = new IndependentSourceResolverService(_storagePaths);
        var resolverReport = resolverService.Run(apply: apply && !dryRun);

        var autoReviewService = new AutoSourceReviewPolicyService(_storagePaths, _runtimeRoot);
        var autoReviewReport = autoReviewService.Run(apply: apply && !dryRun);

        var refreshedQuality = qualityEngine.Run();
        var sourceConfirmationsAfterQuality = sourceConfirmationEngine.LoadReport() ?? sourceConfirmationEngine.Build();
        var validationSyncService = new ValidationStateSynchronizerService(_storagePaths);
        var validationSyncReport = validationSyncService.Run(apply: apply && !dryRun, dryRun: dryRun || !apply);
        var trustPromotionService = new KnowledgeTrustPromotionPipelineService(_storagePaths);
        var trustPromotionReport = trustPromotionService.Run(apply: false);

        if (apply && !dryRun)
        {
            _ = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot)).WriteSnapshot();
        }

        var canonicalItems = BuildCanonicalTraces(
            initialConfirmations,
            sourceConfirmationsAfterQuality,
            initialQuality,
            refreshedQuality,
            multiSourceReport,
            collectorReport,
            directDomainReport,
            importReport,
            matcherReport,
            resolverReport,
            autoReviewReport,
            validationSyncReport,
            trustPromotionReport,
            maxItems);

        var report = new CanonicalEvidenceAcquisitionReport(
            ReportVersion: "canonical_evidence_acquisition_v1",
            UpdatedAtUtc: now,
            Status: apply && !dryRun ? "applied" : "dry_run_ready",
            LoadedItems: initialQuality.Items.Count,
            ConsideredItems: canonicalItems.Count,
            TotalSecondSourceItems: multiSourceReport.ItemsNeedingSecondSource,
            EvidenceCandidatesFound: directDomainReport.ExtractedCandidates + importReport.AcceptedCandidates,
            SemanticMatches: matcherReport.CandidateRelevant + matcherReport.CandidateWeak + matcherReport.NeedsHumanReview,
            IndependentSourcesFound: resolverReport.IndependentCandidates + resolverReport.IndependentExistingCandidates,
            PolicyApprovedSources: autoReviewReport.AutoApprovedCandidates,
            SourceCountIncreasedItems: autoReviewReport.SourceCountIncreasedKnowledgeItems,
            RejectedLowRelevance: directDomainReport.CandidatesRejectedLowRelevance,
            RejectedSameDomain: importReport.BlockedSameDomain + resolverReport.SameDomainCandidates,
            RejectedPolicy: autoReviewReport.RejectedCandidates,
            LoadedRequests: collectorReport.TotalSecondSourceItems,
            ExportedSearchRequests: collectorReport.ExportedSearchRequests,
            AcceptedImportCandidates: importReport.AcceptedCandidates,
            RejectedImportCandidates: importReport.RejectedCandidates,
            ValidationSynchronizedItems: validationSyncReport.SynchronizedItems,
            TrustedPromotionEligibleItems: trustPromotionReport.EligibleForPromotion,
            NextActions: BuildNextActions(multiSourceReport, collectorReport, directDomainReport, importReport, matcherReport, resolverReport, autoReviewReport, validationSyncReport, trustPromotionReport),
            PerItemTrace: canonicalItems,
            PrioritizedKnowledgeItems: multiSourceReport.PrioritizedCandidates.Select(candidate => candidate.KnowledgeId).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            TopRejectionReasons: MergeRejectionReasons(directDomainReport, importReport, matcherReport, resolverReport, autoReviewReport),
            Warnings: BuildWarnings(multiSourceReport, collectorReport, directDomainReport, importReport, matcherReport, resolverReport, autoReviewReport, validationSyncReport, trustPromotionReport),
            MultiSourceEvidencePath: multiSourceService.ReportPath,
            WebResearchRequestsPath: collectorService.ReportPath,
            DirectDomainResearchPath: directDomainService.ReportPath,
            ImportCandidatesPath: importService.ReportPath,
            SemanticMatcherPath: matcherService.ReportPath,
            IndependentResolverPath: resolverService.ReportPath,
            AutoSourceReviewPath: autoReviewService.ReportPath,
            SourceConfirmationsPath: sourceConfirmationEngine.ReportPath,
            ValidationStateSyncPath: validationSyncService.ReportPath,
            TrustPromotionPath: trustPromotionService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: dryRun || !apply,
            Applied: apply && !dryRun,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);

        WriteReport(report);
        return report;
    }

    public CanonicalEvidenceAcquisitionReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(maxItems: 10, apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<CanonicalEvidenceAcquisitionReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(maxItems: 10, apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(maxItems: 10, apply: false, dryRun: true) with { Status = "status_snapshot_generated" };
        }
    }

    private IReadOnlyList<CanonicalEvidenceAcquisitionTrace> BuildCanonicalTraces(
        SourceConfirmationReport initialConfirmations,
        SourceConfirmationReport finalConfirmations,
        KnowledgeQualityReport initialQuality,
        KnowledgeQualityReport finalQuality,
        MultiSourceEvidencePlanReport multiSourceReport,
        WebResearchSourceCollectorReport collectorReport,
        DirectDomainResearchReport directDomainReport,
        WebResearchImportReport importReport,
        KnowledgeEvidenceSemanticMatcherReport matcherReport,
        IndependentSourceResolverReport resolverReport,
        AutoSourceReviewReport autoReviewReport,
        ValidationStateSynchronizerReport validationSyncReport,
        KnowledgeTrustPromotionReport trustPromotionReport,
        int maxItems)
    {
        var qualityById = finalQuality.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var initialConfirmationById = initialConfirmations.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var finalConfirmationById = finalConfirmations.Results.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var collectorById = collectorReport.Requests
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var directById = directDomainReport.RequestResults
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var semanticById = matcherReport.Candidates
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var resolverById = resolverReport.Candidates
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var autoById = autoReviewReport.Candidates
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var syncById = validationSyncReport.Items
            .GroupBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var trustById = trustPromotionReport.Candidates
            .GroupBy(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var prioritized = multiSourceReport.PrioritizedCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.KnowledgeId))
            .Take(Math.Max(1, maxItems))
            .Select(candidate =>
            {
                qualityById.TryGetValue(candidate.KnowledgeId, out var quality);
                initialConfirmationById.TryGetValue(candidate.KnowledgeId, out var beforeConfirmation);
                finalConfirmationById.TryGetValue(candidate.KnowledgeId, out var afterConfirmation);
                collectorById.TryGetValue(candidate.KnowledgeId, out var collectorItem);
                directById.TryGetValue(candidate.KnowledgeId, out var directItem);
                semanticById.TryGetValue(candidate.KnowledgeId, out var semanticItems);
                resolverById.TryGetValue(candidate.KnowledgeId, out var resolverItems);
                autoById.TryGetValue(candidate.KnowledgeId, out var autoItems);
                syncById.TryGetValue(candidate.KnowledgeId, out var syncItem);
                trustById.TryGetValue(candidate.KnowledgeId, out var trustItem);

                var semanticMatches = semanticItems?.Count(item => item.Status is "candidate_relevant" or "candidate_weak" or "needs_human_review") ?? 0;
                var independentSources = resolverItems?.Count(item => item.SourceStatus.Equals("independent_candidate_pending_review", StringComparison.OrdinalIgnoreCase)) ?? 0;
                var policyApproved = autoItems?.Count(item => item.AutoApprovedByPolicy) ?? 0;
                var sourceCountBefore = beforeConfirmation?.SourceCount ?? candidate.CurrentSourceCount;
                var sourceCountAfter = afterConfirmation?.SourceCount ?? sourceCountBefore;
                var nextAction = DetermineNextAction(syncItem, trustItem, policyApproved, sourceCountAfter);

                return new CanonicalEvidenceAcquisitionTrace(
                    KnowledgeItemId: candidate.KnowledgeId,
                    Title: candidate.Title,
                    Domain: candidate.Domain,
                    SourceCountBefore: sourceCountBefore,
                    SourceCountAfter: sourceCountAfter,
                    TrustScore: quality?.TrustScore ?? candidate.TrustScore,
                    QualityScore: quality?.QualityScore ?? candidate.QualityScore,
                    ValidationScore: quality?.ValidationScore ?? candidate.ValidationScore,
                    Query: candidate.Query ?? collectorItem?.Query ?? string.Empty,
                    RecommendedSourceDomains: candidate.RecommendedQueries.Count > 0
                        ? candidate.RecommendedQueries
                        : collectorItem?.RecommendedSourceDomains ?? [],
                    QueryTerms: directItem?.QueryTerms ?? multiSourceQueryTerms(candidate),
                    CatalogSourcesUsed: directItem?.CatalogSourcesUsed ?? [],
                    RequestsExported: collectorItem is null ? 0 : 1,
                    PagesFetched: directItem?.FetchedPages ?? 0,
                    CandidatesFound: directItem?.ExtractedCandidates ?? 0,
                    SemanticMatches: semanticMatches,
                    IndependentSourcesFound: independentSources,
                    PolicyApprovedSources: policyApproved,
                    ValidationSyncStatus: syncItem?.Synchronized == true ? syncItem.RecommendedNextAction : (syncItem is null ? "not_synced" : "pending_sync"),
                    PromotionEligible: trustItem?.EligibleForPromotion == true,
                    NextAction: nextAction,
                    BlockersBefore: syncItem?.RemainingBlockersBefore ?? [],
                    BlockersAfter: syncItem?.RemainingBlockersAfter ?? [],
                    Warnings: BuildTraceWarnings(candidate, directItem, importReport, semanticItems, resolverItems, autoItems, syncItem, trustItem));
            })
            .ToList();

        return prioritized;
    }

    private static IReadOnlyList<string> multiSourceQueryTerms(MultiSourceEvidenceCandidate candidate) =>
        candidate.RecommendedQueries.Take(6).ToList();

    private static string DetermineNextAction(
        ValidationStateSynchronizerItem? syncItem,
        KnowledgeTrustPromotionCandidate? trustItem,
        int policyApproved,
        int sourceCountAfter)
    {
        if (sourceCountAfter < 2)
        {
            return "continue_evidence_acquisition";
        }

        if (syncItem is not null && syncItem.Synchronized)
        {
            if (trustItem?.EligibleForPromotion == true)
            {
                return "run_knowledge_trust_promote";
            }

            return "run_knowledge_trust_promote_dry_run";
        }

        if (policyApproved > 0)
        {
            return "run_validation_state_sync";
        }

        return "continue_evidence_acquisition";
    }

    private static IReadOnlyList<string> BuildTraceWarnings(
        MultiSourceEvidenceCandidate candidate,
        DirectDomainResearchRequestResult? directItem,
        WebResearchImportReport importReport,
        IReadOnlyList<KnowledgeEvidenceSemanticMatchCandidate>? matcherItems,
        IReadOnlyList<IndependentSourceResolverCandidate>? resolverItems,
        IReadOnlyList<AutoSourceReviewCandidate>? autoItems,
        ValidationStateSynchronizerItem? syncItem,
        KnowledgeTrustPromotionCandidate? trustItem)
    {
        var warnings = new List<string>();
        if (candidate.CurrentSourceCount < 2)
        {
            warnings.Add("second_independent_source_missing");
        }

        if ((directItem?.ExtractedCandidates ?? 0) == 0)
        {
            warnings.Add("no_direct_domain_candidates");
        }

        if (importReport.AcceptedCandidates == 0)
        {
            warnings.Add("no_import_candidates_accepted");
        }

        if (matcherItems is null || matcherItems.Count == 0)
        {
            warnings.Add("no_semantic_matches");
        }

        if (resolverItems is null || resolverItems.Count == 0)
        {
            warnings.Add("no_independent_sources");
        }

        if (autoItems is null || autoItems.Count == 0)
        {
            warnings.Add("no_policy_approved_sources");
        }

        if (syncItem is null || !syncItem.Synchronized)
        {
            warnings.Add("validation_state_not_synchronized");
        }

        if (trustItem is null || !trustItem.EligibleForPromotion)
        {
            warnings.Add("not_yet_trusted_ready");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyDictionary<string, int> MergeRejectionReasons(
        DirectDomainResearchReport directDomainReport,
        WebResearchImportReport importReport,
        KnowledgeEvidenceSemanticMatcherReport matcherReport,
        IndependentSourceResolverReport resolverReport,
        AutoSourceReviewReport autoReviewReport)
    {
        var reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        AddReasons(directDomainReport.TopRejectionReasons, reasons);
        AddReasons(importReport.Rejected.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : importReport.Rejected
                .Select(candidate => candidate.RejectionReason ?? "import_rejected")
                .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase), reasons);
        AddReasons(matcherReport.Rejected
            .Select(candidate => candidate.RejectionReason ?? "semantic_rejected")
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase), reasons);
        AddReasons(resolverReport.Rejected
            .Select(candidate => candidate.RejectionReason ?? "resolver_rejected")
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase), reasons);
        AddReasons(autoReviewReport.Rejected
            .Select(candidate => candidate.PolicyReason ?? "policy_rejected")
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase), reasons);
        return reasons
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddReasons(IReadOnlyDictionary<string, int> source, IDictionary<string, int> target)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = target.TryGetValue(pair.Key, out var current) ? current + pair.Value : pair.Value;
        }
    }

    private void WriteReport(CanonicalEvidenceAcquisitionReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static IReadOnlyList<string> BuildNextActions(
        MultiSourceEvidencePlanReport multiSourceReport,
        WebResearchSourceCollectorReport collectorReport,
        DirectDomainResearchReport directDomainReport,
        WebResearchImportReport importReport,
        KnowledgeEvidenceSemanticMatcherReport matcherReport,
        IndependentSourceResolverReport resolverReport,
        AutoSourceReviewReport autoReviewReport,
        ValidationStateSynchronizerReport validationSyncReport,
        KnowledgeTrustPromotionReport trustPromotionReport)
    {
        var actions = new[]
        {
            multiSourceReport.CreatedResearchQueueItems > 0 ? "run_web_research_source_collector" : null,
            collectorReport.ExportedSearchRequests > 0 ? "run_direct_domain_research_fetch" : null,
            directDomainReport.ExtractedCandidates > 0 ? "run_web_research_import" : null,
            importReport.AcceptedCandidates > 0 ? "run_knowledge_evidence_match" : null,
            matcherReport.CandidateRelevant > 0 || matcherReport.CandidateWeak > 0 || matcherReport.NeedsHumanReview > 0 ? "run_independent_source_resolver" : null,
            resolverReport.IndependentCandidates > 0 || resolverReport.IndependentExistingCandidates > 0 ? "run_auto_source_review" : null,
            autoReviewReport.SourceCountIncreasedKnowledgeItems > 0 ? "run_validation_state_sync" : null,
            validationSyncReport.SynchronizedItems > 0 ? "run_knowledge_trust_promote" : null,
            trustPromotionReport.EligibleForPromotion > 0 ? "knowledge_trust_promote_ready" : "continue_canonical_evidence_acquisition"
        };

        return actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList()!;
    }

    private static IReadOnlyList<string> BuildWarnings(
        MultiSourceEvidencePlanReport multiSourceReport,
        WebResearchSourceCollectorReport collectorReport,
        DirectDomainResearchReport directDomainReport,
        WebResearchImportReport importReport,
        KnowledgeEvidenceSemanticMatcherReport matcherReport,
        IndependentSourceResolverReport resolverReport,
        AutoSourceReviewReport autoReviewReport,
        ValidationStateSynchronizerReport validationSyncReport,
        KnowledgeTrustPromotionReport trustPromotionReport)
    {
        var warnings = new List<string>();
        warnings.AddRange(multiSourceReport.Warnings);
        warnings.AddRange(collectorReport.Warnings);
        warnings.AddRange(directDomainReport.Warnings);
        warnings.AddRange(importReport.Warnings);
        warnings.AddRange(matcherReport.Warnings);
        warnings.AddRange(resolverReport.Warnings);
        warnings.AddRange(autoReviewReport.Warnings);
        warnings.AddRange(validationSyncReport.Warnings);
        warnings.AddRange(trustPromotionReport.Warnings);
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildMarkdown(CanonicalEvidenceAcquisitionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Canonical Evidence Acquisition Pipeline");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Items: {report.LoadedItems}");
        sb.AppendLine($"- Considered Items: {report.ConsideredItems}");
        sb.AppendLine($"- Total Second Source Items: {report.TotalSecondSourceItems}");
        sb.AppendLine($"- Evidence Candidates Found: {report.EvidenceCandidatesFound}");
        sb.AppendLine($"- Semantic Matches: {report.SemanticMatches}");
        sb.AppendLine($"- Independent Sources Found: {report.IndependentSourcesFound}");
        sb.AppendLine($"- Policy Approved Sources: {report.PolicyApprovedSources}");
        sb.AppendLine($"- Source Count Increased Items: {report.SourceCountIncreasedItems}");
        sb.AppendLine($"- Rejected Low Relevance: {report.RejectedLowRelevance}");
        sb.AppendLine($"- Rejected Same Domain: {report.RejectedSameDomain}");
        sb.AppendLine($"- Rejected Policy: {report.RejectedPolicy}");
        sb.AppendLine($"- Loaded Requests: {report.LoadedRequests}");
        sb.AppendLine($"- Exported Search Requests: {report.ExportedSearchRequests}");
        sb.AppendLine($"- Accepted Import Candidates: {report.AcceptedImportCandidates}");
        sb.AppendLine($"- Rejected Import Candidates: {report.RejectedImportCandidates}");
        sb.AppendLine($"- Validation Synchronized Items: {report.ValidationSynchronizedItems}");
        sb.AppendLine($"- Trusted Promotion Eligible Items: {report.TrustedPromotionEligibleItems}");
        sb.AppendLine();
        sb.AppendLine("## Next Actions");
        foreach (var action in report.NextActions)
        {
            sb.AppendLine($"- {action}");
        }

        if (report.PrioritizedKnowledgeItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Prioritized Knowledge Items");
            foreach (var item in report.PrioritizedKnowledgeItems.Take(20))
            {
                sb.AppendLine($"- {item}");
            }
        }

        if (report.PerItemTrace.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Per Item Trace");
            foreach (var item in report.PerItemTrace.Take(20))
            {
                sb.AppendLine($"- {item.KnowledgeItemId} | {item.Domain} | source {item.SourceCountBefore} -> {item.SourceCountAfter} | semantic={item.SemanticMatches} | independent={item.IndependentSourcesFound} | policy={item.PolicyApprovedSources} | next={item.NextAction}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
