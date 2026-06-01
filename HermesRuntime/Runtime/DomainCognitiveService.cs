using System.Text.Json;

namespace Hermes.Runtime;

public sealed class DomainCognitiveService
{
    private readonly StoragePaths _storagePaths;

    public DomainCognitiveService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string DomainsRoot => Path.Combine(Root, "domains");

    public string DomainStatusPath => Path.Combine(Root, "domain_status.json");

    public string DomainInsightsPath => Path.Combine(Root, "domain_insights.json");

    public IReadOnlyList<DomainProfile> EnsureProfiles()
    {
        Directory.CreateDirectory(DomainsRoot);
        return DefaultProfiles()
            .Select(profile =>
            {
                var directory = DomainDirectory(profile.DomainId);
                Directory.CreateDirectory(directory);
                var existing = LoadProfile(profile.DomainId);
                var next = existing is null
                    ? profile
                    : profile with
                    {
                        CreatedAtUtc = existing.CreatedAtUtc,
                        LastScannedAtUtc = existing.LastScannedAtUtc,
                        Status = existing.Status
                    };
                WriteDomainFile(profile.DomainId, "domain_profile.json", next);
                WriteDomainFile(profile.DomainId, "domain_goals.json", DefaultGoals(profile.DomainId));
                WriteDomainFile(profile.DomainId, "knowledge_sources.json", DefaultSources(profile.DomainId));
                WriteDomainFile(profile.DomainId, "queue_rules.json", DefaultQueueRules(profile.DomainId));
                return next;
            })
            .ToList();
    }

