using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestSignalDensityEntry(
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    int HistoricalBars,
    int TradingDays,
    int RawPriceEvents,
    int BollingerBandTouches,
    int BollingerRejections,
    int BandWidthPasses,
    int EntryCandidates,
    int SimulatedTrades,
    double TouchRate,
    double RejectionRate,
    double FilterPassRate,
    double TradeConversionRate,
    string RootCause,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Warnings);

public sealed record StrategyBacktestSignalDensityReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int AuditedBacktests,
    IReadOnlyList<StrategyBacktestSignalDensityEntry> Entries,
    IReadOnlyDictionary<string, int> FunnelTotals,
    IReadOnlyDictionary<string, double> DensityScores,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    bool FrankRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestSignalDensityAnalyzerService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyBacktestSignalDensityAnalyzerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_density");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_backtest_signal_density_analyzer.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_backtest_signal_density_analyzer.md");

    public StrategyBacktestSignalDensityReport Run()
    {
        Directory.CreateDirectory(Root);

        var job = FindCompletedJob();
        var entries = new List<StrategyBacktestSignalDensityEntry>();
        var warnings = new List<string>();

        if (job is not null)
        {
            entries.Add(BuildEntry(job, warnings));
        }
        else
        {
            warnings.Add("completed_backtest_job_not_found");
        }

        var report = new StrategyBacktestSignalDensityReport(
            ReportVersion: "strategy_backtest_signal_density_analyzer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            AuditedBacktests: entries.Count,
            Entries: entries,
            FunnelTotals: BuildFunnelTotals(entries),
            DensityScores: BuildDensityScores(entries),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: BuildOperatorSummary(entries),
            FrankRequired: false,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public StrategyBacktestSignalDensityReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestSignalDensityReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategyBacktestSignalDensityEntry BuildEntry(StrategyBacktestJobPlan job, List<string> warnings)
    {
        var candles = LoadCandles(job.Asset, job.Timeframe, out var datasetWarnings, out var datasetErrors);
        warnings.AddRange(datasetWarnings);
        warnings.AddRange(datasetErrors);

        if (candles.Count == 0)
        {
            warnings.Add("dataset_missing");
        }

        const int period = 20;
        const double deviation = 2.0;
        var historicalBars = candles.Count;
        var tradingDays = candles.Select(candle => candle.TimestampUtc.Date).Distinct().Count();
        var rawPriceEvents = historicalBars;
        var bandTouches = 0;
        var rejections = 0;
        var bandWidthPasses = 0;
        var entryCandidates = 0;

        for (var index = period; index < candles.Count; index++)
        {
            var window = candles.Skip(index - period).Take(period).Select(candle => candle.Close).ToArray();
            var mean = window.Average();
            var variance = window.Select(value => Math.Pow(value - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            if (stdDev <= 0)
            {
                continue;
            }

            var upper = mean + deviation * stdDev;
            var lower = mean - deviation * stdDev;
            var current = candles[index];
            var previous = candles[index - 1];
            var bandWidth = upper - lower;
            var touched = current.Low <= lower || current.High >= upper;
            var longSignal = current.Low <= lower && current.Close > lower && current.Close >= previous.Close;
            var shortSignal = current.High >= upper && current.Close < upper && current.Close <= previous.Close;
            var rejection = longSignal || shortSignal;
            var widthPass = bandWidth >= 0.4;

            if (touched)
            {
                bandTouches++;
            }

            if (rejection)
            {
                rejections++;
            }

            if (!widthPass || !rejection)
            {
                continue;
            }

            bandWidthPasses++;
            entryCandidates++;
        }

        var simulatedTrades = Math.Min(entryCandidates, Math.Max(1, job.MaxRuns));
        var touchRate = rawPriceEvents == 0 ? 0 : (double)bandTouches / rawPriceEvents;
        var rejectionRate = bandTouches == 0 ? 0 : (double)rejections / bandTouches;
        var filterPassRate = rejections == 0 ? 0 : (double)bandWidthPasses / rejections;
        var tradeConversionRate = entryCandidates == 0 ? 0 : (double)simulatedTrades / entryCandidates;

        var capReached = entryCandidates > simulatedTrades;
        var rootCause = DetermineRootCause(candles.Count, bandTouches, rejections, bandWidthPasses, entryCandidates, simulatedTrades, capReached);
        var recommendations = BuildRecommendations(rootCause, capReached, entryCandidates, simulatedTrades);
        var entryWarnings = new List<string>();
        if (capReached)
        {
            entryWarnings.Add("max_runs_cap_reached");
        }
        if (datasetWarnings.Count > 0)
        {
            entryWarnings.AddRange(datasetWarnings);
        }
        if (datasetErrors.Count > 0)
        {
            entryWarnings.AddRange(datasetErrors);
        }

        return new StrategyBacktestSignalDensityEntry(
            BacktestJobId: job.BacktestJobId,
            StrategyPattern: job.StrategyPattern,
            Asset: job.Asset,
            Timeframe: job.Timeframe,
            HistoricalBars: historicalBars,
            TradingDays: tradingDays,
            RawPriceEvents: rawPriceEvents,
            BollingerBandTouches: bandTouches,
            BollingerRejections: rejections,
            BandWidthPasses: bandWidthPasses,
            EntryCandidates: entryCandidates,
            SimulatedTrades: simulatedTrades,
            TouchRate: Math.Round(touchRate, 4),
            RejectionRate: Math.Round(rejectionRate, 4),
            FilterPassRate: Math.Round(filterPassRate, 4),
            TradeConversionRate: Math.Round(tradeConversionRate, 4),
            RootCause: rootCause,
            Recommendations: recommendations,
            Warnings: entryWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string DetermineRootCause(
        int historicalBars,
        int bandTouches,
        int rejections,
        int bandWidthPasses,
        int entryCandidates,
        int simulatedTrades,
        bool capReached)
    {
        if (historicalBars == 0)
        {
            return "dataset_issue";
        }

        if (bandTouches == 0 || rejections == 0)
        {
            return "insufficient_market_events";
        }

        if (bandWidthPasses < rejections)
        {
            return "width_filter_too_strict";
        }

        if (entryCandidates == 0)
        {
            return "entry_conditions_too_strict";
        }

        if (capReached && simulatedTrades < entryCandidates)
        {
            return "unknown";
        }

        return "unknown";
    }

    private static IReadOnlyList<string> BuildRecommendations(string rootCause, bool capReached, int entryCandidates, int simulatedTrades)
    {
        var recommendations = new List<string>();
        switch (rootCause)
        {
            case "insufficient_market_events":
                recommendations.Add("Zeitraum erweitern");
                recommendations.Add("Signaldefinition analysieren");
                break;
            case "entry_conditions_too_strict":
                recommendations.Add("Rejection-Regel überprüfen");
                recommendations.Add("Signaldefinition analysieren");
                break;
            case "width_filter_too_strict":
                recommendations.Add("Bollinger Width Filter untersuchen");
                recommendations.Add("Signaldefinition analysieren");
                break;
            case "dataset_issue":
                recommendations.Add("Datensatz prüfen");
                recommendations.Add("Historischen Zeitraum erweitern");
                break;
            default:
                recommendations.Add("Signaldefinition analysieren");
                break;
        }

        if (capReached && simulatedTrades < entryCandidates)
        {
            recommendations.Add("max_runs_limit erhöhen oder Job aufteilen");
        }

        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyDictionary<string, int> BuildFunnelTotals(IReadOnlyList<StrategyBacktestSignalDensityEntry> entries)
    {
        var entry = entries.FirstOrDefault();
        if (entry is null)
        {
            return new Dictionary<string, int>
            {
                ["historical_bars"] = 0,
                ["raw_price_events"] = 0,
                ["band_touches"] = 0,
                ["rejections"] = 0,
                ["band_width_passes"] = 0,
                ["entry_candidates"] = 0,
                ["simulated_trades"] = 0,
            };
        }

        return new Dictionary<string, int>
        {
            ["historical_bars"] = entry.HistoricalBars,
            ["raw_price_events"] = entry.RawPriceEvents,
            ["band_touches"] = entry.BollingerBandTouches,
            ["rejections"] = entry.BollingerRejections,
            ["band_width_passes"] = entry.BandWidthPasses,
            ["entry_candidates"] = entry.EntryCandidates,
            ["simulated_trades"] = entry.SimulatedTrades,
        };
    }

    private static IReadOnlyDictionary<string, double> BuildDensityScores(IReadOnlyList<StrategyBacktestSignalDensityEntry> entries)
    {
        var entry = entries.FirstOrDefault();
        if (entry is null)
        {
            return new Dictionary<string, double>
            {
                ["touch_rate"] = 0,
                ["rejection_rate"] = 0,
                ["filter_pass_rate"] = 0,
                ["trade_conversion_rate"] = 0,
            };
        }

        return new Dictionary<string, double>
        {
            ["touch_rate"] = entry.TouchRate,
            ["rejection_rate"] = entry.RejectionRate,
            ["filter_pass_rate"] = entry.FilterPassRate,
            ["trade_conversion_rate"] = entry.TradeConversionRate,
        };
    }

    private static string BuildOperatorSummary(IReadOnlyList<StrategyBacktestSignalDensityEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "Kein Backtest gefunden. Frank nötig: nein.";
        }

        var entry = entries[0];
        var summary = new StringBuilder();
        summary.AppendLine($"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe}");
        summary.AppendLine();
        summary.AppendLine("Signal-Funnel:");
        summary.AppendLine($"{entry.HistoricalBars} Kerzen");
        summary.AppendLine($"↓ {entry.BollingerBandTouches} Band-Touches");
        summary.AppendLine($"↓ {entry.BollingerRejections} Rejections");
        summary.AppendLine($"↓ {entry.BandWidthPasses} Band-Width-Passes");
        summary.AppendLine($"↓ {entry.EntryCandidates} Entry-Kandidaten");
        summary.AppendLine($"↓ {entry.SimulatedTrades} Trades");
        summary.AppendLine();
        summary.AppendLine(entry.RootCause == "unknown" && entry.SimulatedTrades < entry.EntryCandidates
            ? "Ergebnis: Backtest wurde durch das max_runs-Limit begrenzt."
            : $"Ergebnis: {entry.RootCause}");
        summary.AppendLine();
        summary.AppendLine("Empfehlung:");
        summary.AppendLine(string.Join(", ", entry.Recommendations));
        summary.AppendLine();
        summary.AppendLine("Frank nötig:");
        summary.AppendLine("nein");
        return summary.ToString().TrimEnd();
    }

    private void WriteArtifacts(StrategyBacktestSignalDensityReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyBacktestSignalDensityReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Signal Density Analyzer");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Audited backtests: {report.AuditedBacktests}");
        sb.AppendLine($"- Historical bars: {report.FunnelTotals["historical_bars"]}");
        sb.AppendLine($"- Band touches: {report.FunnelTotals["band_touches"]}");
        sb.AppendLine($"- Rejections: {report.FunnelTotals["rejections"]}");
        sb.AppendLine($"- Entry candidates: {report.FunnelTotals["entry_candidates"]}");
        sb.AppendLine($"- Simulated trades: {report.FunnelTotals["simulated_trades"]}");
        sb.AppendLine();
        sb.AppendLine("## Density Scores");
        foreach (var item in report.DensityScores)
        {
            sb.AppendLine($"- {item.Key}: {item.Value:0.####}");
        }
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in report.Entries)
        {
            sb.AppendLine($"- {entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · bars={entry.HistoricalBars} · touches={entry.BollingerBandTouches} · rejections={entry.BollingerRejections} · candidates={entry.EntryCandidates} · trades={entry.SimulatedTrades} · root_cause={entry.RootCause}");
        }
        return sb.ToString();
    }

    private StrategyBacktestJobPlan? FindCompletedJob()
    {
        var queuePath = Path.Combine(_storagePaths.Root, "queues", "strategy_backtest_jobs.json");
        if (!File.Exists(queuePath))
        {
            return null;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<StrategyBacktestJobPlan>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
            return items.FirstOrDefault(item =>
                item.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                && item.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)
                && item.Timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase)
                && item.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault(item => item.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IReadOnlyList<MarketDataCandle> LoadCandles(string asset, string timeframe, out List<string> warnings, out List<string> errors)
    {
        warnings = [];
        errors = [];
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", asset.ToUpperInvariant(), timeframe.ToUpperInvariant());
        if (!Directory.Exists(directory))
        {
            errors.Add("dataset_missing");
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToList();
        if (files.Count == 0)
        {
            errors.Add("dataset_missing");
            return [];
        }

        var candleMap = new Dictionary<DateTimeOffset, MarketDataCandle>();
        var invalidRows = 0;
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
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
                    invalidRows++;
                    continue;
                }

                if (candle is null || candle.High < candle.Low || candle.Open <= 0 || candle.High <= 0 || candle.Low <= 0 || candle.Close <= 0)
                {
                    invalidRows++;
                    continue;
                }

                candleMap[candle.TimestampUtc] = candle;
            }
        }

        if (candleMap.Count == 0)
        {
            errors.Add("dataset_missing");
            return [];
        }

        if (invalidRows > 0)
        {
            warnings.Add("dataset_rows_filtered");
        }

        return candleMap.Values.OrderBy(candle => candle.TimestampUtc).ToList();
    }
}
