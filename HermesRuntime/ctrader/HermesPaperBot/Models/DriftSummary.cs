namespace HermesPaperBot.Models;

/// <summary>
/// Minimal drift summary placeholder for blocking checks.
/// </summary>
public sealed class DriftSummary
{
    public bool BlockingDriftFound { get; init; } = false;
    public DriftSeverity OverallDriftSeverity { get; init; } = DriftSeverity.None;
}
