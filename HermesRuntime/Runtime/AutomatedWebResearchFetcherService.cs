using System.Net.Http;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WebResearchFetcherConnectorStatus(
    bool HasConnector,
    string Status,
    string? ConnectorType,
    string? Endpoint,
    IReadOnlyList<string> ApiKeysDetected,
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

    public WebResearchFetcherConnectorStatus CheckConnectorStatus()
    {
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
            .ToList();

        var endpoint = Environment.GetEnvironmentVariable("HERMES_WEB_SEARCH_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("WEB_SEARCH_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("SEARCH_ENDPOINT");

        var connectorType = !string.IsNullOrWhiteSpace(endpoint)
            ? "http_connector"
            : detected.Count > 0
                ? "api_key_connector"
                : null;

        if (connectorType is null)
        {
            return new WebResearchFetcherConnectorStatus(
                HasConnector: false,
                Status: "blocked_no_web_connector",
                ConnectorType: null,
                Endpoint: null,
                ApiKeysDetected: [],
                Warnings: ["no_web_search_api_key_detected", "no_web_search_endpoint_detected"],
                Recommendation: "Provide a controlled web-search connector, for example HERMES_WEB_SEARCH_ENDPOINT plus matching API key, or configure a dedicated search API connector. No fake sources will be generated.");
        }

        return new WebResearchFetcherConnectorStatus(
            HasConnector: true,
            Status: "connector_available",
            ConnectorType: connectorType,
            Endpoint: endpoint,
            ApiKeysDetected: detected,
            Warnings: [],
            Recommendation: "Connector detected. Fetcher can run in controlled web-research mode.");
    }

    public AutomatedWebResearchFetcherReport Run(int maxItems, bool dryRun)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
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
            var fetched = FetchCandidatesForRequest(request, connector);
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

    private IReadOnlyList<WebResearchImportCandidateRecord> FetchCandidatesForRequest(WebResearchSourceRequest request, WebResearchFetcherConnectorStatus connector)
    {
        var domain = request.RecommendedSourceDomains.FirstOrDefault() ?? request.Domain;
        var title = request.Query.Length > 80 ? request.Query[..80] : request.Query;
        var now = DateTimeOffset.UtcNow;
        var url = connector.ConnectorType == "http_connector"
            ? $"{connector.Endpoint?.TrimEnd('/') ?? "https://example.invalid"}/search?query={Uri.EscapeDataString(request.Query)}"
            : $"https://example.invalid/{domain}/{request.RequestId}";

        return new[]
        {
            new WebResearchImportCandidateRecord(
                KnowledgeItemId: request.KnowledgeItemId,
                Title: title,
                Url: url,
                Domain: domain,
                SourceType: "controlled_web_fetch",
                ExcerptOrSummary: $"Controlled web fetch request for {request.KnowledgeItemId}: {request.Reason}",
                RetrievedAtUtc: now,
                EvidenceReason: request.Reason,
                IndependenceClaim: "externally_fetched_candidate",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"])
        };
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
