using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record MultiSourceAcquisitionItemTrace(
    string KnowledgeItemId,
    string Title,
    string Domain,
    int SourceCountBefore,
    int SourceCountAfter,
    IReadOnlyList<string> PublisherGroupsBefore,
    IReadOnlyList<string> PublisherGroupsAfter,
    int AcceptedSources,
    int RejectedSources,
    int DuplicatePublisherGroups,
    double CoveragePercent,
    string Status,
    string NextAction,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> QueryTerms,
    IReadOnlyList<string> MatchedSeedIds);

public sealed record MultiSourceAcquisitionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int ConsideredItems,
    int PublisherGroupsFound,
    int IndependentPublishersFound,
    int AcceptedSources,
    int RejectedSources,
    int DuplicatePublisherGroups,
    int PolicyApprovedSources,
    int SourceCountIncreasedItems,
    IReadOnlyDictionary<string, double> CoverageByItem,
    IReadOnlyList<MultiSourceAcquisitionItemTrace> PerItemTrace,
    IReadOnlyList<string> NextActions,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string KnownArticleSeedCatalogPath,
    string TrustedSourceCatalogPath,
    string ImportCandidatesPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class MultiSourceAcquisitionService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private readonly HttpClient _httpClient;
    private readonly PublisherGroupResolverService _publisherGroups;

    public MultiSourceAcquisitionService(StoragePaths storagePaths, string? runtimeRoot = null, HttpClient? httpClient = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HermesRuntime/1.0");
        }

        _publisherGroups = new PublisherGroupResolverService(storagePaths, _runtimeRoot);
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "multi_source_acquisition");

    public string ReportPath => Path.Combine(Root, "multi_source_acquisition_report.json");

    public string MarkdownPath => Path.Combine(Root, "multi_source_acquisition_report.md");

    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");

    public MultiSourceAcquisitionReport LoadStatus() => BuildReport(maxItems: 10, dryRun: true, fetchRemote: false, apply: false);

    public MultiSourceAcquisitionReport Run(int maxItems, bool dryRun) => BuildReport(Math.Max(1, maxItems), dryRun, fetchRemote: true, apply: !dryRun);

    private MultiSourceAcquisitionReport BuildReport(int maxItems, bool dryRun, bool fetchRemote, bool apply)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var catalogService = new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot);
        var seeds = catalogService.LoadSeeds();
        var trustedCatalog = new TrustedSourceCatalogService(_storagePaths, _runtimeRoot).LoadCatalog();
        var qualityReport = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var confirmations = new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
        var sourceByKnowledge = confirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var knowledgeItems = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var existingImportCandidates = LoadImportCandidates();
        var existingUrls = existingImportCandidates
            .Select(candidate => candidate.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingGroups = BuildExistingGroups(confirmations);
        var qualityById = qualityReport.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var prioritizedItems = knowledgeItems
            .Select(item => new
            {
                Item = item,
                SourceCount = SourceConfirmationEngine.CanonicalSourceCount(item, sourceByKnowledge.GetValueOrDefault(item.Id)),
                Quality = qualityById.GetValueOrDefault(item.Id)
            })
            .Where(entry => entry.SourceCount < 2)
            .OrderByDescending(entry => entry.Item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entry => entry.Quality?.TrustScore ?? 0)
            .ThenByDescending(entry => entry.Quality?.QualityScore ?? 0)
            .ThenByDescending(entry => entry.Quality?.ValidationScore ?? 0)
            .ThenBy(entry => entry.Item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        var accepted = new List<WebResearchImportCandidateRecord>();
        var rejected = new List<WebResearchImportCandidateRecord>();
        var traces = new List<MultiSourceAcquisitionItemTrace>();
        var warnings = new List<string>();
        var coverageByItem = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var policyApprovedSources = 0;
        var sourceCountIncreasedItems = 0;
        var groupCountByKnowledge = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in prioritizedItems)
        {
            var item = entry.Item;
            var sourceBefore = entry.SourceCount;
            var itemGroupSet = existingGroups.TryGetValue(item.Id, out var knownGroups)
                ? new HashSet<string>(knownGroups, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!groupCountByKnowledge.TryGetValue(item.Id, out var groups))
            {
                groups = itemGroupSet;
                groupCountByKnowledge[item.Id] = groups;
            }

            var itemSeeds = seeds
                .Where(seed => seed.Allowed && seed.KnowledgeItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(seed => seed.Priority)
                .ToList();

            var queryTerms = BuildQueryTerms(item);
            var acceptedForItem = new List<WebResearchImportCandidateRecord>();
            var rejectedForItem = new List<WebResearchImportCandidateRecord>();
            var matchedSeedIds = new List<string>();
            var itemDuplicatePublisherGroups = 0;

            foreach (var seed in itemSeeds)
            {
                var publisherGroup = _publisherGroups.Resolve(seed.Domain);
                if (!string.IsNullOrWhiteSpace(publisherGroup) && groups.Contains(publisherGroup))
                {
                    itemDuplicatePublisherGroups++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(publisherGroup))
                {
                    groups.Add(publisherGroup);
                }

                matchedSeedIds.Add(seed.SeedId);
                var candidate = fetchRemote
                    ? FetchSeed(seed, item, queryTerms)
                    : BuildPlannedCandidate(seed, item, queryTerms, publisherGroup);

                if (candidate is null)
                {
                    rejectedForItem.Add(CreateRejectedCandidate(seed, item, "fetch_failed", "no_html_content"));
                    continue;
                }

                var evaluation = ScoreCandidate(seed, item, candidate);
                var normalized = candidate with
                {
                    RelevanceScore = evaluation.Score,
                    MatchedTerms = evaluation.MatchedTerms,
                    RejectionReason = evaluation.RejectionReason,
                    SourceRelevanceStatus = evaluation.Status,
                    HumanReviewStatus = "pending",
                    SafetyFlags = ["no_trading_execution", "human_review_required"]
                };

                if (string.IsNullOrWhiteSpace(normalized.Url) || existingUrls.Contains(normalized.Url))
                {
                    rejectedForItem.Add(normalized with { RejectionReason = "duplicate_url", SourceRelevanceStatus = "duplicate_url" });
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(publisherGroup) && itemGroupSet.Contains(publisherGroup))
                {
                    rejectedForItem.Add(normalized with { RejectionReason = "duplicate_publisher_group", SourceRelevanceStatus = "duplicate_publisher_group" });
                    continue;
                }

                if (evaluation.Score < 0.45)
                {
                    rejectedForItem.Add(normalized with { RejectionReason = "low_relevance", SourceRelevanceStatus = "rejected_low_relevance" });
                    continue;
                }

                acceptedForItem.Add(normalized);
                accepted.Add(normalized);
                existingUrls.Add(normalized.Url);
                if (!string.IsNullOrWhiteSpace(publisherGroup))
                {
                    itemGroupSet.Add(publisherGroup);
                }
            }

            var sourceAfter = sourceBefore;
            if (apply)
            {
                sourceAfter = sourceBefore + acceptedForItem.Count;
            }

            var independentPublishers = itemGroupSet.Count;
            var coverage = CoverageFromPublisherCount(independentPublishers);
            coverageByItem[item.Id] = coverage;
            if (coverage > 0)
            {
                // retain per-item latest coverage for trace
            }

            traces.Add(new MultiSourceAcquisitionItemTrace(
                KnowledgeItemId: item.Id,
                Title: item.Title,
                Domain: item.Domain,
                SourceCountBefore: sourceBefore,
                SourceCountAfter: sourceAfter,
                PublisherGroupsBefore: existingGroups.TryGetValue(item.Id, out var beforeGroups) ? beforeGroups.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList() : [],
                PublisherGroupsAfter: itemGroupSet.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                AcceptedSources: acceptedForItem.Count,
                RejectedSources: rejectedForItem.Count,
                DuplicatePublisherGroups: itemDuplicatePublisherGroups,
                CoveragePercent: coverage,
                Status: acceptedForItem.Count > 0 ? "sources_accepted" : rejectedForItem.Count > 0 ? "sources_rejected" : "no_sources_found",
                NextAction: acceptedForItem.Count > 0
                    ? "run_validation_evidence_then_validation_state_sync_then_knowledge_trust_promote"
                    : "expand_seed_catalog_or_review_research_queries",
                Warnings: rejectedForItem.Count > 0 ? rejectedForItem.Select(candidate => candidate.RejectionReason ?? candidate.SourceRelevanceStatus ?? "rejected").Distinct(StringComparer.OrdinalIgnoreCase).ToList() : [],
                QueryTerms: queryTerms,
                MatchedSeedIds: matchedSeedIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

            rejected.AddRange(rejectedForItem);
        }

        if (apply && accepted.Count > 0)
        {
            var merged = existingImportCandidates
                .Concat(accepted)
                .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
            File.WriteAllText(ImportCandidatesPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));

            var importReport = new WebResearchSourceImportService(_storagePaths).Run(apply: true);
            var matcherReport = new KnowledgeEvidenceSemanticMatcherService(_storagePaths).Run(apply: true);
            var resolverReport = new IndependentSourceResolverService(_storagePaths).Run(apply: true);
            var policyReport = new AutoSourceReviewPolicyService(_storagePaths, _runtimeRoot).Run(apply: true);

            warnings.AddRange(importReport.Warnings);
            warnings.AddRange(matcherReport.Warnings);
            warnings.AddRange(resolverReport.Warnings);
            warnings.AddRange(policyReport.Warnings);
            policyApprovedSources = policyReport.AutoApprovedCandidates;
        }
        else if (apply)
        {
            warnings.Add("no_sources_accepted");
        }

        var refreshedConfirmations = apply ? new SourceConfirmationEngine(_storagePaths).LoadOrBuild() : confirmations;
        var refreshedSourceByKnowledge = refreshedConfirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var sourceCountsAfter = refreshedConfirmations.Results.ToDictionary(
            result => result.KnowledgeId,
            result => result.SourceCount,
            StringComparer.OrdinalIgnoreCase);

        var finalTraces = traces
            .Select(trace =>
            {
                var after = sourceCountsAfter.TryGetValue(trace.KnowledgeItemId, out var count) ? count : trace.SourceCountAfter;
                var refreshedGroups = refreshedSourceByKnowledge.TryGetValue(trace.KnowledgeItemId, out var refreshed)
                    ? GetPublisherGroups(refreshed)
                    : trace.PublisherGroupsAfter;

                return trace with
                {
                    SourceCountAfter = after,
                    PublisherGroupsAfter = refreshedGroups,
                    CoveragePercent = CoverageFromPublisherCount(refreshedGroups.Distinct(StringComparer.OrdinalIgnoreCase).Count()),
                    Status = after > trace.SourceCountBefore ? "sources_increased" : trace.Status,
                    NextAction = after >= 2 ? "run_validation_evidence_then_validation_state_sync_then_knowledge_trust_promote" : trace.NextAction
                };
            })
            .ToList();

        sourceCountIncreasedItems = finalTraces.Count(trace => trace.SourceCountAfter > trace.SourceCountBefore);

        var independentPublishersFound = finalTraces.Sum(trace => trace.PublisherGroupsAfter.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var publisherGroupsFound = finalTraces.Sum(trace => trace.PublisherGroupsBefore.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var acceptedSources = accepted.Count;
        var rejectedSources = rejected.Count;
        var duplicatePublisherGroups = finalTraces.Sum(trace => trace.DuplicatePublisherGroups);
        var status = apply
            ? acceptedSources > 0
                ? "applied"
                : "no_sources_accepted"
            : "dry_run_ready";

        var report = new MultiSourceAcquisitionReport(
            ReportVersion: "multi_source_acquisition_v1",
            UpdatedAtUtc: now,
            Status: status,
            LoadedItems: knowledgeItems.Count,
            ConsideredItems: prioritizedItems.Count,
            PublisherGroupsFound: publisherGroupsFound,
            IndependentPublishersFound: independentPublishersFound,
            AcceptedSources: acceptedSources,
            RejectedSources: rejectedSources,
            DuplicatePublisherGroups: duplicatePublisherGroups,
            PolicyApprovedSources: policyApprovedSources,
            SourceCountIncreasedItems: sourceCountIncreasedItems,
            CoverageByItem: coverageByItem,
            PerItemTrace: finalTraces,
            NextActions: BuildNextActions(finalTraces),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceConfirmationsPath: new SourceConfirmationEngine(_storagePaths).ReportPath,
            KnownArticleSeedCatalogPath: catalogService.ConfigPath,
            TrustedSourceCatalogPath: new TrustedSourceCatalogService(_storagePaths, _runtimeRoot).ConfigPath,
            ImportCandidatesPath: ImportCandidatesPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: dryRun,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);

        WriteReport(report);
        return report;
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        if (!File.Exists(ImportCandidatesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(File.ReadAllText(ImportCandidatesPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private Dictionary<string, HashSet<string>> BuildExistingGroups(SourceConfirmationReport confirmations)
    {
        var groups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in confirmations.Results)
        {
            if (!groups.TryGetValue(result.KnowledgeId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                groups[result.KnowledgeId] = set;
            }

            foreach (var candidate in result.CandidateSources ?? [])
            {
                var group = _publisherGroups.Resolve(candidate.Domain);
                if (!string.IsNullOrWhiteSpace(group))
                {
                    set.Add(group);
                }
            }
        }

        return groups;
    }

    private IReadOnlyList<string> GetPublisherGroups(ConfirmationResult result)
    {
        return (result.CandidateSources ?? [])
            .Select(candidate => _publisherGroups.Resolve(candidate.Domain))
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> BuildQueryTerms(KnowledgeCatalogItem item)
    {
        var title = item.Title ?? string.Empty;
        var id = item.Id.Replace("trading:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace('_', ' ');
        var baseTerms = new[]
        {
            title,
            id,
            $"{title} trading strategy",
            $"{title} forex",
            $"{title} definition",
            $"{title} examples",
            $"{title} price action"
        };

        return baseTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double CoverageFromPublisherCount(int independentPublishers) => independentPublishers switch
    {
        <= 0 => 0,
        1 => 0.40,
        2 => 0.70,
        _ => 1.0
    };

    private WebResearchImportCandidateRecord? FetchSeed(KnownArticleSeedDefinition seed, KnowledgeCatalogItem item, IReadOnlyList<string> queryTerms)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, seed.Url);
            httpRequest.Headers.TryAddWithoutValidation("User-Agent", "HermesRuntime/1.0");
            httpRequest.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            using var response = _httpClient.Send(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) && !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var title = ExtractTitle(html) ?? seed.Title;
            var snippet = ExtractSnippet(html);
            return new WebResearchImportCandidateRecord(
                KnowledgeItemId: item.Id,
                Title: title,
                Url: seed.Url,
                Domain: seed.Domain,
                SourceType: "known_article_seed_candidate",
                ExcerptOrSummary: snippet,
                RetrievedAtUtc: DateTimeOffset.UtcNow,
                EvidenceReason: $"multi_source_acquisition:{seed.SeedId}",
                IndependenceClaim: "different_publisher_group_seed",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"],
                RelevanceScore: 0,
                MatchedTerms: [],
                RejectionReason: null,
                SourceRelevanceStatus: null);
        }
        catch
        {
            return null;
        }
    }

    private static WebResearchImportCandidateRecord CreateRejectedCandidate(KnownArticleSeedDefinition seed, KnowledgeCatalogItem item, string reason, string status) =>
        new(
            KnowledgeItemId: item.Id,
            Title: seed.Title,
            Url: seed.Url,
            Domain: seed.Domain,
            SourceType: "known_article_seed_candidate",
            ExcerptOrSummary: string.Empty,
            RetrievedAtUtc: DateTimeOffset.UtcNow,
            EvidenceReason: reason,
            IndependenceClaim: "different_publisher_group_seed",
            HumanReviewStatus: "pending",
            SafetyFlags: ["no_trading_execution", "human_review_required"],
            RelevanceScore: 0,
            MatchedTerms: [],
            RejectionReason: reason,
            SourceRelevanceStatus: status);

    private static WebResearchImportCandidateRecord BuildPlannedCandidate(KnownArticleSeedDefinition seed, KnowledgeCatalogItem item, IReadOnlyList<string> queryTerms, string publisherGroup) =>
        new(
            KnowledgeItemId: item.Id,
            Title: seed.Title,
            Url: seed.Url,
            Domain: seed.Domain,
            SourceType: "known_article_seed_candidate",
            ExcerptOrSummary: string.Empty,
            RetrievedAtUtc: DateTimeOffset.UtcNow,
            EvidenceReason: $"planned_multi_source:{seed.SeedId}",
            IndependenceClaim: publisherGroup,
            HumanReviewStatus: "pending",
            SafetyFlags: ["no_trading_execution", "human_review_required"],
            RelevanceScore: 0,
            MatchedTerms: queryTerms.Where(term => !string.IsNullOrWhiteSpace(term)).Take(3).ToList());

    private static (double Score, IReadOnlyList<string> MatchedTerms, string Status, string? RejectionReason) ScoreCandidate(KnownArticleSeedDefinition seed, KnowledgeCatalogItem item, WebResearchImportCandidateRecord candidate)
    {
        var matched = new List<string>();
        var score = 0d;
        var title = Normalize(candidate.Title);
        var url = Normalize(candidate.Url);
        var excerpt = Normalize(candidate.ExcerptOrSummary);
        var domain = Normalize(candidate.Domain);
        var terms = seed.Keywords.Concat(seed.Synonyms).Concat([item.Title, item.Id.Replace("trading:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace('_', ' ')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var term in terms)
        {
            var normalized = Normalize(term);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (title.Contains(normalized, StringComparison.OrdinalIgnoreCase)) { score += 0.4; matched.Add(term); }
            if (url.Contains(normalized, StringComparison.OrdinalIgnoreCase)) { score += 0.2; matched.Add(term); }
            if (excerpt.Contains(normalized, StringComparison.OrdinalIgnoreCase)) { score += 0.25; matched.Add(term); }
            if (domain.Contains(normalized, StringComparison.OrdinalIgnoreCase)) { score += 0.05; }
        }

        if (candidate.Domain.Contains("babypips", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("investopedia", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("ig.com", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("cmcmarkets.com", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("avatrade.com", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("fxcm.com", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("trading.de", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15;
        }

        score = Math.Min(1, score);
        var status = score >= 0.45 ? "candidate_ready_for_import" : "candidate_rejected_low_relevance";
        var reason = status == "candidate_rejected_low_relevance" ? "low_relevance" : null;
        return (score, matched.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), status, reason);
    }

    private static IReadOnlyList<string> BuildNextActions(IReadOnlyList<MultiSourceAcquisitionItemTrace> traces)
    {
        var actions = new List<string>();
        if (traces.Any(trace => trace.AcceptedSources > 0))
        {
            actions.Add("validation-evidence --apply");
            actions.Add("validation-state-sync --apply");
            actions.Add("knowledge-trust-promote --apply");
            actions.Add("master-status");
        }
        else
        {
            actions.Add("expand_seed_catalog");
            actions.Add("review_trading_terms_and_publisher_groups");
        }

        return actions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, "<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            return CleanText(System.Net.WebUtility.HtmlDecode(match.Groups["title"].Value));
        }

        return string.Empty;
    }

    private static string ExtractSnippet(string html)
    {
        var meta = Regex.Match(html, "<meta[^>]+name=[\"']description[\"'][^>]+content=[\"'](?<content>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (meta.Success)
        {
            return CleanText(System.Net.WebUtility.HtmlDecode(meta.Groups["content"].Value));
        }

        var plain = CleanText(Regex.Replace(html, "<[^>]+>", " "));
        return plain.Length <= 240 ? plain : plain[..240];
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value, "\\s+", " ").Trim();
    }

    private static string Normalize(string value)
    {
        return CleanText(value).ToLowerInvariant();
    }

    private static string NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string ExtractHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri)) return uri.Host;
        return input;
    }

    private static void WriteReport(MultiSourceAcquisitionReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(MultiSourceAcquisitionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Multi Source Acquisition Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Items: {report.LoadedItems}");
        sb.AppendLine($"- Considered Items: {report.ConsideredItems}");
        sb.AppendLine($"- Publisher Groups Found: {report.PublisherGroupsFound}");
        sb.AppendLine($"- Independent Publishers Found: {report.IndependentPublishersFound}");
        sb.AppendLine($"- Accepted Sources: {report.AcceptedSources}");
        sb.AppendLine($"- Rejected Sources: {report.RejectedSources}");
        sb.AppendLine($"- Duplicate Publisher Groups: {report.DuplicatePublisherGroups}");
        sb.AppendLine($"- Policy Approved Sources: {report.PolicyApprovedSources}");
        sb.AppendLine($"- Source Count Increased Items: {report.SourceCountIncreasedItems}");
        sb.AppendLine();
        sb.AppendLine("## Coverage by Item");
        foreach (var pair in report.CoverageByItem.OrderByDescending(pair => pair.Value).Take(100))
        {
            sb.AppendLine($"- {pair.Key}: {pair.Value:0.##}%");
        }
        sb.AppendLine();
        sb.AppendLine("## Per Item Trace");
        foreach (var trace in report.PerItemTrace.Take(100))
        {
            sb.AppendLine($"- {trace.KnowledgeItemId} | before={trace.SourceCountBefore} | after={trace.SourceCountAfter} | accepted={trace.AcceptedSources} | rejected={trace.RejectedSources} | coverage={trace.CoveragePercent:0.##}% | {trace.Status}");
            sb.AppendLine($"  - Groups Before: {(trace.PublisherGroupsBefore.Count == 0 ? "-" : string.Join(", ", trace.PublisherGroupsBefore))}");
            sb.AppendLine($"  - Groups After: {(trace.PublisherGroupsAfter.Count == 0 ? "-" : string.Join(", ", trace.PublisherGroupsAfter))}");
            sb.AppendLine($"  - Query Terms: {(trace.QueryTerms.Count == 0 ? "-" : string.Join(", ", trace.QueryTerms))}");
            sb.AppendLine($"  - Matched Seeds: {(trace.MatchedSeedIds.Count == 0 ? "-" : string.Join(", ", trace.MatchedSeedIds))}");
        }
        sb.AppendLine();
        sb.AppendLine("## Next Actions");
        foreach (var action in report.NextActions)
        {
            sb.AppendLine($"- {action}");
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
