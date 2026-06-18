namespace HermesPaperBot.Models;

/// <summary>
/// Result of a defensive paper runtime step.
/// </summary>
public sealed class RuntimeStepResult
{
    public bool Success { get; init; } = false;
    public string State { get; init; } = "not_implemented";
    public bool ConfigValid { get; init; } = false;
    public bool ImportAttempted { get; init; } = false;
    public bool ImportValid { get; init; } = false;
    public bool BundleValid { get; init; } = false;
    public bool ChecksumValid { get; init; } = false;
    public bool SafetyAllowed { get; init; } = false;
    public bool DriftAllowed { get; init; } = false;
    public bool KillSwitchActive { get; init; } = false;
    public bool FallbackPossible { get; init; } = false;
    public bool DisabledUntilValidBundle { get; init; } = false;
    public string PaperDecision { get; init; } = "would_wait";
    public string BrokerAction { get; init; } = "none";
    public string[] Reasons { get; init; } = [];
}
