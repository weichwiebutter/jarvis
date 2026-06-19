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
    /// Paper decision engine for virtual trade steps.
    /// </summary>
    private readonly PaperDecisionEngine _paperDecisionEngine = new();

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
    /// Parsed signal candidates kept in memory only.
    /// </summary>
    private SignalCandidate[] _signalCandidates = [];

    /// <summary>
    /// Virtual paper portfolio kept in memory only.
    /// </summary>
    private PaperPortfolioState _paperPortfolioState = new();

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
        => StartPaperRuntime(null, null);

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Starts the paper runtime from a supplied configuration or the cloud bootstrap.
    /// </summary>
    public bool StartPaperRuntime(BotConfiguration? configuration, RuntimeMarketContext? context = null)
    {
        try
        {
            CloudBootstrapResult bootstrapResult;
            if (configuration is null)
            {
                bootstrapResult = _cloudEmbeddedPackageBootstrapper.CreateCloudConfiguration();
                _lastCloudBootstrapResult = bootstrapResult;
                _lastConfiguration = bootstrapResult.Configuration;

                if (!bootstrapResult.Success || bootstrapResult.Configuration is null)
                {
                    _lastRuntimeStepResult = CreateBlockedResult(bootstrapResult.Reason ?? "cloud_bootstrap_failed");
                    return false;
                }
            }
            else
            {
                _lastConfiguration = configuration;
                _lastCloudBootstrapResult = new CloudBootstrapResult
                {
                    Success = true,
                    Status = "custom_configuration",
                    Reason = "ok",
                    Configuration = configuration,
                };
            }

            var currentConfiguration = _lastConfiguration ?? new BotConfiguration();
            _signalCandidates = _paperDecisionEngine.ParseSignalCandidates(currentConfiguration.CloudEmbeddedReleasePackage, out var signalWarnings);
            _paperPortfolioState = new PaperPortfolioState();
            _lastRuntimeStepResult = ExecutePaperRuntimeStep(context ?? new RuntimeMarketContext(), signalWarnings);
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
        => RunPaperRuntimeStep(new RuntimeMarketContext());

    /// <summary>
    /// paper_only
    /// broker_action=none
    /// no order API
    ///
    /// Runs one defensive runtime step using the supplied runtime market context.
    /// </summary>
    public RuntimeStepResult RunPaperRuntimeStep(RuntimeMarketContext? context)
    {
        try
        {
            return _lastRuntimeStepResult = ExecutePaperRuntimeStep(context ?? new RuntimeMarketContext(), []);
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

    private RuntimeStepResult ExecutePaperRuntimeStep(RuntimeMarketContext context, string[] signalWarnings)
    {
        if (_lastConfiguration is null)
        {
            return CreateBlockedResult("cloud_configuration_missing");
        }

        var validationResult = _paperRuntimeOrchestrator.RunStep(_lastConfiguration);
        if (!validationResult.Success)
        {
            return PersistRuntimeResult(validationResult);
        }

        var tradeResult = _paperDecisionEngine.EvaluatePaperTrade(
            _signalCandidates,
            _paperPortfolioState,
            context,
            _lastConfiguration,
            out var nextPortfolioState,
            out var tradeWarnings);

        _paperPortfolioState = nextPortfolioState;

        var combinedReasons = MergeReasons(validationResult.Reasons, signalWarnings, tradeWarnings, [tradeResult.Reason]);
        var combinedResult = new RuntimeStepResult
        {
            Success = validationResult.Success && !string.Equals(tradeResult.Decision, "would_block_by_safety", StringComparison.OrdinalIgnoreCase),
            State = BuildCombinedState(validationResult.State, tradeResult.Lifecycle),
            ConfigValid = validationResult.ConfigValid,
            ImportAttempted = validationResult.ImportAttempted,
            ImportValid = validationResult.ImportValid,
            BundleValid = validationResult.BundleValid,
            ChecksumValid = validationResult.ChecksumValid,
            SafetyAllowed = validationResult.SafetyAllowed,
            DriftAllowed = validationResult.DriftAllowed,
            KillSwitchActive = validationResult.KillSwitchActive,
            FallbackPossible = validationResult.FallbackPossible,
            DisabledUntilValidBundle = validationResult.DisabledUntilValidBundle,
            PaperDecision = tradeResult.Decision,
            BrokerAction = "none",
            Reasons = combinedReasons,
            PaperWarnings = MergeWarnings(signalWarnings, tradeWarnings, [tradeResult.Reason]),
            SignalCandidates = _signalCandidates,
            PaperPortfolioState = _paperPortfolioState,
            PaperTr\u0061deResult = tradeResult,
        };

        return PersistRuntimeResult(combinedResult);
    }

    private static string BuildCombinedState(string validationState, PaperTradeLifecycle lifecycle)
    {
        return lifecycle switch
        {
            PaperTradeLifecycle.Open => "paper_trade_open",
            PaperTradeLifecycle.Active => "paper_trade_active",
            PaperTradeLifecycle.TakeProfitHit => "paper_trade_tp_hit",
            PaperTradeLifecycle.StopLossHit => "paper_trade_sl_hit",
            PaperTradeLifecycle.Invalidated => "paper_trade_invalidated",
            PaperTradeLifecycle.Expired => "paper_trade_expired",
            PaperTradeLifecycle.Closed => validationState,
            _ => validationState,
        };
    }

    private RuntimeStepResult PersistRuntimeResult(RuntimeStepResult runtimeResult)
    {
        var logsPath = _lastConfiguration?.LocalRuntimeLogsPathOverride ?? _lastConfiguration?.LocalRuntimeLogsPath ?? string.Empty;
        var logger = new PaperLogger();
        var summaryWriter = new RuntimeSummaryWriter();
        var loggingOk = logger.Write(logsPath, runtimeResult);
        var summaryOk = summaryWriter.Write(logsPath, runtimeResult, _lastConfiguration ?? new BotConfiguration());

        if (!loggingOk || !summaryOk)
        {
            var loggingReasons = new List<string>(runtimeResult.Reasons)
            {
                "logging_failed",
            };

            return new RuntimeStepResult
            {
                Success = runtimeResult.Success,
                State = runtimeResult.State,
                ConfigValid = runtimeResult.ConfigValid,
                ImportAttempted = runtimeResult.ImportAttempted,
                ImportValid = runtimeResult.ImportValid,
                BundleValid = runtimeResult.BundleValid,
                ChecksumValid = runtimeResult.ChecksumValid,
                SafetyAllowed = runtimeResult.SafetyAllowed,
                DriftAllowed = runtimeResult.DriftAllowed,
                KillSwitchActive = runtimeResult.KillSwitchActive,
                FallbackPossible = runtimeResult.FallbackPossible,
                DisabledUntilValidBundle = runtimeResult.DisabledUntilValidBundle,
                PaperDecision = runtimeResult.PaperDecision,
                BrokerAction = "none",
                Reasons = loggingReasons.ToArray(),
                LoggingStatus = "logging_failed",
                PaperWarnings = runtimeResult.PaperWarnings,
                SignalCandidates = runtimeResult.SignalCandidates,
                PaperPortfolioState = runtimeResult.PaperPortfolioState,
                PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
            };
        }

        return new RuntimeStepResult
        {
            Success = runtimeResult.Success,
            State = runtimeResult.State,
            ConfigValid = runtimeResult.ConfigValid,
            ImportAttempted = runtimeResult.ImportAttempted,
            ImportValid = runtimeResult.ImportValid,
            BundleValid = runtimeResult.BundleValid,
            ChecksumValid = runtimeResult.ChecksumValid,
            SafetyAllowed = runtimeResult.SafetyAllowed,
            DriftAllowed = runtimeResult.DriftAllowed,
            KillSwitchActive = runtimeResult.KillSwitchActive,
            FallbackPossible = runtimeResult.FallbackPossible,
            DisabledUntilValidBundle = runtimeResult.DisabledUntilValidBundle,
            PaperDecision = runtimeResult.PaperDecision,
            BrokerAction = "none",
            Reasons = runtimeResult.Reasons,
            LoggingStatus = "ok",
            PaperWarnings = runtimeResult.PaperWarnings,
            SignalCandidates = runtimeResult.SignalCandidates,
            PaperPortfolioState = runtimeResult.PaperPortfolioState,
            PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
        };
    }

    private static string[] MergeReasons(params string[][] reasonGroups)
    {
        var reasons = new List<string>();
        foreach (var group in reasonGroups)
        {
            if (group is null)
            {
                continue;
            }

            foreach (var reason in group)
            {
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    reasons.Add(reason);
                }
            }
        }

        return reasons.ToArray();
    }

    private static string[] MergeWarnings(params string[][] warningGroups)
    {
        var warnings = new List<string>();
        foreach (var group in warningGroups)
        {
            if (group is null)
            {
                continue;
            }

            foreach (var warning in group)
            {
                if (!string.IsNullOrWhiteSpace(warning))
                {
                    warnings.Add(warning);
                }
            }
        }

        return warnings.ToArray();
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
        PaperWarnings = [reason],
        SignalCandidates = [],
        PaperPortfolioState = new PaperPortfolioState(),
    };
}
