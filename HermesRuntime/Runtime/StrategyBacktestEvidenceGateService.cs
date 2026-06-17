using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestEvidenceGateEntry(
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    int HistoricalPeriodDays,
    int HistoricalBars,
    int TradingDays,
    int TradesSimulated,
    string SampleClassification,
    bool PassedResearchGate,
    bool PassedOosGate,
    bool PassedCertificationGate,
    string RootCause,
    IReadOnlyList<string> Warnings);

public sealed record StrategyBacktestEvidenceGateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int AuditedBacktests,
    int PassedResearchGateCount,
    int PassedOosGateCount,
    int PassedCertificationGateCount,
    int InsufficientHistoryCount,
    int InsufficientSampleCount,
    IReadOnlyList<StrategyBacktestEvidenceGateEntry> Entries,
    IReadOnlyDictionary<string, int> Thresholds,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    bool FrankRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestEvidenceGateService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyBacktestEvidenceGateService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_quality");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_backtest_evidence_gate.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_backtest_evidence_gate.md");

    public StrategyBacktestEvidenceGateReport Run()
    {
        Directory.CreateDirectory(Root);

        var qualityAudit = LoadQualityAudit();
        var entries = new List<StrategyBacktestEvidenceGateEntry>();
        foreach (var entry in qualityAudit?.Entries ?? [])
        {
            entries.Add(BuildEntry(entry));
        }

        var report = new StrategyBacktestEvidenceGateReport(
            ReportVersion: "strategy_backtest_evidence_gate_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            AuditedBacktests: entries.Count,
            PassedResearchGateCount: entries.Count(entry => entry.PassedResearchGate),
            PassedOosGateCount: entries.Count(entry => entry.PassedOosGate),
            PassedCertificationGateCount: entries.Count(entry => entry.PassedCertificationGate),
            InsufficientHistoryCount: entries.Count(entry => entry.RootCause == "not_enough_history" || entry.RootCause == "both"),
            InsufficientSampleCount: entries.Count(entry => entry.RootCause == "not_enough_trades" || entry.RootCause == "both"),
            Entries: entries,
            Thresholds: new Dictionary<string, int>
            {
                ["research_gate_historical_period_days_min"] = 180,
                ["research_gate_trades_min"] = 30,
                ["oos_gate_historical_period_days_min"] = 365,
                ["oos_gate_trades_min"] = 100,
                ["certification_gate_historical_period_days_min"] = 730,
                ["certification_gate_trades_min"] = 100,
            },
            Warnings: entries.Count == 0 ? ["no_backtest_results_found"] : [],
            OperatorSummary: BuildOperatorSummary(entries),
            FrankRequired: false,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public StrategyBacktestEvidenceGateReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestEvidenceGateReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategyBacktestEvidenceGateEntry BuildEntry(StrategyBacktestQualityAuditEntry quality)
    {
        var job = FindJob(quality.BacktestJobId);
        var datasetPath = Path.Combine(_storagePaths.Root, "market_data", "candles", quality.Asset.ToUpperInvariant(), quality.Timeframe.ToUpperInvariant());
        var candles = LoadCandles(datasetPath, quality.Asset, quality.Timeframe);
        var historicalBars = candles.Count;
        var historicalPeriodDays = candles.Count == 0
            ? 0
            : Math.Max(1, (int)Math.Round((candles[^1].TimestampUtc.Date - candles[0].TimestampUtc.Date).TotalDays) + 1);
        var tradingDays = candles.Select(candle => candle.TimestampUtc.Date).Distinct().Count();

        var passedResearchGate = historicalPeriodDays >= 180 && quality.TradesSimulated >= 30;
        var passedOosGate = historicalPeriodDays >= 365 && quality.TradesSimulated >= 100;
        var passedCertificationGate = historicalPeriodDays >= 730 && quality.TradesSimulated >= 100 && quality.EligibleForCertification;

        var rootCause = DetermineRootCause(historicalPeriodDays, quality.TradesSimulated);
        var warnings = new List<string>();
        if (historicalBars == 0)
        {
            warnings.Add("dataset_missing");
        }

        return new StrategyBacktestEvidenceGateEntry(
            BacktestJobId: quality.BacktestJobId,
            StrategyPattern: quality.StrategyPattern,
            Asset: quality.Asset,
            Timeframe: quality.Timeframe,
            HistoricalPeriodDays: historicalPeriodDays,
            HistoricalBars: historicalBars,
            TradingDays: tradingDays,
            TradesSimulated: quality.TradesSimulated,
            SampleClassification: Classify(historicalPeriodDays, quality.TradesSimulated),
            PassedResearchGate: passedResearchGate,
            PassedOosGate: passedOosGate,
            PassedCertificationGate: passedCertificationGate,
            RootCause: rootCause,
            Warnings: warnings);
    }

    private static string Classify(int historicalPeriodDays, int trades)
    {
        if (historicalPeriodDays < 180)
        {
            return "insufficient_history";
        }

        if (trades < 30)
        {
            return "insufficient_sample";
        }

        if (historicalPeriodDays >= 730 && trades >= 100)
        {
            return "certification_candidate";
        }

        if (historicalPeriodDays >= 365 && trades >= 100)
        {
            return "oos_ready";
        }

        return "research_ready";
    }

    private static string DetermineRootCause(int historicalPeriodDays, int trades)
    {
        var hasHistory = historicalPeriodDays >= 180;
        var hasTrades = trades >= 30;
        return (hasHistory, hasTrades) switch
        {
            (false, false) => "both",
            (false, true) => "not_enough_history",
            (true, false) => "not_enough_trades",
            _ => "unknown"
        };
    }

    private static IReadOnlyList<MarketDataCandle> LoadCandles(string directory, string asset, string timeframe)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var candles = new List<MarketDataCandle>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
                    if (candle is null)
                    {
                        continue;
                    }

                    if (!candle.Symbol.Equals(asset, StringComparison.OrdinalIgnoreCase) || !candle.Timeframe.Equals(timeframe, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    candles.Add(candle);
                }
                catch (JsonException)
                {
                    continue;
                }
            }
        }

        return candles.OrderBy(candle => candle.TimestampUtc).ToList();
    }

    private StrategyBacktestJobPlan? FindJob(string backtestJobId)
    {
        var queuePath = Path.Combine(_storagePaths.Root, "queues", "strategy_backtest_jobs.json");
        if (!File.Exists(queuePath))
        {
            return null;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<StrategyBacktestJobPlan>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
            return items.FirstOrDefault(item => item.BacktestJobId.Equals(backtestJobId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategyBacktestQualityAuditReport? LoadQualityAudit()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_quality", "strategy_backtest_quality_audit.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestQualityAuditReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string BuildOperatorSummary(IReadOnlyList<StrategyBacktestEvidenceGateEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "Keine Backtest-Ergebnisse gefunden. Frank nötig: nein.";
        }

        var entry = entries[0];
        return $"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe}\n\nTechnischer Backtest: erfolgreich\n\nHistorie:\n{entry.HistoricalPeriodDays} Tage\n\nTrades:\n{entry.TradesSimulated}\n\nErgebnis:\n{entry.SampleClassification.Replace('_', ' ')}\n\nGrund:\n{DescribeRootCause(entry.RootCause)}\n\nEmpfehlung:\n{DescribeRecommendation(entry.RootCause)}\n\nFrank nötig:\nnein";
    }

    private static string DescribeRootCause(string rootCause)
        => rootCause switch
        {
            "not_enough_history" => "Historie unter 180 Tagen",
            "not_enough_trades" => "Trades unter 30",
            "both" => "Historie unter 180 Tagen und Trades unter 30",
            _ => "Unklar"
        };

    private static string DescribeRecommendation(string rootCause)
        => rootCause switch
        {
            "not_enough_history" => "Historischen Zeitraum erweitern.",
            "not_enough_trades" => "Mehr Trades durch längeren Zeitraum oder breiteren Scope erzeugen.",
            "both" => "Historischen Zeitraum und Testumfang erweitern.",
            _ => "Keine klare Empfehlung."
        };

    private void WriteArtifacts(StrategyBacktestEvidenceGateReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyBacktestEvidenceGateReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Evidence Gate");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Audited backtests: {report.AuditedBacktests}");
        sb.AppendLine($"- Passed research gate: {report.PassedResearchGateCount}");
        sb.AppendLine($"- Passed OOS gate: {report.PassedOosGateCount}");
        sb.AppendLine($"- Passed certification gate: {report.PassedCertificationGateCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in report.Entries)
        {
            sb.AppendLine($"- {entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · history={entry.HistoricalPeriodDays}d · bars={entry.HistoricalBars} · trades={entry.TradesSimulated} · gate={entry.SampleClassification}");
        }
        return sb.ToString();
    }
}
