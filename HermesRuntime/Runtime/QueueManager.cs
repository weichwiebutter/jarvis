using System.Text.Json;

namespace Hermes.Runtime;

public sealed class QueueManager
{
    private readonly string _jobsRoot;
    private readonly string _pending;
    private readonly string _running;
    private readonly string _completed;
    private readonly string _failed;
    private readonly string _quarantined;

    public QueueManager(StoragePaths storagePaths)
    {
        _jobsRoot = storagePaths.Jobs;
        _pending = Path.Combine(_jobsRoot, "pending");
        _running = Path.Combine(_jobsRoot, "running");
        _completed = Path.Combine(_jobsRoot, "completed");
        _failed = Path.Combine(_jobsRoot, "failed");
        _quarantined = Path.Combine(_jobsRoot, "quarantined");

        InitializeDirectories();
    }

    public QueueStatus Status => new(
        Pending: CountJobs(_pending),
        Running: CountJobs(_running),
        Completed: CountJobs(_completed),
        Failed: CountJobs(_failed),
        Quarantined: CountJobs(_quarantined));

    public JobManifest Enqueue(JobManifest manifest)
    {
        var pendingManifest = manifest with
        {
            Status = JobStatus.Pending,
            CreatedAtUtc = manifest.CreatedAtUtc == default ? DateTimeOffset.UtcNow : manifest.CreatedAtUtc
        };

        var path = GetJobPath(_pending, pendingManifest.JobId);
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"Job already exists: {pendingManifest.JobId}");
        }

        WriteJob(path, pendingManifest);
        return pendingManifest;
    }

    public bool TryDequeue(out JobManifest? manifest, out JobLease? lease)
    {
        return TryDequeue(jobType: null, out manifest, out lease);
    }

    public bool TryDequeue(string? jobType, out JobManifest? manifest, out JobLease? lease)
    {
        manifest = null;
        lease = null;

        var candidate = GetJobs(JobStatus.Pending)
            .Where(job => jobType is null || string.Equals(job.JobType, jobType, StringComparison.Ordinal))
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.CreatedAtUtc)
            .FirstOrDefault();

        if (candidate is null)
        {
            return false;
        }

        manifest = MarkRunning(candidate.JobId);
        lease = new JobLease(
            LeaseId: $"lease_{Guid.NewGuid():N}",
            JobId: manifest.JobId,
            LeasedAtUtc: DateTimeOffset.UtcNow,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(manifest.MaxRuntimeMinutes));

        return true;
    }

    public JobManifest MarkRunning(string jobId)
    {
        var manifest = ReadJob(GetJobPath(_pending, jobId)) with
        {
            Status = JobStatus.Running
        };

        MoveJob(jobId, _pending, _running, manifest);
        return manifest;
    }

    public JobResult MarkCompleted(
        string jobId,
        string? outputPath = null,
        IReadOnlyDictionary<string, object?>? metrics = null,
        DateTimeOffset? startedAtUtc = null)
    {
        var manifest = ReadJob(GetJobPath(_running, jobId)) with
        {
            Status = JobStatus.Completed
        };

        MoveJob(jobId, _running, _completed, manifest);

        var result = new JobResult(
            JobId: jobId,
            Status: JobStatus.Completed,
            StartedAtUtc: startedAtUtc ?? DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            OutputPath: outputPath,
            ErrorMessage: null,
            Metrics: metrics ?? new Dictionary<string, object?>());

        WriteResult(_completed, result);
        return result;
    }

    public JobResult MarkFailed(
        string jobId,
        string errorMessage,
        IReadOnlyDictionary<string, object?>? metrics = null,
        DateTimeOffset? startedAtUtc = null)
    {
        var manifest = ReadJob(GetJobPath(_running, jobId)) with
        {
            Status = JobStatus.Failed
        };

        MoveJob(jobId, _running, _failed, manifest);

        var result = new JobResult(
            JobId: jobId,
            Status: JobStatus.Failed,
            StartedAtUtc: startedAtUtc ?? DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            OutputPath: null,
            ErrorMessage: errorMessage,
            Metrics: metrics ?? new Dictionary<string, object?>());

        WriteResult(_failed, result);
        return result;
    }

    public JobManifest Quarantine(string jobId, string reason)
    {
        var sourceDirectory = FindJobDirectory(jobId)
            ?? throw new FileNotFoundException($"Job not found: {jobId}");

        var currentManifest = ReadJob(GetJobPath(sourceDirectory, jobId));
        var manifest = currentManifest with
        {
            Status = JobStatus.Quarantined,
            Parameters = new Dictionary<string, object?>(currentManifest.Parameters)
            {
                ["quarantine_reason"] = reason
            }
        };

        MoveJob(jobId, sourceDirectory, _quarantined, manifest);
        return manifest;
    }

    public IReadOnlyList<JobManifest> GetJobs(JobStatus? status = null)
    {
        var directories = status is null
            ? new[] { _pending, _running, _completed, _failed, _quarantined }
            : new[] { GetDirectory(status.Value) };

        return directories
            .SelectMany(ReadJobs)
            .OrderBy(job => job.CreatedAtUtc)
            .ToList();
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_jobsRoot);
        Directory.CreateDirectory(_pending);
        Directory.CreateDirectory(_running);
        Directory.CreateDirectory(_completed);
        Directory.CreateDirectory(_failed);
        Directory.CreateDirectory(_quarantined);
    }

    private string GetDirectory(JobStatus status) => status switch
    {
        JobStatus.Pending => _pending,
        JobStatus.Running => _running,
        JobStatus.Completed => _completed,
        JobStatus.Failed => _failed,
        JobStatus.Quarantined => _quarantined,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private string? FindJobDirectory(string jobId)
    {
        return new[] { _pending, _running, _completed, _failed, _quarantined }
            .FirstOrDefault(directory => File.Exists(GetJobPath(directory, jobId)));
    }

    private static int CountJobs(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.job.json", SearchOption.TopDirectoryOnly).Count()
            : 0;

    private static IEnumerable<JobManifest> ReadJobs(string directory)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.job.json", SearchOption.TopDirectoryOnly))
        {
            yield return ReadJob(path);
        }
    }

    private static string GetJobPath(string directory, string jobId) =>
        Path.Combine(directory, $"{jobId}.job.json");

    private static string GetResultPath(string directory, string jobId) =>
        Path.Combine(directory, $"{jobId}.result.json");

    private static JobManifest ReadJob(string path)
    {
        var manifest = JsonSerializer.Deserialize<JobManifest>(
            File.ReadAllText(path),
            JsonDefaults.SnapshotReadOptions);

        if (manifest is null)
        {
            throw new InvalidOperationException($"Job manifest is empty or invalid: {path}");
        }

        return manifest;
    }

    private static void WriteJob(string path, JobManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));
    }

    private static void WriteResult(string directory, JobResult result)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(GetResultPath(directory, result.JobId), JsonSerializer.Serialize(result, JsonDefaults.WriteOptions));
    }

    private static void MoveJob(string jobId, string sourceDirectory, string targetDirectory, JobManifest manifest)
    {
        var sourcePath = GetJobPath(sourceDirectory, jobId);
        var targetPath = GetJobPath(targetDirectory, jobId);

        Directory.CreateDirectory(targetDirectory);
        WriteJob(targetPath, manifest);

        if (File.Exists(sourcePath))
        {
            File.Delete(sourcePath);
        }
    }
}
