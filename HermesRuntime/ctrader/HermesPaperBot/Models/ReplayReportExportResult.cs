namespace HermesPaperBot.Models;

/// <summary>
/// Result of exporting a replay report.
/// </summary>
public sealed class ReplayReportExportResult
{
    public bool Success { get; init; }
    public string ReportDirectory { get; init; } = string.Empty;
    public string JsonPath { get; init; } = string.Empty;
    public string MarkdownPath { get; init; } = string.Empty;
    public string BrokerAction { get; init; } = "none";
    public string[] Warnings { get; init; } = [];
}
