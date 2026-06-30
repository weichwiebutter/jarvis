using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record BrowserResearchRuntimeStatus(
    bool BrowserRuntimeAvailable,
    string Status,
    string? RuntimeKind,
    string? RuntimeMode,
    string? BrowserChannel,
    string? ExecutablePath,
    bool ExecutableExists,
    string? BrowserBinary,
    string? PlaywrightPackage,
    bool DetectedBrokenSnapChromium,
    IReadOnlyList<string> MissingRequirements,
    IReadOnlyList<string> Warnings,
    string Recommendation);

public sealed record BrowserResearchCandidate(
    string KnowledgeItemId,
    string Title,
    string Url,
    string Domain,
    string Snippet,
    string SourceType,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    DateTimeOffset RetrievedAtUtc);

public sealed record BrowserSearchOutcome(
    string OpenedSearchUrl,
    string PageTitle,
    int ExtractedLinksCount,
    string ExtractionStatus,
    IReadOnlyList<string> DebugArtifactPaths,
    IReadOnlyList<BrowserResearchCandidate> Candidates);

public sealed record BrowserResearchAgentReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedRequests,
    int SkippedDueToSchema,
    int SkippedDueToStatus,
    int SkippedDueToMissingQuery,
    int TotalRequests,
    int ConsideredRequests,
    int FetchedCandidates,
    int RejectedCandidates,
    int DuplicateCandidates,
    int ImportedCandidates,
    string OpenedSearchUrl,
    string PageTitle,
    int ExtractedLinksCount,
    string ExtractionStatus,
    IReadOnlyList<string> DebugArtifactPaths,
    IReadOnlyList<BrowserResearchCandidate> Candidates,
    IReadOnlyList<BrowserResearchCandidate> Rejected,
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

public sealed class BrowserResearchAgentService
{
    private readonly StoragePaths _storagePaths;

