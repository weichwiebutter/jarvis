namespace HermesPaperBot.Services;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Writes paper runtime logs.
/// </summary>
public sealed class PaperLogger
{
    private sealed record TimerTimelineState(int Count, DateTimeOffset FirstAtUtc, DateTimeOffset LastAtUtc);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private static readonly object InMemoryLock = new();
    private static readonly Dictionary<string, List<string>> InMemoryTimerEntries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, TimerTimelineState> TimerTimelineStates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes a paper runtime step entry and decision log.
    /// </summary>
    public bool Write(string logsPath, RuntimeStepResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                return false;
            }

            Directory.CreateDirectory(logsPath);
            var stepLogPath = Path.Combine(logsPath, "paper_runtime_step_log.jsonl");
            var decisionLogPath = Path.Combine(logsPath, "paper_decision_log.jsonl");
            var killSwitchLogPath = Path.Combine(logsPath, "kill_switch_events.jsonl");

            var stepEntry = new
            {
                timestamp_utc = DateTime.UtcNow.ToString("O"),
                result.State,
                result.Success,
                config_valid = result.ConfigValid,
                import_valid = result.ImportValid,
                bundle_valid = result.BundleValid,
                checksum_valid = result.ChecksumValid,
                safety_allowed = result.SafetyAllowed,
                drift_allowed = result.DriftAllowed,
                kill_switch_active = result.KillSwitchActive,
                fallback_possible = result.FallbackPossible,
                disabled_until_valid_bundle = result.DisabledUntilValidBundle,
                paper_decision = result.PaperDecision,
                broker_action = result.BrokerAction,
                signal_seen = result.SignalSeen,
                signal_direction = result.SignalDirection,
                signal_confidence = result.SignalConfidence,
                signal_expired = result.SignalExpired,
                cloud_step_stage = result.CloudStepStage,
                cloud_step_exception_type = result.CloudStepExceptionType,
                cloud_step_exception_message = result.CloudStepExceptionMessage,
                package_loaded = result.PackageLoaded,
                signal_package_loaded = result.SignalPackageLoaded,
                chart_annotation_loaded = result.ChartAnnotationLoaded,
                restore_state = result.RestoreState,
                restore_reason = result.RestoreReason,
                snapshot_valid = result.RestoreSnapshotValid,
                fresh_state_used = result.RestoreFreshStateUsed,
                active_trade_count = result.RestoreActiveTradeCount,
                first_active_signal_id = result.RestoreFirstActiveSignalId,
                first_active_entry = result.RestoreFirstActiveEntry,
                first_active_sl = result.RestoreFirstActiveSl,
                first_active_tp = result.RestoreFirstActiveTp,
                paper_position_open = result.PaperPositionOpen,
                paper_position_status = result.PaperPositionStatus,
                paper_exit_reason = result.PaperExitReason,
                r_multiple = result.RMultiple,
                position_id = result.PositionId,
                reasons = result.Reasons,
                warnings = result.PaperWarnings,
                market_context = result.MarketContext,
            };

            var decisionEntry = new
            {
                timestamp_utc = DateTime.UtcNow.ToString("O"),
                result.State,
                paper_decision = result.PaperDecision,
                broker_action = result.BrokerAction,
                signal_seen = result.SignalSeen,
                signal_direction = result.SignalDirection,
                signal_confidence = result.SignalConfidence,
                signal_expired = result.SignalExpired,
                cloud_step_stage = result.CloudStepStage,
                cloud_step_exception_type = result.CloudStepExceptionType,
                cloud_step_exception_message = result.CloudStepExceptionMessage,
                package_loaded = result.PackageLoaded,
                signal_package_loaded = result.SignalPackageLoaded,
                chart_annotation_loaded = result.ChartAnnotationLoaded,
                restore_state = result.RestoreState,
                restore_reason = result.RestoreReason,
                snapshot_valid = result.RestoreSnapshotValid,
                fresh_state_used = result.RestoreFreshStateUsed,
                active_trade_count = result.RestoreActiveTradeCount,
                first_active_signal_id = result.RestoreFirstActiveSignalId,
                first_active_entry = result.RestoreFirstActiveEntry,
                first_active_sl = result.RestoreFirstActiveSl,
                first_active_tp = result.RestoreFirstActiveTp,
                paper_position_open = result.PaperPositionOpen,
                paper_position_status = result.PaperPositionStatus,
                paper_exit_reason = result.PaperExitReason,
                r_multiple = result.RMultiple,
                position_id = result.PositionId,
                reasons = result.Reasons,
                warnings = result.PaperWarnings,
                market_context = result.MarketContext,
            };

