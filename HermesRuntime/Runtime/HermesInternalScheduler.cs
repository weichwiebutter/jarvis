using System.Globalization;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class HermesInternalScheduler
{
    public static readonly ISet<string> AllowedJobTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "nightly_beta3_research",
        "storage_hygiene",
        "research_insights",
        "health_snapshot",
        "market_data_refresh",
        "strategy_discovery",
        "walkforward_validation",
        "scan_knowledge_sources",
        "process_research_queue",
        "generate_cognitive_insights",
        "trading_nightly_beta3",
        "run_planning_cycle",
        "process_planned_tasks"
    };

    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;

    public HermesInternalScheduler(StoragePaths storagePaths, string configPath)
    {
        _storagePaths = storagePaths;
        _configPath = configPath;
    }

    public string StateDirectory => Path.Combine(_storagePaths.Root, "reports", "supervisor");

    public string SchedulerStatePath => Path.Combine(StateDirectory, "scheduler_state.json");

    public ScheduleConfig LoadConfig() => ScheduleConfig.LoadOrDefault(_configPath);

    public SchedulerStatus GetStatus(DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var config = LoadConfig();
        var existing = LoadStates();
        var warnings = new List<string>();
        var jobs = config.Jobs
            .Select(job =>
            {
                if (!IsAllowed(job))
                {
                    warnings.Add($"Job '{job.JobId}' has unsupported job_type/command and will not run.");
                }

                var state = existing.GetValueOrDefault(job.JobId) ?? EmptyState(job);
                return state with
                {
                    Enabled = job.Enabled,
                    JobType = job.JobType,
                    NextRunUtc = CalculateNextRunUtc(job, state, now),
                    Warnings = state.Warnings.Concat(JobWarnings(job)).Distinct(StringComparer.Ordinal).ToList()
                };
            })
            .OrderBy(job => job.NextRunUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SchedulerStatus(
            UpdatedAtUtc: now,
            ConfigPath: _configPath,
            StatePath: SchedulerStatePath,
            CheckIntervalSeconds: Math.Clamp(config.CheckIntervalSeconds, 5, 3600),
            Jobs: jobs,
            Warnings: warnings,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    public IReadOnlyList<ScheduledJobDefinition> FindDueJobs(DateTimeOffset nowUtc)
    {
        var config = LoadConfig();
        var states = LoadStates();
        return config.Jobs
            .Where(job => job.Enabled && IsAllowed(job))
            .Where(job => IsDue(job, states.GetValueOrDefault(job.JobId) ?? EmptyState(job), nowUtc))
            .OrderBy(job => SchedulePriority(job.ScheduleType))
            .ThenBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ScheduledJobState MarkStarted(ScheduledJobDefinition job, DateTimeOffset startedAtUtc)
    {
        var states = LoadStates();
        var previous = states.GetValueOrDefault(job.JobId) ?? EmptyState(job);
        var state = previous with
        {
            Enabled = job.Enabled,
            JobType = job.JobType,
            Status = "running",
            LastRunUtc = startedAtUtc,
            LastCompletedUtc = null,
            CurrentlyRunning = true,
            RunCount = previous.RunCount + 1,
            LastSkippedReason = null,
            LastAction = "started",
            LastError = null,
            Warnings = JobWarnings(job)
        };
        return WriteState(state);
    }

    public ScheduledJobState MarkCompleted(
        ScheduledJobDefinition job,
        DateTimeOffset completedAtUtc,
        double durationSeconds,
        string action,
        string? reportPath,
        IReadOnlyList<string> warnings)
    {
        var states = LoadStates();
        var previous = states.GetValueOrDefault(job.JobId) ?? EmptyState(job);
        var state = previous with
        {
            Enabled = job.Enabled,
            JobType = job.JobType,
            Status = "completed",
            LastCompletedUtc = completedAtUtc,
            NextRunUtc = CalculateNextRunUtc(job, previous with { LastRunUtc = previous.LastRunUtc ?? completedAtUtc, LastCompletedUtc = completedAtUtc }, completedAtUtc),
            CurrentlyRunning = false,
            LastDurationSeconds = Math.Round(durationSeconds, 3),
            LastSkippedReason = null,
            LastAction = action,
            LastReportPath = reportPath,
            LastError = null,
            Warnings = warnings
        };
        return WriteState(state);
    }

    public ScheduledJobState MarkSkipped(
        ScheduledJobDefinition job,
        DateTimeOffset skippedAtUtc,
        string reason,
        IReadOnlyList<string> warnings)
    {
        var states = LoadStates();
        var previous = states.GetValueOrDefault(job.JobId) ?? EmptyState(job);
        var state = previous with
        {
            Enabled = job.Enabled,
            JobType = job.JobType,
            Status = "skipped",
            LastCompletedUtc = skippedAtUtc,
            NextRunUtc = CalculateNextRunUtc(job, previous, skippedAtUtc),
            CurrentlyRunning = false,
            LastSkippedReason = reason,
            LastAction = "skipped",
            LastError = null,
            Warnings = warnings
        };
        return WriteState(state);
    }

    public ScheduledJobState MarkFailed(
        ScheduledJobDefinition job,
        DateTimeOffset failedAtUtc,
        double durationSeconds,
        string error,
        IReadOnlyList<string> warnings)
    {
        var states = LoadStates();
        var previous = states.GetValueOrDefault(job.JobId) ?? EmptyState(job);
        var state = previous with
        {
            Enabled = job.Enabled,
            JobType = job.JobType,
            Status = "failed",
            LastCompletedUtc = failedAtUtc,
            NextRunUtc = CalculateNextRunUtc(job, previous, failedAtUtc),
            CurrentlyRunning = false,
            FailureCount = previous.FailureCount + 1,
            LastDurationSeconds = Math.Round(durationSeconds, 3),
            LastSkippedReason = null,
            LastAction = "failed",
            LastError = error,
            Warnings = warnings
        };
        return WriteState(state);
    }

    private Dictionary<string, ScheduledJobState> LoadStates()
    {
        if (!File.Exists(SchedulerStatePath))
        {
            return [];
        }

        try
        {
            var states = JsonSerializer.Deserialize<IReadOnlyList<ScheduledJobState>>(
                File.ReadAllText(SchedulerStatePath),
                JsonDefaults.SnapshotReadOptions) ?? [];
            return states.ToDictionary(state => state.JobId, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private ScheduledJobState WriteState(ScheduledJobState state)
    {
        var states = LoadStates();
        states[state.JobId] = state;
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(
            SchedulerStatePath,
            JsonSerializer.Serialize(
                states.Values.OrderBy(item => item.JobId, StringComparer.OrdinalIgnoreCase).ToList(),
                JsonDefaults.WriteOptions));
        return state;
    }

    private static ScheduledJobState EmptyState(ScheduledJobDefinition job) =>
        new(
            JobId: job.JobId,
            JobType: job.JobType,
            Enabled: job.Enabled,
            Status: job.Enabled ? "pending" : "disabled",
            LastRunUtc: null,
            LastCompletedUtc: null,
            NextRunUtc: null,
            RunCount: 0,
            FailureCount: 0,
            LastDurationSeconds: 0,
            CurrentlyRunning: false,
            LastSkippedReason: null,
            LastAction: null,
            LastReportPath: null,
            LastError: null,
            Warnings: JobWarnings(job));

    private static bool IsAllowed(ScheduledJobDefinition job)
    {
        if (!AllowedJobTypes.Contains(job.JobType))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(job.Command))
        {
            return true;
        }

        return job.JobType switch
        {
            "nightly_beta3_research" => job.Command.Equals("run-nightly-beta3", StringComparison.OrdinalIgnoreCase),
            "storage_hygiene" => job.Command.Equals("cleanup-plan", StringComparison.OrdinalIgnoreCase)
                || job.Command.Equals("storage-status", StringComparison.OrdinalIgnoreCase),
            "research_insights" => job.Command.Equals("research-insights", StringComparison.OrdinalIgnoreCase),
            "health_snapshot" => job.Command.Equals("resource-status", StringComparison.OrdinalIgnoreCase)
                || job.Command.Equals("health", StringComparison.OrdinalIgnoreCase),
            "market_data_refresh" => job.Command.Equals("download-history", StringComparison.OrdinalIgnoreCase),
            "strategy_discovery" => job.Command.Equals("strategy-discovery-status", StringComparison.OrdinalIgnoreCase),
            "walkforward_validation" => job.Command.Equals("run-walkforward-validation", StringComparison.OrdinalIgnoreCase),
            "scan_knowledge_sources" => job.Command.Equals("scan-knowledge-sources", StringComparison.OrdinalIgnoreCase),
            "process_research_queue" => job.Command.Equals("process-research-queue", StringComparison.OrdinalIgnoreCase),
            "generate_cognitive_insights" => job.Command.Equals("generate-hypotheses", StringComparison.OrdinalIgnoreCase)
                || job.Command.Equals("cognitive-insights", StringComparison.OrdinalIgnoreCase),
            "trading_nightly_beta3" => job.Command.Equals("run-nightly-beta3", StringComparison.OrdinalIgnoreCase),
            "run_planning_cycle" => job.Command.Equals("run-planning-cycle", StringComparison.OrdinalIgnoreCase),
            "process_planned_tasks" => job.Command.Equals("process-planned-tasks", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static IReadOnlyList<string> JobWarnings(ScheduledJobDefinition job)
    {
        var warnings = new List<string>();
        if (!AllowedJobTypes.Contains(job.JobType))
        {
            warnings.Add("unsupported_job_type");
        }

        if (!IsAllowed(job))
        {
            warnings.Add("unsupported_command_mapping");
        }

        return warnings;
    }

    private static bool IsDue(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (state.CurrentlyRunning)
        {
            return false;
        }

        return job.ScheduleType.ToLowerInvariant() switch
        {
            "window" => IsWindowDue(job, state, nowUtc),
            "daily" => IsDailyDue(job, state, nowUtc),
            "interval" => IsIntervalDue(job, state, nowUtc),
            _ => false
        };
    }

    private static DateTimeOffset? CalculateNextRunUtc(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (!job.Enabled || !IsAllowed(job))
        {
            return null;
        }

        return job.ScheduleType.ToLowerInvariant() switch
        {
            "window" => NextWindowRunUtc(job, state, nowUtc),
            "daily" => NextDailyRunUtc(job, state, nowUtc),
            "interval" => NextIntervalRunUtc(job, state, nowUtc),
            _ => null
        };
    }

    private static bool IsWindowDue(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (!TryParseLocalTime(job.WindowStart, out var start)
            || !TryParseLocalTime(job.WindowEnd, out var end))
        {
            return false;
        }

        var now = nowUtc.ToLocalTime();
        if (!IsInsideWindow(now.TimeOfDay, start, end))
        {
            return false;
        }

        var occurrenceStart = WindowOccurrenceStart(now, start, end);
        return state.LastRunUtc is null || state.LastRunUtc.Value.ToLocalTime() < occurrenceStart;
    }

    private static bool IsDailyDue(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (!TryParseLocalTime(job.DailyAt, out var dailyAt))
        {
            return false;
        }

        var now = nowUtc.ToLocalTime();
        var scheduled = new DateTimeOffset(now.Date + dailyAt, now.Offset);
        return now >= scheduled && (state.LastRunUtc is null || state.LastRunUtc.Value.ToLocalTime() < scheduled);
    }

    private static bool IsIntervalDue(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        var minutes = Math.Clamp(job.EveryMinutes ?? 60, 1, 1440);
        return state.LastRunUtc is null || nowUtc - state.LastRunUtc.Value >= TimeSpan.FromMinutes(minutes);
    }

    private static DateTimeOffset NextWindowRunUtc(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (!TryParseLocalTime(job.WindowStart, out var start)
            || !TryParseLocalTime(job.WindowEnd, out var end))
        {
            return DateTimeOffset.MaxValue;
        }

        var now = nowUtc.ToLocalTime();
        if (IsWindowDue(job, state, nowUtc))
        {
            return nowUtc;
        }

        var candidate = new DateTimeOffset(now.Date + start, now.Offset);
        if (now >= candidate || IsInsideWindow(now.TimeOfDay, start, end))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate.ToUniversalTime();
    }

    private static DateTimeOffset NextDailyRunUtc(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        if (!TryParseLocalTime(job.DailyAt, out var dailyAt))
        {
            return DateTimeOffset.MaxValue;
        }

        if (IsDailyDue(job, state, nowUtc))
        {
            return nowUtc;
        }

        var now = nowUtc.ToLocalTime();
        var candidate = new DateTimeOffset(now.Date + dailyAt, now.Offset);
        if (now >= candidate && (state.LastRunUtc is not null && state.LastRunUtc.Value.ToLocalTime() >= candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate.ToUniversalTime();
    }

    private static DateTimeOffset NextIntervalRunUtc(ScheduledJobDefinition job, ScheduledJobState state, DateTimeOffset nowUtc)
    {
        var minutes = Math.Clamp(job.EveryMinutes ?? 60, 1, 1440);
        return state.LastRunUtc is null
            ? nowUtc
            : state.LastRunUtc.Value.AddMinutes(minutes);
    }

    private static DateTimeOffset WindowOccurrenceStart(DateTimeOffset now, TimeSpan start, TimeSpan end)
    {
        var candidate = new DateTimeOffset(now.Date + start, now.Offset);
        if (start > end && now.TimeOfDay < end)
        {
            candidate = candidate.AddDays(-1);
        }

        return candidate;
    }

    private static bool IsInsideWindow(TimeSpan now, TimeSpan start, TimeSpan end) =>
        start <= end ? now >= start && now < end : now >= start || now < end;

    private static bool TryParseLocalTime(string? value, out TimeSpan time)
    {
        if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time))
        {
            return true;
        }

        time = default;
        return false;
    }

    private static int SchedulePriority(string scheduleType) =>
        scheduleType.ToLowerInvariant() switch
        {
            "window" => 0,
            "daily" => 1,
            "interval" => 2,
            _ => 9
        };
}
