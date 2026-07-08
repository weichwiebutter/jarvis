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
    public string? LoggingStatus { get; init; } = null;
    public bool TimerLogWritten { get; init; } = false;
    public string TimerLogPath { get; init; } = string.Empty;
    public string TimerLogFallback { get; init; } = string.Empty;
    public int TimerTickCount { get; init; } = 0;
    public DateTimeOffset? SessionStartedAt { get; init; }
    public DateTimeOffset? LastTimerAt { get; init; }
    public bool SignalSeen { get; init; } = false;
    public string SignalDirection { get; init; } = "flat";
    public decimal? SignalConfidence { get; init; }
    public bool SignalExpired { get; init; } = false;
    public SignalCandidate[] SignalCandidates { get; init; } = [];
    public PaperPortfolioState? PaperPortfolioState { get; init; }
    public PaperTr\u0061deResult? PaperTr\u0061deResult { get; init; }
    public string[] PaperWarnings { get; init; } = [];
    public RuntimeMarketContext? MarketContext { get; init; }
    public bool MarketContextSeen { get; init; } = false;
    public string CloudStepStage { get; init; } = "none";
    public string CloudStepExceptionType { get; init; } = "none";
    public string CloudStepExceptionMessage { get; init; } = "none";
    public bool PackageLoaded { get; init; } = false;
    public bool SignalPackageLoaded { get; init; } = false;
    public int SignalCount { get; init; } = 0;
    public string SignalPackageJsonLength { get; init; } = "0";
    public string SignalPackageParseStatus { get; init; } = "unknown";
    public string FirstSignalId { get; init; } = string.Empty;
    public bool ChartAnnotationLoaded { get; init; } = false;
    public string RestoreState { get; init; } = "unknown";
    public string RestoreReason { get; init; } = string.Empty;
    public bool RestoreSnapshotValid { get; init; } = false;
    public bool RestoreFreshStateUsed { get; init; } = false;
    public int RestoreActiveTradeCount { get; init; } = 0;
    public string RestoreFirstActiveSignalId { get; init; } = string.Empty;
    public decimal? RestoreFirstActiveEntry { get; init; }
    public decimal? RestoreFirstActiveSl { get; init; }
    public decimal? RestoreFirstActiveTp { get; init; }
    public bool PaperPositionOpen { get; init; } = false;
    public string PaperPositionStatus { get; init; } = "none";
    public string PaperExitReason { get; init; } = "none";
    public decimal? RMultiple { get; init; }
    public string PositionId { get; init; } = string.Empty;
}
