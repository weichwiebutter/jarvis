namespace Hermes.Runtime;

public sealed class CTraderOpenApiConfigLoader
{
    public CTraderOpenApiConfigLoadResult Load(string runtimeRoot)
    {
        var localPath = Path.Combine(runtimeRoot, "config", "ctrader.openapi.local.json");
        var examplePath = Path.Combine(runtimeRoot, "config", "ctrader.openapi.example.json");
        var warnings = new List<string>();

        if (File.Exists(localPath))
        {
            var config = LoadConfig(localPath, warnings);
            AppendSafetyWarnings(config, warnings);
            if (!config.StubMode)
            {
                warnings.Add("Local config requests stub_mode=false, but the real read-only Open API client is not implemented yet. Stub fallback remains active.");
            }

            return new CTraderOpenApiConfigLoadResult(
                config,
                localPath,
                LocalConfigLoaded: true,
                LocalConfigMissing: false,
                ExampleConfigLoaded: false,
                Warnings: warnings);
        }

        warnings.Add("Local cTrader config missing: config/ctrader.openapi.local.json. Stub fallback is active; no real cTrader data will be downloaded.");
        var exampleConfig = LoadConfig(examplePath, warnings);
        AppendSafetyWarnings(exampleConfig, warnings);

        return new CTraderOpenApiConfigLoadResult(
            exampleConfig,
            examplePath,
            LocalConfigLoaded: false,
            LocalConfigMissing: true,
            ExampleConfigLoaded: true,
            Warnings: warnings);
    }

    private static CTraderOpenApiConfig LoadConfig(string path, List<string> warnings)
    {
        try
        {
            return CTraderOpenApiConfig.LoadOrDefault(path);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            warnings.Add($"cTrader config could not be read from {path}: {ex.Message}. Built-in safe defaults are used.");
            return new CTraderOpenApiConfig();
        }
    }

    private static void AppendSafetyWarnings(CTraderOpenApiConfig config, List<string> warnings)
    {
        if (!config.NoOrders)
        {
            warnings.Add("Invalid cTrader config: no_orders must remain true.");
        }

        if (!config.ReadOnlyMarketData)
        {
            warnings.Add("Invalid cTrader config: read_only_market_data must remain true.");
        }
    }
}
