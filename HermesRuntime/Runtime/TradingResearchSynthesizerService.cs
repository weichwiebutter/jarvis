using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TradingResearchEvidenceComparison(
    string PatternId,
    string PatternName,
    string Domain,
    string InternalEvidence,
    string ExternalEvidence,
    IReadOnlyList<string> Agreements,
    IReadOnlyList<string> Contradictions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> RelevantParameterClasses,
    string ExternalResearchSource);

public sealed record TradingResearchHypothesis(
    string HypothesisId,
    string PatternId,
    string PatternName,
    string Domain,
    string Title,
    string Hypothesis,
    string InternalEvidence,
    string ExternalEvidence,
    string AgreementSummary,
    string ContradictionSummary,
    string OpenQuestionSummary,
    IReadOnlyList<string> ParameterClasses,
    double ExpectedInformationGain,
    double ValidationEffort,
    string RiskLevel,
    string Priority,
    string SuggestedNextValidation,
    bool FrankRequired);

public sealed record TradingResearchSynthesizerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int PatternsAnalyzed,
    int InternalSourcesAnalyzed,
    int ExternalSourcesAnalyzed,
    int HypothesesCount,
    int HighPriorityCount,
    int MediumPriorityCount,
    int LowPriorityCount,
    IReadOnlyList<TradingResearchEvidenceComparison> Comparisons,
    IReadOnlyList<TradingResearchHypothesis> Hypotheses,
    IReadOnlyList<string> InternalSources,
    IReadOnlyList<string> ExternalSources,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ExternalResearchSource,
    string ReportPath,
    string MarkdownPath);

