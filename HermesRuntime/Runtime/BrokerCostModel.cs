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
                ["EURUSD"] = 0.3,
                ["XAUUSD"] = 1.8,
                ["GER40"] = 2.0,
                ["US500"] = 0.8
            },
            CommissionR: 0.035,
            BaseSlippageR: 0.025,
            SessionVolatilityMultipliers: new Dictionary<string, double>
            {
                ["london"] = 1.0,
                ["new_york"] = 1.2,
                ["london_new_york_overlap"] = 1.35,
                ["off_session"] = 2.1
            },
            SymbolContractHints: new Dictionary<string, string>
            {
                ["EURUSD"] = "forex_major",
                ["XAUUSD"] = "metal_high_spread_sensitivity",
                ["GER40"] = "index_session_sensitive",
                ["US500"] = "index_session_sensitive"
            });
}
