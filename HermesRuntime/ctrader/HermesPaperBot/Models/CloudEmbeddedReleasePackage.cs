namespace HermesPaperBot.Models;

/// <summary>
/// Embedded release package for cloud runtime.
/// </summary>
public sealed class CloudEmbeddedReleasePackage
{
    public string BotReleaseId { get; init; } = string.Empty;
    public string BotVersion { get; init; } = string.Empty;
    public string StrategyPackageVersion { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = string.Empty;
    public ReleaseMode ReleaseMode { get; init; } = ReleaseMode.PaperOnly;
    public SafetyFlags SafetyFlags { get; init; } = new SafetyFlags();
    public ForbiddenCapabilities ForbiddenCapabilities { get; init; } = new ForbiddenCapabilities();
    public SignalDecision? SignalDecision { get; init; }
    public string? PackageJson { get; init; }
    public string? SignalPackageJson { get; init; }
    public string? EmbeddedManifestJson { get; init; }
    public string? EmbeddedStrategyJson { get; init; }
    public string? ChartAnnotationSpecJson { get; init; }
    public string? EmbeddedChecksum { get; init; }
}
