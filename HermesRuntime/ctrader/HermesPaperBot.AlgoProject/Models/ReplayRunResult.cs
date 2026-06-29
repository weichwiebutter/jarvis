namespace HermesPaperBot.Models;

/// <summary>
/// Result of a market replay run.
/// </summary>
public sealed class ReplayRunResult
{
    public ReplayStatistics Statistics { get; init; } = new();
    public PaperTr\u0061deResult[] PaperTr\u0061deResults { get; init; } = [];
    public RuntimeStepResult[] RuntimeSummaries { get; init; } = [];
    public string BrokerAction { get; init; } = "none";
}
