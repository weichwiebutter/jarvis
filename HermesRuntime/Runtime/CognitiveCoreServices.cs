using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CognitiveCoreService
{
    private readonly StoragePaths _storagePaths;

    public CognitiveCoreService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string MemoryRoot => Path.Combine(Root, "memory");

    public string InsightsRoot => Path.Combine(Root, "insights");

    public string RoleOutputsRoot => Path.Combine(Root, "role_outputs");

    public string StatusPath => Path.Combine(Root, "cognitive_status.json");

    public CognitiveStatus BuildStatus()
    {
        EnsureDirectories();
        var sourceRegistry = new KnowledgeSourceRegistry(_storagePaths);
        var sources = sourceRegistry.LoadOrCreateSources();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var insights = new HypothesisGenerator(_storagePaths).LoadInsights();
        var memory = new TradingDomainAdapter(_storagePaths).SyncMemory();

        var status = new CognitiveStatus(
            StatusVersion: "cognitive_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Domains: Domains(),
            SourceCount: sources.Count,
            KnowledgeItemCount: catalog.Count,
            QueueItemCount: queue.Items.Count,
            InsightCount: insights.Count,
            MemoryEntryCount: memory.Count,
            ActiveDomains: ["trading"],
            NextActions:
            [
                "scan_knowledge_sources",
                "process_research_queue",
                "generate_cognitive_insights",
                "keep_trading_research_inside_no_auto_trading_safety"
            ],
            CognitiveRoot: Root,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    public static IReadOnlyList<CognitiveDomain> Domains() =>
    [
        new("trading", "Trading Research", Active: true, Status: "active_domain_1"),
        new("software", "Software Engineering", Active: false, Status: "planned"),
        new("documentation", "Documentation", Active: false, Status: "planned"),
        new("process", "Process Improvement", Active: false, Status: "planned"),
        new("research", "General Research", Active: false, Status: "planned")
    ];

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(MemoryRoot);
        Directory.CreateDirectory(InsightsRoot);
        Directory.CreateDirectory(RoleOutputsRoot);
    }
}

public sealed class KnowledgeSourceRegistry
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeSourceRegistry(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string SourcesPath => Path.Combine(Root, "knowledge_sources.json");

    public IReadOnlyList<CognitiveSource> LoadOrCreateSources()
    {
        Directory.CreateDirectory(Root);
        var existing = LoadSources();
        if (existing.Count > 0)
        {
            return existing;
        }

        var sources = DefaultSources(DateTimeOffset.UtcNow);
        File.WriteAllText(SourcesPath, JsonSerializer.Serialize(sources, JsonDefaults.WriteOptions));
        return sources;
    }

    public IReadOnlyList<CognitiveSource> ScanSources()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var sources = DefaultSources(now)
            .Select(source => source with
            {
                LastCheckedUtc = now,
                ExtractionStatus = source.SourceType == "trusted_code_repository"
                    ? "metadata_snapshot_only_no_code_execution"
                    : "metadata_extracted",
                RiskFlags = source.RiskFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .OrderBy(source => source.Domain, StringComparer.Ordinal)
            .ThenBy(source => source.SourceId, StringComparer.Ordinal)
            .ToList();

        File.WriteAllText(SourcesPath, JsonSerializer.Serialize(sources, JsonDefaults.WriteOptions));
        new ScoutRole(_storagePaths).WriteSourceScanOutput(sources);
        return sources;
    }

    public IReadOnlyList<CognitiveSource> LoadSources()
    {
        if (!File.Exists(SourcesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<CognitiveSource>>(
                File.ReadAllText(SourcesPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<CognitiveSource> DefaultSources(DateTimeOffset timestampUtc)
    {
        var tradingDe = TradingDeKnowledgeCatalog.Sources()
            .Select(source => new CognitiveSource(
                SourceId: source.SourceId,
                SourceName: source.SourceName,
                UrlOrPath: source.SourceUrl,
                Domain: "trading",
                SourceType: source.Category,
                TrustProfile: Trust(source.SourceTrust, 0.72, "public_education_source_link_required", []),
                LastCheckedUtc: timestampUtc,
                ExtractionStatus: "curated_metadata_available",
                ExtractedConcepts: source.ExtractedConcepts,
                RiskFlags: []));

        var trusted = new[]
        {
            TrustedSource("spotware_github", "Spotware GitHub", "https://github.com/spotware", "trusted_code_repository", ["ctrader", "csharp", "samples"], timestampUtc),
            TrustedSource("spotware_ctrader_algo_samples", "cTrader Algo Samples", "https://github.com/spotware/ctrader-algo-samples", "trusted_code_repository", ["ctrader", "csharp", "indicator", "cbot"], timestampUtc),
            TrustedSource("local_hermes_files", "Local Hermes Files", "local:/mnt/d/HermesData", "local_files", ["reports", "research_memory", "strategy_research"], timestampUtc)
        };

        return tradingDe
            .Concat(trusted)
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .ToList();
    }

    private static CognitiveSource TrustedSource(
        string id,
        string name,
        string url,
        string sourceType,
        IReadOnlyList<string> concepts,
        DateTimeOffset timestampUtc) =>
        new(
            SourceId: id,
            SourceName: name,
            UrlOrPath: url,
            Domain: "trading",
            SourceType: sourceType,
            TrustProfile: Trust("trusted_reference_metadata", 0.78, "license_review_required_before_reuse", ["no_foreign_code_execution"]),
            LastCheckedUtc: timestampUtc,
            ExtractionStatus: "metadata_only",
            ExtractedConcepts: concepts,
            RiskFlags: ["no_foreign_code_execution", "license_review_required"]);

    private static SourceTrustProfile Trust(string level, double score, string license, IReadOnlyList<string> riskFlags) =>
        new(level, score, license, riskFlags);
}

public sealed class KnowledgeSourceScout
{
    private readonly KnowledgeSourceRegistry _registry;

    public KnowledgeSourceScout(StoragePaths storagePaths)
    {
        _registry = new KnowledgeSourceRegistry(storagePaths);
    }

    public IReadOnlyList<CognitiveSource> Scan() => _registry.ScanSources();
}

public sealed class KnowledgeCatalog
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeCatalog(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string CatalogPath => Path.Combine(Root, "knowledge_catalog.json");

    public IReadOnlyList<KnowledgeCatalogItem> LoadOrCreateItems()
    {
        Directory.CreateDirectory(Root);
        var items = new TradingKnowledgeMapper(_storagePaths).MapPatternCatalog();
        File.WriteAllText(CatalogPath, JsonSerializer.Serialize(items, JsonDefaults.WriteOptions));
        new AnalystRole(_storagePaths).WriteCatalogOutput(items);
        return items;
    }

    public KnowledgeCatalogItem? FindById(string id) =>
        LoadOrCreateItems().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

public sealed class TradingKnowledgeMapper
{
    private readonly StoragePaths _storagePaths;

    public TradingKnowledgeMapper(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<KnowledgeCatalogItem> MapPatternCatalog()
    {
        var sources = new KnowledgeSourceRegistry(_storagePaths)
            .LoadOrCreateSources()
            .ToList();
        var patterns = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        return patterns
            .Select(pattern =>
            {
                var sourceIds = SourceIdsFor(pattern, sources);
                return new KnowledgeCatalogItem(
                    Id: $"trading:{pattern.Id}",
                    Domain: "trading",
                    Title: pattern.Name,
                    DescriptionShort: pattern.DescriptionShort ?? pattern.Description,
                    SourceIds: sourceIds,
                    Confidence: ConfidenceFor(pattern),
                    ValidationStatus: "needs_more_data",
                    Tags: pattern.Tags.Select(tag => tag.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    LastValidatedUtc: null,
                    RelatedItems: RelatedItems(pattern, patterns));
            })
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> SourceIdsFor(
        StrategyPatternDefinition pattern,
        IReadOnlyList<CognitiveSource> sources)
    {
        if (string.IsNullOrWhiteSpace(pattern.SourceUrl))
        {
            return ["local_hermes_files"];
        }

        var matches = sources
            .Where(source => source.UrlOrPath.Equals(pattern.SourceUrl, StringComparison.OrdinalIgnoreCase))
            .Select(source => source.SourceId)
            .ToList();
        return matches.Count == 0 ? ["local_hermes_files"] : matches;
    }

    private static double ConfidenceFor(StrategyPatternDefinition pattern)
    {
        var baseScore = pattern.SourceTrust?.Equals("curated_public_education", StringComparison.OrdinalIgnoreCase) == true ? 0.62 : 0.48;
        var priorityBonus = pattern.TestPriority?.Equals("high", StringComparison.OrdinalIgnoreCase) == true ? 0.08 : 0;
        return Math.Round(Math.Clamp(baseScore + priorityBonus, 0, 1), 4);
    }

    private static IReadOnlyList<string> RelatedItems(
        StrategyPatternDefinition pattern,
        IReadOnlyList<StrategyPatternDefinition> all)
    {
        var family = StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id);
        return all
            .Where(other => !other.Id.Equals(pattern.Id, StringComparison.OrdinalIgnoreCase)
                && StrategyPatternCatalog.StrategyFamilyForPattern(other.Id).Equals(family, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .Select(other => $"trading:{other.Id}")
            .ToList();
    }
}

public sealed class ResearchQueueService
{
    private readonly StoragePaths _storagePaths;

    public ResearchQueueService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string QueuePath => Path.Combine(Root, "research_queue.json");

    public ResearchQueue LoadOrCreateQueue()
    {
        Directory.CreateDirectory(Root);
        var existing = LoadQueue();
        if (existing is not null)
        {
            return existing;
        }

        var items = new KnowledgeCatalog(_storagePaths)
            .LoadOrCreateItems()
            .Take(12)
            .Select(item => NewItem("trading", "validation", ResearchPriority.Normal, [item.Id], "cognitive_core_bootstrap", ["validate imported trading knowledge with existing Beta 3 gates"]))
            .ToList();
        return Write(items);
    }

    public ResearchQueue Enqueue(string domain, string type, ResearchPriority priority = ResearchPriority.Normal, IReadOnlyList<string>? sourceRefs = null)
    {
        var queue = LoadOrCreateQueue();
        var items = queue.Items.ToList();
        items.Add(NewItem(domain, type, priority, sourceRefs ?? [], "cli", ["manual_enqueue_research"]));
        return Write(items);
    }

    public ResearchQueue EnqueuePlannedTasks(IReadOnlyList<PlannedTask> tasks)
    {
        var queue = LoadOrCreateQueue();
        var items = queue.Items.ToList();
        var existing = items
            .SelectMany(item => item.Notes)
            .Where(note => note.StartsWith("planned_task:", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var task in tasks)
        {
            var marker = $"planned_task:{task.TaskId}";
            if (existing.Contains(marker))
            {
                continue;
            }

            items.Add(NewItem(
                task.Domain,
                task.TaskType,
                PriorityFor(task.Priority.TotalScore),
                task.SourceRefs.Concat([task.NeedId, task.GoalId]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                "autonomous_planning_engine",
                [
                    marker,
                    $"goal:{task.GoalId}",
                    $"need:{task.NeedId}",
                    $"queue:{task.QueueType}",
                    $"reason:{task.Reason}",
                    $"expected_outcome:{task.ExpectedOutcome}",
                    $"priority:{task.Priority.TotalScore:0.####}",
                    "no_trading_execution",
                    "human_review_required"
                ]));
            existing.Add(marker);
        }

        return Write(items);
    }

    public ResearchQueue Process(int maxItems)
    {
        return ProcessWhere(maxItems, _ => true, "processed_by_cognitive_queue_no_trading_execution");
    }

    public ResearchQueue ProcessNonPlannedItems(int maxItems)
    {
        return ProcessWhere(
            maxItems,
            item => !item.Notes.Any(note => note.StartsWith("planned_task:", StringComparison.OrdinalIgnoreCase)),
            "processed_by_planned_task_executor_no_trading_execution");
    }

    public ResearchQueue MarkPlannedTaskExecution(
        string plannedTaskId,
        string status,
        string reason,
        IReadOnlyList<string> warnings)
    {
        var queue = LoadOrCreateQueue();
        var now = DateTimeOffset.UtcNow;
        var items = queue.Items
            .Select(item =>
            {
                var matches = item.Notes.Any(note =>
                    note.Equals($"planned_task:{plannedTaskId}", StringComparison.OrdinalIgnoreCase));
                if (!matches)
                {
                    return item;
                }

                var nextQueue = status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                    ? "archive"
                    : status.Equals("failed", StringComparison.OrdinalIgnoreCase) || status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
                        ? "review"
                        : item.Queue;
                return item with
                {
                    Status = status,
                    Queue = nextQueue,
                    UpdatedAtUtc = now,
                    Notes = item.Notes
                        .Concat([
                            $"execution_status:{status}",
                            $"execution_reason:{reason}",
                            $"execution_updated_utc:{now:O}"
                        ])
                        .Concat(warnings.Select(warning => $"execution_warning:{warning}"))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .ToList();
        return Write(items);
    }

    private ResearchQueue ProcessWhere(
        int maxItems,
        Func<ResearchQueueItem, bool> predicate,
        string processedNote)
    {
        maxItems = Math.Clamp(maxItems, 1, 500);
        var queue = LoadOrCreateQueue();
        var now = DateTimeOffset.UtcNow;
        var processed = 0;
        var items = queue.Items
            .Select(item =>
            {
                if (processed >= maxItems
                    || !item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                    || !predicate(item))
                {
                    return item;
                }

                processed++;
                new ValidatorRole(_storagePaths).WriteQueueValidationOutput(item);
                new CriticRole(_storagePaths).WriteQueueCriticOutput(item);
                return item with
                {
                    Status = "processed",
                    Queue = item.Type.Equals("review", StringComparison.OrdinalIgnoreCase) ? "review" : "archive",
                    UpdatedAtUtc = now,
                    Notes = item.Notes.Concat([processedNote]).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };
            })
            .ToList();
        return Write(items);
    }

    private ResearchQueue? LoadQueue()
    {
        if (!File.Exists(QueuePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResearchQueue>(
                File.ReadAllText(QueuePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private ResearchQueue Write(IReadOnlyList<ResearchQueueItem> items)
    {
        Directory.CreateDirectory(Root);
        var queue = new ResearchQueue(
            QueueVersion: "research_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Items: items,
            NoTradingExecution: true,
            HumanReviewRequired: true);
        File.WriteAllText(QueuePath, JsonSerializer.Serialize(queue, JsonDefaults.WriteOptions));
        return queue;
    }

    private static ResearchQueueItem NewItem(
        string domain,
        string type,
        ResearchPriority priority,
        IReadOnlyList<string> sourceRefs,
        string requestedBy,
        IReadOnlyList<string> notes) =>
        new(
            QueueItemId: $"research_queue_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            Domain: domain,
            Queue: QueueForType(type),
            Type: type,
            Priority: priority,
            Status: "open",
            SourceRefs: sourceRefs,
            RequestedBy: requestedBy,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: null,
            Notes: notes,
            NoTradingExecution: true,
            HumanReviewRequired: true);

    private static string QueueForType(string type) =>
        type.ToLowerInvariant() switch
        {
            "discovery" => "discovery",
            "scan_knowledge_sources" => "discovery",
            "download_missing_market_data" => "discovery",
            "simulation" => "simulation",
            "run_strategy_research" => "simulation",
            "review" => "review",
            "run_realism_report" => "review",
            "run_overfit_report" => "review",
            "run_storage_hygiene" => "review",
            "archive" => "archive",
            _ => "validation"
        };

    private static ResearchPriority PriorityFor(double score) =>
        score switch
        {
            >= 0.85 => ResearchPriority.Critical,
            >= 0.70 => ResearchPriority.High,
            >= 0.45 => ResearchPriority.Normal,
            _ => ResearchPriority.Low
        };
}

public sealed class TradingDomainAdapter
{
    private readonly StoragePaths _storagePaths;

    public TradingDomainAdapter(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<CognitiveMemoryEntry> SyncMemory()
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "memory");
        Directory.CreateDirectory(root);
        var insights = new ResearchInsightsGenerator(_storagePaths).LoadInsights();
        var entries = new List<CognitiveMemoryEntry>
        {
            new(
                EntryId: "trading_memory_pattern_catalog",
                Domain: "trading",
                EntryType: "knowledge_catalog_sync",
                Summary: "Trading pattern catalog mirrored into Cognitive Core knowledge catalog.",
                SourceRefs: ["strategy_research/pattern_catalog.json"],
                Status: "needs_more_data",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                HumanReviewRequired: true)
        };

        if (insights is not null)
        {
            entries.Add(new(
                EntryId: "trading_memory_research_insights",
                Domain: "trading",
                EntryType: "research_insight_sync",
                Summary: $"Trading research insights mirrored: top={insights.TopStrategies.Count}, overfit={insights.OverfitSuspectedStrategies?.Count ?? 0}, bot_candidates={insights.BotCandidateCount ?? 0}.",
                SourceRefs: ["strategy_research/research_insights.json"],
                Status: "active_reference",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                HumanReviewRequired: true));
        }

        File.WriteAllText(Path.Combine(root, "cognitive_memory.json"), JsonSerializer.Serialize(entries, JsonDefaults.WriteOptions));
        return entries;
    }
}

public sealed class TradingValidationAdapter
{
    private readonly StoragePaths _storagePaths;

    public TradingValidationAdapter(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<CognitiveValidationResult> MirrorValidationResults()
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "memory");
        Directory.CreateDirectory(root);
        var insights = new ResearchInsightsGenerator(_storagePaths).LoadInsights();
        var results = new List<CognitiveValidationResult>
        {
            new(
                ValidationId: "trading_validation_beta3_gate_status",
                Domain: "trading",
                ItemOrHypothesisId: "trading:*",
                Status: insights?.BotCandidateCount > 0 ? "promising" : "needs_more_data",
                Validation: new ValidationScore(
                    insights?.BotCandidateCount > 0 ? 0.6 : 0.25,
                    insights?.BotCandidateCount > 0 ? "promising" : "needs_more_data",
                    ["strategy_research/research_insights.json"]),
                EvidenceRefs: ["strategy_research/research_insights.json"],
                RiskFlags: ["no_auto_trading", "human_review_required"],
                CreatedAtUtc: DateTimeOffset.UtcNow)
        };

        File.WriteAllText(Path.Combine(root, "validation_results.json"), JsonSerializer.Serialize(results, JsonDefaults.WriteOptions));
        return results;
    }
}

public sealed class KnowledgeRelationEngine
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeRelationEngine(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<CrossKnowledgeCandidate> BuildCandidates(string domain)
    {
        var items = new KnowledgeCatalog(_storagePaths)
            .LoadOrCreateItems()
            .Where(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var pairs = new (string First, string Second, string Title, string Plan)[]
        {
            ("breakout", "session", "Breakout plus Session Filter", "validate breakout only in London/New-York sessions with spread filter"),
            ("ema_pullback", "volatility", "EMA Pullback plus Volatility Filter", "validate pullbacks only when ATR is above minimum percentile"),
            ("engulfing", "trend", "Engulfing plus Trend Context", "validate engulfing only when higher-timeframe trend agrees"),
            ("liquidity", "reversal", "Liquidity Sweep plus Reversal Filter", "validate sweep only after reclaim candle and regime filter")
        };

        return pairs
            .Select(pair =>
            {
                var matched = items
                    .Where(item => item.Id.Contains(pair.First, StringComparison.OrdinalIgnoreCase)
                        || item.Id.Contains(pair.Second, StringComparison.OrdinalIgnoreCase)
                        || item.Tags.Any(tag => tag.Contains(pair.First, StringComparison.OrdinalIgnoreCase)
                            || tag.Contains(pair.Second, StringComparison.OrdinalIgnoreCase)))
                    .Take(4)
                    .ToList();
                return matched.Count < 2
                    ? null
                    : new CrossKnowledgeCandidate(
                        CandidateId: $"cross_{domain}_{pair.First}_{pair.Second}",
                        Domain: domain,
                        ItemIds: matched.Select(item => item.Id).ToList(),
                        Combination: $"{pair.First}+{pair.Second}",
                        HypothesisTitle: pair.Title,
                        ValidationPlan: pair.Plan,
                        ExpectedReuseScore: Math.Round(matched.Average(item => item.Confidence), 4));
            })
            .Where(candidate => candidate is not null)
            .Cast<CrossKnowledgeCandidate>()
            .ToList();
    }
}

public sealed class HypothesisGenerator
{
    private readonly StoragePaths _storagePaths;

    public HypothesisGenerator(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string InsightsRoot => Path.Combine(Root, "insights");

    public string HypothesesPath => Path.Combine(InsightsRoot, "hypotheses.json");

    public string InsightsPath => Path.Combine(Root, "cognitive_insights.json");

    public IReadOnlyList<CognitiveHypothesis> Generate(string domain)
    {
        Directory.CreateDirectory(InsightsRoot);
        var candidates = new KnowledgeRelationEngine(_storagePaths).BuildCandidates(domain);
        var hypotheses = candidates
            .Select(candidate => new CognitiveHypothesis(
                HypothesisId: $"hypothesis_{candidate.CandidateId}",
                Domain: candidate.Domain,
                Title: candidate.HypothesisTitle,
                Description: $"Combine {candidate.Combination} and validate with existing Beta 3 research gates.",
                SourceItemIds: candidate.ItemIds,
                ProposedValidation: candidate.ValidationPlan,
                Status: "untested",
                Trust: new TrustScore(Math.Clamp(candidate.ExpectedReuseScore, 0, 1), "needs_validation", ["source_items_curated_or_local"]),
                Evidence: new EvidenceScore(0.35, "early_hypothesis", candidate.ItemIds),
                HumanReviewRequired: true))
            .ToList();

        File.WriteAllText(HypothesesPath, JsonSerializer.Serialize(hypotheses, JsonDefaults.WriteOptions));
        WriteInsights(domain, hypotheses);
        new SummarizerRole(_storagePaths).WriteHypothesisSummary(hypotheses);
        return hypotheses;
    }

    public IReadOnlyList<CognitiveInsight> LoadInsights()
    {
        if (!File.Exists(InsightsPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<CognitiveInsight>>(
                File.ReadAllText(InsightsPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private void WriteInsights(string domain, IReadOnlyList<CognitiveHypothesis> hypotheses)
    {
        var insights = hypotheses
            .Select(hypothesis => new CognitiveInsight(
                InsightId: $"insight_{hypothesis.HypothesisId}",
                Domain: domain,
                Title: hypothesis.Title,
                Summary: hypothesis.Description,
                EvidenceRefs: hypothesis.SourceItemIds,
                RecommendedActions: [
                    "enqueue_research_validation",
                    "run_existing_trading_quality_gates",
                    "keep_human_review_required"
                ],
                Status: "hypothesis_ready_for_validation",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                NoTradingExecution: true,
                HumanReviewRequired: true))
            .ToList();

        File.WriteAllText(InsightsPath, JsonSerializer.Serialize(insights, JsonDefaults.WriteOptions));
    }
}

public sealed class CognitiveNightlyService
{
    private readonly StoragePaths _storagePaths;

    public CognitiveNightlyService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string SummaryPath => Path.Combine(Root, "nightly_cognitive_summary.json");

    public NightlyCognitiveSummary Run(int maxQueueItems = 20)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var sourcesScanned = 0;
        var knowledgeItems = 0;
        var queueItemsProcessed = 0;
        var queuedResearchItems = 0;
        var hypothesesGenerated = 0;
        var insightsGenerated = 0;
        IReadOnlyList<string> activeDomains = ["trading"];
        DateTimeOffset? lastKnowledgeScanUtc = null;
        DateTimeOffset? lastQueueProcessedUtc = null;
        DateTimeOffset? lastCognitiveInsightsUtc = null;
        string? lastError = null;
        var status = "completed";

        try
        {
            var sources = new KnowledgeSourceScout(_storagePaths).Scan();
            sourcesScanned = sources.Count;
            lastKnowledgeScanUtc = now;
            if (sources.Count == 0)
            {
                warnings.Add("no_knowledge_sources_scanned");
            }

            var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
            knowledgeItems = catalog.Count;
            if (catalog.Count == 0)
            {
                warnings.Add("knowledge_catalog_empty");
            }

            var queueService = new ResearchQueueService(_storagePaths);
            var queueBefore = queueService.LoadOrCreateQueue();
            var processedBefore = queueBefore.Items
                .Where(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.QueueItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var queueAfter = queueService.Process(maxQueueItems);
            queueItemsProcessed = queueAfter.Items.Count(item =>
                item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)
                && !processedBefore.Contains(item.QueueItemId));
            queuedResearchItems = queueAfter.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
            lastQueueProcessedUtc = DateTimeOffset.UtcNow;
            if (queueItemsProcessed == 0)
            {
                warnings.Add("no_open_research_queue_items_processed");
            }

            var hypotheses = new HypothesisGenerator(_storagePaths).Generate("trading");
            hypothesesGenerated = hypotheses.Count;
            var insights = new HypothesisGenerator(_storagePaths).LoadInsights();
            insightsGenerated = insights.Count;
            lastCognitiveInsightsUtc = DateTimeOffset.UtcNow;
            if (hypotheses.Count == 0)
            {
                warnings.Add("no_cognitive_hypotheses_generated");
            }

            var cognitiveStatus = new CognitiveCoreService(_storagePaths).BuildStatus();
            activeDomains = cognitiveStatus.ActiveDomains;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            status = "completed_with_warnings";
            lastError = ex.Message;
            warnings.Add($"cognitive_nightly_error:{ex.GetType().Name}");
        }

        var summary = new NightlyCognitiveSummary(
            SummaryVersion: "nightly_cognitive_summary_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            SourcesScanned: sourcesScanned,
            KnowledgeItems: knowledgeItems,
            QueueItemsProcessed: queueItemsProcessed,
            QueuedResearchItems: queuedResearchItems,
            HypothesesGenerated: hypothesesGenerated,
            InsightsGenerated: insightsGenerated,
            ActiveDomains: activeDomains,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            LastKnowledgeScanUtc: lastKnowledgeScanUtc,
            LastQueueProcessedUtc: lastQueueProcessedUtc,
            LastCognitiveInsightsUtc: lastCognitiveInsightsUtc,
            LastError: lastError,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(SummaryPath, JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions));
        return summary;
    }

    public NightlyCognitiveSummary? LoadSummary()
    {
        if (!File.Exists(SummaryPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NightlyCognitiveSummary>(
                File.ReadAllText(SummaryPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}

public sealed class DomainKnowledgeAdapter
{
    private readonly StoragePaths _storagePaths;

    public DomainKnowledgeAdapter(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<KnowledgeCatalogItem> SyncDomain(string domain) =>
        domain.Equals("trading", StringComparison.OrdinalIgnoreCase)
            ? new KnowledgeCatalog(_storagePaths).LoadOrCreateItems()
            : [];
}

public sealed class ScoutRole
{
    private readonly StoragePaths _storagePaths;

    public ScoutRole(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public RoleOutput WriteSourceScanOutput(IReadOnlyList<CognitiveSource> sources) =>
        Write("scout", "trading", "completed", [$"sources_scanned:{sources.Count}", "foreign_code_execution:false"], sources.Select(source => source.SourceId).ToList(), []);

    private RoleOutput Write(string role, string domain, string status, IReadOnlyList<string> findings, IReadOnlyList<string> refs, IReadOnlyList<string> flags)
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "role_outputs");
        Directory.CreateDirectory(root);
        var output = new RoleOutput($"role_{role}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}", role, domain, status, findings, refs, flags, DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(root, $"{role}_latest.json"), JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }
}

public sealed class AnalystRole
{
    private readonly StoragePaths _storagePaths;

    public AnalystRole(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public RoleOutput WriteCatalogOutput(IReadOnlyList<KnowledgeCatalogItem> items) =>
        Write("analyst", "trading", "completed", [$"knowledge_items:{items.Count}", "pattern_catalog_mapped:true"], items.Take(20).Select(item => item.Id).ToList(), []);

    private RoleOutput Write(string role, string domain, string status, IReadOnlyList<string> findings, IReadOnlyList<string> refs, IReadOnlyList<string> flags)
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "role_outputs");
        Directory.CreateDirectory(root);
        var output = new RoleOutput($"role_{role}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}", role, domain, status, findings, refs, flags, DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(root, $"{role}_latest.json"), JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }
}

public sealed class ValidatorRole
{
    private readonly StoragePaths _storagePaths;

    public ValidatorRole(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public RoleOutput WriteQueueValidationOutput(ResearchQueueItem item) =>
        Write("validator", item.Domain, "completed", [$"queue_item:{item.QueueItemId}", "validation_requires_existing_quality_gates"], item.SourceRefs, ["no_trading_execution"]);

    private RoleOutput Write(string role, string domain, string status, IReadOnlyList<string> findings, IReadOnlyList<string> refs, IReadOnlyList<string> flags)
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "role_outputs");
        Directory.CreateDirectory(root);
        var output = new RoleOutput($"role_{role}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}", role, domain, status, findings, refs, flags, DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(root, $"{role}_latest.json"), JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }
}

public sealed class CriticRole
{
    private readonly StoragePaths _storagePaths;

    public CriticRole(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public RoleOutput WriteQueueCriticOutput(ResearchQueueItem item) =>
        Write("critic", item.Domain, "completed", [$"queue_item:{item.QueueItemId}", "critic_requires_overfit_realism_cost_review"], item.SourceRefs, ["overfit_check_required", "human_review_required"]);

    private RoleOutput Write(string role, string domain, string status, IReadOnlyList<string> findings, IReadOnlyList<string> refs, IReadOnlyList<string> flags)
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "role_outputs");
        Directory.CreateDirectory(root);
        var output = new RoleOutput($"role_{role}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}", role, domain, status, findings, refs, flags, DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(root, $"{role}_latest.json"), JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }
}

public sealed class SummarizerRole
{
    private readonly StoragePaths _storagePaths;

    public SummarizerRole(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public RoleOutput WriteHypothesisSummary(IReadOnlyList<CognitiveHypothesis> hypotheses) =>
        Write("summarizer", "trading", "completed", [$"hypotheses:{hypotheses.Count}", "summaries_structured:true"], hypotheses.Take(20).Select(hypothesis => hypothesis.HypothesisId).ToList(), []);

    private RoleOutput Write(string role, string domain, string status, IReadOnlyList<string> findings, IReadOnlyList<string> refs, IReadOnlyList<string> flags)
    {
        var root = Path.Combine(_storagePaths.Root, "cognitive_core", "role_outputs");
        Directory.CreateDirectory(root);
        var output = new RoleOutput($"role_{role}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}", role, domain, status, findings, refs, flags, DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(root, $"{role}_latest.json"), JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }
}
