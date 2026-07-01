using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PublisherGroupEntry(
    string Input,
    string Domain,
    string PublisherGroup,
    string Rule,
    string SourceType,
    string Category,
    bool FromKnownMapping,
    bool FromFallback,
    DateTimeOffset ResolvedAtUtc);

public sealed record PublisherGroupReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedEntries,
    int DistinctPublisherGroups,
    int KnownMappings,
    int FallbackMappings,
    IReadOnlyList<PublisherGroupEntry> Entries,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class PublisherGroupResolverService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PublisherGroupResolverService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "publisher_groups");

    public string ReportPath => Path.Combine(Root, "publisher_group_report.json");

    public string MarkdownPath => Path.Combine(Root, "publisher_group_report.md");

    public PublisherGroupReport LoadStatus()
    {
        Directory.CreateDirectory(Root);
        var entries = BuildEntries();
        var report = new PublisherGroupReport(
            ReportVersion: "publisher_group_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: entries.Count == 0 ? "no_entries" : "loaded",
            LoadedEntries: entries.Count,
            DistinctPublisherGroups: entries.Select(entry => entry.PublisherGroup).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            KnownMappings: entries.Count(entry => entry.FromKnownMapping),
            FallbackMappings: entries.Count(entry => entry.FromFallback),
            Entries: entries,
            Groups: entries.Select(entry => entry.PublisherGroup).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: [],
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);
        Write(report);
        return report;
    }

    public string Resolve(string? input)
    {
        var entry = ResolveEntry(input);
        return string.IsNullOrWhiteSpace(entry.PublisherGroup) ? "unknown" : entry.PublisherGroup;
    }

    public PublisherGroupEntry ResolveEntry(string? input)
    {
        var now = DateTimeOffset.UtcNow;
        var domain = NormalizeDomain(ExtractHost(input));
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new PublisherGroupEntry(input ?? string.Empty, string.Empty, string.Empty, "empty", "unknown", "unknown", false, true, now);
        }

        var (group, rule, knownMapping) = ResolveGroup(domain);
        return new PublisherGroupEntry(
            Input: input ?? string.Empty,
            Domain: domain,
            PublisherGroup: group,
            Rule: rule,
            SourceType: "resolved_domain",
            Category: group,
            FromKnownMapping: knownMapping,
            FromFallback: !knownMapping,
            ResolvedAtUtc: now);
    }

    public IReadOnlyList<string> ResolveGroups(IEnumerable<string> inputs) =>
        inputs
            .Select(Resolve)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IReadOnlyList<PublisherGroupEntry> BuildEntries()
    {
        var entries = new List<PublisherGroupEntry>();
        var sources = new TrustedSourceCatalogService(_storagePaths, _runtimeRoot).LoadCatalog();
        foreach (var source in sources)
        {
            entries.Add(ResolveEntry(source.Domain));
            if (!string.IsNullOrWhiteSpace(source.SearchEntryUrl))
            {
                entries.Add(ResolveEntry(source.SearchEntryUrl));
            }
        }

        var seeds = new KnownArticleSeedCatalogService(_storagePaths).LoadSeeds();
        foreach (var seed in seeds)
        {
            entries.Add(ResolveEntry(seed.Domain));
            entries.Add(ResolveEntry(seed.Url));
        }

        var confirmations = new SourceConfirmationEngine(_storagePaths).LoadOrBuild();
        foreach (var result in confirmations.Results)
        {
            entries.Add(ResolveEntry(result.Domain));
            foreach (var candidate in result.CandidateSources ?? [])
            {
                entries.Add(ResolveEntry(candidate.Domain));
                entries.Add(ResolveEntry(candidate.Url));
            }
        }

        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Domain) || !string.IsNullOrWhiteSpace(entry.Input))
            .DistinctBy(entry => $"{entry.Domain}||{entry.Input}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.PublisherGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string Group, string Rule, bool KnownMapping) ResolveGroup(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return (string.Empty, "empty", false);
        }

        if (domain.EndsWith("forums.babypips.com", StringComparison.OrdinalIgnoreCase) || domain.EndsWith("babypips.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("Babypips", "babypips_domain", true);
        }

        if (domain.EndsWith("investopedia.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("Investopedia", "investopedia_domain", true);
        }

        if (domain.EndsWith("trading.de", StringComparison.OrdinalIgnoreCase))
        {
            return ("TradingDE", "trading_de_domain", true);
        }

        if (domain.EndsWith("learn.microsoft.com", StringComparison.OrdinalIgnoreCase) || domain.EndsWith("docs.microsoft.com", StringComparison.OrdinalIgnoreCase) || domain.EndsWith("microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("Microsoft", "microsoft_domain", true);
        }

        if (domain.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("GitHub", "github_domain", true);
        }

        if (domain.EndsWith("ig.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("IG", "ig_domain", true);
        }

        if (domain.EndsWith("cmcmarkets.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("CMCMarkets", "cmcmarkets_domain", true);
        }

        if (domain.EndsWith("dailyfx.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("DailyFX", "dailyfx_domain", true);
        }

        if (domain.EndsWith("avatrade.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("AvaTrade", "avatrade_domain", true);
        }

        if (domain.EndsWith("fidelity.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("Fidelity", "fidelity_domain", true);
        }

        if (domain.EndsWith("schwab.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("Schwab", "schwab_domain", true);
        }

        if (domain.EndsWith("fxcm.com", StringComparison.OrdinalIgnoreCase))
        {
            return ("FXCM", "fxcm_domain", true);
        }

        return (GetFallbackRootDomain(domain), "fallback_root_domain", false);
    }

    public static string GetFallbackRootDomain(string? host)
    {
        host = NormalizeDomain(host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? string.Join('.', parts[^2..]) : host;
    }

    private static string ExtractHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return input;
    }

    private static string NormalizeDomain(string? value)
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

    private static void Write(PublisherGroupReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PublisherGroupReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Publisher Group Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Entries: {report.LoadedEntries}");
        sb.AppendLine($"- Distinct Publisher Groups: {report.DistinctPublisherGroups}");
        sb.AppendLine($"- Known Mappings: {report.KnownMappings}");
        sb.AppendLine($"- Fallback Mappings: {report.FallbackMappings}");
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in report.Entries.Take(100))
        {
            sb.AppendLine($"- {entry.Input} | {entry.Domain} | {entry.PublisherGroup} | {entry.Rule}");
        }
        return sb.ToString();
    }
}
