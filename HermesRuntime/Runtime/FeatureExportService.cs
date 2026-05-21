using System.Text.Json;

namespace Hermes.Runtime;

public sealed class FeatureExportService
{
    private static readonly string[] DemoSymbols = ["XAUUSD", "EURUSD", "GER40"];

    private readonly StoragePaths _storagePaths;

    public FeatureExportService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public FeatureExportResult CreateDemoFeatureExport(string exportId)
    {
        var exportsRoot = Path.Combine(_storagePaths.Root, "exports");
        var featureDirectory = Path.Combine(exportsRoot, "features");
        var signalDirectory = Path.Combine(exportsRoot, "signals");
        Directory.CreateDirectory(featureDirectory);
        Directory.CreateDirectory(signalDirectory);

        var createdAtUtc = DateTimeOffset.UtcNow;
        var featureVectors = CreateFeatureVectors(createdAtUtc);
        var signalResults = CreateSignalResults(createdAtUtc);

        var featureOutputPath = Path.Combine(featureDirectory, $"{exportId}.features.jsonl");
        var signalOutputPath = Path.Combine(signalDirectory, $"{exportId}.signals.jsonl");

        WriteJsonl(featureOutputPath, featureVectors);
        WriteJsonl(signalOutputPath, signalResults);

        return new FeatureExportResult(
            FeatureOutputPath: featureOutputPath,
            SignalOutputPath: signalOutputPath,
            FeatureRowsWritten: featureVectors.Count,
            SignalRowsWritten: signalResults.Count,
            Symbols: DemoSymbols);
    }

    private static IReadOnlyList<FeatureVector> CreateFeatureVectors(DateTimeOffset timestampUtc)
    {
        return
        [
            new FeatureVector(
                TimestampUtc: timestampUtc.AddMinutes(-15),
                Symbol: "XAUUSD",
                Timeframe: "M15",
                Session: "London/New York overlap",
                H4Regime: "trend_up",
                H1Bias: "long",
                M15Setup: "pullback_rejection",
                M5Trigger: "higher_low_break",
                Adx: 27.8,
                Atr: 3.42,
                Rsi: 58.4,
                StructureState: "higher_high_higher_low",
                PatternCandidate: "trend_pullback",
                SignalScore: 0.74,
                Spread: 0.28),
            new FeatureVector(
                TimestampUtc: timestampUtc.AddMinutes(-10),
                Symbol: "EURUSD",
                Timeframe: "M15",
                Session: "London",
                H4Regime: "range",
                H1Bias: "neutral",
                M15Setup: "mean_reversion_watch",
                M5Trigger: "none",
                Adx: 16.2,
                Atr: 0.00072,
                Rsi: 49.1,
                StructureState: "range_mid",
                PatternCandidate: "no_trade_filter",
                SignalScore: 0.38,
                Spread: 0.00008),
            new FeatureVector(
                TimestampUtc: timestampUtc.AddMinutes(-5),
                Symbol: "GER40",
                Timeframe: "M15",
                Session: "Europe",
                H4Regime: "high_volatility",
                H1Bias: "short_watch",
                M15Setup: "breakout_retest",
                M5Trigger: "liquidity_sweep",
                Adx: 31.5,
                Atr: 42.7,
                Rsi: 43.6,
                StructureState: "lower_high_pressure",
                PatternCandidate: "breakout",
                SignalScore: 0.63,
                Spread: 1.4)
        ];
    }

    private static IReadOnlyList<SignalResult> CreateSignalResults(DateTimeOffset timestampUtc)
    {
        return
        [
            new SignalResult(
                TimestampUtc: timestampUtc.AddMinutes(-15),
                Symbol: "XAUUSD",
                Direction: "long",
                SignalType: "setup_watch",
                Score: 0.74,
                Confidence: 0.68,
                TheoreticalEntry: 2392.40,
                TheoreticalStop: 2386.80,
                TheoreticalTarget: 2404.00,
                ReasonCodes:
                [
                    "h4_trend_up",
                    "h1_long_bias",
                    "m15_pullback_rejection",
                    "no_auto_trading"
                ]),
            new SignalResult(
                TimestampUtc: timestampUtc.AddMinutes(-10),
                Symbol: "EURUSD",
                Direction: "neutral",
                SignalType: "no_trade_filter",
                Score: 0.38,
                Confidence: 0.41,
                TheoreticalEntry: 1.0842,
                TheoreticalStop: 1.0814,
                TheoreticalTarget: 1.0895,
                ReasonCodes:
                [
                    "range_mid",
                    "low_adx",
                    "trigger_missing",
                    "human_review_required"
                ]),
            new SignalResult(
                TimestampUtc: timestampUtc.AddMinutes(-5),
                Symbol: "GER40",
                Direction: "short_watch",
                SignalType: "possible_breakout",
                Score: 0.63,
                Confidence: 0.57,
                TheoreticalEntry: 18420.0,
                TheoreticalStop: 18488.0,
                TheoreticalTarget: 18280.0,
                ReasonCodes:
                [
                    "high_volatility",
                    "breakout_candidate",
                    "spread_guard_required",
                    "no_order_execution"
                ])
        ];
    }

    private static void WriteJsonl<T>(string path, IEnumerable<T> rows)
    {
        File.WriteAllLines(
            path,
            rows.Select(row => JsonSerializer.Serialize(row, JsonDefaults.WriteOptions)));
    }
}
