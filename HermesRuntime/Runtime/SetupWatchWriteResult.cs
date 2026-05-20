namespace Hermes.Runtime;

public sealed record SetupWatchWriteResult(
    IReadOnlyList<SetupWatchCandidate> Candidates,
    string OutputPath)
{
    public int ActiveSetupWatches => Candidates.Count(candidate =>
        candidate.Status is SetupWatchStatus.watching
            or SetupWatchStatus.armed
            or SetupWatchStatus.triggered);
}
