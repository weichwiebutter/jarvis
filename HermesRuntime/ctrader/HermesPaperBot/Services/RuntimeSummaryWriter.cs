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
            safety_flags = new
            {
                config.NoAutoTrading,
                config.HumanReviewRequired,
                broker_trading_enabled = config.BrokerTradingEnabled,
                config.LiveTradingEnabled,
                config.OrderApiEnabled,
                config.PaperMode,
            },
        };

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
        return true;
    }
}
