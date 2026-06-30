using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record ResearchQueryBuilderResult(
    string RequestId,
    string KnowledgeItemId,
    string Domain,
    string KnowledgeTitle,
    string BaseTerm,
    IReadOnlyList<string> QueryTerms,
    IReadOnlyList<string> RecommendedSourceDomains,
    string Status,
    string SkippedReason);

public sealed record ResearchQueryBuilderReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedRequests,
    int GeneratedQueries,
    int KnowledgeItemsMatched,
    IReadOnlyList<ResearchQueryBuilderResult> Items,
    IReadOnlyList<string> Warnings,
    string RequestsPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed record ResearchCandidateRelevanceScoreResult(
    double RelevanceScore,
    string SourceRelevanceStatus,
    IReadOnlyList<string> MatchedTerms,
    string? RejectionReason);

public sealed class ResearchQueryBuilderService
{
    private readonly StoragePaths _storagePaths;

    public ResearchQueryBuilderService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "direct_domain_research");

    public string ReportPath => Path.Combine(Root, "research_query_builder_report.json");

    public string MarkdownPath => Path.Combine(Root, "research_query_builder_report.md");

    public string RequestsPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_requests.json");

    public ResearchQueryBuilderResult BuildForRequest(WebResearchSourceRequest request)
    {
        var catalogItem = LoadKnowledgeCatalog().FirstOrDefault(item =>
            item.Id.Equals(request.KnowledgeItemId, StringComparison.OrdinalIgnoreCase)
            || request.KnowledgeItemId.Contains(item.Id, StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase)
            || request.Query.Contains(item.Title, StringComparison.OrdinalIgnoreCase));

        var knowledgeTitle = catalogItem?.Title
            ?? request.Query
            ?? request.KnowledgeItemId;
        var baseTerm = NormalizeTerm(catalogItem?.Title ?? request.Query ?? request.KnowledgeItemId);
        if (string.IsNullOrWhiteSpace(baseTerm))
        {
            baseTerm = NormalizeTerm(request.KnowledgeItemId);
        }

        var queryTerms = BuildQueryTerms(request, catalogItem, baseTerm);
        var recommendedDomains = request.RecommendedSourceDomains.Any()
            ? request.RecommendedSourceDomains
            : RecommendedSourceDomains(request.Domain, knowledgeTitle);

        return new ResearchQueryBuilderResult(
            RequestId: request.RequestId,
            KnowledgeItemId: request.KnowledgeItemId,
            Domain: request.Domain,
            KnowledgeTitle: knowledgeTitle,
            BaseTerm: baseTerm,
            QueryTerms: queryTerms,
            RecommendedSourceDomains: recommendedDomains,
            Status: "query_terms_generated",
            SkippedReason: string.Empty);
    }

    public ResearchQueryBuilderReport Run(int maxItems)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var envelope = LoadRequestsEnvelope();
        var requests = envelope.Requests.Take(Math.Max(0, maxItems)).ToList();
        var items = requests.Select(BuildForRequest).ToList();
        var generatedQueries = items.Sum(item => item.QueryTerms.Count);
        var knowledgeItemsMatched = items.Count(item => !string.IsNullOrWhiteSpace(item.KnowledgeTitle));
        var report = new ResearchQueryBuilderReport(
            ReportVersion: "research_query_builder_v1",
            UpdatedAtUtc: now,
            Status: items.Count > 0 ? "queries_generated" : "no_requests_available",
            LoadedRequests: envelope.LoadedRequests,
            GeneratedQueries: generatedQueries,
            KnowledgeItemsMatched: knowledgeItemsMatched,
            Items: items,
            Warnings: envelope.Warnings.Concat(items.Count == 0 ? ["no_requests_for_query_builder"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RequestsPath: RequestsPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public ResearchQueryBuilderReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(50) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<ResearchQueryBuilderReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions)
                ?? throw new InvalidOperationException("research_query_builder_report_empty");
        }
        catch
        {
            return Run(50) with { Status = "status_snapshot_generated" };
        }
    }

    private static IReadOnlyList<string> BuildQueryTerms(WebResearchSourceRequest request, KnowledgeCatalogItem? item, string baseTerm)
    {
        var terms = new List<string>();
        void Add(string? value)
        {
            value = NormalizeTerm(value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                terms.Add(value);
            }
        }

        Add(item?.Title);
        Add(baseTerm);
        Add($"{baseTerm} trading strategy");
        Add($"{baseTerm} forex price action");
        Add($"{baseTerm} breakout setup");
        Add($"{baseTerm} examples");
        Add($"{baseTerm} definition");

        foreach (var tag in item?.Tags ?? [])
        {
            Add($"{baseTerm} {tag}");
        }

        foreach (var domainTerm in DomainTerms(request.Domain))
        {
            Add($"{baseTerm} {domainTerm}");
        }

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static IReadOnlyList<string> DomainTerms(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => ["forex", "ctrader", "spotware", "price action", "setup"],
            "documentation" => ["documentation", "manual", "guide", "reference"],
            "software" => ["github", "api", "sdk", "docs"],
            _ => ["guide", "reference", "examples"]
        };

    private static IReadOnlyList<string> RecommendedSourceDomains(string domain, string title) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => ["spotware.com", "help.ctrader.com", "ctrader.com", "trading.de"],
            "documentation" => ["help.ctrader.com", "learn.microsoft.com", "docs.microsoft.com"],
            "software" => ["github.com", "learn.microsoft.com", "docs.microsoft.com"],
            _ when title.Contains("cTrader", StringComparison.OrdinalIgnoreCase) => ["help.ctrader.com", "ctrader.com", "spotware.com"],
            _ => ["spotware.com", "help.ctrader.com", "ctrader.com"]
        };

    public static ResearchCandidateRelevanceScoreResult ScoreCandidate(
        string knowledgeTitle,
        string requestDomain,
        string requestKnowledgeItemId,
        string candidateTitle,
        string candidateUrl,
        string candidateSnippet,
        string candidateDomain,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<string> recommendedDomains)
    {
        var title = NormalizeTerm(candidateTitle);
        var url = NormalizeTerm(candidateUrl);
        var snippet = NormalizeTerm(candidateSnippet);
        var terms = queryTerms
            .Select(NormalizeTerm)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var matchedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double score = 0;

        var titleMatches = terms.Count(term => ContainsTerm(title, term) || ContainsTerm(knowledgeTitle, term));
        if (titleMatches > 0)
        {
            matchedTerms.Add("title_term");
            score += Math.Min(0.4, titleMatches * 0.1);
        }

        var urlMatches = terms.Count(term => ContainsTerm(url, term));
        if (urlMatches > 0)
        {
            matchedTerms.Add("url_term");
            score += Math.Min(0.2, urlMatches * 0.05);
        }

        var snippetMatches = terms.Count(term => ContainsTerm(snippet, term));
        if (snippetMatches > 0)
        {
            matchedTerms.Add("snippet_term");
            score += Math.Min(0.25, snippetMatches * 0.05);
        }

        if (DomainMatches(candidateDomain, recommendedDomains, requestDomain))
        {
            matchedTerms.Add("domain_match");
            score += 0.15;
        }

        var navigationPenalty = NavigationPenalty(title, url, snippet);
        if (navigationPenalty > 0)
        {
            matchedTerms.Add("navigation_penalty");
            score -= navigationPenalty;
        }

        if (ContainsTerm(title, requestKnowledgeItemId) || ContainsTerm(snippet, requestKnowledgeItemId))
        {
            matchedTerms.Add("knowledge_item_id");
            score += 0.1;
        }

        score = Math.Max(0, Math.Min(1, score));
        var status = score >= 0.45 ? "accepted_relevant_candidate" : "rejected_low_relevance";
        var rejectionReason = status == "accepted_relevant_candidate"
            ? null
            : score <= 0.15
                ? "too_low_relevance"
                : "insufficient_term_match";

        return new ResearchCandidateRelevanceScoreResult(
            RelevanceScore: Math.Round(score, 4),
            SourceRelevanceStatus: status,
            MatchedTerms: matchedTerms.OrderBy(term => term, StringComparer.OrdinalIgnoreCase).ToList(),
            RejectionReason: rejectionReason);
    }

    private static bool DomainMatches(string candidateDomain, IReadOnlyList<string> recommendedDomains, string requestDomain)
    {
        if (string.IsNullOrWhiteSpace(candidateDomain))
        {
            return false;
        }

        var normalized = NormalizeTerm(candidateDomain).Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (recommendedDomains.Any(domain => normalized.Equals(NormalizeTerm(domain), StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith($".{NormalizeTerm(domain)}", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var requestDomainNormalized = NormalizeTerm(requestDomain);
        return !string.IsNullOrWhiteSpace(requestDomainNormalized)
            && (normalized.Equals(requestDomainNormalized, StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith($".{requestDomainNormalized}", StringComparison.OrdinalIgnoreCase));
    }

    private static double NavigationPenalty(string title, string url, string snippet)
    {
        var penalty = 0d;
        var all = $"{title} {url} {snippet}".ToLowerInvariant();
        if (all.Contains("/bots") || all.Contains("download") || all.Contains("brokers") || all.Contains("/copy") || all.Contains("/indicators") || all.Contains("start_trading"))
        {
            penalty += 0.25;
        }

        if (all.Contains("navigation") || all.Contains("menu") || all.Contains("search"))
        {
            penalty += 0.1;
        }

        if (all.Contains("home") && !all.Contains("how to") && !all.Contains("guide"))
        {
            penalty += 0.15;
        }

        return penalty;
    }

    private static bool ContainsTerm(string haystack, string term)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        var normalizedHaystack = NormalizeTerm(haystack);
        var normalizedTerm = NormalizeTerm(term);
        return normalizedHaystack.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
        normalized = normalized.Replace("_", " ", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("-", " ", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace(":", " ", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("/", " ", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("|", " ", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("site ", " ", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(normalized, "\\s+", " ").Trim();
    }

    private IReadOnlyList<KnowledgeCatalogItem> LoadKnowledgeCatalog()
    {
        try
        {
            return new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        }
        catch
        {
            return [];
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

    private static string BuildMarkdown(ResearchQueryBuilderReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Research Query Builder Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Requests: {report.LoadedRequests}");
        sb.AppendLine($"- Generated Queries: {report.GeneratedQueries}");
        sb.AppendLine($"- Knowledge Items Matched: {report.KnowledgeItemsMatched}");
        sb.AppendLine();
        sb.AppendLine("## Requests");
        foreach (var item in report.Items.Take(20))
        {
            sb.AppendLine($"- {item.KnowledgeItemId} | {item.Domain} | base={item.BaseTerm} | queries={item.QueryTerms.Count}");
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
}
