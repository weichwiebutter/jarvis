using System.Globalization;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ResourceGuard
{
    private readonly StoragePaths _storagePaths;
    private readonly ResourceGuardPolicy _policy;

    public ResourceGuard(StoragePaths storagePaths, ResourceGuardPolicy? policy = null)
    {
        _storagePaths = storagePaths;
        _policy = policy ?? ResourceGuardPolicy.Default;
    }

    public string ReportDirectory => Path.Combine(_storagePaths.Root, "reports", "resource");

    public string StatusPath => Path.Combine(ReportDirectory, "resource_status.json");

    public ResourceSnapshot Check()
    {
        Directory.CreateDirectory(ReportDirectory);
        var previous = LoadPreviousSnapshot();
        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var cpu = ReadCpuUsagePercent();
        var memory = ReadMemory();
        var disk = ReadDisk();
        var shouldPause = false;
        var shouldStop = false;
        var action = "continue";

        if (cpu >= _policy.CpuPauseThresholdPercent)
        {
            var sustained = previous is not null
                && previous.CpuUsagePercent >= _policy.CpuPauseThresholdPercent
                && (now - previous.TimestampUtc).TotalMinutes >= _policy.CpuSustainedMinutes;
            warnings.Add(sustained
                ? $"CPU usage above pause threshold for sustained window: {cpu:0.#}% >= {_policy.CpuPauseThresholdPercent:0.#}%"
                : $"CPU usage above pause threshold; waiting for sustained window before pausing: {cpu:0.#}% >= {_policy.CpuPauseThresholdPercent:0.#}%");
            if (sustained)
            {
                shouldPause = true;
                action = "pause_research";
            }
        }

        if (memory.UsagePercent >= _policy.MemoryPauseThresholdPercent)
        {
            warnings.Add($"RAM usage above pause threshold: {memory.UsagePercent:0.#}% >= {_policy.MemoryPauseThresholdPercent:0.#}%");
            shouldPause = true;
            action = "pause_research";
        }

        if (disk.FreePercent < _policy.DiskCleanupFreePercent)
        {
            warnings.Add($"Disk free below cleanup threshold: {disk.FreePercent:0.#}% < {_policy.DiskCleanupFreePercent:0.#}%");
            action = "plan_cleanup";
        }

        if (disk.FreePercent < _policy.DiskStopFreePercent)
        {
            warnings.Add($"Disk free below stop threshold: {disk.FreePercent:0.#}% < {_policy.DiskStopFreePercent:0.#}%");
            shouldStop = true;
            action = "safe_stop";
        }

        var snapshot = new ResourceSnapshot(
            TimestampUtc: now,
            CpuUsagePercent: Math.Round(cpu, 2),
            MemoryUsagePercent: Math.Round(memory.UsagePercent, 2),
            TotalMemoryMb: memory.TotalMb,
            UsedMemoryMb: memory.UsedMb,
            FreeDiskMb: disk.FreeMb,
            FreeDiskPercent: Math.Round(disk.FreePercent, 2),
            StorageRoot: _storagePaths.Root,
            Action: action,
            Warnings: warnings,
            ShouldPause: shouldPause,
            ShouldStop: shouldStop,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(StatusPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        return snapshot;
    }

    private ResourceSnapshot? LoadPreviousSnapshot()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResourceSnapshot>(
                File.ReadAllText(StatusPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static double ReadCpuUsagePercent()
    {
        try
        {
            if (File.Exists("/proc/loadavg"))
            {
                var first = File.ReadAllText("/proc/loadavg")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var load))
                {
                    return Math.Clamp(load / Math.Max(1, Environment.ProcessorCount) * 100, 0, 100);
                }
            }
        }
        catch (IOException)
        {
            // Fall through to conservative default.
        }

        return 0;
    }

    private static (long TotalMb, long UsedMb, double UsagePercent) ReadMemory()
    {
        try
        {
            if (File.Exists("/proc/meminfo"))
            {
                var values = File.ReadLines("/proc/meminfo")
                    .Select(line => line.Split(':', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(
                        parts => parts[0],
                        parts => long.TryParse(
                            new string(parts[1].Where(char.IsDigit).ToArray()),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var value) ? value : 0,
                        StringComparer.OrdinalIgnoreCase);
                var totalKb = values.GetValueOrDefault("MemTotal");
                var availableKb = values.GetValueOrDefault("MemAvailable");
                if (totalKb > 0)
                {
                    var usedKb = Math.Max(0, totalKb - availableKb);
                    return (totalKb / 1024, usedKb / 1024, usedKb / (double)totalKb * 100);
                }
            }
        }
        catch (IOException)
        {
            // Fall through to GC fallback.
        }

        var gc = GC.GetGCMemoryInfo();
        var total = Math.Max(1, gc.TotalAvailableMemoryBytes / 1024 / 1024);
        var used = GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024;
        return (total, used, used / (double)total * 100);
    }

    private (long FreeMb, double FreePercent) ReadDisk()
    {
        try
        {
            var root = Path.GetPathRoot(_storagePaths.Root);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                var free = drive.AvailableFreeSpace / 1024 / 1024;
                var total = Math.Max(1, drive.TotalSize / 1024 / 1024);
                return (free, free / (double)total * 100);
            }
        }
        catch (IOException)
        {
            // Return safe-stop values below.
        }

        return (0, 0);
    }
}
