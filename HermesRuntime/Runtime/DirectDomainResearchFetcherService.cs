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
    DateTimeOffset RetrievedAtUtc);

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
    string? OpenedUrl);

public sealed record DirectDomainResearchReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedRequests,
    int ConsideredRequests,
    int FetchedPages,
    int ExtractedCandidates,
    int BlockedDomains,
    IReadOnlyList<DirectDomainResearchRequestResult> RequestResults,
    IReadOnlyList<DirectDomainResearchCandidate> Candidates,
    IReadOnlyList<DirectDomainResearchCandidate> Rejected,
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
        var requests = load.Requests.Take(Math.Max(0, maxItems)).ToList();
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
                BlockedDomains: 0,
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
                    OpenedUrl: null)).ToList(),
                Candidates: [],
                Rejected: [],
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
            BlockedDomains: blockedDomains,
            RequestResults: requestResults,
            Candidates: candidates,
            Rejected: rejected,
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
            return new DirectDomainResearchReport(
                ReportVersion: "direct_domain_research_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "status_unavailable",
                LoadedRequests: LoadRequestsEnvelope().LoadedRequests,
                ConsideredRequests: 0,
                FetchedPages: 0,
                ExtractedCandidates: 0,
                BlockedDomains: 0,
                RequestResults: [],
                Candidates: [],
                Rejected: [],
                Warnings: ["direct_domain_report_missing"],
                RequestsPath: RequestsPath,
                CandidateOutputPath: CandidateOutputPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true,
                ResearchOnly: true);
        }

        try
        {
            return JsonSerializer.Deserialize<DirectDomainResearchReport>(File.ReadAllText(reportPath), JsonDefaults.SnapshotReadOptions)
                ?? throw new InvalidOperationException("direct_domain_report_empty");
        }
        catch
        {
            return new DirectDomainResearchReport(
                ReportVersion: "direct_domain_research_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "status_unavailable",
                LoadedRequests: LoadRequestsEnvelope().LoadedRequests,
                ConsideredRequests: 0,
                FetchedPages: 0,
                ExtractedCandidates: 0,
                BlockedDomains: 0,
                RequestResults: [],
                Candidates: [],
                Rejected: [],
                Warnings: ["direct_domain_report_unreadable"],
                RequestsPath: RequestsPath,
                CandidateOutputPath: CandidateOutputPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true,
                ResearchOnly: true);
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
        var candidateDomains = CandidateDomainsForRequest(request);
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
                    OpenedUrl: null),
                []);
        }

        var requestResults = new List<DirectDomainResearchCandidate>();
        var fetchedPages = 0;
        var openedUrl = (string?)null;
        var status = "no_results";
        foreach (var domain in candidateDomains)
        {
            foreach (var url in BuildCandidateUrls(request.Query, domain).Take(2))
            {
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
                var extracted = ExtractCandidatesFromHtml(html, request, domain);
                requestResults.AddRange(extracted);
                if (extracted.Count > 0)
                {
                    status = "candidates_extracted";
                    break;
                }
            }

            if (requestResults.Count > 0)
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
                request.Query,
                request.Domain,
                Status: status,
                SkippedReason: status == "no_results" ? "no_html_results" : string.Empty,
                FetchedPages: fetchedPages,
                ExtractedCandidates: requestResults.Count,
                CandidateUrls: requestResults.Select(candidate => candidate.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                OpenedUrl: openedUrl),
            requestResults);
    }

    private IReadOnlyList<string> CandidateDomainsForRequest(WebResearchSourceRequest request)
    {
        var domain = request.Domain.Trim().ToLowerInvariant();
        var query = request.Query.ToLowerInvariant();
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

    private IReadOnlyList<DirectDomainResearchCandidate> ExtractCandidatesFromHtml(string html, WebResearchSourceRequest request, string domain)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var pageLinks = Regex.Matches(html, "<a[^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var candidates = new List<DirectDomainResearchCandidate>();
        var now = DateTimeOffset.UtcNow;
        var allowed = request.RecommendedSourceDomains.Any()
            ? request.RecommendedSourceDomains.Select(NormalizeDomain).ToHashSet(StringComparer.OrdinalIgnoreCase)
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
            candidates.Add(new DirectDomainResearchCandidate(
                KnowledgeItemId: request.KnowledgeItemId,
                Title: text,
                Url: href,
                Domain: actualDomain,
                Snippet: snippet,
                SourceType: "direct_domain_research_candidate",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"],
                RetrievedAtUtc: now));
            if (candidates.Count >= 5)
            {
                break;
            }
        }

        return candidates;
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
            SafetyFlags: candidate.SafetyFlags);

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
            SafetyFlags: candidate.SafetyFlags);

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
        sb.AppendLine($"- Blocked Domains: {report.BlockedDomains}");
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
            sb.AppendLine($"- {result.KnowledgeItemId} | {result.Domain} | {result.Status} | {result.ExtractedCandidates} candidates");
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
        IReadOnlyList<DirectDomainResearchCandidate> Candidates);
}
