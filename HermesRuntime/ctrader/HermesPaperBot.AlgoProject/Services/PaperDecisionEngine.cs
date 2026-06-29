namespace HermesPaperBot.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Computes paper-only decisions and virtual paper-trade steps.
/// </summary>
public sealed class PaperDecisionEngine
{
    private const decimal MinimumEmbeddedSignalConfidence = 0.60m;

    /// <summary>
    /// Evaluates a paper-only decision placeholder for the safety orchestrator.
    /// </summary>
    public DecisionResult Evaluate(BotState state, RuntimeMarketContext context)
    {
        if (state is not null && state.KillSwitchActive)
        {
            return new DecisionResult
            {
                Decision = "would_block_by_safety",
                BrokerAction = "none",
                Reason = "kill_switch_active",
            };
        }

        return new DecisionResult
        {
            Decision = "would_wait",
            BrokerAction = "none",
            Reason = "ok",
        };
    }

    /// <summary>
    /// Parses signal candidates from the embedded strategy JSON.
    /// Missing fields are reported as warnings and do not crash parsing.
    /// </summary>
    public SignalCandidate[] ParseSignalCandidates(CloudEmbeddedReleasePackage? package, out string[] warnings)
    {
        var collectedWarnings = new List<string>();
        var candidates = new List<SignalCandidate>();

        if (package is null)
        {
            collectedWarnings.Add("embedded_package_missing");
            warnings = collectedWarnings.ToArray();
            return [];
        }

        if (package.ReleaseMode != ReleaseMode.PaperOnly)
        {
            collectedWarnings.Add("embedded_package_not_paper_only");
            warnings = collectedWarnings.ToArray();
            return [];
        }

        if (string.IsNullOrWhiteSpace(package.EmbeddedStrategyJson))
        {
            collectedWarnings.Add("embedded_strategy_json_missing");
            warnings = collectedWarnings.ToArray();
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(package.EmbeddedStrategyJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                collectedWarnings.Add("embedded_strategy_json_not_object");
                warnings = collectedWarnings.ToArray();
                return [];
            }

            if (!TryGetString(root, "release_mode", out var releaseMode) || !string.Equals(releaseMode, "paper_only", StringComparison.OrdinalIgnoreCase))
            {
                collectedWarnings.Add("embedded_strategy_json_rejected_release_mode");
                warnings = collectedWarnings.ToArray();
                return [];
            }

            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
            {
                collectedWarnings.Add("embedded_strategy_assets_missing");
                warnings = collectedWarnings.ToArray();
                return [];
            }

            foreach (var assetElement in assetsElement.EnumerateArray())
            {
                if (assetElement.ValueKind != JsonValueKind.Object)
                {
                    collectedWarnings.Add("embedded_strategy_asset_not_object");
                    continue;
                }

                var candidateWarnings = new List<string>();
                var asset = TryGetString(assetElement, "asset", out var assetValue) ? assetValue : string.Empty;
                var timeframe = TryGetString(assetElement, "timeframe", out var timeframeValue) ? timeframeValue : string.Empty;
                var direction = TryGetString(assetElement, "direction", out var directionValue) ? directionValue : string.Empty;
                var setupId = TryGetString(assetElement, "setup_id", out var setupIdValue) ? setupIdValue : string.Empty;
                var setupName = TryGetString(assetElement, "setup_name", out var setupNameValue) ? setupNameValue : string.Empty;
                var primaryCandidate = TryGetString(assetElement, "primary_candidate", out var primaryCandidateValue) ? primaryCandidateValue : string.Empty;
                var readiness = TryGetString(assetElement, "readiness", out var readinessValue) ? readinessValue : string.Empty;
                var paperEntryEnabled = TryGetBool(assetElement, "paper_entry_enabled", out var paperEntryEnabledValue) && paperEntryEnabledValue;
                var confidenceBaseline = TryGetDecimal(assetElement, "confidence_baseline", out var confidenceValue) ? confidenceValue : 0m;
                var maxSpread = TryGetDecimal(assetElement, "max_spread", out var maxSpreadValue) ? maxSpreadValue : 0.25m;
                var stopLossR = TryGetDecimal(assetElement, "stop_loss_r", out var stopLossValue) ? stopLossValue : 1m;
                var takeProfitR = TryGetDecimal(assetElement, "take_profit_r", out var takeProfitValue) ? takeProfitValue : 1m;
                var expiresAtUtc = TryGetDateTime(assetElement, "expires_at_utc", out var expiresAtValue) ? expiresAtValue : null;

                var entryLogic = TryGetStringArray(assetElement, "entry_logic", candidateWarnings);
                var exitLogic = TryGetStringArray(assetElement, "exit_logic", candidateWarnings);
                var stopLossLogic = TryGetStringArray(assetElement, "stop_loss_logic", candidateWarnings);
                var takeProfitLogic = TryGetStringArray(assetElement, "take_profit_logic", candidateWarnings);
                var invalidationLogic = TryGetStringArray(assetElement, "invalidation_logic", candidateWarnings);
                var marketRegimeTags = TryGetStringArray(assetElement, "market_regime_tags", candidateWarnings);
                var sessionTags = TryGetStringArray(assetElement, "session_tags", candidateWarnings);
                var riskNotes = TryGetStringArray(assetElement, "risk_notes", candidateWarnings);

                if (string.IsNullOrWhiteSpace(asset)) candidateWarnings.Add("signal_asset_missing");
                if (string.IsNullOrWhiteSpace(timeframe)) candidateWarnings.Add("signal_timeframe_missing");
                if (string.IsNullOrWhiteSpace(direction)) candidateWarnings.Add("signal_direction_missing");
                if (string.IsNullOrWhiteSpace(setupId)) candidateWarnings.Add("signal_setup_id_missing");
                if (string.IsNullOrWhiteSpace(setupName)) candidateWarnings.Add("signal_setup_name_missing");
                if (string.IsNullOrWhiteSpace(primaryCandidate)) candidateWarnings.Add("signal_primary_candidate_missing");
                if (string.IsNullOrWhiteSpace(readiness)) candidateWarnings.Add("signal_readiness_missing");
                if (!paperEntryEnabled) candidateWarnings.Add("paper_entry_disabled");

                candidates.Add(new SignalCandidate
                {
                    SignalId = BuildSignalId(package, asset, setupId),
                    Asset = asset,
                    Timeframe = timeframe,
                    Direction = direction,
                    SetupId = setupId,
                    SetupName = setupName,
                    PrimaryCandidate = primaryCandidate,
                    Readiness = readiness,
                    PaperEntryEnabled = paperEntryEnabled,
                    ConfidenceBaseline = confidenceBaseline,
                    MaxSpread = maxSpread,
                    StopLossR = stopLossR,
                    TakeProfitR = takeProfitR,
                    ExpiresAtUtc = expiresAtUtc,
                    EntryLogic = entryLogic,
                    ExitLogic = exitLogic,
                    StopLossLogic = stopLossLogic,
                    TakeProfitLogic = takeProfitLogic,
                    InvalidationLogic = invalidationLogic,
                    MarketRegimeTags = marketRegimeTags,
                    SessionTags = sessionTags,
                    RiskNotes = riskNotes,
                    ValidationWarnings = candidateWarnings.ToArray(),
                });

                if (candidateWarnings.Count > 0)
                {
                    collectedWarnings.AddRange(candidateWarnings);
                }
            }
        }
        catch (Exception ex)
        {
            collectedWarnings.Add($"embedded_strategy_parse_failed:{ex.GetType().Name}");
            warnings = collectedWarnings.ToArray();
            return [];
        }

        warnings = collectedWarnings.ToArray();
        return candidates.ToArray();
    }

