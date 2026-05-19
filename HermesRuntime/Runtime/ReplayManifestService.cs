using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ReplayManifestService
{
    private const string ReplaySource = "hermes_replay_manifest_service";

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;
    private readonly string _manifestDirectory;

    public ReplayManifestService(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
        _manifestDirectory = Path.Combine(_storagePaths.Root, "replays", "manifests");
        Directory.CreateDirectory(_manifestDirectory);
    }

    public ReplayManifestWriteResult CreateDemoReplayManifest()
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var replayId = $"replay_demo_{createdAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var inputFiles = GetDemoInputFiles();
        var parameters = "replay_type=feature_export_demo;symbol=DEMO_FEATURE_EXPORT;timeframe=M1;mode=manifest_only";

        var manifest = new ReplayManifest(
            ReplayId: replayId,
            ReplayType: "feature_export_demo",
            Symbol: "DEMO_FEATURE_EXPORT",
            Timeframe: "M1",
            FromUtc: createdAtUtc.AddMinutes(-3),
            ToUtc: createdAtUtc,
            DataHash: ComputeHash(string.Join('\n', inputFiles)),
            RuntimeVersion: _runtimeVersion,
            FeatureSchemaVersion: "demo_feature_schema_v1",
            ModelVersion: "none",
            ClusterVersion: "none",
            ParametersHash: ComputeHash(parameters),
            InputFiles: inputFiles);

        var manifestPath = Path.Combine(_manifestDirectory, $"{replayId}.manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));

        _eventBus.Publish(EventEnvelope.Create(
            EventType.ReplayManifestCreated,
            ReplaySource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Demo replay manifest created. No replay was executed.",
                manifest.ReplayId,
                manifest.ReplayType,
                manifest.Symbol,
                manifest.Timeframe,
                manifest.FromUtc,
                manifest.ToUtc,
                manifest.DataHash,
                manifest.ParametersHash,
                manifest.InputFiles,
                manifestPath
            }));

        return new ReplayManifestWriteResult(manifest, manifestPath);
    }

    private IReadOnlyList<string> GetDemoInputFiles()
    {
        var exportDirectory = Path.Combine(_storagePaths.Root, "exports", "features");
        if (!Directory.Exists(exportDirectory))
        {
            return ["no_feature_export_files_available"];
        }

        var latestExport = Directory
            .EnumerateFiles(exportDirectory, "*.features.jsonl", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return latestExport is null
            ? ["no_feature_export_files_available"]
            : [latestExport];
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
