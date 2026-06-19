using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace HermesPaperBot.Bot;

/// <summary>
/// Paper-only cBot skeleton placeholder.
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
public sealed class HermesPaperBot
{
    /// <summary>
    /// Defensive cloud embedded bootstrapper.
    /// </summary>
    private readonly CloudEmbeddedPackageBootstrapper _cloudEmbeddedPackageBootstrapper = new();

    /// <summary>
    /// Defensive paper runtime orchestrator.
    /// </summary>
    private readonly PaperRuntimeOrchestrator _paperRuntimeOrchestrator = new();

    /// <summary>
    /// Last runtime configuration kept in memory only.
    /// </summary>
    private BotConfiguration? _lastConfiguration;

    /// <summary>
    /// Last cloud bootstrap result kept in memory only.
    /// </summary>
    private CloudBootstrapResult? _lastCloudBootstrapResult;

    /// <summary>
    /// Last paper runtime step result kept in memory only.
    /// </summary>
    private RuntimeStepResult? _lastRuntimeStepResult;

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    /// no_auto_trading=true
    /// human_review_required=true
    /// broker_trading_enabled=false
    /// live_trading_enabled=false
    /// order_api_enabled=false
    /// paper_mode=true
    ///
    /// Prepares the cloud embedded bootstrap in memory only.
    /// </summary>
    public void OnStart()
    {
        StartPaperRuntime();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Bootstraps cloud configuration and stores the last result defensively.
    /// </summary>
    public bool StartPaperRuntime()
    {
        try
        {
            var bootstrapResult = _cloudEmbeddedPackageBootstrapper.CreateCloudConfiguration();
            _lastCloudBootstrapResult = bootstrapResult;
            _lastConfiguration = bootstrapResult.Configuration;

            if (!bootstrapResult.Success || bootstrapResult.Configuration is null)
            {
                _lastRuntimeStepResult = CreateBlockedResult(bootstrapResult.Reason ?? "cloud_bootstrap_failed");
                return false;
            }

            _lastRuntimeStepResult = _paperRuntimeOrchestrator.RunStep(bootstrapResult.Configuration);
            return _lastRuntimeStepResult.Success;
        }
        catch
        {
            _lastRuntimeStepResult = CreateBlockedResult("cloud_start_failed");
            return false;
        }
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Runs one defensive runtime step using the last prepared cloud configuration.
    /// </summary>
    public RuntimeStepResult RunPaperRuntimeStep()
    {
        try
        {
            if (_lastConfiguration is null)
            {
                StartPaperRuntime();
            }

            if (_lastConfiguration is null)
            {
                _lastRuntimeStepResult = CreateBlockedResult("cloud_configuration_missing");
                return _lastRuntimeStepResult;
            }

            _lastRuntimeStepResult = _paperRuntimeOrchestrator.RunStep(_lastConfiguration);
            return _lastRuntimeStepResult;
        }
        catch
        {
            _lastRuntimeStepResult = CreateBlockedResult("cloud_runtime_step_failed");
            return _lastRuntimeStepResult;
        }
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Alias for one defensive runtime step.
    /// </summary>
    public void OnTimer()
    {
        RunPaperRuntimeStep();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Updates lightweight market context only.
    /// </summary>
    public void OnTick()
    {
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Optional bar-based setup evaluation placeholder.
    /// </summary>
    public void OnBar()
    {
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Writes shutdown summary for the paper-only runtime, defensively.
    /// </summary>
    public void OnStop()
    {
        StopPaperRuntime();
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Keeps shutdown handling defensive and summary-ready.
    /// </summary>
    public void StopPaperRuntime()
    {
    }

    /// <summary>
    /// Returns the last in-memory runtime step result.
    /// </summary>
    public RuntimeStepResult? GetLastRuntimeStepResult()
        => _lastRuntimeStepResult;

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Activates kill-switch and isolates unexpected exceptions defensively.
    /// </summary>
    public void OnException()
    {
        _lastRuntimeStepResult ??= CreateBlockedResult("unexpected_exception");
    }

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Handles an exception defensively and records a blocked in-memory result.
    /// </summary>
    public void HandleRuntimeException(Exception ex)
    {
        _lastRuntimeStepResult = CreateBlockedResult(string.IsNullOrWhiteSpace(ex.Message) ? "exception_handled" : $"exception_handled:{ex.Message}");
    }

    private static RuntimeStepResult CreateBlockedResult(string reason) => new()
    {
        Success = false,
        State = "blocked_by_safety",
        ConfigValid = false,
        ImportAttempted = false,
        ImportValid = false,
        BundleValid = false,
        ChecksumValid = false,
        SafetyAllowed = false,
        DriftAllowed = false,
        KillSwitchActive = true,
        FallbackPossible = false,
        DisabledUntilValidBundle = true,
        PaperDecision = "would_block_by_safety",
        BrokerAction = "none",
        Reasons = [reason],
    };
}