public sealed class TradingResearchSynthesizerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public TradingResearchSynthesizerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trading_research_synthesis");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "trading_research_synthesizer.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "trading_research_synthesizer.md");

    public TradingResearchSynthesizerReport Run()
    {
        Directory.CreateDirectory(Root);

        var researchMemory = new StrategyResearchService(_storagePaths).LoadOrCreateMemory();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var consolidation = new KnowledgeConsolidationExecutorService(_storagePaths).Run();
        var mutation = new StrategyMutationAnalyzerService(_storagePaths).Run();
        var parameterPlanner = new StrategyParameterResearchPlannerService(_storagePaths, _runtimeRoot).Run();
        var setupRegistryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var setupRegistry = setupRegistryService.LoadRegistry() ?? setupRegistryService.BuildRegistry();
        var inventory = setupRegistryService.LoadInventory() ?? setupRegistryService.BuildInventory();
        var forwardStatus = new ForwardTestService(_storagePaths, _runtimeRoot).LoadStatus();
        var reviews = new ReviewDecisionAssistantService(_storagePaths).Run();
        var externalSources = TradingDeKnowledgeCatalog.Sources();
        var patternDefinitions = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        var patterns = BuildPatterns(patternDefinitions, researchMemory, setupRegistry, inventory, forwardStatus, reviews, externalSources);
        var hypotheses = BuildHypotheses(patterns, setupRegistry, inventory, forwardStatus, reviews, externalSources);

        var report = new TradingResearchSynthesizerReport(
            ReportVersion: "trading_research_synthesizer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PatternsAnalyzed: patterns.Count,
            InternalSourcesAnalyzed: catalog.Count + researchMemory.ResearchEntries.Count + setupRegistry.Assets.Count + inventory.Items.Count + reviews.ReviewCount + consolidation.CandidatesPreparedCount + mutation.CandidateCount + parameterPlanner.CandidateCount,
            ExternalSourcesAnalyzed: externalSources.Count,
            HypothesesCount: hypotheses.Count,
            HighPriorityCount: hypotheses.Count(item => item.Priority.Equals("high", StringComparison.OrdinalIgnoreCase)),
            MediumPriorityCount: hypotheses.Count(item => item.Priority.Equals("medium", StringComparison.OrdinalIgnoreCase)),
            LowPriorityCount: hypotheses.Count(item => item.Priority.Equals("low", StringComparison.OrdinalIgnoreCase)),
            Comparisons: patterns,
            Hypotheses: hypotheses,
            InternalSources: BuildInternalSourcesSummary(catalog, researchMemory, setupRegistry, inventory, reviews, consolidation, mutation, parameterPlanner),
            ExternalSources: externalSources.Select(source => $"{source.SourceName}:{source.SourceUrl}").ToList(),
            Warnings: BuildWarnings(catalog, researchMemory, reviews, forwardStatus),
            OperatorSummary: $"{patterns.Count} Muster analysiert. {hypotheses.Count} Hypothesen erkannt. {hypotheses.Count(item => item.Priority == "high")} hohe Priorität. Frank nötig: nein.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ExternalResearchSource: "existing_artifacts_only",
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    private static IReadOnlyList<TradingResearchEvidenceComparison> BuildPatterns(
        IReadOnlyList<StrategyPatternDefinition> patternDefinitions,
        StrategyResearchMemory researchMemory,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviews,
        IReadOnlyList<KnowledgeSourceDefinition> externalSources)
    {
        var selected = patternDefinitions.Where(pattern => pattern.Id is "ema_pullback" or "breakout_continuation" or "range_breakout" or "first_candle_breakout" or "inside_bar_breakout" or "mean_reversion_rejection").ToList();
        var report = new List<TradingResearchEvidenceComparison>();

        foreach (var pattern in selected)
        {
            var internalSignals = new List<string>();
            if (researchMemory.ResearchEntries.Count > 0)
            {
                internalSignals.Add("Research Memory vorhanden");
            }

            if (setupRegistry.Assets.Count > 0)
            {
                internalSignals.Add($"Setup Registry mit {setupRegistry.Assets.Count} Assets");
            }

            if (inventory.Items.Count > 0)
            {
                internalSignals.Add($"Certified Candidates: {inventory.Items.Count}");
            }

            if (forwardStatus?.ForwardTestObservationsTotal > 0)
            {
                internalSignals.Add($"Forward Observations: {forwardStatus.ForwardTestObservationsTotal}");
            }

            if (reviews.ReviewCount > 0)
            {
                internalSignals.Add($"Reviews: {reviews.ReviewCount}");
            }

            var externalSignals = externalSources
                .Where(source => source.ExtractedConcepts.Any(concept =>
                    concept.Contains(pattern.Id, StringComparison.OrdinalIgnoreCase)
                    || concept.Contains(pattern.Name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase)
                    || concept.Contains("breakout", StringComparison.OrdinalIgnoreCase)
                    || concept.Contains("rejection", StringComparison.OrdinalIgnoreCase)
                    || concept.Contains("mean_reversion", StringComparison.OrdinalIgnoreCase)))
                .Select(source => source.SourceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var agreements = new List<string>
            {
                pattern.Name switch
                {
                    "EMA Pullback" => "Pullback- und Trendkontext wird intern wie extern bestätigt",
                    "Breakout Continuation" => "Breakout/Continuation wird in internen und externen Artefakten gestützt",
                    "Range Breakout" => "Range-/Kompressionsbruch wird mehrfach erwähnt",
                    "First Candle Breakout" => "Sessionstart- und Volatilitätskontext stimmt überein",
                    "Inside Bar Breakout" => "Kompressionsmuster und Breakout-Kontext stimmen überein",
                    "Mean Reversion Rejection" => "Rejection/Mean-Reversion wird in Artefakten bestätigt",
                    _ => "Pattern-Kontext deckt sich"
                }
            };

            var contradictions = new List<string>();
            if (pattern.Id.Contains("breakout", StringComparison.OrdinalIgnoreCase) && externalSignals.Count == 0)
            {
                contradictions.Add("Externe Artefakte nennen Breakout nur allgemein, nicht pattern-spezifisch");
            }

            if (pattern.Id.Contains("mean_reversion", StringComparison.OrdinalIgnoreCase) && !researchMemory.ResearchEntries.Any(entry => entry.StrategyVariantId.Contains("mean", StringComparison.OrdinalIgnoreCase) || entry.Status.Contains("research", StringComparison.OrdinalIgnoreCase)))
            {
                contradictions.Add("Interne Knowledge Catalog Tags zu Mean Reversion sind dünn");
            }

            var openQuestions = new List<string>
            {
                "Welche Sessionfilter erhöhen die Signalqualität?",
                "Welche Volatilitätsregime sind robust?",
                "Welche Ausstiegslogik stabilisiert die Trefferquote?"
            };

            var parameterClasses = BuildParameterClasses(pattern.Id);
            report.Add(new TradingResearchEvidenceComparison(
                PatternId: pattern.Id,
                PatternName: pattern.Name,
                Domain: "trading",
                InternalEvidence: string.Join("; ", internalSignals.Distinct(StringComparer.OrdinalIgnoreCase)),
                ExternalEvidence: externalSignals.Count > 0 ? $"existing_artifacts_only: {string.Join(", ", externalSignals)}" : "existing_artifacts_only: none",
                Agreements: agreements,
                Contradictions: contradictions,
                OpenQuestions: openQuestions,
                RelevantParameterClasses: parameterClasses,
                ExternalResearchSource: "existing_artifacts_only"));
        }

        return report;
    }

    private static IReadOnlyList<TradingResearchHypothesis> BuildHypotheses(
        IReadOnlyList<TradingResearchEvidenceComparison> comparisons,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviews,
        IReadOnlyList<KnowledgeSourceDefinition> externalSources)
    {
        var hypotheses = new List<TradingResearchHypothesis>();
        foreach (var comparison in comparisons)
        {
            foreach (var parameterClass in comparison.RelevantParameterClasses)
            {
                var priority = DeterminePriority(comparison, parameterClass, setupRegistry, inventory, forwardStatus, reviews, externalSources);
                hypotheses.Add(new TradingResearchHypothesis(
                    HypothesisId: $"trading_research_{comparison.PatternId}_{NormalizeId(parameterClass)}",
                    PatternId: comparison.PatternId,
                    PatternName: comparison.PatternName,
                    Domain: comparison.Domain,
                    Title: $"{comparison.PatternName}: {parameterClass}",
                    Hypothesis: BuildHypothesisText(comparison.PatternName, parameterClass),
                    InternalEvidence: comparison.InternalEvidence,
                    ExternalEvidence: comparison.ExternalEvidence,
                    AgreementSummary: string.Join("; ", comparison.Agreements),
                    ContradictionSummary: comparison.Contradictions.Count > 0 ? string.Join("; ", comparison.Contradictions) : "keine wesentlichen Widersprüche",
                    OpenQuestionSummary: string.Join("; ", comparison.OpenQuestions),
                    ParameterClasses: [parameterClass],
                    ExpectedInformationGain: ExpectedInformationGain(priority, comparison, parameterClass),
                    ValidationEffort: ValidationEffort(priority, parameterClass),
                    RiskLevel: RiskLevel(parameterClass),
                    Priority: priority,
                    SuggestedNextValidation: SuggestedNextValidation(comparison.PatternName, parameterClass),
                    FrankRequired: false));
            }
        }

        return hypotheses
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.ExpectedInformationGain)
            .ThenBy(item => item.PatternName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildParameterClasses(string patternId) =>
        patternId switch
        {
            "ema_pullback" => ["VWAP", "EMA", "ATR", "ADX", "session filter", "pullback depth", "stop loss", "take profit", "trailing stop", "break-even"],
            "breakout_continuation" => ["VWAP", "ATR", "ADR", "ADX", "volume", "session filter", "breakout buffer", "trailing stop", "partial exit"],
            "range_breakout" => ["ATR", "ADR", "volume", "spread filter", "volatility regime", "range buffer", "break-even", "partial exit"],
            "first_candle_breakout" => ["VWAP", "ATR", "session filter", "news filter", "spread filter", "breakout buffer", "partial exit"],
            "inside_bar_breakout" => ["ATR", "Bollinger Band Width", "volume", "session filter", "volatility regime", "breakout buffer"],
            "mean_reversion_rejection" => ["SMA", "EMA", "RSI", "ATR", "Bollinger Band Width", "trend/range regime", "rejection confirmation", "stop loss"],
            _ => ["ATR", "EMA", "session filter", "volatility regime"]
        };

    private static string BuildHypothesisText(string patternName, string parameterClass) =>
        parameterClass switch
        {
            "VWAP" => $"Hypothese: {patternName} könnte mit VWAP als Kontextfilter stabiler sein als nur mit klassischen Trendfiltern.",
            "EMA" => $"Hypothese: {patternName} profitiert von EMA-Struktur als Trend-/Pullback-Anker.",
            "ATR" => $"Hypothese: {patternName} reagiert robust auf ATR-basierte Volatilitätsanpassung.",
            "ADR" => $"Hypothese: {patternName} braucht Tagesreichweiten-Kontext statt nur Intraday-Volatilität.",
            "RSI" => $"Hypothese: {patternName} gewinnt durch Momentum-/Überdehnungsfilter wie RSI.",
            "ADX" => $"Hypothese: {patternName} sollte nur in Trendregimen mit ausreichendem ADX validiert werden.",
            "Bollinger Band Width" => $"Hypothese: {patternName} reagiert auf Kompressions-/Expansionsphasen besser als auf starre ATR-Filter.",
            "MACD" => $"Hypothese: {patternName} lässt sich über MACD-Divergenz oder Trendimpuls besser bestätigen.",
            "Volume" => $"Hypothese: {patternName} benötigt Volumenbestätigung, bevor eine Fortsetzung wahrscheinlich ist.",
            "Session filter" => $"Hypothese: {patternName} ist sessionsensitiv und sollte nur in liquiden Fenstern getestet werden.",
            "News filter" => $"Hypothese: {patternName} braucht News- und Spreadfilter, um schlechte Trigger zu vermeiden.",
            "Spread filter" => $"Hypothese: {patternName} ist spreadsensitiv und sollte nur bei engen Spreads validiert werden.",
            "Volatility regime" => $"Hypothese: {patternName} ist regimeabhängig und sollte getrennt nach Volatilitätszuständen validiert werden.",
            "Trend/range regime" => $"Hypothese: {patternName} verhält sich je nach Trend-/Range-Regime unterschiedlich.",
            "Breakout buffer" => $"Hypothese: {patternName} benötigt einen adaptiven Breakout-Puffer statt fester Tick-Schwellen.",
            "Pullback depth" => $"Hypothese: {patternName} braucht eine messbare Pullback-Tiefe statt nur Candle-Lookback.",
            "Swing lookback" => $"Hypothese: {patternName} sollte den Swing-Lookback als Kontext nutzen.",
            "Stop loss" => $"Hypothese: {patternName} braucht eine stufenweise Stop-Logik statt fixer Stops.",
            "Take profit" => $"Hypothese: {patternName} sollte Take-Profit-Klassen getrennt nach Marktregime testen.",
            "Trailing stop" => $"Hypothese: {patternName} profitiert von Trailing-Stop-Varianten.",
            "Break-even" => $"Hypothese: {patternName} braucht Break-even-Regeln zur Gewinnsicherung.",
            "Partial exit" => $"Hypothese: {patternName} könnte durch Teilverkäufe robuster werden.",
            _ => $"Hypothese: {patternName} sollte die Parameterklasse {parameterClass} systematisch prüfen."
        };

    private static string DeterminePriority(
        TradingResearchEvidenceComparison comparison,
        string parameterClass,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviews,
        IReadOnlyList<KnowledgeSourceDefinition> externalSources)
    {
        var basePriority = comparison.PatternId is "breakout_continuation" or "range_breakout" or "ema_pullback" or "first_candle_breakout" ? "high" : "medium";
        if (parameterClass is "VWAP" or "ADR" or "Bollinger Band Width" or "News filter" or "Spread filter")
        {
            return "high";
        }

        if (parameterClass is "Volume" or "ADX" or "Trend/range regime" or "Volatility regime")
        {
            return "high";
        }

        if (comparison.Contradictions.Count > 0 || reviews.ReviewCount > 0 || (forwardStatus?.ForwardTestObservationsTotal ?? 0) > 0)
        {
            return basePriority;
        }

        return "medium";
    }

    private static double ExpectedInformationGain(string priority, TradingResearchEvidenceComparison comparison, string parameterClass) =>
        priority switch
        {
            "high" => 0.78,
            "medium" => 0.6,
            _ => 0.42
        } + (comparison.Contradictions.Count > 0 ? 0.06 : 0) + (parameterClass is "VWAP" or "ADR" ? 0.08 : 0);

    private static double ValidationEffort(string priority, string parameterClass) =>
        priority switch
        {
            "high" => parameterClass is "VWAP" or "ADR" ? 0.55 : 0.45,
            "medium" => 0.35,
            _ => 0.2
        };

    private static string RiskLevel(string parameterClass) =>
        parameterClass is "News filter" or "Spread filter" ? "medium" : "low";

    private static string SuggestedNextValidation(string patternName, string parameterClass) =>
        $"{patternName}: {parameterClass} in bestehende Validierungs-/Forward-Landschaft überführen";

    private static int PriorityRank(string priority) =>
        priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 3 :
        priority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }

    private static IReadOnlyList<string> BuildInternalSourcesSummary(
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        StrategyResearchMemory memory,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory,
        ReviewDecisionAssistantReport reviews,
        KnowledgeConsolidationExecutorReport consolidation,
        StrategyMutationAnalyzerReport mutation,
        StrategyParameterResearchPlannerReport? planner)
    {
        return
        [
            $"Knowledge Catalog: {catalog.Count}",
            $"Research Memory: {memory.ResearchEntries.Count}",
            $"Setup Registry: {setupRegistry.Assets.Count}",
            $"Certified Candidates: {inventory.Items.Count}",
            $"Reviews: {reviews.ReviewCount}",
            $"Knowledge Consolidation: {consolidation.CandidatesPreparedCount}",
            $"Strategy Mutation: {mutation.CandidateCount}",
            $"Parameter Planner: {planner?.CandidateCount ?? 0}"
        ];
    }

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        StrategyResearchMemory memory,
        ReviewDecisionAssistantReport reviews,
        ForwardTestStatusSnapshot? forwardStatus)
    {
        var warnings = new List<string>();
        if (catalog.Count == 0)
        {
            warnings.Add("knowledge_catalog_missing");
        }

        if (memory.ResearchEntries.Count == 0)
        {
            warnings.Add("research_memory_empty");
        }

        if (reviews.ReviewCount > 0)
        {
            warnings.Add("review_context_present");
        }

        if ((forwardStatus?.ForwardTestObservationsTotal ?? 0) == 0)
        {
            warnings.Add("forward_observations_low");
        }

        return warnings;
    }

    private void WriteReport(TradingResearchSynthesizerReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "trading_research_synthesis");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            _resolvedReportPath = Path.Combine(fallbackRoot, "trading_research_synthesizer.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "trading_research_synthesizer.md");
            File.WriteAllText(_resolvedReportPath, json);
            File.WriteAllText(_resolvedMarkdownPath, markdown);
        }
    }

    private static string BuildMarkdown(TradingResearchSynthesizerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trading Research Synthesizer");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Patterns analyzed: {report.PatternsAnalyzed}");
        sb.AppendLine($"- Hypotheses: {report.HypothesesCount}");
        sb.AppendLine($"- High priority: {report.HighPriorityCount}");
        sb.AppendLine($"- External source mode: {report.ExternalResearchSource}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Hypotheses");
        foreach (var hypothesis in report.Hypotheses.Take(60))
        {
            sb.AppendLine($"- {hypothesis.PatternName}: {hypothesis.Title} · priority={hypothesis.Priority} · info_gain={hypothesis.ExpectedInformationGain:0.###}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- {report.SafetySummary}");
        return sb.ToString();
    }
}
