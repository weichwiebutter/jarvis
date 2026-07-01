using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record KnownArticleSeedDefinition(
    string SeedId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Url,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Synonyms,
    string Category,
    int Priority,
    bool Allowed = true,
    string? Reason = null,
    string? PublisherGroup = null,
    bool? Enabled = null);

public sealed record KnownArticleSeedRequest(
    string SeedId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Url,
    string PublisherGroup,
    string Category,
    int Priority,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Synonyms,
    string Status,
    string Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record KnownArticleSeedCandidate(
    string KnowledgeItemId,
    string SeedId,
    string Title,
    string Url,
    string Domain,
    string ExcerptOrSummary,
    string EvidenceReason,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    DateTimeOffset RetrievedAtUtc,
    double RelevanceScore = 0,
    IReadOnlyList<string>? MatchedTerms = null,
    string? RejectionReason = null,
    string? SourceRelevanceStatus = null);

public sealed record KnownArticleSeedStatusReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedKnowledgeItems,
    int ConsideredKnowledgeItems,
    int SeedDefinitions,
    int SeedRequests,
    int FetchedCandidates,
    int AcceptedCandidates,
    int RejectedCandidates,
    int DuplicateCandidates,
    IReadOnlyList<string> LoadedSeeds,
    IReadOnlyList<KnownArticleSeedRequest> Requests,
    IReadOnlyList<KnownArticleSeedCandidate> Candidates,
    IReadOnlyList<KnownArticleSeedCandidate> Rejected,
    IReadOnlyList<string> Warnings,
    string SeedCatalogPath,
    string RequestsPath,
    string ImportCandidatesPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool Applied,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnownArticleSeedCatalogService
{
    private readonly StoragePaths _storagePaths;
    private readonly HttpClient _httpClient;
    private readonly string _runtimeRoot;

    public KnownArticleSeedCatalogService(StoragePaths storagePaths, string? runtimeRoot = null, HttpClient? httpClient = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HermesRuntime/1.0");
        }
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog");

    public string ConfigPath => Path.Combine(_runtimeRoot, "config", "known_article_seed_catalog.json");

    public string ExamplePath => Path.Combine(_runtimeRoot, "config", "known_article_seed_catalog.example.json");

    public string RequestsPath => Path.Combine(_storagePaths.Root, "reports", "known_article_seed_catalog", "known_article_seed_requests.json");

    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");

    public string ReportPath => Path.Combine(Root, "known_article_seed_report.json");

    public string MarkdownPath => Path.Combine(Root, "known_article_seed_report.md");

    public IReadOnlyList<KnownArticleSeedDefinition> LoadSeeds() => LoadSeedDefinitions();

    public KnownArticleSeedStatusReport LoadStatus()
    {
        Directory.CreateDirectory(Root);
        var seeds = LoadSeeds();
        var report = new KnownArticleSeedStatusReport(
            ReportVersion: "known_article_seed_catalog_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: seeds.Count == 0 ? "catalog_missing_or_empty" : "catalog_loaded",
            LoadedKnowledgeItems: new KnowledgeCatalog(_storagePaths).LoadOrCreateItems().Count,
            ConsideredKnowledgeItems: 0,
            SeedDefinitions: seeds.Count,
            SeedRequests: 0,
            FetchedCandidates: 0,
            AcceptedCandidates: 0,
            RejectedCandidates: 0,
            DuplicateCandidates: 0,
            LoadedSeeds: seeds.Select(seed => seed.SeedId).ToList(),
            Requests: [],
            Candidates: [],
            Rejected: [],
            Warnings: seeds.Count == 0 ? ["known_article_seed_catalog_missing"] : [],
            SeedCatalogPath: ConfigPath,
            RequestsPath: RequestsPath,
            ImportCandidatesPath: ImportCandidatesPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: true,
            Applied: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        WriteReport(report);
        return report;
    }

    public KnownArticleSeedStatusReport Run(int maxItems, bool dryRun)
    {
        Directory.CreateDirectory(Root);
        EnsureExampleFile();
        var now = DateTimeOffset.UtcNow;
        var knowledgeItems = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var sourceConfirmations = new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
        var existingCandidates = LoadImportCandidates();
        var existingUrls = existingCandidates
            .Select(candidate => candidate.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seedDefinitions = LoadSeedDefinitions();
        var publisherGroupResolver = new PublisherGroupResolverService(_storagePaths, _runtimeRoot);
        var requests = BuildRequests(knowledgeItems, sourceConfirmations, seedDefinitions)
            .Take(Math.Max(1, maxItems))
            .ToList();
        var finalRequests = new List<KnownArticleSeedRequest>();
        var accepted = new List<KnownArticleSeedCandidate>();
        var rejected = new List<KnownArticleSeedCandidate>();
        var warnings = new List<string>();

        var fetchedCandidates = 0;
        foreach (var request in requests)
        {
            var fetched = FetchSeed(request);
            if (fetched is null)
            {
                finalRequests.Add(request with { Status = "fetch_failed", Reason = "no_html_content" });
                rejected.Add(ConvertRejected(request, "fetch_failed", "no_html_content"));
                warnings.Add($"fetch_failed:{request.SeedId}");
                continue;
            }

            fetchedCandidates++;
            var evaluation = ScoreCandidate(request, fetched);
            var candidate = fetched with
            {
                RelevanceScore = evaluation.Score,
                MatchedTerms = evaluation.MatchedTerms,
                RejectionReason = evaluation.RejectionReason,
                SourceRelevanceStatus = evaluation.Status,
                HumanReviewStatus = "pending",
                SafetyFlags = ["no_trading_execution", "human_review_required"]
            };

            if (string.IsNullOrWhiteSpace(candidate.Url) || existingUrls.Contains(candidate.Url))
            {
                finalRequests.Add(request with { Status = "duplicate_url", Reason = "duplicate_url" });
                rejected.Add(candidate with { RejectionReason = "duplicate_url", SourceRelevanceStatus = "duplicate" });
                continue;
            }

            var publisherGroup = NormalizePublisherGroup(request.PublisherGroup);
            if (!string.Equals(publisherGroup, string.Empty, StringComparison.OrdinalIgnoreCase) &&
                IsDuplicatePublisherGroup(existingCandidates, candidate, publisherGroupResolver, publisherGroup))
            {
                finalRequests.Add(request with { Status = "duplicate_publisher_group", Reason = "duplicate_publisher_group" });
                rejected.Add(candidate with { RejectionReason = "duplicate_publisher_group", SourceRelevanceStatus = "duplicate_publisher_group" });
                warnings.Add($"duplicate_publisher_group:{request.SeedId}:{publisherGroup}");
                continue;
            }

            if (evaluation.Score < 0.45)
            {
                finalRequests.Add(request with { Status = "rejected_low_relevance", Reason = evaluation.RejectionReason ?? "low_relevance" });
                rejected.Add(candidate);
                continue;
            }

            finalRequests.Add(request with { Status = "accepted_candidate", Reason = "accepted_candidate" });
            accepted.Add(candidate);
            if (!dryRun)
            {
                existingUrls.Add(candidate.Url);
            }
        }

        if (finalRequests.Count < requests.Count)
        {
            foreach (var request in requests.Skip(finalRequests.Count))
            {
                finalRequests.Add(request with { Status = request.Status, Reason = request.Reason });
            }
        }

        if (!dryRun && accepted.Count > 0)
        {
            var merged = existingCandidates
                .Concat(accepted.Select(ToImportCandidate))
                .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            File.WriteAllText(ImportCandidatesPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));
        }

        var report = new KnownArticleSeedStatusReport(
            ReportVersion: "known_article_seed_catalog_v1",
            UpdatedAtUtc: now,
            Status: accepted.Count > 0 ? "seed_candidates_ready" : "no_seed_candidates",
            LoadedKnowledgeItems: knowledgeItems.Count,
            ConsideredKnowledgeItems: requests.Count,
            SeedDefinitions: seedDefinitions.Count,
            SeedRequests: requests.Count,
            FetchedCandidates: fetchedCandidates,
            AcceptedCandidates: accepted.Count,
            RejectedCandidates: rejected.Count,
            DuplicateCandidates: rejected.Count(candidate => (candidate.RejectionReason ?? string.Empty).Contains("duplicate", StringComparison.OrdinalIgnoreCase)),
            LoadedSeeds: seedDefinitions.Select(seed => seed.SeedId).ToList(),
            Requests: finalRequests.Count == requests.Count ? finalRequests : requests,
            Candidates: accepted,
            Rejected: rejected,
            Warnings: warnings.Concat(accepted.Count == 0 ? ["no_known_article_seed_candidates"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SeedCatalogPath: ConfigPath,
            RequestsPath: RequestsPath,
            ImportCandidatesPath: ImportCandidatesPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: dryRun,
            Applied: !dryRun,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        if (!dryRun)
        {
            File.WriteAllText(RequestsPath, JsonSerializer.Serialize(requests, JsonDefaults.WriteOptions));
        }

        return report;
    }

    private IReadOnlyList<KnownArticleSeedDefinition> LoadSeedDefinitions()
    {
        if (!File.Exists(ConfigPath))
        {
            return DefaultSeeds();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<KnownArticleSeedCatalogFile>(File.ReadAllText(ConfigPath), JsonDefaults.SnapshotReadOptions);
            return payload?.Seeds.Where(seed => (seed.Enabled ?? seed.Allowed) && seed.Allowed).ToList() ?? DefaultSeeds();
        }
        catch
        {
            return DefaultSeeds();
        }
    }

    private IReadOnlyList<KnownArticleSeedDefinition> DefaultSeeds() =>
    [
        new("bullish_engulfing_babypips", "trading:bullish_engulfing", "Bullish Engulfing", "babypips.com", "https://www.babypips.com/learn/forex/bullish-engulfing-candlestick-pattern", ["bullish engulfing", "engulfing pattern", "candlestick pattern"], ["bullish engulfer", "bullish engulfing pattern"], "candlestick", 100),
        new("bullish_engulfing_investopedia", "trading:bullish_engulfing", "Bullish Engulfing", "investopedia.com", "https://www.investopedia.com/terms/e/engulfingpattern.asp", ["bullish engulfing", "engulfing pattern"], ["candlestick reversal"], "candlestick", 95),
        new("inside_bar_babypips", "trading:inside_bar", "Inside Bar", "babypips.com", "https://www.babypips.com/learn/forex/inside-bar-candlestick-pattern", ["inside bar", "inside candle", "compression"], ["inside bar pattern"], "candlestick", 100),
        new("inside_bar_investopedia", "trading:inside_bar", "Inside Bar", "investopedia.com", "https://www.investopedia.com/terms/i/inside-day.asp", ["inside bar", "inside day"], ["compression candle"], "candlestick", 95),
        new("inside_bar_trading_de", "trading:inside_bar", "Inside Bar", "trading.de", "https://trading.de/charts/candlestick/candlestick-pattern/", ["inside bar"], ["candlestick pattern"], "candlestick", 90),
        new("doji_babypips", "trading:doji", "Doji", "babypips.com", "https://www.babypips.com/learn/forex/doji-candlesticks", ["doji", "indecision candle"], ["neutral candle"], "candlestick", 100),
        new("doji_investopedia", "trading:doji", "Doji", "investopedia.com", "https://www.investopedia.com/terms/d/doji.asp", ["doji"], ["indecision"], "candlestick", 95),
        new("double_top_babypips", "trading:double_top", "Double Top", "babypips.com", "https://www.babypips.com/learn/forex/double-top-chart-pattern", ["double top", "double top pattern"], ["double top reversal"], "chart_pattern", 100),
        new("double_top_investopedia", "trading:double_top", "Double Top", "investopedia.com", "https://www.investopedia.com/terms/d/doubletop.asp", ["double top"], ["chart pattern"], "chart_pattern", 95),
        new("double_bottom_babypips", "trading:double_bottom", "Double Bottom", "babypips.com", "https://www.babypips.com/learn/forex/double-bottom-chart-pattern", ["double bottom", "double bottom pattern"], ["double bottom reversal"], "chart_pattern", 100),
        new("double_bottom_investopedia", "trading:double_bottom", "Double Bottom", "investopedia.com", "https://www.investopedia.com/terms/d/doublebottom.asp", ["double bottom"], ["chart pattern"], "chart_pattern", 95),
        new("hammer_babypips", "trading:hammer", "Hammer", "babypips.com", "https://www.babypips.com/learn/forex/hammer-candlestick", ["hammer", "hammer candlestick"], ["bullish rejection candle"], "candlestick", 100),
        new("hammer_investopedia", "trading:hammer", "Hammer", "investopedia.com", "https://www.investopedia.com/terms/h/hammer.asp", ["hammer"], ["wick rejection"], "candlestick", 95)
    ];

    private IReadOnlyList<KnownArticleSeedRequest> BuildRequests(
        IReadOnlyList<KnowledgeCatalogItem> items,
        SourceConfirmationReport confirmations,
        IReadOnlyList<KnownArticleSeedDefinition> seeds)
    {
        var publisherGroups = new PublisherGroupResolverService(_storagePaths, _runtimeRoot);
        var sourceCounts = confirmations.Results.ToDictionary(result => result.KnowledgeId, result => result.SourceCount, StringComparer.OrdinalIgnoreCase);
        var prioritized = items
            .Select(item => new
            {
                Item = item,
                SourceCount = sourceCounts.TryGetValue(item.Id, out var count) ? count : 0,
                Relevance = ScoreItemPriority(item)
            })
            .Where(entry => entry.SourceCount < 2)
            .OrderByDescending(entry => entry.Relevance)
            .ThenByDescending(entry => entry.Item.ValidationStatus.Contains("trusted", StringComparison.OrdinalIgnoreCase) || entry.Item.ValidationStatus.Contains("validated", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entry => entry.Item.Confidence)
            .ThenBy(entry => entry.Item.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        var requests = new List<KnownArticleSeedRequest>();
        foreach (var entry in prioritized)
        {
            foreach (var seed in seeds.Where(seed => (seed.Enabled ?? seed.Allowed) && seed.Allowed && seed.KnowledgeItemId.Equals(entry.Item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var publisherGroup = NormalizePublisherGroup(seed.PublisherGroup) switch
                {
                    var value when !string.IsNullOrWhiteSpace(value) => value,
                    _ => publisherGroups.Resolve(seed.Url)
                };

                requests.Add(new KnownArticleSeedRequest(
                    SeedId: seed.SeedId,
                    KnowledgeItemId: entry.Item.Id,
                    Title: seed.Title,
                    Domain: seed.Domain,
                    Url: seed.Url,
                    PublisherGroup: publisherGroup,
                    Category: seed.Category,
                    Priority: seed.Priority,
                    Keywords: seed.Keywords,
                    Synonyms: seed.Synonyms,
                    Status: "ready_to_fetch",
                    Reason: $"source_count={entry.SourceCount};priority={entry.Relevance:0.###}",
                    CreatedAtUtc: DateTimeOffset.UtcNow));
            }
        }

        return requests;
    }

    private static bool IsDuplicatePublisherGroup(
        IReadOnlyList<WebResearchImportCandidateRecord> existingCandidates,
        KnownArticleSeedCandidate candidate,
        PublisherGroupResolverService resolver,
        string publisherGroup)
    {
        if (string.IsNullOrWhiteSpace(publisherGroup))
        {
            return false;
        }

        var candidateGroup = resolver.Resolve(candidate.Url);
        if (!string.Equals(candidateGroup, publisherGroup, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return existingCandidates.Any(existing =>
            !string.IsNullOrWhiteSpace(existing.Url) &&
            string.Equals(existing.Domain, candidate.Domain, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(resolver.Resolve(existing.Url), publisherGroup, StringComparison.OrdinalIgnoreCase));
    }

    private static double ScoreItemPriority(KnowledgeCatalogItem item)
    {
        var text = $"{item.Id} {item.Title} {item.DescriptionShort} {string.Join(' ', item.Tags)}".ToLowerInvariant();
        var score = 0d;
        if (text.Contains("bullish engulfing")) score += 1.0;
        if (text.Contains("inside bar")) score += 0.98;
        if (text.Contains("doji")) score += 0.92;
        if (text.Contains("double top")) score += 0.9;
        if (text.Contains("double bottom")) score += 0.9;
        if (text.Contains("hammer")) score += 0.88;
        if (text.Contains("support resistance")) score += 0.72;
        if (text.Contains("breakout")) score += 0.7;
        if (text.Contains("bullish") || text.Contains("bearish")) score += 0.2;
        return score;
    }

    private KnownArticleSeedCandidate? FetchSeed(KnownArticleSeedRequest request)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.Url);
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

            var title = ExtractTitle(html) ?? request.Title;
            var snippet = ExtractSnippet(html);
            return new KnownArticleSeedCandidate(
                KnowledgeItemId: request.KnowledgeItemId,
                SeedId: request.SeedId,
                Title: title,
                Url: request.Url,
                Domain: request.Domain,
                ExcerptOrSummary: snippet,
                EvidenceReason: $"known_article_seed:{request.SeedId}",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"],
                RetrievedAtUtc: DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    private static (double Score, IReadOnlyList<string> MatchedTerms, string Status, string? RejectionReason) ScoreCandidate(KnownArticleSeedRequest request, KnownArticleSeedCandidate candidate)
    {
        var matched = new List<string>();
        var score = 0d;
        var title = NormalizeText(candidate.Title);
        var url = NormalizeText(candidate.Url);
        var excerpt = NormalizeText(candidate.ExcerptOrSummary);
        var domain = NormalizeText(candidate.Domain);
        var terms = request.Keywords.Concat(request.Synonyms).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var term in terms)
        {
            var n = NormalizeText(term);
            if (string.IsNullOrWhiteSpace(n)) continue;
            if (title.Contains(n, StringComparison.OrdinalIgnoreCase)) { score += 0.4; matched.Add(term); }
            if (url.Contains(n, StringComparison.OrdinalIgnoreCase)) { score += 0.2; matched.Add(term); }
            if (excerpt.Contains(n, StringComparison.OrdinalIgnoreCase)) { score += 0.25; matched.Add(term); }
            if (domain.Contains(n, StringComparison.OrdinalIgnoreCase)) { score += 0.05; }
        }

        if (candidate.Domain.Contains("babypips", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("investopedia", StringComparison.OrdinalIgnoreCase) || candidate.Domain.Contains("trading.de", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15;
        }

        score = Math.Min(1, score);
        var status = score >= 0.45 ? "candidate_ready_for_import" : "candidate_rejected_low_relevance";
        var reason = status == "candidate_rejected_low_relevance" ? "low_relevance" : null;
        return (score, matched.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), status, reason);
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

    private static WebResearchImportCandidateRecord ToImportCandidate(KnownArticleSeedCandidate candidate) =>
        new(
            KnowledgeItemId: candidate.KnowledgeItemId,
            Title: candidate.Title,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: "known_article_seed_candidate",
            ExcerptOrSummary: candidate.ExcerptOrSummary,
            RetrievedAtUtc: candidate.RetrievedAtUtc,
            EvidenceReason: candidate.EvidenceReason,
            IndependenceClaim: "known_article_seed_direct_url",
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags,
            RelevanceScore: candidate.RelevanceScore,
            MatchedTerms: candidate.MatchedTerms,
            RejectionReason: candidate.RejectionReason,
            SourceRelevanceStatus: candidate.SourceRelevanceStatus);

    private static string NormalizePublisherGroup(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static KnownArticleSeedCandidate ConvertRejected(KnownArticleSeedRequest request, string reason, string status) =>
        new(
            KnowledgeItemId: request.KnowledgeItemId,
            SeedId: request.SeedId,
            Title: request.Title,
            Url: request.Url,
            Domain: request.Domain,
            ExcerptOrSummary: string.Empty,
            EvidenceReason: reason,
            HumanReviewStatus: "pending",
            SafetyFlags: ["no_trading_execution", "human_review_required"],
            RetrievedAtUtc: DateTimeOffset.UtcNow,
            RelevanceScore: 0,
            MatchedTerms: [],
            RejectionReason: reason,
            SourceRelevanceStatus: status);

    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, "<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            return CleanText(WebUtility.HtmlDecode(match.Groups["title"].Value));
        }

        return null;
    }

    private static string ExtractSnippet(string html)
    {
        var meta = Regex.Match(html, "<meta[^>]+name=[\"']description[\"'][^>]+content=[\"'](?<content>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (meta.Success)
        {
            return CleanText(WebUtility.HtmlDecode(meta.Groups["content"].Value));
        }

        return CleanText(Regex.Replace(html, "<[^>]+>", " "))[0..Math.Min(200, CleanText(Regex.Replace(html, "<[^>]+>", " ")).Length)];
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value, "\\s+", " ").Trim();
    }

    private static string NormalizeText(string value)
    {
        return CleanText(value).ToLowerInvariant();
    }

    private void EnsureExampleFile()
    {
        if (File.Exists(ExamplePath))
        {
            return;
        }

        var example = new KnownArticleSeedCatalogFile(DefaultSeeds());
        Directory.CreateDirectory(Path.GetDirectoryName(ExamplePath) ?? Root);
        File.WriteAllText(ExamplePath, JsonSerializer.Serialize(example, JsonDefaults.WriteOptions));
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(example, JsonDefaults.WriteOptions));
        }
    }

    private static void WriteReport(KnownArticleSeedStatusReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnownArticleSeedStatusReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Known Article Seed Catalog Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Seed Definitions: {report.SeedDefinitions}");
        sb.AppendLine($"- Seed Requests: {report.SeedRequests}");
        sb.AppendLine($"- Fetched Candidates: {report.FetchedCandidates}");
        sb.AppendLine($"- Accepted Candidates: {report.AcceptedCandidates}");
        sb.AppendLine($"- Rejected Candidates: {report.RejectedCandidates}");
        sb.AppendLine($"- Duplicate Candidates: {report.DuplicateCandidates}");
        sb.AppendLine();
        sb.AppendLine("## Requests");
        foreach (var req in report.Requests.Take(20))
        {
            sb.AppendLine($"- {req.KnowledgeItemId} | {req.Title} | {req.Domain} | {req.PublisherGroup} | {req.Url} | {req.Status}");
        }
        sb.AppendLine();
        sb.AppendLine("## Candidates");
        foreach (var candidate in report.Candidates.Take(20))
        {
            sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | score={candidate.RelevanceScore:0.###}");
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

    private sealed record KnownArticleSeedCatalogFile(IReadOnlyList<KnownArticleSeedDefinition> Seeds);
}
