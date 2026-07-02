using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record SeedToPolicyTraceSeedItem(
    string SeedId,
    string SeedUrl,
    string PublisherGroup,
    string CandidatePublisherGroup,
    string FetchStatus,
    string ImportStatus,
    double SemanticScore,
    double IndependenceScore,
    double ContradictionRisk,
    string ResolverStatus,
    string PolicyStatus,
    string SourceCountBeforeAfter,
    string FirstFailedStage,
    string FailureReason,
    string RecommendedNextAction,
    IReadOnlyList<string> MatchedTerms);

public sealed record SeedToPolicyTraceItem(
    string KnowledgeItemId,
    string Title,
    string PrimarySourceDomain,
    IReadOnlyList<string> ExistingPublisherGroups,
    int SourceCountBefore,
    int SourceCountAfter,
    string SourceCountBeforeAfter,
    int PolicyApprovedSourceCount,
    IReadOnlyList<SeedToPolicyTraceSeedItem> Seeds,
    string FirstFailedStage,
    string FailureReason,
    string RecommendedNextAction,
    IReadOnlyDictionary<string, int> StageFailureCounts);

public sealed record SeedToPolicyTraceReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedSeedDefinitions,
    int LoadedImportCandidates,
    int LoadedSourceConfirmations,
    int LoadedSemanticCandidates,
    int LoadedResolverCandidates,
    int LoadedPolicyCandidates,
    int LoadedQualityItems,
    int ConsideredKnowledgeItems,
    int ConsideredSeeds,
    int SuccessfulSeeds,
    int FailedSeeds,
    int SourceCountRecalcCandidates,
    IReadOnlyDictionary<string, int> FirstFailedStageCounts,
    IReadOnlyDictionary<string, int> FetchStatusCounts,
    IReadOnlyDictionary<string, int> ImportStatusCounts,
    IReadOnlyDictionary<string, int> SemanticStatusCounts,
    IReadOnlyDictionary<string, int> ResolverStatusCounts,
    IReadOnlyDictionary<string, int> PolicyStatusCounts,
    IReadOnlyList<SeedToPolicyTraceItem> Items,
    IReadOnlyList<string> Warnings,
    string SeedCatalogPath,
    string RequestsPath,
    string ImportCandidatesPath,
    string SourceConfirmationsPath,
    string MatcherReportPath,
    string ResolverReportPath,
    string AutoReviewReportPath,
    string KnowledgeQualityPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class SeedToPolicyTraceDiagnosticsService
{
    private static readonly IReadOnlyList<string> TargetKnowledgeIds =
    [
        "trading:double_top",
        "trading:double_bottom",
        "trading:breakout",
        "trading:inside_bar",
        "trading:gap_trading",
        "trading:daytrading"
    ];

    private static readonly IReadOnlyDictionary<string, int> StageRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["seed_fetch"] = 1,
        ["web_import"] = 2,
        ["semantic_match"] = 3,
        ["independent_resolver"] = 4,
        ["auto_source_review"] = 5,
        ["source_count_recalc"] = 6,
        ["none"] = 7
    };

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public SeedToPolicyTraceDiagnosticsService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "seed_to_policy_trace");
    public string SeedCatalogPath => Path.Combine(_runtimeRoot, "config", "known_article_seed_catalog.json");
    public string RequestsPath => Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog", "known_article_seed_requests.json");
    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string MatcherReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json");
    public string ResolverReportPath => Path.Combine(_storagePaths.Root, "reports", "independent_source_resolver", "independent_source_resolver_report.json");
    public string AutoReviewReportPath => Path.Combine(_storagePaths.Root, "reports", "auto_source_review", "auto_source_review_report.json");
    public string KnowledgeQualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");
    public string ReportPath => Path.Combine(Root, "seed_to_policy_trace_report.json");
    public string MarkdownPath => Path.Combine(Root, "seed_to_policy_trace_report.md");

    public SeedToPolicyTraceReport Run()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;

        var seedCatalog = LoadSeedCatalog();
        var seedRequests = LoadSeedRequests();
        var importCandidates = LoadImportCandidates();
        var confirmations = LoadSourceConfirmations();
        var matcherReport = LoadMatcherReport();
        var resolverReport = LoadResolverReport();
        var policyReport = LoadPolicyReport();
        var qualityReport = LoadQualityReport();
        var catalogItems = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems()
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var confirmationById = confirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var sourceGroupResolver = new PublisherGroupResolverService(_storagePaths, _runtimeRoot);

        var importByKey = importCandidates
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var matcherByKey = (matcherReport?.Candidates ?? [])
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resolverByKey = (resolverReport?.Candidates ?? [])
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var policyByKey = (policyReport?.Candidates ?? [])
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var qualityById = qualityReport?.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);

        var seedsByKnowledgeId = seedCatalog
            .GroupBy(seed => seed.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var itemTraces = new List<SeedToPolicyTraceItem>();
        var warnings = new List<string>();
        var firstFailedStageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fetchStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var importStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var semanticStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var resolverStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var policyStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var successfulSeeds = 0;
        var failedSeeds = 0;
        var sourceCountRecalcCandidates = 0;
        var consideredSeeds = 0;

        foreach (var knowledgeId in TargetKnowledgeIds)
        {
            var item = catalogItems.GetValueOrDefault(knowledgeId);
            var confirmation = confirmationById.GetValueOrDefault(knowledgeId);
            var existingGroups = BuildExistingPublisherGroups(confirmation, sourceGroupResolver);
            var itemSeeds = seedsByKnowledgeId.TryGetValue(knowledgeId, out var rawSeeds)
                ? rawSeeds
                : new List<KnownArticleSeedDefinition>();

            var seedTraces = new List<SeedToPolicyTraceSeedItem>();
            string itemFirstFailedStage = "none";
            string itemFailureReason = string.Empty;
            string itemRecommendedAction = "ready_for_promotion";
            var itemStageFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (itemSeeds.Count == 0)
            {
                warnings.Add($"seed_definition_missing:{knowledgeId}");
            }

            foreach (var seed in itemSeeds.OrderByDescending(seed => seed.Priority))
            {
                consideredSeeds++;
                var request = seedRequests.FirstOrDefault(itemRequest =>
                    itemRequest.SeedId.Equals(seed.SeedId, StringComparison.OrdinalIgnoreCase)
                    || (itemRequest.KnowledgeItemId.Equals(seed.KnowledgeItemId, StringComparison.OrdinalIgnoreCase)
                        && itemRequest.Url.Equals(seed.Url, StringComparison.OrdinalIgnoreCase)));

                var fetchStatus = request?.Status ?? "missing_request";
                var seedPublisherGroup = ResolvePublisherGroup(seed, sourceGroupResolver);
                var candidatePublisherGroup = ResolveCandidatePublisherGroup(knowledgeId, seed.Url, seedPublisherGroup, confirmation, sourceGroupResolver, importCandidates);
                var matchedImport = importCandidates.FirstOrDefault(candidate =>
                    candidate.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase)
                    && candidate.Url.Equals(seed.Url, StringComparison.OrdinalIgnoreCase));
                var matcherCandidate = matcherByKey.GetValueOrDefault(MakeKey(knowledgeId, seed.Url));
                var resolverCandidate = resolverByKey.GetValueOrDefault(MakeKey(knowledgeId, seed.Url));
                var policyCandidate = policyByKey.GetValueOrDefault(MakeKey(knowledgeId, seed.Url));
                var qualityItem = qualityById.GetValueOrDefault(knowledgeId);

                var importStatus = DetermineImportStatus(fetchStatus, matchedImport, confirmation, seed.Url);
                var semanticScore = matcherCandidate?.SemanticMatchScore ?? 0;
                var semanticStatus = matcherCandidate?.Status ?? "not_matched";
                var independenceScore = resolverCandidate?.IndependenceScore ?? 0;
                var contradictionRisk = resolverCandidate?.ContradictionRisk
                    ?? matcherCandidate?.ContradictionRisk
                    ?? 1;
                var resolverStatus = resolverCandidate?.SourceStatus
                    ?? resolverCandidate?.RelationshipStatus
                    ?? "not_resolved";
                var policyStatus = policyCandidate?.PolicyDecision
                    ?? policyCandidate?.ReviewStatus
                    ?? "not_evaluated";
                var sourceCountBefore = Math.Max(0, (confirmation?.SourceCount ?? 0) - (confirmation?.PolicyApprovedSourceCount ?? 0));
                var sourceCountAfter = confirmation?.SourceCount ?? 0;
                var sourceCountBeforeAfter = $"{sourceCountBefore} -> {sourceCountAfter}";
                var firstFailedStage = DetermineFirstFailedStage(
                    fetchStatus,
                    importStatus,
                    semanticScore,
                    semanticStatus,
                    independenceScore,
                    resolverStatus,
                    policyStatus,
                    sourceCountBefore,
                    sourceCountAfter);
                var failureReason = DetermineFailureReason(
                    firstFailedStage,
                    fetchStatus,
                    importStatus,
                    semanticStatus,
                    resolverStatus,
                    policyStatus,
                    sourceCountBeforeAfter);
                var recommendedNextAction = DetermineRecommendedNextAction(firstFailedStage);

                if (!StageRank.TryGetValue(firstFailedStage, out var rank))
                {
                    rank = StageRank["none"];
                }

                if (firstFailedStage != "none")
                {
                    failedSeeds++;
                    itemStageFailures[firstFailedStage] = itemStageFailures.TryGetValue(firstFailedStage, out var existingCount)
                        ? existingCount + 1
                        : 1;
                }
                else
                {
                    successfulSeeds++;
                }

                if (itemFirstFailedStage == "none" || rank < StageRank[itemFirstFailedStage])
                {
                    itemFirstFailedStage = firstFailedStage;
                    itemFailureReason = failureReason;
                    itemRecommendedAction = recommendedNextAction;
                }

                var seedTrace = new SeedToPolicyTraceSeedItem(
                    SeedId: seed.SeedId,
                    SeedUrl: seed.Url,
                    PublisherGroup: seedPublisherGroup,
                    CandidatePublisherGroup: candidatePublisherGroup,
                    FetchStatus: fetchStatus,
                    ImportStatus: importStatus,
                    SemanticScore: semanticScore,
                    IndependenceScore: independenceScore,
                    ContradictionRisk: contradictionRisk,
                    ResolverStatus: resolverStatus,
                    PolicyStatus: policyStatus,
                    SourceCountBeforeAfter: sourceCountBeforeAfter,
                    FirstFailedStage: firstFailedStage,
                    FailureReason: failureReason,
                    RecommendedNextAction: recommendedNextAction,
                    MatchedTerms: BuildMatchedTerms(matcherCandidate, qualityItem));

                seedTraces.Add(seedTrace);

                Increment(fetchStatusCounts, fetchStatus);
                Increment(importStatusCounts, importStatus);
                Increment(semanticStatusCounts, semanticStatus);
                Increment(resolverStatusCounts, resolverStatus);
                Increment(policyStatusCounts, policyStatus);

                if (firstFailedStage == "source_count_recalc")
                {
                    sourceCountRecalcCandidates++;
                }
            }

            if (itemSeeds.Count > 0 && seedTraces.All(trace => trace.FirstFailedStage == "none"))
            {
                itemFirstFailedStage = "none";
                itemFailureReason = string.Empty;
                itemRecommendedAction = "ready_for_promotion";
            }

            itemTraces.Add(new SeedToPolicyTraceItem(
                KnowledgeItemId: knowledgeId,
                Title: item?.Title ?? knowledgeId,
                PrimarySourceDomain: confirmation?.Domain ?? item?.Domain ?? "unknown",
                ExistingPublisherGroups: existingGroups,
                SourceCountBefore: Math.Max(0, (confirmation?.SourceCount ?? 0) - (confirmation?.PolicyApprovedSourceCount ?? 0)),
                SourceCountAfter: confirmation?.SourceCount ?? 0,
                SourceCountBeforeAfter: $"{Math.Max(0, (confirmation?.SourceCount ?? 0) - (confirmation?.PolicyApprovedSourceCount ?? 0))} -> {(confirmation?.SourceCount ?? 0)}",
                PolicyApprovedSourceCount: confirmation?.PolicyApprovedSourceCount ?? 0,
                Seeds: seedTraces,
                FirstFailedStage: itemFirstFailedStage,
                FailureReason: itemFailureReason,
                RecommendedNextAction: itemRecommendedAction,
                StageFailureCounts: itemStageFailures));

            Increment(firstFailedStageCounts, itemFirstFailedStage);
        }

        var report = new SeedToPolicyTraceReport(
            ReportVersion: "seed_to_policy_trace_v1",
            UpdatedAtUtc: now,
            Status: itemTraces.Count == 0 ? "empty" : "ready",
            LoadedSeedDefinitions: seedCatalog.Count,
            LoadedImportCandidates: importCandidates.Count,
            LoadedSourceConfirmations: confirmations.Results.Count,
            LoadedSemanticCandidates: matcherReport?.Candidates.Count ?? 0,
            LoadedResolverCandidates: resolverReport?.Candidates.Count ?? 0,
            LoadedPolicyCandidates: policyReport?.Candidates.Count ?? 0,
            LoadedQualityItems: qualityReport?.Items.Count ?? 0,
            ConsideredKnowledgeItems: itemTraces.Count,
            ConsideredSeeds: consideredSeeds,
            SuccessfulSeeds: successfulSeeds,
            FailedSeeds: failedSeeds,
            SourceCountRecalcCandidates: sourceCountRecalcCandidates,
            FirstFailedStageCounts: firstFailedStageCounts,
            FetchStatusCounts: fetchStatusCounts,
            ImportStatusCounts: importStatusCounts,
            SemanticStatusCounts: semanticStatusCounts,
            ResolverStatusCounts: resolverStatusCounts,
            PolicyStatusCounts: policyStatusCounts,
            Items: itemTraces,
            Warnings: warnings,
            SeedCatalogPath: SeedCatalogPath,
            RequestsPath: RequestsPath,
            ImportCandidatesPath: ImportCandidatesPath,
            SourceConfirmationsPath: SourceConfirmationsPath,
            MatcherReportPath: MatcherReportPath,
            ResolverReportPath: ResolverReportPath,
            AutoReviewReportPath: AutoReviewReportPath,
            KnowledgeQualityPath: KnowledgeQualityPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public SeedToPolicyTraceReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            return JsonSerializer.Deserialize<SeedToPolicyTraceReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    private IReadOnlyList<KnownArticleSeedDefinition> LoadSeedCatalog()
    {
        return new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot).LoadSeeds()
            .Where(seed => TargetKnowledgeIds.Contains(seed.KnowledgeItemId, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private IReadOnlyList<KnownArticleSeedRequest> LoadSeedRequests()
    {
        if (!File.Exists(RequestsPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<KnownArticleSeedRequest>>(
                File.ReadAllText(RequestsPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        if (!File.Exists(ImportCandidatesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(
                File.ReadAllText(ImportCandidatesPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private SourceConfirmationReport LoadSourceConfirmations()
    {
        var engine = new SourceConfirmationEngine(_storagePaths);
        return engine.LoadReport() ?? engine.Build();
    }

    private KnowledgeEvidenceSemanticMatcherReport? LoadMatcherReport()
    {
        if (!File.Exists(MatcherReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceSemanticMatcherReport>(
                File.ReadAllText(MatcherReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IndependentSourceResolverReport? LoadResolverReport()
    {
        if (!File.Exists(ResolverReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IndependentSourceResolverReport>(
                File.ReadAllText(ResolverReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private AutoSourceReviewReport? LoadPolicyReport()
    {
        if (!File.Exists(AutoReviewReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutoSourceReviewReport>(
                File.ReadAllText(AutoReviewReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private KnowledgeQualityReport? LoadQualityReport()
    {
        if (!File.Exists(KnowledgeQualityPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeQualityReport>(
                File.ReadAllText(KnowledgeQualityPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string ResolvePublisherGroup(KnownArticleSeedDefinition seed, PublisherGroupResolverService resolver)
    {
        var explicitGroup = Normalize(seed.PublisherGroup);
        return !string.IsNullOrWhiteSpace(explicitGroup) ? explicitGroup : resolver.Resolve(seed.Url);
    }

    private static string ResolveCandidatePublisherGroup(
        string knowledgeId,
        string seedUrl,
        string seedPublisherGroup,
        ConfirmationResult? confirmation,
        PublisherGroupResolverService resolver,
        IReadOnlyList<WebResearchImportCandidateRecord> candidates)
    {
        var candidate = candidates.FirstOrDefault(item =>
            item.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase)
            && item.Url.Equals(seedUrl, StringComparison.OrdinalIgnoreCase));

        if (candidate is not null)
        {
            return Normalize(candidate.Domain).Length > 0
                ? resolver.Resolve(candidate.Url)
                : seedPublisherGroup;
        }

        var sourceCandidate = confirmation?.CandidateSources?.FirstOrDefault(item =>
            item.Url.Equals(seedUrl, StringComparison.OrdinalIgnoreCase));
        if (sourceCandidate is not null)
        {
            return resolver.Resolve(sourceCandidate.Url);
        }

        return seedPublisherGroup;
    }

    private static string DetermineImportStatus(
        string fetchStatus,
        WebResearchImportCandidateRecord? importCandidate,
        ConfirmationResult? confirmation,
        string seedUrl)
    {
        if (!string.Equals(fetchStatus, "accepted_candidate", StringComparison.OrdinalIgnoreCase))
        {
            return fetchStatus;
        }

        if (confirmation?.CandidateSources?.Any(candidate =>
                candidate.Url.Equals(seedUrl, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "import_applied";
        }

        if (importCandidate is not null)
        {
            return "candidate_exported";
        }

        return "missing_import_candidate";
    }

    private static string DetermineFirstFailedStage(
        string fetchStatus,
        string importStatus,
        double semanticScore,
        string semanticStatus,
        double independenceScore,
        string resolverStatus,
        string policyStatus,
        int sourceCountBefore,
        int sourceCountAfter)
    {
        if (!string.Equals(fetchStatus, "accepted_candidate", StringComparison.OrdinalIgnoreCase))
        {
            return "seed_fetch";
        }

        if (importStatus is "missing_import_candidate"
            || importStatus.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || importStatus.Contains("rejected", StringComparison.OrdinalIgnoreCase)
            || importStatus.Contains("same_domain", StringComparison.OrdinalIgnoreCase))
        {
            return "web_import";
        }

        if (semanticScore < 0.45
            || semanticStatus is "candidate_rejected" or "not_matched" or "rejected")
        {
            return "semantic_match";
        }

        if (independenceScore < 0.8
            || resolverStatus.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || resolverStatus.Contains("same_domain", StringComparison.OrdinalIgnoreCase)
            || resolverStatus.Contains("not_resolved", StringComparison.OrdinalIgnoreCase))
        {
            return "independent_resolver";
        }

        if (!string.Equals(policyStatus, "auto_approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(policyStatus, "policy_approved_second_source", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(policyStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return "auto_source_review";
        }

        if (sourceCountAfter <= sourceCountBefore)
        {
            return "source_count_recalc";
        }

        return "none";
    }

    private static string DetermineFailureReason(
        string firstFailedStage,
        string fetchStatus,
        string importStatus,
        string semanticStatus,
        string resolverStatus,
        string policyStatus,
        string sourceCountBeforeAfter)
    {
        return firstFailedStage switch
        {
            "seed_fetch" => fetchStatus,
            "web_import" => importStatus,
            "semantic_match" => semanticStatus,
            "independent_resolver" => resolverStatus,
            "auto_source_review" => policyStatus,
            "source_count_recalc" => $"source_count_not_increased:{sourceCountBeforeAfter}",
            _ => string.Empty
        };
    }

    private static string DetermineRecommendedNextAction(string firstFailedStage) =>
        firstFailedStage switch
        {
            "seed_fetch" => "retry_seed_fetch_or_replace_seed_url",
            "web_import" => "fix_import_candidate_and_reexport",
            "semantic_match" => "improve_query_and_relevance_scoring",
            "independent_resolver" => "verify_publisher_group_independence",
            "auto_source_review" => "adjust_policy_or_schedule_human_review",
            "source_count_recalc" => "run_validation_state_sync",
            _ => "ready_for_promotion"
        };

    private static IReadOnlyList<string> BuildExistingPublisherGroups(ConfirmationResult? confirmation, PublisherGroupResolverService resolver)
    {
        if (confirmation?.CandidateSources is null || confirmation.CandidateSources.Count == 0)
        {
            return [];
        }

        return confirmation.CandidateSources
            .Select(candidate => resolver.Resolve(candidate.Url))
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildMatchedTerms(KnowledgeEvidenceSemanticMatchCandidate? matcherCandidate, KnowledgeQualityItem? qualityItem)
    {
        var terms = new List<string>();
        if (matcherCandidate is not null)
        {
            terms.AddRange(matcherCandidate.MatchedTerms);
        }

        if (qualityItem is not null && qualityItem.Reasons.Count > 0)
        {
            terms.AddRange(qualityItem.Reasons.Take(3));
        }

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static string MakeKey(string knowledgeItemId, string url) =>
        $"{knowledgeItemId}||{url}".ToLowerInvariant();

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static void Increment(IDictionary<string, int> counts, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "unknown";
        }

        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static string BuildMarkdown(SeedToPolicyTraceReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Seed To Policy Trace Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Considered Items: {report.ConsideredKnowledgeItems}");
        sb.AppendLine($"- Considered Seeds: {report.ConsideredSeeds}");
        sb.AppendLine($"- Successful Seeds: {report.SuccessfulSeeds}");
        sb.AppendLine($"- Failed Seeds: {report.FailedSeeds}");
        sb.AppendLine($"- Source Count Recalc Candidates: {report.SourceCountRecalcCandidates}");
        sb.AppendLine();
        sb.AppendLine("## First Failed Stage Counts");
        foreach (var pair in report.FirstFailedStageCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {pair.Key}: {pair.Value}");
        }
        sb.AppendLine();
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.KnowledgeItemId}");
            sb.AppendLine($"- Title: {item.Title}");
            sb.AppendLine($"- Primary Source Domain: {item.PrimarySourceDomain}");
            sb.AppendLine($"- Existing Publisher Groups: {(item.ExistingPublisherGroups.Count == 0 ? "-" : string.Join(", ", item.ExistingPublisherGroups))}");
            sb.AppendLine($"- Source Count: {item.SourceCountBeforeAfter}");
            sb.AppendLine($"- Policy Approved Source Count: {item.PolicyApprovedSourceCount}");
            sb.AppendLine($"- First Failed Stage: {item.FirstFailedStage}");
            sb.AppendLine($"- Failure Reason: {item.FailureReason}");
            sb.AppendLine($"- Recommended Next Action: {item.RecommendedNextAction}");
            foreach (var seed in item.Seeds)
            {
                sb.AppendLine($"  - Seed: {seed.SeedId}");
                sb.AppendLine($"    - URL: {seed.SeedUrl}");
                sb.AppendLine($"    - Publisher Group: {seed.PublisherGroup}");
                sb.AppendLine($"    - Candidate Publisher Group: {seed.CandidatePublisherGroup}");
                sb.AppendLine($"    - Fetch Status: {seed.FetchStatus}");
                sb.AppendLine($"    - Import Status: {seed.ImportStatus}");
                sb.AppendLine($"    - Semantic Score: {seed.SemanticScore:0.###}");
                sb.AppendLine($"    - Independence Score: {seed.IndependenceScore:0.###}");
                sb.AppendLine($"    - Contradiction Risk: {seed.ContradictionRisk:0.###}");
                sb.AppendLine($"    - Resolver Status: {seed.ResolverStatus}");
                sb.AppendLine($"    - Policy Status: {seed.PolicyStatus}");
                sb.AppendLine($"    - Source Count Before/After: {seed.SourceCountBeforeAfter}");
                sb.AppendLine($"    - First Failed Stage: {seed.FirstFailedStage}");
                sb.AppendLine($"    - Failure Reason: {seed.FailureReason}");
                sb.AppendLine($"    - Recommended Next Action: {seed.RecommendedNextAction}");
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
