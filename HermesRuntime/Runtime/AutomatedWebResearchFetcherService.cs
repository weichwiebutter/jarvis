using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WebSearchConnectorConfiguration(
    string Provider,
    string? Endpoint,
    string? ApiKey,
    int MaxResults,
    IReadOnlyList<string> AllowedDomains,
    IReadOnlyList<string> MissingVariables);

public sealed record WebResearchFetcherConnectorStatus(
    bool HasConnector,
    string Status,
    string? ConnectorType,
    string? Endpoint,
    string Provider,
    int MaxResults,
    IReadOnlyList<string> AllowedDomains,
    IReadOnlyList<string> ApiKeysDetected,
    IReadOnlyList<string> MissingVariables,
    IReadOnlyList<string> Warnings,
    string Recommendation);

public sealed record AutomatedWebResearchFetchRequest(
    string RequestId,
    string KnowledgeItemId,
    string Domain,
    string Query,
    IReadOnlyList<string> RecommendedSourceDomains,
    string Reason,
    int CurrentSourceCount,
    IReadOnlyList<string> RequiredEvidence,
    string Status,
    bool HumanReviewRequired,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> SafetyFlags);

public sealed record AutomatedWebResearchFetcherReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int TotalRequests,
    int ConsideredRequests,
    int FetchedCandidates,
    int BlockedRequests,
    int AwaitingHumanReview,
    int DuplicateCandidates,
    int SameDomainBlocked,
    IReadOnlyList<WebResearchImportCandidateRecord> Candidates,
    IReadOnlyList<WebResearchImportCandidateRecord> Rejected,
    IReadOnlyList<string> Warnings,
    string RequestsPath,
    string ImportCandidatesPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class AutomatedWebResearchFetcherService
{
    private readonly StoragePaths _storagePaths;
    private readonly HttpClient _httpClient;

    public AutomatedWebResearchFetcherService(StoragePaths storagePaths, HttpClient? httpClient = null)
    {
        _storagePaths = storagePaths;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector");

    public string RequestsPath => Path.Combine(Root, "web_research_requests.json");

    public string ImportCandidatesPath => Path.Combine(Root, "web_research_import_candidates.json");

    public string ReportPath => Path.Combine(Root, "automated_web_research_fetcher_report.json");

    public string MarkdownPath => Path.Combine(Root, "automated_web_research_fetcher_report.md");

    public WebSearchConnectorConfiguration LoadConnectorConfiguration()
    {
        var provider = (Environment.GetEnvironmentVariable("HERMES_WEB_SEARCH_PROVIDER") ?? "none").Trim();
        var endpoint = Normalize(Environment.GetEnvironmentVariable("HERMES_WEB_SEARCH_ENDPOINT"));
        var apiKey = Normalize(Environment.GetEnvironmentVariable("HERMES_WEB_SEARCH_API_KEY"));
        var maxResults = ReadIntEnvironment("HERMES_WEB_SEARCH_MAX_RESULTS", fallback: 10, min: 1, max: 50);
        var allowedDomains = NormalizeList(Environment.GetEnvironmentVariable("HERMES_WEB_SEARCH_ALLOWED_DOMAINS"));
        var missing = new List<string>();

        if (!provider.Equals("none", StringComparison.OrdinalIgnoreCase)
            && !provider.Equals("generic_http_json", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("HERMES_WEB_SEARCH_PROVIDER_invalid");
        }

        if (provider.Equals("generic_http_json", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                missing.Add("HERMES_WEB_SEARCH_ENDPOINT_missing");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                missing.Add("HERMES_WEB_SEARCH_API_KEY_missing");
            }
        }

        return new WebSearchConnectorConfiguration(
            Provider: provider,
            Endpoint: endpoint,
            ApiKey: apiKey,
            MaxResults: maxResults,
            AllowedDomains: allowedDomains,
            MissingVariables: missing);
    }

    public WebResearchFetcherConnectorStatus CheckConnectorStatus()
    {
        var config = LoadConnectorConfiguration();
        var envKeys = new[]
        {
            "HERMES_WEB_SEARCH_API_KEY",
            "WEB_SEARCH_API_KEY",
            "SERPAPI_API_KEY",
            "BRAVE_SEARCH_API_KEY",
            "BING_SEARCH_API_KEY",
            "SEARCH_API_KEY"
        };

        var detected = envKeys
            .Where(key => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var provider = config.Provider.Equals("generic_http_json", StringComparison.OrdinalIgnoreCase)
            ? "generic_http_json"
            : "none";

        if (!provider.Equals("generic_http_json", StringComparison.OrdinalIgnoreCase)
            || config.MissingVariables.Count > 0)
        {
            return new WebResearchFetcherConnectorStatus(
                HasConnector: false,
                Status: "blocked_no_web_connector",
                ConnectorType: null,
                Endpoint: config.Endpoint,
                Provider: provider,
                MaxResults: config.MaxResults,
                AllowedDomains: config.AllowedDomains,
                ApiKeysDetected: [],
                MissingVariables: config.MissingVariables,
                Warnings: config.MissingVariables.Count > 0
                    ? config.MissingVariables.ToList()
                    : ["HERMES_WEB_SEARCH_PROVIDER_none"],
                Recommendation: config.Provider.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? "Set HERMES_WEB_SEARCH_PROVIDER=generic_http_json plus endpoint and API key to enable controlled web search."
                    : "Provide missing web-search variables before automated fetching. No fake sources will be generated.");
        }

        return new WebResearchFetcherConnectorStatus(
            HasConnector: true,
            Status: "connector_available",
            ConnectorType: "generic_http_json",
            Endpoint: config.Endpoint,
            Provider: provider,
            MaxResults: config.MaxResults,
            AllowedDomains: config.AllowedDomains,
            ApiKeysDetected: detected,
            MissingVariables: [],
            Warnings: [],
            Recommendation: "Connector detected. Fetcher can run in controlled web-research mode.");
    }

    public AutomatedWebResearchFetcherReport Run(int maxItems, bool dryRun)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var config = LoadConnectorConfiguration();
        var connector = CheckConnectorStatus();
        var requests = LoadRequests();
        var considered = requests.Take(Math.Max(0, maxItems)).ToList();
        var candidates = new List<WebResearchImportCandidateRecord>();
        var rejected = new List<WebResearchImportCandidateRecord>();
        var warnings = new List<string>();

        if (!connector.HasConnector)
        {
            var blockedReport = new AutomatedWebResearchFetcherReport(
                ReportVersion: "automated_web_research_fetcher_v1",
                UpdatedAtUtc: now,
                Status: connector.Status,
                TotalRequests: requests.Count,
                ConsideredRequests: considered.Count,
                FetchedCandidates: 0,
                BlockedRequests: considered.Count,
                AwaitingHumanReview: 0,
                DuplicateCandidates: 0,
                SameDomainBlocked: 0,
                Candidates: [],
                Rejected: considered.Select(ConvertRequestToCandidate).ToList(),
                Warnings: connector.Warnings.Concat(["web_fetcher_blocked_no_connector"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequestsPath: RequestsPath,
                ImportCandidatesPath: ImportCandidatesPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true,
                ResearchOnly: true);
            WriteReport(blockedReport);
            return blockedReport;
        }

        var importedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingCandidates = LoadImportCandidates();
        foreach (var candidate in existingCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Url))
            {
                importedUrls.Add(candidate.Url);
            }
        }

        foreach (var request in considered)
        {
            var fetched = FetchCandidatesForRequest(request, connector, config);
            foreach (var candidate in fetched)
            {
                if (string.IsNullOrWhiteSpace(candidate.Url) || importedUrls.Contains(candidate.Url))
                {
                    rejected.Add(ConvertRequestToCandidate(request));
                    continue;
                }

                candidates.Add(candidate);
                importedUrls.Add(candidate.Url);
            }
        }

        if (!dryRun && candidates.Count > 0)
        {
            var merged = existingCandidates
                .Concat(candidates)
                .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            File.WriteAllText(ImportCandidatesPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));
        }

        var report = new AutomatedWebResearchFetcherReport(
            ReportVersion: "automated_web_research_fetcher_v1",
            UpdatedAtUtc: now,
            Status: connector.Status,
            TotalRequests: requests.Count,
            ConsideredRequests: considered.Count,
            FetchedCandidates: candidates.Count,
            BlockedRequests: rejected.Count,
            AwaitingHumanReview: candidates.Count,
            DuplicateCandidates: 0,
            SameDomainBlocked: 0,
            Candidates: candidates,
            Rejected: rejected,
            Warnings: warnings.Concat(connector.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RequestsPath: RequestsPath,
            ImportCandidatesPath: ImportCandidatesPath,
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

    private IReadOnlyList<WebResearchSourceRequest> LoadRequests()
    {
        if (!File.Exists(RequestsPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchSourceRequest>>(File.ReadAllText(RequestsPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
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
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(File.ReadAllText(ImportCandidatesPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> FetchCandidatesForRequest(
        WebResearchSourceRequest request,
        WebResearchFetcherConnectorStatus connector,
        WebSearchConnectorConfiguration config)
    {
        if (!connector.HasConnector || !connector.ConnectorType!.Equals("generic_http_json", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        try
        {
            var endpoint = new Uri($"{config.Endpoint!.TrimEnd('/')}/");
            var requestPayload = new
            {
                query = request.Query,
                max_results = config.MaxResults,
                allowed_domains = request.RecommendedSourceDomains.Any() ? request.RecommendedSourceDomains : config.AllowedDomains,
                request_id = request.RequestId,
                knowledge_item_id = request.KnowledgeItemId
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.ApiKey}");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestPayload, JsonDefaults.WriteOptions), Encoding.UTF8, "application/json");
            using var response = _httpClient.Send(httpRequest);
            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload))
            {
                return [];
            }

            var parsed = ParseSearchResponse(payload, request, config);
            return parsed;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<WebResearchImportCandidateRecord> ParseSearchResponse(string payload, WebResearchSourceRequest request, WebSearchConnectorConfiguration config)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var results = new List<WebResearchImportCandidateRecord>();
            var root = document.RootElement;
            var items = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray().ToList()
                : root.TryGetProperty("results", out var resultsNode) && resultsNode.ValueKind == JsonValueKind.Array
                    ? resultsNode.EnumerateArray().ToList()
                    : root.TryGetProperty("items", out var itemsNode) && itemsNode.ValueKind == JsonValueKind.Array
                        ? itemsNode.EnumerateArray().ToList()
                        : root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Array
                            ? dataNode.EnumerateArray().ToList()
                            : [];

            foreach (var item in items.Take(Math.Max(1, config.MaxResults)))
            {
                var title = ReadString(item, "title") ?? ReadString(item, "name") ?? request.Query;
                var url = ReadString(item, "url") ?? ReadString(item, "link") ?? ReadString(item, "href");
                var snippet = ReadString(item, "snippet") ?? ReadString(item, "summary") ?? ReadString(item, "excerpt") ?? ReadString(item, "description") ?? "";
                var domain = ReadString(item, "domain") ?? TryGetDomain(url) ?? request.Domain;
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (config.AllowedDomains.Count > 0
                    && !config.AllowedDomains.Any(allowed => DomainMatches(domain, allowed) || DomainMatches(TryGetDomain(url), allowed)))
                {
                    continue;
                }

                results.Add(new WebResearchImportCandidateRecord(
                    KnowledgeItemId: request.KnowledgeItemId,
                    Title: title ?? request.Query,
                    Url: url,
                    Domain: domain ?? request.Domain,
                    SourceType: "controlled_web_fetch",
                    ExcerptOrSummary: snippet,
                    RetrievedAtUtc: DateTimeOffset.UtcNow,
                    EvidenceReason: request.Reason,
                    IndependenceClaim: "externally_fetched_candidate",
                    HumanReviewStatus: "pending",
                    SafetyFlags: ["no_trading_execution", "human_review_required"]));
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString()
            : null;
    }

    private static string? TryGetDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host;
    }

    private static bool DomainMatches(string? actual, string allowed)
    {
        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        return actual.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || actual.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase)
            || allowed.Equals("*", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static int ReadIntEnvironment(string key, int fallback, int min, int max)
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable(key), out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static WebResearchImportCandidateRecord ConvertRequestToCandidate(WebResearchSourceRequest request)
    {
        var domain = request.RecommendedSourceDomains.FirstOrDefault() ?? request.Domain;
        return new WebResearchImportCandidateRecord(
            KnowledgeItemId: request.KnowledgeItemId,
            Title: request.Query,
            Url: $"https://example.invalid/{domain}/{request.RequestId}",
            Domain: domain,
            SourceType: "controlled_web_fetch",
            ExcerptOrSummary: request.Reason,
            RetrievedAtUtc: request.CreatedAtUtc,
            EvidenceReason: request.Reason,
            IndependenceClaim: "awaiting_web_connector",
            HumanReviewStatus: "pending",
            SafetyFlags: ["no_trading_execution", "human_review_required"]);
    }

    private void WriteReport(AutomatedWebResearchFetcherReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(AutomatedWebResearchFetcherReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Automated Web Research Fetcher Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Total Requests: {report.TotalRequests}");
        sb.AppendLine($"- Considered Requests: {report.ConsideredRequests}");
        sb.AppendLine($"- Fetched Candidates: {report.FetchedCandidates}");
        sb.AppendLine($"- Blocked Requests: {report.BlockedRequests}");
        sb.AppendLine($"- Awaiting Human Review: {report.AwaitingHumanReview}");
        sb.AppendLine();
        sb.AppendLine("## Connector");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        sb.AppendLine($"- research_only: {report.ResearchOnly}");
        sb.AppendLine();
        sb.AppendLine("## Candidates");
        foreach (var candidate in report.Candidates.Take(20))
        {
            sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
        }
        sb.AppendLine();
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
