using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        foreach (var request in requests)
        {
            var fetched = FetchCandidatesForRequest(request, runtime).Take(5).ToList();
            if (fetched.Count == 0)
            {
                warnings.Add($"no_browser_results_for:{request.RequestId}");
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

    private IEnumerable<BrowserResearchCandidate> FetchCandidatesForRequest(WebResearchSourceRequest request, BrowserResearchRuntimeStatus runtime)
    {
        var results = RunBrowserSearch(request.Query, request.RecommendedSourceDomains, 5);
        var now = DateTimeOffset.UtcNow;
        return results.Select(result => new BrowserResearchCandidate(
            KnowledgeItemId: request.KnowledgeItemId,
            Title: result.Title,
            Url: result.Url,
            Domain: result.Domain,
            Snippet: result.Snippet,
            SourceType: "browser_research_candidate",
            HumanReviewStatus: "pending",
            SafetyFlags: ["no_trading_execution", "human_review_required"],
            RetrievedAtUtc: now));
    }

    private static IReadOnlyList<(string Title, string Url, string Domain, string Snippet)> RunBrowserSearch(string query, IReadOnlyList<string> allowedDomains, int maxResults)
    {
        var script = BuildBrowserScript(query, allowedDomains, maxResults);
        var node = FindExecutable("node");
        if (node is null)
        {
            return [];
        }

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
                return [];
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return [];
            }

            return JsonSerializer.Deserialize<IReadOnlyList<BrowserSearchResult>>(output, JsonDefaults.SnapshotReadOptions)
                ?.Select(result => (result.Title ?? string.Empty, result.Url ?? string.Empty, result.Domain ?? string.Empty, result.Snippet ?? string.Empty))
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildBrowserScript(string query, IReadOnlyList<string> allowedDomains, int maxResults)
    {
        var allowed = JsonSerializer.Serialize(allowedDomains ?? [], JsonDefaults.WriteOptions);
        var encodedQuery = JsonSerializer.Serialize(query, JsonDefaults.WriteOptions);
        return $$"""
const allowedDomains = {{allowed}};
const query = {{encodedQuery}};
const maxResults = {{maxResults}};
(async () => {
  try {
    const { chromium } = require('playwright');
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    const url = 'https://duckduckgo.com/?q=' + encodeURIComponent(query);
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 15000 });
    await page.waitForTimeout(1200);
    const results = await page.evaluate((maxResults, allowedDomains) => {
      const normalizeDomain = (value) => {
        try { return new URL(value).hostname.replace(/^www\./, '').toLowerCase(); } catch { return ''; }
      };
      const allowed = (allowedDomains || []).map((value) => String(value || '').trim().toLowerCase()).filter(Boolean);
      const nodes = Array.from(document.querySelectorAll('a[data-testid="result-title-a"], article a[href], h2 a[href]'));
      const seen = new Set();
      const items = [];
      for (const anchor of nodes) {
        const href = anchor.href || '';
        if (!href || seen.has(href)) continue;
        const domain = normalizeDomain(href);
        if (!domain) continue;
        if (allowed.length > 0 && !allowed.some((allowedDomain) => domain === allowedDomain || domain.endsWith('.' + allowedDomain))) continue;
        const title = (anchor.innerText || anchor.textContent || '').trim();
        const snippet = (anchor.closest('article')?.innerText || anchor.parentElement?.innerText || '').trim();
        seen.add(href);
        items.push({ title, url: href, domain, snippet: snippet.slice(0, 400) });
        if (items.length >= maxResults) break;
      }
      return items;
    }, maxResults, allowedDomains);
    await browser.close();
    process.stdout.write(JSON.stringify(results));
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
}
