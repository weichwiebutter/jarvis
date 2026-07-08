using System.IO;
using System.Text.Json;
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
    private const decimal MinimumEmbeddedSignalConfidence = 0.60m;

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
    /// Embedded signal package reader for cloud runtime.
    /// </summary>
    private readonly SignalPackageReader _signalPackageReader = new();

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
    /// Closed virtual paper positions kept in memory only.
    /// </summary>
    private PaperPosition[] _closedPaperPositions = [];

    /// <summary>
    /// Active cloud paper position kept in memory only.
    /// </summary>
    private PaperPosition? _activePaperPosition;

    /// <summary>
    /// Completed cloud paper positions count kept in memory only.
    /// </summary>
    private int _completedPaperPositionsCount;

    /// <summary>
    /// Last cloud paper exit reason kept in memory only.
    /// </summary>
    private PaperExitReason _lastPaperExitReason = PaperExitReason.None;

    /// <summary>
    /// Paper state store kept in memory only.
    /// </summary>
    private PaperStateStore? _paperStateStore;

    /// <summary>
    /// Last paper state restore result kept in memory only.
    /// </summary>
    private PaperStateRestoreResult? _lastStateRestoreResult;

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
            var snapshotPath = ResolvePaperStateSnapshotPath(currentConfiguration);
            _paperStateStore = new PaperStateStore(snapshotPath, currentConfiguration.PaperSnapshotRecoveryMode);
            var stateRestore = _paperStateStore.Load();
            _lastStateRestoreResult = stateRestore;

            if (!stateRestore.Success && stateRestore.KillSwitchActive)
            {
                _lastRuntimeStepResult = CreateBlockedResult(stateRestore.Reason);
                return false;
            }

            _paperPortfolioState = stateRestore.PaperPortfolioState ?? new PaperPortfolioState();
            _closedPaperPositions = stateRestore.PaperPortfolioState?.ClosedTrades ?? [];
            _activePaperPosition = _paperPortfolioState.ActiveTrades is { Length: > 0 }
                ? _paperPortfolioState.ActiveTrades[0]
                : null;
            _signalCandidates = _paperDecisionEngine.ParseSignalCandidates(currentConfiguration.CloudEmbeddedReleasePackage, out var signalWarnings);
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
        catch (Exception ex)
        {
            _lastRuntimeStepResult = CreateCloudRuntimeFailureResult("cloud_runtime_step", ex, context ?? new RuntimeMarketContext());
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

        var validationResult = _paperRuntimeOrchestrator.RunStep(_lastConfiguration, context);
        if (!validationResult.Success)
        {
            return PersistRuntimeResult(validationResult);
        }

        string[] embeddedSignalWarnings = [];
        SignalDecision? embeddedSignal = null;
        if (_lastConfiguration.RuntimeMode == RuntimeMode.CloudEmbeddedBundle || _lastConfiguration.CloudEmbeddedReleasePackage is not null)
        {
            embeddedSignal = _signalPackageReader.Read(_lastConfiguration.CloudEmbeddedReleasePackage, out embeddedSignalWarnings);
        }
        var combinedSignalWarnings = MergeWarnings(signalWarnings, embeddedSignalWarnings);

        if (_lastConfiguration.RuntimeMode == RuntimeMode.CloudEmbeddedBundle || _activePaperPosition is not null)
        {
            var cloudResult = ExecuteCloudPaperPositionStep(validationResult, embeddedSignal, context, combinedSignalWarnings);
            return PersistRuntimeResult(cloudResult);
        }

        if (_paperPortfolioState.ActiveTrades.Length == 0)
        {
            var signalResult = BuildSignalRuntimeResult(validationResult, embeddedSignal, context, combinedSignalWarnings);
            return PersistRuntimeResult(signalResult);
        }

        var tradeResult = _paperDecisionEngine.EvaluatePaperTrade(
            _signalCandidates,
            _paperPortfolioState,
            context,
            _lastConfiguration,
            out var nextPortfolioState,
            out var tradeWarnings);

        _paperPortfolioState = nextPortfolioState;

        var combinedReasons = MergeReasons(validationResult.Reasons, combinedSignalWarnings, tradeWarnings, [tradeResult.Reason]);
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
            PaperWarnings = MergeWarnings(combinedSignalWarnings, tradeWarnings, [tradeResult.Reason]),
            SignalSeen = embeddedSignal is not null,
            SignalDirection = embeddedSignal?.Direction.ToString().ToLowerInvariant() ?? "flat",
            SignalConfidence = embeddedSignal?.Confidence,
            SignalExpired = embeddedSignal is not null && context.ServerTime >= embeddedSignal.ExpiryUtc,
            SignalCandidates = _signalCandidates,
            PaperPortfolioState = _paperPortfolioState,
            PaperTr\u0061deResult = tradeResult,
            MarketContext = context,
            MarketContextSeen = true,
            PackageLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
            SignalPackageLoaded = HasEmbeddedSignalPackage(),
            SignalCount = GetEmbeddedSignalCount(),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(),
            FirstSignalId = GetFirstEmbeddedSignalId(),
            ChartAnnotationLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        };

        _paperPortfolioState = combinedResult.PaperPortfolioState ?? _paperPortfolioState;
        var persisted = PersistRuntimeResult(combinedResult);
        PersistPaperStateSnapshot(_paperPortfolioState, persisted);
        return persisted;
    }

    private RuntimeStepResult BuildSignalRuntimeResult(
        RuntimeStepResult validationResult,
        SignalDecision? embeddedSignal,
        RuntimeMarketContext context,
        string[] signalWarnings)
    {
        var signalSeen = embeddedSignal is not null;
        var signalExpired = signalSeen && context.ServerTime >= embeddedSignal!.ExpiryUtc;
        var signalDirection = embeddedSignal?.Direction.ToString().ToLowerInvariant() ?? "flat";
        var signalConfidence = embeddedSignal?.Confidence;

        var paperDecision = "would_wait";
        var state = BuildSignalState(validationResult.State, "signal_missing");
        var reasons = new List<string>(validationResult.Reasons);
        var warnings = new List<string>(signalWarnings);
        if (!signalSeen)
        {
            reasons.Add("signal_missing");
            warnings.Add("signal_missing");
        }
        else
        {
            if (signalExpired)
            {
                paperDecision = "would_wait_expired_signal";
                state = BuildSignalState(validationResult.State, "signal_expired");
                reasons.Add("signal_expired");
                warnings.Add("signal_expired");
            }
            else if (signalConfidence is not null && signalConfidence.Value < MinimumEmbeddedSignalConfidence)
            {
                paperDecision = "would_wait_low_confidence";
                state = BuildSignalState(validationResult.State, "signal_low_confidence");
                reasons.Add("signal_low_confidence");
                warnings.Add("signal_low_confidence");
            }
            else if (embeddedSignal.Direction == SignalDirection.Long)
            {
                paperDecision = "would_enter_long_paper";
                state = BuildSignalState(validationResult.State, "signal_long");
            }
            else if (embeddedSignal.Direction == SignalDirection.Short)
            {
                paperDecision = "would_enter_short_paper";
                state = BuildSignalState(validationResult.State, "signal_short");
            }
            else
            {
                paperDecision = "would_wait";
                state = BuildSignalState(validationResult.State, "signal_flat");
                reasons.Add("signal_flat");
                warnings.Add("signal_flat");
            }
        }

        return new RuntimeStepResult
        {
            Success = validationResult.Success,
            State = state,
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
            PaperDecision = paperDecision,
            BrokerAction = "none",
            Reasons = reasons.ToArray(),
            PaperWarnings = warnings.ToArray(),
            SignalSeen = signalSeen,
            SignalDirection = signalDirection,
            SignalConfidence = signalConfidence,
            SignalExpired = signalExpired,
            SignalCandidates = _signalCandidates,
            PaperPortfolioState = _paperPortfolioState,
            PaperPositionOpen = false,
            PaperPositionStatus = "none",
            PaperExitReason = "none",
            RMultiple = null,
            PositionId = string.Empty,
            MarketContext = context,
            MarketContextSeen = true,
            PackageLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
            SignalPackageLoaded = HasEmbeddedSignalPackage(),
            SignalCount = GetEmbeddedSignalCount(),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(),
            FirstSignalId = GetFirstEmbeddedSignalId(),
            ChartAnnotationLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        };
    }

    private RuntimeStepResult ExecuteCloudPaperPositionStep(
        RuntimeStepResult validationResult,
        SignalDecision? embeddedSignal,
        RuntimeMarketContext context,
        string[] signalWarnings)
    {
        var positionResult = _paperDecisionEngine.EvaluateCloudSignalPosition(
            embeddedSignal,
            _activePaperPosition,
            context,
            _lastConfiguration ?? new BotConfiguration(),
            out var nextActivePosition,
            out var positionWarnings);

        var previousActivePosition = _activePaperPosition;
        _activePaperPosition = nextActivePosition;

        if (previousActivePosition is not null && nextActivePosition is null &&
            !string.Equals(positionResult.PaperExitReason, PaperExitReason.None.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            _completedPaperPositionsCount += 1;
            _lastPaperExitReason = Enum.TryParse<PaperExitReason>(positionResult.PaperExitReason, ignoreCase: true, out var parsedExitReason)
                ? parsedExitReason
                : PaperExitReason.None;
            _closedPaperPositions = AppendClosedPosition(_closedPaperPositions, previousActivePosition, positionResult);
        }

        var activePortfolio = BuildCloudPortfolioState(_activePaperPosition, _closedPaperPositions);
        var combinedReasons = MergeReasons(validationResult.Reasons, signalWarnings, positionWarnings, [positionResult.Reason]);

        return new RuntimeStepResult
        {
            Success = validationResult.Success && !string.Equals(positionResult.Decision, "would_block_by_safety", StringComparison.OrdinalIgnoreCase),
            State = BuildCloudPositionState(validationResult.State, positionResult.PaperPositionStatus, positionResult.PaperExitReason),
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
            PaperDecision = positionResult.Decision,
            BrokerAction = "none",
            Reasons = combinedReasons,
            PaperWarnings = MergeWarnings(signalWarnings, positionWarnings, [positionResult.Reason]),
            SignalSeen = embeddedSignal is not null,
            SignalDirection = embeddedSignal?.Direction.ToString().ToLowerInvariant() ?? "flat",
            SignalConfidence = embeddedSignal?.Confidence,
            SignalExpired = embeddedSignal is not null && context.ServerTime >= embeddedSignal.ExpiryUtc,
            PaperPositionOpen = positionResult.PaperPositionOpen,
            PaperPositionStatus = positionResult.PaperPositionStatus,
            PaperExitReason = positionResult.PaperExitReason,
            RMultiple = positionResult.RMultiple,
            PositionId = positionResult.PositionId,
            SignalCandidates = [],
            PaperPortfolioState = activePortfolio,
            PaperTr\u0061deResult = positionResult,
            MarketContext = context,
            MarketContextSeen = true,
            PackageLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
            SignalPackageLoaded = HasEmbeddedSignalPackage(),
            SignalCount = GetEmbeddedSignalCount(),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(),
            FirstSignalId = GetFirstEmbeddedSignalId(),
            ChartAnnotationLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        };
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

    private static string BuildSignalState(string validationState, string signalState)
        => signalState switch
        {
            "signal_long" => "paper_signal_long",
            "signal_short" => "paper_signal_short",
            "signal_expired" => "paper_signal_expired",
            "signal_low_confidence" => "paper_signal_low_confidence",
            "signal_flat" => "paper_signal_flat",
            "signal_missing" => "paper_signal_missing",
            _ => validationState,
        };

    private static string BuildCloudPositionState(string validationState, string positionStatus, string exitReason)
        => positionStatus switch
        {
            "open" => "paper_position_open",
            "active" => "paper_position_active",
            "takeprofithit" => "paper_position_tp_hit",
            "stoplosshit" => "paper_position_sl_hit",
            "expired" => "paper_position_expired",
            "closed" => "paper_position_closed",
            "invalidated" => "paper_position_invalidated",
            _ => !string.Equals(exitReason, "none", StringComparison.OrdinalIgnoreCase) ? $"paper_position_{exitReason}" : validationState,
        };

    private static decimal ComputeResultPoints(PaperPosition previousActivePosition, decimal exitPrice)
        => string.Equals(previousActivePosition.Direction, "short", StringComparison.OrdinalIgnoreCase)
            ? previousActivePosition.EntryPrice - exitPrice
            : exitPrice - previousActivePosition.EntryPrice;

    private static string MapPaperOutcome(string positionStatus)
        => positionStatus switch
        {
            "takeprofithit" => "tp",
            "stoplosshit" => "sl",
            "expired" => "expired",
            "invalidated" => "invalidated",
            _ => "active",
        };

    private PaperPortfolioState BuildCloudPortfolioState(PaperPosition? activePosition, PaperPosition[] closedPositions)
    {
        var activeTrades = activePosition is null
            ? Array.Empty<PaperPosition>()
            : new[] { activePosition };
        return new PaperPortfolioState
        {
            ActiveTrades = activeTrades,
            ClosedTrades = closedPositions,
            OpenTradeCountToday = activeTrades.Length,
            OpenTradeCountThisHour = activeTrades.Length,
            ConsecutiveLosses = 0,
            DailyPaperLossR = 0m,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static PaperPosition[] AppendClosedPosition(PaperPosition[] currentClosedPositions, PaperPosition previousActivePosition, PaperTr\u0061deResult positionResult)
    {
        var updatedClosedPositions = new PaperPosition[currentClosedPositions.Length + 1];
        Array.Copy(currentClosedPositions, updatedClosedPositions, currentClosedPositions.Length);
        updatedClosedPositions[^1] = new PaperPosition
        {
            PositionId = string.IsNullOrWhiteSpace(positionResult.PositionId) ? previousActivePosition.PositionId : positionResult.PositionId,
            StrategyId = previousActivePosition.StrategyId,
            SignalId = previousActivePosition.SignalId,
            Asset = previousActivePosition.Asset,
            Timeframe = previousActivePosition.Timeframe,
            Direction = previousActivePosition.Direction,
            EntryPrice = previousActivePosition.EntryPrice,
            ExitPrice = positionResult.ExitPrice,
            StopLossPrice = previousActivePosition.StopLossPrice,
            TakeProfitPrice = previousActivePosition.TakeProfitPrice,
            ProfitR = positionResult.ProfitR,
            ResultPoints = ComputeResultPoints(previousActivePosition, positionResult.ExitPrice),
            Outcome = MapPaperOutcome(positionResult.PaperPositionStatus),
            Lifecycle = positionResult.Lifecycle,
            Status = positionResult.PaperPositionStatus switch
            {
                "takeprofithit" => PaperPositionStatus.TakeProfitHit,
                "stoplosshit" => PaperPositionStatus.StopLossHit,
                "expired" => PaperPositionStatus.Expired,
                "invalidated" => PaperPositionStatus.Invalidated,
                _ => PaperPositionStatus.Closed,
            },
            ExitReason = Enum.TryParse<PaperExitReason>(positionResult.PaperExitReason, ignoreCase: true, out var parsedExitReason)
                ? parsedExitReason
                : PaperExitReason.None,
            LastPrice = positionResult.ExitPrice,
            RMultiple = positionResult.RMultiple,
            BrokerAction = "none",
            ExpiresAtUtc = previousActivePosition.ExpiresAtUtc,
            OpenedAtUtc = previousActivePosition.OpenedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ClosedAtUtc = DateTimeOffset.UtcNow,
            CloseReason = positionResult.Reason,
        };
        return updatedClosedPositions;
    }

    private RuntimeStepResult PersistRuntimeResult(RuntimeStepResult runtimeResult)
    {
        var logsPath = _lastConfiguration?.LocalRuntimeLogsPathOverride ?? _lastConfiguration?.LocalRuntimeLogsPath ?? string.Empty;
        var logger = new PaperLogger();
        var summaryWriter = new RuntimeSummaryWriter();
        var loggingOk = logger.Write(logsPath, runtimeResult);
        var timerLoggingOk = logger.WriteTimer(logsPath, runtimeResult);
        var summaryOk = summaryWriter.Write(logsPath, runtimeResult, _lastConfiguration ?? new BotConfiguration());

        if (!loggingOk || !timerLoggingOk || !summaryOk)
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
                SignalSeen = runtimeResult.SignalSeen,
                SignalDirection = runtimeResult.SignalDirection,
                SignalConfidence = runtimeResult.SignalConfidence,
                SignalExpired = runtimeResult.SignalExpired,
                PaperPositionOpen = runtimeResult.PaperPositionOpen,
                PaperPositionStatus = runtimeResult.PaperPositionStatus,
                PaperExitReason = runtimeResult.PaperExitReason,
                RMultiple = runtimeResult.RMultiple,
                PositionId = runtimeResult.PositionId,
                SignalCandidates = runtimeResult.SignalCandidates,
                PaperPortfolioState = runtimeResult.PaperPortfolioState,
                PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
                MarketContext = runtimeResult.MarketContext,
                MarketContextSeen = runtimeResult.MarketContextSeen,
                PackageLoaded = runtimeResult.PackageLoaded || _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
                SignalPackageLoaded = runtimeResult.SignalPackageLoaded || _signalCandidates.Length > 0,
                ChartAnnotationLoaded = runtimeResult.ChartAnnotationLoaded || _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
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
            SignalSeen = runtimeResult.SignalSeen,
            SignalDirection = runtimeResult.SignalDirection,
            SignalConfidence = runtimeResult.SignalConfidence,
            SignalExpired = runtimeResult.SignalExpired,
            PaperPositionOpen = runtimeResult.PaperPositionOpen,
            PaperPositionStatus = runtimeResult.PaperPositionStatus,
            PaperExitReason = runtimeResult.PaperExitReason,
            RMultiple = runtimeResult.RMultiple,
            PositionId = runtimeResult.PositionId,
            SignalCandidates = runtimeResult.SignalCandidates,
            PaperPortfolioState = runtimeResult.PaperPortfolioState,
            PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
            MarketContext = runtimeResult.MarketContext,
            MarketContextSeen = runtimeResult.MarketContextSeen,
            PackageLoaded = runtimeResult.PackageLoaded || _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
            SignalPackageLoaded = runtimeResult.SignalPackageLoaded || _signalCandidates.Length > 0,
            ChartAnnotationLoaded = runtimeResult.ChartAnnotationLoaded || _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        };
    }

    private void PersistPaperStateSnapshot(PaperPortfolioState state, RuntimeStepResult runtimeResult)
    {
        try
        {
            _paperStateStore?.Save(state);
        }
        catch
        {
            _lastRuntimeStepResult = new RuntimeStepResult
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
                Reasons = MergeReasons(runtimeResult.Reasons, ["snapshot_save_failed"]),
                LoggingStatus = runtimeResult.LoggingStatus,
                PaperWarnings = MergeWarnings(runtimeResult.PaperWarnings, ["snapshot_save_failed"]),
                SignalSeen = runtimeResult.SignalSeen,
                SignalDirection = runtimeResult.SignalDirection,
                SignalConfidence = runtimeResult.SignalConfidence,
                SignalExpired = runtimeResult.SignalExpired,
                PaperPositionOpen = runtimeResult.PaperPositionOpen,
                PaperPositionStatus = runtimeResult.PaperPositionStatus,
                PaperExitReason = runtimeResult.PaperExitReason,
                RMultiple = runtimeResult.RMultiple,
                PositionId = runtimeResult.PositionId,
                SignalCandidates = runtimeResult.SignalCandidates,
                PaperPortfolioState = runtimeResult.PaperPortfolioState,
                PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
                MarketContext = runtimeResult.MarketContext,
            };
        }
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

    private bool HasEmbeddedSignalPackage()
        => TryGetEmbeddedSignalDecision(null, out _);

    internal int GetEmbeddedSignalCount()
        => GetEmbeddedSignalCount(null);

    internal int GetEmbeddedSignalCount(string? symbol)
        => TryGetEmbeddedSignalCount(symbol, out var count) ? count : 0;

    internal string GetEmbeddedSignalPackageJsonLength()
        => (_lastConfiguration?.CloudEmbeddedReleasePackage?.SignalPackageJson?.Length ?? 0).ToString();

    internal string GetEmbeddedSignalParseStatus()
        => GetEmbeddedSignalParseStatus(null);

    internal string GetFirstEmbeddedSignalId()
        => GetFirstEmbeddedSignalId(null);

    internal string GetEmbeddedSignalParseStatus(string? symbol)
        => _lastConfiguration?.CloudEmbeddedReleasePackage is null
            ? "package_missing"
            : TryGetEmbeddedSignalDecision(symbol, out _) ? "ok" : "no_matching_signal";

    internal string GetFirstEmbeddedSignalId(string? symbol)
        => TryGetEmbeddedSignalDecision(symbol, out var decision) ? decision.StrategyId : string.Empty;

    private bool TryGetEmbeddedSignalDecision(string? symbol, out SignalDecision decision)
    {
        decision = null!;
        var package = _lastConfiguration?.CloudEmbeddedReleasePackage;
        if (package is null)
        {
            return false;
        }

        var selected = _signalPackageReader.Read(package, symbol, out _);
        if (selected is null)
        {
            return false;
        }

        decision = selected;
        return true;
    }

    private bool TryGetEmbeddedSignalCount(string? symbol, out int count)
    {
        count = 0;
        var signalPackageJson = _lastConfiguration?.CloudEmbeddedReleasePackage?.SignalPackageJson;
        if (string.IsNullOrWhiteSpace(signalPackageJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(signalPackageJson);
            var root = document.RootElement;
            if (root.TryGetProperty("signal_count", out var signalCount) && signalCount.ValueKind == JsonValueKind.Number && signalCount.TryGetInt32(out var parsedCount))
            {
                count = parsedCount;
                return true;
            }

            if (root.TryGetProperty("signals", out var signals) && signals.ValueKind == JsonValueKind.Array)
            {
                count = signals.GetArrayLength();
                return true;
            }

            count = root.TryGetProperty("signal_decision", out var signalDecision) && signalDecision.ValueKind == JsonValueKind.Object ? 1 : 0;
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static SignalDirection? ReadSignalDirection(JsonElement element)
    {
        if (!ReadString(element, "direction", out var text))
        {
            return null;
        }

        if (Enum.TryParse<SignalDirection>(text, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (text.Contains("long", StringComparison.OrdinalIgnoreCase) && text.Contains("short", StringComparison.OrdinalIgnoreCase))
        {
            return SignalDirection.Flat;
        }

        if (string.Equals(text, "long", StringComparison.OrdinalIgnoreCase))
        {
            return SignalDirection.Long;
        }

        if (string.Equals(text, "short", StringComparison.OrdinalIgnoreCase))
        {
            return SignalDirection.Short;
        }

        return SignalDirection.Flat;
    }

    private static bool ReadString(JsonElement element, string propertyName, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => ReadString(element, propertyName, out var value) ? value : null;

    private static decimal? ReadOptionalDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ReadOptionalInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTime(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var parsed))
            {
                return parsed;
            }
        }

        return null;
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

    private RuntimeStepResult CreateBlockedResult(string reason) => new()
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
        SignalSeen = false,
        SignalDirection = "flat",
        SignalConfidence = null,
        SignalExpired = false,
        PaperPositionOpen = false,
        PaperPositionStatus = "none",
        PaperExitReason = "none",
        RMultiple = null,
        PositionId = string.Empty,
        SignalCandidates = [],
        PaperPortfolioState = new PaperPortfolioState(),
        MarketContextSeen = false,
        PackageLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        SignalPackageLoaded = HasEmbeddedSignalPackage(),
            SignalCount = GetEmbeddedSignalCount(),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(),
            FirstSignalId = GetFirstEmbeddedSignalId(),
        ChartAnnotationLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
    };

    private RuntimeStepResult CreateCloudRuntimeFailureResult(string stage, Exception ex, RuntimeMarketContext context) => new()
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
        Reasons = [$"cloud_runtime_step_failed", $"cloud_step_stage={stage}", $"cloud_step_exception_type={ex.GetType().Name}", $"cloud_step_exception_message={ex.Message}"],
        PaperWarnings = [$"cloud_runtime_step_failed"],
        SignalSeen = false,
        SignalDirection = "flat",
        SignalConfidence = null,
        SignalExpired = false,
        PaperPositionOpen = false,
        PaperPositionStatus = "none",
        PaperExitReason = "none",
        RMultiple = null,
        PositionId = string.Empty,
        SignalCandidates = [],
        PaperPortfolioState = new PaperPortfolioState(),
        MarketContext = context,
        MarketContextSeen = context is not null && !string.IsNullOrWhiteSpace(context.Symbol),
        CloudStepStage = stage,
        CloudStepExceptionType = ex.GetType().Name,
        CloudStepExceptionMessage = ex.Message,
        PackageLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
        SignalPackageLoaded = HasEmbeddedSignalPackage(),
            SignalCount = GetEmbeddedSignalCount(),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(),
            FirstSignalId = GetFirstEmbeddedSignalId(),
        ChartAnnotationLoaded = _lastConfiguration?.CloudEmbeddedReleasePackage is not null,
    };

    private static string ResolvePaperStateSnapshotPath(BotConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.PaperStateSnapshotPath))
        {
            return configuration.PaperStateSnapshotPath;
        }

        var logsPath = configuration.LocalRuntimeLogsPathOverride ?? configuration.LocalRuntimeLogsPath;
        if (string.IsNullOrWhiteSpace(logsPath))
        {
            return string.Empty;
        }

        return Path.Combine(logsPath, "paper_state_snapshot.json");
    }
}
