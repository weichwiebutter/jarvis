using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class StrategyResearchService
{
    private const string MemoryVersion = "strategy_research_memory_v1";

    private readonly StoragePaths _storagePaths;

    public StrategyResearchService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StrategyResearchRoot => Path.Combine(_storagePaths.Root, "strategy_research");

    public string ResultsDirectory => Path.Combine(StrategyResearchRoot, "results");

    public string MemoryPath => Path.Combine(StrategyResearchRoot, "strategy_research_memory.json");

    public StrategyResearchMemory RunResearch()
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        Directory.CreateDirectory(ResultsDirectory);

        var memory = LoadMemory() ?? EmptyMemory();
        var tested = memory.TestedVariantIds.ToHashSet(StringComparer.Ordinal);
        var features = ReadLatestFeatures();
        var warnings = new List<string>();
        if (features.Count == 0)
        {
            warnings.Add("No FeatureVectors found; run generate-features or run-beta-learning before strategy research.");
        }

        var newResults = new List<StrategyResearchResult>();
        foreach (var variant in GenerateVariants())
        {
            if (tested.Contains(variant.VariantId))
            {
                continue;
            }

            var result = EvaluateVariant(variant, features);
            WriteResult(result);
            newResults.Add(result);
            tested.Add(variant.VariantId);
        }

        var allResults = LoadAllResults()
            .Concat(newResults)
            .GroupBy(result => result.Variant.VariantId)
            .Select(group => group.OrderByDescending(result => result.CompletedAtUtc).First())
            .ToList();

        var updated = new StrategyResearchMemory(
            MemoryVersion: MemoryVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            VariantsTested: tested.Count,
            TestedVariantIds: tested.OrderBy(value => value).ToList(),
            TopVariants: allResults
                .Where(result => result.Status == "completed")
                .OrderByDescending(result => result.Fitness.Score)
                .ThenByDescending(result => result.TradeCount)
                .Take(12)
                .ToList(),
            RejectedVariants: allResults
                .Where(result => result.Status == "completed")
                .OrderBy(result => result.Fitness.Score)
                .ThenBy(result => result.TradeCount)
                .Take(12)
                .ToList(),
            Warnings: warnings
                .Concat(memory.Warnings)
                .Distinct(StringComparer.Ordinal)
                .Take(30)
                .ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteMemory(updated);
        return updated;
    }

    public StrategyResearchMemory LoadOrCreateMemory()
    {
        return LoadMemory() ?? EmptyMemory();
    }

    private static IReadOnlyList<StrategyDefinition> StrategyDefinitions() =>
    [
        new("strategy_ema_pullback_v1", "ema_pullback", "EMA pullback candidate on generated FeatureVectors."),
        new("strategy_breakout_v1", "breakout", "Breakout candidate using candle range and signal score."),
        new("strategy_mean_reversion_v1", "mean_reversion", "Mean reversion candidate against short-term directional stretch."),
        new("strategy_trend_continuation_v1", "trend_continuation", "Trend continuation candidate on repeated directional features.")
    ];

    private static IReadOnlyList<StrategyVariant> GenerateVariants()
    {
        var variants = new List<StrategyVariant>();
        var fastEmaValues = new[] { 9, 12 };
        var slowEmaValues = new[] { 21, 34 };
        var rrValues = new[] { 1.4, 1.8 };
        var slValues = new[] { 1.0, 1.5 };

        foreach (var definition in StrategyDefinitions())
        {
            foreach (var fastEma in fastEmaValues)
            foreach (var slowEma in slowEmaValues)
            foreach (var rr in rrValues)
            foreach (var sl in slValues)
            foreach (var confirmation in new[] { false, true })
            foreach (var volatilityFilter in new[] { false, true })
            {
                if (fastEma >= slowEma)
                {
                    continue;
                }

                var idSeed = $"{definition.Family}|{fastEma}|{slowEma}|{rr:0.00}|{sl:0.00}|{confirmation}|{volatilityFilter}";
                variants.Add(new StrategyVariant(
                    VariantId: $"variant_{ShortHash(idSeed)}",
                    Family: definition.Family,
                    FastEma: fastEma,
                    SlowEma: slowEma,
                    RiskRewardRatio: rr,
                    StopLossAtrMultiplier: sl,
                    RequireConfirmationCandle: confirmation,
                    UseVolatilityFilter: volatilityFilter));
            }
        }

        return variants;
    }

    private StrategyResearchResult EvaluateVariant(
        StrategyVariant variant,
        IReadOnlyList<GeneratedFeatureVector> features)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var trades = features
            .Where(feature => IsCandidate(variant, feature))
            .Select(feature => EvaluateTrade(variant, feature))
            .ToList();

        if (trades.Count == 0)
        {
            warnings.Add("Variant produced no research trades on available FeatureVectors.");
        }

        var wins = trades.Count(value => value > 0);
        var losses = trades.Count(value => value <= 0);
        var winrate = trades.Count == 0 ? 0 : wins / (double)trades.Count;
        var averageR = trades.Count == 0 ? 0 : trades.Average();
        var maxDrawdown = CalculateMaxDrawdown(trades);
        var averageRr = Math.Max(0, averageR);
        var drawdownPenalty = Math.Round(Math.Min(0.35, Math.Abs(maxDrawdown) * 0.08), 4);
        var stabilityBonus = Math.Round(Math.Max(0, 1 - StandardDeviation(trades)) * 0.12, 4);
        var tradeCountFactor = Math.Round(Math.Min(0.2, Math.Log10(Math.Max(1, trades.Count)) * 0.08), 4);
        var score = Math.Round(
            Math.Clamp((winrate * 0.42) + (averageRr * 0.22) + stabilityBonus + tradeCountFactor - drawdownPenalty, 0, 1),
            4);

        return new StrategyResearchResult(
            ResultId: $"strategy_research_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{variant.VariantId}",
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Variant: variant,
            Fitness: new StrategyFitnessScore(
                Score: score,
                Winrate: Math.Round(winrate, 4),
                AverageRr: Math.Round(averageRr, 4),
                DrawdownPenalty: drawdownPenalty,
                StabilityBonus: stabilityBonus,
                TradeCountFactor: tradeCountFactor),
            TradeCount: trades.Count,
            WinCount: wins,
            LossCount: losses,
            AverageR: Math.Round(averageR, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            SymbolsProcessed: features.Select(feature => feature.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            TimeframesProcessed: features.Select(feature => feature.Timeframe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            Status: "completed",
            Warnings: warnings,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static bool IsCandidate(StrategyVariant variant, GeneratedFeatureVector feature)
    {
        var scoreThreshold = variant.Family switch
        {
            "breakout" => 0.58,
            "trend_continuation" => 0.54,
            "ema_pullback" => 0.50,
            "mean_reversion" => 0.45,
            _ => 0.5
        };

        if (variant.RequireConfirmationCandle && feature.Direction == "flat")
        {
            return false;
        }

        if (variant.UseVolatilityFilter && feature.CandleRange <= 0)
        {
            return false;
        }

        if (variant.Family == "mean_reversion")
        {
            return feature.MockSignalScore >= scoreThreshold
                && (feature.MockRegime == "range" || Math.Abs(feature.SimpleReturn) > 0.0002);
        }

        return feature.MockSignalScore >= scoreThreshold
            && feature.Direction is "up" or "down";
    }

    private static double EvaluateTrade(StrategyVariant variant, GeneratedFeatureVector feature)
    {
        var baseEdge = feature.MockSignalScore - 0.5;
        var familyEdge = variant.Family switch
        {
            "breakout" when feature.MockRegime == "high_volatility" => 0.09,
            "trend_continuation" when feature.MockRegime.StartsWith("trend", StringComparison.OrdinalIgnoreCase) => 0.08,
            "ema_pullback" when feature.MockSession.Contains("london", StringComparison.OrdinalIgnoreCase) => 0.06,
            "mean_reversion" when feature.MockRegime == "range" => 0.07,
            _ => 0.01
        };
        var confirmationEdge = variant.RequireConfirmationCandle ? 0.025 : -0.01;
        var volatilityEdge = variant.UseVolatilityFilter && feature.MockRegime == "high_volatility" ? 0.035 : 0;
        var rrEdge = (variant.RiskRewardRatio - 1.0) * 0.08;
        var slPenalty = Math.Abs(variant.StopLossAtrMultiplier - 1.2) * 0.04;
        var emaPenalty = Math.Abs(variant.FastEma - 10) * 0.002 + Math.Abs(variant.SlowEma - 21) * 0.001;
        var expectation = baseEdge + familyEdge + confirmationEdge + volatilityEdge + rrEdge - slPenalty - emaPenalty;

        return expectation >= 0.12
            ? variant.RiskRewardRatio
            : expectation >= 0.04
                ? Math.Round(variant.RiskRewardRatio * 0.45, 4)
                : -1.0;
    }

    private IReadOnlyList<GeneratedFeatureVector> ReadLatestFeatures()
    {
        var directory = Path.Combine(_storagePaths.Root, "exports", "features");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var featureFile = Directory.EnumerateFiles(directory, "*.features.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .LastOrDefault();
        if (featureFile is null)
        {
            return [];
        }

        var features = new List<GeneratedFeatureVector>();
        foreach (var line in File.ReadLines(featureFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var feature = JsonSerializer.Deserialize<GeneratedFeatureVector>(line, JsonDefaults.SnapshotReadOptions);
                if (feature is not null)
                {
                    features.Add(feature);
                }
            }
            catch (JsonException)
            {
                // Strategy research skips malformed feature rows and keeps the run local.
            }
        }

        return features;
    }

    private StrategyResearchMemory? LoadMemory()
    {
        if (!File.Exists(MemoryPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyResearchMemory>(
                File.ReadAllText(MemoryPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IEnumerable<StrategyResearchResult> LoadAllResults()
    {
        if (!Directory.Exists(ResultsDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(ResultsDirectory, "*.strategy_result.json", SearchOption.TopDirectoryOnly))
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

    private void WriteResult(StrategyResearchResult result)
    {
        Directory.CreateDirectory(ResultsDirectory);
        var path = Path.Combine(ResultsDirectory, $"{result.ResultId}.strategy_result.json");
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions));
    }

    private void WriteMemory(StrategyResearchMemory memory)
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        File.WriteAllText(MemoryPath, JsonSerializer.Serialize(memory, JsonDefaults.WriteOptions));
    }

    private static StrategyResearchMemory EmptyMemory()
    {
        return new StrategyResearchMemory(
            MemoryVersion: MemoryVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            VariantsTested: 0,
            TestedVariantIds: [],
            TopVariants: [],
            RejectedVariants: [],
            Warnings: [],
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static double CalculateMaxDrawdown(IReadOnlyList<double> trades)
    {
        var equity = 0.0;
        var peak = 0.0;
        var maxDrawdown = 0.0;
        foreach (var trade in trades)
        {
            equity += trade;
            peak = Math.Max(peak, equity);
            maxDrawdown = Math.Min(maxDrawdown, equity - peak);
        }

        return maxDrawdown;
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 1;
        }

        var average = values.Average();
        var variance = values.Average(value => Math.Pow(value - average, 2));
        return Math.Sqrt(variance);
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

