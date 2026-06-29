namespace HermesPaperBot.Services;

using System;
using System.IO;
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

    /// <summary>
    /// Writes a paper runtime step entry and decision log.
    /// </summary>
    public bool Write(string logsPath, RuntimeStepResult result)
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
            };

            File.AppendAllText(positionLogPath, JsonSerializer.Serialize(portfolioEntry, JsonOptions) + Environment.NewLine);
        }

        return true;
    }
}
