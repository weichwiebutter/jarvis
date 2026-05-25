using System.Text.Json;

namespace Hermes.Runtime;

public sealed class TradingDeKnowledgeCatalog
{
    private const string SourceName = "Trading.de";
    private const string SourceTrust = "curated_public_education";

    private readonly StoragePaths _storagePaths;

    public TradingDeKnowledgeCatalog(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string SourcesRoot => Path.Combine(_storagePaths.Root, "strategy_discovery", "sources");

    public string SourcesPath => Path.Combine(SourcesRoot, "trading_de_sources.json");

    public IReadOnlyList<KnowledgeSourceDefinition> LoadOrCreateSources()
    {
        Directory.CreateDirectory(SourcesRoot);
        var sources = Sources();
        File.WriteAllText(SourcesPath, JsonSerializer.Serialize(sources, JsonDefaults.WriteOptions));
        return sources;
    }

    public static IReadOnlyList<KnowledgeSourceDefinition> Sources()
    {
        var curatedAtUtc = DateTimeOffset.UtcNow;
        return
        [
            Source("trading_de_strategies", "https://trading.de/lernen/strategien/", "strategy_overview", ["daytrading", "swing_trading", "scalping", "trend_following", "support_resistance", "news_trading", "gap_trading", "price_action", "smart_money_concepts"], curatedAtUtc),
            Source("trading_de_daytrading_strategies", "https://trading.de/daytrading/daytrading-strategien/", "daytrading", ["daytrading", "scalping", "breakout", "news_trading"], curatedAtUtc),
            Source("trading_de_breakout", "https://trading.de/lernen/strategien/breakout-trading/", "strategy_detail", ["breakout", "support_resistance_breakout", "triangle_breakout"], curatedAtUtc),
            Source("trading_de_engulfing", "https://trading.de/charts/pattern/engulfing/", "candlestick_pattern", ["bullish_engulfing", "bearish_engulfing"], curatedAtUtc),
            Source("trading_de_candlestick_patterns", "https://trading.de/charts/candlestick/candlestick-pattern/", "candlestick_pattern", ["hammer", "shooting_star", "doji", "pin_bar", "engulfing", "inside_bar"], curatedAtUtc),
            Source("trading_de_chart_patterns", "https://trading.de/charts/pattern/", "chart_pattern", ["double_top", "double_bottom", "triangle_breakout", "support_resistance_breakout"], curatedAtUtc),
            Source("trading_de_gold", "https://trading.de/lernen/rohstoff-handel/gold-trading/", "market_context", ["gold_trading", "trend_following", "breakout", "news_trading"], curatedAtUtc),
            Source("trading_de_dax", "https://trading.de/lernen/aktienindizes/dax-trading/", "market_context", ["dax_trading", "daytrading", "gap_trading", "breakout"], curatedAtUtc),
            Source("trading_de_backtesting", "https://trading.de/lernen/backtesting/", "research_method", ["backtesting", "walk_forward", "paper_trading", "data_quality"], curatedAtUtc)
        ];
    }

    public static IReadOnlyList<StrategyPatternDefinition> PatternDefinitions() =>
    [
        Strategy("trend_following", "Trend Following", "both", "Follow confirmed directional market movement.", "trend_following", "https://trading.de/lernen/strategien/", "trend market, Gold, DAX, Forex majors", ["M15", "H1"], "Price aligns with trend direction and closes beyond recent structure.", "Close back inside trend structure or trend filter flips.", "medium", ["strategy_family", "trend", "trading_de"]),
        Strategy("pullback", "Pullback", "both", "Retest after a directional move before continuation.", "pullback", "https://trading.de/lernen/strategien/", "trend market after impulse move", ["M5", "M15", "H1"], "Pullback reaches dynamic/support area and rejects after candle close.", "Close through pullback structure against trend.", "high", ["strategy_family", "pullback", "trading_de"]),
        Strategy("breakout", "Breakout", "both", "Range or level break with continuation potential.", "breakout", "https://trading.de/lernen/strategien/breakout-trading/", "range compression, session open, news volatility", ["M5", "M15", "H1"], "Closed candle breaks resistance/support or compression boundary.", "Failed break and close back inside range.", "high", ["strategy_family", "breakout", "trading_de"]),
        Strategy("support_resistance", "Support Resistance", "both", "Reaction or break around visible support/resistance levels.", "support_resistance", "https://trading.de/lernen/strategien/", "range market or level retest", ["M15", "H1"], "Price rejects or breaks a tested horizontal level.", "Acceptance beyond the opposite side of the level.", "medium", ["strategy_family", "support_resistance", "trading_de"]),
        Strategy("scalping", "Scalping", "both", "Very short-term setup with tight risk and quick exits.", "scalping", "https://trading.de/lernen/strategien/", "liquid sessions, low spread", ["M5"], "Fast momentum or rejection setup appears during liquid session.", "Spread widens or momentum stalls.", "low", ["strategy_family", "scalping", "trading_de"]),
        Strategy("daytrading", "Daytrading", "both", "Intraday setup closed within the trading day.", "daytrading", "https://trading.de/daytrading/daytrading-strategien/", "London/New York sessions, volatile intraday moves", ["M5", "M15"], "Intraday catalyst, level break, or trend continuation confirms.", "Setup loses intraday momentum or session context expires.", "high", ["strategy_family", "daytrading", "trading_de"]),
        Strategy("swing_trading", "Swing Trading", "both", "Multi-session setup based on broader directional context.", "swing_trading", "https://trading.de/lernen/strategien/", "H1/H4 context, slower trend or range rotation", ["H1"], "Higher timeframe structure confirms continuation or reversal zone.", "Higher timeframe structure breaks against setup.", "medium", ["strategy_family", "swing", "trading_de"]),
        Strategy("news_trading", "News Trading", "both", "Volatility setup around scheduled or unexpected news.", "news_trading", "https://trading.de/lernen/strategien/", "high volatility around macro or asset news", ["M5", "M15"], "Post-news candle closes beyond relevant level with volatility confirmation.", "Spread/liquidity filter fails or move fully retraces.", "low", ["strategy_family", "news", "trading_de"]),
        Strategy("gap_trading", "Gap Trading", "both", "Opening gap continuation or fade candidate.", "gap_trading", "https://trading.de/lernen/strategien/", "index/session open, DAX/US indices", ["M5", "M15"], "Open gap forms and first reaction confirms continuation or mean reversion.", "Gap fills/extends against selected scenario.", "medium", ["strategy_family", "gap", "trading_de"]),
        Strategy("price_action", "Price Action", "both", "Chart-only decision based on candle and structure behavior.", "price_action", "https://trading.de/lernen/strategien/", "all liquid markets with clean structure", ["M5", "M15", "H1"], "Candle close, rejection, or structure break confirms setup.", "Invalidating close beyond setup structure.", "high", ["strategy_family", "price_action", "trading_de"]),
        Strategy("smart_money_concepts", "Smart Money Concepts", "both", "Liquidity and structure-focused setup family.", "smart_money_concepts", "https://trading.de/lernen/strategien/", "liquidity sweeps, displacement, structure shifts", ["M5", "M15", "H1"], "Liquidity sweep or structure shift confirms after candle close.", "Price accepts beyond swept level or fails displacement.", "medium", ["strategy_family", "smc", "liquidity", "trading_de"]),
        Strategy("mean_reversion", "Mean Reversion", "both", "Fade stretched moves back toward a reference mean or range midpoint.", "mean_reversion", "https://trading.de/lernen/backtesting/", "range market, stretched candles, failed continuation", ["M5", "M15", "H1"], "Stretch and rejection candle confirm return toward mean.", "Trend continuation acceptance beyond stretch extreme.", "medium", ["strategy_family", "mean_reversion", "backtesting", "trading_de"]),
        Strategy("liquidity_sweep", "Liquidity Sweep", "both", "Sweep of obvious high/low followed by reclaim into structure.", "smart_money_concepts", "https://trading.de/lernen/strategien/", "SMC/liquidity context, session highs/lows", ["M5", "M15"], "Price takes prior high/low and closes back inside structure.", "Acceptance beyond swept level.", "medium", ["strategy_family", "liquidity", "smc", "trading_de"]),
        Strategy("session_breakout", "Session Breakout", "both", "Breakout from session opening range or early session structure.", "daytrading", "https://trading.de/daytrading/daytrading-strategien/", "London/New York session open, DAX/Gold/Forex", ["M5", "M15"], "Session range breaks with candle close and acceptable spread.", "Close back inside session range.", "high", ["strategy_family", "session", "breakout", "trading_de"]),
        Strategy("volatility_expansion", "Volatility Expansion", "both", "Range expansion after compression or catalyst.", "breakout", "https://trading.de/lernen/strategien/breakout-trading/", "compression, news, market open", ["M5", "M15", "H1"], "Candle range expands and closes outside recent structure.", "Range expansion fails and closes back inside prior structure.", "medium", ["strategy_family", "volatility", "breakout", "trading_de"]),
        Pattern("bullish_engulfing", "Bullish Engulfing", "long", "Bullish engulfing candle after downward pressure.", "candlestick_pattern", "https://trading.de/charts/pattern/engulfing/", "reversal or continuation after bearish candle", ["M15", "H1"], "Bullish body engulfs prior bearish body after candle close.", "Break below engulfing low.", "high", ["candlestick", "engulfing", "long", "trading_de"]),
        Pattern("bearish_engulfing", "Bearish Engulfing", "short", "Bearish engulfing candle after upward pressure.", "candlestick_pattern", "https://trading.de/charts/pattern/engulfing/", "reversal or continuation after bullish candle", ["M15", "H1"], "Bearish body engulfs prior bullish body after candle close.", "Break above engulfing high.", "high", ["candlestick", "engulfing", "short", "trading_de"]),
        Pattern("inside_bar", "Inside Bar", "both", "Compression candle fully inside prior candle range.", "candlestick_pattern", "https://trading.de/charts/candlestick/candlestick-pattern/", "compression before continuation or reversal", ["M5", "M15", "H1"], "Inside candle forms and later breaks prior high/low.", "Breakout closes back inside inside-bar range.", "medium", ["candlestick", "inside_bar", "compression", "trading_de"]),
        Pattern("pin_bar", "Pin Bar", "both", "Long wick rejection candle around a level.", "candlestick_pattern", "https://trading.de/charts/candlestick/candlestick-pattern/", "support/resistance or liquidity rejection", ["M5", "M15", "H1"], "Long wick rejects level and candle closes away from wick.", "Price accepts beyond wick extreme.", "medium", ["candlestick", "pin_bar", "rejection", "trading_de"]),
        Pattern("doji", "Doji", "both", "Indecision candle used as context filter, not standalone signal.", "candlestick_pattern", "https://trading.de/charts/candlestick/candlestick-pattern/", "transition, exhaustion, or low conviction area", ["M15", "H1"], "Small body indicates indecision near level or after stretch.", "Next candle closes decisively against setup thesis.", "low", ["candlestick", "doji", "filter", "trading_de"]),
        Pattern("hammer", "Hammer", "long", "Bullish rejection candle with long lower wick.", "candlestick_pattern", "https://trading.de/charts/candlestick/candlestick-pattern/", "after downward move or support sweep", ["M15", "H1"], "Small body and long lower wick after bearish pressure.", "Break below hammer low.", "medium", ["candlestick", "hammer", "long", "trading_de"]),
        Pattern("shooting_star", "Shooting Star", "short", "Bearish rejection candle with long upper wick.", "candlestick_pattern", "https://trading.de/charts/candlestick/candlestick-pattern/", "after upward move or resistance sweep", ["M15", "H1"], "Small body and long upper wick after bullish pressure.", "Break above shooting-star high.", "medium", ["candlestick", "shooting_star", "short", "trading_de"]),
        Pattern("double_top", "Double Top", "short", "Two failed pushes into similar resistance.", "chart_pattern", "https://trading.de/charts/pattern/", "range high or trend exhaustion", ["M15", "H1"], "Second top rejects and neckline/structure breaks.", "Acceptance above second top.", "medium", ["chart_pattern", "double_top", "short", "trading_de"]),
        Pattern("double_bottom", "Double Bottom", "long", "Two failed pushes into similar support.", "chart_pattern", "https://trading.de/charts/pattern/", "range low or trend exhaustion", ["M15", "H1"], "Second bottom rejects and neckline/structure breaks.", "Acceptance below second bottom.", "medium", ["chart_pattern", "double_bottom", "long", "trading_de"]),
        Pattern("triangle_breakout", "Triangle Breakout", "both", "Compression breakout from triangle-like structure.", "chart_pattern", "https://trading.de/charts/pattern/", "volatility compression before expansion", ["M15", "H1"], "Closed candle breaks triangle boundary with range expansion.", "Close back inside triangle boundary.", "medium", ["chart_pattern", "triangle", "breakout", "trading_de"]),
        Pattern("range_breakout", "Range Breakout", "both", "Breakout from a horizontal consolidation range.", "chart_pattern", "https://trading.de/charts/pattern/", "range compression with tested boundaries", ["M5", "M15", "H1"], "Closed candle breaks range high/low with continuation pressure.", "Close back inside the range.", "high", ["chart_pattern", "range", "breakout", "trading_de"]),
        Pattern("support_resistance_breakout", "Support Resistance Breakout", "both", "Breakout through tested horizontal support/resistance.", "chart_pattern", "https://trading.de/charts/pattern/", "tested level with pending liquidity", ["M5", "M15", "H1"], "Closed candle breaks tested support/resistance level.", "Failed break and close back through level.", "high", ["chart_pattern", "support_resistance", "breakout", "trading_de"])
    ];

    private static KnowledgeSourceDefinition Source(
        string id,
        string url,
        string category,
        IReadOnlyList<string> concepts,
        DateTimeOffset curatedAtUtc) =>
        new(id, SourceName, url, SourceTrust, category, concepts, curatedAtUtc);

    private static StrategyPatternDefinition Strategy(
        string id,
        string name,
        string directionBias,
        string description,
        string category,
        string sourceUrl,
        string marketContext,
        IReadOnlyList<string> timeframes,
        string trigger,
        string invalidation,
        string priority,
        IReadOnlyList<string> tags) =>
        Entry(id, name, directionBias, description, category, sourceUrl, marketContext, timeframes, trigger, invalidation, priority, tags);

    private static StrategyPatternDefinition Pattern(
        string id,
        string name,
        string directionBias,
        string description,
        string category,
        string sourceUrl,
        string marketContext,
        IReadOnlyList<string> timeframes,
        string trigger,
        string invalidation,
        string priority,
        IReadOnlyList<string> tags) =>
        Entry(id, name, directionBias, description, category, sourceUrl, marketContext, timeframes, trigger, invalidation, priority, tags);

    private static StrategyPatternDefinition Entry(
        string id,
        string name,
        string directionBias,
        string description,
        string category,
        string sourceUrl,
        string marketContext,
        IReadOnlyList<string> timeframes,
        string trigger,
        string invalidation,
        string priority,
        IReadOnlyList<string> tags) =>
        new(
            Id: id,
            Name: name,
            DirectionBias: directionBias,
            Description: description,
            RequiredTimeframes: timeframes,
            PreferredSessions: category is "daytrading" or "scalping" ? ["london", "new_york"] : ["london", "london_new_york_overlap", "new_york"],
            MarketRegimes: MarketRegimesFor(id, category),
            TriggerRules: [new PatternRuleStub($"{id}_trigger", trigger, ["open", "high", "low", "close", "session", "regime"], StubOnly: true)],
            InvalidationRules: [new PatternRuleStub($"{id}_invalidation", invalidation, ["close", "high", "low", "structure"], StubOnly: true)],
            RiskModelHint: RiskHintFor(id, category),
            Tags: tags.Select(tag => new PatternTag(tag, tag.Replace('_', ' '))).ToList(),
            SourceUrl: sourceUrl,
            SourceName: SourceName,
            Category: category,
            DescriptionShort: description,
            MarketContext: marketContext,
            PossibleTimeframes: timeframes,
            TriggerRuleStub: trigger,
            InvalidationRuleStub: invalidation,
            TestPriority: priority,
            SourceTrust: SourceTrust);

    private static IReadOnlyList<string> MarketRegimesFor(string id, string category)
    {
        if (id.Contains("breakout", StringComparison.OrdinalIgnoreCase) || category == "breakout")
        {
            return ["range", "trend_up", "trend_down", "high_volatility"];
        }

        if (id.Contains("reversion", StringComparison.OrdinalIgnoreCase) || id is "double_top" or "double_bottom")
        {
            return ["range", "high_volatility"];
        }

        if (category == "strategy_family" && (id is "trend_following" or "pullback" or "swing_trading"))
        {
            return ["trend_up", "trend_down"];
        }

        return ["range", "trend_up", "trend_down", "high_volatility"];
    }

    private static string RiskHintFor(string id, string category)
    {
        if (id.Contains("breakout", StringComparison.OrdinalIgnoreCase))
        {
            return "Stop inside broken range or beyond invalidation level; require close confirmation.";
        }

        if (category.Contains("candlestick", StringComparison.OrdinalIgnoreCase))
        {
            return "Stop beyond candle extreme; confirm with context and avoid standalone execution.";
        }

        return "Use predefined invalidation level; no live use without backtest and human review.";
    }
}
