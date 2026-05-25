namespace Hermes.Runtime;

public sealed record BrokerCostModel(
    string ModelVersion,
    string Source,
    IReadOnlyDictionary<string, double> SpreadDefaults,
    double CommissionR,
    double BaseSlippageR,
    IReadOnlyDictionary<string, double> SessionVolatilityMultipliers,
    IReadOnlyDictionary<string, string> SymbolContractHints)
{
    public static BrokerCostModel FusionMarketsManualDefault =>
        new(
            ModelVersion: "broker_cost_model_v1",
            Source: "manual_default",
            SpreadDefaults: new Dictionary<string, double>
            {
                ["EURUSD"] = 0.2,
                ["XAUUSD"] = 1.2,
                ["GER40"] = 1.4,
                ["US500"] = 0.6
            },
            CommissionR: 0.025,
            BaseSlippageR: 0.015,
            SessionVolatilityMultipliers: new Dictionary<string, double>
            {
                ["london"] = 1.0,
                ["new_york"] = 1.1,
                ["london_new_york_overlap"] = 1.25,
                ["off_session"] = 1.75
            },
            SymbolContractHints: new Dictionary<string, string>
            {
                ["EURUSD"] = "forex_major",
                ["XAUUSD"] = "metal_high_spread_sensitivity",
                ["GER40"] = "index_session_sensitive",
                ["US500"] = "index_session_sensitive"
            });
}
