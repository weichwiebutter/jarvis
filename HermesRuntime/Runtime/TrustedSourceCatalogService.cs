using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record TrustedSourceCatalogEntry(
    string Domain,
    string Category,
    bool Allowed,
    string SourceType,
    string ReliabilityHint,
    string? SearchEntryUrl,
    IReadOnlyList<string> TopicPatterns,
    IReadOnlyList<string> BlockedPaths,
    IReadOnlyList<string> PreferredPaths);

public sealed record TrustedSourceCatalogReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedSources,
    int AllowedSources,
    int BlockedSources,
    IReadOnlyList<TrustedSourceCatalogEntry> Sources,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Warnings,
    string CatalogPath,
    string ExamplePath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class TrustedSourceCatalogService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TrustedSourceCatalogService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trusted_source_catalog");

    public string ConfigPath => Path.Combine(_runtimeRoot, "config", "trusted_source_catalog.json");

    public string ExamplePath => Path.Combine(_runtimeRoot, "config", "trusted_source_catalog.example.json");

    public string ReportPath => Path.Combine(Root, "trusted_source_catalog_report.json");

    public string MarkdownPath => Path.Combine(Root, "trusted_source_catalog_report.md");

    public TrustedSourceCatalogReport LoadStatus()
    {
        Directory.CreateDirectory(Root);
        var sources = LoadCatalog();
        var warnings = new List<string>();
        if (!File.Exists(ConfigPath))
        {
            warnings.Add("trusted_source_catalog_missing");
        }

        var report = new TrustedSourceCatalogReport(
            ReportVersion: "trusted_source_catalog_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: sources.Count == 0 ? "catalog_missing_or_empty" : "catalog_loaded",
            LoadedSources: sources.Count,
            AllowedSources: sources.Count(source => source.Allowed),
            BlockedSources: sources.Count(source => !source.Allowed),
            Sources: sources,
            Categories: sources
                .Select(source => source.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Warnings: warnings,
            CatalogPath: ConfigPath,
            ExamplePath: ExamplePath,
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

    public IReadOnlyList<TrustedSourceCatalogEntry> LoadCatalog()
    {
        if (!File.Exists(ConfigPath))
        {
            return [];
        }

        try
        {
            var payload = JsonSerializer.Deserialize<TrustedSourceCatalogFile>(
                File.ReadAllText(ConfigPath),
                JsonDefaults.SnapshotReadOptions);
            return payload?.Sources ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    public IReadOnlyList<TrustedSourceCatalogEntry> ResolveForRequest(WebResearchSourceRequest request, ResearchQueryBuilderResult queryPlan)
    {
        var allSources = LoadCatalog();
        if (allSources.Count == 0)
        {
            return [];
        }

        var requestCategory = Normalize(request.Domain);
        var queryText = Normalize($"{request.Query} {queryPlan.KnowledgeTitle} {queryPlan.BaseTerm} {string.Join(' ', queryPlan.QueryTerms)}");
        var targetPatterns = new List<string>();
        targetPatterns.AddRange(queryPlan.QueryTerms);
        targetPatterns.Add(queryPlan.BaseTerm);
        targetPatterns.Add(request.Query);
        targetPatterns.Add(request.KnowledgeItemId);

        var resolved = allSources
            .Where(source => source.Allowed)
            .Where(source => SourceMatches(source, requestCategory, queryText, targetPatterns))
            .OrderByDescending(source => source.Category.Equals(requestCategory, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(source => source.TopicPatterns.Count(pattern => ContainsAny(queryText, pattern)))
            .ThenByDescending(source => source.ReliabilityHint.Equals("high", StringComparison.OrdinalIgnoreCase))
            .ThenBy(source => source.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resolved.Count > 0)
        {
            return resolved;
        }

        return allSources
            .Where(source => source.Allowed)
            .Where(source => source.Category.Equals(requestCategory, StringComparison.OrdinalIgnoreCase) || IsFallbackCategory(source.Category, requestCategory))
            .OrderByDescending(source => source.ReliabilityHint.Equals("high", StringComparison.OrdinalIgnoreCase))
            .ThenBy(source => source.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsFallbackCategory(string sourceCategory, string requestCategory) =>
        (requestCategory.Equals("trading", StringComparison.OrdinalIgnoreCase)
            && sourceCategory.Equals("documentation", StringComparison.OrdinalIgnoreCase))
        || (requestCategory.Equals("documentation", StringComparison.OrdinalIgnoreCase)
            && sourceCategory.Equals("software", StringComparison.OrdinalIgnoreCase))
        || (requestCategory.Equals("software", StringComparison.OrdinalIgnoreCase)
            && sourceCategory.Equals("documentation", StringComparison.OrdinalIgnoreCase));

    private static bool SourceMatches(
        TrustedSourceCatalogEntry source,
        string requestCategory,
        string queryText,
        IReadOnlyList<string> targetPatterns)
    {
        if (source.Category.Equals(requestCategory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (source.TopicPatterns.Any(pattern => ContainsAny(queryText, pattern)))
        {
            return true;
        }

        return targetPatterns.Any(target => source.TopicPatterns.Any(pattern => ContainsAny(Normalize(target), pattern)));
    }

    public static bool ContainsAny(string haystack, string pattern)
    {
        haystack = Normalize(haystack);
        pattern = Normalize(pattern);
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        return haystack.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> BuildCandidateUrls(TrustedSourceCatalogEntry source, string query)
    {
        var encoded = Uri.EscapeDataString(query);
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(source.SearchEntryUrl))
        {
            urls.Add(ApplyQuery(source.SearchEntryUrl, encoded, query));
        }

        foreach (var path in source.PreferredPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            urls.Add(ApplyQuery(BuildAbsoluteUrl(source.Domain, path), encoded, query));
        }

        if (urls.Count == 0)
        {
            urls.Add($"https://{NormalizeDomain(source.Domain)}/");
        }

        return urls
            .Where(url => !IsBlocked(source, url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsBlocked(TrustedSourceCatalogEntry source, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var path = uri.AbsolutePath ?? string.Empty;
        return source.BlockedPaths.Any(blocked => !string.IsNullOrWhiteSpace(blocked) && path.StartsWith(NormalizePath(blocked), StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildAbsoluteUrl(string domain, string path)
    {
        var normalized = NormalizePath(path);
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return $"https://{NormalizeDomain(domain)}{normalized}";
    }

    private static string ApplyQuery(string value, string encodedQuery, string rawQuery)
    {
        return value
            .Replace("{query}", encodedQuery, StringComparison.OrdinalIgnoreCase)
            .Replace("{raw_query}", rawQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", " ");

    private static string NormalizeDomain(string value)
    {
        var normalized = Normalize(value);
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string NormalizePath(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildMarkdown(TrustedSourceCatalogReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trusted Source Catalog");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Sources: {report.LoadedSources}");
        sb.AppendLine($"- Allowed Sources: {report.AllowedSources}");
        sb.AppendLine($"- Blocked Sources: {report.BlockedSources}");
        if (report.Categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Categories");
            foreach (var category in report.Categories)
            {
                sb.AppendLine($"- {category}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        sb.AppendLine($"- research_only: {report.ResearchOnly}");

        if (report.Sources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Sources");
            foreach (var source in report.Sources.Take(20))
            {
                sb.AppendLine($"- {source.Domain} | {source.Category} | allowed={source.Allowed} | {source.ReliabilityHint}");
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
    private sealed record TrustedSourceCatalogFile(IReadOnlyList<TrustedSourceCatalogEntry> Sources);
}
