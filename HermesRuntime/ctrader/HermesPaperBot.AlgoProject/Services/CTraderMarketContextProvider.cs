using HermesPaperBot.Models;

namespace HermesPaperBot.Services;

/// <summary>
/// CTrader-facing market context provider placeholder that remains read-only and paper-only.
/// </summary>
public sealed class CTraderMarketContextProvider : IMarketContextProvider
{
    private RuntimeMarketContext _context = new();

    /// <summary>
    /// Updates the cached read-only market context.
    /// </summary>
    public void Update(RuntimeMarketContext context)
    {
        _context = context ?? new RuntimeMarketContext();
    }

    /// <summary>
    /// Reads the cached read-only market context.
    /// </summary>
    public RuntimeMarketContext Read()
        => _context;
}
