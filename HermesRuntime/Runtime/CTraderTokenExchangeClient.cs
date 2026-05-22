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

        var code = NormalizeAuthorizationCode(authorizationCode);
        if (string.IsNullOrWhiteSpace(code)
            || code.Contains('<', StringComparison.Ordinal)
            || code.Contains('>', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A fresh cTrader OAuth redirect code is required. Placeholder values are rejected.");
        }

        var tokenUri = BuildTokenUri(config, code);
        using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                BuildFailureMessage(response, responseText));
        }

        using var document = ParseTokenResponse(response, responseText);
        var root = document.RootElement;
        var errorMessage = BuildCTraderErrorSummary(root);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new InvalidOperationException(BuildFailureMessage(response, responseText));
        }

        var accessToken = ReadString(root, "accessToken", "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(BuildFailureMessage(response, responseText));
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

    private static string NormalizeAuthorizationCode(string authorizationCode)
    {
        var trimmed = authorizationCode.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return ExtractCodeFromQuery(uri.Query) ?? trimmed;
        }

        var queryStart = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0 && queryStart < trimmed.Length - 1)
        {
            return ExtractCodeFromQuery(trimmed[queryStart..]) ?? trimmed;
        }

        if (trimmed.Contains("code=", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractCodeFromQuery(trimmed) ?? trimmed;
        }

        return trimmed;
    }

    private static string? ExtractCodeFromQuery(string query)
    {
        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(part[..separator]);
            if (!name.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }

    private static JsonDocument ParseTokenResponse(HttpResponseMessage response, string responseText)
    {
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"cTrader token exchange failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Response was not valid JSON: {ex.Message}");
        }
    }

    private static string BuildFailureMessage(HttpResponseMessage response, string responseText)
    {
        var status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        var details = new List<string>
        {
            $"cTrader token exchange failed with {status}.",
            "Sent parameters: grant_type=authorization_code, code=<redacted>, client_id=<configured>, client_secret=<redacted>, redirect_uri=<configured exactly from config>."
        };

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            var errorSummary = BuildCTraderErrorSummary(root);
            if (!string.IsNullOrWhiteSpace(errorSummary))
            {
                details.Add(errorSummary);
            }

            details.Add($"Response keys: {string.Join(", ", SafeResponseKeys(root))}.");
        }
        catch (JsonException ex)
        {
            details.Add($"Response JSON parse error: {ex.Message}");
        }

        details.Add("OAuth authorization codes are single-use and expire quickly; generate a new URL/code before retrying.");
        return string.Join(" ", details);
    }

    private static string? BuildCTraderErrorSummary(JsonElement root)
    {
        var error = ReadString(root, "error", "errorCode", "error_code");
        var description = ReadString(root, "error_description", "description");

        if (string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return $"cTrader error: {SanitizeDiagnosticValue(error) ?? "-"}; description: {SanitizeDiagnosticValue(description) ?? "-"}.";
    }

    private static IReadOnlyList<string> SafeResponseKeys(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [$"<{root.ValueKind}>"];
        }

        return root.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !IsSensitiveKey(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SanitizeDiagnosticValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
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
