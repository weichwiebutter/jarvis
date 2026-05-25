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

        var patternCatalog = new StrategyPatternCatalog(_storagePaths);
        var patterns = patternCatalog.LoadOrCreateCatalog();
        var memory = LoadMemory() ?? EmptyMemory();
        var tested = memory.TestedVariantIds.ToHashSet(StringComparer.Ordinal);
        var features = ReadLatestFeatures();
        var warnings = new List<string>();
        if (features.Count == 0)
        {
            warnings.Add("No FeatureVectors found; run generate-features or run-beta-learning before strategy research.");
        }

        var newResults = new List<StrategyResearchResult>();
        foreach (var variant in GenerateVariants(memory, patterns)
                     .Where(variant => !tested.Contains(variant.VariantId))
                     .Take(128))
        {
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
            HumanReviewRequired: true,
            ResearchEntries: BuildResearchEntries(allResults));

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

    private static IReadOnlyList<StrategyVariant> GenerateVariants(
        StrategyResearchMemory memory,
        IReadOnlyList<StrategyPatternDefinition> patterns)
    {
        var variants = new List<StrategyVariant>();
        var fastEmaValues = new[] { 9, 12 };
        var slowEmaValues = new[] { 21, 34 };
        var rrValues = new[] { 1.4, 1.8 };
        var slValues = new[] { 1.0, 1.5 };
        var patternContexts = patterns.Count == 0
            ? StrategyDefinitions()
                .Select(definition => (definition.Family, PatternId: (string?)null, Sessions: (IReadOnlyList<string?>)[null], Timeframes: (IReadOnlyList<string?>)[null]))
                .ToList()
            : patterns
                .Select(pattern => (
                    Family: StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id),
                    PatternId: (string?)pattern.Id,
                    Sessions: SessionFiltersFor(pattern),
                    Timeframes: TimeframeFiltersFor(pattern)))
                .ToList();

        foreach (var fastEma in fastEmaValues)
        foreach (var slowEma in slowEmaValues)
        foreach (var rr in rrValues)
        foreach (var sl in slValues)
        foreach (var confirmation in new[] { false, true })
        foreach (var volatilityFilter in new[] { false, true })
        foreach (var context in patternContexts)
        foreach (var sessionFilter in context.Sessions)
        foreach (var timeframe in context.Timeframes)
        {
            if (fastEma >= slowEma)
            {
                continue;
            }

            variants.Add(CreateVariant(
                context.Family,
                fastEma,
                slowEma,
                rr,
                sl,
                confirmation,
                volatilityFilter,
                context.PatternId,
                sessionFilter,
                timeframe));
        }

        variants.AddRange(GenerateAdaptiveVariants(memory, patterns));
        return variants
            .GroupBy(variant => variant.VariantId)
            .Select(group => group.First())
            .ToList();
    }

    private static IEnumerable<StrategyVariant> GenerateAdaptiveVariants(
        StrategyResearchMemory memory,
        IReadOnlyList<StrategyPatternDefinition> patterns)
    {
        var tested = memory.TestedVariantIds.ToHashSet(StringComparer.Ordinal);
        var topSeeds = StrategyVariantGenerator.PrioritizeSeeds(memory).ToList();

        if (topSeeds.Count == 0)
        {
            topSeeds =
            [
                CreateVariant("ema_pullback", 9, 21, 1.8, 1.0, true, false, "ema_pullback"),
                CreateVariant("breakout", 12, 34, 1.8, 1.5, true, true, "inside_bar_breakout"),
                CreateVariant("mean_reversion", 9, 21, 1.4, 1.0, false, false, "mean_reversion_rejection"),
                CreateVariant("trend_continuation", 12, 34, 1.8, 1.0, true, true, "breakout_continuation")
            ];
        }

        foreach (var seed in topSeeds)
        {
            foreach (var fast in StrategyMutationEngine.FastEmaCandidates(seed.FastEma))
            foreach (var slow in StrategyMutationEngine.SlowEmaCandidates(seed.SlowEma))
            foreach (var rr in StrategyMutationEngine.RiskRewardCandidates(seed.RiskRewardRatio))
            foreach (var sl in StrategyMutationEngine.StopLossAtrCandidates(seed.StopLossAtrMultiplier))
            foreach (var confirmation in new[] { seed.RequireConfirmationCandle, !seed.RequireConfirmationCandle })
            foreach (var volatilityFilter in new[] { seed.UseVolatilityFilter, !seed.UseVolatilityFilter })
            {
                if (fast >= slow)
                {
                    continue;
                }

                var variant = CreateVariant(
                    seed.Family,
                    fast,
                    slow,
                    rr,
                    sl,
                    confirmation,
                    volatilityFilter,
                    seed.PatternId,
                    seed.SessionFilter,
                    seed.Timeframe);
                if (!tested.Contains(variant.VariantId))
                {
                    yield return variant;
                }
            }
        }

        var patternContexts = patterns.Count == 0
            ? StrategyDefinitions()
                .Select(definition => (definition.Family, PatternId: (string?)null, Sessions: (IReadOnlyList<string?>)[null], Timeframes: (IReadOnlyList<string?>)[null]))
                .ToList()
            : patterns
                .Select(pattern => (
                    Family: StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id),
                    PatternId: (string?)pattern.Id,
                    Sessions: SessionFiltersFor(pattern),
                    Timeframes: TimeframeFiltersFor(pattern)))
                .ToList();
        foreach (var context in patternContexts)
        {
            var sessionFilter = context.Sessions.FirstOrDefault();
            var timeframe = context.Timeframes.FirstOrDefault();
            foreach (var variant in new[]
            {
                CreateVariant(context.Family, 8, 24, 1.6, 1.2, true, true, context.PatternId, sessionFilter, timeframe),
                CreateVariant(context.Family, 14, 40, 2.0, 1.8, true, false, context.PatternId, sessionFilter, timeframe),
                CreateVariant(context.Family, 6, 18, 1.2, 0.8, false, true, context.PatternId, sessionFilter, timeframe),
                CreateVariant(context.Family, 16, 55, 2.2, 2.0, true, true, context.PatternId, sessionFilter, timeframe)
            })
            {
                if (!tested.Contains(variant.VariantId))
                {
                    yield return variant;
                }
            }
        }
    }

    private static IReadOnlyList<int> NeighborInts(int seed, IReadOnlyList<int> values)
    {
        return values
            .OrderBy(value => Math.Abs(value - seed))
            .Take(4)
            .ToList();
    }

    private static IReadOnlyList<double> NeighborDoubles(double seed, IReadOnlyList<double> values)
    {
        return values
            .OrderBy(value => Math.Abs(value - seed))
            .Take(4)
            .ToList();
    }

    private static StrategyVariant CreateVariant(
        string family,
        int fastEma,
        int slowEma,
        double rr,
        double sl,
        bool confirmation,
        bool volatilityFilter,
        string? patternId = null,
        string? sessionFilter = null,
        string? timeframe = null)
    {
        var idSeed = $"{family}|{patternId ?? "no_pattern"}|{sessionFilter ?? "any_session"}|{timeframe ?? "any_timeframe"}|{fastEma}|{slowEma}|{rr:0.00}|{sl:0.00}|{confirmation}|{volatilityFilter}";
        return new StrategyVariant(
            VariantId: $"variant_{ShortHash(idSeed)}",
            Family: family,
            FastEma: fastEma,
            SlowEma: slowEma,
            RiskRewardRatio: rr,
            StopLossAtrMultiplier: sl,
            RequireConfirmationCandle: confirmation,
            UseVolatilityFilter: volatilityFilter,
            PatternId: patternId,
            SessionFilter: sessionFilter,
            Timeframe: timeframe);
    }

    private StrategyResearchResult EvaluateVariant(
        StrategyVariant variant,
        IReadOnlyList<GeneratedFeatureVector> features)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var candidateFeatures = features
            .Where(feature => IsCandidate(variant, feature))
            .ToList();
        var trades = candidateFeatures
            .Select(feature => EvaluateTrade(variant, feature))
            .ToList();
        var processedFeatures = candidateFeatures.Count == 0 ? features : candidateFeatures;

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
            SymbolsProcessed: processedFeatures.Select(feature => feature.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            TimeframesProcessed: processedFeatures.Select(feature => feature.Timeframe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            Status: "completed",
            Warnings: warnings,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            FromUtc: processedFeatures.Count == 0 ? null : processedFeatures.Min(feature => feature.TimestampUtc),
            ToUtc: processedFeatures.Count == 0 ? null : processedFeatures.Max(feature => feature.TimestampUtc));
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

        if (!string.IsNullOrWhiteSpace(variant.SessionFilter)
            && !feature.MockSession.Equals(variant.SessionFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(variant.Timeframe)
            && !feature.Timeframe.Equals(variant.Timeframe, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (variant.PatternId == "bullish_engulfing" && feature.Direction != "up")
        {
            return false;
        }

        if (variant.PatternId == "bearish_engulfing" && feature.Direction != "down")
        {
            return false;
        }

        if (variant.PatternId == "first_candle_breakout"
            && feature.MockSession.Equals("off_session", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (variant.PatternId == "liquidity_sweep_reversal"
            && feature.MockRegime is not ("range" or "high_volatility"))
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
        var patternEdge = variant.PatternId switch
        {
            "breakout_continuation" when feature.MockRegime.StartsWith("trend", StringComparison.OrdinalIgnoreCase) => 0.035,
            "inside_bar_breakout" when feature.MockRegime == "range" => 0.025,
            "first_candle_breakout" when feature.MockSession is "london" or "new_york" => 0.03,
            "ema_pullback" when feature.MockRegime.StartsWith("trend", StringComparison.OrdinalIgnoreCase) => 0.025,
            "mean_reversion_rejection" when feature.MockRegime == "range" => 0.03,
            "liquidity_sweep_reversal" when feature.MockRegime == "high_volatility" => 0.02,
            "bullish_engulfing" when feature.Direction == "up" => 0.018,
            "bearish_engulfing" when feature.Direction == "down" => 0.018,
            _ => 0
        };
        var rrEdge = (variant.RiskRewardRatio - 1.0) * 0.08;
        var slPenalty = Math.Abs(variant.StopLossAtrMultiplier - 1.2) * 0.04;
        var emaPenalty = Math.Abs(variant.FastEma - 10) * 0.002 + Math.Abs(variant.SlowEma - 21) * 0.001;
        var expectation = baseEdge + familyEdge + confirmationEdge + volatilityEdge + patternEdge + rrEdge - slPenalty - emaPenalty;

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

    private static IReadOnlyList<StrategyResearchMemoryEntry> BuildResearchEntries(
        IReadOnlyList<StrategyResearchResult> results)
    {
        return results
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .SelectMany(result => result.SymbolsProcessed.SelectMany(symbol => result.TimeframesProcessed.Select(timeframe =>
                new StrategyResearchMemoryEntry(
                    PatternId: result.Variant.PatternId ?? "-",
                    StrategyVariantId: result.Variant.VariantId,
                    Symbol: symbol,
                    Timeframe: timeframe,
                    FromUtc: result.FromUtc,
                    ToUtc: result.ToUtc,
                    FitnessScore: result.Fitness.Score,
                    Status: ClassifyResearchStatus(result)))))
            .OrderBy(entry => entry.PatternId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.StrategyVariantId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Symbol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Timeframe, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ClassifyResearchStatus(StrategyResearchResult result)
    {
        if (result.TradeCount == 0 || result.Fitness.Score < 0.35)
        {
            return "rejected";
        }

        if (result.Fitness.Score < 0.55)
        {
            return "weak";
        }

        if (result.Fitness.Score < 0.82)
        {
            return "retest";
        }

        return "promising";
    }

    private static IReadOnlyList<string?> SessionFiltersFor(StrategyPatternDefinition pattern)
    {
        return new string?[] { null }
            .Concat(pattern.PreferredSessions.Take(2))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string?> TimeframeFiltersFor(StrategyPatternDefinition pattern)
    {
        return new string?[] { null }
            .Concat(pattern.RequiredTimeframes.Take(2))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
