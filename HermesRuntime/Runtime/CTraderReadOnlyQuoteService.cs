using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CTraderReadOnlyQuote(
    string Asset,
    double? Bid,
    double? Ask,
    double? Mid,
    double? Spread,
    DateTimeOffset? TimestampUtc,
    string Source,
    bool IsLiveReadonly,
    bool IsPlaceholder,
    double? AgeSeconds,
    string Status);

public sealed record CTraderReadOnlyQuoteSnapshot(
    string SnapshotVersion,
    DateTimeOffset UpdatedAtUtc,
    string QuoteSnapshotStatus,
    IReadOnlyList<string> AssetsRequested,
    IReadOnlyList<string> AssetsAvailable,
    IReadOnlyList<string> Warnings,
    string QuotesJsonPath,
    string QuotesMarkdownPath,
    string QuoteLogPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class CTraderReadOnlyQuoteService
{
    private static readonly string[] Assets = ["EURUSD", "XAUUSD"];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public CTraderReadOnlyQuoteService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "current_market");
    public string QuotesJsonPath => Path.Combine(Root, "current_market_quotes.json");
    public string QuotesMarkdownPath => Path.Combine(Root, "current_market_quotes.md");
    public string QuoteLogPath => Path.Combine(Root, "quote_snapshot_log.jsonl");

    public CTraderReadOnlyQuoteSnapshot UpdateQuotes()
    {
        var warnings = new List<string>();
        var quotes = LoadQuotesFromCTrader(warnings);
        var snapshot = BuildSnapshot(quotes, warnings);

        Directory.CreateDirectory(Root);
        File.WriteAllText(QuotesJsonPath, JsonSerializer.Serialize(quotes, JsonDefaults.WriteOptions));
        File.WriteAllText(QuotesMarkdownPath, BuildMarkdown(snapshot, quotes));
        AppendLog("update_ctrader_readonly_quotes", snapshot);
        return snapshot;
    }

    public CTraderReadOnlyQuoteSnapshot LoadOrCreateStatus()
    {
        if (!File.Exists(QuotesJsonPath))
        {
            return UpdateQuotes();
        }

        var quotes = LoadQuotes();
        var warnings = quotes
            .Where(quote => quote.Status != "available")
            .Select(quote => $"quote_unavailable:{quote.Asset}:{quote.Status}")
            .ToList();
        return BuildSnapshot(quotes, warnings);
    }

    public IReadOnlyList<CTraderReadOnlyQuote> LoadQuotes()
    {
        if (!File.Exists(QuotesJsonPath))
        {
            UpdateQuotes();
        }

        var quotes = JsonSerializer.Deserialize<List<CTraderReadOnlyQuote>>(File.ReadAllText(QuotesJsonPath), JsonDefaults.SnapshotReadOptions);
        return quotes ?? [];
    }

    private IReadOnlyList<CTraderReadOnlyQuote> LoadQuotesFromCTrader(List<string> warnings)
    {
        var loader = new CTraderOpenApiConfigLoader();
        var configLoad = loader.Load(_runtimeRoot);
        warnings.AddRange(configLoad.Warnings);
        var config = configLoad.Config;
        var tokenStore = new CTraderTokenStore(_storagePaths);
        var token = tokenStore.LoadToken();

        if (!config.NoOrders)
        {
            warnings.Add("invalid_ctrader_config:no_orders_false");
            return BuildUnavailableQuotes("invalid_ctrader_config");
        }

        if (!config.ReadOnlyMarketData)
        {
            warnings.Add("invalid_ctrader_config:read_only_market_data_false");
            return BuildUnavailableQuotes("invalid_ctrader_config");
        }

        if (token is null || !token.HasAccessToken)
        {
            warnings.Add("ctrader_quote_token_missing");
            return BuildUnavailableQuotes("token_missing");
        }

        try
        {
            return DownloadQuotes(config, token, warnings).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or WebSocketException or TaskCanceledException or JsonException)
        {
            warnings.Add($"ctrader_readonly_quote_unavailable:{SanitizeMessage(ex.Message)}");
            return BuildUnavailableQuotes("ctrader_readonly_quote");
        }
    }

    private async Task<IReadOnlyList<CTraderReadOnlyQuote>> DownloadQuotes(
        CTraderOpenApiConfig config,
        CTraderStoredToken token,
        List<string> warnings)
    {
        const int applicationAuthReq = 2100;
        const int applicationAuthRes = 2101;
        const int accountAuthReq = 2102;
        const int accountAuthRes = 2103;
        const int symbolsListReq = 2114;
        const int symbolsListRes = 2115;
        const int subscribeSpotsReq = 2127;
        const int subscribeSpotsRes = 2128;
        const int spotEvent = 2131;
        const int errorRes = 2142;
        const int getAccountsReq = 2149;
        const int getAccountsRes = 2150;
        const int heartbeatEvent = 51;

        using var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(config.OpenApiTimeoutSeconds, 5, 120)));

        await webSocket.ConnectAsync(BuildEndpointUri(config), cts.Token).ConfigureAwait(false);
        await SendAsync(webSocket, applicationAuthReq, new
        {
            clientId = config.ClientId,
            clientSecret = config.ClientSecret
        }, cts.Token).ConfigureAwait(false);
        using (await ReceiveExpectedAsync(webSocket, cts.Token, errorRes, heartbeatEvent, applicationAuthRes).ConfigureAwait(false))
        {
        }

        var accountId = await ResolveAccountIdAsync(webSocket, config, token, cts.Token, getAccountsReq, getAccountsRes, errorRes, heartbeatEvent).ConfigureAwait(false);
        await SendAsync(webSocket, accountAuthReq, new
        {
            ctidTraderAccountId = accountId,
            accessToken = token.AccessToken
        }, cts.Token).ConfigureAwait(false);
        using (await ReceiveExpectedAsync(webSocket, cts.Token, errorRes, heartbeatEvent, accountAuthRes).ConfigureAwait(false))
        {
        }

        var symbols = await ResolveSymbolsAsync(webSocket, config, accountId, cts.Token, symbolsListReq, symbolsListRes, errorRes, heartbeatEvent).ConfigureAwait(false);
        var requestedSymbols = symbols
            .Where(item => Assets.Contains(item.HermesSymbol, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (requestedSymbols.Count == 0)
        {
            warnings.Add("ctrader_readonly_quote_symbols_missing");
            return BuildUnavailableQuotes("symbols_missing");
        }

        await SendAsync(webSocket, subscribeSpotsReq, new
        {
            ctidTraderAccountId = accountId,
            symbolId = requestedSymbols.Select(item => item.SymbolId).ToArray()
        }, cts.Token).ConfigureAwait(false);
        using (await ReceiveExpectedAsync(webSocket, cts.Token, errorRes, heartbeatEvent, subscribeSpotsRes).ConfigureAwait(false))
        {
        }

        var received = new Dictionary<string, CTraderReadOnlyQuote>(StringComparer.OrdinalIgnoreCase);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline && received.Count < requestedSymbols.Count)
        {
            using var document = await ReceiveAnyAsync(webSocket, cts.Token).ConfigureAwait(false);
            var payloadType = ReadInt(document.RootElement, "payloadType", "payload_type");
            if (payloadType is null || payloadType == heartbeatEvent)
            {
                continue;
            }

            if (payloadType == errorRes)
            {
                var payload = GetPayload(document.RootElement);
                var description = ReadString(payload, "description") ?? "unknown_ctrader_error";
                warnings.Add($"ctrader_quote_api_error:{SanitizeMessage(description)}");
                break;
            }

            if (payloadType != spotEvent)
            {
                continue;
            }

            var payloadRoot = GetPayload(document.RootElement);
            var symbolId = ReadLong(payloadRoot, "symbolId", "symbol_id");
            if (symbolId is null)
            {
                continue;
            }

            var symbol = requestedSymbols.FirstOrDefault(item => item.SymbolId == symbolId.Value);
            if (symbol is null)
            {
                continue;
            }

            var bid = ReadSpotPrice(payloadRoot, "bid", "bidPrice", "bid_price");
            var ask = ReadSpotPrice(payloadRoot, "ask", "askPrice", "ask_price");
            if (!bid.HasValue || !ask.HasValue)
            {
                continue;
            }

            var timestamp = ReadDateTimeOffset(payloadRoot, "timestamp", "timestamp_utc", "updated_at_utc", "updatedAtUtc")
                ?? DateTimeOffset.UtcNow;
            received[symbol.HermesSymbol] = BuildAvailableQuote(symbol.HermesSymbol, bid.Value, ask.Value, timestamp);
        }

        return Assets
            .Select(asset => received.TryGetValue(asset, out var quote) ? quote : BuildUnavailableQuote(asset, "ctrader_readonly_quote"))
            .ToList();
    }

    private static async Task<long> ResolveAccountIdAsync(
        ClientWebSocket webSocket,
        CTraderOpenApiConfig config,
        CTraderStoredToken token,
        CancellationToken cancellationToken,
        int getAccountsReq,
        int getAccountsRes,
        int errorRes,
        int heartbeatEvent)
    {
        if (long.TryParse(config.AccountId, out var configuredAccountId) && configuredAccountId > 0)
        {
            return configuredAccountId;
        }

        await SendAsync(webSocket, getAccountsReq, new { accessToken = token.AccessToken }, cancellationToken).ConfigureAwait(false);
        using var document = await ReceiveExpectedAsync(webSocket, cancellationToken, errorRes, heartbeatEvent, getAccountsRes).ConfigureAwait(false);
        var payload = GetPayload(document.RootElement);
        if (!TryGetProperty(payload, out var accounts, "ctidTraderAccount", "ctid_trader_account")
            || accounts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("cTrader account list response missing.");
        }

        foreach (var account in accounts.EnumerateArray())
        {
            var accountId = ReadLong(account, "ctidTraderAccountId", "ctid_trader_account_id");
            if (accountId is > 0)
            {
                return accountId.Value;
            }
        }

        throw new InvalidOperationException("cTrader account id unavailable.");
    }

    private static async Task<IReadOnlyList<ResolvedQuoteSymbol>> ResolveSymbolsAsync(
        ClientWebSocket webSocket,
        CTraderOpenApiConfig config,
        long accountId,
        CancellationToken cancellationToken,
        int symbolsListReq,
        int symbolsListRes,
        int errorRes,
        int heartbeatEvent)
    {
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var mappings = mapper.GetMappings()
            .Where(mapping => Assets.Contains(mapping.HermesSymbol, StringComparer.OrdinalIgnoreCase))
            .ToList();

        await SendAsync(webSocket, symbolsListReq, new
        {
            ctidTraderAccountId = accountId,
            includeArchivedSymbols = false
        }, cancellationToken).ConfigureAwait(false);

        using var document = await ReceiveExpectedAsync(webSocket, cancellationToken, errorRes, heartbeatEvent, symbolsListRes).ConfigureAwait(false);
        var payload = GetPayload(document.RootElement);
        if (!TryGetProperty(payload, out var symbols, "symbol") || symbols.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("cTrader symbol list response missing.");
        }

        var resolved = new List<ResolvedQuoteSymbol>();
        foreach (var mapping in mappings)
        {
            foreach (var symbol in symbols.EnumerateArray())
            {
                var name = ReadString(symbol, "symbolName", "symbol_name", "name");
                var symbolId = ReadLong(symbol, "symbolId", "symbol_id");
                if (string.IsNullOrWhiteSpace(name) || symbolId is null or <= 0)
                {
                    continue;
                }

                var normalizedName = NormalizeSymbolName(name);
                var expectedNames = new[] { mapping.HermesSymbol, mapping.CTraderSymbolName }
                    .Concat(mapping.Aliases)
                    .Select(NormalizeSymbolName)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                if (expectedNames.Contains(normalizedName, StringComparer.OrdinalIgnoreCase)
                    || expectedNames.Any(expected => normalizedName.Contains(expected, StringComparison.OrdinalIgnoreCase)))
                {
                    resolved.Add(new ResolvedQuoteSymbol(mapping.HermesSymbol, symbolId.Value));
                    break;
                }
            }
        }

        return resolved;
    }

    private CTraderReadOnlyQuoteSnapshot BuildSnapshot(IReadOnlyList<CTraderReadOnlyQuote> quotes, IReadOnlyList<string> warnings)
    {
        var assetsAvailable = quotes.Where(quote => quote.Status == "available").Select(quote => quote.Asset).ToList();
        var status = assetsAvailable.Count > 0 ? "available" : "unavailable";
        return new CTraderReadOnlyQuoteSnapshot(
            SnapshotVersion: "ctrader_readonly_quote_snapshot_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            QuoteSnapshotStatus: status,
            AssetsRequested: Assets,
            AssetsAvailable: assetsAvailable,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            QuotesJsonPath: QuotesJsonPath,
            QuotesMarkdownPath: QuotesMarkdownPath,
            QuoteLogPath: QuoteLogPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private void AppendLog(string action, CTraderReadOnlyQuoteSnapshot snapshot)
    {
        Directory.CreateDirectory(Root);
        var line = JsonSerializer.Serialize(new
        {
            timestamp_utc = DateTimeOffset.UtcNow,
            action,
            quote_snapshot_status = snapshot.QuoteSnapshotStatus,
            assets_requested = snapshot.AssetsRequested,
            assets_available = snapshot.AssetsAvailable,
            warnings = snapshot.Warnings,
            no_auto_trading = true,
            human_review_required = true,
            broker_orders_enabled = false,
            live_trading_enabled = false
        }, JsonDefaults.WriteOptions);
        File.AppendAllText(QuoteLogPath, line + Environment.NewLine);
    }

    private static string BuildMarkdown(CTraderReadOnlyQuoteSnapshot snapshot, IReadOnlyList<CTraderReadOnlyQuote> quotes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# cTrader Read-Only Quotes");
        builder.AppendLine();
        builder.AppendLine($"- quote_snapshot_status: {snapshot.QuoteSnapshotStatus}");
        builder.AppendLine($"- assets_available: {(snapshot.AssetsAvailable.Count == 0 ? "none" : string.Join(", ", snapshot.AssetsAvailable))}");
        builder.AppendLine("- Read-only market snapshot");
        builder.AppendLine("- No orders");
        builder.AppendLine("- No broker action");
        builder.AppendLine("- No cTrader Order API for trading");
        builder.AppendLine("- no_auto_trading=true");
        builder.AppendLine("- human_review_required=true");
        builder.AppendLine();
        foreach (var quote in quotes)
        {
            builder.AppendLine($"## {quote.Asset}");
            builder.AppendLine($"- status: {quote.Status}");
            builder.AppendLine($"- bid: {(quote.Bid.HasValue ? quote.Bid.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- ask: {(quote.Ask.HasValue ? quote.Ask.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- mid: {(quote.Mid.HasValue ? quote.Mid.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- spread: {(quote.Spread.HasValue ? quote.Spread.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- timestamp_utc: {(quote.TimestampUtc?.ToString("O") ?? "n/a")}");
            builder.AppendLine($"- source: {quote.Source}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<CTraderReadOnlyQuote> BuildUnavailableQuotes(string source)
        => Assets.Select(asset => BuildUnavailableQuote(asset, source)).ToList();

    private static CTraderReadOnlyQuote BuildUnavailableQuote(string asset, string source)
        => new(
            Asset: asset,
            Bid: null,
            Ask: null,
            Mid: null,
            Spread: null,
            TimestampUtc: null,
            Source: source,
            IsLiveReadonly: false,
            IsPlaceholder: false,
            AgeSeconds: null,
            Status: "unavailable");

    private static CTraderReadOnlyQuote BuildAvailableQuote(string asset, double bid, double ask, DateTimeOffset timestampUtc)
    {
        var decimals = asset.Equals("EURUSD", StringComparison.OrdinalIgnoreCase) ? 5 : 2;
        var age = Math.Max(0, (DateTimeOffset.UtcNow - timestampUtc).TotalSeconds);
        return new CTraderReadOnlyQuote(
            Asset: asset,
            Bid: Math.Round(bid, decimals),
            Ask: Math.Round(ask, decimals),
            Mid: Math.Round((bid + ask) / 2.0, decimals),
            Spread: Math.Round(ask - bid, decimals),
            TimestampUtc: timestampUtc,
            Source: "ctrader_readonly_quote",
            IsLiveReadonly: true,
            IsPlaceholder: false,
            AgeSeconds: Math.Round(age, 2),
            Status: age <= 120 ? "available" : age <= 900 ? "stale" : "unavailable");
    }

    private static async Task SendAsync(ClientWebSocket webSocket, int payloadType, object payload, CancellationToken cancellationToken)
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

    private static async Task<JsonDocument> ReceiveExpectedAsync(ClientWebSocket webSocket, CancellationToken cancellationToken, params int[] expectedPayloadTypes)
    {
        while (true)
        {
            var document = await ReceiveAnyAsync(webSocket, cancellationToken).ConfigureAwait(false);
            var payloadType = ReadInt(document.RootElement, "payloadType", "payload_type");
            if (payloadType is null)
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

    private static async Task<JsonDocument> ReceiveAnyAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("cTrader Open API WebSocket closed before quote data was received.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static Uri BuildEndpointUri(CTraderOpenApiConfig config)
    {
        var host = string.IsNullOrWhiteSpace(config.OpenApiJsonHost)
            ? config.Environment.Equals("live", StringComparison.OrdinalIgnoreCase) ? "live.ctraderapi.com" : "demo.ctraderapi.com"
            : config.OpenApiJsonHost.Trim();
        var port = config.OpenApiJsonPort > 0 ? config.OpenApiJsonPort : 5036;
        return new Uri($"wss://{host}:{port}");
    }

    private static JsonElement GetPayload(JsonElement root)
        => TryGetProperty(root, out var payload, "payload") ? payload : root;

    private static double? ReadSpotPrice(JsonElement root, params string[] names)
    {
        var price = ReadDouble(root, names);
        if (!price.HasValue)
        {
            return null;
        }

        return price > 100000 ? price.Value / 100000.0 : price.Value;
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown";
        }

        return message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("accessToken", "token", StringComparison.OrdinalIgnoreCase)
            .Replace("refreshToken", "token", StringComparison.OrdinalIgnoreCase)
            .Replace("clientSecret", "secret", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSymbolName(string value)
        => new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string? ReadString(JsonElement root, params string[] names)
        => TryGetProperty(root, out var value, names) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String when DateTimeOffset.TryParse(value.GetString(), out var parsed) => parsed.ToUniversalTime(),
            JsonValueKind.Number when value.TryGetInt64(out var unixMillis) => DateTimeOffset.FromUnixTimeMilliseconds(unixMillis).ToUniversalTime(),
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

    private sealed record ResolvedQuoteSymbol(string HermesSymbol, long SymbolId);
}
