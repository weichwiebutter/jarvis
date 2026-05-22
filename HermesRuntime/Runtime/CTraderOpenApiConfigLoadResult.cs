namespace Hermes.Runtime;

public sealed record CTraderOpenApiConfigLoadResult(
    CTraderOpenApiConfig Config,
    string ConfigPath,
    bool LocalConfigLoaded,
    bool LocalConfigMissing,
    bool ExampleConfigLoaded,
    IReadOnlyList<string> Warnings);
