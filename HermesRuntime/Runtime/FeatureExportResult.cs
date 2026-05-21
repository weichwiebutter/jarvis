namespace Hermes.Runtime;

public sealed record FeatureExportResult(
    string FeatureOutputPath,
    string SignalOutputPath,
    int FeatureRowsWritten,
    int SignalRowsWritten,
    IReadOnlyList<string> Symbols);
