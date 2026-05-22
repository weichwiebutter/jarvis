namespace Hermes.Runtime;

public sealed record SignalGenerationStubResult(
    string OutputPath,
    int SignalCount,
    IReadOnlyList<string> SymbolsProcessed,
    IReadOnlyList<string> Warnings);
