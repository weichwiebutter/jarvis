namespace HermesPaperBot.Models;

/// <summary>
/// Aggregate replay statistics for the paper engine.
/// </summary>
public sealed class ReplayStatistics
{
    public int TradesTotal { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public decimal WinRate { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal ExpectancyR { get; init; }
    public decimal AverageR { get; init; }
    public decimal MaxDrawdownR { get; init; }
    public string SampleSizeClass { get; init; } = "none";
    public string QualityClass { get; init; } = "invalid";
    public bool IsStatisticallyMeaningful { get; init; }
    public string[] Warnings { get; init; } = [];
}
