namespace Hermes.Runtime;

public sealed record TrustedStrategySource(
    string SourceId,
    string SourceUrl,
    string SourceName,
    string SourceType,
    bool Whitelisted,
    bool CodeExecutionAllowed,
    string LocalSnapshotPath);
