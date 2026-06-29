namespace HermesPaperBot.Models;

/// <summary>
/// Files required for a paper-only bundle import.
/// </summary>
public sealed class BundleFileSet
{
    public string BundleRootPath { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public string ProvenancePath { get; init; } = string.Empty;
    public string ChecksumsPath { get; init; } = string.Empty;
    public string SignalPackagePath { get; init; } = string.Empty;
    public string SignalSchemaPath { get; init; } = string.Empty;
}
