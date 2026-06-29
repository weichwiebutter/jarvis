namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Returns a fixed market context for harnesses and defensive local runs.
/// </summary>
public sealed class StaticMarketContextProvider : IMarketContextProvider
{
    private readonly RuntimeMarketContext _context;

    /// <summary>
    /// Creates a provider with a fixed runtime market context.
    /// </summary>
    public StaticMarketContextProvider(RuntimeMarketContext context)
    {
        _context = context ?? new RuntimeMarketContext();
    }

    /// <summary>
    /// Reads the fixed runtime market context.
    /// </summary>
    public RuntimeMarketContext Read()
        => _context;
}
