using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CTraderTrendbarImporter
{
    private readonly StoragePaths _storagePaths;

    public CTraderTrendbarImporter(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public CTraderTrendbarImportResult ImportStubCandles(
        CTraderHistoricalDataRequest request,
        IReadOnlyList<MarketDataCandle> candles)
    {
        var downloadId = $"ctrader_openapi_stub_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var symbol = NormalizeSegment(request.Symbol);
        var timeframe = NormalizeSegment(request.Timeframe);
        var outputDirectory = Path.Combine(_storagePaths.Root, "market_data", "candles", symbol, timeframe);
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, $"{downloadId}.candles.jsonl");
        File.WriteAllLines(
            outputPath,
            candles.Select(candle => JsonSerializer.Serialize(candle, JsonDefaults.WriteOptions)));

        var orderedCandles = candles.OrderBy(candle => candle.TimestampUtc).ToList();
        return new CTraderTrendbarImportResult(
            DownloadId: downloadId,
            Symbol: symbol,
            Timeframe: timeframe,
            OutputPath: outputPath,
            CandleCount: orderedCandles.Count,
            FromUtc: orderedCandles.Count > 0 ? orderedCandles[0].TimestampUtc : null,
            ToUtc: orderedCandles.Count > 0 ? orderedCandles[^1].TimestampUtc : null,
            StubData: true);
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized) ? "UNKNOWN" : normalized;
    }
}