    public BrowserResearchAgentService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "browser_research_agent");

    public string RequestsPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_requests.json");

    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");

    public string ReportPath => Path.Combine(Root, "browser_research_report.json");

    public string MarkdownPath => Path.Combine(Root, "browser_research_report.md");

    private string DebugRoot => Path.Combine(Root, "debug");

    public BrowserResearchRuntimeStatus CheckRuntimeStatus()
    {
        var missing = new List<string>();
        var explicitPath = NormalizeBrowserPath(Environment.GetEnvironmentVariable("HERMES_BROWSER_EXECUTABLE_PATH"));
        var browserChannel = NormalizeBrowserPath(Environment.GetEnvironmentVariable("HERMES_BROWSER_CHANNEL"));
        var explicitExists = !string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath);
        var detectedBrokenSnapChromium = false;

        if (explicitExists)
        {
            var brokenSnap = IsBrokenSnapChromium(explicitPath);
            detectedBrokenSnapChromium = brokenSnap;
            if (brokenSnap)
            {
                missing.Add("broken_snap_chromium");
            }
            else
            {
                return new BrowserResearchRuntimeStatus(
                    BrowserRuntimeAvailable: true,
                    Status: "browser_runtime_available",
                    RuntimeKind: "system_browser_path",
                    RuntimeMode: "system_browser_path",
                    BrowserChannel: browserChannel,
                    ExecutablePath: explicitPath,
                    ExecutableExists: true,
                    BrowserBinary: explicitPath,
                    PlaywrightPackage: "not_required",
                    DetectedBrokenSnapChromium: false,
                    MissingRequirements: [],
                    Warnings: [],
                    Recommendation: "Browser runtime is available via explicit executable path.");
            }
        }

        var node = FindExecutable("node");
        if (node is null)
        {
            missing.Add("node_missing");
        }

        var playwrightCheck = node is null ? false : CheckPlaywrightAvailable(node);
        if (!playwrightCheck)
        {
            missing.Add("playwright_missing");
        }

        var browserBinary = explicitExists ? explicitPath : FindBrowserBinary();
        if (browserBinary is null)
        {
            missing.Add("browser_binary_missing");
        }
        else if (IsBrokenSnapChromium(browserBinary))
        {
            detectedBrokenSnapChromium = true;
            missing.Add("broken_snap_chromium");
        }

        if (missing.Count > 0)
        {
            return new BrowserResearchRuntimeStatus(
                BrowserRuntimeAvailable: false,
                Status: "blocked_browser_runtime_missing",
                RuntimeKind: null,
                RuntimeMode: "blocked",
                BrowserChannel: browserChannel,
                ExecutablePath: explicitPath,
                ExecutableExists: explicitExists,
                BrowserBinary: browserBinary,
                PlaywrightPackage: playwrightCheck ? "available" : null,
                DetectedBrokenSnapChromium: detectedBrokenSnapChromium,
                MissingRequirements: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings: ["no_local_browser_runtime_detected"],
                Recommendation: "Install Playwright and a local browser runtime (for example Chromium). Hermes will not fake sources. Use docs/research/browser_research_agent_setup_v1.md for the exact setup steps.");
        }

        return new BrowserResearchRuntimeStatus(
            BrowserRuntimeAvailable: true,
            Status: "browser_runtime_available",
            RuntimeKind: explicitExists ? "system_browser_path" : "playwright_managed",
            RuntimeMode: explicitExists ? "system_browser_path" : "playwright_managed",
            BrowserChannel: browserChannel,
            ExecutablePath: explicitPath,
            ExecutableExists: explicitExists,
            BrowserBinary: browserBinary,
            PlaywrightPackage: "available",
            DetectedBrokenSnapChromium: detectedBrokenSnapChromium,
            MissingRequirements: [],
            Warnings: [],
            Recommendation: "Browser runtime is available for controlled browser research.");
    }

    public BrowserResearchAgentReport Run(int maxItems, bool dryRun)
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(DebugRoot);
        var now = DateTimeOffset.UtcNow;
        var runtime = CheckRuntimeStatus();
        var load = LoadRequestsEnvelope();
        var requests = load.Requests.Take(Math.Max(0, maxItems)).ToList();
        var considered = requests.Count;

        if (dryRun)
        {
            var dryRunReport = new BrowserResearchAgentReport(
                ReportVersion: "browser_research_agent_v1",
                UpdatedAtUtc: now,
                Status: "dry_run_request_ready",
                LoadedRequests: load.LoadedRequests,
                SkippedDueToSchema: load.SkippedDueToSchema,
                SkippedDueToStatus: load.SkippedDueToStatus,
                SkippedDueToMissingQuery: load.SkippedDueToMissingQuery,
                TotalRequests: load.LoadedRequests,
                ConsideredRequests: considered,
                FetchedCandidates: 0,
                RejectedCandidates: 0,
                DuplicateCandidates: 0,
                ImportedCandidates: 0,
                OpenedSearchUrl: "-",
                PageTitle: "-",
                ExtractedLinksCount: 0,
                ExtractionStatus: "dry_run_request_ready",
                DebugArtifactPaths: [],
                Candidates: [],
                Rejected: [],
                Warnings: load.Warnings.Concat(runtime.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequestsPath: RequestsPath,
                ImportCandidatesPath: ImportCandidatesPath,
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

        if (!runtime.BrowserRuntimeAvailable)
        {
            var blocked = new BrowserResearchAgentReport(
                ReportVersion: "browser_research_agent_v1",
                UpdatedAtUtc: now,
                Status: runtime.Status,
                LoadedRequests: load.LoadedRequests,
                SkippedDueToSchema: load.SkippedDueToSchema,
                SkippedDueToStatus: load.SkippedDueToStatus,
                SkippedDueToMissingQuery: load.SkippedDueToMissingQuery,
                TotalRequests: load.LoadedRequests,
                ConsideredRequests: considered,
                FetchedCandidates: 0,
                RejectedCandidates: 0,
                DuplicateCandidates: 0,
                ImportedCandidates: 0,
                OpenedSearchUrl: "-",
                PageTitle: "-",
                ExtractedLinksCount: 0,
                ExtractionStatus: runtime.Status,
                DebugArtifactPaths: [],
                Candidates: [],
                Rejected: [],
                Warnings: load.Warnings.Concat(runtime.Warnings).Concat(runtime.MissingRequirements).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequestsPath: RequestsPath,
                ImportCandidatesPath: ImportCandidatesPath,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true,
                ResearchOnly: true);
            WriteReport(blocked);
            return blocked;
        }

        var existing = LoadImportCandidates();
        var importedUrls = existing
            .Select(candidate => candidate.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<BrowserResearchCandidate>();
        var rejected = new List<BrowserResearchCandidate>();
        var warnings = new List<string>();
        var debugArtifacts = new List<string>();
        var firstOpenedSearchUrl = "-";
        var firstPageTitle = "-";
        var firstExtractionStatus = "not_started";
        var firstExtractedLinksCount = 0;

        foreach (var request in requests)
        {
            var outcome = FetchCandidatesForRequest(request, runtime, DebugRoot, allowBrowserExecution: true);
            debugArtifacts.AddRange(outcome.DebugArtifactPaths);
            if (firstOpenedSearchUrl == "-")
            {
                firstOpenedSearchUrl = outcome.OpenedSearchUrl;
                firstPageTitle = outcome.PageTitle;
                firstExtractionStatus = outcome.ExtractionStatus;
                firstExtractedLinksCount = outcome.ExtractedLinksCount;
            }

            var fetched = outcome.Candidates.Take(5).ToList();
            if (fetched.Count == 0)
            {
                warnings.Add($"no_browser_results_for:{request.RequestId}:{outcome.ExtractionStatus}");
            }

            foreach (var candidate in fetched)
            {
                if (string.IsNullOrWhiteSpace(candidate.Url))
                {
                    rejected.Add(candidate);
                    continue;
                }

                if (importedUrls.Contains(candidate.Url))
                {
                    rejected.Add(candidate);
                    continue;
                }

                candidates.Add(candidate);
                importedUrls.Add(candidate.Url);
            }
        }

        if (!dryRun && candidates.Count > 0)
        {
            var merged = existing
                .Concat(candidates.Select(ToImportCandidate))
                .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            File.WriteAllText(ImportCandidatesPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));
        }

        var report = new BrowserResearchAgentReport(
            ReportVersion: "browser_research_agent_v1",
            UpdatedAtUtc: now,
            Status: runtime.Status,
            LoadedRequests: load.LoadedRequests,
            SkippedDueToSchema: load.SkippedDueToSchema,
            SkippedDueToStatus: load.SkippedDueToStatus,
            SkippedDueToMissingQuery: load.SkippedDueToMissingQuery,
            TotalRequests: load.LoadedRequests,
            ConsideredRequests: considered,
            FetchedCandidates: candidates.Count,
            RejectedCandidates: rejected.Count,
            DuplicateCandidates: 0,
            ImportedCandidates: dryRun ? 0 : candidates.Count,
            OpenedSearchUrl: firstOpenedSearchUrl,
            PageTitle: firstPageTitle,
            ExtractedLinksCount: firstExtractedLinksCount,
            ExtractionStatus: firstExtractionStatus,
            DebugArtifactPaths: debugArtifacts.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Candidates: candidates,
            Rejected: rejected,
            Warnings: runtime.Warnings.Concat(runtime.MissingRequirements).Concat(warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
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

    private BrowserResearchRequestsLoadResult LoadRequestsEnvelope()
    {
        if (!File.Exists(RequestsPath))
        {
            return new BrowserResearchRequestsLoadResult([], 0, 0, 0, 0, ["requests_file_missing"]);
        }

        try
        {
            var text = File.ReadAllText(RequestsPath);
            var envelope = JsonSerializer.Deserialize<WebResearchRequestsEnvelope>(text, JsonDefaults.SnapshotReadOptions);
            if (envelope is null)
            {
                return new BrowserResearchRequestsLoadResult([], 0, 0, 0, 0, ["requests_envelope_empty"]);
            }

            var requests = envelope.Requests ?? [];
            var schemaSkipped = 0;
            var statusSkipped = 0;
            var querySkipped = 0;
            var validRequests = new List<WebResearchSourceRequest>();

            foreach (var request in requests)
            {
                if (request is null)
                {
                    schemaSkipped++;
                    continue;
                }

                if (!request.Status.Equals("awaiting_external_search", StringComparison.OrdinalIgnoreCase))
                {
                    statusSkipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    querySkipped++;
                    continue;
                }

                validRequests.Add(request);
            }

            return new BrowserResearchRequestsLoadResult(
                validRequests,
                requests.Count,
                schemaSkipped,
                statusSkipped,
                querySkipped,
                requests.Count == 0 ? ["no_requests_in_export"] : []);
        }
        catch
        {
            return new BrowserResearchRequestsLoadResult([], 0, 1, 0, 0, ["requests_deserialize_failed"]);
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

    private BrowserSearchOutcome FetchCandidatesForRequest(
        WebResearchSourceRequest request,
        BrowserResearchRuntimeStatus runtime,
        string debugRoot,
        bool allowBrowserExecution)
    {
        if (!allowBrowserExecution)
        {
            return new BrowserSearchOutcome(
                OpenedSearchUrl: "-",
                PageTitle: "-",
                ExtractedLinksCount: 0,
                ExtractionStatus: "browser_execution_disabled",
                DebugArtifactPaths: [],
                Candidates: []);
        }

        var results = RunBrowserSearch(request.Query, request.RecommendedSourceDomains, 5, debugRoot, request.RequestId, runtime.ExecutablePath, runtime.RuntimeMode, runtime.BrowserChannel);
        var now = DateTimeOffset.UtcNow;
        return new BrowserSearchOutcome(
            OpenedSearchUrl: results.OpenedSearchUrl,
            PageTitle: results.PageTitle,
            ExtractedLinksCount: results.ExtractedLinksCount,
            ExtractionStatus: results.ExtractionStatus,
            DebugArtifactPaths: results.DebugArtifactPaths,
            Candidates: results.Links.Select(result => new BrowserResearchCandidate(
                KnowledgeItemId: request.KnowledgeItemId,
                Title: result.Title,
                Url: result.Url,
                Domain: result.Domain,
                Snippet: result.Snippet,
                SourceType: "browser_research_candidate",
                HumanReviewStatus: "pending",
                SafetyFlags: ["no_trading_execution", "human_review_required"],
                RetrievedAtUtc: now)).ToList());
    }

    private static BrowserSearchExecutionResult RunBrowserSearch(string query, IReadOnlyList<string> allowedDomains, int maxResults, string debugRoot, string requestId, string? executablePath, string? runtimeMode, string? browserChannel)
    {
        Directory.CreateDirectory(debugRoot);
        var searchUrlPath = Path.Combine(debugRoot, "search_url.txt");
        var pageTitlePath = Path.Combine(debugRoot, "page_title.txt");
        var pageExcerptPath = Path.Combine(debugRoot, "page_excerpt.html");
        var screenshotPath = Path.Combine(debugRoot, $"{SanitizeFileName(requestId)}.png");
        var browserStatePath = Path.Combine(debugRoot, $"{SanitizeFileName(requestId)}.json");

        var node = FindExecutable("node");
        if (node is null)
        {
            return BrowserSearchExecutionResult.Blocked("node_missing", searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath);
        }

        var script = BuildBrowserScript(query, allowedDomains, maxResults, searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath, executablePath, runtimeMode, browserChannel);

        var psi = new ProcessStartInfo
        {
            FileName = node,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return BrowserSearchExecutionResult.Blocked("browser_process_start_failed", searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath);
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                var fallback = TryBrowserDumpDomSearch(query, allowedDomains, maxResults, searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath, executablePath);
                if (fallback is not null)
                {
                    return fallback;
                }

                return BrowserSearchExecutionResult.Blocked(string.IsNullOrWhiteSpace(error) ? "browser_search_failed" : error.Trim(), searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath);
            }

            var result = JsonSerializer.Deserialize<BrowserSearchExecutionResult>(output, JsonDefaults.SnapshotReadOptions);
            if (result is not null && result.Links.Count > 0)
            {
                return result;
            }

            var fallbackResult = TryBrowserDumpDomSearch(query, allowedDomains, maxResults, searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath, executablePath);
            return fallbackResult ?? BrowserSearchExecutionResult.Blocked("browser_search_parse_failed", searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath);
        }
        catch
        {
            var fallback = TryBrowserDumpDomSearch(query, allowedDomains, maxResults, searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath, executablePath);
            return fallback ?? BrowserSearchExecutionResult.Blocked("browser_search_exception", searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath);
        }
    }

    private static BrowserSearchExecutionResult? TryBrowserDumpDomSearch(
        string query,
        IReadOnlyList<string> allowedDomains,
        int maxResults,
        string searchUrlPath,
        string pageTitlePath,
        string pageExcerptPath,
        string screenshotPath,
        string browserStatePath,
        string? executablePath)
    {
        var browserPath = NormalizeBrowserPath(executablePath) ?? FindBrowserBinary();
        if (string.IsNullOrWhiteSpace(browserPath) || !File.Exists(browserPath))
        {
            return null;
        }

        var attempts = new[]
        {
            "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query),
            "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query)
        };

        var lastStatus = "no_results";

        foreach (var searchUrl in attempts)
        {
            var html = RunBrowserDumpDom(browserPath, searchUrl);
            File.WriteAllText(searchUrlPath, searchUrl);
            if (string.IsNullOrWhiteSpace(html))
            {
                lastStatus = "empty_html";
                continue;
            }

            var pageTitle = ExtractPageTitle(html);
            File.WriteAllText(pageTitlePath, pageTitle);
            File.WriteAllText(pageExcerptPath, html.Length > 100000 ? html[..100000] : html);
            var challengeDetected = IsChallengePage(html);
            lastStatus = challengeDetected ? "captcha_challenge_detected" : "dump_dom";
            try
            {
                File.WriteAllText(browserStatePath, JsonSerializer.Serialize(new
                {
                    openedSearchUrl = searchUrl,
                    pageTitle,
                    extractionStatus = lastStatus,
                    extractedLinksCount = 0
                }, JsonDefaults.WriteOptions));
            }
            catch
            {
            }

            var links = ExtractLinksFromHtml(html, allowedDomains, maxResults);
            if (links.Count > 0)
            {
                TryCaptureScreenshot(browserPath, searchUrl, screenshotPath);
                return new BrowserSearchExecutionResult(
                    OpenedSearchUrl: searchUrl,
                    PageTitle: pageTitle,
                    ExtractedLinksCount: links.Count,
                    ExtractionStatus: "results_extracted",
                    DebugArtifactPaths: [searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath],
                    Links: links);
            }

            if (challengeDetected)
            {
                return new BrowserSearchExecutionResult(
                    OpenedSearchUrl: searchUrl,
                    PageTitle: pageTitle,
                    ExtractedLinksCount: 0,
                    ExtractionStatus: "captcha_challenge_detected",
                    DebugArtifactPaths: [searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath],
                    Links: []);
            }
        }

        return new BrowserSearchExecutionResult(
            OpenedSearchUrl: attempts.LastOrDefault() ?? "-",
            PageTitle: "-",
            ExtractedLinksCount: 0,
            ExtractionStatus: lastStatus,
            DebugArtifactPaths: [searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath],
            Links: []);
    }

    private static string RunBrowserDumpDom(string browserPath, string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = browserPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--headless=new");
            psi.ArgumentList.Add("--disable-gpu");
            psi.ArgumentList.Add("--no-first-run");
            psi.ArgumentList.Add("--disable-extensions");
            psi.ArgumentList.Add("--disable-background-networking");
            psi.ArgumentList.Add("--no-default-browser-check");
            psi.ArgumentList.Add("--dump-dom");
            psi.ArgumentList.Add(url);
            using var process = Process.Start(psi);
            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(25000);
            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(output) ? output : error;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<BrowserSearchResult> ExtractLinksFromHtml(string html, IReadOnlyList<string> allowedDomains, int maxResults)
    {
        var results = new List<BrowserSearchResult>();
        if (string.IsNullOrWhiteSpace(html))
        {
            return results;
        }

        var allowed = (allowedDomains ?? []).Select(value => value.Trim().ToLowerInvariant()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var hrefMatches = Regex.Matches(html, "<a[^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<inner>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in hrefMatches)
        {
            var href = System.Net.WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var domain = uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
            if (allowed.Count > 0 && !allowed.Any(allowedDomain => domain == allowedDomain || domain.EndsWith("." + allowedDomain, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (href.Contains("duckduckgo.com", StringComparison.OrdinalIgnoreCase) && (href.Contains("/y.js", StringComparison.OrdinalIgnoreCase) || href.Contains("/l/?", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var title = CleanHtmlText(System.Net.WebUtility.HtmlDecode(match.Groups["inner"].Value));
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var snippet = ExtractSnippetFromHtml(html, match.Index);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                snippet = title;
            }

            results.Add(new BrowserSearchResult(title, href, domain, snippet));
            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results;
    }

    private static string ExtractPageTitle(string html)
    {
        var match = Regex.Match(html, "<title>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return string.Empty;
        }

        return CleanHtmlText(System.Net.WebUtility.HtmlDecode(match.Groups["title"].Value));
    }

    private static string ExtractSnippetFromHtml(string html, int anchorIndex)
    {
        var start = Math.Max(0, anchorIndex - 240);
        var length = Math.Min(500, html.Length - start);
        if (length <= 0)
        {
            return string.Empty;
        }

        return CleanHtmlText(System.Net.WebUtility.HtmlDecode(html.Substring(start, length)));
    }

    private static string CleanHtmlText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, "<[^>]+>", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return cleaned;
    }

    private static bool IsChallengePage(string html) =>
        html.Contains("captcha", StringComparison.OrdinalIgnoreCase)
        || html.Contains("suspicious", StringComparison.OrdinalIgnoreCase)
        || html.Contains("Unfortunately, bots use DuckDuckGo too", StringComparison.OrdinalIgnoreCase)
        || html.Contains("Your request has been flagged as being suspicious", StringComparison.OrdinalIgnoreCase);

    private static void TryCaptureScreenshot(string browserPath, string url, string screenshotPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = browserPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--headless=new");
            psi.ArgumentList.Add("--disable-gpu");
            psi.ArgumentList.Add("--no-first-run");
            psi.ArgumentList.Add("--disable-extensions");
            psi.ArgumentList.Add("--disable-background-networking");
            psi.ArgumentList.Add("--no-default-browser-check");
            psi.ArgumentList.Add($"--screenshot={screenshotPath}");
            psi.ArgumentList.Add(url);
            using var process = Process.Start(psi);
            if (process is null)
            {
                return;
            }

            process.WaitForExit(25000);
        }
        catch
        {
        }
    }

    private static string BuildBrowserScript(
        string query,
        IReadOnlyList<string> allowedDomains,
        int maxResults,
        string searchUrlPath,
        string pageTitlePath,
        string pageExcerptPath,
        string screenshotPath,
        string browserStatePath,
        string? executablePath,
        string? runtimeMode,
        string? browserChannel)
    {
        var allowed = JsonSerializer.Serialize(allowedDomains ?? [], JsonDefaults.WriteOptions);
        var encodedQuery = JsonSerializer.Serialize(query, JsonDefaults.WriteOptions);
        var encodedSearchUrlPath = JsonSerializer.Serialize(searchUrlPath, JsonDefaults.WriteOptions);
        var encodedPageTitlePath = JsonSerializer.Serialize(pageTitlePath, JsonDefaults.WriteOptions);
        var encodedPageExcerptPath = JsonSerializer.Serialize(pageExcerptPath, JsonDefaults.WriteOptions);
        var encodedScreenshotPath = JsonSerializer.Serialize(screenshotPath, JsonDefaults.WriteOptions);
        var encodedBrowserStatePath = JsonSerializer.Serialize(browserStatePath, JsonDefaults.WriteOptions);
        var encodedExecutablePath = JsonSerializer.Serialize(executablePath ?? string.Empty, JsonDefaults.WriteOptions);
        var encodedRuntimeMode = JsonSerializer.Serialize(runtimeMode ?? string.Empty, JsonDefaults.WriteOptions);
        var encodedBrowserChannel = JsonSerializer.Serialize(browserChannel ?? string.Empty, JsonDefaults.WriteOptions);
        return $$"""
const allowedDomains = {{allowed}};
const query = {{encodedQuery}};
const maxResults = {{maxResults}};
const searchUrlPath = {{encodedSearchUrlPath}};
const pageTitlePath = {{encodedPageTitlePath}};
const pageExcerptPath = {{encodedPageExcerptPath}};
const screenshotPath = {{encodedScreenshotPath}};
const browserStatePath = {{encodedBrowserStatePath}};
const executablePath = {{encodedExecutablePath}};
const runtimeMode = {{encodedRuntimeMode}};
const browserChannel = {{encodedBrowserChannel}};
(async () => {
  try {
    const { chromium } = require('playwright');
    const launchOptions = { headless: true };
    if (executablePath && executablePath.length > 0) {
      launchOptions.executablePath = executablePath;
    }
    if (browserChannel && browserChannel.length > 0 && !launchOptions.executablePath) {
      launchOptions.channel = browserChannel;
    }
    const browser = await chromium.launch(launchOptions);
    const page = await browser.newPage();
    const attempts = [
      'https://html.duckduckgo.com/html/?q=' + encodeURIComponent(query),
      'https://duckduckgo.com/?q=' + encodeURIComponent(query)
    ];
    let openedSearchUrl = '';
    let pageTitle = '';
    let pageHtml = '';
    let extractionStatus = 'no_results';
    let links = [];
    for (const url of attempts) {
      openedSearchUrl = url;
      try {
        await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 20000 });
        await page.waitForTimeout(2000);
        pageTitle = await page.title();
        pageHtml = await page.content();
        links = await page.evaluate((maxResults, allowedDomains) => {
      const normalizeDomain = (value) => {
        try { return new URL(value).hostname.replace(/^www\./, '').toLowerCase(); } catch { return ''; }
      };
      const allowed = (allowedDomains || []).map((value) => String(value || '').trim().toLowerCase()).filter(Boolean);
      const selectors = [
        'a[data-testid="result-title-a"]',
        '.result__title a[href]',
        '.links_main a[href]',
        'article a[href]',
        'h2 a[href]',
        'a[href]'
      ];
      const nodes = Array.from(new Set(selectors.flatMap((selector) => Array.from(document.querySelectorAll(selector)))));
      const seen = new Set();
      const items = [];
      for (const anchor of nodes) {
        const href = anchor.href || '';
        if (!href || seen.has(href)) continue;
        const domain = normalizeDomain(href);
        if (!domain) continue;
        if (allowed.length > 0 && !allowed.some((allowedDomain) => domain === allowedDomain || domain.endsWith('.' + allowedDomain))) continue;
        const title = (anchor.innerText || anchor.textContent || '').trim();
        const containerText = anchor.closest('article')?.innerText || anchor.closest('.result')?.innerText || anchor.parentElement?.innerText || '';
        const snippet = String(containerText || '').trim();
        seen.add(href);
        items.push({ title, url: href, domain, snippet: snippet.slice(0, 400) });
        if (items.length >= maxResults) break;
      }
      return items;
    }, maxResults, allowedDomains);
        extractionStatus = links.length > 0 ? 'results_extracted' : 'no_results';
        if (links.length > 0) break;
      } catch (error) {
        extractionStatus = String(error && error.message ? error.message : error);
      }
    }
    try {
      await require('node:fs').promises.writeFile(searchUrlPath, openedSearchUrl, 'utf8');
      await require('node:fs').promises.writeFile(pageTitlePath, pageTitle || '', 'utf8');
      await require('node:fs').promises.writeFile(pageExcerptPath, (pageHtml || '').slice(0, 100000), 'utf8');
      await require('node:fs').promises.writeFile(browserStatePath, JSON.stringify({ openedSearchUrl, pageTitle, extractionStatus, extractedLinksCount: links.length }, null, 2), 'utf8');
    } catch {}
    try { await page.screenshot({ path: screenshotPath, fullPage: false }); } catch {}
    await browser.close();
    process.stdout.write(JSON.stringify({
      openedSearchUrl,
      pageTitle,
      extractedLinksCount: links.length,
      extractionStatus,
      debugArtifactPaths: [searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath],
      links
    }));
  } catch (error) {
    process.stderr.write(String(error && error.stack ? error.stack : error));
    process.exit(1);
  }
})();
""";
    }

    private static WebResearchImportCandidateRecord ToImportCandidate(BrowserResearchCandidate candidate) =>
        new(
            KnowledgeItemId: candidate.KnowledgeItemId,
            Title: candidate.Title,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: candidate.SourceType,
            ExcerptOrSummary: candidate.Snippet,
            RetrievedAtUtc: candidate.RetrievedAtUtc,
            EvidenceReason: candidate.Snippet,
            IndependenceClaim: "browser_research_candidate",
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags);

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var part in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(part, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? NormalizeBrowserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.Trim().Trim('"');
    }

    private static bool CheckPlaywrightAvailable(string nodeExecutable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nodeExecutable,
                ArgumentList = { "-e", "try { require('playwright'); process.exit(0); } catch { process.exit(1); }" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindBrowserBinary()
    {
        var explicitPath = NormalizeBrowserPath(Environment.GetEnvironmentVariable("HERMES_BROWSER_EXECUTABLE_PATH"));
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var candidates = new[] { "chromium", "chromium-browser", "google-chrome", "google-chrome-stable", "msedge" };
        foreach (var candidate in candidates)
        {
            var path = FindExecutable(candidate);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static bool IsBrokenSnapChromium(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Contains("/snap/bin/chromium", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Contains("/snap/", StringComparison.OrdinalIgnoreCase))
        {
            return RunProcessAndMatch(path, "--version", "snap", "yaml", "internal error");
        }

        return false;
    }

    private static bool RunProcessAndMatch(string fileName, string arguments, params string[] needles)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            var combined = string.Concat(output, "\n", error);
            return needles.Any(needle => combined.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void WriteReport(BrowserResearchAgentReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(BrowserResearchAgentReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Browser Research Agent Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Requests: {report.LoadedRequests}");
        sb.AppendLine($"- Skipped Due To Schema: {report.SkippedDueToSchema}");
        sb.AppendLine($"- Skipped Due To Status: {report.SkippedDueToStatus}");
        sb.AppendLine($"- Skipped Due To Missing Query: {report.SkippedDueToMissingQuery}");
        sb.AppendLine($"- Total Requests: {report.TotalRequests}");
        sb.AppendLine($"- Considered Requests: {report.ConsideredRequests}");
        sb.AppendLine($"- Fetched Candidates: {report.FetchedCandidates}");
        sb.AppendLine($"- Imported Candidates: {report.ImportedCandidates}");
        sb.AppendLine($"- Opened Search URL: {report.OpenedSearchUrl}");
        sb.AppendLine($"- Page Title: {report.PageTitle}");
        sb.AppendLine($"- Extracted Links Count: {report.ExtractedLinksCount}");
        sb.AppendLine($"- Extraction Status: {report.ExtractionStatus}");
        if (report.DebugArtifactPaths.Count > 0)
        {
            sb.AppendLine("- Debug Artifacts:");
            foreach (var artifact in report.DebugArtifactPaths)
            {
                sb.AppendLine($"  - {artifact}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        sb.AppendLine($"- research_only: {report.ResearchOnly}");
        if (report.Candidates.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Candidates");
            foreach (var candidate in report.Candidates.Take(20))
            {
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
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

    private sealed record BrowserSearchResult(string? Title, string? Url, string? Domain, string? Snippet);
    private sealed record WebResearchRequestsEnvelope(IReadOnlyList<WebResearchSourceRequest> Requests);
    private sealed record BrowserResearchRequestsLoadResult(
        IReadOnlyList<WebResearchSourceRequest> Requests,
        int LoadedRequests,
        int SkippedDueToSchema,
        int SkippedDueToStatus,
        int SkippedDueToMissingQuery,
        IReadOnlyList<string> Warnings);
    private sealed record BrowserSearchExecutionResult(
        string OpenedSearchUrl,
        string PageTitle,
        int ExtractedLinksCount,
        string ExtractionStatus,
        IReadOnlyList<string> DebugArtifactPaths,
        IReadOnlyList<BrowserSearchResult> Links)
    {
        public static BrowserSearchExecutionResult Blocked(
            string reason,
            string searchUrlPath,
            string pageTitlePath,
            string pageExcerptPath,
            string screenshotPath,
            string browserStatePath) =>
            new(
                OpenedSearchUrl: "-",
                PageTitle: "-",
                ExtractedLinksCount: 0,
                ExtractionStatus: reason,
                DebugArtifactPaths: [searchUrlPath, pageTitlePath, pageExcerptPath, screenshotPath, browserStatePath],
                Links: []);
    }
    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }
        return builder.ToString();
    }
}
