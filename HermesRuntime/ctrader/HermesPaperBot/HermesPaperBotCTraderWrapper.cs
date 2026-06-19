using System;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

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
    /// Wrapper-local market context provider placeholder.
    /// </summary>
    private readonly CTraderMarketContextProvider _marketContextProvider = new();

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnStart()
    {
        _host = new HermesPaperBotCloudHost(_marketContextProvider);
        _marketContextProvider.Update(CaptureMarketContext());
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
        _marketContextProvider.Update(CaptureMarketContext());
        _host?.OnTimer();
        var result = _host?.GetLastRuntimeStepResult();
        Print($"state={result?.State ?? "unknown"}; paper_decision={result?.PaperDecision ?? "unknown"}; broker_action={result?.BrokerAction ?? "none"}; kill_switch_active={result?.KillSwitchActive ?? true}; symbol={result?.MarketContext?.CurrentSymbol ?? "unknown"}; spread={result?.MarketContext?.Spread ?? 0m}");
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

    /// <summary>
    /// Captures the current read-only market context from cTrader runtime values.
    /// </summary>
    private RuntimeMarketContext CaptureMarketContext()
    {
        var bid = Symbol.Bid;
        var ask = Symbol.Ask;
        return new RuntimeMarketContext
        {
            CurrentSymbol = SymbolName ?? string.Empty,
            CurrentTimeframe = Bars?.TimeFrame?.ToString() ?? string.Empty,
            Bid = bid,
            Ask = ask,
            Spread = ask > bid ? ask - bid : 0m,
            ServerTime = Server.Time,
        };
    }
}

/// <summary>
/// cTrader-cloud-local market context provider placeholder for future API wiring.
/// </summary>
public sealed class CTraderMarketContextProvider : IMarketContextProvider
{
    private RuntimeMarketContext _context = new();

    /// <summary>
    /// Updates the cached market context from a future cTrader runtime bridge.
    /// </summary>
    public void Update(RuntimeMarketContext context)
    {
        _context = context ?? new RuntimeMarketContext();
    }

    /// <summary>
    /// Reads the cached market context.
    /// </summary>
    public RuntimeMarketContext Read()
        => _context;
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
    private readonly HermesPaperBotCloudHost _host = new(new StaticMarketContextProvider(new RuntimeMarketContext()));

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
