using System.Text.Json;

namespace Hermes.Runtime;

public sealed record SupervisorRunOptions(
    int MaxRuntimeMinutes,
    int CheckIntervalSeconds,
    int MaxJobsPerLoop);

public sealed record SupervisorJobContext(
    string SupervisorId,
    int Iteration,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    TimeSpan RemainingRuntime);

public sealed record ScheduledJobExecutionResult(
    string Status,
    bool WorkPerformed,
    string Action,
    string? ReportPath,
    IReadOnlyList<string> Warnings);

public sealed record HermesSupervisorRunResult(
    HermesSupervisorState State,
    SchedulerStatus SchedulerStatus);

public sealed class HermesSupervisor
{
    private const string StateVersion = "hermes_supervisor_state_v1";

    private readonly StoragePaths _storagePaths;
    private readonly string _scheduleConfigPath;

    public HermesSupervisor(StoragePaths storagePaths, string scheduleConfigPath)
    {
        _storagePaths = storagePaths;
        _scheduleConfigPath = scheduleConfigPath;
    }

    public string StateDirectory => Path.Combine(_storagePaths.Root, "reports", "supervisor");

    public string StatePath => Path.Combine(StateDirectory, "supervisor_state.json");

    public string HeartbeatPath => Path.Combine(StateDirectory, "supervisor_heartbeat.json");

    public string StopRequestPath => Path.Combine(StateDirectory, "supervisor_stop_requested.flag");

    public string PidPath => new SupervisorProcessManager(_storagePaths).PidPath;

    public string LogPath => new SupervisorProcessManager(_storagePaths).LogPath;

