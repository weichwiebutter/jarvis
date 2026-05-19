namespace Hermes.Runtime;

public sealed record ReplayManifest(
    string ReplayId,
    string ReplayType,
    string Symbol,
    string Timeframe,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string DataHash,
    string RuntimeVersion,
    string FeatureSchemaVersion,
    string ModelVersion,
    string ClusterVersion,
    string ParametersHash,
    IReadOnlyList<string> InputFiles);
