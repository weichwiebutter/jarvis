using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ResearchInsightsGenerator
{
    private const string SummaryVersion = "strategy_evolution_summary_v1";

    private readonly StoragePaths _storagePaths;

    public ResearchInsightsGenerator(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StrategyResearchRoot => Path.Combine(_storagePaths.Root, "strategy_research");

    public string InsightsPath => Path.Combine(StrategyResearchRoot, "research_insights.json");

    public string ClustersPath => Path.Combine(StrategyResearchRoot, "strategy_clusters.json");

    public StrategyEvolutionSummary Generate()
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        var patterns = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        var results = LoadResults().ToList();
        var completed = results
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var walkForward = new WalkForwardValidationService(_storagePaths).LoadReport();
        var costReport = new RealisticSimulationService(_storagePaths).LoadCostSensitivityReport();
        var botCandidateService = new BotCandidatePipelineService(_storagePaths);
        var botCandidateReport = botCandidateService.LoadReport() ?? botCandidateService.Evaluate();
        var acceptableVariantIds = walkForward?.Assessments
            .Where(item => item.StrategyConfidence is not "overfit_suspected" and not "rejected" and not "unstable")
            .Select(item => item.StrategyVariantId)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
        var top = completed
            .Where(result => acceptableVariantIds.Count == 0 || acceptableVariantIds.Contains(result.Variant.VariantId))
            .OrderByDescending(result => result.Fitness.Score)
            .ThenByDescending(result => result.TradeCount)
            .Take(12)
            .ToList();
        var weak = completed
            .OrderBy(result => result.Fitness.Score)
            .ThenBy(result => result.TradeCount)
            .Take(12)
            .ToList();
        var clusters = BuildClusters(completed);
        var regimeAnalysis = new MarketRegimeClassifier(_storagePaths).Run();

        var summary = new StrategyEvolutionSummary(
            SummaryVersion: SummaryVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            TopStrategies: top,
            WeakStrategies: weak,
            BestSymbols: BestValues(completed.SelectMany(result => result.SymbolsProcessed)),
            BestTimeframes: BestValues(completed.SelectMany(result => result.TimeframesProcessed)),
            StabilityMetrics: BuildStabilityMetrics(completed),
            FitnessTrends: BuildFitnessTrends(completed),
            ExplorationCoverage: BuildExplorationCoverage(completed),
            StrategyRankings: BuildStrategyRankings(completed),
            ParameterStatistics: BuildParameterStatistics(completed),
            TimeframeComparisons: BuildTimeframeComparisons(completed),
            Clusters: clusters,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BestPatterns: BuildPatternPerformance(completed, patterns, descending: true, limit: 8),
            WeakPatterns: BuildPatternPerformance(completed, patterns, descending: false, limit: 8),
            AvoidCombinations: BuildAvoidCombinations(completed, patterns),
            NextRecommendedTests: BuildNextRecommendedTests(completed, patterns),
            SourcePerformance: BuildSourcePerformance(completed, patterns),
            BestTradingDePatterns: BuildTradingDePatternPerformance(completed, patterns, limit: 8),
            RobustStrategies: BuildRobustStrategies(walkForward, completed),
            OverfitSuspectedStrategies: BuildOverfitStrategies(walkForward, completed),
            HighRiskStrategies: BuildHighRiskStrategies(walkForward),
            StableSymbolTimeframeCombinations: BuildStableSymbolTimeframeCombinations(completed),
            BestRegimes: regimeAnalysis.StrategyPerformance.StrongRegimeMatches,
            WeakRegimes: regimeAnalysis.StrategyPerformance.WeakRegimeMatches,
            PreferredSessions: regimeAnalysis.StrategyPerformance.PreferredSessions,
            AvoidSessions: regimeAnalysis.StrategyPerformance.AvoidSessions,
            VolatilityPreference: regimeAnalysis.StrategyPerformance.VolatilityPreference,
            RegimeConsistencyScore: regimeAnalysis.StrategyPerformance.RegimeConsistencyScore,
            PreferredRegimes: regimeAnalysis.StrategyPerformance.PreferredRegimes,
            AvoidedRegimes: regimeAnalysis.StrategyPerformance.AvoidedRegimes,
            TooGoodToBeTrueStrategies: BuildTooGoodToBeTrueStrategies(walkForward),
            CostSensitiveStrategies: BuildCostSensitiveStrategies(costReport),
            CostSensitivitySummary: BuildCostSensitivitySummary(costReport),
            RobustGateSummary: BuildRobustGateSummary(walkForward, costReport, regimeAnalysis.StrategyPerformance),
            BotCandidateCount: botCandidateReport.BotCandidateCount,
            RejectedCandidateCount: botCandidateReport.RejectedCandidateCount,
            TopDemoBotCandidates: botCandidateReport.TopDemoBotCandidates,
            NextValidationRecommendations: botCandidateReport.NextValidationRecommendations);

        File.WriteAllText(InsightsPath, JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions));
        File.WriteAllText(ClustersPath, JsonSerializer.Serialize(clusters, JsonDefaults.WriteOptions));
        return summary;
    }

    public StrategyEvolutionSummary? LoadInsights()
    {
        if (!File.Exists(InsightsPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyEvolutionSummary>(
                File.ReadAllText(InsightsPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<StrategyCluster> LoadClusters()
    {
        if (!File.Exists(ClustersPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<StrategyCluster>>(
                File.ReadAllText(ClustersPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    public IReadOnlyList<string> LoadPatternPerformance()
    {
        var patterns = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        var completed = LoadResults()
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return BuildPatternPerformance(completed, patterns, descending: true, limit: 50);
    }

    public IReadOnlyList<string> LoadSourcePerformance()
    {
        var patterns = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog();
        var completed = LoadResults()
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return BuildSourcePerformance(completed, patterns);
    }

    private IEnumerable<StrategyResearchResult> LoadResults()
    {
        var directory = Path.Combine(StrategyResearchRoot, "results");
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

    private static IReadOnlyList<StrategyCluster> BuildClusters(IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .GroupBy(result => ClusterFamily(result))
            .Select(group =>
            {
                var ordered = group.OrderByDescending(result => result.Fitness.Score).ToList();
                var averageFitness = ordered.Count == 0 ? 0 : ordered.Average(result => result.Fitness.Score);
                var commonParameters = ordered
                    .Take(5)
                    .Select(result => $"ema={result.Variant.FastEma}/{result.Variant.SlowEma},rr={result.Variant.RiskRewardRatio:0.##},sl={result.Variant.StopLossAtrMultiplier:0.##}")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                return new StrategyCluster(
                    ClusterId: $"cluster_{group.Key}",
                    Family: group.Key,
                    VariantCount: ordered.Count,
                    AverageFitness: Math.Round(averageFitness, 4),
                    BestFitness: Math.Round(ordered.FirstOrDefault()?.Fitness.Score ?? 0, 4),
                    AverageWinrate: Math.Round(ordered.Count == 0 ? 0 : ordered.Average(result => result.Fitness.Winrate), 4),
                    AverageTradeCount: Math.Round(ordered.Count == 0 ? 0 : ordered.Average(result => result.TradeCount), 2),
                    CommonParameters: commonParameters,
                    Prioritized: averageFitness >= 0.7,
                    Reduced: averageFitness < 0.45);
            })
            .OrderByDescending(cluster => cluster.BestFitness)
            .ToList();
    }

    private static string ClusterFamily(StrategyResearchResult result)
    {
        var patternId = result.Variant.PatternId ?? string.Empty;
        if (patternId.Contains("breakout", StringComparison.OrdinalIgnoreCase)
            || patternId.Contains("range", StringComparison.OrdinalIgnoreCase)
            || result.Variant.Family.Contains("breakout", StringComparison.OrdinalIgnoreCase))
        {
            return "breakout_family";
        }

        if (patternId.Contains("engulfing", StringComparison.OrdinalIgnoreCase))
        {
            return "engulfing_family";
        }

        if (patternId.Contains("pullback", StringComparison.OrdinalIgnoreCase)
            || result.Variant.Family.Contains("pullback", StringComparison.OrdinalIgnoreCase))
        {
            return "pullback_family";
        }

        if (patternId.Contains("trend", StringComparison.OrdinalIgnoreCase)
            || result.Variant.Family.Contains("trend", StringComparison.OrdinalIgnoreCase))
        {
            return "trend_family";
        }

        return result.Variant.Family;
    }

    private static IReadOnlyList<string> BestValues(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildStabilityMetrics(IReadOnlyList<StrategyResearchResult> results)
    {
        if (results.Count == 0)
        {
            return ["no_results"];
        }

        var average = results.Average(result => result.Fitness.Score);
        var variance = results.Average(result => Math.Pow(result.Fitness.Score - average, 2));
        return
        [
            $"avg_fitness:{average:0.####}",
            $"fitness_stddev:{Math.Sqrt(variance):0.####}",
            $"avg_drawdown_penalty:{results.Average(result => result.Fitness.DrawdownPenalty):0.####}",
            $"avg_stability_bonus:{results.Average(result => result.Fitness.StabilityBonus):0.####}"
        ];
    }

    private static IReadOnlyList<string> BuildFitnessTrends(IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .OrderBy(result => result.CompletedAtUtc)
            .Chunk(32)
            .Select((chunk, index) => $"batch_{index + 1}:avg_fitness={chunk.Average(result => result.Fitness.Score):0.####},count={chunk.Length}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildExplorationCoverage(IReadOnlyList<StrategyResearchResult> results)
    {
        return
        [
            $"variants:{results.Count}",
            $"families:{results.Select(result => result.Variant.Family).Distinct(StringComparer.OrdinalIgnoreCase).Count()}",
            $"patterns:{results.Select(result => result.Variant.PatternId ?? "no_pattern").Distinct(StringComparer.OrdinalIgnoreCase).Count()}",
            $"fast_ema_values:{string.Join("/", results.Select(result => result.Variant.FastEma).Distinct().OrderBy(value => value))}",
            $"slow_ema_values:{string.Join("/", results.Select(result => result.Variant.SlowEma).Distinct().OrderBy(value => value))}",
            $"rr_values:{string.Join("/", results.Select(result => result.Variant.RiskRewardRatio).Distinct().OrderBy(value => value).Select(value => value.ToString("0.##")))}"
        ];
    }

    private static IReadOnlyList<string> BuildStrategyRankings(IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .GroupBy(result => result.Variant.Family)
            .Select(group => $"{group.Key}:best={group.Max(result => result.Fitness.Score):0.####},avg={group.Average(result => result.Fitness.Score):0.####},count={group.Count()}")
            .OrderByDescending(line => line)
            .ToList();
    }

    private static IReadOnlyList<string> BuildParameterStatistics(IReadOnlyList<StrategyResearchResult> results)
    {
        return
        [
            $"best_rr:{BestParameter(results, result => result.Variant.RiskRewardRatio.ToString("0.##"))}",
            $"weak_rr:{WeakParameter(results, result => result.Variant.RiskRewardRatio.ToString("0.##"))}",
            $"best_sl:{BestParameter(results, result => result.Variant.StopLossAtrMultiplier.ToString("0.##"))}",
            $"weak_sl:{WeakParameter(results, result => result.Variant.StopLossAtrMultiplier.ToString("0.##"))}",
            $"best_confirmation:{BestParameter(results, result => result.Variant.RequireConfirmationCandle.ToString().ToLowerInvariant())}",
            $"best_volatility_filter:{BestParameter(results, result => result.Variant.UseVolatilityFilter.ToString().ToLowerInvariant())}",
            $"best_session:{BestParameter(results, result => result.Variant.SessionFilter ?? "any")}",
            $"weak_session:{WeakParameter(results, result => result.Variant.SessionFilter ?? "any")}",
            $"best_variant_timeframe:{BestParameter(results, result => result.Variant.Timeframe ?? "any")}",
            $"weak_variant_timeframe:{WeakParameter(results, result => result.Variant.Timeframe ?? "any")}"
        ];
    }

    private static IReadOnlyList<string> BuildTimeframeComparisons(IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .SelectMany(result => result.TimeframesProcessed.Select(timeframe => new { timeframe, result.Fitness.Score }))
            .GroupBy(item => item.timeframe)
            .Select(group => $"{group.Key}:avg_fitness={group.Average(item => item.Score):0.####},count={group.Count()}")
            .OrderBy(line => line)
            .ToList();
    }

    private static string BestParameter(IReadOnlyList<StrategyResearchResult> results, Func<StrategyResearchResult, string> selector)
    {
        return ParameterByScore(results, selector, descending: true);
    }

    private static string WeakParameter(IReadOnlyList<StrategyResearchResult> results, Func<StrategyResearchResult, string> selector)
    {
        return ParameterByScore(results, selector, descending: false);
    }

    private static string ParameterByScore(
        IReadOnlyList<StrategyResearchResult> results,
        Func<StrategyResearchResult, string> selector,
        bool descending)
    {
        var query = results
            .GroupBy(selector)
            .Select(group => new { Value = group.Key, Score = group.Average(result => result.Fitness.Score), Count = group.Count() });
        var best = descending
            ? query.OrderByDescending(item => item.Score).FirstOrDefault()
            : query.OrderBy(item => item.Score).FirstOrDefault();

        return best is null ? "-" : $"{best.Value}:avg={best.Score:0.####},count={best.Count}";
    }

    private static IReadOnlyList<string> BuildPatternPerformance(
        IReadOnlyList<StrategyResearchResult> results,
        IReadOnlyList<StrategyPatternDefinition> patterns,
        bool descending,
        int limit)
    {
        if (results.Count == 0)
        {
            return ["no_pattern_results"];
        }

        var patternNames = patterns.ToDictionary(
            pattern => pattern.Id,
            pattern => pattern.Name,
            StringComparer.OrdinalIgnoreCase);
        var groups = results
            .GroupBy(result => result.Variant.PatternId ?? $"family:{result.Variant.Family}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var average = group.Average(result => result.Fitness.Score);
                var best = group.Max(result => result.Fitness.Score);
                var trades = group.Sum(result => result.TradeCount);
                var key = group.Key;
                var name = patternNames.TryGetValue(key, out var patternName)
                    ? patternName
                    : key;

                return new
                {
                    Line = $"{name} ({key}):avg={average:0.####},best={best:0.####},count={group.Count()},trades={trades}",
                    Score = average
                };
            });

        groups = descending
            ? groups.OrderByDescending(item => item.Score).ThenBy(item => item.Line, StringComparer.OrdinalIgnoreCase)
            : groups.OrderBy(item => item.Score).ThenBy(item => item.Line, StringComparer.OrdinalIgnoreCase);

        return groups
            .Take(limit)
            .Select(item => item.Line)
            .ToList();
    }

    private static IReadOnlyList<string> BuildTradingDePatternPerformance(
        IReadOnlyList<StrategyResearchResult> results,
        IReadOnlyList<StrategyPatternDefinition> patterns,
        int limit)
    {
        var tradingDeIds = patterns
            .Where(pattern => pattern.SourceName?.Equals("Trading.de", StringComparison.OrdinalIgnoreCase) == true)
            .Select(pattern => pattern.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tradingDeResults = results
            .Where(result => result.Variant.PatternId is not null && tradingDeIds.Contains(result.Variant.PatternId))
            .ToList();

        return BuildPatternPerformance(tradingDeResults, patterns, descending: true, limit: limit);
    }

    private static IReadOnlyList<string> BuildSourcePerformance(
        IReadOnlyList<StrategyResearchResult> results,
        IReadOnlyList<StrategyPatternDefinition> patterns)
    {
        var patternsById = patterns.ToDictionary(
            pattern => pattern.Id,
            pattern => pattern,
            StringComparer.OrdinalIgnoreCase);

        return results
            .GroupBy(result =>
            {
                if (result.Variant.PatternId is not null
                    && patternsById.TryGetValue(result.Variant.PatternId, out var pattern))
                {
                    return $"{pattern.SourceName ?? "local"}|{pattern.SourceUrl ?? "-"}";
                }

                return $"local|family:{result.Variant.Family}";
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var parts = group.Key.Split('|', 2);
                var sourceName = parts[0];
                var sourceUrl = parts.Length > 1 ? parts[1] : "-";
                return new
                {
                    Line = $"{sourceName}:{sourceUrl}:avg={group.Average(result => result.Fitness.Score):0.####},best={group.Max(result => result.Fitness.Score):0.####},count={group.Count()}",
                    Score = group.Average(result => result.Fitness.Score)
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Line, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .Select(item => item.Line)
            .ToList();
    }

    private static IReadOnlyList<string> BuildAvoidCombinations(
        IReadOnlyList<StrategyResearchResult> results,
        IReadOnlyList<StrategyPatternDefinition> patterns)
    {
        var patternNames = patterns.ToDictionary(
            pattern => pattern.Id,
            pattern => pattern.Name,
            StringComparer.OrdinalIgnoreCase);

        return results
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .GroupBy(result => new
            {
                Pattern = result.Variant.PatternId ?? $"family:{result.Variant.Family}",
                Session = result.Variant.SessionFilter ?? "any",
                Timeframe = result.Variant.Timeframe ?? "any",
                Rr = result.Variant.RiskRewardRatio.ToString("0.##"),
                Sl = result.Variant.StopLossAtrMultiplier.ToString("0.##")
            })
            .Select(group =>
            {
                var name = patternNames.TryGetValue(group.Key.Pattern, out var patternName)
                    ? patternName
                    : group.Key.Pattern;
                return new
                {
                    Line = $"{name}:session={group.Key.Session},timeframe={group.Key.Timeframe},rr={group.Key.Rr},sl={group.Key.Sl},avg={group.Average(result => result.Fitness.Score):0.####},count={group.Count()}",
                    Score = group.Average(result => result.Fitness.Score),
                    Count = group.Count()
                };
            })
            .Where(item => item.Count >= 2 || item.Score < 0.55)
            .OrderBy(item => item.Score)
            .Take(8)
            .Select(item => item.Line)
            .ToList();
    }

    private static IReadOnlyList<string> BuildNextRecommendedTests(
        IReadOnlyList<StrategyResearchResult> results,
        IReadOnlyList<StrategyPatternDefinition> patterns)
    {
        var patternNames = patterns.ToDictionary(
            pattern => pattern.Id,
            pattern => pattern.Name,
            StringComparer.OrdinalIgnoreCase);

        return results
            .Where(result => result.Fitness.Score >= 0.82 && result.TradeCount > 0)
            .GroupBy(result => result.Variant.PatternId ?? $"family:{result.Variant.Family}", StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Average(result => result.Fitness.Score))
            .Take(8)
            .Select(group =>
            {
                var best = group.OrderByDescending(result => result.Fitness.Score).First();
                var name = patternNames.TryGetValue(group.Key, out var patternName)
                    ? patternName
                    : group.Key;
                return $"{name}: retest rr={best.Variant.RiskRewardRatio:0.##},sl={best.Variant.StopLossAtrMultiplier:0.##},session={best.Variant.SessionFilter ?? "any"},timeframe={best.Variant.Timeframe ?? "any"}";
            })
            .ToList();
    }

    private static IReadOnlyList<string> BuildRobustStrategies(
        WalkForwardValidationReport? walkForward,
        IReadOnlyList<StrategyResearchResult> results)
    {
        if (walkForward is not null && walkForward.Assessments.Count > 0)
        {
            return walkForward.Assessments
                .Where(item => item.Robust)
                .OrderByDescending(item => item.ValidationScore)
                .Take(12)
                .Select(item => $"{item.StrategyFamily}/{item.PatternId ?? "-"}:{item.StrategyVariantId}:validation={item.ValidationScore:0.####},oos={item.OutOfSampleScore:0.####}")
                .ToList();
        }

        return results
            .Where(result => result.Fitness.Score >= 0.82 && result.Fitness.Winrate is >= 0.35 and <= 0.95)
            .OrderByDescending(result => result.Fitness.Score)
            .Take(12)
            .Select(result => $"{result.Variant.Family}/{result.Variant.PatternId ?? "-"}:{result.Variant.VariantId}:score={result.Fitness.Score:0.####}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildOverfitStrategies(
        WalkForwardValidationReport? walkForward,
        IReadOnlyList<StrategyResearchResult> results)
    {
        if (walkForward is not null && walkForward.Assessments.Count > 0)
        {
            return walkForward.Assessments
                .Where(item => item.StrategyConfidence == "overfit_suspected")
                .Take(16)
                .Select(item => $"{item.StrategyFamily}/{item.PatternId ?? "-"}:{item.StrategyVariantId}:{string.Join("+", item.OverfitFlags)}")
                .ToList();
        }

        return results
            .Where(result => result.Fitness.Winrate >= 0.98 && result.TradeCount >= 500)
            .Take(16)
            .Select(result => $"{result.Variant.Family}/{result.Variant.PatternId ?? "-"}:{result.Variant.VariantId}:suspicious_winrate={result.Fitness.Winrate:0.####}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildHighRiskStrategies(WalkForwardValidationReport? walkForward)
    {
        return walkForward?.Assessments
            .Where(item => item.HighRisk)
            .Take(16)
            .Select(item => $"{item.StrategyFamily}/{item.PatternId ?? "-"}:{item.StrategyVariantId}:confidence={item.StrategyConfidence}")
            .ToList() ?? [];
    }

    private static IReadOnlyList<string> BuildTooGoodToBeTrueStrategies(WalkForwardValidationReport? walkForward)
    {
        return walkForward?.Assessments
            .Where(item => item.TooGoodToBeTrue || item.OverfitFlags.Contains("too_good_to_be_true", StringComparer.Ordinal))
            .Take(16)
            .Select(item =>
            {
                var reason = string.IsNullOrWhiteSpace(item.RealismPenaltyReason)
                    ? string.Join("+", item.OverfitFlags.Take(5))
                    : item.RealismPenaltyReason;
                return $"{item.StrategyFamily}/{item.PatternId ?? "-"}:{item.StrategyVariantId}:realism={item.RealismScore:0.####},reason={reason}";
            })
            .ToList() ?? [];
    }

    private static IReadOnlyList<string> BuildCostSensitiveStrategies(CostSensitivityReport? costReport)
    {
        return costReport?.Entries
            .Where(entry => entry.Status is "cost_sensitive" or "fails_under_stress_cost" || entry.WorksOnlyWithoutCosts)
            .Take(16)
            .Select(entry => $"{entry.StrategyFamily}/{entry.PatternId ?? "-"}:{entry.StrategyVariantId}:normal={entry.NormalCostScore:0.####},stress={entry.StressCostScore:0.####},status={entry.Status}")
            .ToList() ?? [];
    }

    private static IReadOnlyList<string> BuildCostSensitivitySummary(CostSensitivityReport? costReport)
    {
        if (costReport is null)
        {
            return ["cost_sensitivity_report_missing"];
        }

        return
        [
            $"strategies_evaluated:{costReport.StrategiesEvaluated}",
            $"cost_sensitive:{costReport.CostSensitiveStrategies}",
            $"stress_failures:{costReport.StressCostFailures}",
            $"avg_cost_sensitivity:{costReport.AverageCostSensitivity:0.####}"
        ];
    }

    private static IReadOnlyList<string> BuildRobustGateSummary(
        WalkForwardValidationReport? walkForward,
        CostSensitivityReport? costReport,
        StrategyRegimePerformanceReport regimeReport)
    {
        var assessments = walkForward?.Assessments ?? [];
        return
        [
            $"robust:{assessments.Count(item => item.Robust)}",
            $"promising:{assessments.Count(item => item.StrategyConfidence == "promising")}",
            $"experimental:{assessments.Count(item => item.StrategyConfidence == "experimental")}",
            $"overfit_suspected:{assessments.Count(item => item.StrategyConfidence == "overfit_suspected")}",
            $"rejected:{assessments.Count(item => item.StrategyConfidence == "rejected")}",
            $"too_good_to_be_true:{assessments.Count(item => item.TooGoodToBeTrue)}",
            $"oos_available:{assessments.Count(item => item.OosAvailable)}",
            $"cost_sensitive:{costReport?.CostSensitiveStrategies ?? 0}",
            $"regime_consistency:{regimeReport.RegimeConsistencyScore:0.####}",
            $"regime_sample_quality:{regimeReport.RegimeSampleQuality:0.####}"
        ];
    }

    private static IReadOnlyList<string> BuildStableSymbolTimeframeCombinations(IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .Where(result => result.Fitness.Score >= 0.72)
            .SelectMany(result => result.SymbolsProcessed.SelectMany(symbol => result.TimeframesProcessed.Select(timeframe => new { symbol, timeframe, result.Fitness.Score })))
            .GroupBy(item => $"{item.symbol}:{item.timeframe}", StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}:avg={group.Average(item => item.Score):0.####},count={group.Count()}")
            .OrderByDescending(line => line)
            .Take(12)
            .ToList();
    }
}
