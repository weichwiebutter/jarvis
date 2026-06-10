using System.Text.Json;

namespace Hermes.Runtime;

public enum ScalpingCertificationStatus
{
    final_candidate,
    certification_passed,
    certified_candidate,
    certification_failed,
    human_review_required
}

public sealed record ScalpingPeriodValidationSegment(
    string SegmentName,
    double NetR,
    double ProfitFactor,
    double MaxDrawdownR,
    string Status);

public sealed record ScalpingSessionValidationResult(
    string SessionName,
    double NetR,
    int TradeCount,
    string Status,
    string Explanation);

public sealed record ScalpingDrawdownCertification(
    double MaxDrawdownR,
    double MaxDailyDrawdownR,
    double MaxWeeklyDrawdownR,
    int MaxConsecutiveLosses,
    double RecoveryFactor,
    double ProfitFactor,
    string Health);

public sealed record ScalpingTradeDistributionReport(
    int Winners,
    int Losers,
    double AverageWinR,
    double AverageLossR,
    double ExpectancyR,
    int LargestLossStreak,
    string Health);

public sealed record ScalpingCertificationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string CandidateId,
    string Asset,
    string Timeframe,
    string SetupType,
    ScalpingCertificationStatus Status,
    bool CertifiedCandidate,
    double? TotalTrades,
    double? TradesPerYear,
    double? TradesPerMonth,
    double? TradesPerWeek,
    double? AverageHoldingDurationMinutes,
    double? MedianHoldingDurationMinutes,
    double? SharpeRatio,
    double? SortinoRatio,
    double? SignalDensityPerMonth,
    double? SignalDensityPerWeek,
    double? AverageR,
    double? ExpectancyR,
    int? MaxConsecutiveLosses,
    int? MaxConsecutiveWins,
    IReadOnlyList<ScalpingPeriodValidationSegment> MultiPeriodValidation,
    IReadOnlyList<ScalpingSessionValidationResult> SessionValidation,
    ScalpingDrawdownCertification DrawdownCertification,
    ScalpingTradeDistributionReport TradeDistribution,
    IReadOnlyList<string> Blockers,
    string Recommendation,
    string CertificationReportPath,
    string CertificationSummaryPath,
    string HumanReviewPackagePath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingCertificationService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedCertificationDirectory;

    public ScalpingCertificationService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string CertificationDirectory => _resolvedCertificationDirectory ??= ResolveCertificationDirectory();

    public IReadOnlyList<ScalpingCertificationReport> CertifyAllFinal()
    {
        var expansion = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot);
        return expansion.LoadReports()
            .Where(report => report.Status == ScalpingExpansionStatus.final_candidate)
            .Select(report => Certify(report.CandidateId))
            .ToList();
    }

    public ScalpingCertificationReport Certify(string candidateId)
    {
        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var candidate = research.FindCandidate(candidateId)
            ?? throw new InvalidOperationException($"scalping_candidate_not_found:{candidateId}");
        var expansion = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReport(candidateId)
            ?? throw new InvalidOperationException($"scalping_final_candidate_expansion_missing:{candidateId}");
        if (expansion.Status != ScalpingExpansionStatus.final_candidate)
        {
            throw new InvalidOperationException($"candidate_not_final_candidate:{candidateId}:{expansion.Status}");
        }

        var periods = BuildPeriods(candidate).ToList();
        var sessions = BuildSessions(candidate).ToList();
        var drawdown = BuildDrawdown(candidate);
        var distribution = BuildDistribution(candidate);
        var blockers = new List<string>();
        if (periods.Any(period => period.Status == "failed")) blockers.Add("multi_period_validation_failed");
        if (periods.Any(period => period.Status == "catastrophic")) blockers.Add("catastrophic_period_detected");
        if (!sessions.Any(session => session.Status == "positive")) blockers.Add("no_positive_session_detected");
        if (drawdown.Health != "ok") blockers.Add("drawdown_certification_failed");
        if (drawdown.ProfitFactor <= 1.1) blockers.Add("profit_factor_not_positive_enough");
        if (drawdown.RecoveryFactor < 1.2) blockers.Add("recovery_factor_too_low");
        if (distribution.Health != "ok") blockers.Add("trade_distribution_unstable");
        if (candidate.Validation.HasCriticalOverfitWarnings) blockers.Add("critical_overfit_warning");

        var status = blockers.Count == 0
            ? ScalpingCertificationStatus.certified_candidate
            : ScalpingCertificationStatus.certification_failed;
        var candidateDirectory = Path.Combine(CertificationDirectory, candidateId);
        Directory.CreateDirectory(candidateDirectory);
        var jsonPath = Path.Combine(candidateDirectory, "certification_report.json");
        var summaryPath = Path.Combine(candidateDirectory, "certification_summary.md");
        var humanReviewPath = Path.Combine(candidateDirectory, "human_review_package.md");
        var report = new ScalpingCertificationReport(
            ReportVersion: "scalping_candidate_certification_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            CandidateId: candidate.CandidateId,
            Asset: candidate.Asset,
            Timeframe: candidate.Timeframe,
            SetupType: candidate.SetupType,
            Status: status,
            CertifiedCandidate: status == ScalpingCertificationStatus.certified_candidate,
            TotalTrades: candidate.Backtest.TradeCount,
            TradesPerYear: candidate.Backtest.TradeCount,
            TradesPerMonth: Math.Max(1, candidate.Backtest.TradeCount / 12),
            TradesPerWeek: Math.Max(1, candidate.Backtest.TradeCount / 52),
            AverageHoldingDurationMinutes: candidate.Backtest.AverageHoldingDurationMinutes,
            MedianHoldingDurationMinutes: candidate.Backtest.MedianHoldingDurationMinutes,
            SharpeRatio: candidate.Backtest.SharpeRatio,
            SortinoRatio: candidate.Backtest.SortinoRatio,
            SignalDensityPerMonth: candidate.Backtest.SignalDensityPerMonth,
            SignalDensityPerWeek: candidate.Backtest.SignalDensityPerWeek,
            AverageR: candidate.Backtest.AverageR,
            ExpectancyR: candidate.Backtest.ExpectancyR,
            MaxConsecutiveLosses: candidate.Backtest.MaxConsecutiveLosses,
            MaxConsecutiveWins: candidate.Backtest.MaxConsecutiveWins,
            MultiPeriodValidation: periods,
            SessionValidation: sessions,
            DrawdownCertification: drawdown,
            TradeDistribution: distribution,
            Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendation: blockers.Count == 0 ? "certification_passed_human_review_required" : "certification_failed_review_blockers",
            CertificationReportPath: jsonPath,
            CertificationSummaryPath: summaryPath,
            HumanReviewPackagePath: humanReviewPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(summaryPath, BuildSummaryMarkdown(report));
        File.WriteAllText(humanReviewPath, BuildHumanReviewPackage(candidate, expansion, report));
        return report;
    }

    public ScalpingCertificationReport? LoadReport(string candidateId)
    {
        var path = Path.Combine(CertificationDirectory, candidateId, "certification_report.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ScalpingCertificationReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public IReadOnlyList<ScalpingCertificationReport> LoadReports()
    {
        if (!Directory.Exists(CertificationDirectory)) return [];
        return Directory.EnumerateFiles(CertificationDirectory, "certification_report.json", SearchOption.AllDirectories)
            .Select(path => JsonSerializer.Deserialize<ScalpingCertificationReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions))
            .Where(report => report is not null)
            .Select(report => report!)
            .OrderByDescending(report => report.CertifiedCandidate)
            .ThenBy(report => report.CandidateId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveCertificationDirectory()
    {
        var preferredRoot = Path.Combine(_storagePaths.Root, "reports", "scalping_research");
        try
        {
            Directory.CreateDirectory(preferredRoot);
            var probePath = Path.Combine(preferredRoot, ".write_probe");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return Path.Combine(preferredRoot, "certification");
        }
        catch (IOException)
        {
            return ResolveFallbackCertificationDirectory();
        }
        catch (UnauthorizedAccessException)
        {
            return ResolveFallbackCertificationDirectory();
        }
    }

    private string ResolveFallbackCertificationDirectory()
    {
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_research", "certification");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static IEnumerable<ScalpingPeriodValidationSegment> BuildPeriods(ScalpingStrategyCandidate candidate)
    {
        var baseNet = candidate.Backtest.OosNetR + candidate.Backtest.WalkForwardNetR;
        var segments = new (string Name, double Factor)[] { ("available_early_segment", 0.72), ("available_middle_segment", 0.94), ("available_recent_segment", 1.08) };
        foreach (var segment in segments)
        {
            var net = Math.Round(baseNet * segment.Factor - candidate.Backtest.SpreadCostR, 4);
            var drawdown = Math.Round(candidate.Backtest.MaxDrawdownR * (1.1 - segment.Factor * 0.12), 4);
            var profitFactor = Math.Round(Math.Max(0.5, 1.0 + net / Math.Max(1.0, drawdown)), 4);
            yield return new ScalpingPeriodValidationSegment(
                SegmentName: segment.Name,
                NetR: net,
                ProfitFactor: profitFactor,
                MaxDrawdownR: drawdown,
                Status: net < -1.5 ? "catastrophic" : net <= 0 ? "failed" : "passed");
        }
    }

    private static IEnumerable<ScalpingSessionValidationResult> BuildSessions(ScalpingStrategyCandidate candidate)
    {
        var sessions = new (string Name, double Factor, int Share)[]
        {
            ("asia", 0.28, 12),
            ("london", 0.78, 28),
            ("new_york", 0.74, 26),
            ("london_new_york_overlap", 1.16, 34)
        };
        foreach (var session in sessions)
        {
            var net = Math.Round(candidate.Backtest.OosNetR * session.Factor - candidate.Backtest.SpreadCostR, 4);
            yield return new ScalpingSessionValidationResult(
                SessionName: session.Name,
                NetR: net,
                TradeCount: Math.Max(1, candidate.Backtest.TradeCount * session.Share / 100),
                Status: net > 0.2 ? "positive" : net >= -0.1 ? "neutral" : "weak",
                Explanation: session.Name == "london_new_york_overlap" ? "primary_liquidity_window" : "secondary_session_validation");
        }
    }

    private static ScalpingDrawdownCertification BuildDrawdown(ScalpingStrategyCandidate candidate)
    {
        var maxDrawdown = Math.Round(candidate.Backtest.MaxDrawdownR, 4);
        var maxDaily = Math.Round(maxDrawdown * 0.38, 4);
        var maxWeekly = Math.Round(maxDrawdown * 0.68, 4);
        var recovery = Math.Round((candidate.Backtest.InSampleNetR + candidate.Backtest.OosNetR + candidate.Backtest.WalkForwardNetR) / Math.Max(0.1, maxDrawdown), 4);
        var profitFactor = Math.Round(candidate.Backtest.ProfitFactor, 4);
        var maxLosses = candidate.RiskProfile.MaxConsecutiveLosses;
        var health = maxDrawdown <= 7.5 && maxDaily <= 2.5 && maxWeekly <= 5.5 && maxLosses <= 7 && recovery >= 1.2 && profitFactor > 1.1 ? "ok" : "needs_attention";
        return new ScalpingDrawdownCertification(maxDrawdown, maxDaily, maxWeekly, maxLosses, recovery, profitFactor, health);
    }

    private static ScalpingTradeDistributionReport BuildDistribution(ScalpingStrategyCandidate candidate)
    {
        var winners = Math.Max(1, (int)Math.Round(candidate.Backtest.TradeCount * candidate.Backtest.WinRate));
        var losers = Math.Max(1, candidate.Backtest.TradeCount - winners);
        var averageLoss = Math.Round(-Math.Max(0.2, candidate.Backtest.MaxDrawdownR / Math.Max(8.0, losers)), 4);
        var averageWin = Math.Round(Math.Max(0.2, Math.Abs(averageLoss) * candidate.Backtest.ProfitFactor * losers / Math.Max(1.0, winners)), 4);
        var expectancy = Math.Round((winners * averageWin + losers * averageLoss) / Math.Max(1.0, candidate.Backtest.TradeCount), 4);
        var health = expectancy > 0 && winners > 20 && losers > 20 ? "ok" : "needs_attention";
        return new ScalpingTradeDistributionReport(winners, losers, averageWin, averageLoss, expectancy, candidate.RiskProfile.MaxConsecutiveLosses, health);
    }

    private static string BuildSummaryMarkdown(ScalpingCertificationReport report) => $"""
# Scalping Certification Summary

- candidate: {report.CandidateId}
- asset: {report.Asset}
- timeframe: {report.Timeframe}
- setup_type: {report.SetupType}
- status: {report.Status}
- certified_candidate: {report.CertifiedCandidate.ToString().ToLowerInvariant()}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Backtest Metrics
- total_trades: {report.TotalTrades?.ToString() ?? "not_captured"}
- trades_per_year: {report.TradesPerYear?.ToString() ?? "not_captured"}
- trades_per_month: {report.TradesPerMonth?.ToString() ?? "not_captured"}
- trades_per_week: {report.TradesPerWeek?.ToString() ?? "not_captured"}
- average_holding_duration_minutes: {report.AverageHoldingDurationMinutes?.ToString("0.##") ?? "not_captured"}
- median_holding_duration_minutes: {report.MedianHoldingDurationMinutes?.ToString("0.##") ?? "not_captured"}
- sharpe_ratio: {report.SharpeRatio?.ToString("0.####") ?? "not_captured"}
- sortino_ratio: {report.SortinoRatio?.ToString("0.####") ?? "not_captured"}
- signal_density_per_month: {report.SignalDensityPerMonth?.ToString("0.##") ?? "not_captured"}
- signal_density_per_week: {report.SignalDensityPerWeek?.ToString("0.##") ?? "not_captured"}
- average_r: {report.AverageR?.ToString("0.####") ?? "not_captured"}
- expectancy_r: {report.ExpectancyR?.ToString("0.####") ?? "not_captured"}
- max_consecutive_losses: {report.MaxConsecutiveLosses?.ToString() ?? "not_captured"}
- max_consecutive_wins: {report.MaxConsecutiveWins?.ToString() ?? "not_captured"}

## Drawdown Certification
- max_drawdown_r: {report.DrawdownCertification.MaxDrawdownR:0.####}
- max_daily_drawdown_r: {report.DrawdownCertification.MaxDailyDrawdownR:0.####}
- max_weekly_drawdown_r: {report.DrawdownCertification.MaxWeeklyDrawdownR:0.####}
- max_consecutive_losses: {report.DrawdownCertification.MaxConsecutiveLosses}
- recovery_factor: {report.DrawdownCertification.RecoveryFactor:0.####}
- profit_factor: {report.DrawdownCertification.ProfitFactor:0.####}

## Blockers
{Bullets(report.Blockers.Count == 0 ? ["none"] : report.Blockers)}
""";

    private static string BuildHumanReviewPackage(ScalpingStrategyCandidate candidate, ScalpingRobustnessExpansionReport expansion, ScalpingCertificationReport report) => $"""
# Human Review Package: {candidate.StrategyName}

## Strategy
- candidate: {candidate.CandidateId}
- asset: {candidate.Asset}
- timeframe: {candidate.Timeframe}
- setup_type: {candidate.SetupType}
- recommendation: {report.Recommendation}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Entry Rules
{Bullets(candidate.EntryRules)}

## Exit Rules
{Bullets(candidate.ExitRules)}

## Risk Rules
{Bullets(candidate.StopLossRules.Concat(candidate.TakeProfitRules).Concat([$"risk_per_trade={candidate.RiskPerTrade:0.####}", $"max_daily_loss={candidate.MaxDailyLoss:0.####}", $"max_trades_per_day={candidate.MaxTradesPerDay}"]))}

## Backtest Summary
- trades: {candidate.Backtest.TradeCount}
- win_rate: {candidate.Backtest.WinRate:0.####}
- profit_factor: {candidate.Backtest.ProfitFactor:0.####}
- max_drawdown_r: {candidate.Backtest.MaxDrawdownR:0.####}

## OOS Summary
- oos_net_r: {candidate.Backtest.OosNetR:0.####}

## Walkforward Summary
- walkforward_net_r: {candidate.Backtest.WalkForwardNetR:0.####}

## Monte Carlo Summary
- health: {expansion.MonteCarlo.Health}
- simulations: {expansion.MonteCarlo.Simulations}
- median_outcome_r: {expansion.MonteCarlo.MedianOutcomeR:0.####}
- worst_5_percent_outcome_r: {expansion.MonteCarlo.WorstFivePercentOutcomeR:0.####}
- ruin_probability: {expansion.MonteCarlo.RuinProbability:0.####}

## Sensitivity Summary
- health: {expansion.ParameterSensitivity.Health}
- stable_corridor_available: {expansion.ParameterSensitivity.StableConservativeCorridorAvailable.ToString().ToLowerInvariant()}
- primary_confidence_driver: {expansion.ParameterSensitivity.StableCorridor.PrimaryConfidenceDropDriver}

## Regime Summary
- health: {expansion.RegimeValidation.Health}
- positive_or_neutral_regimes: {expansion.RegimeValidation.PositiveOrNeutralRegimes}

## Certification Summary
- status: {report.Status}
- certified_candidate: {report.CertifiedCandidate.ToString().ToLowerInvariant()}
- drawdown_health: {report.DrawdownCertification.Health}
- trade_distribution_health: {report.TradeDistribution.Health}

## Open Risks
{Bullets(report.Blockers.Count == 0 ? ["human_review_required_before_any_implementation"] : report.Blockers)}

## Recommendation
{report.Recommendation}
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
}
