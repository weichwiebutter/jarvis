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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private static readonly object InMemoryLock = new();
    private static readonly Dictionary<string, List<string>> InMemoryTimerEntries = new(StringComparer.OrdinalIgnoreCase);

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
    public bool WriteTimer(string logsPath, RuntimeStepResult result)
    {
        try
        {
            var entry = new
            {
                entry_type = "timer",
                timestamp_utc = DateTime.UtcNow.ToString("O"),
                symbol = result.MarketContext?.Symbol ?? string.Empty,
                timeframe = result.MarketContext?.Timeframe ?? string.Empty,
                decision = result.PaperDecision,
                state = result.State,
                signal_count = result.SignalCount,
                open_positions = result.PaperPortfolioState?.ActiveTrades.Length ?? 0,
                closed_trades = result.PaperPortfolioState?.ClosedTrades.Length ?? 0,
                net_r = ComputeNetR(result),
                safety_status = BuildSafetyStatus(result),
                broker_action = result.BrokerAction,
            };

            var line = JsonSerializer.Serialize(entry, JsonOptions);
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                AppendInMemory("paper_runtime_step_log.jsonl", line);
                return true;
            }

            try
            {
                Directory.CreateDirectory(logsPath);
                var timerLogPath = Path.Combine(logsPath, "paper_runtime_step_log.jsonl");
                File.AppendAllText(timerLogPath, line + Environment.NewLine);
                return true;
            }
            catch
            {
                AppendInMemory(logsPath, line);
                return true;
            }
        }
        catch
        {
            return false;
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
