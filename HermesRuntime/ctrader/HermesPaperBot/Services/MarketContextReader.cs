namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Reads market context without trading actions.
/// </summary>
public sealed class MarketContextReader : IMarketContextProvider
{
    /// <summary>
    /// Reads the current runtime market context.
    /// </summary>
    public RuntimeMarketContext Read()
    {
        return new RuntimeMarketContext();
    }
}
