using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyMutationCandidate(
    string MutationId,
    string SourcePattern,
    IReadOnlyList<string> ParameterChanges,
    string ExpectedBenefit,
    bool ValidationRequired,
    bool OosRequired,
    bool ForwardObservationRequired,
    double TrustBaseline);

public sealed record StrategyMutationPattern(
    string PatternId,
    string PatternName,
    string Domain,
    string PatternDescription,
    IReadOnlyList<string> ParametersAvailable,
    IReadOnlyList<string> ParametersVariations,
    IReadOnlyList<string> SupportingSignals,
    int MutationCount);

public sealed record StrategyMutationAnalyzerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int PatternsAnalyzed,
    int MutationsPrepared,
    int CandidateCount,
    int KnowledgeItemsAnalyzed,
    int ReviewItemsAnalyzed,
    int ResearchEntriesAnalyzed,
    IReadOnlyList<StrategyMutationPattern> Patterns,
    IReadOnlyList<StrategyMutationCandidate> Candidates,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyMutationAnalyzerService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyMutationAnalyzerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_mutation");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_mutation_analyzer.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_mutation_analyzer.md");

    public StrategyMutationAnalyzerReport Run()
    {
        Directory.CreateDirectory(Root);

        var catalog = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        var memory = new StrategyResearchService(_storagePaths).LoadOrCreateMemory();
        var consolidation = new KnowledgeConsolidationAnalyzerService(_storagePaths).Run();
        var review = new HumanReviewWorkflow(_storagePaths).BuildSummary();
        var patterns = BuildPatterns(catalog);
        var candidates = BuildCandidates(patterns, memory, consolidation, review);

        var report = new StrategyMutationAnalyzerReport(
            ReportVersion: "strategy_mutation_analyzer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PatternsAnalyzed: patterns.Count,
            MutationsPrepared: candidates.Count,
            CandidateCount: candidates.Count,
            KnowledgeItemsAnalyzed: consolidation.TotalKnowledgeItems,
            ReviewItemsAnalyzed: review.PendingReviews,
            ResearchEntriesAnalyzed: memory.ResearchEntries?.Count ?? 0,
            Patterns: patterns,
            Candidates: candidates,
            Domains: patterns.Select(pattern => pattern.Domain).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: ["strategy_mutation_preparation_only"],
            OperatorSummary: $"{patterns.Count} Muster analysiert. {candidates.Count} Mutationen vorbereitet. Frank nötig: nein.",
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

    private static IReadOnlyList<StrategyMutationPattern> BuildPatterns(IReadOnlyList<StrategyPatternDefinition> catalog)
    {
        var sourcePatterns = catalog
            .Where(pattern => pattern.Id is "ema_pullback" or "breakout_continuation" or "first_candle_breakout" or "inside_bar_breakout" or "breakout" or "range_breakout")
            .Select(pattern =>
            {
                var available = new List<string> { "ATR7", "ATR14", "ATR21", "ATR28", "EMA20", "EMA50", "EMA100", "EMA200", "SL1ATR", "SL1.5ATR", "SL2ATR", "TP1R", "TP1.5R", "TP2R", "TP3R", "London", "New York", "London + New York" };
                var variations = new List<string>();
                if (pattern.Id.Equals("ema_pullback", StringComparison.OrdinalIgnoreCase))
                {
                    variations.AddRange(["EMA20 + ATR14", "EMA20 + ATR21", "EMA50 + ATR14", "EMA50 + ATR21"]);
                }
                else if (pattern.Id.Equals("breakout_continuation", StringComparison.OrdinalIgnoreCase))
                {
                    variations.AddRange(["ATR14 Variante", "ATR21 Variante", "Sessionfilter London", "Sessionfilter NY"]);
                }
                else
                {
                    variations.AddRange(["ATR14", "ATR21", "London", "New York"]);
                }

                return new StrategyMutationPattern(
                    PatternId: pattern.Id,
                    PatternName: pattern.Name,
                    Domain: "trading",
                    PatternDescription: pattern.Description,
                    ParametersAvailable: available,
                    ParametersVariations: variations,
                    SupportingSignals: pattern.PreferredSessions,
                    MutationCount: variations.Count);
            })
            .ToList();

        return sourcePatterns;
    }

    private static IReadOnlyList<StrategyMutationCandidate> BuildCandidates(
        IReadOnlyList<StrategyMutationPattern> patterns,
        StrategyResearchMemory memory,
        KnowledgeConsolidationAnalyzerReport consolidation,
        HumanReviewSummary review)
    {
        var candidates = new List<StrategyMutationCandidate>();
        var baselineTrust = consolidation.TrustedKnowledgeItems > 0 ? 0.62 : 0.55;
        foreach (var pattern in patterns)
        {
            foreach (var variation in pattern.ParametersVariations)
            {
                candidates.Add(new StrategyMutationCandidate(
                    MutationId: $"mutation_{pattern.PatternId}_{NormalizeId(variation)}",
                    SourcePattern: pattern.PatternName,
                    ParameterChanges: [variation],
                    ExpectedBenefit: pattern.PatternId switch
                    {
                        "ema_pullback" => "Bessere EMA-/ATR-Filterung und stabilere Pullback-Selektion",
                        "breakout_continuation" => "Stabilere Breakout- und Sessionsignale",
                        "first_candle_breakout" => "Frühere Session-Klarheit und robustere Volatilitätsfilter",
                        _ => "Parametervariation für spätere Validierung"
                    },
                    ValidationRequired: true,
                    OosRequired: true,
                    ForwardObservationRequired: true,
                    TrustBaseline: Math.Round(Math.Clamp(baselineTrust + (memory.ResearchEntries?.Count ?? 0) / 100000.0, 0.45, 0.75), 4)));
            }
        }

        return candidates;
    }

    private void WriteReport(StrategyMutationAnalyzerReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "strategy_mutation");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            var fallbackReportPath = Path.Combine(fallbackRoot, "strategy_mutation_analyzer.json");
            var fallbackMarkdownPath = Path.Combine(fallbackRoot, "strategy_mutation_analyzer.md");
            File.WriteAllText(fallbackReportPath, json);
            File.WriteAllText(fallbackMarkdownPath, markdown);
            _resolvedReportPath = fallbackReportPath;
            _resolvedMarkdownPath = fallbackMarkdownPath;
        }
    }

    private static string BuildMarkdown(StrategyMutationAnalyzerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Mutation Analyzer");
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
        foreach (var candidate in report.Candidates.Take(60))
        {
            sb.AppendLine($"- {candidate.SourcePattern}: {string.Join(", ", candidate.ParameterChanges)} · trust={candidate.TrustBaseline:0.####} · validation={(candidate.ValidationRequired ? "yes" : "no")} · oos={(candidate.OosRequired ? "yes" : "no")} · forward={(candidate.ForwardObservationRequired ? "yes" : "no")}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine("- no_auto_trading=true");
        sb.AppendLine("- human_review_required=true");
        sb.AppendLine("- broker_orders_enabled=false");
        sb.AppendLine("- live_trading_enabled=false");
        sb.AppendLine("- research_only=true");
        return sb.ToString();
    }

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
}