    /// <summary>
    /// Evaluates a virtual paper-trade step from parsed candidates and the current paper portfolio.
    /// </summary>
    public PaperTr\u0061deResult EvaluatePaperTrade(
        IReadOnlyList<SignalCandidate> signalCandidates,
        PaperPortfolioState paperPortfolioState,
        RuntimeMarketContext context,
        BotConfiguration config,
        out PaperPortfolioState nextPortfolioState,
        out string[] warnings)
    {
        var collectedWarnings = new List<string>();
        nextPortfolioState = paperPortfolioState ?? new PaperPortfolioState();

        if (config is null || context is null)
        {
            warnings = ["paper_trade_inputs_missing"];
            return BlockedTrade("paper_trade_inputs_missing", "would_block_by_safety");
        }

        if (config.MaxActivePaperTrades < 1 ||
            config.MaxNewPaperTradesPerDay < 1 ||
            config.MaxNewPaperTradesPerHour < 1 ||
            config.MaxConsecutivePaperLosses < 0 ||
            config.MaxDailyPaperRLoss < 0m)
        {
            warnings = ["paper_trade_limits_invalid"];
            return BlockedTrade("paper_trade_limits_invalid", "would_block_by_safety");
        }

        var now = DateTimeOffset.UtcNow;
        var activeTrades = nextPortfolioState.ActiveTrades ?? [];
        var activeTrade = activeTrades.Length > 0 ? activeTrades[0] : null;

        if (activeTrade is not null)
        {
            return EvaluateActiveTrade(activeTrade, nextPortfolioState, context, out nextPortfolioState, out warnings);
        }

        if (activeTrades.Length >= config.MaxActivePaperTrades)
        {
            warnings = ["max_active_paper_trades_reached"];
            return BlockedTrade("max_active_paper_trades_reached", "would_block_by_safety");
        }

        if (nextPortfolioState.OpenTradeCountToday >= config.MaxNewPaperTradesPerDay)
        {
            warnings = ["max_new_paper_trades_per_day_reached"];
            return BlockedTrade("max_new_paper_trades_per_day_reached", "would_block_by_safety");
        }

        if (nextPortfolioState.OpenTradeCountThisHour >= config.MaxNewPaperTradesPerHour)
        {
            warnings = ["max_new_paper_trades_per_hour_reached"];
            return BlockedTrade("max_new_paper_trades_per_hour_reached", "would_block_by_safety");
        }

        if (nextPortfolioState.ConsecutiveLosses >= config.MaxConsecutivePaperLosses)
        {
            warnings = ["max_consecutive_paper_losses_reached"];
            return BlockedTrade("max_consecutive_paper_losses_reached", "would_block_by_safety");
        }

        if (nextPortfolioState.DailyPaperLossR >= config.MaxDailyPaperRLoss)
        {
            warnings = ["max_daily_paper_r_loss_reached"];
            return BlockedTrade("max_daily_paper_r_loss_reached", "would_block_by_safety");
        }

        if (signalCandidates is null || signalCandidates.Count == 0)
        {
            warnings = collectedWarnings.ToArray();
            return new PaperTr\u0061deResult
            {
                Decision = "would_wait",
                BrokerAction = "none",
                Lifecycle = PaperTradeLifecycle.Active,
                Reason = "no_signal_candidates",
            };
        }

        foreach (var candidate in signalCandidates)
        {
            if (!candidate.PaperEntryEnabled)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(candidate.ExpiresAtUtc?.ToString()) && candidate.ExpiresAtUtc.HasValue && candidate.ExpiresAtUtc.Value <= now)
            {
                warnings = ["signal_expired_before_entry"];
                return new PaperTr\u0061deResult
                {
                    SignalId = candidate.SignalId,
                    Asset = candidate.Asset,
                    Timeframe = candidate.Timeframe,
                    Direction = candidate.Direction,
                    Decision = "would_expire",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Expired,
                    Reason = "signal_expired_before_entry",
                };
            }

            if (!IsContextCompatible(candidate, context))
            {
                warnings = ["signal_not_compatible_with_context"];
                continue;
            }

            var spreadFilter = new SpreadFilter().Evaluate(context, candidate.MaxSpread);
            if (!spreadFilter.Allowed && string.Equals(spreadFilter.Status, "blocked_by_spread", StringComparison.OrdinalIgnoreCase))
            {
                warnings = ["spread_too_high"];
                return BlockedTrade("spread_too_high", "would_block_by_spread");
            }

            if (string.Equals(spreadFilter.Status, "spread_pips_missing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(spreadFilter.Status, "spread_context_missing", StringComparison.OrdinalIgnoreCase))
            {
                warnings = [spreadFilter.Reason];
            }

            if (string.Equals(candidate.Direction, "long", StringComparison.OrdinalIgnoreCase))
            {
                var entryPrice = context.Ask > 0m ? context.Ask : 1m;
                var position = CreatePosition(candidate, entryPrice, true);
                nextPortfolioState = AddOpenTrade(nextPortfolioState, position, now);
                warnings = candidate.ValidationWarnings;
                return new PaperTr\u0061deResult
                {
                    SignalId = candidate.SignalId,
                    Asset = candidate.Asset,
                    Timeframe = candidate.Timeframe,
                    Direction = candidate.Direction,
                    Decision = "would_enter_long",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Open,
                    Reason = "paper_long_entry_selected",
                    EntryPrice = entryPrice,
                };
            }

            if (string.Equals(candidate.Direction, "short", StringComparison.OrdinalIgnoreCase))
            {
                var entryPrice = context.Bid > 0m ? context.Bid : 1m;
                var position = CreatePosition(candidate, entryPrice, false);
                nextPortfolioState = AddOpenTrade(nextPortfolioState, position, now);
                warnings = candidate.ValidationWarnings;
                return new PaperTr\u0061deResult
                {
                    SignalId = candidate.SignalId,
                    Asset = candidate.Asset,
                    Timeframe = candidate.Timeframe,
                    Direction = candidate.Direction,
                    Decision = "would_enter_short",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Open,
                    Reason = "paper_short_entry_selected",
                    EntryPrice = entryPrice,
                };
            }

            warnings = ["signal_direction_not_actionable"];
            return new PaperTr\u0061deResult
            {
                SignalId = candidate.SignalId,
                Asset = candidate.Asset,
                Timeframe = candidate.Timeframe,
                Direction = candidate.Direction,
                Decision = "would_skip",
                BrokerAction = "none",
                Lifecycle = PaperTradeLifecycle.Closed,
                Reason = "direction_not_actionable",
            };
        }

        warnings = collectedWarnings.ToArray();
        return new PaperTr\u0061deResult
        {
            Decision = "would_wait",
            BrokerAction = "none",
            Lifecycle = PaperTradeLifecycle.Active,
            Reason = "no_actionable_signal",
        };
    }

    /// <summary>
    /// Evaluates a cloud embedded signal decision into a virtual paper position lifecycle.
    /// </summary>
    public PaperTr\u0061deResult EvaluateCloudSignalPosition(
        SignalDecision? signal,
        PaperPosition? activePosition,
        RuntimeMarketContext context,
        BotConfiguration config,
        out PaperPosition? nextActivePosition,
        out string[] warnings)
    {
        var collectedWarnings = new List<string>();
        nextActivePosition = activePosition;

        if (config is null || context is null)
        {
            warnings = ["paper_signal_inputs_missing"];
            return BlockedTrade("paper_signal_inputs_missing", "would_block_by_safety");
        }

        if (signal is null)
        {
            warnings = ["signal_missing"];
            return new PaperTr\u0061deResult
            {
                Decision = "would_wait",
                BrokerAction = "none",
                Lifecycle = activePosition is not null ? PaperTradeLifecycle.Active : PaperTradeLifecycle.Closed,
                Reason = "signal_missing",
                PaperPositionOpen = activePosition is not null,
                PaperPositionStatus = activePosition is not null ? activePosition.Status.ToString().ToLowerInvariant() : PaperPositionStatus.Closed.ToString().ToLowerInvariant(),
                PaperExitReason = PaperExitReason.SignalMissing.ToString().ToLowerInvariant(),
            };
        }

        var now = DateTimeOffset.UtcNow;
        var entryPrice = ResolveCurrentPrice(signal.Direction.ToString(), context);

        if (activePosition is null)
        {
            if (signal.Confidence < MinimumEmbeddedSignalConfidence)
            {
                warnings = ["signal_low_confidence"];
                return new PaperTr\u0061deResult
                {
                    Decision = "would_wait_low_confidence",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Closed,
                    Reason = "signal_low_confidence",
                    PaperPositionOpen = false,
                    PaperPositionStatus = PaperPositionStatus.Closed.ToString().ToLowerInvariant(),
                    PaperExitReason = PaperExitReason.SignalMissing.ToString().ToLowerInvariant(),
                };
            }

            if (!signal.StopLossPrice.HasValue || !signal.TakeProfitPrice.HasValue)
            {
                warnings = ["missing_risk_bounds"];
                return new PaperTr\u0061deResult
                {
                    Decision = "would_wait_missing_risk_bounds",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Closed,
                    Reason = "missing_risk_bounds",
                    PaperPositionOpen = false,
                    PaperPositionStatus = PaperPositionStatus.Closed.ToString().ToLowerInvariant(),
                    PaperExitReason = PaperExitReason.MissingRiskBounds.ToString().ToLowerInvariant(),
                };
            }

            if (signal.ExpiryUtc <= now)
            {
                warnings = ["signal_expired_before_entry"];
                return new PaperTr\u0061deResult
                {
                    Decision = "would_wait_expired_signal",
                    BrokerAction = "none",
                    Lifecycle = PaperTradeLifecycle.Expired,
                    Reason = "signal_expired_before_entry",
                    PaperPositionOpen = false,
                    PaperPositionStatus = PaperPositionStatus.Closed.ToString().ToLowerInvariant(),
                    PaperExitReason = PaperExitReason.Expired.ToString().ToLowerInvariant(),
                };
            }

            var openedPosition = CreateCloudPosition(signal, context, entryPrice);
            nextActivePosition = openedPosition;
            warnings = [];
            return new PaperTr\u0061deResult
            {
                PositionId = openedPosition.PositionId,
                StrategyId = openedPosition.StrategyId,
                SignalId = openedPosition.SignalId,
                Asset = openedPosition.Asset,
                Timeframe = openedPosition.Timeframe,
                Direction = openedPosition.Direction,
                Decision = string.Equals(openedPosition.Direction, "short", StringComparison.OrdinalIgnoreCase) ? "would_enter_short_paper" : "would_enter_long_paper",
                BrokerAction = "none",
                Lifecycle = PaperTradeLifecycle.Active,
                Reason = "paper_position_opened",
                EntryPrice = openedPosition.EntryPrice,
                PaperPositionOpen = true,
                PaperPositionStatus = openedPosition.Status.ToString().ToLowerInvariant(),
                PaperExitReason = PaperExitReason.None.ToString().ToLowerInvariant(),
                RMultiple = 0m,
            };
        }

        if (activePosition.Status == PaperPositionStatus.Open)
        {
            activePosition = new PaperPosition
            {
                PositionId = activePosition.PositionId,
                StrategyId = activePosition.StrategyId,
                SignalId = activePosition.SignalId,
                Asset = activePosition.Asset,
                Timeframe = activePosition.Timeframe,
                Direction = activePosition.Direction,
                EntryPrice = activePosition.EntryPrice,
                StopLossPrice = activePosition.StopLossPrice,
                TakeProfitPrice = activePosition.TakeProfitPrice,
                ProfitR = activePosition.ProfitR,
                Lifecycle = activePosition.Lifecycle,
                Status = PaperPositionStatus.Active,
                ExitReason = activePosition.ExitReason,
                LastPrice = entryPrice,
                RMultiple = activePosition.RMultiple,
                BrokerAction = "none",
                ExpiresAtUtc = activePosition.ExpiresAtUtc,
                OpenedAtUtc = activePosition.OpenedAtUtc,
                UpdatedAtUtc = now,
                ClosedAtUtc = activePosition.ClosedAtUtc,
                CloseReason = activePosition.CloseReason,
            };
        }

        var isLong = string.Equals(activePosition.Direction, "long", StringComparison.OrdinalIgnoreCase);
        var closePrice = ResolveExitPrice(activePosition.Direction, context);
        var isExpiredActive = activePosition.ExpiresAtUtc.HasValue && activePosition.ExpiresAtUtc.Value <= now;
        var hitTakeProfit = isLong
            ? closePrice >= activePosition.TakeProfitPrice
            : closePrice <= activePosition.TakeProfitPrice;
        var hitStopLoss = isLong
            ? closePrice <= activePosition.StopLossPrice
            : closePrice >= activePosition.StopLossPrice;

        if (isExpiredActive)
        {
            var closed = CloseCloudPosition(activePosition, closePrice, PaperPositionStatus.Expired, PaperExitReason.Expired, "paper_position_expired");
            nextActivePosition = null;
            warnings = ["paper_position_expired"];
            return closed;
        }

        if (hitTakeProfit)
        {
            var closed = CloseCloudPosition(activePosition, closePrice, PaperPositionStatus.TakeProfitHit, PaperExitReason.TakeProfitHit, "paper_take_profit_hit");
            nextActivePosition = null;
            warnings = ["take_profit_hit"];
            return closed;
        }

        if (hitStopLoss)
        {
            var closed = CloseCloudPosition(activePosition, closePrice, PaperPositionStatus.StopLossHit, PaperExitReason.StopLossHit, "paper_stop_loss_hit");
            nextActivePosition = null;
            warnings = ["stop_loss_hit"];
            return closed;
        }

        nextActivePosition = new PaperPosition
        {
            PositionId = activePosition.PositionId,
            StrategyId = activePosition.StrategyId,
            SignalId = activePosition.SignalId,
            Asset = activePosition.Asset,
            Timeframe = activePosition.Timeframe,
            Direction = activePosition.Direction,
            EntryPrice = activePosition.EntryPrice,
            StopLossPrice = activePosition.StopLossPrice,
            TakeProfitPrice = activePosition.TakeProfitPrice,
            ProfitR = activePosition.ProfitR,
            Lifecycle = PaperTradeLifecycle.Active,
            Status = PaperPositionStatus.Active,
            ExitReason = PaperExitReason.None,
            LastPrice = closePrice,
            RMultiple = activePosition.RMultiple,
            BrokerAction = "none",
            ExpiresAtUtc = activePosition.ExpiresAtUtc,
            OpenedAtUtc = activePosition.OpenedAtUtc,
            UpdatedAtUtc = now,
            ClosedAtUtc = activePosition.ClosedAtUtc,
            CloseReason = activePosition.CloseReason,
        };

        warnings = [];
        return new PaperTr\u0061deResult
        {
            PositionId = nextActivePosition.PositionId,
            StrategyId = nextActivePosition.StrategyId,
            SignalId = nextActivePosition.SignalId,
            Asset = nextActivePosition.Asset,
            Timeframe = nextActivePosition.Timeframe,
            Direction = nextActivePosition.Direction,
            Decision = "would_hold_paper_position",
            BrokerAction = "none",
            Lifecycle = PaperTradeLifecycle.Active,
            Reason = "paper_position_active",
            EntryPrice = nextActivePosition.EntryPrice,
            PaperPositionOpen = true,
            PaperPositionStatus = nextActivePosition.Status.ToString().ToLowerInvariant(),
            PaperExitReason = PaperExitReason.None.ToString().ToLowerInvariant(),
            RMultiple = nextActivePosition.RMultiple,
        };
    }

    private static PaperTr\u0061deResult EvaluateActiveTrade(
        PaperPosition activeTrade,
        PaperPortfolioState currentPortfolio,
        RuntimeMarketContext context,
        out PaperPortfolioState nextPortfolio,
        out string[] warnings)
    {
        var activeTrades = currentPortfolio.ActiveTrades ?? [];
        var mutableTrades = new List<PaperPosition>(activeTrades);
        var now = DateTimeOffset.UtcNow;
        var tradeIndex = mutableTrades.FindIndex(trade => string.Equals(trade.SignalId, activeTrade.SignalId, StringComparison.Ordinal));
        if (tradeIndex < 0)
        {
            warnings = ["active_trade_missing_from_portfolio"];
            nextPortfolio = currentPortfolio;
            return BlockedTrade("active_trade_missing_from_portfolio", "would_block_by_safety");
        }

        if (activeTrade.Lifecycle == PaperTradeLifecycle.Open)
        {
                mutableTrades[tradeIndex] = new PaperPosition
                {
                    SignalId = activeTrade.SignalId,
                    Asset = activeTrade.Asset,
                    Timeframe = activeTrade.Timeframe,
                    Direction = activeTrade.Direction,
                    EntryPrice = activeTrade.EntryPrice,
                    StopLossPrice = activeTrade.StopLossPrice,
                    TakeProfitPrice = activeTrade.TakeProfitPrice,
                    ProfitR = activeTrade.ProfitR,
                    Lifecycle = PaperTradeLifecycle.Active,
                    ExpiresAtUtc = activeTrade.ExpiresAtUtc,
                    OpenedAtUtc = activeTrade.OpenedAtUtc,
                    UpdatedAtUtc = now,
                    ClosedAtUtc = activeTrade.ClosedAtUtc,
                    CloseReason = activeTrade.CloseReason,
                };

            nextPortfolio = new PaperPortfolioState
            {
                ActiveTrades = mutableTrades.ToArray(),
                OpenTradeCountToday = currentPortfolio.OpenTradeCountToday,
                OpenTradeCountThisHour = currentPortfolio.OpenTradeCountThisHour,
                ConsecutiveLosses = currentPortfolio.ConsecutiveLosses,
                DailyPaperLossR = currentPortfolio.DailyPaperLossR,
                LastUpdatedAtUtc = now,
            };

            warnings = [];
            return new PaperTr\u0061deResult
            {
                SignalId = activeTrade.SignalId,
                Asset = activeTrade.Asset,
                Timeframe = activeTrade.Timeframe,
                Direction = activeTrade.Direction,
                Decision = "would_wait",
                BrokerAction = "none",
                Lifecycle = PaperTradeLifecycle.Active,
                Reason = "trade_promoted_to_active",
                EntryPrice = activeTrade.EntryPrice,
            };
        }

        var isLong = string.Equals(activeTrade.Direction, "long", StringComparison.OrdinalIgnoreCase);
        var isExpired = activeTrade.ExpiresAtUtc.HasValue && activeTrade.ExpiresAtUtc.Value <= now;
        var hitTakeProfit = isLong
            ? context.Bid >= activeTrade.TakeProfitPrice
            : context.Ask <= activeTrade.TakeProfitPrice;
        var hitStopLoss = isLong
            ? context.Bid <= activeTrade.StopLossPrice
            : context.Ask >= activeTrade.StopLossPrice;

        if (isExpired)
        {
            return CloseActiveTrade(activeTrade, currentPortfolio, mutableTrades, tradeIndex, PaperTradeLifecycle.Expired, "trade_expired", 0m, out nextPortfolio, out warnings);
        }

        if (hitTakeProfit)
        {
            var profitR = activeTrade.TakeProfitPrice > activeTrade.EntryPrice
                ? Math.Abs((activeTrade.TakeProfitPrice - activeTrade.EntryPrice) / Math.Max(Math.Abs(activeTrade.TakeProfitPrice - activeTrade.StopLossPrice), 0.0001m))
                : 0m;
            return CloseActiveTrade(activeTrade, currentPortfolio, mutableTrades, tradeIndex, PaperTradeLifecycle.TakeProfitHit, "take_profit_hit", profitR, out nextPortfolio, out warnings);
        }

        if (hitStopLoss)
        {
            var lossR = activeTrade.EntryPrice > activeTrade.StopLossPrice
                ? Math.Abs((activeTrade.EntryPrice - activeTrade.StopLossPrice) / Math.Max(Math.Abs(activeTrade.TakeProfitPrice - activeTrade.StopLossPrice), 0.0001m))
                : 0m;
            return CloseActiveTrade(activeTrade, currentPortfolio, mutableTrades, tradeIndex, PaperTradeLifecycle.StopLossHit, "stop_loss_hit", -lossR, out nextPortfolio, out warnings);
        }

        mutableTrades[tradeIndex] = new PaperPosition
        {
            SignalId = activeTrade.SignalId,
            Asset = activeTrade.Asset,
            Timeframe = activeTrade.Timeframe,
            Direction = activeTrade.Direction,
            EntryPrice = activeTrade.EntryPrice,
            StopLossPrice = activeTrade.StopLossPrice,
            TakeProfitPrice = activeTrade.TakeProfitPrice,
            ProfitR = activeTrade.ProfitR,
            Lifecycle = PaperTradeLifecycle.Active,
            ExpiresAtUtc = activeTrade.ExpiresAtUtc,
            OpenedAtUtc = activeTrade.OpenedAtUtc,
            UpdatedAtUtc = now,
            ClosedAtUtc = activeTrade.ClosedAtUtc,
            CloseReason = activeTrade.CloseReason,
        };

        nextPortfolio = new PaperPortfolioState
        {
            ActiveTrades = mutableTrades.ToArray(),
            OpenTradeCountToday = currentPortfolio.OpenTradeCountToday,
            OpenTradeCountThisHour = currentPortfolio.OpenTradeCountThisHour,
            ConsecutiveLosses = currentPortfolio.ConsecutiveLosses,
            DailyPaperLossR = currentPortfolio.DailyPaperLossR,
            LastUpdatedAtUtc = now,
        };
        warnings = [];
        return new PaperTr\u0061deResult
        {
            SignalId = activeTrade.SignalId,
            Asset = activeTrade.Asset,
            Timeframe = activeTrade.Timeframe,
            Direction = activeTrade.Direction,
            Decision = "would_wait",
            BrokerAction = "none",
            Lifecycle = PaperTradeLifecycle.Active,
            Reason = "trade_still_active",
            EntryPrice = activeTrade.EntryPrice,
        };
    }

    private static PaperTr\u0061deResult CloseActiveTrade(
        PaperPosition activeTrade,
        PaperPortfolioState currentPortfolio,
        List<PaperPosition> mutableTrades,
        int tradeIndex,
        PaperTradeLifecycle lifecycle,
        string reason,
        decimal profitR,
        out PaperPortfolioState nextPortfolio,
        out string[] warnings)
    {
        var now = DateTimeOffset.UtcNow;
        mutableTrades.RemoveAt(tradeIndex);

        var consecutiveLosses = currentPortfolio.ConsecutiveLosses;
        var dailyLossR = currentPortfolio.DailyPaperLossR;

        if (lifecycle == PaperTradeLifecycle.StopLossHit || profitR < 0m)
        {
            consecutiveLosses += 1;
            dailyLossR += Math.Abs(profitR);
        }
        else if (lifecycle == PaperTradeLifecycle.TakeProfitHit)
        {
            consecutiveLosses = 0;
        }

        nextPortfolio = new PaperPortfolioState
        {
            ActiveTrades = mutableTrades.ToArray(),
            OpenTradeCountToday = currentPortfolio.OpenTradeCountToday,
            OpenTradeCountThisHour = currentPortfolio.OpenTradeCountThisHour,
            ConsecutiveLosses = consecutiveLosses,
            DailyPaperLossR = dailyLossR,
            LastUpdatedAtUtc = now,
        };
        warnings = [];
        return new PaperTr\u0061deResult
        {
            SignalId = activeTrade.SignalId,
            Asset = activeTrade.Asset,
            Timeframe = activeTrade.Timeframe,
            Direction = activeTrade.Direction,
            Decision = lifecycle == PaperTradeLifecycle.TakeProfitHit ? "would_wait" : lifecycle == PaperTradeLifecycle.StopLossHit ? "would_invalidate" : "would_expire",
            BrokerAction = "none",
            Lifecycle = lifecycle,
            Reason = reason,
            EntryPrice = activeTrade.EntryPrice,
            ExitPrice = lifecycle == PaperTradeLifecycle.TakeProfitHit ? activeTrade.TakeProfitPrice : activeTrade.StopLossPrice,
            ProfitR = profitR,
        };
    }

    private static PaperPosition CreatePosition(SignalCandidate candidate, decimal entryPrice, bool isLong)
    {
        var stopLossPrice = isLong
            ? entryPrice - candidate.StopLossR
            : entryPrice + candidate.StopLossR;
        var takeProfitPrice = isLong
            ? entryPrice + candidate.TakeProfitR
            : entryPrice - candidate.TakeProfitR;

        return new PaperPosition
        {
            SignalId = candidate.SignalId,
            Asset = candidate.Asset,
            Timeframe = candidate.Timeframe,
            Direction = candidate.Direction,
            EntryPrice = entryPrice,
            StopLossPrice = stopLossPrice,
            TakeProfitPrice = takeProfitPrice,
            ProfitR = 0m,
            Lifecycle = PaperTradeLifecycle.Open,
            ExpiresAtUtc = candidate.ExpiresAtUtc,
            OpenedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static PaperPortfolioState AddOpenTrade(PaperPortfolioState currentPortfolio, PaperPosition position, DateTimeOffset now)
    {
        var currentTrades = currentPortfolio.ActiveTrades ?? [];
        var updatedTrades = new PaperPosition[currentTrades.Length + 1];
        Array.Copy(currentTrades, updatedTrades, currentTrades.Length);
        updatedTrades[updatedTrades.Length - 1] = position;

        return new PaperPortfolioState
        {
            ActiveTrades = updatedTrades,
            OpenTradeCountToday = currentPortfolio.OpenTradeCountToday + 1,
            OpenTradeCountThisHour = currentPortfolio.OpenTradeCountThisHour + 1,
            ConsecutiveLosses = currentPortfolio.ConsecutiveLosses,
            DailyPaperLossR = currentPortfolio.DailyPaperLossR,
            LastUpdatedAtUtc = now,
        };
    }

    private static bool IsContextCompatible(SignalCandidate candidate, RuntimeMarketContext context)
    {
        if (context is null)
        {
            return false;
        }

        var contextSymbol = !string.IsNullOrWhiteSpace(context.Symbol) ? context.Symbol : context.CurrentSymbol;
        if (!string.IsNullOrWhiteSpace(contextSymbol) && !string.Equals(contextSymbol, candidate.Asset, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var contextTimeframe = !string.IsNullOrWhiteSpace(context.Timeframe) ? context.Timeframe : context.CurrentTimeframe;
        if (!string.IsNullOrWhiteSpace(contextTimeframe) && !string.Equals(contextTimeframe, candidate.Timeframe, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static PaperTr\u0061deResult BlockedTrade(string reason, string decision)
        => new()
        {
            Decision = decision,
            BrokerAction = "none",
            Lifecycle = PaperTradeLifecycle.Closed,
            Reason = reason,
        };

    private static decimal ResolveCurrentPrice(string direction, RuntimeMarketContext context)
    {
        if (string.Equals(direction, "short", StringComparison.OrdinalIgnoreCase))
        {
            return context.Bid > 0m ? context.Bid : context.Ask;
        }

        return context.Ask > 0m ? context.Ask : context.Bid;
    }

    private static decimal ResolveExitPrice(string direction, RuntimeMarketContext context)
    {
        if (string.Equals(direction, "short", StringComparison.OrdinalIgnoreCase))
        {
            return context.Ask > 0m ? context.Ask : context.Bid;
        }

        return context.Bid > 0m ? context.Bid : context.Ask;
    }

    private static PaperPosition CreateCloudPosition(SignalDecision signal, RuntimeMarketContext context, decimal entryPrice)
    {
        var isLong = string.Equals(signal.Direction.ToString(), "long", StringComparison.OrdinalIgnoreCase);
        var stopLoss = signal.StopLossPrice ?? entryPrice;
        var takeProfit = signal.TakeProfitPrice ?? entryPrice;
        var expiry = signal.MaxHoldingSeconds.HasValue && signal.MaxHoldingSeconds.Value > 0
            ? DateTimeOffset.UtcNow.AddSeconds(signal.MaxHoldingSeconds.Value)
            : signal.ExpiryUtc;

        if (!signal.StopLossPrice.HasValue || !signal.TakeProfitPrice.HasValue)
        {
            stopLoss = isLong ? entryPrice - 1m : entryPrice + 1m;
            takeProfit = isLong ? entryPrice + 1m : entryPrice - 1m;
        }

        return new PaperPosition
        {
            PositionId = string.Join(':', [signal.StrategyId, context.Symbol, context.Timeframe, Guid.NewGuid().ToString("N")]),
            StrategyId = signal.StrategyId,
            SignalId = signal.StrategyId,
            Asset = !string.IsNullOrWhiteSpace(context.Symbol) ? context.Symbol : context.CurrentSymbol,
            Timeframe = !string.IsNullOrWhiteSpace(context.Timeframe) ? context.Timeframe : context.CurrentTimeframe,
            Direction = signal.Direction.ToString().ToLowerInvariant(),
            EntryPrice = entryPrice,
            StopLossPrice = stopLoss,
            TakeProfitPrice = takeProfit,
            ProfitR = 0m,
            Lifecycle = PaperTradeLifecycle.Active,
            Status = PaperPositionStatus.Active,
            ExitReason = PaperExitReason.None,
            LastPrice = entryPrice,
            RMultiple = signal.RiskR ?? 0m,
            BrokerAction = "none",
            ExpiresAtUtc = expiry,
            OpenedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static PaperTr\u0061deResult CloseCloudPosition(
        PaperPosition activePosition,
        decimal exitPrice,
        PaperPositionStatus status,
        PaperExitReason exitReason,
        string reason)
    {
        var risk = Math.Max(Math.Abs(activePosition.EntryPrice - activePosition.StopLossPrice), 0.0001m);
        var rMultiple = string.Equals(activePosition.Direction, "short", StringComparison.OrdinalIgnoreCase)
            ? (activePosition.EntryPrice - exitPrice) / risk
            : (exitPrice - activePosition.EntryPrice) / risk;

        return new PaperTr\u0061deResult
        {
            PositionId = activePosition.PositionId,
            StrategyId = activePosition.StrategyId,
            SignalId = activePosition.SignalId,
            Asset = activePosition.Asset,
            Timeframe = activePosition.Timeframe,
            Direction = activePosition.Direction,
            Decision = status switch
            {
                PaperPositionStatus.TakeProfitHit => "would_close_paper_tp",
                PaperPositionStatus.StopLossHit => "would_close_paper_sl",
                PaperPositionStatus.Expired => "would_close_paper_expired",
                _ => "would_hold_paper_position",
            },
            BrokerAction = "none",
            Lifecycle = status switch
            {
                PaperPositionStatus.TakeProfitHit => PaperTradeLifecycle.TakeProfitHit,
                PaperPositionStatus.StopLossHit => PaperTradeLifecycle.StopLossHit,
                PaperPositionStatus.Expired => PaperTradeLifecycle.Expired,
                _ => PaperTradeLifecycle.Active,
            },
            Reason = reason,
            EntryPrice = activePosition.EntryPrice,
            ExitPrice = exitPrice,
            ProfitR = rMultiple,
            PaperPositionOpen = false,
            PaperPositionStatus = status.ToString().ToLowerInvariant(),
            PaperExitReason = exitReason.ToString().ToLowerInvariant(),
            RMultiple = rMultiple,
        };
    }

    private static string BuildSignalId(CloudEmbeddedReleasePackage package, string asset, string setupId)
        => string.Join(':', [package.BotReleaseId, asset, string.IsNullOrWhiteSpace(setupId) ? "signal" : setupId]);

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0m;
        return false;
    }

    private static bool TryGetDateTime(JsonElement element, string propertyName, out DateTimeOffset? value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string[] TryGetStringArray(JsonElement element, string propertyName, List<string> warnings)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            var values = new List<string>();
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        values.Add(text);
                    }
                }
            }

            return values.ToArray();
        }

        warnings.Add($"signal_array_missing:{propertyName}");
        return [];
    }
}
