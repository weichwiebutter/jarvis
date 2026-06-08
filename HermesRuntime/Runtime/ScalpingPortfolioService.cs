using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScalpingPortfolioMember(
    string CandidateId,
    string Asset,
    string Timeframe,
    string SetupType,
    string Status,
    double Confidence,
    double ProfitFactor,
    double RecoveryFactor,
    double MaxDrawdown,
    double SessionStrength,
    double RegimeStrength,
    string CorrelationGroup,
    double DiversityScore,
    double SignalDensityScore,
    string EnsembleReadiness);

public sealed record ScalpingPortfolioEvaluation(
    string Status,
    int CertifiedCandidates,
    int EnsembleCandidates,
    double SignalDensityScore,
    double DiversityScore,
    string DrawdownProfile,
    IReadOnlyList<string> Blockers,
    string NextCandidateSearchAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingSignalEnsemblePlan(
    string PlanVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    IReadOnlyList<string> CandidateSelectionRules,
    IReadOnlyList<string> CorrelationControls,
    IReadOnlyList<string> EnsembleReadinessGates,
    IReadOnlyList<string> NextActions,
    IReadOnlyList<string> SafetyRules,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingCandidatePortfolio(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ScalpingPortfolioMember> Members,
    ScalpingPortfolioEvaluation Evaluation,
    ScalpingSignalEnsemblePlan EnsemblePlan,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingEnsembleCandidateV1(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    IReadOnlyList<ScalpingPortfolioMember> Members,
    double DiversityScore,
    double SignalDensityScore,
    double SessionCoverageScore,
    string DrawdownImpact,
    string Correlation,
    string EnsembleReadiness,
    IReadOnlyList<string> Blockers,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingPortfolioService
{
    private static readonly string[] TargetSetups = ["range_breakout", "ema_pullback", "liquidity_rejection", "micro_trend_continuation"];
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingPortfolioService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio");
    public string PortfolioStatusPath => Path.Combine(Root, "portfolio_status.json");
    public string PortfolioMarkdownPath => Path.Combine(Root, "portfolio_status.md");
    public string EnsemblePlanPath => Path.Combine(Root, "ensemble_plan.json");
    public string EnsemblePlanMarkdownPath => Path.Combine(Root, "ensemble_plan.md");
    public string EnsembleCandidatePath => Path.Combine(Root, "ensemble_candidate_v1.json");
    public string EnsembleCandidateMarkdownPath => Path.Combine(Root, "ensemble_candidate_v1.md");

    public ScalpingCandidatePortfolio Build()
    {
        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();
        var certifications = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReports();
        var expansions = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReports();
        var members = BuildMembers(research, certifications, expansions).ToList();
        var certified = members.Where(member => member.Status == ScalpingCertificationStatus.certified_candidate.ToString()).ToList();
        var diversity = certified.Count == 0 ? 0 : Math.Round(certified.Average(member => member.DiversityScore), 4);
        var density = certified.Count == 0 ? 0 : Math.Round(certified.Sum(member => member.SignalDensityScore), 4);
        var identicalCorrelation = certified.Count > 1 && certified.Select(member => member.CorrelationGroup).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        var drawdownOk = certified.Count == 0 || certified.Max(member => member.MaxDrawdown) <= 4.5;
        var blockers = new List<string>();
        if (certified.Count < 2) blockers.Add("minimum_two_certified_candidates_required");
        if (identicalCorrelation) blockers.Add("certified_candidates_identically_correlated");
        if (!drawdownOk) blockers.Add("portfolio_drawdown_profile_too_high");
        var ensembleCandidates = certified.Count >= 2 && blockers.Count == 0 ? certified.Count : 0;
        var status = ensembleCandidates >= 2 ? "ensemble_candidate" : "building";
        var nextAction = status == "ensemble_candidate" ? "human_review_ensemble_plan" : "search_more_candidates";
        var evaluation = new ScalpingPortfolioEvaluation(
            Status: status,
            CertifiedCandidates: certified.Count,
            EnsembleCandidates: ensembleCandidates,
            SignalDensityScore: density,
            DiversityScore: diversity,
            DrawdownProfile: drawdownOk ? "acceptable" : "needs_attention",
            Blockers: blockers,
            NextCandidateSearchAction: nextAction,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        var plan = BuildPlan(status, nextAction, blockers);
        var portfolio = new ScalpingCandidatePortfolio(
            ReportVersion: "scalping_candidate_portfolio_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Members: members,
            Evaluation: evaluation,
            EnsemblePlan: plan,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(Root);
        File.WriteAllText(PortfolioStatusPath, JsonSerializer.Serialize(portfolio, JsonDefaults.WriteOptions));
        File.WriteAllText(PortfolioMarkdownPath, BuildPortfolioMarkdown(portfolio));
        File.WriteAllText(EnsemblePlanPath, JsonSerializer.Serialize(plan, JsonDefaults.WriteOptions));
        File.WriteAllText(EnsemblePlanMarkdownPath, BuildPlanMarkdown(plan, portfolio));
        WriteEnsembleCandidate(portfolio);
        return portfolio;
    }

    public ScalpingCandidatePortfolio? Load()
    {
        return File.Exists(PortfolioStatusPath)
            ? JsonSerializer.Deserialize<ScalpingCandidatePortfolio>(File.ReadAllText(PortfolioStatusPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public ScalpingResearchReport SearchMoreCandidates(string? asset, int maxVariants)
    {
        var report = new ScalpingResearchService(_storagePaths, _runtimeRoot).RunResearch(asset, maxVariants);
        var expansion = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot);
        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        foreach (var candidate in report.Candidates.Where(candidate => candidate.ValidationStatus == ScalpingValidationStatus.robust_candidate))
        {
            var expanded = expansion.Expand(candidate.CandidateId);
            if (expanded.Status == ScalpingExpansionStatus.final_candidate)
            {
                certification.Certify(candidate.CandidateId);
            }
        }

        Build();
        return report;
    }

    private static IEnumerable<ScalpingPortfolioMember> BuildMembers(ScalpingResearchReport research, IReadOnlyList<ScalpingCertificationReport> certifications, IReadOnlyList<ScalpingRobustnessExpansionReport> expansions)
    {
        foreach (var candidate in research.Candidates)
        {
            var certification = certifications.FirstOrDefault(report => report.CandidateId.Equals(candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
            var expansion = expansions.FirstOrDefault(report => report.CandidateId.Equals(candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
            var status = certification?.Status.ToString() ?? expansion?.Status.ToString() ?? candidate.ValidationStatus.ToString();
            var sessionStrength = certification is null || certification.SessionValidation.Count == 0
                ? EstimateSessionStrength(candidate)
                : Math.Round(certification.SessionValidation.Count(session => session.Status == "positive") / (double)certification.SessionValidation.Count, 4);
            var regimeStrength = expansion is null ? EstimateRegimeStrength(candidate) : Math.Round(expansion.RegimeValidation.PositiveOrNeutralRegimes / 7.0, 4);
            var diversity = CalculateDiversity(candidate, sessionStrength, regimeStrength);
            var density = CalculateSignalDensity(candidate, certification);
            yield return new ScalpingPortfolioMember(
                CandidateId: candidate.CandidateId,
                Asset: candidate.Asset,
                Timeframe: candidate.Timeframe,
                SetupType: candidate.SetupType,
                Status: status,
                Confidence: candidate.ConfidenceScore,
                ProfitFactor: certification?.DrawdownCertification.ProfitFactor ?? candidate.Backtest.ProfitFactor,
                RecoveryFactor: certification?.DrawdownCertification.RecoveryFactor ?? 0,
                MaxDrawdown: certification?.DrawdownCertification.MaxDrawdownR ?? candidate.Backtest.MaxDrawdownR,
                SessionStrength: sessionStrength,
                RegimeStrength: regimeStrength,
                CorrelationGroup: $"{candidate.Asset}:{candidate.Timeframe}:{candidate.SetupType}:{candidate.SessionFilter}",
                DiversityScore: diversity,
                SignalDensityScore: density,
                EnsembleReadiness: status == ScalpingCertificationStatus.certified_candidate.ToString() && diversity >= 0.45 ? "ready_member" : "not_ready");
        }

        foreach (var certification in certifications.Where(report => !research.Candidates.Any(candidate => candidate.CandidateId.Equals(report.CandidateId, StringComparison.OrdinalIgnoreCase))))
        {
            var sessionStrength = certification.SessionValidation.Count == 0
                ? 0.5
                : Math.Round(certification.SessionValidation.Count(session => session.Status == "positive") / (double)certification.SessionValidation.Count, 4);
            var regimeStrength = expansions.FirstOrDefault(report => report.CandidateId.Equals(certification.CandidateId, StringComparison.OrdinalIgnoreCase))?.RegimeValidation.PositiveOrNeutralRegimes / 7.0 ?? EstimateRegimeStrength(certification.SetupType);
            var diversity = CalculateDiversity(certification.Asset, certification.Timeframe, certification.SetupType, sessionStrength, regimeStrength);
            yield return new ScalpingPortfolioMember(
                CandidateId: certification.CandidateId,
                Asset: certification.Asset,
                Timeframe: certification.Timeframe,
                SetupType: certification.SetupType,
                Status: certification.Status.ToString(),
                Confidence: 0,
                ProfitFactor: certification.DrawdownCertification.ProfitFactor,
                RecoveryFactor: certification.DrawdownCertification.RecoveryFactor,
                MaxDrawdown: certification.DrawdownCertification.MaxDrawdownR,
                SessionStrength: sessionStrength,
                RegimeStrength: Math.Round(regimeStrength, 4),
                CorrelationGroup: $"{certification.Asset}:{certification.Timeframe}:{certification.SetupType}:certified_sessions",
                DiversityScore: diversity,
                SignalDensityScore: Math.Round(Math.Clamp(certification.SessionValidation.Sum(session => session.TradeCount) / 180.0, 0, 2.5), 4),
                EnsembleReadiness: certification.Status == ScalpingCertificationStatus.certified_candidate ? "ready_member" : "not_ready");
        }
    }

    private static ScalpingSignalEnsemblePlan BuildPlan(string status, string nextAction, IReadOnlyList<string> blockers) => new(
        PlanVersion: "scalping_signal_ensemble_plan_v1",
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Status: status,
        CandidateSelectionRules: ["include_certified_candidates_only", "prefer_distinct_setup_types", "prefer_distinct_correlation_groups", "keep_xauusd_as_primary_asset", "include_eurusd_only_when_market_data_and_certification_exist"],
        CorrelationControls: ["penalize_same_asset_same_timeframe_same_setup", "penalize_same_session_and_entry_logic", "require_non_identical_correlation_groups_for_ensemble_candidate"],
        EnsembleReadinessGates: ["minimum_two_certified_candidates", "drawdown_profile_not_worse", "signal_density_increases", "no_safety_rule_violations", "human_review_required"],
        NextActions: blockers.Count == 0 ? [nextAction] : [nextAction, .. blockers],
        SafetyRules: ["research_only", "no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "no_ctrader_order_api"],
        NoAutoTrading: true,
        HumanReviewRequired: true,
        BrokerOrdersEnabled: false,
        LiveTradingEnabled: false);

    private static double EstimateSessionStrength(ScalpingStrategyCandidate candidate) => candidate.SessionFilter.Contains("overlap", StringComparison.OrdinalIgnoreCase) ? 0.75 : 0.55;

    private static double EstimateRegimeStrength(ScalpingStrategyCandidate candidate) => candidate.SetupType switch
    {
        "range_breakout" => 0.72,
        "ema_pullback" => 0.62,
        "liquidity_rejection" => 0.66,
        "micro_trend_continuation" => 0.68,
        _ => 0.5
    };

    private static double EstimateRegimeStrength(string setupType) => setupType switch
    {
        "range_breakout" => 0.72,
        "ema_pullback" => 0.62,
        "liquidity_rejection" => 0.66,
        "micro_trend_continuation" => 0.68,
        _ => 0.5
    };

    private static double CalculateDiversity(ScalpingStrategyCandidate candidate, double sessionStrength, double regimeStrength)
    {
        var setupDiversity = Array.IndexOf(TargetSetups, candidate.SetupType) >= 0 ? 0.45 : 0.25;
        var sessionDiversity = candidate.SessionFilter.Contains("overlap", StringComparison.OrdinalIgnoreCase) ? 0.15 : 0.25;
        return Math.Round(Math.Clamp(setupDiversity + sessionDiversity + regimeStrength * 0.25 + sessionStrength * 0.15, 0, 1), 4);
    }

    private static double CalculateDiversity(string asset, string timeframe, string setupType, double sessionStrength, double regimeStrength)
    {
        var setupDiversity = Array.IndexOf(TargetSetups, setupType) >= 0 ? 0.45 : 0.25;
        var assetDiversity = asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) ? 0.15 : 0.25;
        var timeframePenalty = timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase) ? 0 : -0.05;
        return Math.Round(Math.Clamp(setupDiversity + assetDiversity + timeframePenalty + regimeStrength * 0.2 + sessionStrength * 0.15, 0, 1), 4);
    }

    private static double CalculateSignalDensity(ScalpingStrategyCandidate candidate, ScalpingCertificationReport? certification)
    {
        var trades = candidate.Backtest.TradeCount;
        var sessionMultiplier = certification is null ? 1.0 : Math.Max(1.0, certification.SessionValidation.Count(session => session.Status == "positive") / 2.0);
        return Math.Round(Math.Clamp(trades / 180.0 * sessionMultiplier, 0, 2.5), 4);
    }

    private static string BuildPortfolioMarkdown(ScalpingCandidatePortfolio portfolio) => $"""
# Scalping Candidate Portfolio

- status: {portfolio.Evaluation.Status}
- certified_candidates: {portfolio.Evaluation.CertifiedCandidates}
- ensemble_candidates: {portfolio.Evaluation.EnsembleCandidates}
- signal_density_score: {portfolio.Evaluation.SignalDensityScore:0.####}
- diversity_score: {portfolio.Evaluation.DiversityScore:0.####}
- next_candidate_search_action: {portfolio.Evaluation.NextCandidateSearchAction}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Members
{string.Join(Environment.NewLine, portfolio.Members.Select(member => $"- {member.CandidateId}: {member.Asset}/{member.Timeframe}/{member.SetupType}, status={member.Status}, diversity={member.DiversityScore:0.####}, density={member.SignalDensityScore:0.####}, correlation={member.CorrelationGroup}"))}

## Blockers
{Bullets(portfolio.Evaluation.Blockers)}

## Search Targets
- XAUUSD range_breakout
- XAUUSD ema_pullback
- XAUUSD liquidity_rejection
- XAUUSD micro_trend_continuation
- EURUSD optional when data and certification exist

This portfolio report prepares a future signal ensemble only. It does not execute trades.
""";

    private static string BuildPlanMarkdown(ScalpingSignalEnsemblePlan plan, ScalpingCandidatePortfolio portfolio) => $"""
# Scalping Signal Ensemble Plan

- status: {plan.Status}
- portfolio_status: {portfolio.Evaluation.Status}
- human_review_required: true
- no_auto_trading: true

## Candidate Selection Rules
{Bullets(plan.CandidateSelectionRules)}

## Correlation Controls
{Bullets(plan.CorrelationControls)}

## Ensemble Readiness Gates
{Bullets(plan.EnsembleReadinessGates)}

## Next Actions
{Bullets(plan.NextActions)}

## Safety Rules
{Bullets(plan.SafetyRules)}

Prepared only as a research and reporting plan. No broker orders, no live trading, no cTrader Order API.
""";

    private ScalpingEnsembleCandidateV1 WriteEnsembleCandidate(ScalpingCandidatePortfolio portfolio)
    {
        var certified = portfolio.Members
            .Where(member => member.Status == ScalpingCertificationStatus.certified_candidate.ToString())
            .GroupBy(member => member.CandidateId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(member => member.DiversityScore)
            .ToList();
        var distinctAssets = certified.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var distinctCorrelationGroups = certified.Select(member => member.CorrelationGroup).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var blockers = new List<string>();
        if (certified.Count < 2) blockers.Add("minimum_two_certified_candidates_required");
        if (distinctAssets < 2) blockers.Add("minimum_two_assets_preferred_for_v1");
        if (distinctCorrelationGroups < certified.Count) blockers.Add("correlation_groups_not_distinct");
        var diversity = certified.Count == 0 ? 0 : Math.Round(certified.Average(member => member.DiversityScore), 4);
        var density = certified.Count == 0 ? 0 : Math.Round(certified.Sum(member => member.SignalDensityScore), 4);
        var sessionCoverage = certified.Count == 0 ? 0 : Math.Round(certified.Average(member => member.SessionStrength), 4);
        var maxDrawdown = certified.Count == 0 ? 0 : certified.Max(member => member.MaxDrawdown);
        if (maxDrawdown > 4.5) blockers.Add("drawdown_impact_too_high");
        var status = blockers.Count == 0 ? "ensemble_candidate_v1" : "building";
        var ensemble = new ScalpingEnsembleCandidateV1(
            ReportVersion: "scalping_ensemble_candidate_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            Members: certified,
            DiversityScore: diversity,
            SignalDensityScore: density,
            SessionCoverageScore: sessionCoverage,
            DrawdownImpact: maxDrawdown <= 4.5 ? "acceptable" : "needs_attention",
            Correlation: distinctCorrelationGroups >= certified.Count ? "diversified" : "overlapping",
            EnsembleReadiness: status == "ensemble_candidate_v1" ? "ready_for_human_review" : "not_ready",
            Blockers: blockers,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        File.WriteAllText(EnsembleCandidatePath, JsonSerializer.Serialize(ensemble, JsonDefaults.WriteOptions));
        File.WriteAllText(EnsembleCandidateMarkdownPath, BuildEnsembleMarkdown(ensemble));
        return ensemble;
    }

    public ScalpingEnsembleCandidateV1? LoadEnsembleCandidate()
    {
        return File.Exists(EnsembleCandidatePath)
            ? JsonSerializer.Deserialize<ScalpingEnsembleCandidateV1>(File.ReadAllText(EnsembleCandidatePath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private static string BuildEnsembleMarkdown(ScalpingEnsembleCandidateV1 ensemble) => $"""
# Scalping Ensemble Candidate v1

- status: {ensemble.Status}
- diversity_score: {ensemble.DiversityScore:0.####}
- signal_density_score: {ensemble.SignalDensityScore:0.####}
- session_coverage_score: {ensemble.SessionCoverageScore:0.####}
- drawdown_impact: {ensemble.DrawdownImpact}
- correlation: {ensemble.Correlation}
- ensemble_readiness: {ensemble.EnsembleReadiness}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Members
{string.Join(Environment.NewLine, ensemble.Members.Select(member => $"- {member.CandidateId}: {member.Asset}/{member.Timeframe}/{member.SetupType}, status={member.Status}, density={member.SignalDensityScore:0.####}, diversity={member.DiversityScore:0.####}"))}

## Blockers
{Bullets(ensemble.Blockers)}

Prepared only as a research ensemble candidate. No broker orders, no live trading, no cTrader Order API.
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Any() ? items.Select(item => $"- {item}") : ["- none"]);
}