            File.AppendAllText(stepLogPath, JsonSerializer.Serialize(stepEntry, JsonOptions) + Environment.NewLine);
            File.AppendAllText(decisionLogPath, JsonSerializer.Serialize(decisionEntry, JsonOptions) + Environment.NewLine);

            if (result.KillSwitchActive)
            {
                var killEntry = new
                {
                    timestamp_utc = DateTime.UtcNow.ToString("O"),
                    result.State,
                    result.Reasons,
                    broker_action = "none",
                };

                File.AppendAllText(killSwitchLogPath, JsonSerializer.Serialize(killEntry, JsonOptions) + Environment.NewLine);
            }

            if (result.PaperTr\u0061deResult is not null)
            {
                var tradeResultLogPath = Path.Combine(logsPath, "paper_trade_result_log.jsonl");
                var tradeEntry = new
                {
                    timestamp_utc = DateTime.UtcNow.ToString("O"),
                    result.PaperTr\u0061deResult.SignalId,
                    result.PaperTr\u0061deResult.Asset,
                    result.PaperTr\u0061deResult.Timeframe,
                    result.PaperTr\u0061deResult.Direction,
                    result.PaperTr\u0061deResult.Decision,
                    result.PaperTr\u0061deResult.BrokerAction,
                    result.PaperTr\u0061deResult.Lifecycle,
                    result.PaperTr\u0061deResult.Reason,
                    result.PaperTr\u0061deResult.EntryPrice,
                    result.PaperTr\u0061deResult.ExitPrice,
                    result.PaperTr\u0061deResult.ProfitR,
                    result.PaperPositionOpen,
                    result.PaperPositionStatus,
                    result.PaperExitReason,
                    result.RMultiple,
                    result.PositionId,
                    result.RestoreState,
                    result.RestoreReason,
                    result.RestoreSnapshotValid,
                    result.RestoreFreshStateUsed,
                    result.RestoreActiveTradeCount,
                    result.RestoreFirstActiveSignalId,
                    result.RestoreFirstActiveEntry,
                    result.RestoreFirstActiveSl,
                    result.RestoreFirstActiveTp,
                };

                File.AppendAllText(tradeResultLogPath, JsonSerializer.Serialize(tradeEntry, JsonOptions) + Environment.NewLine);
            }

