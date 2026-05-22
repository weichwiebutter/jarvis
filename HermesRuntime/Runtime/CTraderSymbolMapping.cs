namespace Hermes.Runtime;

public sealed record CTraderSymbolMapping(
    string HermesSymbol,
    string CTraderSymbolName,
    string CTraderSymbolId,
    IReadOnlyList<string> Aliases,
    bool StubMapping);
