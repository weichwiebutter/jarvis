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
        var context = CaptureMarketContext();
        _marketContextProvider.Update(context);
        _host.OnStart();
        Timer.Start(30);
        Print($"HermesPaperBot OnStart; paper_mode=true; broker_action=none; market_context_seen=true; symbol={context.CurrentSymbol}; timeframe={context.CurrentTimeframe}; spread={context.Spread}; decision=starting");
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    protected override void OnTimer()
    {
        var context = CaptureMarketContext();
        _marketContextProvider.Update(context);
        _host?.OnTimer();
        var result = _host?.GetLastRuntimeStepResult();
        var currentContext = result?.MarketContext ?? context;
        Print($"HermesPaperBot OnTimer; paper_mode=true; broker_action={result?.BrokerAction ?? "none"}; market_context_seen={result?.MarketContextSeen ?? true}; state={result?.State ?? "unknown"}; decision={result?.PaperDecision ?? "unknown"}; symbol={currentContext.CurrentSymbol}; timeframe={currentContext.CurrentTimeframe}; spread={currentContext.Spread}; kill_switch_active={result?.KillSwitchActive ?? true}");
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
            var result = _host?.GetLastRuntimeStepResult();
            Print($"HermesPaperBot OnStop; paper_mode=true; broker_action={result?.BrokerAction ?? "none"}; state={result?.State ?? "unknown"}; decision={result?.PaperDecision ?? "unknown"}");
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
        Print($"HermesPaperBot OnException; paper_mode=true; broker_action=none; exception={ex.GetType().Name}; message={ex.Message}");
    }

    /// <summary>
    /// Captures the current read-only market context from cTrader runtime values.
    /// </summary>
    private RuntimeMarketContext CaptureMarketContext()
    {
        var bid = (decimal)Symbol.Bid;
        var ask = (decimal)Symbol.Ask;
        var spread = ask > bid ? ask - bid : 0m;
        var pipSize = (decimal)Symbol.PipSize;
        return new RuntimeMarketContext
        {
            Symbol = SymbolName ?? string.Empty,
            Timeframe = Bars?.TimeFrame?.ToString() ?? string.Empty,
            Bid = bid,
            Ask = ask,
            Spread = spread,
            SpreadPips = pipSize > 0m ? spread / pipSize : null,
            TickSize = (decimal)Symbol.TickSize,
            PipSize = pipSize,
            ServerTime = Server.Time,
            Source = "ctrader_wrapper",
        };
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
