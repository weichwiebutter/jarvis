namespace Hermes.Runtime;

public sealed class CTraderSymbolMapper
{
    private static readonly IReadOnlyList<CTraderSymbolMapping> DefaultMappings =
    [
        new CTraderSymbolMapping("XAUUSD", "XAUUSD", "stub_symbol_xauusd", ["GOLD"], StubMapping: true),
        new CTraderSymbolMapping("EURUSD", "EURUSD", "stub_symbol_eurusd", ["EUR/USD"], StubMapping: true),
        new CTraderSymbolMapping("GER40", "GER40", "stub_symbol_ger40", ["DE40", "DAX40"], StubMapping: true),
        new CTraderSymbolMapping("US500", "US500", "stub_symbol_us500", ["SPX500", "S&P500"], StubMapping: true)
    ];

    private readonly HashSet<string> _allowedSymbols;

    public CTraderSymbolMapper(IEnumerable<string> allowedSymbols)
    {
        _allowedSymbols = allowedSymbols
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CTraderSymbolMapping> GetMappings()
    {
        return DefaultMappings
            .Where(mapping => _allowedSymbols.Count == 0 || _allowedSymbols.Contains(mapping.HermesSymbol))
            .ToList();
    }

    public bool TryMap(string symbol, out CTraderSymbolMapping mapping)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        foreach (var candidate in GetMappings())
        {
            if (candidate.HermesSymbol.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || candidate.CTraderSymbolName.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || candidate.Aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                mapping = candidate;
                return true;
            }
        }

        mapping = default!;
        return false;
    }
}
