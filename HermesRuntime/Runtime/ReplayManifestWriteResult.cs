namespace Hermes.Runtime;

public sealed record ReplayManifestWriteResult(
    ReplayManifest Manifest,
    string ManifestPath);
