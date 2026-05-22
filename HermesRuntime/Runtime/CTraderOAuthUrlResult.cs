namespace Hermes.Runtime;

public sealed record CTraderOAuthUrlResult(
    bool Available,
    string? Url,
    string AuthorizeUrl,
    string RedirectUri,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Warnings);
