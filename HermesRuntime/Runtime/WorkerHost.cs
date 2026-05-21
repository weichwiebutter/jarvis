namespace Hermes.Runtime;

public sealed class WorkerHost
{
    private const string WorkerSource = "hermes_worker_host";
    private const string WorkerName = "hermes_worker_host_once";

    private readonly StoragePaths _storagePaths;
    private readonly QueueManager _queueManager;
    private readonly EventBus _eventBus;
    private readonly WorkerRegistry _workerRegistry = new();
    private readonly string _runtimeVersion;
    private readonly string _workerId = $"worker_{Guid.NewGuid():N}";

    public WorkerHost(
        StoragePaths storagePaths,
        QueueManager queueManager,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _queueManager = queueManager;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;

        _workerRegistry.Register(new FeatureExportWorker(_storagePaths));
        _workerRegistry.Register(new BacktestWorker(_storagePaths));
    }

    public void RunOnce(string? jobType = null)
    {
        PublishWorkerStarted();
        PublishHeartbeat("idle", currentJobId: null);

        if (!TryDequeueRegisteredJob(jobType, out var job, out var lease)
            || job is null
            || lease is null)
        {
            PublishWorkerStopped(jobType is null ? "no_registered_job" : $"no_{jobType}_job");
            return;
        }

        PublishJobStarted(job, lease);
        PublishHeartbeat("running", job.JobId);

        try
        {
            if (!_workerRegistry.TryGetWorker(job.JobType, out var worker) || worker is null)
            {
                throw new InvalidOperationException($"No worker registered for job type: {job.JobType}");
            }

            PublishWorkerJobStarted(job);
            var workerResult = worker.Execute(job);
            PublishWorkerJobCompleted(job, workerResult);

            var completed = _queueManager.MarkCompleted(
                job.JobId,
                workerResult.OutputPath,
                workerResult.Metrics,
                lease.LeasedAtUtc);

            PublishJobCompleted(completed);
            PublishHeartbeat("completed", job.JobId);
        }
        catch (Exception ex)
        {
            var failed = _queueManager.MarkFailed(
                job.JobId,
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["exception_type"] = ex.GetType().Name
                },
                lease.LeasedAtUtc);

            PublishJobFailed(failed);
            PublishHeartbeat("failed", job.JobId);
        }
        finally
        {
            PublishWorkerStopped("run_once_completed");
        }
    }

    private bool TryDequeueRegisteredJob(
        string? jobType,
        out JobManifest? job,
        out JobLease? lease)
    {
        if (!string.IsNullOrWhiteSpace(jobType))
        {
            if (!_workerRegistry.CanHandle(jobType))
            {
                job = null;
                lease = null;
                return false;
            }

            return _queueManager.TryDequeue(jobType, out job, out lease);
        }

        foreach (var worker in _workerRegistry.Workers)
        {
            if (_queueManager.TryDequeue(worker.JobType, out job, out lease))
            {
                return true;
            }
        }

        job = null;
        lease = null;
        return false;
    }

    private void PublishWorkerStarted()
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.WorkerStarted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "WorkerHost started.",
                WorkerId = _workerId,
                WorkerName,
                registeredWorkers = _workerRegistry.Workers.Select(worker => new
                {
                    worker.WorkerName,
                    worker.JobType
                }).ToList(),
                queueStatus = _queueManager.Status
            }));
    }

    private void PublishHeartbeat(string status, string? currentJobId)
    {
        var heartbeat = new WorkerHeartbeat(
            WorkerId: _workerId,
            WorkerName: WorkerName,
            TimestampUtc: DateTimeOffset.UtcNow,
            Status: status,
            CurrentJobId: currentJobId,
            QueueStatus: _queueManager.Status);

        _eventBus.Publish(EventEnvelope.Create(
            EventType.WorkerHeartbeat,
            WorkerSource,
            EventSeverity.Debug,
            _runtimeVersion,
            heartbeat));
    }

    private void PublishJobStarted(JobManifest job, JobLease lease)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.JobStarted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Job started.",
                job.JobId,
                job.JobType,
                job.Priority,
                job.Status,
                lease.LeaseId,
                lease.LeasedAtUtc,
                lease.ExpiresAtUtc,
                queueStatus = _queueManager.Status
            }));
    }

    private void PublishFeatureExportStarted(JobManifest job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.FeatureExportStarted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Feature export started.",
                job.JobId,
                job.JobType,
                exportDirectory = Path.Combine(_storagePaths.Root, "exports", "features")
            }));
    }

    private void PublishFeatureExportCompleted(JobManifest job, WorkerExecutionResult result)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.FeatureExportCompleted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Feature export completed.",
                job.JobId,
                result.OutputPath,
                result.Metrics
            }));
    }

    private void PublishBacktestStarted(JobManifest job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.BacktestStarted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Demo backtest started. No market replay or trading execution is possible.",
                job.JobId,
                job.JobType,
                job.Parameters,
                reportDirectory = Path.Combine(_storagePaths.Root, "reports", "backtests"),
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishBacktestCompleted(JobManifest job, WorkerExecutionResult result)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.BacktestCompleted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Demo backtest completed. Report is local and read-only for UI/CLI.",
                job.JobId,
                result.OutputPath,
                result.Metrics,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishWorkerJobStarted(JobManifest job)
    {
        if (job.JobType == FeatureExportWorker.FeatureExportJobType)
        {
            PublishFeatureExportStarted(job);
            return;
        }

        if (job.JobType == BacktestWorker.BacktestJobType)
        {
            PublishBacktestStarted(job);
        }
    }

    private void PublishWorkerJobCompleted(JobManifest job, WorkerExecutionResult result)
    {
        if (job.JobType == FeatureExportWorker.FeatureExportJobType)
        {
            PublishFeatureExportCompleted(job, result);
            PublishSignalResultExported(job, result);
            return;
        }

        if (job.JobType == BacktestWorker.BacktestJobType)
        {
            PublishBacktestCompleted(job, result);
        }
    }

    private void PublishSignalResultExported(JobManifest job, WorkerExecutionResult result)
    {
        if (!result.Metrics.TryGetValue("signal_output_path", out var signalOutputPath)
            || signalOutputPath is null)
        {
            return;
        }

        _eventBus.Publish(EventEnvelope.Create(
            EventType.SignalResultExported,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Demo signal results exported. No orders were created.",
                job.JobId,
                signalOutputPath,
                signalRowsWritten = result.Metrics.TryGetValue("signal_rows_written", out var count)
                    ? count
                    : null,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishJobCompleted(JobResult result)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.JobCompleted,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Job completed.",
                result.JobId,
                result.Status,
                result.StartedAtUtc,
                result.CompletedAtUtc,
                result.OutputPath,
                result.Metrics,
                queueStatus = _queueManager.Status
            }));
    }

    private void PublishJobFailed(JobResult result)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.JobFailed,
            WorkerSource,
            EventSeverity.Error,
            _runtimeVersion,
            new
            {
                message = "Job failed.",
                result.JobId,
                result.Status,
                result.StartedAtUtc,
                result.CompletedAtUtc,
                result.ErrorMessage,
                result.Metrics,
                queueStatus = _queueManager.Status
            }));
    }

    private void PublishWorkerStopped(string reason)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.WorkerStopped,
            WorkerSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "WorkerHost stopped.",
                WorkerId = _workerId,
                WorkerName,
                reason,
                queueStatus = _queueManager.Status
            }));
    }
}
