namespace Hermes.Runtime;

public sealed record StoragePaths(
    string Root,
    string Events,
    string Snapshots,
    string Logs,
    string Cache,
    string Archive)
{
    public IReadOnlyList<string> AllDirectories =>
    [
        Root,
        Events,
        Snapshots,
        Logs,
        Cache,
        Archive
    ];
}
