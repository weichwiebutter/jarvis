using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyParameterRange(
    string Name,
    IReadOnlyList<string> Values,
    string Reason);

public sealed record StrategyParameterMutationPlan(
    string MutationId,
    string SourcePattern,
    string Domain,
    string PatternDescription,
    IReadOnlyList<StrategyParameterRange> ParameterRanges,
    string AssetContext,
    string TimeframeContext,
    string ExpectedBenefit,
    double TrustBaseline,
    bool ValidationRequired,
    bool OosRequired,
    bool ForwardObservationRequired,
    string EvidenceBasis);

public sealed record StrategyParameterResearchPattern(
    string PatternId,
    string PatternName,
    string Domain,
    string PatternDescription,
    IReadOnlyList<string> AssetContexts,
    IReadOnlyList<string> TimeframeContexts,
    IReadOnlyList<string> SessionContexts,
    IReadOnlyList<StrategyParameterRange> SuggestedRanges,
    int MutationCount,
    string EvidenceBasis);

public sealed record StrategyParameterResearchPlannerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int PatternsAnalyzed,
    int MutationsPrepared,
    int CandidateCount,
    int KnowledgeItemsAnalyzed,
    int SetupCandidatesAnalyzed,
    int CertifiedCandidatesAnalyzed,
    int ForwardObservationsAnalyzed,
    int ReviewItemsAnalyzed,
    int ResearchEntriesAnalyzed,
    IReadOnlyList<StrategyParameterResearchPattern> Patterns,
    IReadOnlyList<StrategyParameterMutationPlan> Candidates,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyParameterResearchPlannerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyParameterResearchPlannerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_parameter_research");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_parameter_research_planner.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_parameter_research_planner.md");

    public StrategyParameterResearchPlannerReport Run()
    {
        Directory.CreateDirectory(Root);

        var mutationAnalyzer = new StrategyMutationAnalyzerService(_storagePaths).Run();
        var consolidation = new KnowledgeConsolidationExecutorService(_storagePaths).Run();
        var setupRegistryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var setupRegistry = setupRegistryService.LoadRegistry() ?? setupRegistryService.BuildRegistry();
        var inventory = setupRegistryService.LoadInventory() ?? setupRegistryService.BuildInventory();
        var forwardTest = new ForwardTestService(_storagePaths, _runtimeRoot);
        var forwardStatus = forwardTest.LoadStatus();
        var reviewAssistant = new ReviewDecisionAssistantService(_storagePaths).Run();
        var memory = new StrategyResearchService(_storagePaths).LoadOrCreateMemory();
        var catalog = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();

        var patterns = BuildPatterns(catalog, setupRegistry, inventory, forwardStatus, reviewAssistant, memory);
        var candidates = BuildCandidates(patterns);
        var report = new StrategyParameterResearchPlannerReport(
            ReportVersion: "strategy_parameter_research_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PatternsAnalyzed: patterns.Count,
            MutationsPrepared: candidates.Count,
            CandidateCount: candidates.Count,
            KnowledgeItemsAnalyzed: mutationAnalyzer.KnowledgeItemsAnalyzed,
            SetupCandidatesAnalyzed: setupRegistry.Assets.Count,
            CertifiedCandidatesAnalyzed: inventory.Items.Count,
            ForwardObservationsAnalyzed: forwardStatus?.ForwardTestObservationsTotal ?? 0,
            ReviewItemsAnalyzed: reviewAssistant.ReviewCount,
            ResearchEntriesAnalyzed: memory.ResearchEntries?.Count ?? 0,
            Patterns: patterns,
            Candidates: candidates,
            Domains: patterns.Select(pattern => pattern.Domain).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: mutationAnalyzer.Warnings.Concat(consolidation.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            OperatorSummary: $"{patterns.Count} Muster analysiert. {candidates.Count} Mutationen vorbereitet. Frank nötig: nein.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    private static IReadOnlyList<StrategyParameterResearchPattern> BuildPatterns(
        IReadOnlyList<StrategyPatternDefinition> catalog,
        SetupRegistry registry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviewAssistant,
        StrategyResearchMemory memory)
    {
        var selectedPatterns = catalog
            .Where(pattern => pattern.Id is "ema_pullback" or "breakout_continuation" or "first_candle_breakout" or "inside_bar_breakout" or "breakout" or "range_breakout")
            .Select(pattern =>
            {
                var (ranges, basis) = BuildRanges(pattern, registry, inventory, forwardStatus, reviewAssistant, memory);
                var assetContexts = registry.Assets
                    .Where(item => item.SetupType.Contains(pattern.Id, StringComparison.OrdinalIgnoreCase)
                        || item.SetupType.Contains(pattern.Name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase)
                        || item.PrimaryCandidate.Contains(pattern.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Asset)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (assetContexts.Count == 0)
                {
                    assetContexts = inventory.Items
                        .Where(item => item.SetupType.Contains(pattern.Id, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Asset)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (assetContexts.Count == 0)
                {
                    assetContexts = ["EURUSD", "XAUUSD"];
                }

                var timeframeContexts = registry.Assets
                    .Where(item => item.Asset.Length > 0 && item.SetupType.Contains(pattern.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.PrimaryTimeframe)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (timeframeContexts.Count == 0)
                {
                    timeframeContexts = pattern.RequiredTimeframes.Take(3).ToList();
                }

                var sessionContexts = pattern.PreferredSessions
                    .Select(NormalizeSession)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new StrategyParameterResearchPattern(
                    PatternId: pattern.Id,
                    PatternName: pattern.Name,
                    Domain: "trading",
                    PatternDescription: pattern.Description,
                    AssetContexts: assetContexts,
                    TimeframeContexts: timeframeContexts,
                    SessionContexts: sessionContexts,
                    SuggestedRanges: ranges,
                    MutationCount: ranges.Sum(range => range.Values.Count),
                    EvidenceBasis: basis);
            })
            .ToList();

        return selectedPatterns;
    }

    private static (IReadOnlyList<StrategyParameterRange> Ranges, string EvidenceBasis) BuildRanges(
        StrategyPatternDefinition pattern,
        SetupRegistry registry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviewAssistant,
        StrategyResearchMemory memory)
    {
        var ranges = new List<StrategyParameterRange>();
        var evidenceNotes = new List<string>();
        var setupAssets = registry.Assets.Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var setupTimeframes = registry.Assets.Select(item => item.PrimaryTimeframe).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var hasForwardObservations = (forwardStatus?.ForwardTestObservationsTotal ?? 0) > 0;
        var tradingReviews = reviewAssistant.ReviewCount > 0;
        var researchCount = memory.ResearchEntries?.Count ?? 0;
        var inventoryAssets = inventory.Items.Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (pattern.Id.Equals("ema_pullback", StringComparison.OrdinalIgnoreCase))
        {
            ranges.Add(new StrategyParameterRange("EMA", ["20", "50", "100"], "EMA Pullback reagiert auf Trend-/Pullback-Bereiche; 20/50 zuerst, 100 als konservativer Filter."));
            ranges.Add(new StrategyParameterRange("ATR", ["14", "21", "28"], "EMA Pullbacks profitieren von moderater Volatilitätsanpassung; ATR14/21 zuerst."));
            ranges.Add(new StrategyParameterRange("Stop", ["1 ATR", "1.5 ATR", "2 ATR"], "Pullback-Stopps orientieren sich an Trendtiefe und Swingbreite."));
            ranges.Add(new StrategyParameterRange("Take Profit", ["1.5R", "2R", "3R"], "Fortsetzung nach Pullback rechtfertigt höhere RR-Bereiche."));
            ranges.Add(new StrategyParameterRange("Session", ["London", "London + New York"], "EMA Pullbacks sind in aktivem Liquiditätsfenster robuster."));
        }
        else if (pattern.Id.Equals("breakout_continuation", StringComparison.OrdinalIgnoreCase) || pattern.Id.Equals("breakout", StringComparison.OrdinalIgnoreCase) || pattern.Id.Equals("range_breakout", StringComparison.OrdinalIgnoreCase))
        {
            ranges.Add(new StrategyParameterRange("ATR", ["14", "21", "28"], "Breakout-Setups brauchen Volatilitätsfilter, ATR14/21 zuerst."));
            ranges.Add(new StrategyParameterRange("EMA", ["20", "50", "100"], "Trendfilter über schnelle bis mittlere EMA-Bereiche."));
            ranges.Add(new StrategyParameterRange("Stop", ["1 ATR", "1.5 ATR", "2 ATR"], "Breakouts benötigen knappe, aber nicht zu enge Absicherung."));
            ranges.Add(new StrategyParameterRange("Take Profit", ["1R", "1.5R", "2R", "3R"], "Breakouts erlauben oft frühe Teilziele und größere Runner."));
            ranges.Add(new StrategyParameterRange("Session", ["London", "New York", "London + New York"], "Breakouts sind sessionsensitiv; London/NY zuerst."));
        }
        else if (pattern.Id.Equals("first_candle_breakout", StringComparison.OrdinalIgnoreCase) || pattern.Id.Equals("inside_bar_breakout", StringComparison.OrdinalIgnoreCase))
        {
            ranges.Add(new StrategyParameterRange("ATR", ["7", "14", "21"], "Frühe Session-/Kompressionsmuster brauchen engere ATR-Spanne."));
            ranges.Add(new StrategyParameterRange("EMA", ["20", "50"], "Kurze EMA-Filter reichen oft für Session-Startmuster."));
            ranges.Add(new StrategyParameterRange("Stop", ["1 ATR", "1.5 ATR"], "Opening-Range-Breakouts vertragen nur moderate Stops."));
            ranges.Add(new StrategyParameterRange("Take Profit", ["1R", "1.5R", "2R"], "Schnelle Zielerreichung ist wahrscheinlicher als Langläufer."));
            ranges.Add(new StrategyParameterRange("Session", ["London", "New York"], "Sessionstart ist der zentrale Kontexttreiber."));
        }
        else
        {
            ranges.Add(new StrategyParameterRange("ATR", ["14", "21"], "Fallback auf robuste Volatilitätsfenster."));
            ranges.Add(new StrategyParameterRange("EMA", ["20", "50"], "Fallback auf bewährte Trendfilter."));
            ranges.Add(new StrategyParameterRange("Stop", ["1.5 ATR", "2 ATR"], "Fallback auf konservative Stops."));
            ranges.Add(new StrategyParameterRange("Take Profit", ["1.5R", "2R"], "Fallback auf moderate Zielbereiche."));
            ranges.Add(new StrategyParameterRange("Session", ["London", "New York"], "Fallback auf liquide Sessions."));
        }

        if (setupAssets.Contains("EURUSD", StringComparer.OrdinalIgnoreCase) || inventoryAssets.Contains("EURUSD", StringComparer.OrdinalIgnoreCase))
        {
            evidenceNotes.Add("EURUSD Setup-/Signal-Inventory vorhanden");
        }

        if (setupAssets.Any(asset => asset is "XAUUSD" or "GER40"))
        {
            evidenceNotes.Add("Asset-Kontext mit volatileren Märkten berücksichtigt");
        }

        if (hasForwardObservations)
        {
            evidenceNotes.Add("Forward-Observation vorhanden");
        }

        if (tradingReviews)
        {
            evidenceNotes.Add("Trading-Reviews liefern aktuelle Prioritäten");
        }

        if (researchCount > 0)
        {
            evidenceNotes.Add("Research Memory vorhanden");
        }

        if (setupTimeframes.Any(timeframe => timeframe is "M5" or "M15"))
        {
            evidenceNotes.Add("Kurzfristige Timeframes im Setup Registry sichtbar");
        }

        var basis = evidenceNotes.Count == 0
            ? "Fallback-Werte verwendet; keine bessere Evidenz vorhanden"
            : string.Join("; ", evidenceNotes.Distinct(StringComparer.OrdinalIgnoreCase));

        return (ranges, basis);
    }

    private static IReadOnlyList<StrategyParameterMutationPlan> BuildCandidates(IReadOnlyList<StrategyParameterResearchPattern> patterns)
    {
        var plans = new List<StrategyParameterMutationPlan>();
        foreach (var pattern in patterns)
        {
            var variations = pattern.SuggestedRanges
                .Select(range => $"{range.Name}={string.Join("|", range.Values)}")
                .ToList();
            foreach (var variation in variations)
            {
                plans.Add(new StrategyParameterMutationPlan(
                    MutationId: $"mutation_plan_{pattern.PatternId}_{NormalizeId(variation)}",
                    SourcePattern: pattern.PatternName,
                    Domain: pattern.Domain,
                    PatternDescription: pattern.PatternDescription,
                    ParameterRanges: pattern.SuggestedRanges,
                    AssetContext: string.Join(", ", pattern.AssetContexts),
                    TimeframeContext: string.Join(", ", pattern.TimeframeContexts),
                    ExpectedBenefit: BuildExpectedBenefit(pattern.PatternId),
                    TrustBaseline: pattern.Domain == "trading" ? 0.62 : 0.55,
                    ValidationRequired: true,
                    OosRequired: true,
                    ForwardObservationRequired: true,
                    EvidenceBasis: pattern.EvidenceBasis));
            }
        }

        return plans;
    }

    private static string BuildExpectedBenefit(string patternId) =>
        patternId switch
        {
            "ema_pullback" => "EMA-/ATR-Bereiche auf realen Markt- und Setup-Kontext abstimmen",
            "breakout_continuation" => "Breakout-Bestätigung und Sessionsensitivität schärfen",
            "first_candle_breakout" => "Session-Start und ATR-Filter auf bessere Frühsignale trimmen",
            "inside_bar_breakout" => "Kompressionsmuster mit engeren Volatilitätsfenstern stabilisieren",
            "breakout" or "range_breakout" => "Range-/Volatilitätsparameter robust kontextualisieren",
            _ => "Parameterbereich aus Muster- und Kontextwissen ableiten"
        };

    private static string NormalizeSession(string session) =>
        session.Replace("_new_york", " new_york", StringComparison.OrdinalIgnoreCase).Replace("_", " ", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeId(string text)
    {
        var normalized = text.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("+", "plus")
            .Replace(".", "")
            .Replace("-", "_")
            .Replace("/", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }

    private void WriteReport(StrategyParameterResearchPlannerReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "strategy_parameter_research");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            var fallbackReportPath = Path.Combine(fallbackRoot, "strategy_parameter_research_planner.json");
            var fallbackMarkdownPath = Path.Combine(fallbackRoot, "strategy_parameter_research_planner.md");
            File.WriteAllText(fallbackReportPath, json);
            File.WriteAllText(fallbackMarkdownPath, markdown);
            _resolvedReportPath = fallbackReportPath;
            _resolvedMarkdownPath = fallbackMarkdownPath;
        }
    }

    private static string BuildMarkdown(StrategyParameterResearchPlannerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Parameter Research Planner");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Patterns analyzed: {report.PatternsAnalyzed}");
        sb.AppendLine($"- Mutations prepared: {report.MutationsPrepared}");
        sb.AppendLine($"- Knowledge items analyzed: {report.KnowledgeItemsAnalyzed}");
        sb.AppendLine($"- Review items analyzed: {report.ReviewItemsAnalyzed}");
        sb.AppendLine($"- Research entries analyzed: {report.ResearchEntriesAnalyzed}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Candidates");
        foreach (var candidate in report.Candidates.Take(50))
        {
            sb.AppendLine($"- {candidate.SourcePattern}: {string.Join(", ", candidate.ParameterRanges.Select(range => $"{range.Name}[{string.Join("|", range.Values)}]"))}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- {report.SafetySummary}");
        return sb.ToString();
    }
}
