namespace Hermes.Runtime;

public static class CTraderAuthTokenPlaceholder
{
    public static CTraderAuthTokenState Evaluate(
        CTraderOpenApiConfig config,
        bool localConfigLoaded)
    {
        var warnings = new List<string>();
        var authMode = string.IsNullOrWhiteSpace(config.AuthMode)
            ? "not_configured"
            : config.AuthMode;

        if (!localConfigLoaded)
        {
            warnings.Add("No local cTrader config loaded; OAuth/token setup is unavailable.");
        }

        if (!IsConfiguredValue(config.ClientId, "example_client_id"))
        {
            warnings.Add("cTrader client_id is not configured with a local value.");
        }

        if (authMode.Equals("not_configured", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Auth mode is not configured. Real Open API calls remain disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.TokenCachePath))
        {
            warnings.Add("Token cache path is not configured. No token file is read by this placeholder.");
        }

        return new CTraderAuthTokenState(
            AuthConfigured: false,
            TokenAvailable: false,
            AuthMode: authMode,
            TokenCachePath: config.TokenCachePath,
            Warnings: warnings);
    }

    private static bool IsConfiguredValue(string value, string placeholder)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
    }
}
