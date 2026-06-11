using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Runtime;

public sealed record ScheduleConfig(
    string ScheduleVersion,
    int CheckIntervalSeconds,
    IReadOnlyList<ScheduledJobDefinition> Jobs)
{
    public string TimeZone { get; init; } = "Europe/Berlin";

    public SchedulerWindowConfig WorkWindow { get; init; } = new("08:00", "18:00", true);

    public SchedulerWindowConfig NightlyWindow { get; init; } = new("23:00", "05:00", true);

    public SchedulerWindowConfig LearningWindow { get; init; } = new("05:30", "07:00", true);

    public SchedulerWindowConfig HumanReviewWindow { get; init; } = new("08:00", "18:00", true);

    public IReadOnlyList<string> ActiveWeekdays { get; init; } =
    [
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday"
    ];

    [JsonIgnore]
    public IReadOnlyList<string> InactiveWeekdays => AllWeekdays
        .Where(day => !NormalizeWeekdays(ActiveWeekdays).Contains(day, StringComparer.OrdinalIgnoreCase))
        .ToList();

    public static ScheduleConfig Default => new(
        ScheduleVersion: "schedules_v1",
        CheckIntervalSeconds: 60,
        Jobs:
        [
            new(
                JobId: "planning_cycle_hourly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "interval",
                Command: "run-planning-cycle",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "planning_cycle_before_nightly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "daily",
                Command: "run-planning-cycle",
                DailyAt: "22:45",
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "goal_review_before_nightly",
                JobType: "review_goals",
                Enabled: true,
                ScheduleType: "daily",
                Command: "goals",
                DailyAt: "22:50"),
            new(
                JobId: "nightly_beta3_research",
                JobType: "trading_nightly_beta3",
                Enabled: true,
                ScheduleType: "window",
                Command: "run-nightly-beta3",
                WindowStart: "23:00",
                WindowEnd: "05:00",
                MaxRuntimeMinutes: 360,
                SleepSeconds: 60,
                MaxIdleIterations: 10),
            new(
                JobId: "storage_hygiene",
                JobType: "storage_hygiene",
                Enabled: true,
                ScheduleType: "daily",
                DailyAt: "05:15"),
            new(
                JobId: "research_insights",
                JobType: "research_insights",
                Enabled: true,
                ScheduleType: "daily",
                Command: "research-insights",
                DailyAt: "05:30"),
            new(
                JobId: "scan_knowledge_sources",
                JobType: "scan_knowledge_sources",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-knowledge-sources",
                DailyAt: "05:40"),
            new(
                JobId: "scan_software_domain",
                JobType: "scan_software_domain",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-software-domain",
                DailyAt: "05:42"),
            new(
                JobId: "scan_documentation_domain",
                JobType: "scan_documentation_domain",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-documentation-domain",
                DailyAt: "05:43"),
            new(
                JobId: "scan_process_domain",
                JobType: "scan_process_domain",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-process-domain",
                DailyAt: "05:44"),
            new(
                JobId: "scan_research_domain",
                JobType: "scan_research_domain",
                Enabled: true,
                ScheduleType: "daily",
                Command: "scan-research-domain",
                DailyAt: "05:45"),
            new(
                JobId: "process_research_queue",
                JobType: "process_research_queue",
                Enabled: true,
                ScheduleType: "window",
                Command: "process-research-queue",
                WindowStart: "23:00",
                WindowEnd: "05:00",
                MaxRuntimeMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "50" }),
            new(
                JobId: "generate_cognitive_insights",
                JobType: "generate_cognitive_insights",
                Enabled: true,
                ScheduleType: "daily",
                Command: "generate-hypotheses",
                DailyAt: "05:50",
                Parameters: new Dictionary<string, string> { ["domain"] = "trading" }),
            new(
                JobId: "generate_domain_insights",
                JobType: "generate_domain_insights",
                Enabled: true,
                ScheduleType: "daily",
                Command: "domain-insights",
                DailyAt: "05:52"),
            new(
                JobId: "planning_cycle_after_nightly",
                JobType: "run_planning_cycle",
                Enabled: true,
                ScheduleType: "daily",
                Command: "run-planning-cycle",
                DailyAt: "05:55",
                Parameters: new Dictionary<string, string> { ["max_items"] = "20" }),
            new(
                JobId: "goal_progress_update",
                JobType: "update_goal_progress",
                Enabled: true,
                ScheduleType: "daily",
                Command: "goal-progress",
                DailyAt: "05:58"),
            new(
                JobId: "goal_review_after_nightly",
                JobType: "review_goals",
                Enabled: true,
                ScheduleType: "daily",
                Command: "goals",
                DailyAt: "06:00"),
            new(
                JobId: "process_planned_tasks_after_planning",
                JobType: "process_planned_tasks",
                Enabled: true,
                ScheduleType: "interval",
                Command: "execute-planned-tasks",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "10" }),
            new(
                JobId: "outcome_feedback_after_planned_tasks",
                JobType: "evaluate_task_outcomes",
                Enabled: true,
                ScheduleType: "interval",
                Command: "evaluate-task-outcomes",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string> { ["max_items"] = "50" }),
            new(
                JobId: "knowledge_quality_after_outcomes",
                JobType: "evaluate_knowledge_quality",
                Enabled: true,
                ScheduleType: "interval",
                Command: "knowledge-health",
                EveryMinutes: 120),
            new(
                JobId: "domain_knowledge_validation",
                JobType: "validate_domain_knowledge",
                Enabled: true,
                ScheduleType: "interval",
                Command: "validate-domain-knowledge",
                EveryMinutes: 180,
                Parameters: new Dictionary<string, string>
                {
                    ["domain"] = "documentation",
                    ["max_items"] = "20"
                }),
            new(
                JobId: "memory_consolidation_daily",
                JobType: "consolidate_memory",
                Enabled: true,
                ScheduleType: "daily",
                Command: "consolidate-memory",
                DailyAt: "06:04"),
            new(
                JobId: "autonomous_loop_hourly",
                JobType: "run_autonomous_loop",
                Enabled: true,
                ScheduleType: "interval",
                Command: "run-autonomous-loop",
                EveryMinutes: 60,
                Parameters: new Dictionary<string, string>
                {
                    ["max_iterations"] = "1",
                    ["max_minutes"] = "10"
                }),
            new(
                JobId: "health_snapshot",
                JobType: "health_snapshot",
                Enabled: true,
                ScheduleType: "interval",
                Command: "resource-status",
                EveryMinutes: 60),
            new(
                JobId: "market_data_refresh",
                JobType: "market_data_refresh",
                Enabled: false,
                ScheduleType: "daily",
                Command: "download-history",
                DailyAt: "22:30")
        ])
    {
        TimeZone = "Europe/Berlin",
        WorkWindow = new SchedulerWindowConfig("08:00", "18:00", true),
        NightlyWindow = new SchedulerWindowConfig("23:00", "05:00", true),
        LearningWindow = new SchedulerWindowConfig("05:30", "07:00", true),
        HumanReviewWindow = new SchedulerWindowConfig("08:00", "18:00", true),
        ActiveWeekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
    };

    public static ScheduleConfig LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduleConfig>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Default;
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(this, JsonDefaults.WriteOptions));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    public ScheduleConfig WithTimeControl(ScheduleTimeControlUpdate update)
    {
        var activeWeekdays = NormalizeWeekdays(update.ActiveWeekdays);

        return this with
        {
            TimeZone = string.IsNullOrWhiteSpace(update.TimeZone) ? TimeZone : update.TimeZone.Trim(),
            WorkWindow = update.WorkWindow ?? WorkWindow,
            NightlyWindow = update.NightlyWindow ?? NightlyWindow,
            LearningWindow = update.LearningWindow ?? LearningWindow,
            HumanReviewWindow = update.HumanReviewWindow ?? HumanReviewWindow,
            ActiveWeekdays = activeWeekdays.Count > 0 ? activeWeekdays : ActiveWeekdays
        };
    }

    public ScheduleTimeControlStatus BuildTimeControlStatus(DateTimeOffset nowUtc, string configPath)
    {
        var warnings = new List<string>();
        var timeZone = ResolveTimeZone(TimeZone, warnings);
        var currentLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var activeWeekdays = NormalizeWeekdays(ActiveWeekdays);
        var inWorkWeekday = activeWeekdays.Contains(currentLocal.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase);

        var workWindowActive = EvaluateWindow(WorkWindow, "Arbeitszeit", currentLocal, inWorkWeekday);
        var nightlyWindowActive = EvaluateWindow(NightlyWindow, "Nightly", currentLocal, true);
        var learningWindowActive = EvaluateWindow(LearningWindow, "Lernfenster", currentLocal, true);
        var humanReviewWindowActive = EvaluateWindow(HumanReviewWindow, "Human-Review", currentLocal, true);

        return new ScheduleTimeControlStatus(
            ConfigPath: configPath,
            TimeZone: timeZone.Id,
            CurrentUtc: nowUtc,
            CurrentLocal: currentLocal,
            StatusLabel: workWindowActive.ActiveNow ? "Derzeit im Arbeitsfenster" : "Außerhalb des Arbeitsfensters",
            InWorkWindow: workWindowActive.ActiveNow,
            WorkWindow: workWindowActive,
            NightlyWindow: nightlyWindowActive,
            LearningWindow: learningWindowActive,
            HumanReviewWindow: humanReviewWindowActive,
            Weekdays: AllWeekdays.Select(day => new ScheduleWeekdayStatus(day, activeWeekdays.Contains(day, StringComparer.OrdinalIgnoreCase))).ToList(),
            ActiveWeekdays: activeWeekdays,
            InactiveWeekdays: AllWeekdays.Where(day => !activeWeekdays.Contains(day, StringComparer.OrdinalIgnoreCase)).ToList(),
            Warnings: warnings,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static SchedulerWindowStatus EvaluateWindow(
        SchedulerWindowConfig window,
        string label,
        DateTimeOffset currentLocal,
        bool dayAllowed)
    {
        var active = window.Enabled && dayAllowed && IsInsideWindow(TimeOnly.FromTimeSpan(currentLocal.TimeOfDay), ParseTime(window.Start), ParseTime(window.End));
        return new SchedulerWindowStatus(
            Label: label,
            Enabled: window.Enabled,
            Start: window.Start,
            End: window.End,
            ActiveNow: active,
            Summary: active ? "aktiv" : "inaktiv");
    }

    private static IReadOnlyList<string> NormalizeWeekdays(IReadOnlyList<string>? weekdays)
    {
        var normalized = weekdays?
            .Select(day => day.Trim())
            .Where(day => !string.IsNullOrWhiteSpace(day))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return normalized;
    }

    private static TimeOnly ParseTime(string? value)
    {
        if (TimeOnly.TryParse(value, out var time))
        {
            return time;
        }

        return new TimeOnly(0, 0);
    }

    private static bool IsInsideWindow(TimeOnly current, TimeOnly start, TimeOnly end)
    {
        return start <= end
            ? current >= start && current < end
            : current >= start || current < end;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZone, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                warnings.Add($"time_zone_not_found:{timeZone}");
            }
            catch (InvalidTimeZoneException)
            {
                warnings.Add($"time_zone_invalid:{timeZone}");
            }
        }

        return TimeZoneInfo.Local;
    }

    private static readonly IReadOnlyList<string> AllWeekdays =
    [
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
        "Sunday"
    ];
}

