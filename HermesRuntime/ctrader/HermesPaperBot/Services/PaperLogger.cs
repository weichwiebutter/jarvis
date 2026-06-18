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
    /// Writes a paper decision log entry.
    /// </summary>
    public bool Write(string logsPath, RuntimeStepResult result)
    {
        if (string.IsNullOrWhiteSpace(logsPath))
        {
            return false;
        }

        Directory.CreateDirectory(logsPath);
        var stepLogPath = Path.Combine(logsPath, "paper_runtime_step_log.jsonl");
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
            reasons = result.Reasons,
        };

        File.AppendAllText(stepLogPath, JsonSerializer.Serialize(stepEntry, JsonOptions) + Environment.NewLine);

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

        return true;
    }
}
