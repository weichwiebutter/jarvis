using System.Text.Json;

namespace Hermes.Runtime;

public sealed class HistoricalDataImportService
{
    private const string ImportSource = "hermes_historical_data_import";

    private static readonly string[] SupportedSymbols = ["XAUUSD", "EURUSD", "GER40"];
    private static readonly string[] SupportedTimeframes = ["H4", "H1", "M15", "M5"];

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public HistoricalDataImportService(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public (MarketDataImportJob Job, int CandleCount, IReadOnlyList<string> OutputPaths) ImportDemoHistoricalCandles()
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var job = new MarketDataImportJob(
            ImportId: $"market_data_demo_{requestedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            Symbols: SupportedSymbols,
            Timeframes: SupportedTimeframes,
            Source: "demo_fixture_ctrader_compatible",
            RequestedAtUtc: requestedAtUtc,
            DemoData: true);

        PublishHistoricalImportStarted(job);

        var outputPaths = new List<string>();
        var candleCount = 0;
        var candlesRoot = Path.Combine(_storagePaths.Root, "market_data", "candles");

        foreach (var symbol in SupportedSymbols)
        {
            var symbolDirectory = Path.Combine(candlesRoot, symbol);
            Directory.CreateDirectory(symbolDirectory);

            foreach (var timeframe in SupportedTimeframes)
            {
                var candles = CreateDemoCandles(symbol, timeframe, requestedAtUtc).ToList();
                var outputPath = Path.Combine(symbolDirectory, $"{timeframe}.candles.jsonl");
                WriteJsonl(outputPath, candles);

                outputPaths.Add(outputPath);
                candleCount += candles.Count;
            }
        }

        PublishHistoricalImportCompleted(job, candleCount, outputPaths);
        return (job, candleCount, outputPaths);
    }

    private static IEnumerable<MarketDataCandle> CreateDemoCandles(
        string symbol,
        string timeframe,
        DateTimeOffset referenceUtc)
    {
        const int candleCount = 12;
        var interval = TimeframeInterval(timeframe);
        var startUtc = referenceUtc - TimeSpan.FromTicks(interval.Ticks * (candleCount - 1));
        var basePrice = BasePrice(symbol);
        var range = CandleRange(symbol, timeframe);
        var trend = range * 0.18;

        for (var index = 0; index < candleCount; index++)
        {
            var timestampUtc = startUtc + TimeSpan.FromTicks(interval.Ticks * index);
            var direction = index % 2 == 0 ? 1.0 : -0.6;
            var wave = ((index % 4) - 1.5) * range * 0.18;
            var open = basePrice + (trend * index) + wave;
            var close = open + (range * direction * 0.22);
            var high = Math.Max(open, close) + (range * (0.22 + (index % 3 * 0.06)));
            var low = Math.Min(open, close) - (range * (0.18 + (index % 2 * 0.05)));
            var volume = BaseVolume(symbol) + (index * VolumeStep(symbol)) + (timeframe.Length * 7);

            yield return new MarketDataCandle(
                TimestampUtc: timestampUtc,
                Open: RoundPrice(symbol, open),
                High: RoundPrice(symbol, high),
                Low: RoundPrice(symbol, low),
                Close: RoundPrice(symbol, close),
                Volume: Math.Round(volume, 2),
                Symbol: symbol,
                Timeframe: timeframe);
        }
    }

    private static TimeSpan TimeframeInterval(string timeframe) =>
        timeframe switch
        {
            "H4" => TimeSpan.FromHours(4),
            "H1" => TimeSpan.FromHours(1),
            "M15" => TimeSpan.FromMinutes(15),
            "M5" => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(5)
        };

    private static double BasePrice(string symbol) =>
        symbol switch
        {
            "XAUUSD" => 2392.40,
            "EURUSD" => 1.08420,
            "GER40" => 18420.0,
            _ => 100.0
        };

    private static double CandleRange(string symbol, string timeframe)
    {
        var baseRange = symbol switch
        {
            "XAUUSD" => 3.2,
            "EURUSD" => 0.00062,
            "GER40" => 38.0,
            _ => 1.0
        };

        var timeframeFactor = timeframe switch
        {
            "H4" => 2.4,
            "H1" => 1.5,
            "M15" => 0.8,
            "M5" => 0.45,
            _ => 1.0
        };

        return baseRange * timeframeFactor;
    }

    private static double BaseVolume(string symbol) =>
        symbol switch
        {
            "XAUUSD" => 1200,
            "EURUSD" => 900,
            "GER40" => 700,
            _ => 100
        };

    private static double VolumeStep(string symbol) =>
        symbol switch
        {
            "XAUUSD" => 18,
            "EURUSD" => 11,
            "GER40" => 9,
            _ => 5
        };

    private static double RoundPrice(string symbol, double price) =>
        symbol switch
        {
            "EURUSD" => Math.Round(price, 5),
            "GER40" => Math.Round(price, 1),
            _ => Math.Round(price, 2)
        };

    private static void WriteJsonl<T>(string path, IEnumerable<T> rows)
    {
        File.WriteAllLines(
            path,
            rows.Select(row => JsonSerializer.Serialize(row, JsonDefaults.WriteOptions)));
    }

    private void PublishHistoricalImportStarted(MarketDataImportJob job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.HistoricalImportStarted,
            ImportSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Historical market data import started. Demo fixture data only.",
                job.ImportId,
                job.Source,
                job.Symbols,
                job.Timeframes,
                job.RequestedAtUtc,
                job.DemoData,
                candlesRoot = Path.Combine(_storagePaths.Root, "market_data", "candles"),
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishHistoricalImportCompleted(
        MarketDataImportJob job,
        int candleCount,
        IReadOnlyList<string> outputPaths)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.HistoricalImportCompleted,
            ImportSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Historical market data import completed. Files are local JSONL candles for replay and feature preparation.",
                job.ImportId,
                job.Source,
                candleCount,
                outputPaths,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }
}