            if (result.PaperPortfolioState is not null)
            {
                var positionLogPath = Path.Combine(logsPath, "paper_position_log.jsonl");
                var portfolioEntry = new
                {
                    timestamp_utc = DateTime.UtcNow.ToString("O"),
                    active_trade_count = result.PaperPortfolioState.ActiveTrades.Length,
                    result.PaperPortfolioState.OpenTradeCountToday,
                    result.PaperPortfolioState.OpenTradeCountThisHour,
                    result.PaperPortfolioState.ConsecutiveLosses,
                    result.PaperPortfolioState.DailyPaperLossR,
                    trades = result.PaperPortfolioState.ActiveTrades,
                    result.PaperPositionOpen,
                    result.PaperPositionStatus,
                    result.PaperExitReason,
                    result.RMultiple,
                    result.PositionId,
                    result.RestoreState,
                    result.RestoreReason,
                    result.RestoreSnapshotValid,
                    result.RestoreFreshStateUsed,
                    result.RestoreActiveTradeCount,
                    result.RestoreFirstActiveSignalId,
                    result.RestoreFirstActiveEntry,
                    result.RestoreFirstActiveSl,
                    result.RestoreFirstActiveTp,
                };

                File.AppendAllText(positionLogPath, JsonSerializer.Serialize(portfolioEntry, JsonOptions) + Environment.NewLine);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes a compact per-timer entry and falls back to memory if file IO is not available.
    /// </summary>
    public (bool Written, string Path, string Fallback, int TickCount, DateTimeOffset? SessionStartedAtUtc, DateTimeOffset? LastTimerAtUtc) WriteTimer(string logsPath, RuntimeStepResult result)
    {
        var timerLogPath = string.IsNullOrWhiteSpace(logsPath) ? "paper_runtime_step_log.jsonl" : Path.Combine(logsPath, "paper_runtime_step_log.jsonl");
        var timerTimestamp = DateTimeOffset.UtcNow;
        var timerKey = string.IsNullOrWhiteSpace(logsPath) ? "paper_runtime_step_log.jsonl" : logsPath;
        var timeline = GetOrCreateTimerTimeline(timerKey, timerTimestamp);

        try
        {
            var entry = new Dictionary<string, object?>
            {
                ["entry_type"] = "timer",
                ["timestamp_utc"] = timerTimestamp.ToString("O"),
                ["symbol"] = result.MarketContext?.Symbol ?? string.Empty,
                ["timeframe"] = result.MarketContext?.Timeframe ?? string.Empty,
                ["decision"] = result.PaperDecision,
                ["state"] = result.State,
                ["signal_count"] = result.SignalCount,
                ["open_positions"] = result.PaperPortfolioState?.ActiveTrades.Length ?? 0,
                ["closed_trades"] = result.PaperPortfolioState?.ClosedTrades.Length ?? 0,
                ["net_r"] = ComputeNetR(result),
                ["safety_status"] = BuildSafetyStatus(result),
                ["broker_action"] = result.BrokerAction,
                ["restore_state"] = result.RestoreState,
                ["restore_reason"] = result.RestoreReason,
                ["snapshot_valid"] = result.RestoreSnapshotValid,
                ["fresh_state_used"] = result.RestoreFreshStateUsed,
                ["active_trade_count"] = result.RestoreActiveTradeCount,
                ["first_active_signal_id"] = result.RestoreFirstActiveSignalId,
                ["first_active_entry"] = result.RestoreFirstActiveEntry,
                ["first_active_sl"] = result.RestoreFirstActiveSl,
                ["first_active_tp"] = result.RestoreFirstActiveTp,
            };

            var line = JsonSerializer.Serialize(entry, JsonOptions);
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                AppendInMemory(timerKey, line);
                return (true, timerLogPath, "in_memory", timeline.Count, timeline.FirstAtUtc, timeline.LastAtUtc);
            }

            try
            {
                Directory.CreateDirectory(logsPath);
                File.AppendAllText(timerLogPath, line + Environment.NewLine);
                return (true, timerLogPath, "file", timeline.Count, timeline.FirstAtUtc, timeline.LastAtUtc);
            }
            catch
            {
                AppendInMemory(timerKey, line);
                return (true, timerLogPath, "in_memory", timeline.Count, timeline.FirstAtUtc, timeline.LastAtUtc);
            }
        }
        catch
        {
            return (false, timerLogPath, "error", timeline.Count, timeline.FirstAtUtc, timeline.LastAtUtc);
        }
    }

    private static TimerTimelineState GetOrCreateTimerTimeline(string timerKey, DateTimeOffset timestampUtc)
    {
        lock (InMemoryLock)
        {
            if (TimerTimelineStates.TryGetValue(timerKey, out var existing))
            {
                var updated = new TimerTimelineState(existing.Count + 1, existing.FirstAtUtc, timestampUtc);
                TimerTimelineStates[timerKey] = updated;
                return updated;
            }

            var created = new TimerTimelineState(1, timestampUtc, timestampUtc);
            TimerTimelineStates[timerKey] = created;
            return created;
        }
    }

    private static decimal ComputeNetR(RuntimeStepResult result)
    {
        var closedTrades = result.PaperPortfolioState?.ClosedTrades;
        if (closedTrades is null || closedTrades.Length == 0)
        {
            return 0m;
        }

        return Math.Round(closedTrades.Sum(position => position.RMultiple != 0m
            ? position.RMultiple
            : ComputeFallbackR(position)), 4);
    }

    private static decimal ComputeFallbackR(PaperPosition position)
    {
        var risk = Math.Max(Math.Abs(position.EntryPrice - position.StopLossPrice), 0.0001m);
        return string.Equals(position.Direction, "short", StringComparison.OrdinalIgnoreCase)
            ? (position.EntryPrice - position.ExitPrice) / risk
            : (position.ExitPrice - position.EntryPrice) / risk;
    }

    private static string BuildSafetyStatus(RuntimeStepResult result)
        => result.KillSwitchActive
            ? "blocked"
            : result.SafetyAllowed && string.Equals(result.BrokerAction, "none", StringComparison.OrdinalIgnoreCase)
                ? "safe"
                : "partial";

    private static void AppendInMemory(string logsPath, string line)
    {
        lock (InMemoryLock)
        {
            var key = string.IsNullOrWhiteSpace(logsPath) ? "paper_runtime_step_log.jsonl" : logsPath;
            if (!InMemoryTimerEntries.TryGetValue(key, out var entries))
            {
                entries = [];
                InMemoryTimerEntries[key] = entries;
            }

            entries.Add(line);
        }
    }
}
