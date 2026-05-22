using System.Text.Json.Serialization;

namespace Hermes.Runtime;

public sealed class CTraderStoredToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; init; }

    [JsonPropertyName("expires_at_utc")]
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
}

