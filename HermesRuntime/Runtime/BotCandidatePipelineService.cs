using System.Text.Json;

namespace Hermes.Runtime;

public sealed class BotCandidatePipelineService
{
    private const double MinimumWalkForwardConfidence = 0.62;
    private const double MinimumRealismScore = 0.68;
    private const double MaximumOverfitRisk = 0.38;
    private const double MaximumCostSensitivity = 0.52;
    private const double MinimumRegimeConsistency = 0.52;
    private const double MinimumMaxDrawdown = -8.0;
    private const double MinimumProfitFactor = 1.4;
    private const double MinimumSampleQuality = 0.55;

    private readonly StoragePaths _storagePaths;

    public BotCandidatePipelineService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string CandidateRoot => Path.Combine(_storagePaths.Root, "bot_candidates");

    public string BotCandidatesPath => Path.Combine(CandidateRoot, "bot_candidates.json");

    public string RejectedCandidatesPath => Path.Combine(CandidateRoot, "rejected_candidates.json");

    public string LatestReportPath => Path.Combine(CandidateRoot, "latest_bot_candidate_report.json");

    public BotCandidateReport Evaluate()
    {
        Directory.CreateDirectory(CandidateRoot);

        var walkForwardService = new WalkForwardValidationService(_storagePaths);
        var walkForward = walkForwardService.LoadReport() ?? walkForwardService.Run();
        var simulations = LoadSimulations();
        if (simulations.Count == 0)
        {
            simulations = new RealisticSimulationService(_storagePaths)
                .Run()
                .GroupBy(report => report.StrategyVariantId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(report => report.CreatedAtUtc).First(),
                    StringComparer.Ordinal);
        }

        var researchResults = LoadStrategyResults()
            .GroupBy(result => result.Variant.VariantId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.CompletedAtUtc).First(),
                StringComparer.Ordinal);

        var candidates = walkForward.Assessments
            .Select(assessment =>
            {
                simulations.TryGetValue(assessment.StrategyVariantId, out var simulation);
                researchResults.TryGetValue(assessment.StrategyVariantId, out var strategy);
                return BuildCandidate(assessment, simulation, strategy);
            })
            .OrderBy(candidate => candidate.Status == BotCandidateStatus.demo_bot_candidate ? 0 : 1)
            .ThenBy(candidate => candidate.Status == BotCandidateStatus.robust ? 0 : 1)
            .ThenByDescending(candidate => candidate.Criteria.WalkForwardConfidence)
            .ThenByDescending(candidate => candidate.Criteria.RealismScore)
            .ToList();

