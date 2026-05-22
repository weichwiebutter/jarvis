namespace Hermes.Runtime;

public sealed record CTraderAuthTokenState(
    bool AuthConfigured,
    bool TokenAvailable,
    string AuthMode,
    string? TokenCachePath,
    IReadOnlyList<string> Warnings);
