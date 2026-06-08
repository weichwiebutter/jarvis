using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MarketDataSource(
    string SourceId,
    string RootPath,
    bool Exists,
    DateTimeOffset ScannedAtUtc);

public sealed record MarketDataFile(
    string Asset,
    string Timeframe,
    string Source,
    string FilePath,
    DateTimeOffset? FirstTimestamp,
    DateTimeOffset? LastTimestamp,
    int CandleCount,
    int MissingCandles,
    int DuplicateCandles,
    int InvalidCandles,
    string Timezone,
    bool SpreadAvailable,
    bool VolumeAvailable,
    IReadOnlyList<string> Warnings);

public sealed record MarketDataAvailability(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<MarketDataSource> Sources,
    IReadOnlyList<MarketDataFile> Files,
    IReadOnlyList<string> AssetsAvailable,
    IReadOnlyList<string> DataGaps,
    bool XauusdAvailable,
    bool EurusdAvailable,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record MarketDataQualityReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Asset,
    int FileCount,
    int CandleCount,
    int MissingCandles,
    int DuplicateCandles,
    int InvalidCandles,
    string QualityHealth,
    IReadOnlyList<string> TimeframesAvailable,
    IReadOnlyList<string> DataGaps,
    IReadOnlyList<MarketDataFile> Files,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record NormalizedCandle(
    DateTimeOffset TimestampUtc,
    double Open,
    double High,
    double Low,
    double Close,
    double? Volume,
    double? Bid,
    double? Ask,
    double? Spread,
    string Asset,
    string Timeframe,
    string Source,
    string Timezone);

public sealed record MarketDataNormalizationResult(
    string Asset,
    int FilesProcessed,
    int CandlesWritten,
    int InvalidCandles,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> DataGaps,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class MarketDataAvailabilityService
{
    private static readonly string[] SupportedAssets = ["XAUUSD", "GOLD", "EURUSD"];
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public MarketDataAvailabilityService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string ReportsDirectory => Path.Combine(_storagePaths.Root, "reports", "market_data");
    public string AvailabilityPath => Path.Combine(ReportsDirectory, "market_data_availability.json");
    public string QualityPath => Path.Combine(ReportsDirectory, "market_data_quality.json");
    public string NormalizedDirectory => Path.Combine(_storagePaths.Root, "market_data", "normalized");

    public MarketDataAvailability Scan()
    {
        var scannedAt = DateTimeOffset.UtcNow;
        var sources = ScanRoots(scannedAt).ToList();
        var files = sources
            .Where(source => source.Exists)
            .SelectMany(source => Directory.EnumerateFiles(source.RootPath, "*.csv", SearchOption.AllDirectories)
                .Select(path => AnalyzeCsv(source.SourceId, path)))
            .Where(file => file is not null)
            .Select(file => file!)
            .Concat(AnalyzeCTraderJsonlCandles())
            .OrderBy(file => file.Asset, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.Timeframe, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assetsAvailable = files
            .Where(file => file.CandleCount > 0)
            .Select(file => CanonicalAsset(file.Asset))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dataGaps = new List<string>();
        if (!assetsAvailable.Contains("XAUUSD", StringComparer.OrdinalIgnoreCase)) dataGaps.Add("market_data_missing_for_asset:XAUUSD");
        if (!assetsAvailable.Contains("EURUSD", StringComparer.OrdinalIgnoreCase)) dataGaps.Add("market_data_missing_for_asset:EURUSD");

        var report = new MarketDataAvailability(
            ReportVersion: "market_data_availability_v1",
            UpdatedAtUtc: scannedAt,
            Sources: sources,
            Files: files,
            AssetsAvailable: assetsAvailable,
            DataGaps: dataGaps,
            XauusdAvailable: assetsAvailable.Contains("XAUUSD", StringComparer.OrdinalIgnoreCase),
            EurusdAvailable: assetsAvailable.Contains("EURUSD", StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(ReportsDirectory);
        File.WriteAllText(AvailabilityPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public MarketDataQualityReport BuildQuality(string asset)
    {
        var normalizedAsset = CanonicalAsset(asset);
        var availability = Scan();
        var files = availability.Files
            .Where(file => CanonicalAsset(file.Asset).Equals(normalizedAsset, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var dataGaps = new List<string>();
        if (files.Count == 0) dataGaps.Add($"market_data_missing_for_asset:{normalizedAsset}");
        if (files.Sum(file => file.CandleCount) < 500) dataGaps.Add($"market_data_insufficient_candles:{normalizedAsset}");
        if (!files.Any(file => file.Timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase))) dataGaps.Add($"market_data_timeframe_missing:{normalizedAsset}:M5");

        var invalid = files.Sum(file => file.InvalidCandles);
        var missing = files.Sum(file => file.MissingCandles);
        var duplicates = files.Sum(file => file.DuplicateCandles);
        var candles = files.Sum(file => file.CandleCount);
        var health = files.Count == 0 ? "missing" : dataGaps.Count > 0 ? "needs_more_data" : invalid > candles * 0.02 ? "quality_warning" : "ok";
        var report = new MarketDataQualityReport(
            ReportVersion: "market_data_quality_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Asset: normalizedAsset,
            FileCount: files.Count,
            CandleCount: candles,
            MissingCandles: missing,
            DuplicateCandles: duplicates,
            InvalidCandles: invalid,
            QualityHealth: health,
            TimeframesAvailable: files.Select(file => file.Timeframe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            DataGaps: dataGaps,
            Files: files,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        Directory.CreateDirectory(ReportsDirectory);
        File.WriteAllText(QualityPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public MarketDataNormalizationResult Normalize(string asset)
    {
        var normalizedAsset = CanonicalAsset(asset);
        var quality = BuildQuality(normalizedAsset);
        var outputPaths = new List<string>();
        var candlesWritten = 0;
        var invalid = 0;
        foreach (var file in quality.Files)
        {
            var readResult = ReadCsvCandles(file.Source, file.FilePath, normalizedAsset, file.Timeframe);
            var candles = readResult.Candles;
            var invalidRows = readResult.InvalidRows;
            invalid += invalidRows;
            if (candles.Count == 0) continue;
            var directory = Path.Combine(NormalizedDirectory, normalizedAsset, file.Timeframe);
            Directory.CreateDirectory(directory);
            var outputPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file.FilePath)}.normalized.jsonl");
            File.WriteAllLines(outputPath, candles.Select(candle => JsonSerializer.Serialize(candle, JsonDefaults.WriteOptions)));
            outputPaths.Add(outputPath);
            candlesWritten += candles.Count;
        }

        var result = new MarketDataNormalizationResult(
            Asset: normalizedAsset,
            FilesProcessed: quality.Files.Count,
            CandlesWritten: candlesWritten,
            InvalidCandles: invalid,
            OutputPaths: outputPaths,
            DataGaps: quality.DataGaps,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        return result;
    }

    public MarketDataAvailability? LoadAvailability()
    {
        if (!File.Exists(AvailabilityPath)) return null;
        return JsonSerializer.Deserialize<MarketDataAvailability>(File.ReadAllText(AvailabilityPath), JsonDefaults.SnapshotReadOptions);
    }

    public IReadOnlyList<string> ExplainGap(string asset)
    {
        var quality = BuildQuality(asset);
        var reasons = new List<string>(quality.DataGaps);
        if (quality.FileCount == 0)
        {
            reasons.Add("No matching CSV files found in configured scan roots.");
            reasons.Add("No existing cTrader candle JSONL files found for this asset under market_data/candles.");
            reasons.Add($"Import command: dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol {quality.Asset} --timeframe M5 --from YYYY-MM-DD --to YYYY-MM-DD");
            reasons.Add($"Alias: dotnet run --project ./cli/Hermes.Cli.csproj -- import-ctrader-history --asset {quality.Asset} --timeframe M5 --from YYYY-MM-DD --to YYYY-MM-DD");
            reasons.Add($"Scan roots: {string.Join(", ", ScanRoots(DateTimeOffset.UtcNow).Select(source => source.RootPath))}");
        }
        else
        {
            reasons.Add($"files={quality.FileCount}, candles={quality.CandleCount}, timeframes={string.Join(",", quality.TimeframesAvailable)}");
        }
        return reasons;
    }

    public bool HasUsableScalpingData(string asset, out IReadOnlyList<string> dataGaps, out int candleCount)
    {
        var quality = BuildQuality(asset);
        candleCount = quality.CandleCount;
        dataGaps = quality.DataGaps;
        return quality.DataGaps.Count == 0 && quality.CandleCount >= 500 && quality.TimeframesAvailable.Contains("M5", StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<MarketDataSource> ScanRoots(DateTimeOffset scannedAt)
    {
        var roots = new[]
        {
            Path.Combine(_storagePaths.Root, "market_data"),
            Path.Combine(_storagePaths.Root, "data"),
            Path.Combine(_storagePaths.Root, "import"),
            Path.Combine(_runtimeRoot, "data"),
            Path.Combine(_runtimeRoot, "import")
        };
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return new MarketDataSource(SourceId: SourceId(root), RootPath: root, Exists: Directory.Exists(root), ScannedAtUtc: scannedAt);
        }
    }

    private IEnumerable<MarketDataFile> AnalyzeCTraderJsonlCandles()
    {
        var root = Path.Combine(_storagePaths.Root, "market_data", "candles");
        if (!Directory.Exists(root)) yield break;
        foreach (var file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).OrderBy(path => path))
        {
            var relative = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relative.Length < 3) continue;
            var asset = CanonicalAsset(relative[0]);
            if (!SupportedAssets.Contains(asset, StringComparer.OrdinalIgnoreCase)) continue;
            var timeframe = relative[1].ToUpperInvariant();
            var candles = ReadJsonlCandles(file).ToList();
            var ordered = candles.OrderBy(candle => candle.TimestampUtc).ToList();
            var duplicateCount = candles.GroupBy(candle => candle.TimestampUtc).Sum(group => Math.Max(0, group.Count() - 1));
            yield return new MarketDataFile(
                Asset: asset,
                Timeframe: timeframe,
                Source: "ctrader_candles_jsonl",
                FilePath: file,
                FirstTimestamp: ordered.Count > 0 ? ordered[0].TimestampUtc : null,
                LastTimestamp: ordered.Count > 0 ? ordered[^1].TimestampUtc : null,
                CandleCount: ordered.Count,
                MissingCandles: CountMissing(ordered.Select(ToNormalized).ToList(), timeframe),
                DuplicateCandles: duplicateCount,
                InvalidCandles: 0,
                Timezone: "UTC",
                SpreadAvailable: false,
                VolumeAvailable: ordered.Any(candle => candle.Volume > 0),
                Warnings: []);
        }
    }

    private static IEnumerable<MarketDataCandle> ReadJsonlCandles(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            MarketDataCandle? candle;
            try
            {
                candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (candle is not null && candle.High >= candle.Low && candle.Open > 0 && candle.High > 0 && candle.Low > 0 && candle.Close > 0)
            {
                yield return candle;
            }
        }
    }

    private static NormalizedCandle ToNormalized(MarketDataCandle candle) => new(
        TimestampUtc: candle.TimestampUtc,
        Open: candle.Open,
        High: candle.High,
        Low: candle.Low,
        Close: candle.Close,
        Volume: candle.Volume,
        Bid: null,
        Ask: null,
        Spread: null,
        Asset: candle.Symbol,
        Timeframe: candle.Timeframe,
        Source: "ctrader_candles_jsonl",
        Timezone: "UTC");

    private MarketDataFile? AnalyzeCsv(string sourceId, string path)
    {
        var fileName = Path.GetFileName(path);
        var asset = DetectAsset(fileName);
        if (asset is null) return null;
        var timeframe = DetectTimeframe(fileName);
        var readResult = ReadCsvCandles(sourceId, path, CanonicalAsset(asset), timeframe);
        var candles = readResult.Candles;
        var invalidRows = readResult.InvalidRows;
        var duplicateCount = candles.GroupBy(candle => candle.TimestampUtc).Sum(group => Math.Max(0, group.Count() - 1));
        var ordered = candles.OrderBy(candle => candle.TimestampUtc).ToList();
        var missing = CountMissing(ordered, timeframe);
        var warnings = new List<string>();
        if (candles.Count == 0) warnings.Add("no_valid_candles");
        if (timeframe == "UNKNOWN") warnings.Add("timeframe_not_detected");
        var header = File.ReadLines(path).FirstOrDefault() ?? string.Empty;
        var headers = SplitCsvLine(header, DetectDelimiter(header)).Select(NormalizeHeader).ToList();
        return new MarketDataFile(
            Asset: CanonicalAsset(asset),
            Timeframe: timeframe,
            Source: sourceId,
            FilePath: path,
            FirstTimestamp: ordered.Count > 0 ? ordered[0].TimestampUtc : null,
            LastTimestamp: ordered.Count > 0 ? ordered[^1].TimestampUtc : null,
            CandleCount: ordered.Count,
            MissingCandles: missing,
            DuplicateCandles: duplicateCount,
            InvalidCandles: invalidRows,
            Timezone: "UTC_assumed_or_parsed_offset",
            SpreadAvailable: headers.Any(headerName => headerName.Contains("spread", StringComparison.OrdinalIgnoreCase)) || (headers.Contains("bid") && headers.Contains("ask")),
            VolumeAvailable: headers.Any(headerName => headerName is "volume" or "tickvolume"),
            Warnings: warnings);
    }

    private static (List<NormalizedCandle> Candles, int InvalidRows) ReadCsvCandles(string source, string path, string asset, string timeframe)
    {
        var candles = new List<NormalizedCandle>();
        var invalidRows = 0;
        if (!File.Exists(path)) return (candles, invalidRows);
        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(header)) return (candles, invalidRows);
        var delimiter = DetectDelimiter(header);
        var headers = SplitCsvLine(header, delimiter).Select(NormalizeHeader).ToList();
        var map = BuildColumnMap(headers);
        if (!map.ContainsKey("time") || !map.ContainsKey("open") || !map.ContainsKey("high") || !map.ContainsKey("low") || !map.ContainsKey("close")) return (candles, invalidRows);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line, delimiter);
            if (!TryReadCandle(fields, map, source, asset, timeframe, out var candle))
            {
                invalidRows++;
                continue;
            }
            candles.Add(candle);
        }

        return (candles, invalidRows);
    }

    private static bool TryReadCandle(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> map, string source, string asset, string timeframe, out NormalizedCandle candle)
    {
        candle = default!;
        if (!TryGetField(fields, map["time"], out var timeText) || !TryParseTimestamp(timeText, out var timestamp)) return false;
        if (!TryGetDouble(fields, map["open"], out var open)) return false;
        if (!TryGetDouble(fields, map["high"], out var high)) return false;
        if (!TryGetDouble(fields, map["low"], out var low)) return false;
        if (!TryGetDouble(fields, map["close"], out var close)) return false;
        if (high < low || open <= 0 || high <= 0 || low <= 0 || close <= 0) return false;
        var volume = map.TryGetValue("volume", out var volumeIndex) && TryGetDouble(fields, volumeIndex, out var volumeValue) ? volumeValue : (double?)null;
        var bid = map.TryGetValue("bid", out var bidIndex) && TryGetDouble(fields, bidIndex, out var bidValue) ? bidValue : (double?)null;
        var ask = map.TryGetValue("ask", out var askIndex) && TryGetDouble(fields, askIndex, out var askValue) ? askValue : (double?)null;
        var spread = map.TryGetValue("spread", out var spreadIndex) && TryGetDouble(fields, spreadIndex, out var spreadValue) ? spreadValue : ask is not null && bid is not null ? ask - bid : null;
        candle = new NormalizedCandle(timestamp, open, high, low, close, volume, bid, ask, spread, asset, timeframe, source, "UTC_assumed_or_parsed_offset");
        return true;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            if (header is "time" or "timestamp" or "date" or "datetime" or "timeutc" or "timestamputc" or "dateutc" or "opentime") map.TryAdd("time", index);
            else if (header is "open" or "bidopen") map.TryAdd("open", index);
            else if (header is "high" or "bidhigh") map.TryAdd("high", index);
            else if (header is "low" or "bidlow") map.TryAdd("low", index);
            else if (header is "close" or "bidclose") map.TryAdd("close", index);
            else if (header is "volume" or "tickvolume") map.TryAdd("volume", index);
            else if (header is "bid") map.TryAdd("bid", index);
            else if (header is "ask") map.TryAdd("ask", index);
            else if (header is "spread") map.TryAdd("spread", index);
        }
        return map;
    }

    private static int CountMissing(IReadOnlyList<NormalizedCandle> candles, string timeframe)
    {
        var step = timeframe switch { "M1" => TimeSpan.FromMinutes(1), "M5" => TimeSpan.FromMinutes(5), "M15" => TimeSpan.FromMinutes(15), "M30" => TimeSpan.FromMinutes(30), "H1" => TimeSpan.FromHours(1), "H4" => TimeSpan.FromHours(4), "D1" => TimeSpan.FromDays(1), _ => TimeSpan.Zero };
        if (step == TimeSpan.Zero || candles.Count < 2) return 0;
        var missing = 0;
        for (var index = 1; index < candles.Count; index++)
        {
            var gap = candles[index].TimestampUtc - candles[index - 1].TimestampUtc;
            if (gap > step) missing += Math.Max(0, (int)Math.Round(gap.TotalSeconds / step.TotalSeconds) - 1);
        }
        return missing;
    }

    private static string? DetectAsset(string fileName)
    {
        var normalized = NormalizeHeader(fileName);
        if (normalized.Contains("xauusd", StringComparison.OrdinalIgnoreCase) || normalized.Contains("gold", StringComparison.OrdinalIgnoreCase)) return "XAUUSD";
        if (normalized.Contains("eurusd", StringComparison.OrdinalIgnoreCase)) return "EURUSD";
        return null;
    }

    private static string DetectTimeframe(string fileName)
    {
        var normalized = NormalizeHeader(fileName);
        foreach (var timeframe in new[] { "M1", "M5", "M15", "M30", "H1", "H4", "D1" })
        {
            if (normalized.Contains(timeframe.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) return timeframe;
        }
        return "UNKNOWN";
    }

    private static string CanonicalAsset(string asset) => asset.Trim().Equals("GOLD", StringComparison.OrdinalIgnoreCase) ? "XAUUSD" : asset.Trim().ToUpperInvariant();
    private static string SourceId(string root) => root.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_').Trim('_').ToLowerInvariant();
    private static char DetectDelimiter(string headerLine) => new[] { ',', ';', '\t' }.OrderByDescending(candidate => SplitCsvLine(headerLine, candidate).Count).First();

    private static IReadOnlyList<string> SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        foreach (var character in line)
        {
            if (character == '"') { inQuotes = !inQuotes; continue; }
            if (character == delimiter && !inQuotes) { fields.Add(builder.ToString().Trim()); builder.Clear(); continue; }
            builder.Append(character);
        }
        fields.Add(builder.ToString().Trim());
        return fields;
    }

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Trim('"'))
        {
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static bool TryGetField(IReadOnlyList<string> fields, int index, out string value)
    {
        value = string.Empty;
        if (index < 0 || index >= fields.Count) return false;
        value = fields[index].Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDouble(IReadOnlyList<string> fields, int index, out double value)
    {
        value = 0;
        return TryGetField(fields, index, out var text)
            && double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp)) return true;
        foreach (var format in new[] { "yyyy.MM.dd HH:mm", "yyyy.MM.dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyyMMdd HH:mm:ss", "yyyyMMddHHmmss" })
        {
            if (DateTimeOffset.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp)) return true;
        }
        return false;
    }
}
