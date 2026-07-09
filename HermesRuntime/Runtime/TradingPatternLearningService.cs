using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TradingPatternLearningPattern(
    string PatternId,
    string Confidence,
    int SampleSize,
    string Observation,
    IReadOnlyList<string> SupportingMetrics,
    string Recommendation,
    bool RequiresValidation,
    IReadOnlyList<string> Assets,
    IReadOnlyList<string> Sessions,
    IReadOnlyList<string> Outcomes);

public sealed record TradingPatternLearningReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int ClosedTradeCount,
    int EvaluatedSignalCount,
    int ForwardSignalCount,
    int BotEvolutionEntryCount,
    int PatternCount,
    IReadOnlyList<TradingPatternLearningPattern> Patterns,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SourceReportPaths,
    string ReportPath,
    string MarkdownPath);

public sealed class TradingPatternLearningService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TradingPatternLearningService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trading_pattern_learning");
    public string ReportPath => Path.Combine(Root, "trading_pattern_learning.json");
    public string MarkdownPath => Path.Combine(Root, "trading_pattern_learning.md");

    public TradingPatternLearningReport Run()
    {
        Directory.CreateDirectory(Root);

        var warnings = new List<string>();

        var historyService = new PaperTradeHistoryService(_storagePaths, _runtimeRoot);
        var summaryService = new PaperTradeSummaryService(_storagePaths, _runtimeRoot);
        var forwardEvaluationService = new PaperForwardEvaluationService(_storagePaths, _runtimeRoot);
        var signalEvaluationService = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot);
        var evolutionService = new BotEvolutionHistoryService(_storagePaths, _runtimeRoot);

        var historyReport = historyService.LoadLatestReport() ?? historyService.Run();
        var summaryReport = summaryService.LoadLatestReport() ?? summaryService.Run();
        var forwardReport = forwardEvaluationService.LoadLatestReport() ?? forwardEvaluationService.Run();
        var signalReport = signalEvaluationService.LoadLatestReport();
        if (signalReport is null)
        {
            warnings.Add("paper_signal_evaluation_report_missing");
            signalReport = signalEvaluationService.Run(null, null);
        }

        var evolutionReport = evolutionService.LoadLatestReport() ?? evolutionService.Run();

        var patterns = new List<TradingPatternLearningPattern>();
        patterns.AddRange(BuildAssetSessionPatterns(historyReport));
        patterns.AddRange(BuildSignalBlockerPatterns(signalReport, forwardReport));
        patterns.AddRange(BuildCurrentSystemStatePatterns(signalReport));
        patterns.AddRange(BuildSuccessConditionPatterns(signalReport, historyReport, summaryReport));
        patterns.Add(BuildEvolutionTrendPattern(evolutionReport));

        if (patterns.Count == 0)
        {
            warnings.Add("no_pattern_hypotheses_generated");
        }

        var distinctPatterns = patterns
            .GroupBy(pattern => pattern.PatternId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(pattern => ConfidenceRank(pattern.Confidence))
            .ThenByDescending(pattern => pattern.SampleSize)
            .ThenBy(pattern => pattern.PatternId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new TradingPatternLearningReport(
            ReportVersion: "trading_pattern_learning_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: warnings.Count == 0 ? "ready" : "partial",
            ClosedTradeCount: historyReport.ClosedTradeCount,
            EvaluatedSignalCount: signalReport.EvaluatedSignals,
            ForwardSignalCount: forwardReport.SignalCount,
            BotEvolutionEntryCount: evolutionReport.EntryCount,
            PatternCount: distinctPatterns.Count,
            Patterns: distinctPatterns,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceReportPaths: new[]
            {
                historyService.ReportPath,
                summaryService.ReportPath,
                forwardEvaluationService.ReportPath,
                signalEvaluationService.ReportPath,
                evolutionService.ReportPath,
            },
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public TradingPatternLearningReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TradingPatternLearningReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<TradingPatternLearningPattern> BuildAssetSessionPatterns(
        PaperTradeHistoryReport historyReport)
    {
        var patterns = new List<TradingPatternLearningPattern>();
        var groupedTrades = historyReport.ClosedTrades
            .Select(trade => new
            {
                Trade = trade,
                Session = DetermineSession(trade.OpenedAtUtc),
            })
            .GroupBy(entry => $"{entry.Trade.Asset}|{entry.Session}", StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groupedTrades)
        {
            var trades = group.Select(entry => entry.Trade).ToList();
            if (trades.Count == 0)
            {
                continue;
            }

            var keyParts = group.Key.Split('|', 2);
            var asset = keyParts.Length > 0 ? keyParts[0] : "-";
            var bestSession = keyParts.Length > 1 ? keyParts[1] : "Off Session";
            var wins = trades.Count(item => item.Outcome.Equals("tp", StringComparison.OrdinalIgnoreCase) || item.RMultiple > 0m);
            var losses = trades.Count(item => item.Outcome.Equals("sl", StringComparison.OrdinalIgnoreCase) || item.RMultiple < 0m);
            var expires = trades.Count(item => item.Outcome.Equals("expired", StringComparison.OrdinalIgnoreCase));
            var invalidated = trades.Count(item => item.Outcome.Equals("invalidated", StringComparison.OrdinalIgnoreCase));
            var avgR = trades.Average(item => item.RMultiple);
            var netR = trades.Sum(item => item.RMultiple);
            var avgDuration = trades.Where(item => item.ClosedAtUtc.HasValue)
                .Select(item => Math.Max(0m, (decimal)(item.ClosedAtUtc!.Value - item.OpenedAtUtc).TotalMinutes))
                .DefaultIfEmpty(0m)
                .Average();
            var winRate = trades.Count == 0 ? 0m : (decimal)wins / trades.Count;
            var confidence = DetermineConfidence(trades.Count, avgR, winRate, netR);
            var observation = $"{asset} trades perform best during {bestSession} with {FormatPercent(winRate)} win rate and {FormatDecimal(avgR)} R average.";
            var supportingMetrics = new List<string>
            {
                $"sample_size={trades.Count}",
                $"win_rate={FormatPercent(winRate)}",
                $"avg_r={FormatDecimal(avgR)}",
                $"net_r={FormatDecimal(netR)}",
                $"avg_duration_minutes={FormatDecimal(avgDuration)}",
                $"tp_count={wins}",
                $"sl_count={losses}",
                $"expired_count={expires}",
                $"invalidated_count={invalidated}",
            };

            var recommendation = netR >= 0m
                ? $"Treat {bestSession} as the preferred validation window for {asset} and collect more samples before changing parameters."
                : $"Keep {bestSession} under validation for {asset}; current edge is not yet strong enough for a rule change.";

            patterns.Add(new TradingPatternLearningPattern(
                PatternId: $"{asset.ToLowerInvariant()}_{NormalizeSessionKey(bestSession)}_performance",
                Confidence: confidence,
                SampleSize: trades.Count,
                Observation: observation,
                SupportingMetrics: supportingMetrics,
                Recommendation: recommendation,
                RequiresValidation: true,
                Assets: [asset],
                Sessions: [bestSession],
                Outcomes: BuildOutcomeSummary(trades)));
        }

        return patterns;
    }

    private static IReadOnlyList<TradingPatternLearningPattern> BuildSignalBlockerPatterns(
        PaperSignalEvaluationReport signalReport,
        PaperForwardEvaluationReport forwardReport)
    {
        var patterns = new List<TradingPatternLearningPattern>();
        var signals = signalReport.Signals;
        var total = Math.Max(1, signals.Count);

        AddBlockerPattern(
            patterns,
            "session_gating_blocks_entries",
            signals.Where(signal => !signal.SessionAllowed).ToList(),
            total,
            "Most signals are currently blocked by session gating.",
            "Validate only during the allowed session windows before revisiting entries.");

        AddBlockerPattern(
            patterns,
            "spread_gating_blocks_entries",
            signals.Where(signal => !signal.SpreadAllowed).ToList(),
            total,
            "Spread filters are blocking a subset of signals from becoming actionable.",
            "Keep the spread filter; validate the feed during lower-spread periods.");

        AddBlockerPattern(
            patterns,
            "paper_entry_disabled_signals",
            signals.Where(signal => signal.SignalStatus.Equals("invalidated", StringComparison.OrdinalIgnoreCase)
                && signal.Warnings.Any(warning => warning.Contains("paper_entry_disabled", StringComparison.OrdinalIgnoreCase))).ToList(),
            total,
            "Some signals are invalidated because paper entry is disabled in the embedded package mapping.",
            "Review the embedded signal export and keep the paper entry defaults consistent.");

        AddBlockerPattern(
            patterns,
            "signal_expiry_blocks_entries",
            signals.Where(signal => signal.SignalExpired).ToList(),
            total,
            "Signals can expire before execution when the expiry window is too short.",
            "Validate the expiry window against the forward session length before changing any entry rule.");

        AddBlockerPattern(
            patterns,
            "missing_risk_bounds_blocks_entries",
            signals.Where(signal => signal.Warnings.Any(warning => warning.Contains("missing_", StringComparison.OrdinalIgnoreCase))).ToList(),
            total,
            "Risk-bound or metadata gaps still block a subset of signals from being actionable.",
            "Keep validating the embedded package and chart annotations until the missing fields disappear.");

        if (forwardReport.ForwardRunStatus.Equals("green", StringComparison.OrdinalIgnoreCase))
        {
            patterns.Add(new TradingPatternLearningPattern(
                PatternId: "forward_run_safety_stable",
                Confidence: DetermineConfidence(Math.Max(1, forwardReport.SignalCount), forwardReport.NetR, 1m, forwardReport.NetR),
                SampleSize: Math.Max(1, forwardReport.SignalCount),
                Observation: "The latest forward evaluation is green, so the paper runtime safety path is currently stable.",
                SupportingMetrics:
                [
                    $"forward_run_status={forwardReport.ForwardRunStatus}",
                    $"timer_ticks={forwardReport.TimerTicks}",
                    $"signal_count={forwardReport.SignalCount}",
                    $"open_positions={forwardReport.OpenPositions}",
                    $"closed_trades={forwardReport.ClosedTrades}",
                    $"net_r={FormatDecimal(forwardReport.NetR)}",
                    $"expired_signal_count={forwardReport.ExpiredSignalCount}",
                    $"invalidated_signal_count={forwardReport.InvalidatedSignalCount}",
                ],
                Recommendation: "Keep the current runtime safety configuration and monitor the next forward session for regressions.",
                RequiresValidation: true,
                Assets: ["EURUSD", "XAUUSD", "GER40"],
                Sessions: ["forward_session"],
                Outcomes: ["green"]));
        }

        return patterns;
    }

    private static IReadOnlyList<TradingPatternLearningPattern> BuildSuccessConditionPatterns(
        PaperSignalEvaluationReport signalReport,
        PaperTradeHistoryReport historyReport,
        PaperTradeSummaryReport summaryReport)
    {
        var patterns = new List<TradingPatternLearningPattern>();
        var actionable = signalReport.Signals.Where(signal => signal.PaperDecision.Equals("would_trigger", StringComparison.OrdinalIgnoreCase)).ToList();
        if (actionable.Count > 0)
        {
            var triggerAssets = actionable.Select(signal => signal.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var avgConfidence = actionable.Average(signal => signal.ConfidenceBaseline);
            var wouldTriggerRate = signalReport.EvaluatedSignals == 0 ? 0m : (decimal)actionable.Count / signalReport.EvaluatedSignals;
            patterns.Add(new TradingPatternLearningPattern(
                PatternId: "signals_reach_would_trigger_when_session_and_spread_pass",
                Confidence: DetermineConfidence(actionable.Count, avgConfidence, wouldTriggerRate, actionable.Count),
                SampleSize: actionable.Count,
                Observation: "Signals that pass session and spread checks are the ones reaching would_trigger.",
                SupportingMetrics:
                [
                    $"actionable_signals={actionable.Count}",
                    $"would_trigger_rate={FormatPercent(wouldTriggerRate)}",
                    $"avg_confidence={FormatDecimal(avgConfidence)}",
                    $"closed_trades_from_same_period={historyReport.ClosedTradeCount}",
                    $"paper_closed_count={summaryReport.PaperClosedCount}",
                ],
                Recommendation: "Keep the current session and spread filters as the first-line success condition and validate any changes against new forward samples.",
                RequiresValidation: true,
                Assets: triggerAssets,
                Sessions: ["forward_runtime"],
                Outcomes: ["would_trigger"]));
        }

        return patterns;
    }

    private static IReadOnlyList<TradingPatternLearningPattern> BuildCurrentSystemStatePatterns(PaperSignalEvaluationReport signalReport)
    {
        var patterns = new List<TradingPatternLearningPattern>();

        var ger40Signals = signalReport.Signals.Where(signal => signal.Asset.Equals("GER40", StringComparison.OrdinalIgnoreCase)).ToList();
        var ger40Blocked = ger40Signals.Where(signal =>
            signal.SignalStatus.Equals("invalidated", StringComparison.OrdinalIgnoreCase)
            || signal.Warnings.Any(warning => warning.Contains("paper_entry_disabled", StringComparison.OrdinalIgnoreCase))
            || signal.Warnings.Any(warning => warning.Contains("missing_", StringComparison.OrdinalIgnoreCase))).ToList();
        if (ger40Blocked.Count > 0)
        {
            var paperEntryDisabledCount = ger40Blocked.Count(item => item.Warnings.Any(warning => warning.Contains("paper_entry_disabled", StringComparison.OrdinalIgnoreCase)));
            var missingRiskBoundsCount = ger40Blocked.Count(item => item.Warnings.Any(warning => warning.Contains("missing_", StringComparison.OrdinalIgnoreCase)));
            patterns.Add(new TradingPatternLearningPattern(
                PatternId: "ger40_chart_annotation_coverage_gap",
                Confidence: ger40Blocked.Count >= 2 ? "high" : "medium",
                SampleSize: ger40Blocked.Count,
                Observation: "GER40 still shows incomplete chart-annotation or risk-bound coverage in the current embedded signal state.",
                SupportingMetrics:
                [
                    $"signal_count={ger40Signals.Count}",
                    $"blocked_count={ger40Blocked.Count}",
                    $"paper_entry_disabled_count={paperEntryDisabledCount}",
                    $"missing_risk_bounds_count={missingRiskBoundsCount}",
                ],
                Recommendation: "Complete the GER40 review artifact and re-export the embedded package before treating the asset as fully ready.",
                RequiresValidation: true,
                Assets: ["GER40"],
                Sessions: ["current_system_state"],
                Outcomes: ger40Blocked.Select(signal => signal.SignalStatus).Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
        }

        var xauusdSignals = signalReport.Signals.Where(signal => signal.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)).ToList();
        var xauusdExpired = xauusdSignals.Where(signal => signal.SignalExpired || signal.SignalLifecycleStatus.Equals("expired", StringComparison.OrdinalIgnoreCase)).ToList();
        if (xauusdExpired.Count > 0)
        {
            patterns.Add(new TradingPatternLearningPattern(
                PatternId: "xauusd_signal_expiry_window_review",
                Confidence: xauusdExpired.Count >= 2 ? "high" : "medium",
                SampleSize: xauusdExpired.Count,
                Observation: "XAUUSD still has signals that expire before the paper runtime can execute them reliably.",
                SupportingMetrics:
                [
                    $"signal_count={xauusdSignals.Count}",
                    $"expired_count={xauusdExpired.Count}",
                    $"actionable_count={xauusdSignals.Count(signal => signal.PaperDecision.Equals("would_trigger", StringComparison.OrdinalIgnoreCase))}",
                ],
                Recommendation: "Keep the expiry window under review and validate the export timestamp and session timing before changing entry logic.",
                RequiresValidation: true,
                Assets: ["XAUUSD"],
                Sessions: ["current_system_state"],
                Outcomes: ["expired"]));
        }

        return patterns;
    }

    private static TradingPatternLearningPattern BuildEvolutionTrendPattern(BotEvolutionHistoryReport evolutionReport)
    {
        var metrics = new List<string>
        {
            $"entry_count={evolutionReport.EntryCount}",
            $"best_score={FormatNullableDecimal(evolutionReport.BestScore)}",
            $"worst_score={FormatNullableDecimal(evolutionReport.WorstScore)}",
            $"average_score={FormatNullableDecimal(evolutionReport.AverageScore)}",
            $"biggest_improvement={FormatNullableDecimal(evolutionReport.BiggestImprovement)}",
            $"biggest_regression={FormatNullableDecimal(evolutionReport.BiggestRegression)}",
        };

        var observation = evolutionReport.Trend switch
        {
            "improving" => "The bot evolution history is trending upward, which suggests the latest exports are generally better than earlier ones.",
            "declining" => "The bot evolution history is trending downward, which suggests recent exports should be reviewed before promotion.",
            _ => "The bot evolution history is broadly stable, so changes should be validated before treating them as improvements.",
        };

        var recommendation = evolutionReport.Trend switch
        {
            "improving" => "Use the latest export as the current baseline and keep validating new versions against the saved score history.",
            "declining" => "Avoid automatic recommendation changes until a newer export shows a measurable improvement.",
            _ => "Treat future version changes as hypotheses and verify them against the baseline before recommending a new export.",
        };

        return new TradingPatternLearningPattern(
            PatternId: $"bot_evolution_trend_{evolutionReport.Trend}",
            Confidence: evolutionReport.EntryCount >= 3 ? "high" : evolutionReport.EntryCount >= 2 ? "medium" : "low",
            SampleSize: evolutionReport.EntryCount,
            Observation: observation,
            SupportingMetrics: metrics,
            Recommendation: recommendation,
            RequiresValidation: true,
            Assets: [],
            Sessions: [],
            Outcomes: [evolutionReport.Trend]);
    }

    private static void AddBlockerPattern(
        ICollection<TradingPatternLearningPattern> patterns,
        string patternId,
        IReadOnlyList<PaperSignalEvaluationItem> matchingSignals,
        int totalSignals,
        string observation,
        string recommendation)
    {
        if (matchingSignals.Count == 0)
        {
            return;
        }

        var assets = matchingSignals.Select(signal => signal.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sessions = matchingSignals.Select(signal => signal.SignalLifecycleStatus).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var outcomes = matchingSignals.Select(signal => signal.SignalStatus).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var supportingMetrics = new List<string>
        {
            $"sample_size={matchingSignals.Count}",
            $"share_of_signals={FormatPercent((decimal)matchingSignals.Count / Math.Max(1, totalSignals))}",
            $"assets={string.Join(", ", assets)}",
            $"statuses={string.Join(", ", outcomes)}",
        };

        var confidence = matchingSignals.Count >= 8
            ? "high"
            : matchingSignals.Count >= 3
                ? "medium"
                : "low";

        patterns.Add(new TradingPatternLearningPattern(
            PatternId: patternId,
            Confidence: confidence,
            SampleSize: matchingSignals.Count,
            Observation: observation,
            SupportingMetrics: supportingMetrics,
            Recommendation: recommendation,
            RequiresValidation: true,
            Assets: assets,
            Sessions: ["current_system_state"],
            Outcomes: outcomes));
    }

    private static string DetermineSession(DateTimeOffset openedAtUtc)
    {
        var hour = openedAtUtc.UtcDateTime.Hour;
        return hour switch
        {
            >= 13 and < 17 => "Overlap",
            >= 7 and < 13 => "London",
            >= 17 and < 21 => "New York",
            _ => "Off Session",
        };
    }

    private static IReadOnlyList<string> BuildOutcomeSummary(IEnumerable<PaperTradeHistoryItem> trades)
    {
        return trades
            .Select(trade => trade.Outcome.Equals("tp", StringComparison.OrdinalIgnoreCase)
                ? "tp"
                : trade.Outcome.Equals("sl", StringComparison.OrdinalIgnoreCase)
                    ? "sl"
                    : trade.Outcome.Equals("expired", StringComparison.OrdinalIgnoreCase)
                        ? "expired"
                        : trade.Outcome.Equals("invalidated", StringComparison.OrdinalIgnoreCase)
                            ? "invalidated"
                            : "other")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DetermineConfidence(int sampleSize, decimal avgR, decimal winRate, decimal netR)
    {
        if (sampleSize >= 15 && (avgR > 0.25m || winRate >= 0.60m || netR > 1.5m))
        {
            return "high";
        }

        if (sampleSize >= 8)
        {
            return "medium";
        }

        return "low";
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            _ => 1,
        };
    }

    private static string NormalizeSessionKey(string session)
    {
        return session.ToLowerInvariant().Replace(" ", "_", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPercent(decimal value)
        => $"{Math.Round(value * 100m, 1).ToString("0.0", CultureInfo.InvariantCulture)}%";

    private static string FormatDecimal(decimal value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatNullableDecimal(decimal? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";

    private void WriteReport(TradingPatternLearningReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(TradingPatternLearningReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trading Pattern Learning");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- closed_trade_count: {report.ClosedTradeCount}");
        sb.AppendLine($"- evaluated_signal_count: {report.EvaluatedSignalCount}");
        sb.AppendLine($"- forward_signal_count: {report.ForwardSignalCount}");
        sb.AppendLine($"- bot_evolution_entry_count: {report.BotEvolutionEntryCount}");
        sb.AppendLine($"- pattern_count: {report.PatternCount}");

        if (report.SourceReportPaths.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Source Reports");
            foreach (var sourcePath in report.SourceReportPaths)
            {
                sb.AppendLine($"- {sourcePath}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Pattern Summary");
        foreach (var pattern in report.Patterns)
        {
            sb.AppendLine($"- pattern_id: {pattern.PatternId}");
            sb.AppendLine($"  - confidence: {pattern.Confidence}");
            sb.AppendLine($"  - sample_size: {pattern.SampleSize}");
            sb.AppendLine($"  - observation: {pattern.Observation}");
            sb.AppendLine($"  - supporting_metrics: {string.Join("; ", pattern.SupportingMetrics)}");
            sb.AppendLine($"  - recommendation: {pattern.Recommendation}");
            sb.AppendLine($"  - requires_validation: {pattern.RequiresValidation.ToString().ToLowerInvariant()}");
            if (pattern.Assets.Count > 0)
            {
                sb.AppendLine($"  - assets: {string.Join(", ", pattern.Assets)}");
            }

            if (pattern.Sessions.Count > 0)
            {
                sb.AppendLine($"  - sessions: {string.Join(", ", pattern.Sessions)}");
            }

            if (pattern.Outcomes.Count > 0)
            {
                sb.AppendLine($"  - outcomes: {string.Join(", ", pattern.Outcomes)}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
