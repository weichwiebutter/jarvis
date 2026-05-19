namespace Hermes.Runtime;

public sealed record RuntimeHealthWriteResult(
    RuntimeHealth Health,
    string ReportPath);
