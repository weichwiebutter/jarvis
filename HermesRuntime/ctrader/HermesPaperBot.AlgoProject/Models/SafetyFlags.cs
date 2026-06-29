namespace HermesPaperBot.Models;

/// <summary>
/// Mandatory safety flags for the paper-only runtime.
/// </summary>
public sealed class SafetyFlags
{
    public bool NoAutoTrading { get; init; } = true;
    public bool HumanReviewRequired { get; init; } = true;
    public bool BrokerTradingEnabled { get; init; } = false;
    public bool LiveTradingEnabled { get; init; } = false;
    public bool OrderApiEnabled { get; init; } = false;
    public bool PaperMode { get; init; } = true;
    public string BrokerAction { get; init; } = "none";
}
