using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CTraderOpenApiHistoricalDataClient : ICTraderHistoricalDataClient
{
    private const int ApplicationAuthReq = 2100;
    private const int ApplicationAuthRes = 2101;
    private const int AccountAuthReq = 2102;
    private const int AccountAuthRes = 2103;
    private const int SymbolsListReq = 2114;
    private const int SymbolsListRes = 2115;
    private const int GetTrendbarsReq = 2137;
    private const int GetTrendbarsRes = 2138;
    private const int ErrorRes = 2142;
    private const int GetAccountsByAccessTokenReq = 2149;
    private const int GetAccountsByAccessTokenRes = 2150;
    private const int HeartbeatEvent = 51;

    private readonly CTraderOpenApiConfig _config;
    private readonly CTraderSymbolMapper _symbolMapper;
    private readonly CTraderStoredToken _token;

    public CTraderOpenApiHistoricalDataClient(
        CTraderOpenApiConfig config,
        CTraderSymbolMapper symbolMapper,
        CTraderStoredToken token)
    {
        _config = config;
        _symbolMapper = symbolMapper;
        _token = token;
    }

    public CTraderConnectionHealth CheckHealth()
    {
        var warnings = new List<string>();
        if (!IsConfiguredValue(_config.ClientId, "example_client_id"))
        {
            warnings.Add("cTrader client_id is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_config.ClientSecret))
        {
            warnings.Add("cTrader client_secret is not configured locally.");
        }

        if (!_token.HasAccessToken)
        {
            warnings.Add("cTrader token store is missing an access token.");
        }

        if (!_config.NoOrders)
        {
            warnings.Add("Invalid cTrader config: no_orders must remain true.");
        }

        if (!_config.ReadOnlyMarketData)
        {
            warnings.Add("Invalid cTrader config: read_only_market_data must remain true.");
        }

        return new CTraderConnectionHealth(
            TimestampUtc: DateTimeOffset.UtcNow,
            Status: warnings.Count == 0 ? "real_client_ready" : "auth_or_config_incomplete",
            Environment: _config.Environment,
            StubActive: false,
            AuthConfigured: _token.HasAccessToken,
            ClientIdConfigured: IsConfiguredValue(_config.ClientId, "example_client_id"),
            AccountIdConfigured: !string.IsNullOrWhiteSpace(_config.AccountId),
            NoOrders: _config.NoOrders,
            ReadOnlyMarketData: _config.NoOrders && _config.ReadOnlyMarketData,
            Warnings: warnings);
    }

    public IReadOnlyList<MarketDataCandle> DownloadHistoricalCandles(CTraderHistoricalDataRequest request)
    {
        return DownloadHistoricalCandlesAsync(request).GetAwaiter().GetResult();
    }

    private async Task<IReadOnlyList<MarketDataCandle>> DownloadHistoricalCandlesAsync(CTraderHistoricalDataRequest request)
    {
        ValidateRequest(request);

        using var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds()));
        await webSocket.ConnectAsync(BuildEndpointUri(), cts.Token).ConfigureAwait(false);

        await SendAsync(webSocket, ApplicationAuthReq, new
        {
            clientId = _config.ClientId,
            clientSecret = _config.ClientSecret
        }, cts.Token).ConfigureAwait(false);
        using (await ReceiveExpectedAsync(webSocket, cts.Token, ApplicationAuthRes).ConfigureAwait(false))
        {
        }

        var accountId = await ResolveAccountIdAsync(webSocket, cts.Token).ConfigureAwait(false);

        await SendAsync(webSocket, AccountAuthReq, new
        {
            ctidTraderAccountId = accountId,
            accessToken = _token.AccessToken
        }, cts.Token).ConfigureAwait(false);
        using (await ReceiveExpectedAsync(webSocket, cts.Token, AccountAuthRes).ConfigureAwait(false))
        {
        }

        var symbol = await ResolveSymbolAsync(webSocket, accountId, request.Symbol, cts.Token).ConfigureAwait(false);
        var trendbars = await RequestTrendbarsAsync(webSocket, accountId, symbol.SymbolId, request, cts.Token).ConfigureAwait(false);

        return trendbars
            .Select(trendbar => ToCandle(trendbar, request.Symbol, request.Timeframe))
            .OrderBy(candle => candle.TimestampUtc)
            .ToList();
    }

    private async Task<long> ResolveAccountIdAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        if (long.TryParse(_config.AccountId, out var configuredAccountId) && configuredAccountId > 0)
        {
            return configuredAccountId;
        }

        await SendAsync(webSocket, GetAccountsByAccessTokenReq, new
        {
            accessToken = _token.AccessToken
        }, cancellationToken).ConfigureAwait(false);

        using var document = await ReceiveExpectedAsync(webSocket, cancellationToken, GetAccountsByAccessTokenRes).ConfigureAwait(false);
        var payload = GetPayload(document.RootElement);
        if (!TryGetProperty(payload, out var accounts, "ctidTraderAccount", "ctid_trader_account")
            || accounts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("cTrader account list response did not contain ctidTraderAccount entries.");
        }

        var environmentIsLive = _config.Environment.Equals("live", StringComparison.OrdinalIgnoreCase);
        long? fallbackAccountId = null;
        foreach (var account in accounts.EnumerateArray())
        {
            var accountId = ReadLong(account, "ctidTraderAccountId", "ctid_trader_account_id");
            if (accountId is null or <= 0)
            {
                continue;
            }

            fallbackAccountId ??= accountId.Value;
            var isLive = ReadBool(account, "isLive", "is_live");
            if (isLive is not null && isLive == environmentIsLive)
            {
                return accountId.Value;
            }
        }

        return fallbackAccountId
            ?? throw new InvalidOperationException("No cTrader account ID was returned for this access token. Configure account_id locally if needed.");
    }

    private async Task<CTraderResolvedSymbol> ResolveSymbolAsync(
        ClientWebSocket webSocket,
        long accountId,
        string requestedSymbol,
        CancellationToken cancellationToken)
    {
        if (!_symbolMapper.TryMap(requestedSymbol, out var mapping))
        {
            throw new InvalidOperationException($"Unsupported cTrader symbol mapping: {requestedSymbol}");
        }

        await SendAsync(webSocket, SymbolsListReq, new
        {
            ctidTraderAccountId = accountId,
            includeArchivedSymbols = false
        }, cancellationToken).ConfigureAwait(false);

        using var document = await ReceiveExpectedAsync(webSocket, cancellationToken, SymbolsListRes).ConfigureAwait(false);
        var payload = GetPayload(document.RootElement);
        if (!TryGetProperty(payload, out var symbols, "symbol") || symbols.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("cTrader symbols response did not contain a symbol list.");
        }

        var expectedNames = new[] { mapping.HermesSymbol, mapping.CTraderSymbolName }
            .Concat(mapping.Aliases)
            .Select(NormalizeSymbolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CTraderResolvedSymbol? fuzzyMatch = null;
        foreach (var symbol in symbols.EnumerateArray())
        {
            var name = ReadString(symbol, "symbolName", "symbol_name", "name");
            var symbolId = ReadLong(symbol, "symbolId", "symbol_id");
            if (string.IsNullOrWhiteSpace(name) || symbolId is null or <= 0)
            {
                continue;
            }

            var normalizedName = NormalizeSymbolName(name);
            if (expectedNames.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
            {
                return new CTraderResolvedSymbol(symbolId.Value, name);
            }

            if (expectedNames.Any(expected => normalizedName.StartsWith(expected, StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains(expected, StringComparison.OrdinalIgnoreCase)))
            {
                fuzzyMatch ??= new CTraderResolvedSymbol(symbolId.Value, name);
            }
        }

        return fuzzyMatch
            ?? throw new InvalidOperationException($"Symbol {requestedSymbol} was not found in the authenticated cTrader symbol list for this account.");
    }

    private async Task<IReadOnlyList<CTraderTrendbar>> RequestTrendbarsAsync(
        ClientWebSocket webSocket,
        long accountId,
        long symbolId,
        CTraderHistoricalDataRequest request,
        CancellationToken cancellationToken)
    {
        await SendAsync(webSocket, GetTrendbarsReq, new
        {
            ctidTraderAccountId = accountId,
            fromTimestamp = request.FromUtc.ToUnixTimeMilliseconds(),
            toTimestamp = request.ToUtc.ToUnixTimeMilliseconds(),
            period = TimeframePeriod(request.Timeframe),
            symbolId,
            count = 1000
        }, cancellationToken).ConfigureAwait(false);

        using var document = await ReceiveExpectedAsync(webSocket, cancellationToken, GetTrendbarsRes).ConfigureAwait(false);
        var payload = GetPayload(document.RootElement);
        if (!TryGetProperty(payload, out var trendbars, "trendbar") || trendbars.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return trendbars
            .EnumerateArray()
            .Select(ParseTrendbar)
            .Where(trendbar => trendbar is not null)
            .Select(trendbar => trendbar!)
            .ToList();
    }

    private static async Task SendAsync(
        ClientWebSocket webSocket,
        int payloadType,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(new
        {
            clientMsgId = Guid.NewGuid().ToString("N"),
            payloadType,
            payload
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonDocument> ReceiveExpectedAsync(
        ClientWebSocket webSocket,
        CancellationToken cancellationToken,
        params int[] expectedPayloadTypes)
    {
        while (true)
        {
            var json = await ReceiveTextAsync(webSocket, cancellationToken).ConfigureAwait(false);
            var document = JsonDocument.Parse(json);
            var payloadType = ReadInt(document.RootElement, "payloadType", "payload_type");

            if (payloadType is null)
            {
                document.Dispose();
                continue;
            }

            if (payloadType is ErrorRes or 50)
            {
                var payload = GetPayload(document.RootElement);
                var errorCode = ReadString(payload, "errorCode", "error_code") ?? "unknown";
                var description = ReadString(payload, "description") ?? "No error description returned.";
                document.Dispose();
                throw new InvalidOperationException($"cTrader Open API error: {errorCode} - {description}");
            }

            if (payloadType == HeartbeatEvent)
            {
                document.Dispose();
                continue;
            }

            if (expectedPayloadTypes.Contains(payloadType.Value))
            {
                return document;
            }

            document.Dispose();
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("cTrader Open API WebSocket closed before the expected response was received.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void ValidateRequest(CTraderHistoricalDataRequest request)
    {
        if (!_config.NoOrders)
        {
            throw new InvalidOperationException("Invalid cTrader config: no_orders must be true for read-only historical download.");
        }

        if (!_config.ReadOnlyMarketData)
        {
            throw new InvalidOperationException("Invalid cTrader config: read_only_market_data must be true for read-only historical download.");
        }

        if (string.IsNullOrWhiteSpace(_config.ClientSecret))
        {
            throw new InvalidOperationException("cTrader client_secret is missing from config/ctrader.openapi.local.json.");
        }

        if (!_token.HasAccessToken)
        {
            throw new InvalidOperationException("cTrader OAuth token is missing. Run ctrader-auth-url, open it in a browser, then run ctrader-auth-code --code <CODE> with a fresh redirect code.");
        }

        if (!_config.AllowedTimeframes.Contains(request.Timeframe, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported cTrader timeframe: {request.Timeframe}");
        }

        if (request.ToUtc < request.FromUtc)
        {
            throw new InvalidOperationException("Invalid time range: --to must be greater than or equal to --from.");
        }
    }

    private Uri BuildEndpointUri()
    {
        var host = string.IsNullOrWhiteSpace(_config.OpenApiJsonHost)
            ? _config.Environment.Equals("live", StringComparison.OrdinalIgnoreCase)
                ? "live.ctraderapi.com"
                : "demo.ctraderapi.com"
            : _config.OpenApiJsonHost.Trim();
        var port = _config.OpenApiJsonPort > 0 ? _config.OpenApiJsonPort : 5036;
        return new Uri($"wss://{host}:{port}");
    }

    private int TimeoutSeconds() => Math.Clamp(_config.OpenApiTimeoutSeconds, 5, 120);

    private static MarketDataCandle ToCandle(CTraderTrendbar trendbar, string symbol, string timeframe)
    {
        var low = trendbar.Low / 100000.0;
        var open = (trendbar.Low + trendbar.DeltaOpen) / 100000.0;
        var close = (trendbar.Low + trendbar.DeltaClose) / 100000.0;
        var high = (trendbar.Low + trendbar.DeltaHigh) / 100000.0;
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(trendbar.UtcTimestampInMinutes * 60).ToUniversalTime();

        return new MarketDataCandle(
            TimestampUtc: timestamp,
            Open: RoundPrice(symbol, open),
            High: RoundPrice(symbol, high),
            Low: RoundPrice(symbol, low),
            Close: RoundPrice(symbol, close),
            Volume: trendbar.Volume,
            Symbol: symbol.ToUpperInvariant(),
            Timeframe: timeframe.ToUpperInvariant());
    }

    private static CTraderTrendbar? ParseTrendbar(JsonElement element)
    {
        var timestampMinutes = ReadLong(element, "utcTimestampInMinutes", "utc_timestamp_in_minutes");
        var low = ReadLong(element, "low");
        if (timestampMinutes is null || low is null)
        {
            return null;
        }

        return new CTraderTrendbar(
            UtcTimestampInMinutes: timestampMinutes.Value,
            Low: low.Value,
            DeltaOpen: ReadLong(element, "deltaOpen", "delta_open") ?? 0,
            DeltaClose: ReadLong(element, "deltaClose", "delta_close") ?? 0,
            DeltaHigh: ReadLong(element, "deltaHigh", "delta_high") ?? 0,
            Volume: ReadDouble(element, "volume") ?? 0);
    }

    private static JsonElement GetPayload(JsonElement root)
    {
        return TryGetProperty(root, out var payload, "payload")
            ? payload
            : root;
    }

    private static int TimeframePeriod(string timeframe) =>
        timeframe.ToUpperInvariant() switch
        {
            "M5" => 5,
            "M15" => 7,
            "H1" => 9,
            "H4" => 10,
            _ => throw new InvalidOperationException($"Unsupported cTrader trendbar period: {timeframe}")
        };

    private static string NormalizeSymbolName(string value)
    {
        return new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static double RoundPrice(string symbol, double value) =>
        symbol.ToUpperInvariant() switch
        {
            "EURUSD" => Math.Round(value, 5),
            "GER40" or "US500" => Math.Round(value, 1),
            _ => Math.Round(value, 2)
        };

    private static string? ReadString(JsonElement root, params string[] names)
    {
        return TryGetProperty(root, out var value, names) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement root, params string[] names)
    {
        var value = ReadLong(root, names);
        return value is null ? null : Convert.ToInt32(value.Value);
    }

    private static long? ReadLong(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static bool? ReadBool(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var flag) => flag,
            _ => null
        };
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

    private static bool IsConfiguredValue(string value, string placeholder)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CTraderResolvedSymbol(long SymbolId, string SymbolName);

    private sealed record CTraderTrendbar(
        long UtcTimestampInMinutes,
        long Low,
        long DeltaOpen,
        long DeltaClose,
        long DeltaHigh,
        double Volume);
}