public sealed record SchedulerWindowConfig(
    string Start,
    string End,
    bool Enabled);

public sealed record ScheduleTimeControlUpdate(
    string? TimeZone,
    SchedulerWindowConfig? WorkWindow,
    SchedulerWindowConfig? NightlyWindow,
    SchedulerWindowConfig? LearningWindow,
    SchedulerWindowConfig? HumanReviewWindow,
    IReadOnlyList<string>? ActiveWeekdays);

public sealed record SchedulerWindowStatus(
    string Label,
    bool Enabled,
    string Start,
    string End,
    bool ActiveNow,
    string Summary);

public sealed record ScheduleWeekdayStatus(
    string Day,
    bool Active);

public sealed record ScheduleTimeControlStatus(
    string ConfigPath,
    string TimeZone,
    DateTimeOffset CurrentUtc,
    DateTimeOffset CurrentLocal,
    string StatusLabel,
    bool InWorkWindow,
    SchedulerWindowStatus WorkWindow,
    SchedulerWindowStatus NightlyWindow,
    SchedulerWindowStatus LearningWindow,
    SchedulerWindowStatus HumanReviewWindow,
    IReadOnlyList<ScheduleWeekdayStatus> Weekdays,
    IReadOnlyList<string> ActiveWeekdays,
    IReadOnlyList<string> InactiveWeekdays,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);
