namespace Hermes.Runtime;

public sealed class CTraderHistoricalDataClientStub
{
    private const int MaxStubCandles = 500;

    private readonly CTraderOpenApiConfig _config;
    private readonly CTraderSymbolMapper _symbolMapper;

    public CTraderHistoricalDataClientStub(
        CTraderOpenApiConfig config,
        CTraderSymbolMapper symbolMapper)
    {
        _config = config;
        _symbolMapper = symbolMapper;
    }

    public CTraderConnectionHealth CheckHealth()
    {
        var warnings = new List<string>
        {
            "Open API connector stub active. No live cTrader connection was opened.",
            "OAuth/token handling is not implemented in foundation v1.",
            "Historical downloads are deterministic demo data until a real read-only client is added."
        };
        if (!_config.NoOrders)
        {
            warnings.Add("Invalid local config: no_orders must remain true for connector foundation v1.");
        }

        return new CTraderConnectionHealth(
            TimestampUtc: DateTimeOffset.UtcNow,
            Status: !_config.NoOrders
                ? "config_invalid_no_orders_false"
                : _config.StubMode
                    ? "stub_ready"
                    : "not_connected",
            Environment: _config.Environment,
            StubActive: true,
            AuthConfigured: false,
            ClientIdConfigured: IsConfiguredValue(_config.ClientId, "example_client_id"),
            AccountIdConfigured: !string.IsNullOrWhiteSpace(_config.AccountId),
            NoOrders: _config.NoOrders,
            ReadOnlyMarketData: _config.NoOrders,
            Warnings: warnings);
    }

    public IReadOnlyList<MarketDataCandle> DownloadHistoricalCandles(CTraderHistoricalDataRequest request)
    {
        if (!_config.NoOrders)
        {
            throw new InvalidOperationException("Invalid cTrader config: no_orders must be true for connector foundation v1.");
        }

        if (!_symbolMapper.TryMap(request.Symbol, out var mapping))
        {
            throw new InvalidOperationException($"Unsupported cTrader symbol mapping: {request.Symbol}");
        }

        if (!_config.AllowedTimeframes.Contains(request.Timeframe, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported cTrader timeframe: {request.Timeframe}");
        }

        if (request.ToUtc < request.FromUtc)
        {
            throw new InvalidOperationException("Invalid time range: --to must be greater than or equal to --from.");
        }

        var interval = TimeframeInterval(request.Timeframe);
        var candles = new List<MarketDataCandle>();
        var timestamp = request.FromUtc;
        var index = 0;

        while (timestamp <= request.ToUtc && candles.Count < MaxStubCandles)
        {
            candles.Add(CreateStubCandle(mapping.HermesSymbol, request.Timeframe, timestamp, index));
            timestamp = timestamp.Add(interval);
            index++;
        }

        return candles;
    }

    private static bool IsConfiguredValue(string value, string placeholder)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
    }

    private static MarketDataCandle CreateStubCandle(
        string symbol,
        string timeframe,
        DateTimeOffset timestampUtc,
        int index)
    {
        var basePrice = BasePrice(symbol);
        var range = CandleRange(symbol, timeframe);
        var wave = Math.Sin(index / 3.0) * range * 0.32;
        var drift = index * range * 0.035;
        var open = basePrice + drift + wave;
        var direction = index % 3 == 0 ? 1.0 : -0.55;
        var close = open + (range * direction * 0.22);
        var high = Math.Max(open, close) + (range * 0.24);
        var low = Math.Min(open, close) - (range * 0.2);
        var volume = BaseVolume(symbol) + (index * 3);

        return new MarketDataCandle(
            TimestampUtc: timestampUtc.ToUniversalTime(),
            Open: RoundPrice(symbol, open),
            High: RoundPrice(symbol, high),
            Low: RoundPrice(symbol, low),
            Close: RoundPrice(symbol, close),
            Volume: Math.Round(volume, 2),
            Symbol: symbol,
            Timeframe: timeframe.ToUpperInvariant());
    }

    private static TimeSpan TimeframeInterval(string timeframe) =>
        timeframe.ToUpperInvariant() switch
        {
            "H4" => TimeSpan.FromHours(4),
            "H1" => TimeSpan.FromHours(1),
            "M15" => TimeSpan.FromMinutes(15),
            "M5" => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(5)
        };

    private static double BasePrice(string symbol) =>
        symbol.ToUpperInvariant() switch
        {
            "XAUUSD" => 2392.40,
            "EURUSD" => 1.08420,
            "GER40" => 18420.0,
            "US500" => 5280.0,
            _ => 100.0
        };

    private static double CandleRange(string symbol, string timeframe)
    {
        var baseRange = symbol.ToUpperInvariant() switch
        {
            "XAUUSD" => 2.6,
            "EURUSD" => 0.00048,
            "GER40" => 31.0,
            "US500" => 8.5,
            _ => 1.0
        };

        var timeframeFactor = timeframe.ToUpperInvariant() switch
        {
            "H4" => 2.4,
            "H1" => 1.5,
            "M15" => 0.8,
            "M5" => 0.45,
            _ => 1.0
        };

        return baseRange * timeframeFactor;
    }

    private static double BaseVolume(string symbol) =>
        symbol.ToUpperInvariant() switch
        {
            "XAUUSD" => 1200,
            "EURUSD" => 900,
            "GER40" => 700,
            "US500" => 820,
            _ => 100
        };

    private static double RoundPrice(string symbol, double value) =>
        symbol.ToUpperInvariant() switch
        {
            "EURUSD" => Math.Round(value, 5),
            "GER40" or "US500" => Math.Round(value, 1),
            _ => Math.Round(value, 2)
        };
}
