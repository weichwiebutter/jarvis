using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace HermesPaperBot.Bot;

/// <summary>
/// Cloud host adapter skeleton that only delegates to the safe paper bot skeleton.
/// </summary>
/// <remarks>
/// paper_only
/// broker_action=none
/// no order API
/// no_auto_trading=true
/// human_review_required=true
/// broker_trading_enabled=false
/// live_trading_enabled=false
/// order_api_enabled=false
/// paper_mode=true
/// </remarks>
public sealed class HermesPaperBotCloudHost
{
    /// <summary>
    /// Safe paper bot skeleton delegate.
    /// </summary>
    private readonly HermesPaperBot _bot = new();

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnStart()
    {
        _bot.StartPaperRuntime();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnTimer()
    {
        _bot.RunPaperRuntimeStep();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnStop()
    {
        _bot.StopPaperRuntime();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnException(Exception ex)
    {
        _bot.HandleRuntimeException(ex);
    }

    /// <summary>
    /// Returns the last in-memory runtime step result.
    /// </summary>
    public RuntimeStepResult? GetLastRuntimeStepResult()
        => _bot.GetLastRuntimeStepResult();
}
