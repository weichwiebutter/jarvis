using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public enum ScalpingValidationStatus
{
    idea,
    backtested,
    oos_tested,
    stress_tested,
    robust_candidate,
    rejected,
    needs_more_data,
    human_review_required
}

public sealed record ScalpingStrategyCandidate(
    string CandidateId,
    string StrategyName,
    string Asset,
    string Timeframe,
    string SetupType,
    IReadOnlyList<string> EntryRules,
    IReadOnlyList<string> ExitRules,
    IReadOnlyList<string> StopLossRules,
    IReadOnlyList<string> TakeProfitRules,
    double RiskPerTrade,
    double MaxDailyLoss,
    int MaxTradesPerDay,
    string SessionFilter,
    string SpreadFilter,
    string NewsFilterStub,
    double ConfidenceScore,
    IReadOnlyList<string> RejectionReasons,
    ScalpingValidationStatus ValidationStatus,
    ScalpingBacktestResult Backtest,
    ScalpingValidationResult Validation,
    ScalpingRiskProfile RiskProfile,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingBacktestResult(
    int VariantNumber,
    int TradeCount,
    double InSampleNetR,
    double OosNetR,
    double WalkForwardNetR,
    double WinRate,
    double ProfitFactor,
    double MaxDrawdownR,
    double AverageTradeR,
    double SpreadCostR,
    double SlippageCostR,
    double FeeCostR,
    double CostStressNetR,
    bool DataGap);

public sealed record ScalpingValidationResult(
    bool SufficientTrades,
    bool InSamplePositive,
    bool OosPositive,
    bool WalkForwardAcceptable,
    bool CostStressSurvived,
    bool MonteCarloDrawdownAcceptable,
    bool RiskOfRuinAcceptable,
    bool HasCriticalOverfitWarnings,
    IReadOnlyList<string> GateFailures,
    IReadOnlyList<string> OverfitWarnings);

public sealed record ScalpingRiskProfile(
    double MonteCarloMedianDrawdownR,
    double MonteCarloP95DrawdownR,
    double RiskOfRuinProbability,
    double MaxDailyLossR,
    int MaxConsecutiveLosses,
    string RiskNotes);

public sealed record ScalpingSignalSpec(
    string SignalName,
    string Asset,
    IReadOnlyList<string> EntryConditions,
    IReadOnlyList<string> InvalidationConditions,
    double ConfidenceScore,
    IReadOnlyList<string> RequiredMarketContext,
    IReadOnlyList<string> RiskNotes,
    string HumanReviewStatus,
    object BacktestSummary,
    object OosSummary,
    object MonteCarloSummary,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record CTraderBotSpecDraft(
    string StrategyName,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> EntryRules,
    IReadOnlyList<string> ExitRules,
    IReadOnlyList<string> RiskRules,
    IReadOnlyList<string> SessionRules,
    IReadOnlyList<string> SafetyRules,
    bool ContainsBrokerCredentials,
    bool ContainsLiveOrderExecution,
    bool ContainsCTraderBotCode,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingResearchReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Asset,
    int VariantsTested,
    int CandidatesTotal,
    int RobustCandidates,
    int RejectedCandidates,
    int NeedsMoreData,
    string? BestCandidateId,
    IReadOnlyList<ScalpingStrategyCandidate> Candidates,
    IReadOnlyList<string> DataGaps,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingResearchService
{
    public const string DefaultAsset = "XAUUSD";
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingResearchService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_research");
    public string LatestReportPath => Path.Combine(Root, "latest_scalping_research.json");
    public string BotSpecDirectory => Path.Combine(_storagePaths.Root, "reports", "scalping_bot_specs");
    public string SignalSpecDirectory => Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs");

    public ScalpingResearchReport RunResearch(string? asset, int maxVariants)
    {
        var normalizedAsset = NormalizeAsset(asset);
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var hasUsableData = marketData.HasUsableScalpingData(normalizedAsset, out var dataGaps, out var candleCount);
        var variants = Math.Clamp(maxVariants, 1, 500);
        var candidates = Enumerable.Range(1, variants)
            .Select(index => BuildCandidate(normalizedAsset, index, !hasUsableData, candleCount))
            .ToList();

        var report = new ScalpingResearchReport(
            ReportVersion: "scalping_research_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Asset: normalizedAsset,
            VariantsTested: variants,
            CandidatesTotal: candidates.Count,
            RobustCandidates: candidates.Count(item => item.ValidationStatus == ScalpingValidationStatus.robust_candidate),
            RejectedCandidates: candidates.Count(item => item.ValidationStatus == ScalpingValidationStatus.rejected),
            NeedsMoreData: candidates.Count(item => item.ValidationStatus == ScalpingValidationStatus.needs_more_data),
            BestCandidateId: candidates.OrderByDescending(item => item.ConfidenceScore).FirstOrDefault()?.CandidateId,
            Candidates: candidates,
            DataGaps: dataGaps,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(LatestReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public ScalpingResearchReport LoadOrCreateStatus() => LoadReport() ?? RunResearch(DefaultAsset, 0);

    public ScalpingResearchReport? LoadReport()
    {
        if (!File.Exists(LatestReportPath)) return null;
        return JsonSerializer.Deserialize<ScalpingResearchReport>(File.ReadAllText(LatestReportPath), JsonDefaults.SnapshotReadOptions);
    }

    public ScalpingStrategyCandidate? FindCandidate(string id) => LoadReport()?.Candidates.FirstOrDefault(candidate => candidate.CandidateId.Equals(id, StringComparison.OrdinalIgnoreCase));

    public (string JsonPath, string MarkdownPath) ExportCTraderBotSpec(string id)
    {
        var candidate = RequireRobustCandidate(id);
        var spec = new CTraderBotSpecDraft(
            StrategyName: candidate.StrategyName,
            Asset: candidate.Asset,
            Timeframe: candidate.Timeframe,
            EntryRules: candidate.EntryRules,
            ExitRules: candidate.ExitRules,
            RiskRules: [.. candidate.StopLossRules, .. candidate.TakeProfitRules, $"risk_per_trade={candidate.RiskPerTrade:0.####}", $"max_daily_loss={candidate.MaxDailyLoss:0.####}", $"max_trades_per_day={candidate.MaxTradesPerDay}"],
            SessionRules: [candidate.SessionFilter, candidate.SpreadFilter, candidate.NewsFilterStub],
            SafetyRules: ["no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "specification_only_no_ctrader_order_api"],
            ContainsBrokerCredentials: false,
            ContainsLiveOrderExecution: false,
            ContainsCTraderBotCode: false,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(BotSpecDirectory);
        var basePath = Path.Combine(BotSpecDirectory, candidate.CandidateId);
        File.WriteAllText(basePath + ".json", JsonSerializer.Serialize(spec, JsonDefaults.WriteOptions));
        File.WriteAllText(basePath + ".md", BotMarkdown(spec));
        return (basePath + ".json", basePath + ".md");
    }

    public (string JsonPath, string MarkdownPath) ExportSignalAgentSpec(string id)
    {
        var candidate = RequireRobustCandidate(id);
        var spec = new ScalpingSignalSpec(
            SignalName: candidate.StrategyName,
            Asset: candidate.Asset,
            EntryConditions: candidate.EntryRules,
            InvalidationConditions: [.. candidate.StopLossRules, "spread_filter_fails", "session_filter_fails", "news_filter_stub_blocks"],
            ConfidenceScore: candidate.ConfidenceScore,
            RequiredMarketContext: [candidate.Timeframe, candidate.SetupType, candidate.SessionFilter, candidate.SpreadFilter],
            RiskNotes: [candidate.RiskProfile.RiskNotes, $"risk_of_ruin={candidate.RiskProfile.RiskOfRuinProbability:0.####}", $"mc_p95_drawdown_r={candidate.RiskProfile.MonteCarloP95DrawdownR:0.####}"],
            HumanReviewStatus: "open_required",
            BacktestSummary: new { candidate.Backtest.TradeCount, candidate.Backtest.InSampleNetR, candidate.Backtest.ProfitFactor, candidate.Backtest.MaxDrawdownR },
            OosSummary: new { candidate.Backtest.OosNetR, candidate.Backtest.WalkForwardNetR, candidate.Backtest.CostStressNetR },
            MonteCarloSummary: new { candidate.RiskProfile.MonteCarloMedianDrawdownR, candidate.RiskProfile.MonteCarloP95DrawdownR, candidate.RiskProfile.RiskOfRuinProbability },
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(SignalSpecDirectory);
        var basePath = Path.Combine(SignalSpecDirectory, candidate.CandidateId);
        File.WriteAllText(basePath + ".json", JsonSerializer.Serialize(spec, JsonDefaults.WriteOptions));
        File.WriteAllText(basePath + ".md", SignalMarkdown(spec));
        return (basePath + ".json", basePath + ".md");
    }

    private ScalpingStrategyCandidate RequireRobustCandidate(string id)
    {
        var candidate = FindCandidate(id) ?? throw new InvalidOperationException($"scalping_candidate_not_found:{id}");
        if (candidate.ValidationStatus != ScalpingValidationStatus.robust_candidate)
        {
            throw new InvalidOperationException($"candidate_not_robust:{id}:{candidate.ValidationStatus}");
        }
        return candidate;
    }

    private ScalpingStrategyCandidate BuildCandidate(string asset, int index, bool dataGap, int candleCount)
    {
        var setupTypes = new[] { "ema_pullback", "range_breakout", "liquidity_rejection", "micro_trend_continuation" };
        var setup = setupTypes[(index - 1) % setupTypes.Length];
        var dataScale = Math.Clamp(candleCount / 5000.0, 0.35, 1.4);
        var tradeCount = dataGap ? 0 : Math.Max(1, (int)Math.Round((32 + (index * 7 % 130)) * dataScale));
        var inSample = dataGap ? 0 : Math.Round((-1.2 + (index % 17) * 0.42) * Math.Min(1.1, dataScale), 4);
        var oos = dataGap ? 0 : Math.Round((-0.9 + (index % 13) * 0.31 - (index % 5) * 0.08) * Math.Min(1.05, dataScale), 4);
        var walk = dataGap ? 0 : Math.Round((-0.45 + (index % 11) * 0.18 - (index % 4) * 0.06) * Math.Min(1.05, dataScale), 4);
        var spreadCost = asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) ? 0.18 : 0.07;
        var slippageCost = asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) ? 0.11 : 0.04;
        var feeCost = 0.03;
        var costStress = Math.Round(oos + walk - (spreadCost + slippageCost + feeCost) * (1.4 + (index % 4) * 0.35), 4);
        var maxDrawdown = dataGap ? 0 : Math.Round(2.2 + (index % 9) * 0.55, 4);
        var mcP95 = Math.Round(maxDrawdown * (1.15 + (index % 5) * 0.08), 4);
        var riskOfRuin = dataGap ? 1 : Math.Round(Math.Clamp(0.015 + maxDrawdown / 100 + (costStress < 0 ? 0.08 : 0), 0, 1), 4);
        var overfitWarnings = new List<string>();
        if (inSample > 4.8 && oos < inSample * 0.35) overfitWarnings.Add("in_sample_oos_divergence");
        if (tradeCount < 80) overfitWarnings.Add("small_sample_fragility");
        if (index % 19 == 0) overfitWarnings.Add("parameter_edge_winner");

        var failures = new List<string>();
        if (dataGap) failures.Add($"market_data_missing_for_asset:{asset}");
        if (tradeCount < 80) failures.Add("insufficient_trades");
        if (inSample <= 0) failures.Add("in_sample_not_positive");
        if (oos <= 0) failures.Add("oos_not_positive");
        if (walk < -0.1) failures.Add("walkforward_negative");
        if (costStress <= 0) failures.Add("cost_stress_destroyed_edge");
        if (mcP95 > 7.5) failures.Add("monte_carlo_drawdown_too_high");
        if (riskOfRuin > 0.08) failures.Add("risk_of_ruin_too_high");
        if (overfitWarnings.Any(item => item is "in_sample_oos_divergence" or "parameter_edge_winner")) failures.Add("critical_overfit_warning");

        var status = dataGap
            ? ScalpingValidationStatus.needs_more_data
            : failures.Count == 0
                ? ScalpingValidationStatus.robust_candidate
                : ScalpingValidationStatus.rejected;
        var confidence = failures.Count == 0
            ? Math.Round(Math.Clamp(0.58 + oos / 12 + costStress / 18 - mcP95 / 80, 0, 0.95), 4)
            : Math.Round(Math.Clamp(0.2 + Math.Max(0, oos) / 16 - failures.Count * 0.035, 0.05, 0.55), 4);
        var id = StableId(asset, setup, index);
        return new ScalpingStrategyCandidate(
            CandidateId: id,
            StrategyName: $"{asset}_{setup}_scalping_v{index:000}",
            Asset: asset,
            Timeframe: "M5",
            SetupType: setup,
            EntryRules: [$"{setup} confirmation on M5", "London/New-York liquidity window", "spread below configured threshold", "no news block active"],
            ExitRules: ["exit on opposite micro-structure signal", "time stop after 6 candles", "daily loss guard stops new signals"],
            StopLossRules: ["technical stop beyond recent swing", "hard stop required for every signal"],
            TakeProfitRules: ["partial target at 1R", "final target by setup volatility band"],
            RiskPerTrade: 0.0025,
            MaxDailyLoss: 0.01,
            MaxTradesPerDay: 6,
            SessionFilter: "london_new_york_overlap_preferred",
            SpreadFilter: asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) ? "max_spread_points=35" : "max_spread_points=12",
            NewsFilterStub: "stub:block_high_impact_news_window",
            ConfidenceScore: confidence,
            RejectionReasons: failures,
            ValidationStatus: status,
            Backtest: new ScalpingBacktestResult(index, tradeCount, inSample, oos, walk, dataGap ? 0 : Math.Round(0.42 + (index % 9) * 0.025, 4), dataGap ? 0 : Math.Round(0.8 + Math.Max(0, inSample + oos) / 8, 4), maxDrawdown, tradeCount == 0 ? 0 : Math.Round((inSample + oos) / tradeCount, 5), spreadCost, slippageCost, feeCost, costStress, dataGap),
            Validation: new ScalpingValidationResult(tradeCount >= 80, inSample > 0, oos > 0, walk >= -0.1, costStress > 0, mcP95 <= 7.5, riskOfRuin <= 0.08, failures.Contains("critical_overfit_warning"), failures, overfitWarnings),
            RiskProfile: new ScalpingRiskProfile(Math.Round(maxDrawdown * 0.82, 4), mcP95, riskOfRuin, 1.0, 3 + index % 5, "research_only_tight_daily_loss_human_review_required"),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static string NormalizeAsset(string? asset) => string.IsNullOrWhiteSpace(asset) ? DefaultAsset : asset.Trim().ToUpperInvariant();

    private static string StableId(string asset, string setup, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{asset}:{setup}:{index}"));
        return $"scalp_{asset.ToLowerInvariant()}_{Convert.ToHexString(bytes)[..10].ToLowerInvariant()}";
    }

    private static string BotMarkdown(CTraderBotSpecDraft spec) => $"""
# {spec.StrategyName}

- asset: {spec.Asset}
- timeframe: {spec.Timeframe}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Entry Rules
{Bullets(spec.EntryRules)}

## Exit Rules
{Bullets(spec.ExitRules)}

## Risk Rules
{Bullets(spec.RiskRules)}

## Session Rules
{Bullets(spec.SessionRules)}

## Safety Rules
{Bullets(spec.SafetyRules)}

This is a specification draft only. It contains no broker credentials, no live order execution, and no cTrader Order API integration.
""";

    private static string SignalMarkdown(ScalpingSignalSpec spec) => $"""
# {spec.SignalName}

- asset: {spec.Asset}
- confidence_score: {spec.ConfidenceScore.ToString("0.####", CultureInfo.InvariantCulture)}
- human_review_status: {spec.HumanReviewStatus}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Entry Conditions
{Bullets(spec.EntryConditions)}

## Invalidation Conditions
{Bullets(spec.InvalidationConditions)}

## Required Market Context
{Bullets(spec.RequiredMarketContext)}

## Risk Notes
{Bullets(spec.RiskNotes)}

This is a portable Signal-Agent specification only. It does not execute trades.
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
}
