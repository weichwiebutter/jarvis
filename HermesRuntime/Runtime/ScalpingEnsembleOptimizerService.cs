using System.Text.Json;

namespace Hermes.Runtime;

public enum ScalpingEnsembleOptimizationMode
{
    conservative,
    balanced,
    aggressive_research_only
}

public enum ScalpingOptimizedEnsembleStatus
{
    building,
    optimized_candidate,
    ensemble_ready,
    rejected_portfolio,
    needs_more_diversity,
    human_review_required
}

public sealed record ScalpingOptimizedEnsembleMember(
    string CandidateId,
    string Asset,
    string SetupType,
    double Confidence,
    double ProfitFactor,
    double RecoveryFactor,
    double Drawdown,
    double MaxDailyDrawdown,
    double MaxWeeklyDrawdown,
    double SignalDensityScore,
    string ContributionReason,
    IReadOnlyList<string> RiskNotes);

public sealed record ScalpingOptimizedEnsembleSelection(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    ScalpingEnsembleOptimizationMode Mode,
    ScalpingOptimizedEnsembleStatus Status,
    IReadOnlyList<ScalpingOptimizedEnsembleMember> Members,
    double PreviousPortfolioDrawdown,
    double OptimizedPortfolioDrawdown,
    double PreviousSignalDensity,
    double OptimizedSignalDensity,
    double AssetDiversityScore,
    double SetupDiversityScore,
    double SessionDiversityScore,
    double CorrelationPenalty,
    double AverageProfitFactor,
    double AverageRecoveryFactor,
    double RiskOfRuinEstimate,
    double EnsembleStability,
    string Readiness,
    IReadOnlyList<string> Blockers,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingEnsembleOptimizerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    ScalpingEnsembleOptimizationMode Mode,
    int CertifiedCandidatesEvaluated,
    int CombinationsEvaluated,
    ScalpingOptimizedEnsembleSelection SelectedEnsemble,
    IReadOnlyList<ScalpingOptimizedEnsembleSelection> TopSelections,
    string OptimizerHealth,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingEnsembleOptimizerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingEnsembleOptimizerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "optimizer");
    public string OptimizerReportPath => Path.Combine(Root, "ensemble_optimizer_report.json");
    public string OptimizerMarkdownPath => Path.Combine(Root, "ensemble_optimizer_report.md");
    public string BalancedSelectionPath => Path.Combine(Root, "selected_ensemble_balanced.json");
    public string BalancedSelectionMarkdownPath => Path.Combine(Root, "selected_ensemble_balanced.md");

    public ScalpingEnsembleOptimizerReport Optimize(ScalpingEnsembleOptimizationMode mode = ScalpingEnsembleOptimizationMode.balanced)
    {
        var portfolioService = new ScalpingPortfolioService(_storagePaths, _runtimeRoot);
        var portfolio = portfolioService.Load() ?? portfolioService.Build();
        var certified = portfolio.Members
            .Where(member => member.Status == ScalpingCertificationStatus.certified_candidate.ToString())
            .GroupBy(member => member.CandidateId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var previousDrawdown = certified.Count == 0 ? 0 : Math.Round(certified.Sum(member => member.MaxDrawdown), 4);
        var previousDensity = certified.Count == 0 ? 0 : Math.Round(certified.Sum(member => member.SignalDensityScore), 4);
        var combinations = BuildCombinations(certified, mode).Select(items => ScoreSelection(items, mode, previousDrawdown, previousDensity)).ToList();
        var selected = combinations
            .OrderByDescending(selection => SelectionScore(selection, mode))
            .ThenBy(selection => selection.OptimizedPortfolioDrawdown)
            .FirstOrDefault()
            ?? EmptySelection(mode, previousDrawdown, previousDensity, "not_enough_certified_candidates");
        var report = new ScalpingEnsembleOptimizerReport(
            ReportVersion: "scalping_ensemble_optimizer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Mode: mode,
            CertifiedCandidatesEvaluated: certified.Count,
            CombinationsEvaluated: combinations.Count,
            SelectedEnsemble: selected,
            TopSelections: combinations.OrderByDescending(selection => SelectionScore(selection, mode)).Take(10).ToList(),
            OptimizerHealth: selected.Status is ScalpingOptimizedEnsembleStatus.ensemble_ready or ScalpingOptimizedEnsembleStatus.optimized_candidate ? "ok" : "needs_attention",
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(Root);
        File.WriteAllText(OptimizerReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(OptimizerMarkdownPath, BuildReportMarkdown(report));
        if (mode == ScalpingEnsembleOptimizationMode.balanced)
        {
            File.WriteAllText(BalancedSelectionPath, JsonSerializer.Serialize(selected, JsonDefaults.WriteOptions));
            File.WriteAllText(BalancedSelectionMarkdownPath, BuildSelectionMarkdown(selected));
        }

        return report;
    }

    public ScalpingEnsembleOptimizerReport? LoadReport()
    {
        return File.Exists(OptimizerReportPath)
            ? JsonSerializer.Deserialize<ScalpingEnsembleOptimizerReport>(File.ReadAllText(OptimizerReportPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public ScalpingOptimizedEnsembleMember? FindMember(string id)
    {
        return LoadReport()?.SelectedEnsemble.Members.FirstOrDefault(member => member.CandidateId.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<IReadOnlyList<ScalpingPortfolioMember>> BuildCombinations(IReadOnlyList<ScalpingPortfolioMember> candidates, ScalpingEnsembleOptimizationMode mode)
    {
        var maxMembers = mode switch
        {
            ScalpingEnsembleOptimizationMode.conservative => 3,
            ScalpingEnsembleOptimizationMode.aggressive_research_only => 5,
            _ => 4
        };
        var ranked = candidates
            .OrderBy(member => member.MaxDrawdown)
            .ThenByDescending(member => member.RecoveryFactor)
            .ThenByDescending(member => member.ProfitFactor)
            .Take(14)
            .ToList();
        for (var size = 2; size <= Math.Min(maxMembers, ranked.Count); size++)
        {
            foreach (var combination in Combinations(ranked, size))
            {
                yield return combination;
            }
        }
    }

    private static IEnumerable<IReadOnlyList<ScalpingPortfolioMember>> Combinations(IReadOnlyList<ScalpingPortfolioMember> items, int size)
    {
        var indices = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return indices.Select(index => items[index]).ToList();
            var position = size - 1;
            while (position >= 0 && indices[position] == items.Count - size + position) position--;
            if (position < 0) yield break;
            indices[position]++;
            for (var index = position + 1; index < size; index++) indices[index] = indices[index - 1] + 1;
        }
    }

    private static ScalpingOptimizedEnsembleSelection ScoreSelection(IReadOnlyList<ScalpingPortfolioMember> members, ScalpingEnsembleOptimizationMode mode, double previousDrawdown, double previousDensity)
    {
        var optimizedDrawdown = Math.Round(members.Sum(member => member.MaxDrawdown) * CorrelationMultiplier(members), 4);
        var optimizedDensity = Math.Round(members.Sum(member => member.SignalDensityScore), 4);
        var assetDiversity = Math.Round(members.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count() / (double)members.Count, 4);
        var setupDiversity = Math.Round(members.Select(member => member.SetupType).Distinct(StringComparer.OrdinalIgnoreCase).Count() / (double)members.Count, 4);
        var sessionDiversity = Math.Round(members.Average(member => member.SessionStrength), 4);
        var correlationPenalty = Math.Round(1 - Math.Min(1, members.Select(member => member.CorrelationGroup).Distinct(StringComparer.OrdinalIgnoreCase).Count() / (double)members.Count), 4);
        var averageProfitFactor = Math.Round(members.Average(member => member.ProfitFactor), 4);
        var averageRecoveryFactor = Math.Round(members.Average(member => member.RecoveryFactor), 4);
        var riskOfRuin = Math.Round(Math.Clamp(optimizedDrawdown / 100 + correlationPenalty * 0.05 - averageRecoveryFactor / 100, 0, 1), 4);
        var stability = Math.Round(Math.Clamp((1 - optimizedDrawdown / 20) * 0.35 + assetDiversity * 0.2 + setupDiversity * 0.15 + Math.Min(1, averageRecoveryFactor / 3) * 0.2 + Math.Min(1, averageProfitFactor / 2) * 0.1, 0, 1), 4);
        var blockers = new List<string>();
        if (members.Count < 2) blockers.Add("minimum_two_certified_candidates_required");
        if (members.Count > 5) blockers.Add("maximum_five_candidates_allowed");
        if (optimizedDrawdown > DrawdownLimit(mode)) blockers.Add("optimized_drawdown_too_high");
        if (correlationPenalty > 0.35) blockers.Add("correlation_penalty_too_high");
        if (optimizedDensity <= 0) blockers.Add("signal_density_not_improved");
        if (assetDiversity < 0.4 && setupDiversity < 0.5) blockers.Add("needs_more_asset_or_setup_diversity");
        if (riskOfRuin > 0.08) blockers.Add("risk_of_ruin_too_high");
        var status = blockers.Count == 0 && stability >= 0.72
            ? ScalpingOptimizedEnsembleStatus.ensemble_ready
            : blockers.Any(blocker => blocker.Contains("diversity", StringComparison.OrdinalIgnoreCase))
                ? ScalpingOptimizedEnsembleStatus.needs_more_diversity
                : blockers.Count >= 3
                    ? ScalpingOptimizedEnsembleStatus.rejected_portfolio
                    : ScalpingOptimizedEnsembleStatus.optimized_candidate;
        return new ScalpingOptimizedEnsembleSelection(
            ReportVersion: "scalping_optimized_ensemble_selection_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Mode: mode,
            Status: status,
            Members: members.Select(member => new ScalpingOptimizedEnsembleMember(
                CandidateId: member.CandidateId,
                Asset: member.Asset,
                SetupType: member.SetupType,
                Confidence: member.Confidence,
                ProfitFactor: member.ProfitFactor,
                RecoveryFactor: member.RecoveryFactor,
                Drawdown: member.MaxDrawdown,
                MaxDailyDrawdown: Math.Round(member.MaxDrawdown * 0.38, 4),
                MaxWeeklyDrawdown: Math.Round(member.MaxDrawdown * 0.68, 4),
                SignalDensityScore: member.SignalDensityScore,
                ContributionReason: BuildContributionReason(member, members),
                RiskNotes: ["certified_candidate_only", "research_only", "human_review_required", $"correlation_group={member.CorrelationGroup}"])).ToList(),
            PreviousPortfolioDrawdown: previousDrawdown,
            OptimizedPortfolioDrawdown: optimizedDrawdown,
            PreviousSignalDensity: previousDensity,
            OptimizedSignalDensity: optimizedDensity,
            AssetDiversityScore: assetDiversity,
            SetupDiversityScore: setupDiversity,
            SessionDiversityScore: sessionDiversity,
            CorrelationPenalty: correlationPenalty,
            AverageProfitFactor: averageProfitFactor,
            AverageRecoveryFactor: averageRecoveryFactor,
            RiskOfRuinEstimate: riskOfRuin,
            EnsembleStability: stability,
            Readiness: status == ScalpingOptimizedEnsembleStatus.ensemble_ready ? "human_review_required_before_use" : "not_ready",
            Blockers: blockers,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static double DrawdownLimit(ScalpingEnsembleOptimizationMode mode) => mode switch
    {
        ScalpingEnsembleOptimizationMode.conservative => 7.5,
        ScalpingEnsembleOptimizationMode.aggressive_research_only => 14.0,
        _ => 10.0
    };

    private static double CorrelationMultiplier(IReadOnlyList<ScalpingPortfolioMember> members)
    {
        var distinctAssets = members.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var distinctSetups = members.Select(member => member.SetupType).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var diversificationCredit = Math.Min(0.35, (distinctAssets - 1) * 0.12 + (distinctSetups - 1) * 0.06);
        return Math.Clamp(1.0 - diversificationCredit, 0.62, 1.0);
    }

    private static double SelectionScore(ScalpingOptimizedEnsembleSelection selection, ScalpingEnsembleOptimizationMode mode)
    {
        var riskWeight = mode == ScalpingEnsembleOptimizationMode.conservative ? 0.45 : mode == ScalpingEnsembleOptimizationMode.aggressive_research_only ? 0.2 : 0.32;
        var densityWeight = mode == ScalpingEnsembleOptimizationMode.aggressive_research_only ? 0.35 : mode == ScalpingEnsembleOptimizationMode.conservative ? 0.12 : 0.24;
        return selection.EnsembleStability * 0.28
            + (1 - Math.Min(1, selection.OptimizedPortfolioDrawdown / 18)) * riskWeight
            + Math.Min(1, selection.OptimizedSignalDensity / 8) * densityWeight
            + selection.AssetDiversityScore * 0.08
            + selection.SetupDiversityScore * 0.08
            - selection.CorrelationPenalty * 0.12
            - selection.Blockers.Count * 0.04;
    }

    private static string BuildContributionReason(ScalpingPortfolioMember member, IReadOnlyList<ScalpingPortfolioMember> selection)
    {
        var reasons = new List<string>();
        if (selection.Count(item => item.Asset.Equals(member.Asset, StringComparison.OrdinalIgnoreCase)) == 1) reasons.Add("asset_diversifier");
        if (selection.Count(item => item.SetupType.Equals(member.SetupType, StringComparison.OrdinalIgnoreCase)) == 1) reasons.Add("setup_diversifier");
        if (member.RecoveryFactor >= selection.Average(item => item.RecoveryFactor)) reasons.Add("recovery_factor_support");
        if (member.MaxDrawdown <= selection.Average(item => item.MaxDrawdown)) reasons.Add("drawdown_reducer");
        return reasons.Count == 0 ? "balanced_member" : string.Join(",", reasons);
    }

    private static ScalpingOptimizedEnsembleSelection EmptySelection(ScalpingEnsembleOptimizationMode mode, double previousDrawdown, double previousDensity, string blocker) => new(
        ReportVersion: "scalping_optimized_ensemble_selection_v1",
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Mode: mode,
        Status: ScalpingOptimizedEnsembleStatus.building,
        Members: [],
        PreviousPortfolioDrawdown: previousDrawdown,
        OptimizedPortfolioDrawdown: 0,
        PreviousSignalDensity: previousDensity,
        OptimizedSignalDensity: 0,
        AssetDiversityScore: 0,
        SetupDiversityScore: 0,
        SessionDiversityScore: 0,
        CorrelationPenalty: 0,
        AverageProfitFactor: 0,
        AverageRecoveryFactor: 0,
        RiskOfRuinEstimate: 1,
        EnsembleStability: 0,
        Readiness: "not_ready",
        Blockers: [blocker],
        NoAutoTrading: true,
        HumanReviewRequired: true,
        BrokerOrdersEnabled: false,
        LiveTradingEnabled: false);

    private static string BuildReportMarkdown(ScalpingEnsembleOptimizerReport report) => $"""
# Scalping Ensemble Optimizer

- mode: {report.Mode}
- health: {report.OptimizerHealth}
- certified_candidates_evaluated: {report.CertifiedCandidatesEvaluated}
- combinations_evaluated: {report.CombinationsEvaluated}
- no_auto_trading: true
- human_review_required: true

## Selected Ensemble
{BuildSelectionMarkdown(report.SelectedEnsemble)}
""";

    private static string BuildSelectionMarkdown(ScalpingOptimizedEnsembleSelection selection) => $"""
# Selected Scalping Ensemble

- mode: {selection.Mode}
- status: {selection.Status}
- members: {selection.Members.Count}
- previous_portfolio_drawdown: {selection.PreviousPortfolioDrawdown:0.####}
- optimized_portfolio_drawdown: {selection.OptimizedPortfolioDrawdown:0.####}
- previous_signal_density: {selection.PreviousSignalDensity:0.####}
- optimized_signal_density: {selection.OptimizedSignalDensity:0.####}
- asset_diversity_score: {selection.AssetDiversityScore:0.####}
- setup_diversity_score: {selection.SetupDiversityScore:0.####}
- correlation_penalty: {selection.CorrelationPenalty:0.####}
- ensemble_stability: {selection.EnsembleStability:0.####}
- readiness: {selection.Readiness}
- no_auto_trading: true
- human_review_required: true

## Members
{string.Join(Environment.NewLine, selection.Members.Select(member => $"- {member.CandidateId}: {member.Asset}/{member.SetupType}, pf={member.ProfitFactor:0.####}, recovery={member.RecoveryFactor:0.####}, drawdown={member.Drawdown:0.####}, contribution={member.ContributionReason}"))}

## Blockers
{Bullets(selection.Blockers)}

Research-only optimizer output. No broker orders, no live trading, no cTrader Order API.
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Any() ? items.Select(item => $"- {item}") : ["- none"]);
}
