using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MasterStatusWriter
{
    private readonly MasterStatusService _service;

    public MasterStatusWriter(MasterStatusService service)
    {
        _service = service;
    }

    public string SnapshotPath => _service.SnapshotPath;

    public MasterStatusSnapshot WriteSnapshot()
    {
        var snapshot = _service.BuildSnapshot();
        Directory.CreateDirectory(_service.SnapshotDirectory);
        File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        return snapshot;
    }
}
