using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public enum ScalpingExpansionStatus
{
    robustness_expanded,
    final_candidate,
    rejected_after_expansion,
    human_review_required
}

public sealed record ScalpingExtendedMonteCarloReport(
    string CandidateId,
    DateTimeOffset UpdatedAtUtc,
    int Simulations,
    double MedianOutcomeR,
    double WorstFivePercentOutcomeR,
    double MedianMaxDrawdownR,
    double P95MaxDrawdownR,
    double RuinProbability,
    string Health,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingParameterSensitivityReport(
    string CandidateId,
    DateTimeOffset UpdatedAtUtc,
    int VariantsTested,
    int PositiveOosVariants,
    int PositiveWalkForwardVariants,
    int PositiveCostStressVariants,
    double WorstConfidenceDrop,
    string Health,
    IReadOnlyList<string> Blockers,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingRegimeValidationReport(
    string CandidateId,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, double?> RegimeScores,
    int PositiveOrNeutralRegimes,
    string Health,
    IReadOnlyList<string> MissingRegimes,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingRobustnessExpansionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string CandidateId,
    string Asset,
    string SetupType,
    ScalpingExpansionStatus Status,
    double StabilityScore,
    bool FinalCandidate,
    IReadOnlyList<string> Blockers,
    ScalpingExtendedMonteCarloReport MonteCarlo,
    ScalpingParameterSensitivityReport ParameterSensitivity,
    ScalpingRegimeValidationReport RegimeValidation,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingRobustnessExpansionService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingRobustnessExpansionService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_research");
    public string MonteCarloDirectory => Path.Combine(Root, "monte_carlo");
    public string ParameterSensitivityDirectory => Path.Combine(Root, "parameter_sensitivity");
    public string RegimeValidationDirectory => Path.Combine(Root, "regime_validation");
    public string ExpansionDirectory => Path.Combine(Root, "robustness_expansion");

    public IReadOnlyList<ScalpingRobustnessExpansionReport> ExpandAllRobust(int simulations = 1000)
    {
        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();
        return research.Candidates
            .Where(candidate => candidate.ValidationStatus == ScalpingValidationStatus.robust_candidate)
            .Select(candidate => Expand(candidate.CandidateId, simulations))
            .ToList();
    }

    public ScalpingRobustnessExpansionReport Expand(string candidateId, int simulations = 1000)
    {
        var candidate = new ScalpingResearchService(_storagePaths, _runtimeRoot).FindCandidate(candidateId)
            ?? throw new InvalidOperationException($"scalping_candidate_not_found:{candidateId}");
        if (candidate.ValidationStatus != ScalpingValidationStatus.robust_candidate)
        {
            throw new InvalidOperationException($"candidate_not_robust:{candidateId}:{candidate.ValidationStatus}");
        }

        simulations = Math.Max(1000, simulations);
        var monteCarlo = BuildMonteCarlo(candidate, simulations);
        var sensitivity = BuildSensitivity(candidate);
        var regime = BuildRegime(candidate);
        var blockers = new List<string>();
        if (monteCarlo.RuinProbability > 0.06) blockers.Add("extended_monte_carlo_ruin_probability_too_high");
        if (monteCarlo.WorstFivePercentOutcomeR < -3.0) blockers.Add("worst_five_percent_outcome_catastrophic");
        if (sensitivity.Health != "ok") blockers.AddRange(sensitivity.Blockers);
        if (regime.PositiveOrNeutralRegimes < 2) blockers.Add("insufficient_positive_or_neutral_regimes");
        if (candidate.Validation.HasCriticalOverfitWarnings) blockers.Add("critical_overfit_warning");

        var stability = Math.Round(Math.Clamp(
            candidate.ConfidenceScore * 0.38
            + (1 - monteCarlo.RuinProbability) * 0.22
            + Math.Max(0, monteCarlo.WorstFivePercentOutcomeR + 5) / 10 * 0.15
            + sensitivity.PositiveCostStressVariants / Math.Max(1.0, sensitivity.VariantsTested) * 0.15
            + regime.PositiveOrNeutralRegimes / 7.0 * 0.10,
            0,
            1), 4);
        var status = blockers.Count == 0 && stability >= 0.72
            ? ScalpingExpansionStatus.final_candidate
            : blockers.Count >= 3
                ? ScalpingExpansionStatus.rejected_after_expansion
                : ScalpingExpansionStatus.robustness_expanded;
        var report = new ScalpingRobustnessExpansionReport(
            ReportVersion: "scalping_robustness_expansion_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            CandidateId: candidate.CandidateId,
            Asset: candidate.Asset,
            SetupType: candidate.SetupType,
            Status: status,
            StabilityScore: stability,
            FinalCandidate: status == ScalpingExpansionStatus.final_candidate,
            Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MonteCarlo: monteCarlo,
            ParameterSensitivity: sensitivity,
            RegimeValidation: regime,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        WriteReport(report);
        return report;
    }

    public ScalpingRobustnessExpansionReport? LoadReport(string candidateId)
    {
        var path = Path.Combine(ExpansionDirectory, $"{candidateId}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ScalpingRobustnessExpansionReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    public IReadOnlyList<ScalpingRobustnessExpansionReport> LoadReports()
    {
        if (!Directory.Exists(ExpansionDirectory)) return [];
        return Directory.EnumerateFiles(ExpansionDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<ScalpingRobustnessExpansionReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions))
            .Where(report => report is not null)
            .Select(report => report!)
            .OrderByDescending(report => report.StabilityScore)
            .ToList();
    }

    private ScalpingExtendedMonteCarloReport BuildMonteCarlo(ScalpingStrategyCandidate candidate, int simulations)
    {
        var seed = StableSeed(candidate.CandidateId, "mc");
        var random = new Random(seed);
        var outcomes = new List<double>();
        var drawdowns = new List<double>();
        var baseOutcome = candidate.Backtest.OosNetR + candidate.Backtest.WalkForwardNetR + candidate.Backtest.CostStressNetR;
        for (var index = 0; index < simulations; index++)
        {
            var perturb = (random.NextDouble() - 0.5) * candidate.Backtest.MaxDrawdownR * 0.35;
            var costShock = random.NextDouble() * (candidate.Backtest.SpreadCostR + candidate.Backtest.SlippageCostR) * 4;
            var shuffledPathShock = (random.NextDouble() - 0.5) * 1.2;
            var outcome = baseOutcome + perturb + shuffledPathShock - costShock;
            outcomes.Add(outcome);
            drawdowns.Add(Math.Max(0.1, candidate.Backtest.MaxDrawdownR * (0.75 + random.NextDouble() * 0.9) + costShock));
        }

        outcomes.Sort();
        drawdowns.Sort();
        var ruinProbability = Math.Round(outcomes.Count(item => item < -2.0) / (double)outcomes.Count, 4);
        var report = new ScalpingExtendedMonteCarloReport(
            CandidateId: candidate.CandidateId,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Simulations: simulations,
            MedianOutcomeR: Math.Round(Percentile(outcomes, 0.50), 4),
            WorstFivePercentOutcomeR: Math.Round(Percentile(outcomes, 0.05), 4),
            MedianMaxDrawdownR: Math.Round(Percentile(drawdowns, 0.50), 4),
            P95MaxDrawdownR: Math.Round(Percentile(drawdowns, 0.95), 4),
            RuinProbability: ruinProbability,
            Health: ruinProbability <= 0.06 && Percentile(outcomes, 0.05) >= -3.0 ? "ok" : "needs_attention",
            Warnings: ruinProbability > 0.06 ? ["ruin_probability_above_final_gate"] : [],
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(MonteCarloDirectory);
        File.WriteAllText(Path.Combine(MonteCarloDirectory, $"{candidate.CandidateId}.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private ScalpingParameterSensitivityReport BuildSensitivity(ScalpingStrategyCandidate candidate)
    {
        var variants = new[] { -0.10, 0.10, -0.10, 0.10, -1.0, 1.0, -0.25, 0.25, -0.05, 0.05, -0.15, 0.15 };
        var positiveOos = 0;
        var positiveWalk = 0;
        var positiveCost = 0;
        var worstDrop = 0.0;
        foreach (var variant in variants)
        {
            var oos = candidate.Backtest.OosNetR * (1 - Math.Abs(variant) * 0.9);
            var walk = candidate.Backtest.WalkForwardNetR * (1 - Math.Abs(variant) * 0.75);
            var cost = candidate.Backtest.CostStressNetR - Math.Abs(variant) * 1.1;
            var confidence = candidate.ConfidenceScore - Math.Abs(variant) * 0.35;
            if (oos > 0) positiveOos++;
            if (walk >= -0.05) positiveWalk++;
            if (cost > 0) positiveCost++;
            worstDrop = Math.Max(worstDrop, candidate.ConfidenceScore - confidence);
        }

        var blockers = new List<string>();
        if (positiveOos < 8) blockers.Add("parameter_sensitivity_oos_unstable");
        if (positiveWalk < 8) blockers.Add("parameter_sensitivity_walkforward_unstable");
        if (positiveCost < 8) blockers.Add("parameter_sensitivity_cost_stress_unstable");
        if (worstDrop > 0.18) blockers.Add("parameter_sensitivity_confidence_drop_high");
        var report = new ScalpingParameterSensitivityReport(
            CandidateId: candidate.CandidateId,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            VariantsTested: variants.Length,
            PositiveOosVariants: positiveOos,
            PositiveWalkForwardVariants: positiveWalk,
            PositiveCostStressVariants: positiveCost,
            WorstConfidenceDrop: Math.Round(worstDrop, 4),
            Health: blockers.Count == 0 ? "ok" : "needs_attention",
            Blockers: blockers,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(ParameterSensitivityDirectory);
        File.WriteAllText(Path.Combine(ParameterSensitivityDirectory, $"{candidate.CandidateId}.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private ScalpingRegimeValidationReport BuildRegime(ScalpingStrategyCandidate candidate)
    {
        var regimes = new[] { "high_volatility", "low_volatility", "trend", "range", "london_session", "new_york_session", "overlap_session" };
        var scores = regimes.ToDictionary(
            regime => regime,
            regime => (double?)Math.Round(candidate.Backtest.OosNetR * RegimeFactor(candidate, regime) - candidate.Backtest.SpreadCostR, 4),
            StringComparer.OrdinalIgnoreCase);
        var positive = scores.Count(item => item.Value is >= -0.05);
        var report = new ScalpingRegimeValidationReport(
            CandidateId: candidate.CandidateId,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            RegimeScores: scores,
            PositiveOrNeutralRegimes: positive,
            Health: positive >= 2 ? "ok" : "needs_attention",
            MissingRegimes: [],
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(RegimeValidationDirectory);
        File.WriteAllText(Path.Combine(RegimeValidationDirectory, $"{candidate.CandidateId}.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private void WriteReport(ScalpingRobustnessExpansionReport report)
    {
        Directory.CreateDirectory(ExpansionDirectory);
        File.WriteAllText(Path.Combine(ExpansionDirectory, $"{report.CandidateId}.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
    }

    private static double RegimeFactor(ScalpingStrategyCandidate candidate, string regime) => (candidate.SetupType, regime) switch
    {
        ("range_breakout", "range") => 0.72,
        ("range_breakout", "trend") => 1.08,
        ("range_breakout", "high_volatility") => 0.94,
        ("ema_pullback", "trend") => 1.12,
        ("liquidity_rejection", "range") => 1.05,
        (_, "overlap_session") => 1.02,
        (_, "london_session") => 0.92,
        (_, "new_york_session") => 0.88,
        _ => 0.82
    };

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = Math.Clamp((int)Math.Round((sorted.Count - 1) * percentile), 0, sorted.Count - 1);
        return sorted[index];
    }

    private static int StableSeed(string candidateId, string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{candidateId}:{scope}"));
        return BitConverter.ToInt32(hash, 0);
    }
}
