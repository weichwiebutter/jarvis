using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyMutationValidationPlan(
    string ValidationPlanId,
    string SourceHypothesisId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> ParametersToValidate,
    bool RequiredBacktest,
    bool RequiredOosTest,
    bool RequiredWalkForward,
    bool RequiredMonteCarlo,
    bool RequiredCostSpreadTest,
    bool RequiredForwardObservation,
    double ExpectedInformationGain,
    double ValidationEffort,
    string Priority,
    IReadOnlyList<string> SafetyFlags);

public sealed record StrategyMutationValidationPlannerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int HypothesesAnalyzed,
    int ValidationPlansPrepared,
    IReadOnlyList<StrategyMutationValidationPlan> ValidationPlans,
    IReadOnlyList<string> SourcesUsed,
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

public sealed class StrategyMutationValidationPlannerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyMutationValidationPlannerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_mutation_validation");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_mutation_validation_planner.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_mutation_validation_planner.md");

    public StrategyMutationValidationPlannerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyMutationValidationPlannerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public StrategyMutationValidationPlannerReport Run()
    {
        Directory.CreateDirectory(Root);

        var synth = new TradingResearchSynthesizerService(_storagePaths, _runtimeRoot).Run();
        var parameterPlanner = new StrategyParameterResearchPlannerService(_storagePaths, _runtimeRoot).Run();
        var mutationAnalyzer = new StrategyMutationAnalyzerService(_storagePaths).Run();
        var consolidation = new KnowledgeConsolidationExecutorService(_storagePaths).Run();
        var setupService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var setupRegistry = setupService.LoadRegistry() ?? setupService.BuildRegistry();
        var inventory = setupService.LoadInventory() ?? setupService.BuildInventory();
        var forwardStatus = new ForwardTestService(_storagePaths, _runtimeRoot).LoadStatus();
        var reviews = new ReviewDecisionAssistantService(_storagePaths).Run();

        var plans = BuildPlans(synth, parameterPlanner, setupRegistry, inventory, forwardStatus, reviews);
        var report = new StrategyMutationValidationPlannerReport(
            ReportVersion: "strategy_mutation_validation_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            HypothesesAnalyzed: synth.HypothesesCount,
            ValidationPlansPrepared: plans.Count,
            ValidationPlans: plans,
            SourcesUsed:
            [
                "trading_research_synthesizer.json",
                "strategy_parameter_research_planner.json",
                "strategy_mutation_analyzer.json",
                "knowledge_consolidation_executor.json",
                "setup_registry.json",
                "certified_candidates",
                "forward_observations",
                "review_decision_assistant"
            ],
            Warnings: mutationAnalyzer.Warnings.Concat(consolidation.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            OperatorSummary: $"{synth.HypothesesCount} Hypothesen analysiert. {plans.Count} Validierungsaufträge vorbereitet. Frank nötig: nein. Keine Backtests gestartet. Keine Broker-Aktionen.",
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

    private static IReadOnlyList<StrategyMutationValidationPlan> BuildPlans(
        TradingResearchSynthesizerReport synth,
        StrategyParameterResearchPlannerReport parameterPlanner,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory,
        ForwardTestStatusSnapshot? forwardStatus,
        ReviewDecisionAssistantReport reviews)
    {
        var setupsByAsset = setupRegistry.Assets
            .GroupBy(asset => asset.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var candidates = synth.Hypotheses
            .Select(hypothesis =>
            {
                var parameterPlannerEntry = parameterPlanner.Candidates
                    .FirstOrDefault(candidate => candidate.SourcePattern.Equals(hypothesis.PatternName, StringComparison.OrdinalIgnoreCase));
                return new
                {
                    Hypothesis = hypothesis,
                    EvidenceBasis = hypothesis.InternalEvidence + " | " + hypothesis.ExternalEvidence,
                    AssetPriority = AssetPriorityFor(setupRegistry, inventory, hypothesis.PatternName),
                    Confidence = hypothesis.ExpectedInformationGain,
                    Effort = hypothesis.ValidationEffort,
                    Parameters = parameterPlannerEntry?.ParameterRanges.Select(range => $"{range.Name}[{string.Join("|", range.Values)}]").ToList() ?? hypothesis.ParameterClasses.ToList()
                };
            })
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.Effort)
            .ThenByDescending(item => item.AssetPriority)
            .ToList();

        var plans = new List<StrategyMutationValidationPlan>();
        foreach (var candidate in candidates.Take(12))
        {
            var asset = SelectAsset(candidate.Hypothesis.PatternName, setupsByAsset, setupRegistry, inventory);
            var timeframe = SelectTimeframe(candidate.Hypothesis.PatternName, setupRegistry, forwardStatus);
            plans.Add(new StrategyMutationValidationPlan(
                ValidationPlanId: $"validation_plan_{NormalizeId(candidate.Hypothesis.HypothesisId)}",
                SourceHypothesisId: candidate.Hypothesis.HypothesisId,
                StrategyPattern: candidate.Hypothesis.PatternName,
                Asset: asset,
                Timeframe: timeframe,
                ParametersToValidate: candidate.Parameters,
                RequiredBacktest: true,
                RequiredOosTest: true,
                RequiredWalkForward: true,
                RequiredMonteCarlo: true,
                RequiredCostSpreadTest: true,
                RequiredForwardObservation: true,
                ExpectedInformationGain: candidate.Confidence,
                ValidationEffort: candidate.Effort,
                Priority: DeterminePriority(candidate.Hypothesis.Priority, asset, candidate.Confidence),
                SafetyFlags:
                [
                    "no_auto_trading=true",
                    "no_broker_action=true",
                    "no_live_trading=true",
                    "no_demo_orders=true",
                    "human_review_required=true"
                ]));
        }

        return plans;
    }

    private static string SelectAsset(
        string patternName,
        IReadOnlyDictionary<string, List<SetupRegistryEntry>> setupsByAsset,
        SetupRegistry setupRegistry,
        CertifiedCandidateInventory inventory)
    {
        var ordered = setupsByAsset
            .OrderByDescending(pair => pair.Value.Count)
            .Select(pair => pair.Key)
            .ToList();

        if (patternName.Contains("Breakout", StringComparison.OrdinalIgnoreCase) || patternName.Contains("Continuation", StringComparison.OrdinalIgnoreCase))
        {
            if (setupsByAsset.ContainsKey("GER40"))
            {
                return "GER40";
            }

            if (setupsByAsset.ContainsKey("XAUUSD"))
            {
                return "XAUUSD";
            }
        }

        if (patternName.Contains("Mean Reversion", StringComparison.OrdinalIgnoreCase))
        {
            return setupsByAsset.ContainsKey("XAUUSD") ? "XAUUSD" : "EURUSD";
        }

        if (inventory.Items.Any(item => item.Asset.Equals("EURUSD", StringComparison.OrdinalIgnoreCase)))
        {
            return "EURUSD";
        }

        return ordered.FirstOrDefault() ?? (setupRegistry.Assets.FirstOrDefault()?.Asset ?? "EURUSD");
    }

    private static string SelectTimeframe(
        string patternName,
        SetupRegistry setupRegistry,
        ForwardTestStatusSnapshot? forwardStatus)
    {
        var preferred = setupRegistry.Assets
            .Where(asset => patternName.Contains(asset.SetupType, StringComparison.OrdinalIgnoreCase) || patternName.Contains(asset.PrimaryTimeframe, StringComparison.OrdinalIgnoreCase))
            .Select(asset => asset.PrimaryTimeframe)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (preferred.Count > 0)
        {
            return preferred.First();
        }

        if ((forwardStatus?.ForwardTestObservationsTotal ?? 0) > 0)
        {
            return "M5";
        }

        return patternName.Contains("Breakout", StringComparison.OrdinalIgnoreCase) ? "M5" : "M15";
    }

    private static string DeterminePriority(string hypothesisPriority, string asset, double informationGain)
    {
        var rank = hypothesisPriority.Equals("high", StringComparison.OrdinalIgnoreCase) ? "high" : hypothesisPriority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? "medium" : "low";
        if (asset is "GER40" or "XAUUSD")
        {
            return "high";
        }

        return informationGain >= 0.8 ? "high" : rank;
    }

    private static int AssetPriorityFor(SetupRegistry setupRegistry, CertifiedCandidateInventory inventory, string patternName) =>
        patternName.Contains("Breakout", StringComparison.OrdinalIgnoreCase) || patternName.Contains("Continuation", StringComparison.OrdinalIgnoreCase)
            ? (setupRegistry.Assets.Any(asset => asset.Asset.Equals("GER40", StringComparison.OrdinalIgnoreCase)) ? 3 : 2)
            : inventory.Items.Any(item => item.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)) ? 2 : 1;

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }

    private void WriteReport(StrategyMutationValidationPlannerReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "strategy_mutation_validation");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            _resolvedReportPath = Path.Combine(fallbackRoot, "strategy_mutation_validation_planner.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "strategy_mutation_validation_planner.md");
            File.WriteAllText(_resolvedReportPath, json);
            File.WriteAllText(_resolvedMarkdownPath, markdown);
        }
    }

    private static string BuildMarkdown(StrategyMutationValidationPlannerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Mutation Validation Planner");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Hypotheses analyzed: {report.HypothesesAnalyzed}");
        sb.AppendLine($"- Validation plans prepared: {report.ValidationPlansPrepared}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Validation Plans");
        foreach (var plan in report.ValidationPlans.Take(20))
        {
            sb.AppendLine($"- {plan.StrategyPattern} @ {plan.Asset} {plan.Timeframe} · priority={plan.Priority} · info_gain={plan.ExpectedInformationGain:0.###}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- {report.SafetySummary}");
        return sb.ToString();
    }
}
