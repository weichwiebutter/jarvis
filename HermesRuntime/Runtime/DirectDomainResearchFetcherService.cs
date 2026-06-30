using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    double BestRelevanceScore = 0,
    int AcceptedRelevantCandidates = 0,
    int RejectedLowRelevanceCandidates = 0,
    IReadOnlyDictionary<string, int>? TopRejectionReasons = null);

public sealed record DirectDomainResearchReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedRequests,
    int ConsideredRequests,
    int FetchedPages,
    int ExtractedCandidates,
    int AcceptedRelevantCandidates,
    int CandidatesRejectedLowRelevance,
    int BlockedDomains,
    IReadOnlyList<string> GeneratedQueries,
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
    private readonly HttpClient _httpClient;

    public DirectDomainResearchFetcherService(StoragePaths storagePaths, HttpClient? httpClient = null)
    {
        _storagePaths = storagePaths;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
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

    public DirectDomainResearchReport Run(int maxItems, bool dryRun)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
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
        var acceptedRelevantCandidates = 0;
        var rejectedLowRelevanceCandidates = 0;
        var topRejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (dryRun)
        {
            var dryRunReport = new DirectDomainResearchReport(
                ReportVersion: "direct_domain_research_v1",
                UpdatedAtUtc: now,
                Status: "dry_run_request_ready",
                LoadedRequests: load.LoadedRequests,
                ConsideredRequests: considered,
                FetchedPages: 0,
                ExtractedCandidates: 0,
                AcceptedRelevantCandidates: 0,
                CandidatesRejectedLowRelevance: 0,
                BlockedDomains: 0,
                GeneratedQueries: [],
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
            var result = FetchForRequest(request);
            requestResults.Add(result.RequestResult);
            fetchedPages += result.RequestResult.FetchedPages;
            generatedQueries.AddRange(result.RequestResult.QueryTerms ?? []);
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

        var report = new DirectDomainResearchReport(
            ReportVersion: "direct_domain_research_v1",
            UpdatedAtUtc: now,
            Status: candidates.Count > 0 ? "candidates_extracted" : "no_candidates_extracted",
            LoadedRequests: load.LoadedRequests,
            ConsideredRequests: considered,
            FetchedPages: fetchedPages,
            ExtractedCandidates: candidates.Count,
            AcceptedRelevantCandidates: acceptedRelevantCandidates,
            CandidatesRejectedLowRelevance: rejectedLowRelevanceCandidates,
            BlockedDomains: blockedDomains,
            GeneratedQueries: generatedQueries.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RequestResults: requestResults,
            Candidates: candidates,
            Rejected: rejected,
            TopRejectionReasons: topRejectionReasons.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Warnings: warnings.Concat(candidates.Count == 0 ? ["no_direct_domain_candidates_extracted"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
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

    private DirectDomainResearchFetchResult FetchForRequest(WebResearchSourceRequest request)
    {
        var queryBuilder = new ResearchQueryBuilderService(_storagePaths);
        var queryPlan = queryBuilder.BuildForRequest(request);
        var candidateDomains = CandidateDomainsForRequest(request, queryPlan);
        if (candidateDomains.Count == 0)
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
        var maxPagesPerRequest = 4;
        foreach (var domain in candidateDomains)
        {
            foreach (var query in queryTerms)
            {
                foreach (var url in BuildCandidateUrls(query, domain).Take(2))
                {
                    if (fetchedPages >= maxPagesPerRequest)
                    {
                        break;
                    }

                    if (openedUrl is null)
                    {
                        openedUrl = url;
                    }

                    var html = FetchHtml(url, domain);
                    if (string.IsNullOrWhiteSpace(html))
                    {
                        continue;
                    }

                    fetchedPages++;
                    var extracted = ExtractCandidatesFromHtml(html, request, domain, queryPlan);
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
            status = "no_direct_domain_results";
        }

        return new DirectDomainResearchFetchResult(
            new DirectDomainResearchRequestResult(
                request.RequestId,
                request.KnowledgeItemId,
                queryPlan.BaseTerm,
                request.Domain,
                Status: status,
                SkippedReason: status == "no_results" ? "no_html_results" : string.Empty,
                FetchedPages: fetchedPages,
                ExtractedCandidates: requestResults.Count,
                CandidateUrls: requestResults.Select(candidate => candidate.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                OpenedUrl: openedUrl,
                QueryTerms: queryPlan.QueryTerms,
                BestRelevanceScore: bestRelevanceScore,
                AcceptedRelevantCandidates: acceptedRelevantCandidates,
                RejectedLowRelevanceCandidates: rejectedLowRelevanceCandidates,
                TopRejectionReasons: topRejectionReasons),
            requestResults,
            rejectedLowRelevanceCandidates,
            topRejectionReasons,
            bestRelevanceScore);
    }

    private IReadOnlyList<string> CandidateDomainsForRequest(WebResearchSourceRequest request, ResearchQueryBuilderResult queryPlan)
    {
        var domain = request.Domain.Trim().ToLowerInvariant();
        var query = string.Join(' ', queryPlan.QueryTerms).ToLowerInvariant();
        var domains = new List<string>();

        if (domain.Contains("trading") || query.Contains("ctrader") || query.Contains("spotware"))
        {
            domains.AddRange(["spotware.com", "help.ctrader.com", "ctrader.com", "trading.de"]);
        }

        if (domain.Contains("documentation"))
        {
            domains.AddRange(["help.ctrader.com", "learn.microsoft.com", "docs.microsoft.com"]);
        }

        if (domain.Contains("software") || query.Contains("github"))
        {
            domains.AddRange(["github.com", "learn.microsoft.com"]);
        }

        if (domains.Count == 0)
        {
            domains.AddRange(request.RecommendedSourceDomains.Any() ? request.RecommendedSourceDomains : ["spotware.com", "help.ctrader.com", "ctrader.com"]);
        }

        return domains
            .Select(NormalizeDomain)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<string> BuildCandidateUrls(string query, string domain)
    {
        var encoded = Uri.EscapeDataString(query);
        return NormalizeDomain(domain).ToLowerInvariant() switch
        {
            "github.com" => [
                $"https://github.com/search?q={encoded}&type=repositories",
                $"https://github.com/search?q={encoded}&type=code"
            ],
            "learn.microsoft.com" => [
                $"https://learn.microsoft.com/en-us/search/?terms={encoded}",
                $"https://learn.microsoft.com/search/?terms={encoded}"
            ],
            "docs.microsoft.com" => [
                $"https://learn.microsoft.com/en-us/search/?terms={encoded}",
                $"https://docs.microsoft.com/en-us/search/?terms={encoded}"
            ],
            "help.ctrader.com" => [
                $"https://help.ctrader.com/search/?q={encoded}",
                $"https://help.ctrader.com/?s={encoded}"
            ],
            "ctrader.com" => [
                $"https://ctrader.com/search/?q={encoded}",
                $"https://www.ctrader.com/search/?q={encoded}"
            ],
            "spotware.com" => [
                $"https://www.spotware.com/search/?query={encoded}",
                $"https://spotware.com/search/?q={encoded}"
            ],
            "trading.de" => [
                $"https://trading.de/?s={encoded}",
                $"https://www.trading.de/?s={encoded}"
            ],
            _ => [$"https://{domain}/"]
        };
    }

    private string? FetchHtml(string url, string domain)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "HermesRuntime/1.0");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) && !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private CandidateExtractionResult ExtractCandidatesFromHtml(string html, WebResearchSourceRequest request, string domain, ResearchQueryBuilderResult queryPlan)
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
        var allowed = queryPlan.RecommendedSourceDomains.Any()
            ? queryPlan.RecommendedSourceDomains.Select(NormalizeDomain).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (relevance.RelevanceScore >= 0.30)
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
        sb.AppendLine($"- Loaded Requests: {report.LoadedRequests}");
        sb.AppendLine($"- Considered Requests: {report.ConsideredRequests}");
        sb.AppendLine($"- Fetched Pages: {report.FetchedPages}");
        sb.AppendLine($"- Extracted Candidates: {report.ExtractedCandidates}");
        sb.AppendLine($"- Accepted Relevant Candidates: {report.AcceptedRelevantCandidates}");
        sb.AppendLine($"- Candidates Rejected Low Relevance: {report.CandidatesRejectedLowRelevance}");
        sb.AppendLine($"- Blocked Domains: {report.BlockedDomains}");
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
