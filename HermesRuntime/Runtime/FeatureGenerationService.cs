using System.Text.Json;

namespace Hermes.Runtime;

public sealed class FeatureGenerationService
{
    private const string GenerationSource = "hermes_feature_generation";

    private static readonly string[] SupportedSymbols = ["XAUUSD", "EURUSD", "GER40"];
    private static readonly string[] SupportedTimeframes = ["M5", "M15", "H1", "H4"];

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public FeatureGenerationService(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public (FeatureGenerationJob Job, int FeatureCount, string OutputPath) GenerateFromMarketData()
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var sourceRoot = Path.Combine(_storagePaths.Root, "market_data", "candles");
        var job = new FeatureGenerationJob(
            GenerationId: $"feature_generation_{requestedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            Symbols: SupportedSymbols,
            Timeframes: SupportedTimeframes,
            SourceRoot: sourceRoot,
            RequestedAtUtc: requestedAtUtc,
            DemoData: true);

        PublishFeatureGenerationStarted(job);

        var features = new List<GeneratedFeatureVector>();
        foreach (var symbol in SupportedSymbols)
        {
            foreach (var timeframe in SupportedTimeframes)
            {
                var candles = ReadCandles(symbol, timeframe).ToList();
                features.AddRange(CreateFeatures(candles));
            }
        }

        var outputDirectory = Path.Combine(_storagePaths.Root, "exports", "features");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, $"{job.GenerationId}.features.jsonl");
        WriteJsonl(outputPath, features);

        PublishFeatureGenerationCompleted(job, outputPath, features.Count);
        return (job, features.Count, outputPath);
    }

    private IEnumerable<MarketDataCandle> ReadCandles(string symbol, string timeframe)
    {
        foreach (var path in FindCandleFiles(symbol, timeframe))
        {
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MarketDataCandle? candle;
                try
                {
                    candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (candle is not null)
                {
                    yield return candle;
                }
            }
        }
    }

    private IEnumerable<string> FindCandleFiles(string symbol, string timeframe)
    {
        var symbolRoot = Path.Combine(_storagePaths.Root, "market_data", "candles", symbol);
        var legacyPath = Path.Combine(symbolRoot, $"{timeframe}.candles.jsonl");
        if (File.Exists(legacyPath))
        {
            yield return legacyPath;
        }

        var timeframeDirectory = Path.Combine(symbolRoot, timeframe);
        if (!Directory.Exists(timeframeDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(timeframeDirectory, "*.jsonl", SearchOption.TopDirectoryOnly)
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            if (!path.Equals(legacyPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<GeneratedFeatureVector> CreateFeatures(IReadOnlyList<MarketDataCandle> candles)
    {
        var orderedCandles = candles
            .OrderBy(candle => candle.TimestampUtc)
            .ToList();

        for (var index = 0; index < orderedCandles.Count; index++)
        {
            var candle = orderedCandles[index];
            var previousClose = index > 0 ? orderedCandles[index - 1].Close : candle.Open;
            var simpleReturn = previousClose == 0
                ? 0
                : (candle.Close - previousClose) / previousClose;
            var candleRange = Math.Max(0, candle.High - candle.Low);
            var bodySize = Math.Abs(candle.Close - candle.Open);
            var direction = candle.Close > candle.Open
                ? "up"
                : candle.Close < candle.Open
                    ? "down"
                    : "flat";

            yield return new GeneratedFeatureVector(
                TimestampUtc: candle.TimestampUtc,
                Symbol: candle.Symbol,
                Timeframe: candle.Timeframe,
                Close: RoundPrice(candle.Symbol, candle.Close),
                SimpleReturn: Math.Round(simpleReturn, 8),
                CandleRange: RoundPrice(candle.Symbol, candleRange),
                BodySize: RoundPrice(candle.Symbol, bodySize),
                Direction: direction,
                MockSession: MockSession(candle.TimestampUtc),
                MockRegime: MockRegime(simpleReturn, candleRange, candle.Close),
                MockSignalScore: MockSignalScore(simpleReturn, candleRange, bodySize, candle.Close));
        }
    }

    private static string MockSession(DateTimeOffset timestampUtc)
    {
        var hour = timestampUtc.UtcDateTime.Hour;
        return hour switch
        {
            >= 7 and < 12 => "london",
            >= 12 and < 17 => "london_new_york_overlap",
            >= 17 and < 21 => "new_york",
            _ => "off_session"
        };
    }

    private static string MockRegime(double simpleReturn, double candleRange, double close)
    {
        var rangeRatio = close == 0 ? 0 : candleRange / Math.Abs(close);
        if (rangeRatio > 0.003)
        {
            return "high_volatility";
        }

        if (simpleReturn > 0.0004)
        {
            return "trend_up";
        }

        if (simpleReturn < -0.0004)
        {
            return "trend_down";
        }

        return "range";
    }

    private static double MockSignalScore(
        double simpleReturn,
        double candleRange,
        double bodySize,
        double close)
    {
        var rangeRatio = close == 0 ? 0 : candleRange / Math.Abs(close);
        var bodyRatio = candleRange == 0 ? 0 : bodySize / candleRange;
        var score = 0.35
            + Math.Min(0.25, Math.Abs(simpleReturn) * 80)
            + Math.Min(0.25, rangeRatio * 80)
            + Math.Min(0.15, bodyRatio * 0.15);

        return Math.Round(Math.Clamp(score, 0, 1), 4);
    }

    private static double RoundPrice(string symbol, double value) =>
        symbol switch
        {
            "EURUSD" => Math.Round(value, 5),
            "GER40" => Math.Round(value, 1),
            _ => Math.Round(value, 2)
        };

    private static void WriteJsonl<T>(string path, IEnumerable<T> rows)
    {
        File.WriteAllLines(
            path,
            rows.Select(row => JsonSerializer.Serialize(row, JsonDefaults.WriteOptions)));
    }

    private void PublishFeatureGenerationStarted(FeatureGenerationJob job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.FeatureGenerationStarted,
            GenerationSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Feature generation from local historical candle data started.",
                job.GenerationId,
                job.SourceRoot,
                job.Symbols,
                job.Timeframes,
                job.RequestedAtUtc,
                job.DemoData,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishFeatureGenerationCompleted(
        FeatureGenerationJob job,
        string outputPath,
        int featureCount)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.FeatureGenerationCompleted,
            GenerationSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Feature generation from local historical candle data completed.",
                job.GenerationId,
                outputPath,
                featureCount,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }
}
