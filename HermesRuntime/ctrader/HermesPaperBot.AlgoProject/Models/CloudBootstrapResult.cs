namespace HermesPaperBot.Models;

/// <summary>
/// Result of cloud embedded bootstrap creation.
/// </summary>
public sealed class CloudBootstrapResult
{
    /// <summary>
    /// Indicates whether the cloud configuration could be created.
    /// </summary>
    public bool Success { get; init; } = false;

    /// <summary>
    /// Placeholder status.
    /// </summary>
    public string Status { get; init; } = "blocked_by_skeleton";

    /// <summary>
    /// Failure or fallback reason.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";

    /// <summary>
    /// Created bot configuration, if available.
    /// </summary>
    public BotConfiguration? Configuration { get; init; }
}
