namespace Hermes.Runtime;

public interface IWorker
{
    string WorkerName { get; }

    string JobType { get; }

    WorkerExecutionResult Execute(JobManifest job);
}

public sealed record WorkerExecutionResult(
    string OutputPath,
    IReadOnlyDictionary<string, object?> Metrics);

public sealed class WorkerRegistry
{
    private readonly Dictionary<string, IWorker> _workers = new(StringComparer.Ordinal);

    public IReadOnlyCollection<IWorker> Workers => _workers.Values.ToList();

    public void Register(IWorker worker)
    {
        _workers[worker.JobType] = worker;
    }

    public bool TryGetWorker(string jobType, out IWorker? worker)
    {
        return _workers.TryGetValue(jobType, out worker);
    }

    public bool CanHandle(string jobType)
    {
        return _workers.ContainsKey(jobType);
    }
}