        var botCandidates = candidates
            .Where(candidate => candidate.Status != BotCandidateStatus.rejected)
            .ToList();
        var rejected = candidates
            .Where(candidate => candidate.Status == BotCandidateStatus.rejected)
            .ToList();
        var rejectionReasonCounts = rejected
            .SelectMany(candidate => candidate.RejectionReasons)
            .GroupBy(reason => reason, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        File.WriteAllText(BotCandidatesPath, JsonSerializer.Serialize(botCandidates, JsonDefaults.WriteOptions));
        File.WriteAllText(RejectedCandidatesPath, JsonSerializer.Serialize(rejected, JsonDefaults.WriteOptions));

        var report = new BotCandidateReport(
            ReportId: $"bot_candidate_report_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategiesEvaluated: candidates.Count,
            BotCandidateCount: botCandidates.Count,
            DemoBotCandidateCount: candidates.Count(candidate => candidate.Status == BotCandidateStatus.demo_bot_candidate),
            PromisingCandidateCount: candidates.Count(candidate => candidate.Status == BotCandidateStatus.promising),
            RobustCandidateCount: candidates.Count(candidate => candidate.Status == BotCandidateStatus.robust),
            RejectedCandidateCount: rejected.Count,
            Candidates: botCandidates.Take(200).ToList(),
            RejectedCandidates: rejected.Take(200).ToList(),
            TopDemoBotCandidates: candidates
                .Where(candidate => candidate.Status == BotCandidateStatus.demo_bot_candidate)
                .Take(25)
                .Select(candidate => $"{candidate.StrategyFamily}/{candidate.PatternId ?? "-"}:{candidate.StrategyId}:wf={candidate.Criteria.WalkForwardConfidence:0.####},realism={candidate.Criteria.RealismScore:0.####}")
                .ToList(),
            NextValidationRecommendations: BuildRecommendations(candidates, rejectionReasonCounts),
            RejectionReasonCounts: rejectionReasonCounts,
            BotCandidatesPath: BotCandidatesPath,
            RejectedCandidatesPath: RejectedCandidatesPath,
            NoBotCreated: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(LatestReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public BotCandidateReport? LoadReport()
    {
        if (!File.Exists(LatestReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotCandidateReport>(
                File.ReadAllText(LatestReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private BotCandidate BuildCandidate(
        WalkForwardStrategyAssessment assessment,
        StrategySimulationReport? simulation,
        StrategyResearchResult? strategy)
    {
        var metrics = simulation?.Metrics;
        var profitFactor = metrics?.ProfitFactor ?? 0;
        var maxDrawdown = metrics?.MaxDrawdown ?? strategy?.MaxDrawdown ?? 0;
        var criteria = new BotCandidateCriteria(
            Confidence: assessment.StrategyConfidence,
            ConfidenceRobust: assessment.StrategyConfidence == "robust",
            OosAvailable: assessment.OosAvailable,
            WalkForwardConfidence: assessment.WalkForwardConfidence,
            WalkForwardConfidencePassed: assessment.WalkForwardConfidence >= MinimumWalkForwardConfidence,
            RealismScore: assessment.RealismScore,
            RealismScorePassed: assessment.RealismScore >= MinimumRealismScore,
            OverfitRisk: assessment.OverfitRisk,
            OverfitRiskPassed: assessment.OverfitRisk <= MaximumOverfitRisk,
            CostSensitivity: assessment.CostSensitivity,
            CostSensitivityPassed: assessment.CostSensitivity <= MaximumCostSensitivity,
            RegimeConsistencyScore: assessment.RegimeConsistencyScore,
            RegimeConsistencyPassed: assessment.RegimeConsistencyScore >= MinimumRegimeConsistency,
            MaxDrawdown: maxDrawdown,
            MaxDrawdownPassed: maxDrawdown >= MinimumMaxDrawdown && maxDrawdown <= 0,
            ProfitFactor: profitFactor,
            ProfitFactorPassed: profitFactor >= MinimumProfitFactor,
            SampleQuality: assessment.SampleQuality,
            SampleQualityPassed: assessment.SampleQuality >= MinimumSampleQuality,
            TooGoodToBeTrue: assessment.TooGoodToBeTrue,
            TooGoodToBeTruePassed: !assessment.TooGoodToBeTrue);
        var rejectionReasons = BuildRejectionReasons(criteria, assessment, simulation).ToList();
        var status = ClassifyStatus(assessment, criteria, rejectionReasons);

        return new BotCandidate(
            CandidateId: $"candidate_{assessment.StrategyVariantId}",
            StrategyId: assessment.StrategyVariantId,
            StrategyFamily: assessment.StrategyFamily,
            PatternId: assessment.PatternId,
            Symbol: strategy?.SymbolsProcessed.FirstOrDefault() ?? simulation?.SampleTrades.FirstOrDefault()?.Symbol ?? "-",
            Timeframe: strategy?.TimeframesProcessed.FirstOrDefault() ?? simulation?.SampleTrades.FirstOrDefault()?.Timeframe ?? "-",
            Status: status,
            Criteria: criteria,
            RejectionReasons: rejectionReasons,
            NextValidationRecommendation: NextValidationRecommendation(status, rejectionReasons),
            OverfitFlags: assessment.OverfitFlags,
            NoBotCreated: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static BotCandidateStatus ClassifyStatus(
        WalkForwardStrategyAssessment assessment,
        BotCandidateCriteria criteria,
        IReadOnlyList<string> rejectionReasons)
    {
        if (criteria.Passed)
        {
            return BotCandidateStatus.demo_bot_candidate;
        }

        if (assessment.StrategyConfidence is "overfit_suspected" or "rejected" or "unstable"
            || assessment.TooGoodToBeTrue
            || assessment.HighRisk
            || rejectionReasons.Contains("too_good_to_be_true", StringComparer.Ordinal)
            || rejectionReasons.Contains("missing_out_of_sample", StringComparer.Ordinal))
        {
            return BotCandidateStatus.rejected;
        }

        return assessment.StrategyConfidence switch
        {
            "robust" => BotCandidateStatus.robust,
            "promising" => BotCandidateStatus.promising,
            _ => BotCandidateStatus.research_candidate
        };
    }

    private static IEnumerable<string> BuildRejectionReasons(
        BotCandidateCriteria criteria,
        WalkForwardStrategyAssessment assessment,
        StrategySimulationReport? simulation)
    {
        if (!criteria.ConfidenceRobust)
        {
            yield return $"confidence_not_robust:{criteria.Confidence}";
        }

        if (!criteria.OosAvailable)
        {
            yield return "missing_out_of_sample";
        }

        if (!criteria.WalkForwardConfidencePassed)
        {
            yield return "walkforward_confidence_too_low";
        }

        if (!criteria.RealismScorePassed)
        {
            yield return "realism_score_too_low";
        }

        if (!criteria.OverfitRiskPassed)
        {
            yield return "overfit_risk_too_high";
        }

        if (!criteria.CostSensitivityPassed)
        {
            yield return "cost_sensitivity_too_high";
        }

        if (!criteria.RegimeConsistencyPassed)
        {
            yield return "regime_consistency_too_low";
        }

        if (!criteria.MaxDrawdownPassed)
        {
            yield return "max_drawdown_unacceptable";
        }

        if (!criteria.ProfitFactorPassed)
        {
            yield return "profit_factor_too_low";
        }

        if (!criteria.SampleQualityPassed)
        {
            yield return "sample_quality_too_low";
        }

        if (!criteria.TooGoodToBeTruePassed)
        {
            yield return "too_good_to_be_true";
        }

        if (simulation is null)
        {
            yield return "simulation_report_missing";
        }

        foreach (var flag in assessment.OverfitFlags.Where(flag => !string.IsNullOrWhiteSpace(flag)).Take(8))
        {
            yield return $"overfit_flag:{flag}";
        }
    }

    private static string NextValidationRecommendation(BotCandidateStatus status, IReadOnlyList<string> rejectionReasons)
    {
        if (status == BotCandidateStatus.demo_bot_candidate)
        {
            return "manual_review_then_prepare_demo_validation_plan";
        }

        if (rejectionReasons.Any(reason => reason.StartsWith("missing_out_of_sample", StringComparison.Ordinal)))
        {
            return "collect_oos_data_and_rerun_walkforward_before_demo_validation";
        }

        if (rejectionReasons.Any(reason => reason.Contains("too_good_to_be_true", StringComparison.Ordinal)
            || reason.Contains("overfit", StringComparison.Ordinal)))
        {
            return "retest_with_stricter_realism_and_oos_windows";
        }

        if (rejectionReasons.Any(reason => reason.Contains("profit_factor", StringComparison.Ordinal)
            || reason.Contains("sample_quality", StringComparison.Ordinal)))
        {
            return "continue_research_until_sample_and_net_performance_improve";
        }

        return "keep_as_research_candidate_only";
    }

    private static IReadOnlyList<string> BuildRecommendations(
        IReadOnlyList<BotCandidate> candidates,
        IReadOnlyDictionary<string, int> rejectionReasonCounts)
    {
        var recommendations = new List<string>();
        var demoCount = candidates.Count(candidate => candidate.Status == BotCandidateStatus.demo_bot_candidate);
        recommendations.Add(demoCount > 0
            ? "manual_review_required_before_demo_validation"
            : "no_demo_bot_candidate_ready");

        recommendations.AddRange(rejectionReasonCounts
            .Take(5)
            .Select(item => $"fix_or_collect:{item.Key}:count={item.Value}"));

        recommendations.Add("no_bot_created_no_trades_no_broker_action");
        return recommendations;
    }

    private Dictionary<string, StrategySimulationReport> LoadSimulations()
    {
        return new RealisticSimulationService(_storagePaths)
            .LoadReports()
            .GroupBy(report => report.StrategyVariantId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(report => report.CreatedAtUtc).First(),
                StringComparer.Ordinal);
    }

    private IEnumerable<StrategyResearchResult> LoadStrategyResults()
    {
        var directory = Path.Combine(_storagePaths.Root, "strategy_research", "results");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.strategy_result.json", SearchOption.TopDirectoryOnly))
        {
            StrategyResearchResult? result;
            try
            {
                result = JsonSerializer.Deserialize<StrategyResearchResult>(
                    File.ReadAllText(path),
                    JsonDefaults.SnapshotReadOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            if (result is not null)
            {
                yield return result;
            }
        }
    }
}
