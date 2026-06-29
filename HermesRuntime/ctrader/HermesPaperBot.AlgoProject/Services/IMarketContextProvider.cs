namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Provides the current runtime market context without any trading actions.
/// </summary>
public interface IMarketContextProvider
{
    /// <summary>
    /// Reads the current runtime market context.
    /// </summary>
    RuntimeMarketContext Read();
}
