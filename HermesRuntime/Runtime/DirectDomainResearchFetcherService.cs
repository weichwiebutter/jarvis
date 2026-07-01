using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Hermes.Runtime;

public sealed record DirectDomainResearchCandidate(
    string KnowledgeItemId,
    string Title,
    string Url,
    string Domain,
    string Snippet,
    string SourceType,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    DateTimeOffset RetrievedAtUtc,
    double RelevanceScore = 0,
    IReadOnlyList<string>? MatchedTerms = null,
    string? RejectionReason = null,
    string? SourceRelevanceStatus = null);

public sealed record DirectDomainResearchRequestResult(
    string RequestId,
    string KnowledgeItemId,
    string Query,
    string Domain,
    string Status,
    string SkippedReason,
    int FetchedPages,
    int ExtractedCandidates,
    IReadOnlyList<string> CandidateUrls,
    string? OpenedUrl,
    IReadOnlyList<string>? QueryTerms = null,
    IReadOnlyList<string>? CatalogSourcesUsed = null,
    double BestRelevanceScore = 0,
    int AcceptedRelevantCandidates = 0,
    int RejectedLowRelevanceCandidates = 0,
    IReadOnlyDictionary<string, int>? TopRejectionReasons = null);

public sealed record DirectDomainResearchReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ExternalFetchTimeouts,
    int SkippedDueToTimeout,
    long FetchDurationMs,
    string LastSuccessfulStage,
    int LoadedRequests,
    int ConsideredRequests,
    int FetchedPages,
    int ExtractedCandidates,
    int AcceptedRelevantCandidates,
    int CandidatesRejectedLowRelevance,
    int BlockedDomains,
    IReadOnlyList<string> GeneratedQueries,
    IReadOnlyList<string> CatalogSourcesUsed,
    IReadOnlyList<DirectDomainResearchRequestResult> RequestResults,
    IReadOnlyList<DirectDomainResearchCandidate> Candidates,
    IReadOnlyList<DirectDomainResearchCandidate> Rejected,
    IReadOnlyDictionary<string, int> TopRejectionReasons,
    IReadOnlyList<string> Warnings,
    string RequestsPath,
    string CandidateOutputPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class DirectDomainResearchFetcherService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private readonly HttpClient _httpClient;

    public DirectDomainResearchFetcherService(StoragePaths storagePaths, string? runtimeRoot = null, HttpClient? httpClient = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HermesRuntime/1.0");
        }
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "direct_domain_research");

    public string RequestsPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_requests.json");

    public string CandidateOutputPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");

    public string ReportPath => Path.Combine(Root, "direct_domain_research_report.json");

    public string MarkdownPath => Path.Combine(Root, "direct_domain_research_report.md");

    public DirectDomainResearchReport Run(int maxItems, bool dryRun, int maxFetchSeconds = 120)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var fetchWatch = Stopwatch.StartNew();
        var catalogService = new TrustedSourceCatalogService(_storagePaths, _runtimeRoot);
        var load = LoadRequestsEnvelope();
        var requests = load.Requests
            .OrderByDescending(RequestPriorityScore)
            .ThenBy(request => request.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(request => request.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxItems))
            .ToList();
        var considered = requests.Count;
        var existingCandidates = LoadImportCandidates();
        var importedUrls = existingCandidates
            .Select(candidate => candidate.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<DirectDomainResearchCandidate>();
        var rejected = new List<DirectDomainResearchCandidate>();
        var requestResults = new List<DirectDomainResearchRequestResult>();
        var warnings = load.Warnings.ToList();
        var fetchedPages = 0;
        var blockedDomains = 0;
        var generatedQueries = new List<string>();
        var catalogSourcesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acceptedRelevantCandidates = 0;
        var rejectedLowRelevanceCandidates = 0;
        var topRejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var runWatch = Stopwatch.StartNew();

        if (dryRun)
        {
            var dryRunReport = new DirectDomainResearchReport(
                ReportVersion: "direct_domain_research_v1",
                UpdatedAtUtc: now,
                Status: "dry_run_request_ready",
                ExternalFetchTimeouts: 0,
                SkippedDueToTimeout: 0,
                FetchDurationMs: 0,
                LastSuccessfulStage: "dry_run_ready",
                LoadedRequests: load.LoadedRequests,
                ConsideredRequests: considered,
                FetchedPages: 0,
                ExtractedCandidates: 0,
                AcceptedRelevantCandidates: 0,
                CandidatesRejectedLowRelevance: 0,
                BlockedDomains: 0,
                GeneratedQueries: [],
                CatalogSourcesUsed: [],
                RequestResults: requests.Select(request => new DirectDomainResearchRequestResult(
                    request.RequestId,
                    request.KnowledgeItemId,
                    request.Query,
                    request.Domain,
                    Status: "dry_run_request_ready",
                    SkippedReason: "dry_run_no_network",
                    FetchedPages: 0,
                    ExtractedCandidates: 0,
                    CandidateUrls: [],
                    OpenedUrl: null,
                    QueryTerms: [],
                    CatalogSourcesUsed: [],
                    BestRelevanceScore: 0,
                    AcceptedRelevantCandidates: 0,
                    RejectedLowRelevanceCandidates: 0,
                    TopRejectionReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))).ToList(),
                Candidates: [],
                Rejected: [],
                TopRejectionReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Warnings: warnings.Concat(["dry_run_no_network"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequestsPath: RequestsPath,
                CandidateOutputPath: CandidateOutputPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true,
                ResearchOnly: true);
            WriteReport(dryRunReport);
            return dryRunReport;
        }

        foreach (var request in requests)
        {
            var remainingBudget = TimeSpan.FromSeconds(Math.Max(5, maxFetchSeconds)) - runWatch.Elapsed;
            if (remainingBudget <= TimeSpan.Zero)
            {
                requestResults.Add(new DirectDomainResearchRequestResult(
                    request.RequestId,
                    request.KnowledgeItemId,
                    request.Query,
                    request.Domain,
                    Status: "blocked_external_fetch_timeout",
                    SkippedReason: "blocked_external_fetch_timeout",
                    FetchedPages: 0,
                    ExtractedCandidates: 0,
                    CandidateUrls: [],
                    OpenedUrl: null,
                    QueryTerms: [],
                    CatalogSourcesUsed: [],
                    BestRelevanceScore: 0,
                    AcceptedRelevantCandidates: 0,
                    RejectedLowRelevanceCandidates: 0,
                    TopRejectionReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["blocked_external_fetch_timeout"] = 1
                    }));
                topRejectionReasons["blocked_external_fetch_timeout"] = topRejectionReasons.TryGetValue("blocked_external_fetch_timeout", out var currentTimeout) ? currentTimeout + 1 : 1;
                continue;
            }

            var result = FetchForRequest(request, catalogService, remainingBudget);
            requestResults.Add(result.RequestResult);
            fetchedPages += result.RequestResult.FetchedPages;
            generatedQueries.AddRange(result.RequestResult.QueryTerms ?? []);
            foreach (var source in result.RequestResult.CatalogSourcesUsed ?? [])
            {
                catalogSourcesUsed.Add(source);
            }
            acceptedRelevantCandidates += result.RequestResult.AcceptedRelevantCandidates;
            rejectedLowRelevanceCandidates += result.RequestResult.RejectedLowRelevanceCandidates;
            foreach (var pair in result.RequestResult.TopRejectionReasons ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
            {
                topRejectionReasons[pair.Key] = topRejectionReasons.TryGetValue(pair.Key, out var current) ? current + pair.Value : pair.Value;
            }
            if (result.RequestResult.Status.Equals("blocked_domain", StringComparison.OrdinalIgnoreCase))
            {
                blockedDomains++;
            }

            foreach (var candidate in result.Candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.Url) || importedUrls.Contains(candidate.Url))
                {
                    rejected.Add(candidate);
                    continue;
                }

                candidates.Add(candidate);
                importedUrls.Add(candidate.Url);
            }

            if (runWatch.Elapsed >= TimeSpan.FromSeconds(Math.Max(5, maxFetchSeconds)))
            {
                break;
            }
        }

        if (candidates.Count > 0)
        {
            var merged = existingCandidates
                .Concat(candidates.Select(ToImportCandidate))
                .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            File.WriteAllText(CandidateOutputPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));
        }

        var hadTimeout = requestResults.Any(result => result.Status.Equals("blocked_external_fetch_timeout", StringComparison.OrdinalIgnoreCase));
        var report = new DirectDomainResearchReport(
            ReportVersion: "direct_domain_research_v1",
            UpdatedAtUtc: now,
            Status: candidates.Count > 0 ? "candidates_extracted" : hadTimeout ? "blocked_external_fetch_timeout" : "no_candidates_extracted",
            ExternalFetchTimeouts: requestResults.Count(result => result.Status.Equals("blocked_external_fetch_timeout", StringComparison.OrdinalIgnoreCase)),
            SkippedDueToTimeout: requestResults.Count(result => result.Status.Equals("blocked_external_fetch_timeout", StringComparison.OrdinalIgnoreCase)),
            FetchDurationMs: fetchWatch.ElapsedMilliseconds,
            LastSuccessfulStage: candidates.Count > 0
                ? "direct_domain_candidates_extracted"
                : hadTimeout
                    ? "blocked_external_fetch_timeout"
                    : "direct_domain_no_candidates",
            LoadedRequests: load.LoadedRequests,
            ConsideredRequests: considered,
            FetchedPages: fetchedPages,
            ExtractedCandidates: candidates.Count,
            AcceptedRelevantCandidates: acceptedRelevantCandidates,
            CandidatesRejectedLowRelevance: rejectedLowRelevanceCandidates,
            BlockedDomains: blockedDomains,
            GeneratedQueries: generatedQueries.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CatalogSourcesUsed: catalogSourcesUsed.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            RequestResults: requestResults,
            Candidates: candidates,
            Rejected: rejected,
            TopRejectionReasons: topRejectionReasons.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Warnings: warnings
                .Concat(candidates.Count == 0 ? ["no_direct_domain_candidates_extracted"] : [])
                .Concat(hadTimeout ? ["blocked_external_fetch_timeout"] : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            RequestsPath: RequestsPath,
            CandidateOutputPath: CandidateOutputPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);
        WriteReport(report);
        return report;
    }

    public DirectDomainResearchReport LoadStatus()
    {
        var reportPath = ReportPath;
        if (!File.Exists(reportPath))
        {
            return Run(maxItems: 5, dryRun: true) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<DirectDomainResearchReport>(File.ReadAllText(reportPath), JsonDefaults.SnapshotReadOptions)
                ?? throw new InvalidOperationException("direct_domain_report_empty");
        }
        catch
        {
            return Run(maxItems: 5, dryRun: true) with { Status = "status_snapshot_generated" };
        }
    }

    private DirectDomainRequestEnvelope LoadRequestsEnvelope()
    {
        if (!File.Exists(RequestsPath))
        {
            return new DirectDomainRequestEnvelope([], 0, ["requests_file_missing"]);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<WebResearchRequestsEnvelope>(File.ReadAllText(RequestsPath), JsonDefaults.SnapshotReadOptions);
            if (envelope is null)
            {
                return new DirectDomainRequestEnvelope([], 0, ["requests_envelope_empty"]);
            }

            var requests = envelope.Requests
                .Where(request => request is not null)
                .Where(request => !string.IsNullOrWhiteSpace(request.Query))
                .Where(request => request.Status.Equals("awaiting_external_search", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var warnings = new List<string>();
            if (envelope.Requests.Count == 0)
            {
                warnings.Add("no_requests_in_export");
            }

            return new DirectDomainRequestEnvelope(requests, envelope.Requests.Count, warnings);
        }
        catch
        {
            return new DirectDomainRequestEnvelope([], 0, ["requests_deserialize_failed"]);
        }
    }

    private DirectDomainResearchFetchResult FetchForRequest(WebResearchSourceRequest request, TrustedSourceCatalogService catalogService, TimeSpan fetchBudget)
    {
        var queryBuilder = new ResearchQueryBuilderService(_storagePaths);
        var queryPlan = queryBuilder.BuildForRequest(request);
        var catalogEntries = catalogService.ResolveForRequest(request, queryPlan);
        if (catalogEntries.Count == 0)
        {
            return new DirectDomainResearchFetchResult(
                new DirectDomainResearchRequestResult(
                    request.RequestId,
                    request.KnowledgeItemId,
                    request.Query,
                    request.Domain,
                    Status: "blocked_domain",
                    SkippedReason: "no_candidate_domains",
                    FetchedPages: 0,
                    ExtractedCandidates: 0,
                    CandidateUrls: [],
                    OpenedUrl: null,
                    QueryTerms: queryPlan.QueryTerms,
                    CatalogSourcesUsed: [],
                    BestRelevanceScore: 0,
                    AcceptedRelevantCandidates: 0,
                    RejectedLowRelevanceCandidates: 0,
                    TopRejectionReasons: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
                [],
                0,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                0);
        }

        var requestResults = new List<DirectDomainResearchCandidate>();
        var fetchedPages = 0;
        var openedUrl = (string?)null;
        var status = "no_results";
        var acceptedRelevantCandidates = 0;
        var rejectedLowRelevanceCandidates = 0;
        var topRejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var bestRelevanceScore = 0d;
        var queryTerms = queryPlan.QueryTerms.Take(3).ToList();
        var maxPagesPerRequest = 3;
        var maxUrlAttemptsPerRequest = Math.Max(6, maxPagesPerRequest * 3);
        var catalogSourcesUsed = catalogEntries
            .Select(entry => $"{entry.Domain}|{entry.Category}|{entry.SourceType}|allowed={entry.Allowed}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var urlAttempts = 0;

        var fetchStart = Stopwatch.StartNew();
        foreach (var entry in catalogEntries)
        {
            if (fetchStart.Elapsed > fetchBudget)
            {
                status = "blocked_external_fetch_timeout";
                topRejectionReasons["blocked_external_fetch_timeout"] = topRejectionReasons.TryGetValue("blocked_external_fetch_timeout", out var currentTimeout) ? currentTimeout + 1 : 1;
                break;
            }

            if (urlAttempts >= maxUrlAttemptsPerRequest)
            {
                break;
            }

            foreach (var query in queryTerms)
            {
                if (urlAttempts >= maxUrlAttemptsPerRequest || fetchedPages >= maxPagesPerRequest)
                {
                    break;
                }

                foreach (var url in catalogService.BuildCandidateUrls(entry, query).Take(2))
                {
                    if (fetchStart.Elapsed > fetchBudget)
                    {
                        status = "blocked_external_fetch_timeout";
                        topRejectionReasons["blocked_external_fetch_timeout"] = topRejectionReasons.TryGetValue("blocked_external_fetch_timeout", out var currentTimeout) ? currentTimeout + 1 : 1;
                        break;
                    }

                    if (urlAttempts >= maxUrlAttemptsPerRequest)
                    {
                        break;
                    }

                    urlAttempts++;
                    if (fetchedPages >= maxPagesPerRequest)
                    {
                        break;
                    }

                    if (openedUrl is null)
                    {
                        openedUrl = url;
                    }

                    var html = FetchHtml(url, entry.Domain, fetchBudget, fetchStart);
                    if (string.IsNullOrWhiteSpace(html))
                    {
                        if (fetchStart.Elapsed > fetchBudget)
                        {
                            status = "blocked_external_fetch_timeout";
                            topRejectionReasons["blocked_external_fetch_timeout"] = topRejectionReasons.TryGetValue("blocked_external_fetch_timeout", out var currentTimeout) ? currentTimeout + 1 : 1;
                            break;
                        }

                        continue;
                    }

                    fetchedPages++;
                    var extracted = ExtractCandidatesFromHtml(html, request, entry.Domain, queryPlan, catalogEntries);
                    requestResults.AddRange(extracted.Accepted);
                    acceptedRelevantCandidates += extracted.Accepted.Count;
                    rejectedLowRelevanceCandidates += extracted.RejectedLowRelevance.Count;
                    bestRelevanceScore = Math.Max(bestRelevanceScore, extracted.BestRelevanceScore);
                    foreach (var pair in extracted.RejectionReasons)
                    {
                        topRejectionReasons[pair.Key] = topRejectionReasons.TryGetValue(pair.Key, out var current) ? current + pair.Value : pair.Value;
                    }
                    if (extracted.Accepted.Count > 0)
                    {
                        status = "candidates_extracted";
                        break;
                    }
                }

                if (requestResults.Count > 0 || fetchedPages >= maxPagesPerRequest)
                {
                    break;
                }
            }

            if (requestResults.Count > 0 || fetchedPages >= maxPagesPerRequest)
            {
                break;
            }
        }

        if (requestResults.Count == 0 && status == "no_results")
        {
            status = topRejectionReasons.ContainsKey("blocked_external_fetch_timeout")
                ? "blocked_external_fetch_timeout"
                : "no_direct_domain_results";
        }

        return new DirectDomainResearchFetchResult(
            new DirectDomainResearchRequestResult(
                request.RequestId,
                request.KnowledgeItemId,
                queryPlan.BaseTerm,
                request.Domain,
                Status: status,
                SkippedReason: status == "blocked_external_fetch_timeout"
                    ? "blocked_external_fetch_timeout"
                    : status == "no_results" ? "no_html_results" : string.Empty,
                FetchedPages: fetchedPages,
                ExtractedCandidates: requestResults.Count,
                CandidateUrls: requestResults.Select(candidate => candidate.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                OpenedUrl: openedUrl,
                QueryTerms: queryPlan.QueryTerms,
                CatalogSourcesUsed: catalogSourcesUsed,
                BestRelevanceScore: bestRelevanceScore,
                AcceptedRelevantCandidates: acceptedRelevantCandidates,
                RejectedLowRelevanceCandidates: rejectedLowRelevanceCandidates,
                TopRejectionReasons: topRejectionReasons),
            requestResults,
            rejectedLowRelevanceCandidates,
            topRejectionReasons,
            bestRelevanceScore);
    }

    private string? FetchHtml(string url, string domain, TimeSpan fetchBudget, Stopwatch fetchStart)
    {
        try
        {
            var remaining = fetchBudget - fetchStart.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "HermesRuntime/1.0");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            var sendTask = _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!sendTask.Wait(remaining))
            {
                return null;
            }

            using var response = sendTask.Result;
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) && !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            remaining = fetchBudget - fetchStart.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            var contentTask = response.Content.ReadAsStringAsync();
            if (!contentTask.Wait(remaining))
            {
                return null;
            }

            return contentTask.Result;
        }
        catch
        {
            return null;
        }
    }

    private CandidateExtractionResult ExtractCandidatesFromHtml(
        string html,
        WebResearchSourceRequest request,
        string domain,
        ResearchQueryBuilderResult queryPlan,
        IReadOnlyList<TrustedSourceCatalogEntry> catalogEntries)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new CandidateExtractionResult([], [], new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), 0);
        }

        var pageLinks = Regex.Matches(html, "<a[^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var accepted = new List<DirectDomainResearchCandidate>();
        var rejectedLowRelevance = new List<DirectDomainResearchCandidate>();
        var rejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var allowed = catalogEntries.Count > 0
            ? catalogEntries.Select(entry => NormalizeDomain(entry.Domain)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : queryPlan.RecommendedSourceDomains.Select(NormalizeDomain).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in pageLinks)
        {
            var href = System.Net.WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var actualDomain = NormalizeDomain(uri.Host);
            if (!DomainMatchesAny(actualDomain, allowed, request.Domain, domain))
            {
                continue;
            }

            var text = CleanText(System.Net.WebUtility.HtmlDecode(match.Groups["text"].Value));
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var snippet = ExtractSnippet(html, match.Index);
            var relevance = ResearchQueryBuilderService.ScoreCandidate(
                knowledgeTitle: queryPlan.KnowledgeTitle,
                requestDomain: request.Domain,
                requestKnowledgeItemId: request.KnowledgeItemId,
                candidateTitle: text,
                candidateUrl: href,
                candidateSnippet: snippet,
                candidateDomain: actualDomain,
                queryTerms: queryPlan.QueryTerms,
                recommendedDomains: queryPlan.RecommendedSourceDomains);

            var candidate = new DirectDomainResearchCandidate(
                KnowledgeItemId: request.KnowledgeItemId,
                Title: text,
                Url: href,
                Domain: actualDomain,
                Snippet: snippet,
                SourceType: "direct_domain_research_candidate",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"],
                RetrievedAtUtc: now,
                RelevanceScore: relevance.RelevanceScore,
                MatchedTerms: relevance.MatchedTerms,
                RejectionReason: relevance.RejectionReason,
                SourceRelevanceStatus: relevance.SourceRelevanceStatus);

            if (relevance.RelevanceScore >= 0.15)
            {
                accepted.Add(candidate);
            }
            else
            {
                rejectedLowRelevance.Add(candidate);
                if (!string.IsNullOrWhiteSpace(relevance.RejectionReason))
                {
                    rejectionReasons[relevance.RejectionReason] = rejectionReasons.TryGetValue(relevance.RejectionReason, out var existing) ? existing + 1 : 1;
                }
            }

            if (accepted.Count >= 5)
            {
                break;
            }
        }

        return new CandidateExtractionResult(accepted, rejectedLowRelevance, rejectionReasons, accepted.Count == 0 ? 0 : accepted.Max(candidate => candidate.RelevanceScore));
    }

    private static bool DomainMatchesAny(string actualDomain, HashSet<string> allowed, string requestDomain, string candidateDomain)
    {
        if (string.IsNullOrWhiteSpace(actualDomain))
        {
            return false;
        }

        if (allowed.Count > 0 && allowed.Any(allowedDomain => actualDomain.Equals(allowedDomain, StringComparison.OrdinalIgnoreCase) || actualDomain.EndsWith($".{allowedDomain}", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var normalizedRequestDomain = NormalizeDomain(requestDomain);
        var normalizedCandidateDomain = NormalizeDomain(candidateDomain);
        return actualDomain.Equals(normalizedRequestDomain, StringComparison.OrdinalIgnoreCase)
            || actualDomain.Equals(normalizedCandidateDomain, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDomain(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, "<[^>]+>", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return cleaned;
    }

    private static string ExtractSnippet(string html, int index)
    {
        var start = Math.Max(0, index - 200);
        var length = Math.Min(500, html.Length - start);
        if (length <= 0)
        {
            return string.Empty;
        }

        return CleanText(System.Net.WebUtility.HtmlDecode(html.Substring(start, length)));
    }

    private static int RequestPriorityScore(WebResearchSourceRequest request)
    {
        var text = $"{request.Domain} {request.Query} {request.Reason}".ToLowerInvariant();
        var score = 0;

        if (text.Contains("trading") || text.Contains("ctrader") || text.Contains("spotware"))
        {
            score += 100;
        }

        if (text.Contains("breakout") || text.Contains("inside bar") || text.Contains("liquidity") || text.Contains("engulfing") || text.Contains("sweep"))
        {
            score += 50;
        }

        if (text.Contains("strategy") || text.Contains("setup") || text.Contains("examples"))
        {
            score += 20;
        }

        if (request.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }
        else if (request.Domain.Equals("software", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }
        else if (request.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        if (!File.Exists(CandidateOutputPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(File.ReadAllText(CandidateOutputPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static WebResearchImportCandidateRecord ToImportCandidate(DirectDomainResearchCandidate candidate) =>
        new(
            KnowledgeItemId: candidate.KnowledgeItemId,
            Title: candidate.Title,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: candidate.SourceType,
            ExcerptOrSummary: candidate.Snippet,
            RetrievedAtUtc: candidate.RetrievedAtUtc,
            EvidenceReason: candidate.Snippet,
            IndependenceClaim: "direct_domain_research_candidate",
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags,
            RelevanceScore: candidate.RelevanceScore,
            MatchedTerms: candidate.MatchedTerms,
            RejectionReason: candidate.RejectionReason,
            SourceRelevanceStatus: candidate.SourceRelevanceStatus);

    private static WebResearchImportCandidateRecord ToRejectedCandidate(DirectDomainResearchCandidate candidate) =>
        new(
            KnowledgeItemId: candidate.KnowledgeItemId,
            Title: candidate.Title,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: candidate.SourceType,
            ExcerptOrSummary: candidate.Snippet,
            RetrievedAtUtc: candidate.RetrievedAtUtc,
            EvidenceReason: candidate.Snippet,
            IndependenceClaim: "direct_domain_research_candidate",
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags,
            RelevanceScore: candidate.RelevanceScore,
            MatchedTerms: candidate.MatchedTerms,
            RejectionReason: candidate.RejectionReason,
            SourceRelevanceStatus: candidate.SourceRelevanceStatus);

    private static void WriteReport(DirectDomainResearchReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(DirectDomainResearchReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Direct Domain Research Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- External Fetch Timeouts: {report.ExternalFetchTimeouts}");
        sb.AppendLine($"- Skipped Due To Timeout: {report.SkippedDueToTimeout}");
        sb.AppendLine($"- Fetch Duration Ms: {report.FetchDurationMs}");
        sb.AppendLine($"- Last Successful Stage: {report.LastSuccessfulStage}");
        sb.AppendLine($"- Loaded Requests: {report.LoadedRequests}");
        sb.AppendLine($"- Considered Requests: {report.ConsideredRequests}");
        sb.AppendLine($"- Fetched Pages: {report.FetchedPages}");
        sb.AppendLine($"- Extracted Candidates: {report.ExtractedCandidates}");
        sb.AppendLine($"- Accepted Relevant Candidates: {report.AcceptedRelevantCandidates}");
        sb.AppendLine($"- Candidates Rejected Low Relevance: {report.CandidatesRejectedLowRelevance}");
        sb.AppendLine($"- Blocked Domains: {report.BlockedDomains}");
        var catalogSourcesUsed = report.CatalogSourcesUsed ?? [];
        if (catalogSourcesUsed.Count > 0)
        {
            sb.AppendLine($"- Catalog Sources Used: {catalogSourcesUsed.Count}");
        }
        if (report.GeneratedQueries.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Generated Queries");
            foreach (var query in report.GeneratedQueries.Take(20))
            {
                sb.AppendLine($"- {query}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        sb.AppendLine($"- research_only: {report.ResearchOnly}");
        sb.AppendLine();
        sb.AppendLine("## Request Results");
        foreach (var result in report.RequestResults.Take(20))
        {
            sb.AppendLine($"- {result.KnowledgeItemId} | {result.Domain} | {result.Status} | {result.ExtractedCandidates} candidates | best={result.BestRelevanceScore:0.###}");
        }
        if (report.TopRejectionReasons.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Top Rejection Reasons");
            foreach (var item in report.TopRejectionReasons.Take(10))
            {
                sb.AppendLine($"- {item.Key}: {item.Value}");
            }
        }
        if (catalogSourcesUsed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Catalog Sources Used");
            foreach (var item in catalogSourcesUsed.Take(20))
            {
                sb.AppendLine($"- {item}");
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

    private sealed record WebResearchRequestsEnvelope(IReadOnlyList<WebResearchSourceRequest> Requests);
    private sealed record DirectDomainRequestEnvelope(IReadOnlyList<WebResearchSourceRequest> Requests, int LoadedRequests, IReadOnlyList<string> Warnings);
    private sealed record DirectDomainResearchFetchResult(
        DirectDomainResearchRequestResult RequestResult,
        IReadOnlyList<DirectDomainResearchCandidate> Candidates,
        int RejectedLowRelevanceCandidates,
        IReadOnlyDictionary<string, int> RejectionReasons,
        double BestRelevanceScore);
    private sealed record CandidateExtractionResult(
        IReadOnlyList<DirectDomainResearchCandidate> Accepted,
        IReadOnlyList<DirectDomainResearchCandidate> RejectedLowRelevance,
        IReadOnlyDictionary<string, int> RejectionReasons,
        double BestRelevanceScore);
}
