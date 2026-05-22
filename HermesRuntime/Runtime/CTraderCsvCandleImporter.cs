using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CTraderCsvCandleImporter
{
    private const string ImportSource = "hermes_ctrader_csv_import";

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public CTraderCsvCandleImporter(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public CTraderCsvImportResult Import(
        string symbol,
        string timeframe,
        string sourcePath,
        bool copyRawCsv = true)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var importId = $"ctrader_csv_{requestedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var normalizedSymbol = NormalizeSegment(symbol);
        var normalizedTimeframe = NormalizeSegment(timeframe);
        var fullSourcePath = Path.GetFullPath(sourcePath);

        PublishHistoricalImportStarted(
            importId,
            normalizedSymbol,
            normalizedTimeframe,
            fullSourcePath,
            requestedAtUtc);

        if (!File.Exists(fullSourcePath))
        {
            var validation = new ImportValidationResult(
                IsValid: false,
                SourceRowCount: 0,
                ImportedRowCount: 0,
                InvalidRowCount: 0,
                FromUtc: null,
                ToUtc: null,
                MissingColumns: [],
                InvalidRows: [$"CSV file not found: {fullSourcePath}"],
                Warnings: []);

            PublishHistoricalImportFailed(importId, normalizedSymbol, normalizedTimeframe, validation);
            return new CTraderCsvImportResult(
                importId,
                normalizedSymbol,
                normalizedTimeframe,
                MarketDataImportFormat.CTraderCsv,
                fullSourcePath,
                OutputPath: null,
                RawImportPath: null,
                validation);
        }

        var lines = File.ReadAllLines(fullSourcePath);
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            var validation = new ImportValidationResult(
                IsValid: false,
                SourceRowCount: 0,
                ImportedRowCount: 0,
                InvalidRowCount: 0,
                FromUtc: null,
                ToUtc: null,
                MissingColumns: ["header"],
                InvalidRows: ["CSV file is empty or has no header row."],
                Warnings: []);

            PublishHistoricalImportFailed(importId, normalizedSymbol, normalizedTimeframe, validation);
            return new CTraderCsvImportResult(
                importId,
                normalizedSymbol,
                normalizedTimeframe,
                MarketDataImportFormat.CTraderCsv,
                fullSourcePath,
                OutputPath: null,
                RawImportPath: null,
                validation);
        }

        var delimiter = DetectDelimiter(lines[0]);
        var headers = SplitCsvLine(lines[0], delimiter);
        var columnMap = BuildColumnMap(headers);
        var missingColumns = GetMissingColumns(columnMap);
        var sourceRowCount = Math.Max(0, lines.Length - 1);

        if (missingColumns.Count > 0)
        {
            var validation = new ImportValidationResult(
                IsValid: false,
                SourceRowCount: sourceRowCount,
                ImportedRowCount: 0,
                InvalidRowCount: sourceRowCount,
                FromUtc: null,
                ToUtc: null,
                MissingColumns: missingColumns,
                InvalidRows: ["Missing required CSV columns."],
                Warnings: []);

            PublishHistoricalImportFailed(importId, normalizedSymbol, normalizedTimeframe, validation);
            return new CTraderCsvImportResult(
                importId,
                normalizedSymbol,
                normalizedTimeframe,
                MarketDataImportFormat.CTraderCsv,
                fullSourcePath,
                OutputPath: null,
                RawImportPath: null,
                validation);
        }

        var candles = new List<MarketDataCandle>();
        var invalidRows = new List<string>();
        var futureRows = 0;

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var rowNumber = lineIndex + 1;
            var fields = SplitCsvLine(line, delimiter);
            if (!TryReadCandle(
                    fields,
                    columnMap,
                    normalizedSymbol,
                    normalizedTimeframe,
                    out var candle,
                    out var error))
            {
                invalidRows.Add($"Row {rowNumber}: {error}");
                continue;
            }

            if (candle.TimestampUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                futureRows++;
            }

            candles.Add(candle);
        }

        var warnings = new List<string>();
        if (futureRows > 0)
        {
            warnings.Add($"{futureRows} row(s) contain future timestamps. Import was not blocked.");
        }

        var orderedCandles = candles
            .OrderBy(candle => candle.TimestampUtc)
            .ToList();

        var fromUtc = orderedCandles.Count > 0 ? orderedCandles[0].TimestampUtc : (DateTimeOffset?)null;
        var toUtc = orderedCandles.Count > 0 ? orderedCandles[^1].TimestampUtc : (DateTimeOffset?)null;
        var isValid = orderedCandles.Count > 0 && missingColumns.Count == 0;

        var outputPath = default(string);
        var rawImportPath = default(string);
        if (isValid)
        {
            var outputDirectory = Path.Combine(
                _storagePaths.Root,
                "market_data",
                "candles",
                normalizedSymbol,
                normalizedTimeframe);
            Directory.CreateDirectory(outputDirectory);
            outputPath = Path.Combine(outputDirectory, $"{importId}.candles.jsonl");
            WriteJsonl(outputPath, orderedCandles);

            if (copyRawCsv)
            {
                var rawDirectory = Path.Combine(
                    _storagePaths.Root,
                    "market_data",
                    "raw_imports",
                    normalizedSymbol,
                    normalizedTimeframe);
                Directory.CreateDirectory(rawDirectory);
                rawImportPath = Path.Combine(rawDirectory, $"{importId}{Path.GetExtension(fullSourcePath)}");
                File.Copy(fullSourcePath, rawImportPath, overwrite: true);
            }
        }

        var validationResult = new ImportValidationResult(
            IsValid: isValid,
            SourceRowCount: sourceRowCount,
            ImportedRowCount: orderedCandles.Count,
            InvalidRowCount: invalidRows.Count,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            MissingColumns: missingColumns,
            InvalidRows: invalidRows.Take(20).ToList(),
            Warnings: warnings);

        if (isValid)
        {
            PublishHistoricalImportCompleted(
                importId,
                normalizedSymbol,
                normalizedTimeframe,
                fullSourcePath,
                outputPath!,
                rawImportPath,
                validationResult);
        }
        else
        {
            PublishHistoricalImportFailed(importId, normalizedSymbol, normalizedTimeframe, validationResult);
        }

        return new CTraderCsvImportResult(
            importId,
            normalizedSymbol,
            normalizedTimeframe,
            MarketDataImportFormat.CTraderCsv,
            fullSourcePath,
            outputPath,
            rawImportPath,
            validationResult);
    }

    private static bool TryReadCandle(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columnMap,
        string symbol,
        string timeframe,
        out MarketDataCandle candle,
        out string error)
    {
        candle = default!;
        error = string.Empty;

        if (!TryGetField(fields, columnMap["time"], out var timeText)
            || !TryParseTimestamp(timeText, out var timestampUtc))
        {
            error = "invalid Time/Timestamp/Date";
            return false;
        }

        if (!TryGetDouble(fields, columnMap["open"], out var open))
        {
            error = "invalid Open";
            return false;
        }

        if (!TryGetDouble(fields, columnMap["high"], out var high))
        {
            error = "invalid High";
            return false;
        }

        if (!TryGetDouble(fields, columnMap["low"], out var low))
        {
            error = "invalid Low";
            return false;
        }

        if (!TryGetDouble(fields, columnMap["close"], out var close))
        {
            error = "invalid Close";
            return false;
        }

        if (!TryGetDouble(fields, columnMap["volume"], out var volume))
        {
            error = "invalid Volume/TickVolume";
            return false;
        }

        if (high < low)
        {
            error = "High is lower than Low";
            return false;
        }

        candle = new MarketDataCandle(
            TimestampUtc: timestampUtc,
            Open: open,
            High: high,
            Low: low,
            Close: close,
            Volume: volume,
            Symbol: symbol,
            Timeframe: timeframe);
        return true;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = NormalizeHeader(headers[index]);
            if (IsTimeColumn(normalized))
            {
                map.TryAdd("time", index);
            }
            else if (normalized == "open")
            {
                map.TryAdd("open", index);
            }
            else if (normalized == "high")
            {
                map.TryAdd("high", index);
            }
            else if (normalized == "low")
            {
                map.TryAdd("low", index);
            }
            else if (normalized == "close")
            {
                map.TryAdd("close", index);
            }
            else if (normalized is "volume" or "tickvolume")
            {
                map.TryAdd("volume", index);
            }
        }

        return map;
    }

    private static List<string> GetMissingColumns(IReadOnlyDictionary<string, int> columnMap)
    {
        var missing = new List<string>();
        foreach (var column in new[] { "time", "open", "high", "low", "close", "volume" })
        {
            if (!columnMap.ContainsKey(column))
            {
                missing.Add(column == "time" ? "Time/Timestamp/Date" : column);
            }
        }

        return missing;
    }

    private static bool IsTimeColumn(string normalizedHeader) =>
        normalizedHeader is "time" or "timestamp" or "date" or "datetime" or "timeutc" or "timestamputc" or "dateutc" or "opentime";

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Trim('"'))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
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

    private static char DetectDelimiter(string headerLine)
    {
        var candidates = new[] { ',', ';', '\t' };
        return candidates
            .OrderByDescending(candidate => SplitCsvLine(headerLine, candidate).Count)
            .First();
    }

    private static IReadOnlyList<string> SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == delimiter && !inQuotes)
            {
                fields.Add(builder.ToString().Trim());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        fields.Add(builder.ToString().Trim());
        return fields;
    }

    private static bool TryGetField(IReadOnlyList<string> fields, int index, out string value)
    {
        value = string.Empty;
        if (index < 0 || index >= fields.Count)
        {
            return false;
        }

        value = fields[index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDouble(IReadOnlyList<string> fields, int index, out double value)
    {
        value = 0;
        if (!TryGetField(fields, index, out var text))
        {
            return false;
        }

        return TryParseDouble(text, out value);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        var normalized = text.Trim().Trim('"');
        if (!normalized.Contains('.', StringComparison.Ordinal) && normalized.Contains(',', StringComparison.Ordinal))
        {
            var decimalComma = normalized.Replace(',', '.');
            if (double.TryParse(
                    decimalComma,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }
        }

        if (double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseTimestamp(string text, out DateTimeOffset timestampUtc)
    {
        var value = text.Trim().Trim('"');
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, styles, out var timestamp))
        {
            timestampUtc = timestamp.ToUniversalTime();
            return true;
        }

        var exactFormats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.fff",
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm:ss.fff",
            "MM/dd/yyyy HH:mm:ss",
            "M/d/yyyy H:mm:ss",
            "M/d/yyyy h:mm:ss tt"
        };

        if (DateTimeOffset.TryParseExact(
                value,
                exactFormats,
                CultureInfo.InvariantCulture,
                styles,
                out timestamp))
        {
            timestampUtc = timestamp.ToUniversalTime();
            return true;
        }

        timestampUtc = default;
        return false;
    }

    private static void WriteJsonl<T>(string path, IEnumerable<T> rows)
    {
        File.WriteAllLines(
            path,
            rows.Select(row => JsonSerializer.Serialize(row, JsonDefaults.WriteOptions)));
    }

    private void PublishHistoricalImportStarted(
        string importId,
        string symbol,
        string timeframe,
        string sourcePath,
        DateTimeOffset requestedAtUtc)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.HistoricalImportStarted,
            ImportSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Historical cTrader CSV import started. Local file import only.",
                importId,
                symbol,
                timeframe,
                format = MarketDataImportFormat.CTraderCsv,
                sourcePath,
                requestedAtUtc,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishHistoricalImportCompleted(
        string importId,
        string symbol,
        string timeframe,
        string sourcePath,
        string outputPath,
        string? rawImportPath,
        ImportValidationResult validation)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.HistoricalImportCompleted,
            ImportSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Historical cTrader CSV import completed. Candles were stored as local JSONL.",
                importId,
                symbol,
                timeframe,
                format = MarketDataImportFormat.CTraderCsv,
                sourcePath,
                outputPath,
                rawImportPath,
                validation.SourceRowCount,
                validation.ImportedRowCount,
                validation.InvalidRowCount,
                validation.FromUtc,
                validation.ToUtc,
                validation.Warnings,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishHistoricalImportFailed(
        string importId,
        string symbol,
        string timeframe,
        ImportValidationResult validation)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.HistoricalImportFailed,
            ImportSource,
            EventSeverity.Warning,
            _runtimeVersion,
            new
            {
                message = "Historical cTrader CSV import failed validation. No trading action was possible.",
                importId,
                symbol,
                timeframe,
                format = MarketDataImportFormat.CTraderCsv,
                validation.SourceRowCount,
                validation.ImportedRowCount,
                validation.InvalidRowCount,
                validation.MissingColumns,
                validation.InvalidRows,
                validation.Warnings,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }
}
