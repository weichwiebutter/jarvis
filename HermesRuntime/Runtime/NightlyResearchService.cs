using System.Text.Json;

namespace Hermes.Runtime;

public sealed class NightlyResearchService
{
    private const string StateVersion = "nightly_research_state_v1";

    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;

    public NightlyResearchService(StoragePaths storagePaths, string configPath)
    {
        _storagePaths = storagePaths;
        _configPath = configPath;
    }

    public string StateDirectory => Path.Combine(_storagePaths.Root, "reports", "nightly_beta3");

    public string StatePath => Path.Combine(StateDirectory, "nightly_state.json");

    public string StopRequestPath => Path.Combine(StateDirectory, "stop_requested.flag");

    public NightlyResearchConfig LoadConfig() => NightlyResearchConfig.LoadOrDefault(_configPath);

    public NightlyResearchState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return EmptyState("not_started");
        }

        try
        {
            var state = JsonSerializer.Deserialize<NightlyResearchState>(
                File.ReadAllText(StatePath),
                JsonDefaults.SnapshotReadOptions) ?? EmptyState("not_started");
            return NormalizeState(state);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptyState("state_unreadable");
        }
    }

    public NightlyResearchState WriteState(NightlyResearchState state)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    public NightlyResearchState EmptyState(string status) =>
        new(
            StateVersion: StateVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: string.Empty,
            StartedAtUtc: null,
            DeadlineUtc: null,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            NextAction: "wait_for_nightly_window",
            LastCheckpointPath: null,
            LastAutopilotReportPath: null,
            LastError: null,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            NextScheduledStartUtc: CalculateNextScheduledStart(LoadConfig(), DateTimeOffset.Now).ToUniversalTime(),
            LastStartUtc: null,
            LastStopUtc: null,
            CurrentlyRunning: false,
            RuntimeDurationMinutes: 0,
            ProcessId: null,
            StopRequestedAtUtc: null);

    public NightlyResearchState CreateRunState(
        string runId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineUtc,
        string status,
        string nextAction)
    {
        return new NightlyResearchState(
            StateVersion: StateVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            NextAction: nextAction,
            LastCheckpointPath: null,
            LastAutopilotReportPath: null,
            LastError: null,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            NextScheduledStartUtc: CalculateNextScheduledStart(LoadConfig(), DateTimeOffset.Now).ToUniversalTime(),
            LastStartUtc: startedAtUtc,
            LastStopUtc: null,
            CurrentlyRunning: true,
            RuntimeDurationMinutes: 0,
            ProcessId: Environment.ProcessId,
            StopRequestedAtUtc: null);
    }

    public bool IsStopRequested() => File.Exists(StopRequestPath);

    public NightlyResearchState RequestStop()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StopRequestPath, $"stop_requested_at={DateTimeOffset.UtcNow:O}{Environment.NewLine}");
        var state = LoadState();
        var updated = state with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Status = state.CurrentlyRunning ? "stop_requested" : "stop_requested_no_running_process",
            NextAction = state.CurrentlyRunning ? "wait_for_safe_stop_checkpoint" : "no_running_nightly_process",
            StopRequestedAtUtc = DateTimeOffset.UtcNow,
            LastStopUtc = state.CurrentlyRunning ? state.LastStopUtc : DateTimeOffset.UtcNow,
            CurrentlyRunning = state.CurrentlyRunning
        };

        return WriteState(updated);
    }

    private NightlyResearchState NormalizeState(NightlyResearchState state)
    {
        var now = DateTimeOffset.UtcNow;
        var stopRequested = IsStopRequested();
        var processAlive = SupervisorProcessManager.IsProcessAlive(state.ProcessId);

        if (state.CurrentlyRunning && !processAlive)
        {
            var recovered = state with
            {
                UpdatedAtUtc = now,
                Status = stopRequested ? "stop_requested_no_running_process" : "stale_running_recovered",
                NextAction = stopRequested ? "no_running_nightly_process" : "wait_for_nightly_window",
                LastStopUtc = state.LastStopUtc ?? now,
                StopRequestedAtUtc = stopRequested ? state.StopRequestedAtUtc ?? now : null,
                CurrentlyRunning = false
            };

            if (stopRequested)
            {
                ClearStopRequest();
            }

            return WriteState(recovered);
        }

        if (stopRequested && !state.CurrentlyRunning)
        {
            var recovered = state with
            {
                UpdatedAtUtc = now,
                Status = "stop_requested_no_running_process",
                NextAction = "no_running_nightly_process",
                StopRequestedAtUtc = null,
                CurrentlyRunning = false
            };

            ClearStopRequest();
            return WriteState(recovered);
        }

        if (stopRequested && state.CurrentlyRunning)
        {
            var waiting = state with
            {
                UpdatedAtUtc = now,
                Status = "stop_requested",
                NextAction = "wait_for_safe_stop_checkpoint",
                StopRequestedAtUtc = state.StopRequestedAtUtc ?? now
            };

            return WriteState(waiting);
        }

        return state;
    }

    public DateTimeOffset? StopRequestedAtUtc()
    {
        if (!File.Exists(StopRequestPath))
        {
            return null;
        }

        return File.GetLastWriteTimeUtc(StopRequestPath);
    }

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

    public static DateTimeOffset CalculateNextScheduledStart(NightlyResearchConfig config, DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        var candidate = new DateTimeOffset(
            local.Year,
            local.Month,
            local.Day,
            config.StartHour,
            0,
            0,
            now.Offset);

        if (now >= candidate)
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }
}
