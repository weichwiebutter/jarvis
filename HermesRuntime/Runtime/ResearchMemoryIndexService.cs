using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ResearchMemoryIndexService
{
    private const string IndexVersion = "research_memory_index_v1";

    private readonly StoragePaths _storagePaths;

    public ResearchMemoryIndexService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string MemoryRoot => Path.Combine(_storagePaths.Root, "research_memory");

    public string IndexPath => Path.Combine(MemoryRoot, "research_index.json");

    public string CheckpointDirectory => Path.Combine(MemoryRoot, "checkpoints");

    public ResearchMemoryIndex? LoadIndex()
    {
        if (!File.Exists(IndexPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(IndexPath);
            return JsonSerializer.Deserialize<ResearchMemoryIndex>(json, JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public ResearchMemoryIndex UpdateIndex()
    {
        Directory.CreateDirectory(MemoryRoot);
        Directory.CreateDirectory(CheckpointDirectory);

        var reports = ReadReportMetrics().ToList();
        var ranges = BuildCurrentProcessedRanges();
        var warnings = reports
            .SelectMany(report => report.Warnings)
            .Concat(ranges.Warnings)
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .Take(80)
            .ToList();

        var symbols = reports
            .SelectMany(report => report.SymbolsProcessed)
            .Concat(ranges.Ranges.Select(range => range.Symbol))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var timeframes = ranges.Ranges
            .Select(range => range.Timeframe)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = new ResearchMemoryIndex(
            IndexVersion: IndexVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            LastRunAt: reports.Count == 0 ? null : reports.Max(report => report.CompletedAtUtc),
            SymbolsProcessed: symbols,
            TimeframesProcessed: timeframes,
            CandlesProcessed: reports.Sum(report => report.CandlesProcessed),
            FeaturesGenerated: reports.Sum(report => report.FeaturesGenerated),
            SignalsGenerated: reports.Sum(report => report.SignalsGenerated),
            OutcomesGenerated: reports.Sum(report => report.OutcomesGenerated),
            BacktestsGenerated: reports.Sum(report => report.BacktestsGenerated),
            ProcessedRanges: ranges.Ranges,
            Warnings: warnings,
            LearningReady: reports.Any(report => report.LearningReady),
            IndexedRunIds: reports.Select(report => report.RunId).Distinct(StringComparer.Ordinal).OrderBy(value => value).ToList(),
            RunCount: reports.Count);

        WriteIndex(index);
        return index;
    }

    public IReadOnlyList<ResearchProcessedRange> GetCurrentMarketDataRanges()
    {
        return BuildCurrentProcessedRanges().Ranges;
    }

    public string BuildMarketDataFingerprint(IReadOnlyList<ResearchProcessedRange> ranges)
    {
        return string.Join(
            "|",
            ranges
                .OrderBy(range => range.Symbol, StringComparer.OrdinalIgnoreCase)
                .ThenBy(range => range.Timeframe, StringComparer.OrdinalIgnoreCase)
                .ThenBy(range => range.SourcePath, StringComparer.OrdinalIgnoreCase)
                .Select(range => $"{range.Symbol}:{range.Timeframe}:{range.CandleCount}:{range.FromUtc:O}:{range.ToUtc:O}:{range.SourcePath}"));
    }

    public string WriteCheckpoint(
        LongRunResearchJob job,
        int iteration,
        string status,
        string message,
        ResearchMemoryIndex? index,
        string? betaRunId)
    {
        Directory.CreateDirectory(CheckpointDirectory);
        var path = Path.Combine(
            CheckpointDirectory,
            $"{job.JobId}.iteration_{iteration:000}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.checkpoint.json");

        var checkpoint = new
        {
            job.JobId,
            job.StartedAtUtc,
            job.DeadlineUtc,
            job.RequestedHours,
            iteration,
            status,
            message,
            checkpointAtUtc = DateTimeOffset.UtcNow,
            betaRunId,
            indexPath = IndexPath,
            runCount = index?.RunCount ?? 0,
            candlesProcessed = index?.CandlesProcessed ?? 0,
            featuresGenerated = index?.FeaturesGenerated ?? 0,
            signalsGenerated = index?.SignalsGenerated ?? 0,
            outcomesGenerated = index?.OutcomesGenerated ?? 0,
            backtestsGenerated = index?.BacktestsGenerated ?? 0,
            learningReady = index?.LearningReady ?? false,
            noAutoTrading = job.NoAutoTrading,
            humanReviewRequired = job.HumanReviewRequired
        };

        File.WriteAllText(path, JsonSerializer.Serialize(checkpoint, JsonDefaults.WriteOptions));
        return path;
    }

    private void WriteIndex(ResearchMemoryIndex index)
    {
        var json = JsonSerializer.Serialize(index, JsonDefaults.WriteOptions);
        File.WriteAllText(IndexPath, json);

        var historyDirectory = Path.Combine(MemoryRoot, "history");
        Directory.CreateDirectory(historyDirectory);
        var historyPath = Path.Combine(historyDirectory, $"{index.UpdatedAtUtc:yyyyMMddHHmmssfff}.research_index.json");
        File.WriteAllText(historyPath, json);
    }

    private IEnumerable<ReportMetrics> ReadReportMetrics()
    {
        var seenRunIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in EnumerateReportFiles("beta", "*.beta_report.json"))
        {
            if (TryReadReportMetrics(path, betaReport: true, out var report) && seenRunIds.Add(report.RunId))
            {
                yield return report;
            }
        }

        foreach (var path in EnumerateReportFiles("research", "*.research_summary.json"))
        {
            if (TryReadReportMetrics(path, betaReport: false, out var report) && seenRunIds.Add(report.RunId))
            {
                yield return report;
            }
        }
    }

    private IEnumerable<string> EnumerateReportFiles(string reportType, string pattern)
    {
        var directory = Path.Combine(_storagePaths.Root, "reports", reportType);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return path;
        }
    }

    private static bool TryReadReportMetrics(string path, bool betaReport, out ReportMetrics metrics)
    {
        metrics = default!;
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var runId = ReadString(root, "run_id", "runId");
            if (string.IsNullOrWhiteSpace(runId))
            {
                return false;
            }

            var completedAtUtc = ReadDateTimeOffset(root, "completed_at_utc", "completedAtUtc")
                ?? ReadDateTimeOffset(root, "started_at_utc", "startedAtUtc")
                ?? File.GetLastWriteTimeUtc(path);

            var learningReady = betaReport
                ? ReadBool(root, "learning_ready", "learningReady")
                : ReadInt(root, "candles_processed", "candlesProcessed") > 0
                    && ReadInt(root, "features_generated", "featuresGenerated") > 0
                    && ReadInt(root, "signals_generated", "signalsGenerated") > 0
                    && ReadInt(root, "backtests_generated", "backtestsGenerated") > 0;

            metrics = new ReportMetrics(
                RunId: runId,
                CompletedAtUtc: completedAtUtc,
                SymbolsProcessed: ReadStringArray(root, "symbols_processed", "symbolsProcessed"),
                CandlesProcessed: ReadInt(root, "candles_processed", "candlesProcessed"),
                FeaturesGenerated: ReadInt(root, "features_generated", "featuresGenerated"),
                SignalsGenerated: ReadInt(root, "signals_generated", "signalsGenerated"),
                OutcomesGenerated: ReadInt(root, "outcomes_generated", "outcomesGenerated"),
                BacktestsGenerated: ReadInt(root, "backtests_generated", "backtestsGenerated"),
                Warnings: ReadStringArray(root, "warnings"),
                LearningReady: learningReady);
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return false;
        }
    }

    private RangeScanResult BuildCurrentProcessedRanges()
    {
        var root = Path.Combine(_storagePaths.Root, "market_data", "candles");
        var warnings = new List<string>();
        if (!Directory.Exists(root))
        {
            return new RangeScanResult([], warnings);
        }

        var ranges = new List<ResearchProcessedRange>();
        foreach (var path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).OrderBy(path => path))
        {
            var count = 0;
            DateTimeOffset? fromUtc = null;
            DateTimeOffset? toUtc = null;
            string? symbol = null;
            string? timeframe = null;

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
                    warnings.Add($"Invalid candle JSON skipped in {path}.");
                    continue;
                }

                if (candle is null)
                {
                    continue;
                }

                count++;
                symbol ??= candle.Symbol;
                timeframe ??= candle.Timeframe;
                fromUtc = fromUtc is null || candle.TimestampUtc < fromUtc ? candle.TimestampUtc : fromUtc;
                toUtc = toUtc is null || candle.TimestampUtc > toUtc ? candle.TimestampUtc : toUtc;
            }

            if (count > 0)
            {
                ranges.Add(new ResearchProcessedRange(
                    Symbol: symbol ?? "UNKNOWN",
                    Timeframe: timeframe ?? "UNKNOWN",
                    FromUtc: fromUtc,
                    ToUtc: toUtc,
                    CandleCount: count,
                    SourcePath: path));
            }
        }

        return new RangeScanResult(ranges, warnings.Distinct(StringComparer.Ordinal).Take(20).ToList());
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int ReadInt(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : int.TryParse(ReadString(root, names), out var parsed) ? parsed : 0;
    }

    private static bool ReadBool(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => bool.TryParse(ReadString(root, names), out var parsed) && parsed
        };
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, params string[] names)
    {
        return DateTimeOffset.TryParse(ReadString(root, names), out var timestamp)
            ? timestamp.ToUniversalTime()
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private sealed record ReportMetrics(
        string RunId,
        DateTimeOffset CompletedAtUtc,
        IReadOnlyList<string> SymbolsProcessed,
        int CandlesProcessed,
        int FeaturesGenerated,
        int SignalsGenerated,
        int OutcomesGenerated,
        int BacktestsGenerated,
        IReadOnlyList<string> Warnings,
        bool LearningReady);

    private sealed record RangeScanResult(
        IReadOnlyList<ResearchProcessedRange> Ranges,
        IReadOnlyList<string> Warnings);
}

