using System;
using System.Linq;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

#if HERMES_CTRADER_WRAPPER
using cAlgo.API;
#endif

[assembly: System.Reflection.AssemblyMetadata("build_stamp", "20260707_timer_diag_v2")]
[assembly: System.Reflection.AssemblyMetadata("log_format_version", "timer_diag_v2")]

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
    /// Visible diagnostic stamp for the cBot build.
    /// </summary>
    private const string BuildStamp = "20260707_timer_diag_v2";

    /// <summary>
    /// Visible diagnostic log format version.
    /// </summary>
    private const string LogFormatVersion = "timer_diag_v2";

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
        Print(
            $"HermesPaperBot OnStart; build_stamp={BuildStamp}; log_format_version={LogFormatVersion}; assembly_version={GetAssemblyVersion()}; " +
            $"paper_mode=true; broker_action=none; market_context_seen=true; symbol={context.CurrentSymbol}; timeframe={context.CurrentTimeframe}; spread={context.Spread}; decision=starting");
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
        var marketContextSeen = HasReadableMarketContext(currentContext);
        var safetyBlockReason = GetSafetyBlockReason(result);
        var signalCount = _host?.GetEmbeddedSignalCount() ?? 0;
        var signalPackageJsonLength = _host?.GetEmbeddedSignalPackageJsonLength() ?? "0";
        var signalPackageParseStatus = _host?.GetEmbeddedSignalParseStatus() ?? "unknown";
        var firstSignalId = _host?.GetFirstEmbeddedSignalId() ?? "none";
        Print(
            $"HermesPaperBot OnTimer; build_stamp={BuildStamp}; log_format_version={LogFormatVersion}; assembly_version={GetAssemblyVersion()}; " +
            $"paper_mode=true; broker_action={result?.BrokerAction ?? "none"}; market_context_seen={marketContextSeen}; " +
            $"state={result?.State ?? "unknown"}; decision={result?.PaperDecision ?? "unknown"}; symbol={currentContext.CurrentSymbol}; timeframe={currentContext.CurrentTimeframe}; " +
            $"market_context_bid={currentContext.Bid}; market_context_ask={currentContext.Ask}; market_context_mid={GetMidPrice(currentContext)}; spread={currentContext.Spread}; " +
            $"server_time_seen={currentContext.ServerTime != default}; spread_source={GetSpreadSource(currentContext)}; " +
            $"kill_switch_active={result?.KillSwitchActive ?? true}; safety_block_reason={safetyBlockReason}; " +
            $"cloud_step_stage={result?.CloudStepStage ?? "none"}; cloud_step_exception_type={result?.CloudStepExceptionType ?? "none"}; cloud_step_exception_message={result?.CloudStepExceptionMessage ?? "none"}; " +
            $"package_loaded={result?.PackageLoaded.ToString().ToLowerInvariant() ?? "false"}; signal_package_loaded={result?.SignalPackageLoaded.ToString().ToLowerInvariant() ?? "false"}; signal_count={signalCount}; signal_package_json_length={signalPackageJsonLength}; signal_package_parse_status={signalPackageParseStatus}; first_signal_id={firstSignalId}; chart_annotation_loaded={result?.ChartAnnotationLoaded.ToString().ToLowerInvariant() ?? "false"}");
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

    /// <summary>
    /// Determines whether the wrapper captured a readable market context.
    /// </summary>
    private static bool HasReadableMarketContext(RuntimeMarketContext context)
        => context is not null
            && !string.IsNullOrWhiteSpace(context.Symbol)
            && context.Bid > 0m
            && context.Ask > 0m
            && context.ServerTime != default;

    /// <summary>
    /// Returns the mid price if bid/ask are available.
    /// </summary>
    private static decimal GetMidPrice(RuntimeMarketContext context)
        => context.Bid > 0m && context.Ask > 0m ? (context.Bid + context.Ask) / 2m : 0m;

    /// <summary>
    /// Describes how the spread value was obtained.
    /// </summary>
    private static string GetSpreadSource(RuntimeMarketContext context)
    {
        if (context.SpreadPips.HasValue)
        {
            return "spread_pips";
        }

        if (context.PipSize > 0m)
        {
            return "spread_from_bid_ask";
        }

        return "spread_unknown";
    }

    /// <summary>
    /// Extracts the most relevant safety block reason from the latest runtime result.
    /// </summary>
    private static string GetSafetyBlockReason(RuntimeStepResult? result)
    {
        if (result is null)
        {
            return "runtime_result_missing";
        }

        if (!result.KillSwitchActive)
        {
            return "ok";
        }

        var reason = result.Reasons?.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry)) ?? string.Empty;
        return string.IsNullOrWhiteSpace(reason) ? "none" : reason;
    }

    /// <summary>
    /// Returns the runtime assembly version when available.
    /// </summary>
    private static string GetAssemblyVersion()
        => typeof(HermesPaperBotCTraderWrapper).Assembly.GetName().Version?.ToString() ?? "n/a";
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
