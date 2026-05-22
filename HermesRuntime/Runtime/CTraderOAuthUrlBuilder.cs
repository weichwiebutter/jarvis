namespace Hermes.Runtime;

public sealed class CTraderOAuthUrlBuilder
{
    public CTraderOAuthUrlResult Build(CTraderOpenApiConfig config)
    {
        var warnings = new List<string>();
        var authorizeUrl = string.IsNullOrWhiteSpace(config.OAuthAuthorizeUrl)
            ? "https://id.ctrader.com/my/settings/openapi/grantingaccess/"
            : config.OAuthAuthorizeUrl.Trim();
        var redirectUri = string.IsNullOrWhiteSpace(config.RedirectUri)
            ? "http://127.0.0.1:17890/callback"
            : config.RedirectUri.Trim();
        var scopes = config.Scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!IsConfiguredValue(config.ClientId, "example_client_id"))
        {
            warnings.Add("cTrader client_id is not configured with a local value.");
        }

        if (!config.NoOrders)
        {
            warnings.Add("no_orders must remain true before OAuth is used.");
        }

        if (!config.ReadOnlyMarketData)
        {
            warnings.Add("read_only_market_data must remain true before OAuth is used.");
        }

        if (scopes.Count == 0)
        {
            warnings.Add("No OAuth scopes configured. Defaulting to market_data.");
            scopes.Add("market_data");
        }
        var forbiddenScopes = scopes
            .Where(scope => scope.Contains("order", StringComparison.OrdinalIgnoreCase)
                || scope.Contains("trade", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (forbiddenScopes.Count > 0)
        {
            warnings.Add($"Order/trading scopes are not allowed for read-only auth v1: {string.Join(", ", forbiddenScopes)}.");
        }

        var available = IsConfiguredValue(config.ClientId, "example_client_id")
            && config.NoOrders
            && config.ReadOnlyMarketData
            && forbiddenScopes.Count == 0;
        var query = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', scopes),
            ["environment"] = config.Environment
        };

        var separator = authorizeUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url = authorizeUrl
            + separator
            + string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new CTraderOAuthUrlResult(
            Available: available,
            Url: url,
            AuthorizeUrl: authorizeUrl,
            RedirectUri: redirectUri,
            Scopes: scopes,
            Warnings: warnings);
    }

    private static bool IsConfiguredValue(string value, string placeholder)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
    }
}
