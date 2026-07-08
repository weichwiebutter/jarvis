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
    /// Market context provider for paper-only runtime steps.
    /// </summary>
    private readonly IMarketContextProvider _marketContextProvider;

    /// <summary>
    /// Creates a host with a fixed fallback market context provider.
    /// </summary>
    public HermesPaperBotCloudHost()
        : this(new StaticMarketContextProvider(new RuntimeMarketContext()))
    {
    }

    /// <summary>
    /// Creates a host with a supplied market context provider.
    /// </summary>
    public HermesPaperBotCloudHost(IMarketContextProvider marketContextProvider)
    {
        _marketContextProvider = marketContextProvider ?? new StaticMarketContextProvider(new RuntimeMarketContext());
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnStart()
    {
        _bot.StartPaperRuntime(null, _marketContextProvider.Read());
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// </summary>
    public void OnTimer()
    {
        _bot.RunPaperRuntimeStep(_marketContextProvider.Read());
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

    /// <summary>
    /// Returns the embedded signal count from the current bot state.
    /// </summary>
    public int GetEmbeddedSignalCount()
        => _bot.GetEmbeddedSignalCount();

    /// <summary>
    /// Returns the embedded signal count for a symbol from the current bot state.
    /// </summary>
    public int GetEmbeddedSignalCount(string? symbol)
        => _bot.GetEmbeddedSignalCount(symbol);

    /// <summary>
    /// Returns the embedded signal package JSON length from the current bot state.
    /// </summary>
    public string GetEmbeddedSignalPackageJsonLength()
        => _bot.GetEmbeddedSignalPackageJsonLength();

    /// <summary>
    /// Returns the embedded signal package parse status from the current bot state.
    /// </summary>
    public string GetEmbeddedSignalParseStatus()
        => _bot.GetEmbeddedSignalParseStatus();

    /// <summary>
    /// Returns the embedded signal parse status for a symbol from the current bot state.
    /// </summary>
    public string GetEmbeddedSignalParseStatus(string? symbol)
        => _bot.GetEmbeddedSignalParseStatus(symbol);

    /// <summary>
    /// Returns the first embedded signal identifier from the current bot state.
    /// </summary>
    public string GetFirstEmbeddedSignalId()
        => _bot.GetFirstEmbeddedSignalId();

    /// <summary>
    /// Returns the first embedded signal identifier for a symbol from the current bot state.
    /// </summary>
    public string GetFirstEmbeddedSignalId(string? symbol)
        => _bot.GetFirstEmbeddedSignalId(symbol);
}
