using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Runtime;

public sealed class CTraderOpenApiConfig
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = "example_client_id";

    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; init; } = "http://127.0.0.1:17890/callback";

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = "demo";

    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    [JsonPropertyName("no_orders")]
    public bool NoOrders { get; init; } = true;

    [JsonPropertyName("read_only_market_data")]
    public bool ReadOnlyMarketData { get; init; } = true;

    [JsonPropertyName("stub_mode")]
    public bool StubMode { get; init; } = true;

    [JsonPropertyName("auth_mode")]
    public string AuthMode { get; init; } = "not_configured";

    [JsonPropertyName("token_cache_path")]
    public string? TokenCachePath { get; init; }

    [JsonPropertyName("allowed_symbols")]
    public string[] AllowedSymbols { get; init; } = ["XAUUSD", "EURUSD", "GER40", "US500"];

    [JsonPropertyName("allowed_timeframes")]
    public string[] AllowedTimeframes { get; init; } = ["H4", "H1", "M15", "M5"];

    public static CTraderOpenApiConfig LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return new CTraderOpenApiConfig();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CTraderOpenApiConfig>(json, JsonDefaults.ReadOptions)
            ?? new CTraderOpenApiConfig();
    }
}
