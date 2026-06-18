namespace HermesPaperBot.Models;

/// <summary>
/// Paper-only bot configuration.
/// </summary>
public sealed class BotConfiguration
{
    /// <summary>
    /// Release bundle inbox path.
    /// </summary>
    public string ReleaseBundleInboxPath { get; init; } = string.Empty;

    /// <summary>
    /// Active release bundle path.
    /// </summary>
    public string ActiveReleaseBundlePath { get; init; } = string.Empty;

    /// <summary>
    /// Last valid release bundle path.
    /// </summary>
    public string LastValidReleaseBundlePath { get; init; } = string.Empty;

    /// <summary>
    /// Local runtime logs path.
    /// </summary>
    public string LocalRuntimeLogsPath { get; init; } = string.Empty;

    /// <summary>
    /// Reload interval in seconds.
    /// </summary>
    public int ReloadIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Whether bundle import is enabled.
    /// </summary>
    public bool ImportEnabled { get; init; } = true;

    /// <summary>
    /// Manual kill switch.
    /// </summary>
    public bool ManualKillSwitch { get; init; } = false;

    /// <summary>
    /// Logging verbosity.
    /// </summary>
    public string LogVerbosity { get; init; } = "normal";

    /// <summary>
    /// Paper-only safety defaults.
    /// </summary>
    public bool NoAutoTrading { get; init; } = true;

    /// <summary>
    /// Human review is required.
    /// </summary>
    public bool HumanReviewRequired { get; init; } = true;

    /// <summary>
    /// Broker trading is disabled.
    /// </summary>
    public bool BrokerTradingEnabled { get; init; } = false;

    /// <summary>
    /// Live trading is disabled.
    /// </summary>
    public bool LiveTradingEnabled { get; init; } = false;

    /// <summary>
    /// Order API is disabled.
    /// </summary>
    public bool OrderApiEnabled { get; init; } = false;

    /// <summary>
    /// Paper mode is enabled.
    /// </summary>
    public bool PaperMode { get; init; } = true;
}
