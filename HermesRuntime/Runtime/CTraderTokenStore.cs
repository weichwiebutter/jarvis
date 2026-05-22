using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CTraderTokenStore
{
    private readonly StoragePaths _storagePaths;

    public CTraderTokenStore(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string TokenStorePath => Path.Combine(_storagePaths.Root, "auth", "ctrader_tokens.json");

    public CTraderStoredToken? LoadToken()
    {
        if (!File.Exists(TokenStorePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(TokenStorePath);
            using var document = JsonDocument.Parse(stream);
            return ParseStoredToken(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public void SaveToken(CTraderStoredToken token)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Cannot save an empty cTrader access token.");
        }

        var directory = Path.GetDirectoryName(TokenStorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            TokenStorePath,
            JsonSerializer.Serialize(token, JsonDefaults.WriteOptions));
    }

    public CTraderAuthStatus GetStatus(
        CTraderOpenApiConfig config,
        CTraderOpenApiConfigLoadResult configLoad,
        CTraderOAuthUrlResult oauthUrl)
    {
        var warnings = new List<string>();
        warnings.AddRange(configLoad.Warnings);
        warnings.AddRange(oauthUrl.Warnings);

        if (!configLoad.LocalConfigLoaded)
        {
            warnings.Add("Local cTrader config is missing; authentication cannot be completed yet.");
        }

        if (!File.Exists(TokenStorePath))
        {
            return new CTraderAuthStatus(
                Status: "not_authenticated",
                AuthUrlAvailable: oauthUrl.Available,
                AuthConfigured: oauthUrl.Available && configLoad.LocalConfigLoaded,
                TokenLoaded: false,
                TokenStoreExists: false,
                TokenStorePath: TokenStorePath,
                AuthMode: NormalizeAuthMode(config.AuthMode),
                ExpiresAtUtc: null,
                Warnings: warnings);
        }

        try
        {
            using var stream = File.OpenRead(TokenStorePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var accessTokenPresent = HasNonEmptyString(root, "access_token", "accessToken");
            var refreshTokenPresent = HasNonEmptyString(root, "refresh_token", "refreshToken");
            var expiresAtUtc = ReadDateTimeOffset(root, "expires_at_utc", "expiresAtUtc", "expires_at");
            var tokenLoaded = accessTokenPresent || refreshTokenPresent;
            var expired = expiresAtUtc is not null && expiresAtUtc <= DateTimeOffset.UtcNow;
            var status = tokenLoaded
                ? expired ? "token_expired" : "authenticated"
                : "not_authenticated";

            if (!tokenLoaded)
            {
                warnings.Add("Token store exists, but no token fields were found.");
            }

            if (expired)
            {
                warnings.Add("Token store exists, but the stored token metadata is expired.");
            }

            return new CTraderAuthStatus(
                Status: status,
                AuthUrlAvailable: oauthUrl.Available,
                AuthConfigured: oauthUrl.Available && configLoad.LocalConfigLoaded,
                TokenLoaded: tokenLoaded,
                TokenStoreExists: true,
                TokenStorePath: TokenStorePath,
                AuthMode: NormalizeAuthMode(config.AuthMode),
                ExpiresAtUtc: expiresAtUtc,
                Warnings: warnings);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"Token store could not be read: {ex.Message}");
            return new CTraderAuthStatus(
                Status: "token_store_unreadable",
                AuthUrlAvailable: oauthUrl.Available,
                AuthConfigured: oauthUrl.Available && configLoad.LocalConfigLoaded,
                TokenLoaded: false,
                TokenStoreExists: true,
                TokenStorePath: TokenStorePath,
                AuthMode: NormalizeAuthMode(config.AuthMode),
                ExpiresAtUtc: null,
                Warnings: warnings);
        }
    }

    private static string NormalizeAuthMode(string? authMode)
    {
        return string.IsNullOrWhiteSpace(authMode) ? "not_configured" : authMode;
    }

    private static CTraderStoredToken? ParseStoredToken(JsonElement root)
    {
        var accessToken = ReadString(root, "access_token", "accessToken");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        return new CTraderStoredToken
        {
            AccessToken = accessToken,
            RefreshToken = ReadString(root, "refresh_token", "refreshToken"),
            TokenType = ReadString(root, "token_type", "tokenType"),
            ExpiresIn = ReadLong(root, "expires_in", "expiresIn"),
            ExpiresAtUtc = ReadDateTimeOffset(root, "expires_at_utc", "expiresAtUtc", "expires_at"),
            CreatedAtUtc = ReadDateTimeOffset(root, "created_at_utc", "createdAtUtc") ?? DateTimeOffset.UtcNow
        };
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        return TryGetProperty(root, out var value, names)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
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

    private static bool HasNonEmptyString(JsonElement root, params string[] names)
    {
        return TryGetProperty(root, out var value, names)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString());
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();

        return DateTimeOffset.TryParse(text, out var timestamp)
            ? timestamp.ToUniversalTime()
            : null;
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
