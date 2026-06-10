using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MasterStatusWriter
{
    private readonly MasterStatusService _service;
    private string? _resolvedSnapshotPath;

    public MasterStatusWriter(MasterStatusService service)
    {
        _service = service;
    }

    public string SnapshotPath => _resolvedSnapshotPath ?? _service.SnapshotPath;

    public MasterStatusSnapshot? LoadSnapshot()
    {
        foreach (var path in CandidateSnapshotPaths())
        {
            if (!File.Exists(path)) continue;
            var snapshot = JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null)
            {
                _resolvedSnapshotPath = path;
                return snapshot;
            }
        }

        return null;
    }

    public MasterStatusSnapshot WriteSnapshot()
    {
        var snapshot = _service.BuildSnapshot();
        try
        {
            Directory.CreateDirectory(_service.SnapshotDirectory);
            File.WriteAllText(_service.SnapshotPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
            _resolvedSnapshotPath = _service.SnapshotPath;
        }
        catch (IOException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(snapshot);
        }
        catch (UnauthorizedAccessException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(snapshot);
        }

        return snapshot;
    }

    private string WriteFallbackSnapshot(MasterStatusSnapshot snapshot)
    {
        var fallbackDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "master-status");
        Directory.CreateDirectory(fallbackDirectory);
        var fallbackPath = Path.Combine(fallbackDirectory, "master_status.json");
        File.WriteAllText(fallbackPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        return fallbackPath;
    }

    private IEnumerable<string> CandidateSnapshotPaths()
    {
        yield return _service.SnapshotPath;
        yield return Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "master-status", "master_status.json");
    }
}
