using System.Net.Http.Headers;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CTraderTokenExchangeClient
{
    private readonly HttpClient _httpClient;

    public CTraderTokenExchangeClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<CTraderStoredToken> ExchangeAuthorizationCodeAsync(
        CTraderOpenApiConfig config,
        string authorizationCode,
        CancellationToken cancellationToken = default)
    {
        ValidateConfig(config);

        if (string.IsNullOrWhiteSpace(authorizationCode)
            || authorizationCode.Contains('<', StringComparison.Ordinal)
            || authorizationCode.Contains('>', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A fresh cTrader OAuth redirect code is required. Placeholder values are rejected.");
        }

        var tokenUri = BuildTokenUri(config, authorizationCode.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"cTrader token exchange failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Verify the redirect code, redirect_uri and local client credentials. OAuth codes expire quickly.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var accessToken = ReadString(root, "accessToken", "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("cTrader token exchange response did not contain an access token.");
        }

        var expiresIn = ReadLong(root, "expiresIn", "expires_in");
        return new CTraderStoredToken
        {
            AccessToken = accessToken,
            RefreshToken = ReadString(root, "refreshToken", "refresh_token"),
            TokenType = ReadString(root, "tokenType", "token_type"),
            ExpiresIn = expiresIn,
            ExpiresAtUtc = ResolveExpiresAtUtc(expiresIn),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Uri BuildTokenUri(CTraderOpenApiConfig config, string authorizationCode)
    {
        var endpoint = string.IsNullOrWhiteSpace(config.TokenEndpointUrl)
            ? "https://openapi.ctrader.com/apps/token"
            : config.TokenEndpointUrl.Trim();

        var query = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = config.RedirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret!
        };

        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url = endpoint
            + separator
            + string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new Uri(url);
    }

    private static void ValidateConfig(CTraderOpenApiConfig config)
    {
        if (!config.NoOrders)
        {
            throw new InvalidOperationException("Invalid cTrader config: no_orders must remain true.");
        }

        if (!config.ReadOnlyMarketData)
        {
            throw new InvalidOperationException("Invalid cTrader config: read_only_market_data must remain true.");
        }

        if (string.IsNullOrWhiteSpace(config.ClientId)
            || config.ClientId.Equals("example_client_id", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("cTrader client_id is missing from config/ctrader.openapi.local.json.");
        }

        if (string.IsNullOrWhiteSpace(config.ClientSecret)
            || config.ClientSecret.Equals("example_client_secret", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("cTrader client_secret is missing from config/ctrader.openapi.local.json.");
        }
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        return TryGetProperty(root, out var value, names) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

    private static DateTimeOffset? ResolveExpiresAtUtc(long? expiresIn)
    {
        if (expiresIn is null or <= 0)
        {
            return null;
        }

        if (expiresIn > 1_000_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(expiresIn.Value).ToUniversalTime();
        }

        if (expiresIn > 1_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeSeconds(expiresIn.Value).ToUniversalTime();
        }

        return DateTimeOffset.UtcNow.AddSeconds(expiresIn.Value);
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
}

