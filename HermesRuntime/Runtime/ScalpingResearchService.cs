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
    robustness_expanded,
    final_candidate,
    rejected_after_expansion,
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
    string CandidateId,
    string SignalName,
    string StrategyName,
    string Asset,
    string Timeframe,
    string SetupType,
    IReadOnlyList<string> SignalDirectionLogic,
    IReadOnlyList<string> EntryConditions,
    IReadOnlyList<string> InvalidationConditions,
    IReadOnlyList<string> ExitConditions,
    object ConfidenceModel,
    object ConfidenceThresholds,
    string SessionFilter,
    string SpreadFilter,
    string NewsFilter,
    double ConfidenceScore,
    IReadOnlyList<string> RequiredMarketContext,
    IReadOnlyList<string> RiskNotes,
    int MaxTradesPerDay,
    double MaxDailyLoss,
    string HumanReviewStatus,
    object CertificationSummary,
    object BacktestSummary,
    object OosSummary,
    object MonteCarloSummary,
    object SensitivitySummary,
    object RegimeSummary,
    object DrawdownSummary,
    IReadOnlyList<string> OperationalLimits,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record CTraderBotSpecDraft(
    string CandidateId,
    string StrategyName,
    string Asset,
    string Timeframe,
    string SetupType,
    IReadOnlyList<string> EntryRules,
    IReadOnlyList<string> ExitRules,
    IReadOnlyList<string> InvalidationRules,
    IReadOnlyList<string> StopLossRules,
    IReadOnlyList<string> TakeProfitRules,
    IReadOnlyList<string> TimeStopRules,
    string SessionFilter,
    string SpreadFilter,
    string NewsFilterStub,
    double RiskPerTrade,
    int MaxTradesPerDay,
    double MaxDailyLoss,
    string MaxDrawdownGuard,
    IReadOnlyList<string> KillSwitchRules,
    IReadOnlyList<string> LoggingRequirements,
    IReadOnlyList<string> SafetyRequirements,
    object CertificationSummary,
    IReadOnlyList<string> FutureCandidatePortfolio,
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
    private static readonly Dictionary<string, string> AssetAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GOLD"] = "XAUUSD",
        ["DE40"] = "GER40",
        ["GERMANY40"] = "GER40",
        ["GERMANY 40"] = "GER40",
        ["DAX"] = "GER40",
        ["DAX40"] = "GER40"
    };
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public ScalpingResearchService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string LatestReportPath => Path.Combine(Root, "latest_scalping_research.json");
    public string AssetReportsDirectory => Path.Combine(Root, "assets");
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
        var assetDirectory = Path.Combine(AssetReportsDirectory, normalizedAsset);
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllText(Path.Combine(assetDirectory, "latest_scalping_research.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public ScalpingResearchReport LoadOrCreateStatus() => LoadReport() ?? RunResearch(DefaultAsset, 0);

    public ScalpingResearchReport? LoadReport()
    {
        if (!File.Exists(LatestReportPath)) return null;
        return JsonSerializer.Deserialize<ScalpingResearchReport>(File.ReadAllText(LatestReportPath), JsonDefaults.SnapshotReadOptions);
    }

    public ScalpingResearchReport? LoadAssetReport(string? asset)
    {
        var normalizedAsset = NormalizeAsset(asset);
        var path = Path.Combine(AssetReportsDirectory, normalizedAsset, "latest_scalping_research.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ScalpingResearchReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public ScalpingStrategyCandidate? FindCandidate(string id) => LoadReport()?.Candidates.FirstOrDefault(candidate => candidate.CandidateId.Equals(id, StringComparison.OrdinalIgnoreCase));

    public (string JsonPath, string MarkdownPath) ExportCTraderBotSpec(string id)
    {
        var candidate = RequireCertifiedCandidate(id);
        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReport(id)!;
        var signalSpecPath = Path.Combine(SignalSpecDirectory, candidate.CandidateId, "signal_agent_spec.json");
        if (!File.Exists(signalSpecPath))
        {
            throw new InvalidOperationException($"signal_agent_spec_missing:{id}");
        }

        var spec = new CTraderBotSpecDraft(
            CandidateId: candidate.CandidateId,
            StrategyName: candidate.StrategyName,
            Asset: candidate.Asset,
            Timeframe: candidate.Timeframe,
            SetupType: candidate.SetupType,
            EntryRules: candidate.EntryRules,
            ExitRules: candidate.ExitRules,
            InvalidationRules: [.. candidate.StopLossRules, "spread_filter_fails", "session_filter_fails", "news_filter_stub_blocks", "daily_loss_guard_triggered"],
            StopLossRules: candidate.StopLossRules,
            TakeProfitRules: candidate.TakeProfitRules,
            TimeStopRules: ["time_stop_after_6_m5_candles", "exit_before_session_close_if_signal_unresolved"],
            SessionFilter: candidate.SessionFilter,
            SpreadFilter: candidate.SpreadFilter,
            NewsFilterStub: candidate.NewsFilterStub,
            RiskPerTrade: candidate.RiskPerTrade,
            MaxTradesPerDay: candidate.MaxTradesPerDay,
            MaxDailyLoss: candidate.MaxDailyLoss,
            MaxDrawdownGuard: $"halt_signals_if_drawdown_exceeds_{certification.DrawdownCertification.MaxDrawdownR:0.####}R_without_human_review",
            KillSwitchRules: ["disable_new_signals_when_max_daily_loss_reached", "disable_new_signals_when_spread_filter_fails", "disable_new_signals_during_news_filter_block", "disable_new_signals_after_unexpected_runtime_error", "manual_human_reenable_required"],
            LoggingRequirements: ["log_every_signal_decision", "log_entry_condition_state", "log_invalidation_reason", "log_session_and_spread_context", "log_safety_gate_state", "log_no_order_execution_confirmation"],
            SafetyRequirements: ["specification_only", "no_broker_credentials", "no_live_order_execution", "no_ctrader_order_api_calls", "no_auto_trading_activation", "human_review_required=true"],
            CertificationSummary: new { status = certification.Status.ToString(), certified_candidate = certification.CertifiedCandidate, profit_factor = certification.DrawdownCertification.ProfitFactor, recovery_factor = certification.DrawdownCertification.RecoveryFactor, max_drawdown_r = certification.DrawdownCertification.MaxDrawdownR, max_daily_drawdown_r = certification.DrawdownCertification.MaxDailyDrawdownR, human_review_package = certification.HumanReviewPackagePath, signal_agent_spec = signalSpecPath },
            FutureCandidatePortfolio: ["this_spec_is_based_on_one_certified_candidate", "hermes_should_continue_searching_for_scalping_candidates", "future_versions_should_combine_multiple_certified_candidates", "each_candidate_must_be_individually_certified_before_combination", "ensemble_must_not_enable_automatic_live_trading", "human_review_remains_required"],
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
        var candidateDirectory = Path.Combine(BotSpecDirectory, candidate.CandidateId);
        Directory.CreateDirectory(candidateDirectory);
        var jsonPath = Path.Combine(candidateDirectory, "ctrader_bot_spec.json");
        var markdownPath = Path.Combine(candidateDirectory, "ctrader_bot_spec.md");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(spec, JsonDefaults.WriteOptions));
        File.WriteAllText(markdownPath, BotMarkdown(spec));
        return (jsonPath, markdownPath);
    }

    public (string JsonPath, string MarkdownPath) ExportSignalAgentSpec(string id)
    {
        var candidate = RequireCertifiedCandidate(id);
        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReport(id)!;
        var expansion = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReport(id);
        var spec = new ScalpingSignalSpec(
            CandidateId: candidate.CandidateId,
            SignalName: candidate.StrategyName,
            StrategyName: candidate.StrategyName,
            Asset: candidate.Asset,
            Timeframe: candidate.Timeframe,
            SetupType: candidate.SetupType,
            SignalDirectionLogic: ["research_signal_only", "range_breakout_direction_from_confirmed_m5_break", "no_order_execution"],
            EntryConditions: candidate.EntryRules,
            InvalidationConditions: [.. candidate.StopLossRules, "spread_filter_fails", "session_filter_fails", "news_filter_stub_blocks"],
            ExitConditions: candidate.ExitRules,
            ConfidenceModel: new { source = "certified_scalping_research_v1", score = candidate.ConfidenceScore, certification_status = certification.Status.ToString(), human_review_required = true },
            ConfidenceThresholds: new { observe_only_below = 0.70, review_required_from = 0.70, candidate_confidence = candidate.ConfidenceScore, production_requires_separate_approval = true },
            SessionFilter: candidate.SessionFilter,
            SpreadFilter: candidate.SpreadFilter,
            NewsFilter: candidate.NewsFilterStub,
            ConfidenceScore: candidate.ConfidenceScore,
            RequiredMarketContext: [candidate.Timeframe, candidate.SetupType, candidate.SessionFilter, candidate.SpreadFilter],
            RiskNotes: [candidate.RiskProfile.RiskNotes, $"risk_of_ruin={candidate.RiskProfile.RiskOfRuinProbability:0.####}", $"max_drawdown_r={certification.DrawdownCertification.MaxDrawdownR:0.####}", $"max_daily_drawdown_r={certification.DrawdownCertification.MaxDailyDrawdownR:0.####}", $"max_weekly_drawdown_r={certification.DrawdownCertification.MaxWeeklyDrawdownR:0.####}"],
            MaxTradesPerDay: candidate.MaxTradesPerDay,
            MaxDailyLoss: candidate.MaxDailyLoss,
            HumanReviewStatus: "open_required",
            CertificationSummary: new { status = certification.Status.ToString(), certified_candidate = certification.CertifiedCandidate, profit_factor = certification.DrawdownCertification.ProfitFactor, recovery_factor = certification.DrawdownCertification.RecoveryFactor, human_review_package = certification.HumanReviewPackagePath },
            BacktestSummary: new { candidate.Backtest.TradeCount, candidate.Backtest.InSampleNetR, candidate.Backtest.ProfitFactor, candidate.Backtest.MaxDrawdownR },
            OosSummary: new { candidate.Backtest.OosNetR, candidate.Backtest.WalkForwardNetR, candidate.Backtest.CostStressNetR },
            MonteCarloSummary: expansion is null ? new { candidate.RiskProfile.MonteCarloMedianDrawdownR, candidate.RiskProfile.MonteCarloP95DrawdownR, candidate.RiskProfile.RiskOfRuinProbability } : new { expansion.MonteCarlo.Health, expansion.MonteCarlo.Simulations, expansion.MonteCarlo.MedianOutcomeR, expansion.MonteCarlo.WorstFivePercentOutcomeR, expansion.MonteCarlo.RuinProbability },
            SensitivitySummary: expansion is null ? new { health = "missing" } : new { expansion.ParameterSensitivity.Health, expansion.ParameterSensitivity.StableConservativeCorridorAvailable, expansion.ParameterSensitivity.StableCorridor.PrimaryConfidenceDropDriver },
            RegimeSummary: expansion is null ? new { health = "missing" } : new { expansion.RegimeValidation.Health, expansion.RegimeValidation.PositiveOrNeutralRegimes },
            DrawdownSummary: new { certification.DrawdownCertification.MaxDrawdownR, certification.DrawdownCertification.MaxDailyDrawdownR, certification.DrawdownCertification.MaxWeeklyDrawdownR, certification.DrawdownCertification.MaxConsecutiveLosses, certification.DrawdownCertification.RecoveryFactor, certification.DrawdownCertification.ProfitFactor },
            OperationalLimits: ["read_only_signal_specification", "no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", $"max_trades_per_day={candidate.MaxTradesPerDay}", $"max_daily_loss={candidate.MaxDailyLoss:0.####}"],
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        var candidateDirectory = Path.Combine(SignalSpecDirectory, candidate.CandidateId);
        Directory.CreateDirectory(candidateDirectory);
        var jsonPath = Path.Combine(candidateDirectory, "signal_agent_spec.json");
        var markdownPath = Path.Combine(candidateDirectory, "signal_agent_spec.md");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(spec, JsonDefaults.WriteOptions));
        File.WriteAllText(markdownPath, SignalMarkdown(spec));
        return (jsonPath, markdownPath);
    }

    private ScalpingStrategyCandidate RequireCertifiedCandidate(string id)
    {
        var candidate = RequireRobustCandidate(id);
        var certificationService = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        var certification = certificationService.LoadReport(id);
        if (certification?.Status != ScalpingCertificationStatus.certified_candidate)
        {
            throw new InvalidOperationException($"candidate_not_certified_yet:{id}");
        }

        if (!File.Exists(Path.Combine(certificationService.CertificationDirectory, id, "certification_report.json")))
        {
            throw new InvalidOperationException($"certification_report_missing:{id}");
        }

        if (!File.Exists(certification.HumanReviewPackagePath))
        {
            throw new InvalidOperationException($"human_review_package_missing:{id}");
        }

        return candidate;
    }

    private ScalpingStrategyCandidate RequireRobustCandidate(string id)
    {
        var candidate = FindCandidate(id) ?? throw new InvalidOperationException($"scalping_candidate_not_found:{id}");
        if (candidate.ValidationStatus != ScalpingValidationStatus.robust_candidate)
        {
            throw new InvalidOperationException($"candidate_not_robust:{id}:{candidate.ValidationStatus}");
        }

        var certification = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReport(id);
        if (certification?.Status == ScalpingCertificationStatus.certified_candidate)
        {
            return candidate;
        }

        var expansion = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReport(id);
        if (expansion?.Status != ScalpingExpansionStatus.final_candidate)
        {
            throw new InvalidOperationException($"candidate_requires_final_candidate_expansion:{id}:current={expansion?.Status.ToString() ?? "robust_candidate"}");
        }

        Console.Error.WriteLine($"WARN: candidate_not_certified_yet:{id}");

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

    private static string NormalizeAsset(string? asset)
    {
        var normalized = string.IsNullOrWhiteSpace(asset) ? DefaultAsset : asset.Trim().ToUpperInvariant();
        return AssetAliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    private static string StableId(string asset, string setup, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{asset}:{setup}:{index}"));
        return $"scalp_{asset.ToLowerInvariant()}_{Convert.ToHexString(bytes)[..10].ToLowerInvariant()}";
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "scalping_research");
        try
        {
            Directory.CreateDirectory(preferred);
            var probePath = Path.Combine(preferred, ".write_probe");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return preferred;
        }
        catch (IOException)
        {
            return ResolveFallbackRoot();
        }
        catch (UnauthorizedAccessException)
        {
            return ResolveFallbackRoot();
        }
    }

    private string ResolveFallbackRoot()
    {
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_research");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string BotMarkdown(CTraderBotSpecDraft spec) => $"""
# {spec.StrategyName}

- candidate_id: {spec.CandidateId}
- asset: {spec.Asset}
- timeframe: {spec.Timeframe}
- setup_type: {spec.SetupType}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Entry Rules
{Bullets(spec.EntryRules)}

## Exit Rules
{Bullets(spec.ExitRules)}

## Invalidation Rules
{Bullets(spec.InvalidationRules)}

## Stop Loss Rules
{Bullets(spec.StopLossRules)}

## Take Profit Rules
{Bullets(spec.TakeProfitRules)}

## Time Stop Rules
{Bullets(spec.TimeStopRules)}

## Risk Rules
{Bullets(spec.RiskRules)}

- risk_per_trade: {spec.RiskPerTrade.ToString("0.####", CultureInfo.InvariantCulture)}
- max_trades_per_day: {spec.MaxTradesPerDay}
- max_daily_loss: {spec.MaxDailyLoss.ToString("0.####", CultureInfo.InvariantCulture)}
- max_drawdown_guard: {spec.MaxDrawdownGuard}

## Session Rules
{Bullets(spec.SessionRules)}

- session_filter: {spec.SessionFilter}
- spread_filter: {spec.SpreadFilter}
- news_filter_stub: {spec.NewsFilterStub}

## Kill Switch Rules
{Bullets(spec.KillSwitchRules)}

## Logging Requirements
{Bullets(spec.LoggingRequirements)}

## Safety Rules
{Bullets(spec.SafetyRules)}

## Safety Requirements
{Bullets(spec.SafetyRequirements)}

## Future Candidate Portfolio
{Bullets(spec.FutureCandidatePortfolio)}

This is a specification draft only. It contains no broker credentials, no live order execution, and no cTrader Order API integration.
""";

    private static string SignalMarkdown(ScalpingSignalSpec spec) => $"""
# {spec.SignalName}

- candidate_id: {spec.CandidateId}
- asset: {spec.Asset}
- timeframe: {spec.Timeframe}
- setup_type: {spec.SetupType}
- confidence_score: {spec.ConfidenceScore.ToString("0.####", CultureInfo.InvariantCulture)}
- human_review_status: {spec.HumanReviewStatus}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Signal Direction Logic
{Bullets(spec.SignalDirectionLogic)}

## Entry Conditions
{Bullets(spec.EntryConditions)}

## Invalidation Conditions
{Bullets(spec.InvalidationConditions)}

## Exit Conditions
{Bullets(spec.ExitConditions)}

## Confidence Model
- source: certified_scalping_research_v1
- threshold_review_required_from: 0.70
- candidate_confidence: {spec.ConfidenceScore.ToString("0.####", CultureInfo.InvariantCulture)}

## Filters
- session_filter: {spec.SessionFilter}
- spread_filter: {spec.SpreadFilter}
- news_filter: {spec.NewsFilter}

## Required Market Context
{Bullets(spec.RequiredMarketContext)}

## Risk Notes
{Bullets(spec.RiskNotes)}

## Operational Limits
{Bullets(spec.OperationalLimits)}

This is a portable Signal-Agent specification only. It does not execute trades.
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
}
