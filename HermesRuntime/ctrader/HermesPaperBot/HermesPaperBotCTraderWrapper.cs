using System;
using HermesPaperBot.Models;

#if HERMES_CTRADER_WRAPPER
using cAlgo.API;
#endif

namespace HermesPaperBot.Bot;

/// <summary>
/// cTrader cloud wrapper that only delegates lifecycle handling to the safe paper host.
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
#if HERMES_CTRADER_WRAPPER
[Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
public class HermesPaperBotCTraderWrapper : Robot
{
    /// <summary>
    /// Safe paper host delegate.
    /// </summary>
    private HermesPaperBotCloudHost? _host;

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnStart()
    {
        _host = new HermesPaperBotCloudHost();
        _host.OnStart();
        Timer.Start(30);
        Print("paper-only start: host delegated, broker_action=none");
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnTimer()
    {
        _host?.OnTimer();
        var result = _host?.GetLastRuntimeStepResult();
        Print($"state={result?.State ?? "unknown"}; paper_decision={result?.PaperDecision ?? "unknown"}; broker_action={result?.BrokerAction ?? "none"}; kill_switch_active={result?.KillSwitchActive ?? true}");
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnStop()
    {
        try
        {
            _host?.OnStop();
        }
        finally
        {
            Timer.Stop();
            Print("paper-only stopped");
        }
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnException(Exception ex)
    {
        _host?.OnException(ex);
        Print("defensive exception handled");
    }
}
#else
/// <summary>
/// Local paper-host wrapper stub used when the cTrader SDK is unavailable.
/// </summary>
public class HermesPaperBotCTraderWrapper
{
    /// <summary>
    /// Safe paper host delegate.
    /// </summary>
    private readonly HermesPaperBotCloudHost _host = new();

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnStart()
    {
        _host.OnStart();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnTimer()
    {
        _host.OnTimer();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnStop()
    {
        _host.OnStop();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnException(Exception ex)
    {
        _host.OnException(ex);
    }

    /// <summary>
    /// Returns the last in-memory runtime step result.
    /// </summary>
    public RuntimeStepResult? GetLastRuntimeStepResult()
        => _host.GetLastRuntimeStepResult();
}
#endif
