namespace Hermes.Runtime;

public sealed class SupervisorProcessManager
{
    private const long MaxLogBytes = 50L * 1024L * 1024L;

    private readonly StoragePaths _storagePaths;

    public SupervisorProcessManager(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StateDirectory => Path.Combine(_storagePaths.Root, "reports", "supervisor");

    public string PidPath => Path.Combine(StateDirectory, "supervisor.pid");

    public string LogPath => Path.Combine(_storagePaths.Logs, "supervisor.log");

    public SupervisorProcessStatus GetStatus(HermesSupervisorState state, SupervisorHeartbeat? heartbeat)
    {
        var activeHeartbeat = heartbeat?.SupervisorId == state.SupervisorId ? heartbeat : null;
        var pid = ReadPid();
        var pidAlive = IsProcessAlive(pid);
        var statePidAlive = IsProcessAlive(state.ProcessId);
        var running = pidAlive || (state.CurrentlyRunning && statePidAlive);
        var stalePid = pid is not null && !pidAlive;
        double? heartbeatAge = activeHeartbeat is null
            ? null
            : Math.Max(0, (DateTimeOffset.UtcNow - activeHeartbeat.TimestampUtc).TotalSeconds);

        return new SupervisorProcessStatus(
            Running: running,
            Pid: pid ?? state.ProcessId,
            StalePid: stalePid,
            PidPath: PidPath,
            LogPath: LogPath,
            StartedAtUtc: state.StartedAtUtc,
            HeartbeatUtc: activeHeartbeat?.TimestampUtc ?? state.HeartbeatUtc,
            HeartbeatAgeSeconds: heartbeatAge,
            Warning: stalePid ? "stale_pid_detected" : null);
    }

    public int? ReadPid()
    {
        if (!File.Exists(PidPath))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(PidPath).Trim();
            return int.TryParse(text, out var pid) ? pid : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void WritePid(int pid)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(PidPath, pid.ToString());
    }

    public void ClearPidIfCurrent(int pid)
    {
        try
        {
            if (ReadPid() == pid)
            {
                File.Delete(PidPath);
            }
        }
        catch (IOException)
        {
            // Best effort only. A later status call will mark stale PID if needed.
        }
    }

    public void ClearStalePid()
    {
        var pid = ReadPid();
        if (pid is null || IsProcessAlive(pid))
        {
            return;
        }

        try
        {
            File.Delete(PidPath);
        }
        catch (IOException)
        {
            // Best effort only; duplicate prevention still uses process liveness.
        }
    }

    public void RotateLogIfNeeded()
    {
        Directory.CreateDirectory(_storagePaths.Logs);
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length <= MaxLogBytes)
        {
            return;
        }

        var archivePath = Path.Combine(
            _storagePaths.Logs,
            $"supervisor.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.log");
        File.Move(LogPath, archivePath, overwrite: false);
    }

    public void AppendLogLine(string message)
    {
        Directory.CreateDirectory(_storagePaths.Logs);
        File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
    }

    public static bool IsProcessAlive(int? processId)
    {
        if (processId is null || processId <= 0)
        {
            return false;
        }

        if (Directory.Exists($"/proc/{processId.Value}"))
        {
            return true;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId.Value);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
