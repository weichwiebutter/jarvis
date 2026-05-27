using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MarketRegimeClassifier
{
    private const string ReportVersion = "market_regime_intelligence_v1";

    private readonly StoragePaths _storagePaths;

    public MarketRegimeClassifier(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string RegimeMemoryRoot => Path.Combine(_storagePaths.Root, "research_memory", "regimes");

    public string RegimeReportRoot => Path.Combine(_storagePaths.Root, "reports", "regimes");

    public string SummaryPath => Path.Combine(RegimeReportRoot, "regime_summary.json");

    public string DistributionPath => Path.Combine(RegimeReportRoot, "regime_distribution.json");

    public string StrategyPerformancePath => Path.Combine(RegimeReportRoot, "strategy_regime_performance.json");

    public string SnapshotMemoryPath => Path.Combine(RegimeMemoryRoot, "latest_regime_snapshots.jsonl");

    public MarketRegimeAnalysisResult Run()
    {
        Directory.CreateDirectory(RegimeMemoryRoot);
        Directory.CreateDirectory(RegimeReportRoot);

        var warnings = new List<string>();
        var featureFile = FindLatestFeatureFile();
        var contexts = featureFile is null
            ? new List<RegimeContext>()
            : ReadContexts(featureFile, warnings).ToList();
        if (featureFile is null)
        {
            warnings.Add("No generated FeatureVectors found. Run generate-features or beta learning before regime classification.");
        }

        var snapshots = BuildSnapshots(contexts);
        var summary = BuildSummary(featureFile ?? "-", contexts.Count, snapshots, warnings);
        var distribution = BuildDistribution(snapshots, warnings);
        var strategyPerformance = BuildStrategyPerformance(snapshots, warnings);

        WriteSnapshotMemory(snapshots);
        File.WriteAllText(SummaryPath, JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions));
        File.WriteAllText(DistributionPath, JsonSerializer.Serialize(distribution, JsonDefaults.WriteOptions));
        File.WriteAllText(StrategyPerformancePath, JsonSerializer.Serialize(strategyPerformance, JsonDefaults.WriteOptions));

        return new MarketRegimeAnalysisResult(
            summary,
            distribution,
            strategyPerformance,
            SummaryPath,
            DistributionPath,
            StrategyPerformancePath,
            SnapshotMemoryPath);
    }

    public RegimeSummaryReport? LoadSummary() => LoadReport<RegimeSummaryReport>(SummaryPath);

    public RegimeDistributionReport? LoadDistribution() => LoadReport<RegimeDistributionReport>(DistributionPath);

    public StrategyRegimePerformanceReport? LoadStrategyPerformance() =>
        LoadReport<StrategyRegimePerformanceReport>(StrategyPerformancePath);

    private string? FindLatestFeatureFile()
    {
        var directory = Path.Combine(_storagePaths.Root, "exports", "features");
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*.features.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.Ordinal)
            .LastOrDefault();
    }

    private static IEnumerable<RegimeContext> ReadContexts(string featureFile, List<string> warnings)
    {
        var rowsRead = 0;
        var malformed = 0;
        foreach (var line in File.ReadLines(featureFile))
        {
            rowsRead++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            GeneratedFeatureVector? feature;
            try
            {
                feature = JsonSerializer.Deserialize<GeneratedFeatureVector>(line, JsonDefaults.SnapshotReadOptions);
            }
            catch (JsonException)
            {
                malformed++;
                continue;
            }

            if (feature is null)
            {
                continue;
            }

            yield return Classify(feature);
        }

        if (malformed > 0)
        {
            warnings.Add($"Skipped malformed feature rows: {malformed}/{rowsRead}.");
        }
    }

    private static RegimeContext Classify(GeneratedFeatureVector feature)
    {
        var rangeRatio = feature.Close == 0 ? 0 : feature.CandleRange / Math.Abs(feature.Close);
        var bodyRatio = feature.CandleRange == 0 ? 0 : Math.Clamp(feature.BodySize / feature.CandleRange, 0, 1);
        var trendSlope = feature.SimpleReturn;
        var momentumPersistence = Math.Clamp(Math.Abs(feature.SimpleReturn) * 1200, 0, 1);
        var breakoutFrequency = Math.Clamp((rangeRatio * 320) + (bodyRatio * 0.35) + Math.Max(0, feature.MockSignalScore - 0.55), 0, 1);
        var volatilityCompression = Math.Clamp(1 - (rangeRatio * 600), 0, 1);
        var regime = ClassifyRegime(feature, rangeRatio, bodyRatio, breakoutFrequency, volatilityCompression);
        var confidence = regime switch
        {
            "news_like_volatility" => Math.Clamp((rangeRatio * 140) + momentumPersistence, 0.55, 0.98),
            "breakout" => Math.Clamp(breakoutFrequency, 0.52, 0.95),
            "trending" => Math.Clamp(0.52 + momentumPersistence + bodyRatio * 0.18, 0.52, 0.94),
            "ranging" => Math.Clamp(0.5 + volatilityCompression * 0.3 + (1 - bodyRatio) * 0.18, 0.5, 0.92),
            "high_volatility" => Math.Clamp(rangeRatio * 220, 0.5, 0.93),
            "low_volatility" => Math.Clamp(volatilityCompression, 0.5, 0.9),
            _ => 0.35
        };

        return new RegimeContext(
            TimestampUtc: feature.TimestampUtc,
            Symbol: feature.Symbol,
            Timeframe: feature.Timeframe,
            RegimeType: regime,
            Session: NormalizeSession(feature.MockSession, feature.TimestampUtc),
            AtrProxy: Math.Round(feature.CandleRange, 6),
            RangeRatio: Math.Round(rangeRatio, 8),
            BodyRatio: Math.Round(bodyRatio, 4),
            TrendSlope: Math.Round(trendSlope, 8),
            MomentumPersistence: Math.Round(momentumPersistence, 4),
            BreakoutFrequency: Math.Round(breakoutFrequency, 4),
            VolatilityCompression: Math.Round(volatilityCompression, 4),
            Confidence: Math.Round(confidence, 4));
    }

    private static string ClassifyRegime(
        GeneratedFeatureVector feature,
        double rangeRatio,
        double bodyRatio,
        double breakoutFrequency,
        double volatilityCompression)
    {
        var absReturn = Math.Abs(feature.SimpleReturn);
        if (rangeRatio >= 0.006 || absReturn >= 0.004)
        {
            return "news_like_volatility";
        }

        if (breakoutFrequency >= 0.72 && bodyRatio >= 0.55)
        {
            return "breakout";
        }

        if (feature.MockRegime.StartsWith("trend", StringComparison.OrdinalIgnoreCase)
            || (absReturn >= 0.00045 && bodyRatio >= 0.42))
        {
            return "trending";
        }

        if (rangeRatio >= 0.0025)
        {
            return "high_volatility";
        }

        if (volatilityCompression >= 0.76 && bodyRatio <= 0.45)
        {
            return "low_volatility";
        }

        if (feature.MockRegime.Equals("range", StringComparison.OrdinalIgnoreCase)
            || bodyRatio <= 0.55)
        {
            return "ranging";
        }

        return "unknown";
    }

    private static string NormalizeSession(string session, DateTimeOffset timestampUtc)
    {
        if (session.Equals("london_new_york_overlap", StringComparison.OrdinalIgnoreCase))
        {
            return "session_overlap";
        }

        if (session.Equals("london", StringComparison.OrdinalIgnoreCase))
        {
            return "session_london";
        }

        if (session.Equals("new_york", StringComparison.OrdinalIgnoreCase))
        {
            return "session_newyork";
        }

        var hour = timestampUtc.UtcDateTime.Hour;
        return hour is >= 0 and < 7 ? "session_asia" : "session_unknown";
    }

    private static IReadOnlyList<MarketRegimeSnapshot> BuildSnapshots(IReadOnlyList<RegimeContext> contexts)
    {
        return contexts
            .GroupBy(context => new
            {
                context.Symbol,
                context.Timeframe,
                context.RegimeType,
                context.Session
            })
            .Select(group =>
            {
                var first = group.Min(context => context.TimestampUtc);
                var last = group.Max(context => context.TimestampUtc);
                var trendSlope = group.Average(context => context.TrendSlope);
                var snapshotId = $"regime_{group.Key.Symbol}_{group.Key.Timeframe}_{group.Key.RegimeType}_{group.Key.Session}";

                return new MarketRegimeSnapshot(
                    SnapshotId: snapshotId.ToLowerInvariant(),
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Symbol: group.Key.Symbol,
                    Timeframe: group.Key.Timeframe,
                    FromUtc: first,
                    ToUtc: last,
                    RegimeType: group.Key.RegimeType,
                    Session: group.Key.Session,
                    CandleCount: group.Count(),
                    AverageAtrProxy: Math.Round(group.Average(context => context.AtrProxy), 6),
                    AverageRangeRatio: Math.Round(group.Average(context => context.RangeRatio), 8),
                    AverageBodyRatio: Math.Round(group.Average(context => context.BodyRatio), 4),
                    TrendSlope: Math.Round(trendSlope, 8),
                    MomentumPersistence: Math.Round(group.Average(context => context.MomentumPersistence), 4),
                    BreakoutFrequency: Math.Round(group.Average(context => context.BreakoutFrequency), 4),
                    VolatilityCompression: Math.Round(group.Average(context => context.VolatilityCompression), 4),
                    Confidence: Math.Round(group.Average(context => context.Confidence), 4),
                    NoAutoTrading: true,
                    HumanReviewRequired: true);
            })
            .OrderByDescending(snapshot => snapshot.CandleCount)
            .ThenBy(snapshot => snapshot.Symbol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Timeframe, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RegimeSummaryReport BuildSummary(
        string sourceFeatureFile,
        int featuresAnalyzed,
        IReadOnlyList<MarketRegimeSnapshot> snapshots,
        IReadOnlyList<string> warnings)
    {
        return new RegimeSummaryReport(
            ReportVersion: ReportVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            SourceFeatureFile: sourceFeatureFile,
            FeaturesAnalyzed: featuresAnalyzed,
            SnapshotCount: snapshots.Count,
            Symbols: snapshots.Select(snapshot => snapshot.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            Timeframes: snapshots.Select(snapshot => snapshot.Timeframe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            DominantRegimes: snapshots
                .GroupBy(snapshot => snapshot.RegimeType)
                .OrderByDescending(group => group.Sum(snapshot => snapshot.CandleCount))
                .Select(group => $"{group.Key}:{group.Sum(snapshot => snapshot.CandleCount)}")
                .Take(8)
                .ToList(),
            DominantSessions: snapshots
                .GroupBy(snapshot => snapshot.Session)
                .OrderByDescending(group => group.Sum(snapshot => snapshot.CandleCount))
                .Select(group => $"{group.Key}:{group.Sum(snapshot => snapshot.CandleCount)}")
                .Take(8)
                .ToList(),
            TopSnapshots: snapshots.Take(16).ToList(),
            Warnings: warnings.Distinct(StringComparer.Ordinal).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static RegimeDistributionReport BuildDistribution(
        IReadOnlyList<MarketRegimeSnapshot> snapshots,
        IReadOnlyList<string> warnings)
    {
        var total = Math.Max(1, snapshots.Sum(snapshot => snapshot.CandleCount));
        var entries = snapshots
            .Select(snapshot => new RegimeDistributionEntry(
                Symbol: snapshot.Symbol,
                Timeframe: snapshot.Timeframe,
                RegimeType: snapshot.RegimeType,
                Session: snapshot.Session,
                CandleCount: snapshot.CandleCount,
                Percentage: Math.Round(snapshot.CandleCount / (double)total, 4),
                AverageConfidence: snapshot.Confidence))
            .ToList();

        return new RegimeDistributionReport(
            ReportVersion: ReportVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            TotalCandles: snapshots.Sum(snapshot => snapshot.CandleCount),
            Entries: entries,
            Warnings: warnings.Distinct(StringComparer.Ordinal).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private StrategyRegimePerformanceReport BuildStrategyPerformance(
        IReadOnlyList<MarketRegimeSnapshot> snapshots,
        IReadOnlyList<string> inheritedWarnings)
    {
        var warnings = new List<string>(inheritedWarnings)
        {
            "Strategy-to-regime performance is foundation-level attribution based on StrategyResearchResult, pattern metadata, and classified feature distributions."
        };
        var results = LoadStrategyResults().Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)).ToList();
        var walkForwardAssessments = new WalkForwardValidationService(_storagePaths)
            .LoadReport()
            ?.Assessments
            .GroupBy(assessment => assessment.StrategyVariantId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(assessment => assessment.ValidationScore).First(), StringComparer.Ordinal)
            ?? [];
        var patterns = new StrategyPatternCatalog(_storagePaths)
            .LoadOrCreateCatalog()
            .ToDictionary(pattern => pattern.Id, pattern => pattern, StringComparer.OrdinalIgnoreCase);

        if (results.Count == 0)
        {
            warnings.Add("No StrategyResearchResult files found; run strategy research before regime performance analysis.");
        }

        var joined = results
            .SelectMany(result => MatchingSnapshots(result, snapshots)
                .Select(snapshot => new
                {
                    Result = result,
                    Snapshot = snapshot,
                    Compatibility = StrategyRegimeCompatibility(result, snapshot),
                    AdjustedFitness = AdjustedFitness(result, walkForwardAssessments)
                }))
            .ToList();

        var entries = joined
            .GroupBy(item => string.Join(
                    "|",
                    item.Result.Variant.Family,
                    item.Result.Variant.PatternId ?? "-",
                    item.Snapshot.RegimeType,
                    item.Snapshot.Session),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var family = first.Result.Variant.Family;
                var patternId = first.Result.Variant.PatternId ?? "-";
                var regimeType = first.Snapshot.RegimeType;
                var session = first.Snapshot.Session;
                patterns.TryGetValue(patternId, out var pattern);
                var averageFitness = group.Average(item => item.AdjustedFitness);
                var compatibility = group.Average(item => item.Compatibility);
                var confidence = group.Average(item => item.Snapshot.Confidence);
                var fit = Math.Clamp((averageFitness * 0.68) + (compatibility * 0.2) + (confidence * 0.12), 0, 1);

                return new StrategyRegimePerformanceEntry(
                    StrategyFamily: family,
                    PatternId: patternId,
                    PatternName: pattern?.Name ?? patternId,
                    RegimeType: regimeType,
                    Session: session,
                    VariantCount: group.Select(item => item.Result.Variant.VariantId).Distinct(StringComparer.Ordinal).Count(),
                    TotalTrades: group.Sum(item => item.Result.TradeCount),
                    AverageFitness: Math.Round(averageFitness, 4),
                    AverageWinrate: Math.Round(group.Average(item => item.Result.Fitness.Winrate), 4),
                    AverageRegimeConfidence: Math.Round(confidence, 4),
                    RegimeFitScore: Math.Round(fit, 4),
                    Status: fit >= 0.74 ? "strong" : fit <= 0.46 ? "weak" : "watch");
            })
            .OrderByDescending(entry => entry.RegimeFitScore)
            .ThenByDescending(entry => entry.TotalTrades)
            .ToList();

        return new StrategyRegimePerformanceReport(
            ReportVersion: ReportVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            StrategiesAnalyzed: results.Count,
            RegimeSnapshotsAnalyzed: snapshots.Count,
            Entries: entries,
            StrongRegimeMatches: BestRegimeMatches(entries),
            WeakRegimeMatches: entries
                .Where(entry => entry.Status == "weak")
                .OrderBy(entry => entry.RegimeFitScore)
                .Take(12)
                .Select(entry => $"{entry.StrategyFamily}/{entry.PatternName}:{entry.RegimeType}:{entry.Session}:fit={entry.RegimeFitScore:0.####}")
                .ToList(),
            PreferredSessions: SessionStrength(entries, descending: true),
            AvoidSessions: SessionStrength(entries, descending: false),
            VolatilityPreference: VolatilityPreference(entries),
            RegimeConsistencyScore: RegimeConsistency(entries),
            PreferredRegimes: RegimeStrength(entries, descending: true),
            AvoidedRegimes: RegimeStrength(entries, descending: false),
            RegimeSampleQuality: RegimeSampleQuality(entries),
            Warnings: warnings.Distinct(StringComparer.Ordinal).Take(30).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static IReadOnlyList<string> BestRegimeMatches(IReadOnlyList<StrategyRegimePerformanceEntry> entries)
    {
        var strong = entries
            .Where(entry => entry.Status == "strong")
            .Take(12)
            .Select(entry => $"{entry.StrategyFamily}/{entry.PatternName}:{entry.RegimeType}:{entry.Session}:fit={entry.RegimeFitScore:0.####}")
            .ToList();

        if (strong.Count > 0)
        {
            return strong;
        }

        return entries
            .Take(12)
            .Select(entry => $"{entry.StrategyFamily}/{entry.PatternName}:{entry.RegimeType}:{entry.Session}:fit={entry.RegimeFitScore:0.####},status={entry.Status}")
            .ToList();
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

    private static IReadOnlyList<MarketRegimeSnapshot> MatchingSnapshots(
        StrategyResearchResult result,
        IReadOnlyList<MarketRegimeSnapshot> snapshots)
    {
        var matches = snapshots
            .Where(snapshot => result.SymbolsProcessed.Contains(snapshot.Symbol, StringComparer.OrdinalIgnoreCase)
                && result.TimeframesProcessed.Contains(snapshot.Timeframe, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 0 ? snapshots.Take(12).ToList() : matches;
    }

    private static double StrategyRegimeCompatibility(StrategyResearchResult result, MarketRegimeSnapshot snapshot)
    {
        var family = result.Variant.Family;
        var pattern = result.Variant.PatternId ?? string.Empty;
        var regime = snapshot.RegimeType;
        var session = snapshot.Session;
        var score = 0.62;

        if ((family.Contains("breakout", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("breakout", StringComparison.OrdinalIgnoreCase))
            && regime is "breakout" or "trending" or "high_volatility")
        {
            score += 0.2;
        }

        if ((family.Contains("trend", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("pullback", StringComparison.OrdinalIgnoreCase))
            && regime == "trending")
        {
            score += 0.18;
        }

        if ((family.Contains("mean_reversion", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("reversion", StringComparison.OrdinalIgnoreCase))
            && regime is "ranging" or "low_volatility")
        {
            score += 0.2;
        }

        if (pattern.Contains("liquidity_sweep", StringComparison.OrdinalIgnoreCase)
            && regime is "high_volatility" or "news_like_volatility")
        {
            score += 0.12;
        }

        if (result.Variant.UseVolatilityFilter && regime is "high_volatility" or "breakout")
        {
            score += 0.08;
        }

        if (result.Variant.SessionFilter is not null
            && session.Contains(result.Variant.SessionFilter.Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase))
        {
            score += 0.06;
        }

        if ((family.Contains("mean_reversion", StringComparison.OrdinalIgnoreCase) && regime is "breakout" or "news_like_volatility")
            || (family.Contains("breakout", StringComparison.OrdinalIgnoreCase) && regime == "low_volatility"))
        {
            score -= 0.16;
        }

        return Math.Round(Math.Clamp(score, 0.1, 1), 4);
    }

    private static double AdjustedFitness(
        StrategyResearchResult result,
        IReadOnlyDictionary<string, WalkForwardStrategyAssessment> walkForwardAssessments)
    {
        var score = result.Fitness.Score;
        if (result.Fitness.Winrate >= 0.98 && result.TradeCount >= 500)
        {
            score -= 0.22;
        }

        if (result.LossCount <= 1 && result.TradeCount >= 100)
        {
            score -= 0.14;
        }

        if (result.MaxDrawdown > -1 && result.TradeCount >= 100)
        {
            score -= 0.05;
        }

        if (walkForwardAssessments.TryGetValue(result.Variant.VariantId, out var assessment))
        {
            score -= assessment.RealismPenalty * 0.2;
            score -= assessment.OverfitRisk * 0.2;
            score -= assessment.DegradationScore * 0.15;
            if (assessment.StrategyConfidence is "overfit_suspected" or "rejected" or "unstable")
            {
                score -= 0.18;
            }

            if (assessment.Robust)
            {
                score += 0.06;
            }
        }

        return Math.Round(Math.Clamp(score, 0, 1), 4);
    }

    private static IReadOnlyList<string> SessionStrength(
        IReadOnlyList<StrategyRegimePerformanceEntry> entries,
        bool descending)
    {
        var query = entries
            .GroupBy(entry => entry.Session, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Session = group.Key,
                Score = group.Average(entry => entry.RegimeFitScore),
                Count = group.Sum(entry => entry.VariantCount)
            });
        query = descending
            ? query.OrderByDescending(item => item.Score).ThenByDescending(item => item.Count)
            : query.OrderBy(item => item.Score).ThenByDescending(item => item.Count);

        return query
            .Take(6)
            .Select(item => $"{item.Session}:avg_fit={item.Score:0.####},variants={item.Count}")
            .ToList();
    }

    private static IReadOnlyList<string> VolatilityPreference(IReadOnlyList<StrategyRegimePerformanceEntry> entries)
    {
        return entries
            .Where(entry => entry.RegimeType is "high_volatility" or "low_volatility" or "news_like_volatility" or "breakout")
            .GroupBy(entry => entry.RegimeType, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}:avg_fit={group.Average(entry => entry.RegimeFitScore):0.####},variants={group.Sum(entry => entry.VariantCount)}")
            .OrderByDescending(line => line)
            .ToList();
    }

    private static IReadOnlyList<string> RegimeStrength(
        IReadOnlyList<StrategyRegimePerformanceEntry> entries,
        bool descending)
    {
        var query = entries
            .GroupBy(entry => entry.RegimeType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Regime = group.Key,
                Score = group.Average(entry => entry.RegimeFitScore),
                Count = group.Sum(entry => entry.VariantCount)
            });
        query = descending
            ? query.OrderByDescending(item => item.Score).ThenByDescending(item => item.Count)
            : query.OrderBy(item => item.Score).ThenByDescending(item => item.Count);

        return query
            .Take(8)
            .Select(item => $"{item.Regime}:avg_fit={item.Score:0.####},variants={item.Count}")
            .ToList();
    }

    private static double RegimeSampleQuality(IReadOnlyList<StrategyRegimePerformanceEntry> entries)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        var regimeCount = entries.Select(entry => entry.RegimeType).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var sessionCount = entries.Select(entry => entry.Session).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return Math.Round(Math.Clamp((regimeCount / 5.0 * 0.65) + (sessionCount / 4.0 * 0.35), 0, 1), 4);
    }

    private static double RegimeConsistency(IReadOnlyList<StrategyRegimePerformanceEntry> entries)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        var byFamily = entries
            .GroupBy(entry => entry.StrategyFamily, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var average = group.Average(entry => entry.RegimeFitScore);
                var variance = group.Average(entry => Math.Pow(entry.RegimeFitScore - average, 2));
                return Math.Clamp(1 - Math.Sqrt(variance), 0, 1);
            })
            .ToList();

        return Math.Round(byFamily.Average(), 4);
    }

    private void WriteSnapshotMemory(IReadOnlyList<MarketRegimeSnapshot> snapshots)
    {
        File.WriteAllLines(
            SnapshotMemoryPath,
            snapshots.Select(snapshot => JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions)));
    }

    private static T? LoadReport<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return default;
        }
    }
}
