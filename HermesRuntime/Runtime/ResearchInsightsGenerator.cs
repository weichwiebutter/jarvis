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
        var results = LoadResults().ToList();
        var completed = results
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var top = completed
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
            HumanReviewRequired: true);

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
            .GroupBy(result => result.Variant.Family)
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
            $"best_volatility_filter:{BestParameter(results, result => result.Variant.UseVolatilityFilter.ToString().ToLowerInvariant())}"
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
}

