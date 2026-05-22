namespace Hermes.Runtime;

public sealed record CTraderAuthStatus(
    string Status,
    bool AuthUrlAvailable,
    bool AuthConfigured,
    bool TokenLoaded,
    bool TokenStoreExists,
    string TokenStorePath,
    string AuthMode,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<string> Warnings);
