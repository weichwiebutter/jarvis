namespace HermesPaperBot.Services;

using System;
using System.IO;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Writes the runtime summary for the paper-only bot.
/// </summary>
public sealed class RuntimeSummaryWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Writes a runtime summary entry.
    /// </summary>
    public bool Write(string logsPath, RuntimeStepResult result, BotConfiguration config)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                return false;
            }

            Directory.CreateDirectory(logsPath);
            var summaryPath = Path.Combine(logsPath, "bot_runtime_summary.json");
            var summary = new
            {
                updated_at_utc = DateTime.UtcNow.ToString("O"),
                result.State,
                result.Success,
                result.ConfigValid,
                result.ImportValid,
                result.BundleValid,
                result.ChecksumValid,
                result.SafetyAllowed,
                result.DriftAllowed,
                result.KillSwitchActive,
                result.FallbackPossible,
                result.DisabledUntilValidBundle,
                result.PaperDecision,
                result.BrokerAction,
                signal_seen = result.SignalSeen,
                signal_direction = result.SignalDirection,
                signal_confidence = result.SignalConfidence,
                signal_expired = result.SignalExpired,
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
                paper_warnings = result.PaperWarnings,
                paper_trade_result = result.PaperTr\u0061deResult,
                paper_portfolio_state = result.PaperPortfolioState,
                market_context = result.MarketContext,
                safety_flags = new
                {
                    config.NoAutoTrading,
                    config.HumanReviewRequired,
                    broker_trading_enabled = config.BrokerTradingEnabled,
                    config.LiveTradingEnabled,
                    config.OrderApiEnabled,
                    config.PaperMode,
                },
                paper_trade_limits = new
                {
                    config.MaxActivePaperTrades,
                    config.MaxNewPaperTradesPerDay,
                    config.MaxNewPaperTradesPerHour,
                    config.MaxConsecutivePaperLosses,
                    config.MaxDailyPaperRLoss,
                },
            };

            File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
