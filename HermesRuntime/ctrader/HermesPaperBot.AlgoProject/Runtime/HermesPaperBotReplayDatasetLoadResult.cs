namespace Hermes.Runtime;

using System.Collections.Generic;
using HermesPaperBot.Models;

/// <summary>
/// Minimal replay dataset load result needed for the AlgoProject build.
/// </summary>
public sealed record HermesPaperBotReplayDatasetLoadResult(
    bool Success,
    string Status,
    string Reason,
    string DatasetPath,
    int BarsTotal,
    int BarsValid,
    int BarsSkipped,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ReplayBar> Bars);