    public DomainScanResult ScanDomain(string domain)
    {
        var normalized = NormalizeDomain(domain);
        EnsureProfiles();
        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var sources = DefaultSources(normalized).Sources
            .Select(source => source with { LastScannedAtUtc = now })
            .ToList();
        var items = normalized switch
        {
            "software" => ScanSoftwareDomain(warnings),
            "documentation" => ScanDocumentationDomain(warnings),
            "process" => ScanProcessDomain(warnings),
            "research" => ScanResearchDomain(warnings),
            "trading" => new TradingKnowledgeMapper(_storagePaths).MapPatternCatalog(),
            _ => []
        };
        var profile = (LoadProfile(normalized) ?? DefaultProfiles().First(item => item.DomainId == normalized)) with
        {
            LastScannedAtUtc = now,
            Status = items.Count == 0 ? "scanned_no_items" : "scanned"
        };
        WriteDomainFile(normalized, "domain_profile.json", profile);
        WriteDomainFile(normalized, "knowledge_sources.json", new DomainKnowledgeSources(normalized, sources));
        WriteDomainFile(normalized, "knowledge_items.json", items);
        var status = BuildStatus();
        var insights = BuildInsights(status);

        return new DomainScanResult(
            Domain: normalized,
            ScannedAtUtc: now,
            SourcesScanned: sources.Count,
            KnowledgeItems: items.Count,
            OutputPaths:
            [
                DomainFile(normalized, "domain_profile.json"),
                DomainFile(normalized, "knowledge_sources.json"),
                DomainFile(normalized, "knowledge_items.json"),
                DomainStatusPath,
                DomainInsightsPath
            ],
            Warnings: warnings
                .Concat(insights.Insights.Where(insight => insight.Domain == normalized && insight.Severity == "warning").Select(insight => insight.InsightId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    public DomainStatusReport BuildStatus()
    {
        var profiles = EnsureProfiles();
        var needs = new NeedDetectionEngine(_storagePaths).LoadNeeds();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var entries = profiles
            .Select(profile =>
            {
                var (sourceCount, effectiveLastScannedUtc, items) = DomainInventory(profile);
                var domainNeeds = needs
                    .Where(need => need.Domain.Equals(profile.DomainId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var openQueue = queue.Items.Count(item =>
                    item.Domain.Equals(profile.DomainId, StringComparison.OrdinalIgnoreCase)
                    && item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
                var warnings = new List<string>();
                if (profile.Active && effectiveLastScannedUtc is null)
                {
                    warnings.Add("domain_never_scanned");
                }

                if (profile.Active && items.Count == 0)
                {
                    warnings.Add("domain_knowledge_empty");
                }

                if (domainNeeds.Count > 0)
                {
                    warnings.Add("domain_open_needs");
                }

                return new DomainStatusEntry(
                    Domain: profile.DomainId,
                    Active: profile.Active,
                    LastScannedAtUtc: effectiveLastScannedUtc,
                    SourceCount: sourceCount,
                    KnowledgeItemCount: items.Count,
                    OpenNeeds: domainNeeds.Count,
                    OpenQueueItems: openQueue,
                    NextRecommendedTasks: NextTasksFor(profile.DomainId, warnings, domainNeeds),
                    Warnings: warnings);
            })
            .OrderByDescending(entry => entry.Active)
            .ThenBy(entry => entry.Domain, StringComparer.Ordinal)
            .ToList();
        var report = new DomainStatusReport(
            StatusVersion: "domain_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ActiveDomains: entries.Where(entry => entry.Active).Select(entry => entry.Domain).ToList(),
            Domains: entries,
            WeakDomains: entries
                .Where(entry => entry.Active && (entry.KnowledgeItemCount == 0 || entry.Warnings.Count > 0))
                .Select(entry => entry.Domain)
                .ToList(),
            StrongDomains: entries
                .Where(entry => entry.Active && entry.KnowledgeItemCount >= 3 && entry.Warnings.Count == 0)
                .Select(entry => entry.Domain)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        Directory.CreateDirectory(Root);
        File.WriteAllText(DomainStatusPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private (int SourceCount, DateTimeOffset? LastScannedAtUtc, IReadOnlyList<KnowledgeCatalogItem> Items) DomainInventory(DomainProfile profile)
    {
        if (profile.DomainId.Equals("trading", StringComparison.OrdinalIgnoreCase))
        {
            var sources = new KnowledgeSourceRegistry(_storagePaths)
                .LoadOrCreateSources()
                .Where(source => source.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var lastSourceScan = sources.Count == 0 ? (DateTimeOffset?)null : sources.Max(source => source.LastCheckedUtc);
            var items = new TradingKnowledgeMapper(_storagePaths).MapPatternCatalog();
            return (sources.Count, profile.LastScannedAtUtc ?? lastSourceScan, items);
        }

        var domainSources = LoadSources(profile.DomainId).Sources;
        var lastDomainSourceScan = domainSources
            .Where(source => source.LastScannedAtUtc is not null)
            .Select(source => source.LastScannedAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        if (lastDomainSourceScan == default)
        {
            lastDomainSourceScan = profile.LastScannedAtUtc ?? default;
        }

        return (
            domainSources.Count,
            profile.LastScannedAtUtc ?? (lastDomainSourceScan == default ? null : lastDomainSourceScan),
            LoadKnowledgeItems(profile.DomainId));
    }

    public DomainInsightsReport BuildInsights(DomainStatusReport? status = null)
    {
        status ??= BuildStatus();
        var insights = new List<DomainInsight>();
        foreach (var entry in status.Domains)
        {
            if (entry.KnowledgeItemCount == 0)
            {
                insights.Add(new DomainInsight(
                    InsightId: $"domain_insight_{entry.Domain}_knowledge_gap",
                    Domain: entry.Domain,
                    Severity: entry.Active ? "warning" : "info",
                    Title: $"{entry.Domain} knowledge gap",
                    Summary: "Domain has no structured knowledge items yet.",
                    EvidenceRefs: [DomainFile(entry.Domain, "knowledge_items.json")],
                    RecommendedTasks: [ScanTaskFor(entry.Domain), "generate_domain_insights"]));
            }

            if (entry.LastScannedAtUtc is null)
            {
                insights.Add(new DomainInsight(
                    InsightId: $"domain_insight_{entry.Domain}_missing_scan",
                    Domain: entry.Domain,
                    Severity: entry.Active ? "warning" : "info",
                    Title: $"{entry.Domain} was not scanned",
                    Summary: "Domain profile exists but no scan timestamp is available.",
                    EvidenceRefs: [DomainFile(entry.Domain, "domain_profile.json")],
                    RecommendedTasks: [ScanTaskFor(entry.Domain), "generate_domain_insights"]));
            }

            if (entry.OpenNeeds > 0)
            {
                insights.Add(new DomainInsight(
                    InsightId: $"domain_insight_{entry.Domain}_open_needs",
                    Domain: entry.Domain,
                    Severity: "info",
                    Title: $"{entry.Domain} has open needs",
                    Summary: $"Open domain needs: {entry.OpenNeeds}.",
                    EvidenceRefs: [new NeedDetectionEngine(_storagePaths).NeedsPath],
                    RecommendedTasks: entry.NextRecommendedTasks));
            }
        }

        var report = new DomainInsightsReport(
            StatusVersion: "domain_insights_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Insights: insights
                .GroupBy(insight => insight.InsightId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(insight => insight.Severity == "warning")
                .ThenBy(insight => insight.Domain, StringComparer.Ordinal)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(DomainInsightsPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public IReadOnlyList<KnowledgeCatalogItem> LoadAllDomainKnowledgeItems()
    {
        EnsureProfiles();
        return DefaultProfiles()
            .Where(profile => profile.DomainId != "trading")
            .SelectMany(profile => LoadKnowledgeItems(profile.DomainId))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }

    public DomainProfile? LoadProfile(string domain)
    {
        var path = DomainFile(NormalizeDomain(domain), "domain_profile.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DomainProfile>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public DomainKnowledgeSources LoadSources(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var path = DomainFile(normalized, "knowledge_sources.json");
        if (!File.Exists(path))
        {
            return DefaultSources(normalized);
        }

        try
        {
            return JsonSerializer.Deserialize<DomainKnowledgeSources>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? DefaultSources(normalized);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return DefaultSources(normalized);
        }
    }

    public IReadOnlyList<KnowledgeCatalogItem> LoadKnowledgeItems(string domain)
    {
        var path = DomainFile(NormalizeDomain(domain), "knowledge_items.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<KnowledgeCatalogItem>>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<CognitiveSource> DefaultCognitiveSources(DateTimeOffset timestampUtc)
    {
        return DefaultProfiles()
            .Where(profile => profile.DomainId != "trading")
            .SelectMany(profile => DefaultSources(profile.DomainId).Sources.Select(source => new CognitiveSource(
                SourceId: source.SourceId,
                SourceName: source.Description,
                UrlOrPath: source.PathOrUrl,
                Domain: source.Domain,
                SourceType: source.SourceType,
                TrustProfile: new SourceTrustProfile(source.TrustLevel, TrustScoreFor(source.TrustLevel), "local_project_metadata", source.RiskFlags),
                LastCheckedUtc: source.LastScannedAtUtc ?? timestampUtc,
                ExtractionStatus: "domain_source_registered",
                ExtractedConcepts: ConceptsForDomain(source.Domain),
                RiskFlags: source.RiskFlags)))
            .ToList();
    }

    private IReadOnlyList<KnowledgeCatalogItem> ScanSoftwareDomain(List<string> warnings)
    {
        var root = ResolveRepoRoot();
        var items = new List<KnowledgeCatalogItem>
        {
            Item("software", "architecture_decision", "Hermes Beta 3 Architecture", "Cognitive Core, Planner, Feedback Loop and Supervisor architecture are tracked as local architecture decisions.", ["architecture", "decision"]),
            Item("software", "test_command_dotnet_build_runtime", "Runtime Build Test", "dotnet build ./Hermes.Runtime.csproj", ["test_command", "dotnet"]),
            Item("software", "test_command_dotnet_build_cli", "CLI Build Test", "dotnet build ./cli/Hermes.Cli.csproj", ["test_command", "dotnet"])
        };

        var runtimeRoot = Path.Combine(root, "HermesRuntime", "Runtime");
        if (Directory.Exists(runtimeRoot))
        {
            items.AddRange(Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Take(40)
                .Select(path => Item(
                    "software",
                    $"code_module_{Path.GetFileNameWithoutExtension(path)}",
                    Path.GetFileName(path),
                    $"Runtime module discovered at {Relative(root, path)}.",
                    ["code_module", "runtime"])));
        }
        else
        {
            warnings.Add("runtime_source_root_missing");
        }

        var csproj = Path.Combine(root, "HermesRuntime", "Hermes.Runtime.csproj");
        if (File.Exists(csproj))
        {
            items.Add(Item("software", "dependency_runtime_project", "Hermes Runtime Project", "Runtime project file is tracked as dependency metadata.", ["dependency", "csproj"]));
        }

        return DistinctItems(items);
    }

    private IReadOnlyList<KnowledgeCatalogItem> ScanDocumentationDomain(List<string> warnings)
    {
        var root = ResolveRepoRoot();
        var docsRoot = Path.Combine(root, "docs");
        var items = new List<KnowledgeCatalogItem>();
        if (File.Exists(Path.Combine(root, "README.md")))
        {
            items.Add(Item("documentation", "project_readme", "Project README", "Root README is available for project orientation.", ["readme", "documentation"]));
        }
        else
        {
            warnings.Add("readme_missing");
        }

        if (Directory.Exists(docsRoot))
        {
            var markdownFiles = Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
                .Take(80)
                .ToList();
            items.AddRange(markdownFiles.Select(path => Item(
                "documentation",
                $"doc_{StableId(Relative(root, path))}",
                Path.GetFileName(path),
                $"Markdown document discovered at {Relative(root, path)}.",
                TagsForDocument(path))));
            if (!markdownFiles.Any(path => path.Contains("architecture", StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add("architecture_docs_missing");
            }

            var todoCount = markdownFiles.Sum(path => CountOccurrences(path, "TODO"));
            if (todoCount > 0)
            {
                items.Add(Item("documentation", "open_todos_in_docs", "Open TODOs in Docs", $"{todoCount} TODO markers found in markdown documentation.", ["todo", "documentation_gap"]));
            }
        }
        else
        {
            warnings.Add("docs_root_missing");
        }

        return DistinctItems(items);
    }

    private IReadOnlyList<KnowledgeCatalogItem> ScanProcessDomain(List<string> warnings)
    {
        return
        [
            Item("process", "workflow_codex_task", "Codex-Auftrag", "Standard workflow: read context, make scoped changes, run tests, summarize diff.", ["workflow", "codex"]),
            Item("process", "workflow_test_run", "Testlauf", "Run targeted build/test commands after code changes.", ["workflow", "test_command"]),
            Item("process", "workflow_git_commit", "Git Commit", "Human-controlled add/commit/push workflow; Codex must not push autonomously.", ["workflow", "git", "human_review"]),
            Item("process", "workflow_nightly_review", "Nightly Auswertung", "Review supervisor, scheduler, nightly, loop, feedback and storage reports after night runs.", ["workflow", "nightly"]),
            Item("process", "risk_point_secrets", "Secrets Risk Point", "Secrets and tokens must not be logged or committed.", ["risk_point", "secrets"]),
            Item("process", "automation_candidate_master_status", "Master Status CLI", "Candidate automation: one status command for supervisor, scheduler, loops, feedback, storage and cognitive core.", ["automation_candidate", "status"])
        ];
    }

    private IReadOnlyList<KnowledgeCatalogItem> ScanResearchDomain(List<string> warnings)
    {
        return
        [
            Item("research", "source_registry_curated_web", "Curated Web Source Registry", "Research sources are metadata-only and require human review before use.", ["source_registry", "web", "human_review"]),
            Item("research", "source_registry_github_metadata", "GitHub Repository Metadata", "Trusted repositories may be represented as metadata; foreign code is not executed.", ["source_registry", "github", "no_foreign_code_execution"]),
            Item("research", "local_research_notes", "Local Research Notes", "Local docs and notes can seed research hypotheses without external crawling.", ["local_notes", "research"]),
            Item("research", "research_safety_rule", "Research Safety Rule", "No unchecked crawler, no install, no execution, no secrets.", ["risk_point", "safety"]),
            Item("research", "workflow_weekly_summary", "Research Summary Workflow", "Future workflow candidate: curated summary of source changes, duplicate filtering and relevance scoring.", ["workflow", "summary"])
        ];
    }

    private static DomainKnowledgeSources DefaultSources(string domain)
    {
        IReadOnlyList<DomainKnowledgeSource> sources = domain switch
        {
            "software" =>
            [
                Source("software", "software_local_repo", "local_repo", "~/jarvis", "Local Jarvis repository", "local_trusted", ["no_shell_commands"]),
                Source("software", "software_readme", "local_file", "~/jarvis/README.md", "Root README", "local_trusted", []),
                Source("software", "software_arch_docs", "local_docs", "~/jarvis/docs/architecture", "Architecture documents", "local_trusted", []),
                Source("software", "software_git_metadata", "metadata", "local_git_metadata", "Git history metadata placeholder", "metadata_only", ["no_free_shell_commands"])
            ],
            "documentation" =>
            [
                Source("documentation", "documentation_architecture_docs", "local_docs", "~/jarvis/docs/architecture", "Architecture documentation", "local_trusted", []),
                Source("documentation", "documentation_readme", "local_file", "~/jarvis/README.md", "Project README", "local_trusted", []),
                Source("documentation", "documentation_markdown", "local_docs", "~/jarvis/docs", "Markdown documentation", "local_trusted", [])
            ],
            "process" =>
            [
                Source("process", "process_agents_rules", "local_file", "~/jarvis/AGENTS.md", "Codex/Jarvis development rules", "local_trusted", ["human_review_required"]),
                Source("process", "process_masterplan", "local_docs", "~/jarvis/docs", "Masterplan and process docs", "local_trusted", [])
            ],
            "research" =>
            [
                Source("research", "research_curated_web_metadata", "curated_metadata", "curated:web_sources", "Curated web source metadata", "curated_metadata", ["no_unchecked_crawlers"]),
                Source("research", "research_github_metadata", "curated_metadata", "curated:github_repositories", "GitHub metadata only", "metadata_only", ["no_foreign_code_execution"]),
                Source("research", "research_local_notes", "local_docs", "~/jarvis/docs", "Local research notes and docs", "local_trusted", [])
            ],
            _ => []
        };
        return new DomainKnowledgeSources(domain, sources);
    }

    private static IReadOnlyList<DomainProfile> DefaultProfiles()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            Profile("trading", "Trading Research", "Historical market research, strategy validation and quality gates.", now),
            Profile("software", "Software Engineering", "Local codebase structure, architecture decisions, tests and improvement candidates.", now),
            Profile("documentation", "Documentation", "Project documentation, masterplan consistency and documentation gaps.", now),
            Profile("process", "Process Improvement", "Recurring workflows, checklists, risk points and automation candidates.", now),
            Profile("research", "General Research", "Curated source metadata and reusable research workflows.", now)
        ];
    }

    private static DomainGoals DefaultGoals(string domain) =>
        new(domain, domain switch
        {
            "software" =>
            [
                Goal(domain, "software_architecture_map", "Keep local architecture and code-module knowledge current.", 10),
                Goal(domain, "software_test_knowledge", "Track important test commands and known issues.", 20)
            ],
            "documentation" =>
            [
                Goal(domain, "documentation_gap_detection", "Detect stale, missing or contradictory documentation.", 10),
                Goal(domain, "documentation_masterplan_alignment", "Keep docs aligned with Masterplan and TODO rules.", 20)
            ],
            "process" =>
            [
                Goal(domain, "process_workflow_memory", "Capture recurring workflows as reusable checklists.", 10),
                Goal(domain, "process_risk_reduction", "Surface risk points and safe automation candidates.", 20)
            ],
            "research" =>
            [
                Goal(domain, "research_source_registry", "Maintain curated metadata-only research sources.", 10),
                Goal(domain, "research_safety", "Keep external research read-only and review-gated.", 20)
            ],
            _ =>
            [
                Goal(domain, "trading_research_quality", "Improve historical research quality without market execution.", 10)
            ]
        });

    private static DomainQueueRules DefaultQueueRules(string domain) =>
        new(domain, domain switch
        {
            "software" =>
            [
                Rule(domain, "software_discovery", "discovery", "scan_software_domain", "normal"),
                Rule(domain, "software_review", "review", "generate_domain_insights", "normal")
            ],
            "documentation" =>
            [
                Rule(domain, "documentation_discovery", "discovery", "scan_documentation_domain", "normal"),
                Rule(domain, "documentation_review", "review", "generate_domain_insights", "normal")
            ],
            "process" =>
            [
                Rule(domain, "process_discovery", "discovery", "scan_process_domain", "normal"),
                Rule(domain, "process_review", "review", "generate_domain_insights", "normal")
            ],
            "research" =>
            [
                Rule(domain, "research_discovery", "discovery", "scan_research_domain", "normal"),
                Rule(domain, "research_review", "review", "generate_domain_insights", "normal")
            ],
            _ =>
            [
                Rule(domain, "trading_review", "review", "generate_domain_insights", "normal")
            ]
        });

    private string DomainDirectory(string domain) => Path.Combine(DomainsRoot, NormalizeDomain(domain));

    private string DomainFile(string domain, string fileName) => Path.Combine(DomainDirectory(domain), fileName);

    private void WriteDomainFile<T>(string domain, string fileName, T value)
    {
        var directory = DomainDirectory(domain);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(value, JsonDefaults.WriteOptions));
    }

    private static string NormalizeDomain(string domain) =>
        string.IsNullOrWhiteSpace(domain)
            ? "research"
            : domain.Trim().ToLowerInvariant();

    private static DomainProfile Profile(string id, string name, string description, DateTimeOffset now) =>
        new(id, name, Active: true, description, CreatedAtUtc: now, LastScannedAtUtc: null, Status: "active", Tags: [id], NoTradingExecution: true, NoBrokerAction: true, NoAutoTrading: true, HumanReviewRequired: true);

    private static DomainGoal Goal(string domain, string id, string description, int priority) =>
        new(id, domain, description, priority, "active");

    private static DomainKnowledgeSource Source(string domain, string id, string type, string path, string description, string trust, IReadOnlyList<string> risks) =>
        new(id, domain, type, path, description, trust, LastScannedAtUtc: null, RiskFlags: risks);

    private static DomainQueueRule Rule(string domain, string id, string queue, string taskType, string priority) =>
        new(id, domain, queue, taskType, priority, "no_free_shell_commands_no_trading_execution");

    private static KnowledgeCatalogItem Item(string domain, string id, string title, string description, IReadOnlyList<string> tags) =>
        new(
            Id: $"{domain}:{id}",
            Domain: domain,
            Title: title,
            DescriptionShort: description,
            SourceIds: [$"{domain}_domain_scan"],
            Confidence: 0.58,
            ValidationStatus: "needs_review",
            Tags: tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            LastValidatedUtc: null,
            RelatedItems: []);

    private static IReadOnlyList<KnowledgeCatalogItem> DistinctItems(IEnumerable<KnowledgeCatalogItem> items) =>
        items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> NextTasksFor(string domain, IReadOnlyList<string> warnings, IReadOnlyList<DetectedNeed> needs)
    {
        var tasks = new List<string>();
        if (warnings.Contains("domain_never_scanned") || warnings.Contains("domain_knowledge_empty"))
        {
            tasks.Add(ScanTaskFor(domain));
        }

        tasks.AddRange(needs.SelectMany(need => need.SuggestedTaskTypes));
        tasks.Add("generate_domain_insights");
        return tasks.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }

    private static string ScanTaskFor(string domain) =>
        domain switch
        {
            "software" => "scan_software_domain",
            "documentation" => "scan_documentation_domain",
            "process" => "scan_process_domain",
            "research" => "scan_research_domain",
            _ => "scan_knowledge_sources"
        };

    private static IReadOnlyList<string> TagsForDocument(string path)
    {
        var tags = new List<string> { "documentation" };
        if (path.Contains("architecture", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("architecture");
        }

        if (path.Contains("todo", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("todo");
        }

        if (path.Contains("masterplan", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("masterplan");
        }

        return tags;
    }

    private static IReadOnlyList<string> ConceptsForDomain(string domain) =>
        domain switch
        {
            "software" => ["architecture", "code_module", "test_command", "known_issue"],
            "documentation" => ["docs", "masterplan", "todo", "architecture"],
            "process" => ["workflow", "checklist", "risk_point", "automation_candidate"],
            "research" => ["curated_sources", "metadata", "source_safety"],
            _ => ["trading", "research"]
        };

    private static double TrustScoreFor(string trust) =>
        trust switch
        {
            "local_trusted" => 0.82,
            "curated_metadata" => 0.68,
            "metadata_only" => 0.58,
            _ => 0.5
        };

    private static int CountOccurrences(string path, string pattern)
    {
        try
        {
            return File.ReadLines(path).Count(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "HermesRuntime")))
            {
                return directory.FullName;
            }

            if (directory.Name.Equals("HermesRuntime", StringComparison.OrdinalIgnoreCase)
                && directory.Parent is not null)
            {
                return directory.Parent.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string Relative(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static string StableId(string text)
    {
        var chars = text
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }
}
