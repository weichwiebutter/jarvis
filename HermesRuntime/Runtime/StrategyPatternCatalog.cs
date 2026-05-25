using System.Text.Json;

namespace Hermes.Runtime;

public sealed class StrategyPatternCatalog
{
    private readonly StoragePaths _storagePaths;

    public StrategyPatternCatalog(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StrategyResearchRoot => Path.Combine(_storagePaths.Root, "strategy_research");

    public string CatalogPath => Path.Combine(StrategyResearchRoot, "pattern_catalog.json");

    public IReadOnlyList<StrategyPatternDefinition> LoadOrCreateCatalog()
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        var existing = LoadCatalog();
        var defaults = DefaultPatterns();
        var mergedById = defaults.ToDictionary(
            pattern => pattern.Id,
            pattern => pattern,
            StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in existing)
        {
            if (mergedById.TryGetValue(pattern.Id, out var curated)
                && ShouldPreferCurated(curated, pattern))
            {
                continue;
            }

            mergedById[pattern.Id] = pattern;
        }

        var merged = mergedById.Values
            .OrderBy(pattern => pattern.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(CatalogPath, JsonSerializer.Serialize(merged, JsonDefaults.WriteOptions));
        return merged;
    }

    public IReadOnlyList<StrategyPatternDefinition> LoadCatalog()
    {
        if (!File.Exists(CatalogPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<StrategyPatternDefinition>>(
                File.ReadAllText(CatalogPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    public static string PatternName(
        IEnumerable<StrategyPatternDefinition> patterns,
        string? patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return "-";
        }

        return patterns.FirstOrDefault(
                pattern => pattern.Id.Equals(patternId, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? patternId;
    }

    public static string StrategyFamilyForPattern(string patternId) =>
        patternId switch
        {
            "inside_bar_breakout" or "first_candle_breakout" => "breakout",
            "breakout" or "support_resistance_breakout" or "triangle_breakout" or "gap_trading" or "session_breakout" or "volatility_expansion" or "range_breakout" => "breakout",
            "breakout_continuation" or "trend_following" or "swing_trading" => "trend_continuation",
            "mean_reversion" or "mean_reversion_rejection" or "liquidity_sweep" or "liquidity_sweep_reversal" or "support_resistance" or "smart_money_concepts" or "double_top" or "double_bottom" => "mean_reversion",
            "bullish_engulfing" or "bearish_engulfing" or "ema_pullback" or "pullback" or "price_action" or "inside_bar" or "pin_bar" or "doji" or "hammer" or "shooting_star" => "ema_pullback",
            "scalping" or "daytrading" or "news_trading" => "trend_continuation",
            _ => "ema_pullback"
        };

    public static IReadOnlyList<StrategyPatternDefinition> DefaultPatterns() =>
        BasePatterns()
            .Concat(TradingDeKnowledgeCatalog.PatternDefinitions())
            .GroupBy(pattern => pattern.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(pattern => pattern.SourceName?.Equals("Trading.de", StringComparison.OrdinalIgnoreCase) == true)
                .First())
            .OrderBy(pattern => pattern.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<StrategyPatternDefinition> BasePatterns() =>
    [
        new(
            Id: "bearish_engulfing",
            Name: "Bearish Engulfing",
            DirectionBias: "short",
            Description: "Bearish reversal or continuation candle pattern after an upward push.",
            RequiredTimeframes: ["M15", "H1"],
            PreferredSessions: ["london", "new_york"],
            MarketRegimes: ["trend_down", "range", "high_volatility"],
            TriggerRules:
            [
                Rule("engulfing_body", "Current bearish body fully covers previous bullish body.", ["open", "close", "previous_open", "previous_close"]),
                Rule("close_confirmation", "Signal is only considered after candle close.", ["timestamp_utc", "close"])
            ],
            InvalidationRules:
            [
                Rule("engulfing_high_break", "Pattern invalidates if price breaks above engulfing candle high.", ["high", "current_price"])
            ],
            RiskModelHint: "Stop above engulfing high; target prior liquidity or 1.5R+.",
            Tags: [Tag("candlestick"), Tag("short"), Tag("reversal")]),
        new(
            Id: "breakout_continuation",
            Name: "Breakout Continuation",
            DirectionBias: "both",
            Description: "Continuation after range expansion in the direction of the active regime.",
            RequiredTimeframes: ["M5", "M15", "H1"],
            PreferredSessions: ["london", "london_new_york_overlap", "new_york"],
            MarketRegimes: ["trend_up", "trend_down", "high_volatility"],
            TriggerRules:
            [
                Rule("range_expansion", "Candle range expands beyond recent compression.", ["candle_range", "atr"]),
                Rule("directional_close", "Close holds in breakout direction after candle close.", ["close", "high", "low"])
            ],
            InvalidationRules:
            [
                Rule("range_reentry", "Breakout invalidates if price closes back inside the prior range.", ["close", "prior_range"])
            ],
            RiskModelHint: "Stop inside broken range; prefer partials near first measured move.",
            Tags: [Tag("breakout"), Tag("continuation"), Tag("trend")]),
        new(
            Id: "bullish_engulfing",
            Name: "Bullish Engulfing",
            DirectionBias: "long",
            Description: "Bullish reversal or continuation candle pattern after a downward push.",
            RequiredTimeframes: ["M15", "H1"],
            PreferredSessions: ["london", "new_york"],
            MarketRegimes: ["trend_up", "range", "high_volatility"],
            TriggerRules:
            [
                Rule("engulfing_body", "Current bullish body fully covers previous bearish body.", ["open", "close", "previous_open", "previous_close"]),
                Rule("close_confirmation", "Signal is only considered after candle close.", ["timestamp_utc", "close"])
            ],
            InvalidationRules:
            [
                Rule("engulfing_low_break", "Pattern invalidates if price breaks below engulfing candle low.", ["low", "current_price"])
            ],
            RiskModelHint: "Stop below engulfing low; target prior liquidity or 1.5R+.",
            Tags: [Tag("candlestick"), Tag("long"), Tag("reversal")]),
        new(
            Id: "ema_pullback",
            Name: "EMA Pullback",
            DirectionBias: "both",
            Description: "Pullback into EMA area with continuation confirmation.",
            RequiredTimeframes: ["M5", "M15", "H1"],
            PreferredSessions: ["london", "london_new_york_overlap"],
            MarketRegimes: ["trend_up", "trend_down"],
            TriggerRules:
            [
                Rule("ema_area_retest", "Price retraces toward fast/slow EMA zone.", ["fast_ema", "slow_ema", "close"]),
                Rule("continuation_rejection", "Rejection candle appears in trend direction.", ["direction", "body_size"])
            ],
            InvalidationRules:
            [
                Rule("ema_zone_failure", "Setup invalidates if candle closes through the EMA zone against trend.", ["close", "fast_ema", "slow_ema"])
            ],
            RiskModelHint: "Stop beyond pullback swing; RR 1.4-2.2 preferred.",
            Tags: [Tag("pullback"), Tag("trend"), Tag("ema")]),
        new(
            Id: "first_candle_breakout",
            Name: "First Candle Breakout",
            DirectionBias: "both",
            Description: "Session-opening candle range breakout, especially London or New York.",
            RequiredTimeframes: ["M5", "M15"],
            PreferredSessions: ["london", "new_york"],
            MarketRegimes: ["trend_up", "trend_down", "high_volatility"],
            TriggerRules:
            [
                Rule("session_range_break", "Price breaks the first session candle high or low.", ["session", "high", "low", "close"]),
                Rule("spread_filter", "Spread/liquidity filter must be acceptable.", ["spread", "session"])
            ],
            InvalidationRules:
            [
                Rule("session_range_reentry", "Breakout invalidates when price closes back inside opening range.", ["close", "opening_range"])
            ],
            RiskModelHint: "Stop inside opening range; avoid late-session low-liquidity breakouts.",
            Tags: [Tag("breakout"), Tag("session"), Tag("london")]),
        new(
            Id: "inside_bar_breakout",
            Name: "Inside Bar Breakout",
            DirectionBias: "both",
            Description: "Breakout from an inside bar compression pattern.",
            RequiredTimeframes: ["M5", "M15", "H1"],
            PreferredSessions: ["london", "new_york"],
            MarketRegimes: ["range", "trend_up", "trend_down"],
            TriggerRules:
            [
                Rule("inside_bar_range", "Current candle remains inside prior candle range.", ["high", "low", "previous_high", "previous_low"]),
                Rule("breakout_close", "Entry only after break and close outside inside-bar range.", ["close", "inside_bar_high", "inside_bar_low"])
            ],
            InvalidationRules:
            [
                Rule("failed_inside_break", "Invalidates if breakout immediately closes back inside range.", ["close", "inside_bar_range"])
            ],
            RiskModelHint: "Stop opposite side of inside-bar range; reduce size in choppy regimes.",
            Tags: [Tag("breakout"), Tag("compression"), Tag("pattern")]),
        new(
            Id: "liquidity_sweep_reversal",
            Name: "Liquidity Sweep Reversal",
            DirectionBias: "both",
            Description: "Sweep of recent high/low followed by rejection back into the prior range.",
            RequiredTimeframes: ["M5", "M15", "H1"],
            PreferredSessions: ["london", "london_new_york_overlap", "new_york"],
            MarketRegimes: ["range", "high_volatility"],
            TriggerRules:
            [
                Rule("sweep_extreme", "Price takes a recent high/low and rejects.", ["high", "low", "previous_swing"]),
                Rule("reclaim_close", "Candle closes back inside prior structure.", ["close", "market_structure"])
            ],
            InvalidationRules:
            [
                Rule("sweep_continuation", "Invalidates if price accepts beyond swept level.", ["close", "swept_level"])
            ],
            RiskModelHint: "Stop beyond swept level; prefer conservative RR until validated.",
            Tags: [Tag("liquidity"), Tag("reversal"), Tag("mean_reversion")]),
        new(
            Id: "mean_reversion_rejection",
            Name: "Mean Reversion Rejection",
            DirectionBias: "both",
            Description: "Rejection from stretched move back toward range mean.",
            RequiredTimeframes: ["M5", "M15"],
            PreferredSessions: ["london", "new_york"],
            MarketRegimes: ["range", "high_volatility"],
            TriggerRules:
            [
                Rule("stretch_detected", "Move is stretched relative to recent candle range.", ["simple_return", "candle_range"]),
                Rule("rejection_close", "Candle closes back toward range mean.", ["close", "body_size", "direction"])
            ],
            InvalidationRules:
            [
                Rule("trend_acceptance", "Invalidates if the stretched move continues with acceptance.", ["close", "market_regime"])
            ],
            RiskModelHint: "Stop beyond rejection extreme; avoid strong trend continuation regimes.",
            Tags: [Tag("mean_reversion"), Tag("rejection"), Tag("range")])
    ];

    private static bool ShouldPreferCurated(
        StrategyPatternDefinition curated,
        StrategyPatternDefinition existing)
    {
        return !string.IsNullOrWhiteSpace(curated.SourceUrl)
            && (string.IsNullOrWhiteSpace(existing.SourceUrl)
                || string.IsNullOrWhiteSpace(existing.SourceTrust)
                || !existing.SourceTrust.Equals("curated_public_education", StringComparison.OrdinalIgnoreCase));
    }

    private static PatternRuleStub Rule(string id, string description, IReadOnlyList<string> inputs) =>
        new(id, description, inputs, StubOnly: true);

    private static PatternTag Tag(string id) =>
        new(id, id.Replace('_', ' '));
}
