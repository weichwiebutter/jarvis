namespace Hermes.Runtime;

public sealed record BrokerRealitySettings(
    string BrokerProfile,
    double CommissionR,
    double BaseSlippageR,
    int MaxConcurrentTrades,
    IReadOnlyDictionary<string, double> TypicalSpreadPoints,
    IReadOnlyDictionary<string, double> VolatileSpreadMultiplier);
