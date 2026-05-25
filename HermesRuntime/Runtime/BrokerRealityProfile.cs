namespace Hermes.Runtime;

public sealed record BrokerRealityProfile(
    string ProfileId,
    string BrokerName,
    string Source,
    string AccountType,
    IReadOnlyDictionary<string, double> TypicalSpreadPoints,
    IReadOnlyDictionary<string, double> TickSize,
    IReadOnlyDictionary<string, double> PipSize,
    double CommissionR,
    double BaseSlippageR,
    int MaxConcurrentTrades);