    public HermesSupervisorState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return EmptyState("not_started");
        }

        try
        {
            return JsonSerializer.Deserialize<HermesSupervisorState>(
                File.ReadAllText(StatePath),
                JsonDefaults.SnapshotReadOptions) ?? EmptyState("not_started");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptyState("state_unreadable");
        }
    }

    public SupervisorHeartbeat? LoadHeartbeat()
    {
        if (!File.Exists(HeartbeatPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SupervisorHeartbeat>(
                File.ReadAllText(HeartbeatPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public HermesSupervisorState RequestStop()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StopRequestPath, $"stop_requested_at={DateTimeOffset.UtcNow:O}{Environment.NewLine}");
        var state = LoadState();
        return WriteState(state with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = state.CurrentlyRunning ? "stop_requested" : "stop_requested_no_running_process",
            StopRequestedAtUtc = DateTimeOffset.UtcNow,
            NextAction = state.CurrentlyRunning ? "wait_for_safe_supervisor_stop" : "no_running_supervisor_process"
        });
    }

    public bool IsStopRequested() => File.Exists(StopRequestPath);

    public void ClearStopRequest()
    {
        try
        {
            if (File.Exists(StopRequestPath))
            {
                File.Delete(StopRequestPath);
            }
        }
        catch (IOException)
        {
            // Best effort only; the next loop will observe the file if it remains.
        }
    }

    public HermesSupervisorRunResult Run(
        SupervisorRunOptions options,
        Func<ScheduledJobDefinition, SupervisorJobContext, ScheduledJobExecutionResult> executor)
    {
        Directory.CreateDirectory(StateDirectory);
        var processManager = new SupervisorProcessManager(_storagePaths);
        processManager.WritePid(Environment.ProcessId);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var deadlineUtc = startedAtUtc.AddMinutes(Math.Clamp(options.MaxRuntimeMinutes, 1, 10080));
        var supervisorId = $"hermes_supervisor_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var scheduler = new HermesInternalScheduler(_storagePaths, _scheduleConfigPath);
        var state = WriteState(new HermesSupervisorState(
            StateVersion: StateVersion,
            UpdatedAtUtc: startedAtUtc,
            Status: "running",
            SupervisorId: supervisorId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            StoppedAtUtc: null,
            ProcessId: Environment.ProcessId,
            HeartbeatUtc: startedAtUtc,
            IterationsCompleted: 0,
            JobsStarted: 0,
            JobsCompleted: 0,
            JobsSkipped: 0,
            CurrentJobId: null,
            LastJobId: null,
            LastError: null,
            NextAction: "check_scheduler",
            StopRequestedAtUtc: null,
            CurrentlyRunning: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));
        WriteHeartbeat(supervisorId, state, "starting", "storage_not_checked", "check_scheduler");

        ClearStopRequest();
        var status = "completed_deadline_reached";
        var nextAction = "deadline_reached";
        string? lastError = null;

        while (DateTimeOffset.UtcNow < deadlineUtc)
        {
            var iteration = state.IterationsCompleted + 1;
            if (IsStopRequested())
            {
                status = "stopped_by_stop_request";
                nextAction = "safe_stop_requested";
                break;
            }

            var resource = new ResourceGuard(_storagePaths).Check();
            var cleanupPlan = new StorageHygieneService(_storagePaths).BuildPlan();
            var storageAction = cleanupPlan.Candidates.Count > 0 ? "cleanup_plan_available" : "storage_ok";

            if (resource.ShouldStop)
            {
                status = "stopped_resource_guard";
                nextAction = "review_resource_status";
                state = WriteState(state with
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Status = status,
                    IterationsCompleted = iteration,
                    NextAction = nextAction,
                    LastError = string.Join("; ", resource.Warnings),
                    CurrentlyRunning = true
                });
                WriteHeartbeat(supervisorId, state, resource.Action, storageAction, nextAction);
                break;
            }

            var dueJobs = resource.ShouldPause
                ? []
                : scheduler.FindDueJobs(DateTimeOffset.UtcNow).Take(Math.Clamp(options.MaxJobsPerLoop, 1, 8)).ToList();

            if (resource.ShouldPause)
            {
                nextAction = "paused_by_resource_guard";
                state = WriteState(state with
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Status = "paused_resource_guard",
                    IterationsCompleted = iteration,
                    JobsSkipped = state.JobsSkipped + 1,
                    CurrentJobId = null,
                    NextAction = nextAction,
                    LastError = null,
                    CurrentlyRunning = true
                });
                WriteHeartbeat(supervisorId, state, resource.Action, storageAction, nextAction);
            }
            else if (dueJobs.Count == 0)
            {
                nextAction = "wait_for_next_schedule";
                state = WriteState(state with
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Status = "running",
                    IterationsCompleted = iteration,
                    CurrentJobId = null,
                    NextAction = nextAction,
                    LastError = null,
                    CurrentlyRunning = true
                });
                WriteHeartbeat(supervisorId, state, resource.Action, storageAction, nextAction);
            }
            else
            {
                foreach (var job in dueJobs)
                {
                    if (IsStopRequested() || DateTimeOffset.UtcNow >= deadlineUtc)
                    {
                        break;
                    }

                    var jobResource = new ResourceGuard(_storagePaths).Check();
                    if (jobResource.ShouldStop)
                    {
                        scheduler.MarkSkipped(job, DateTimeOffset.UtcNow, "resource_guard_safe_stop", jobResource.Warnings);
                        status = "stopped_resource_guard";
                        nextAction = "review_resource_status";
                        state = state with { JobsSkipped = state.JobsSkipped + 1, LastError = string.Join("; ", jobResource.Warnings) };
                        break;
                    }

                    if (jobResource.ShouldPause)
                    {
                        scheduler.MarkSkipped(job, DateTimeOffset.UtcNow, "resource_guard_pause", jobResource.Warnings);
                        state = WriteState(state with
                        {
                            UpdatedAtUtc = DateTimeOffset.UtcNow,
                            Status = "paused_resource_guard",
                            JobsSkipped = state.JobsSkipped + 1,
                            CurrentJobId = null,
                            LastJobId = job.JobId,
                            NextAction = "sleep_then_recheck_resources",
                            CurrentlyRunning = true
                        });
                        WriteHeartbeat(supervisorId, state, jobResource.Action, storageAction, "sleep_then_recheck_resources");
                        continue;
                    }

                    var jobStartedAtUtc = DateTimeOffset.UtcNow;
                    scheduler.MarkStarted(job, jobStartedAtUtc);
                    state = WriteState(state with
                    {
                        UpdatedAtUtc = jobStartedAtUtc,
                        Status = "running_job",
                        CurrentJobId = job.JobId,
                        LastJobId = job.JobId,
                        JobsStarted = state.JobsStarted + 1,
                        NextAction = $"execute_{job.JobType}",
                        CurrentlyRunning = true
                    });
                    WriteHeartbeat(supervisorId, state, jobResource.Action, storageAction, $"execute_{job.JobType}");

                    try
                    {
                        var context = new SupervisorJobContext(
                            SupervisorId: supervisorId,
                            Iteration: iteration,
                            StartedAtUtc: jobStartedAtUtc,
                            DeadlineUtc: deadlineUtc,
                            RemainingRuntime: deadlineUtc - DateTimeOffset.UtcNow);
                        var result = executor(job, context);
                        var completedAtUtc = DateTimeOffset.UtcNow;
                        var duration = (completedAtUtc - jobStartedAtUtc).TotalSeconds;
                    if (result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                    {
                        scheduler.MarkSkipped(job, completedAtUtc, result.Action, result.Warnings);
                        state = state with { JobsSkipped = state.JobsSkipped + 1 };
                    }
                    else if (result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        scheduler.MarkFailed(
                            job,
                            completedAtUtc,
                            duration,
                            result.Action,
                            result.Warnings.Count == 0 ? [result.Action] : result.Warnings);
                        lastError = result.Action;
                        state = state with { LastError = lastError };
                    }
                    else
                    {
                        scheduler.MarkCompleted(job, completedAtUtc, duration, result.Action, result.ReportPath, result.Warnings);
                        state = state with { JobsCompleted = state.JobsCompleted + 1 };
                    }
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or HttpRequestException)
                    {
                        var failedAtUtc = DateTimeOffset.UtcNow;
                        scheduler.MarkFailed(job, failedAtUtc, (failedAtUtc - jobStartedAtUtc).TotalSeconds, ex.Message, [ex.Message]);
                        lastError = ex.Message;
                        state = state with { LastError = lastError };
                    }
                    finally
                    {
                        state = WriteState(state with
                        {
                            UpdatedAtUtc = DateTimeOffset.UtcNow,
                            Status = "running",
                            CurrentJobId = null,
                            NextAction = "check_scheduler",
                            CurrentlyRunning = true
                        });
                        WriteHeartbeat(supervisorId, state, jobResource.Action, storageAction, "check_scheduler");
                    }
                }
            }

            var remaining = deadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            SleepUntilNextLoop(Math.Min(Math.Clamp(options.CheckIntervalSeconds, 5, 3600), Math.Max(1, (int)remaining.TotalSeconds)));
        }

        if (IsStopRequested())
        {
            status = "stopped_by_stop_request";
            nextAction = "safe_stop_requested";
        }

        try
        {
            var final = WriteState(state with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Status = status,
                StoppedAtUtc = DateTimeOffset.UtcNow,
                ProcessId = null,
                HeartbeatUtc = DateTimeOffset.UtcNow,
                CurrentJobId = null,
                LastError = lastError,
                NextAction = nextAction,
                StopRequestedAtUtc = IsStopRequested() ? DateTimeOffset.UtcNow : state.StopRequestedAtUtc,
                CurrentlyRunning = false
            });
            WriteHeartbeat(supervisorId, final, "stopped", "storage_not_checked", nextAction);
            ClearStopRequest();

            return new HermesSupervisorRunResult(final, scheduler.GetStatus());
        }
        finally
        {
            processManager.ClearPidIfCurrent(Environment.ProcessId);
        }
    }

    private HermesSupervisorState EmptyState(string status) =>
        new(
            StateVersion: StateVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            SupervisorId: string.Empty,
            StartedAtUtc: null,
            DeadlineUtc: null,
            StoppedAtUtc: null,
            ProcessId: null,
            HeartbeatUtc: null,
            IterationsCompleted: 0,
            JobsStarted: 0,
            JobsCompleted: 0,
            JobsSkipped: 0,
            CurrentJobId: null,
            LastJobId: null,
            LastError: null,
            NextAction: "start_supervisor",
            StopRequestedAtUtc: null,
            CurrentlyRunning: false,
            NoAutoTrading: true,
            HumanReviewRequired: true);

    private HermesSupervisorState WriteState(HermesSupervisorState state)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    private void WriteHeartbeat(string supervisorId, HermesSupervisorState state, string resourceAction, string storageAction, string nextAction)
    {
        var heartbeat = new SupervisorHeartbeat(
            TimestampUtc: DateTimeOffset.UtcNow,
            SupervisorId: supervisorId,
            ProcessId: Environment.ProcessId,
            Status: state.Status,
            IterationsCompleted: state.IterationsCompleted,
            CurrentJobId: state.CurrentJobId,
            ResourceAction: resourceAction,
            StorageAction: storageAction,
            NextAction: nextAction,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(HeartbeatPath, JsonSerializer.Serialize(heartbeat, JsonDefaults.WriteOptions));
    }

    private bool SleepUntilNextLoop(int seconds)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (IsStopRequested())
            {
                return true;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Min(5, Math.Max(0.1, remaining.TotalSeconds))));
        }

        return IsStopRequested();
    }
}
