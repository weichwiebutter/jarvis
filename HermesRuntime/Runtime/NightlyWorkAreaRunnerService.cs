using System.Text.Json;

namespace Hermes.Runtime;

public sealed record NightlyWorkAreaDecision(
    string AreaId,
    string AreaTitle,
    string Status,
    string NextExecutionWindow,
    DateTimeOffset? NextExecutionAtUtc,
    bool ResourceHealthy,
    bool InNightlyWindow,
    string PlannedAction,
    string Result,
    string? OutputReportPath,
    DateTimeOffset? ExecutedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record NightlyWorkAreaRunnerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string TimeControlPath,
    string ResourcePath,
    bool InNightlyWindow,
    bool ResourceHealthy,
    NightlyWorkAreaDecision Revalidation,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class NightlyWorkAreaRunnerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;
    private readonly string _policyPath;

    public NightlyWorkAreaRunnerService(StoragePaths storagePaths, string? configPath = null)
    {
        _storagePaths = storagePaths;
        _configPath = configPath ?? Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "work_area_executor_policy.json");
        _policyPath = Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue", "work_area_executor_policy.json");
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue");

    public string ReportPath => Path.Combine(Root, "nightly_work_area_status.json");

    public string MarkdownPath => Path.Combine(Root, "nightly_work_area_status.md");

    public NightlyWorkAreaRunnerReport Run()
    {
        Directory.CreateDirectory(Root);
        var scheduler = new HermesInternalScheduler(_storagePaths, Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "schedules.json"));
        var timeControl = scheduler.GetTimeControlStatus();
        var resourceGuard = new ResourceGuard(_storagePaths);
        var resource = resourceGuard.Check();
        var policy = LoadPolicy();

        var inNightlyWindow = timeControl.NightlyWindow.ActiveNow;
        var resourceHealthy = !resource.ShouldPause && !resource.ShouldStop;
        var ready = inNightlyWindow && resourceHealthy;

        NightlyWorkAreaDecision decision;
        if (ready)
        {
            var audit = new KnowledgeValidationAuditService(_storagePaths).Run();
            decision = new NightlyWorkAreaDecision(
                AreaId: policy.AreaId,
                AreaTitle: policy.AreaTitle,
                Status: "ausgeführt",
                NextExecutionWindow: "jetzt",
                NextExecutionAtUtc: DateTimeOffset.UtcNow,
                ResourceHealthy: resourceHealthy,
                InNightlyWindow: inNightlyWindow,
                PlannedAction: policy.PlannedAction,
                Result: "Re-Validierungsplan aktualisiert",
                OutputReportPath: audit.AuditPath,
                ExecutedAtUtc: DateTimeOffset.UtcNow,
                Warnings: []);
        }
        else
        {
            decision = new NightlyWorkAreaDecision(
                AreaId: policy.AreaId,
                AreaTitle: policy.AreaTitle,
                Status: !resourceHealthy ? "geplant" : "wartet auf Nightly",
                NextExecutionWindow: !resourceHealthy ? "nach ResourceGuard" : BuildNextNightlyWindowLabel(timeControl),
                NextExecutionAtUtc: !resourceHealthy ? null : CalculateNextNightlyStartUtc(timeControl),
                ResourceHealthy: resourceHealthy,
                InNightlyWindow: inNightlyWindow,
                PlannedAction: policy.PlannedAction,
                Result: "geplant",
                OutputReportPath: policy.ReportPathHint,
                ExecutedAtUtc: null,
                Warnings: BuildWarnings(inNightlyWindow, resourceHealthy));
        }

        var report = new NightlyWorkAreaRunnerReport(
            ReportVersion: ready ? "nightly_work_area_runner_v1" : "nightly_work_area_runner_pending_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TimeControlPath: timeControl.ConfigPath,
            ResourcePath: resourceGuard.StatusPath,
            InNightlyWindow: inNightlyWindow,
            ResourceHealthy: resourceHealthy,
            Revalidation: decision,
            Warnings: decision.Warnings,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public NightlyWorkAreaRunnerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            var report = JsonSerializer.Deserialize<NightlyWorkAreaRunnerReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private WorkAreaExecutorPolicy LoadPolicy()
    {
        if (File.Exists(_policyPath))
        {
            try
            {
                var policies = JsonSerializer.Deserialize<List<WorkAreaExecutorPolicy>>(
                    File.ReadAllText(_policyPath),
                    JsonDefaults.SnapshotReadOptions);
                var match = policies?.FirstOrDefault(item => string.Equals(item.AreaId, "schedule_revalidation", StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Fall through.
            }
        }

        return new WorkAreaExecutorPolicy(
            "schedule_revalidation",
            "Re-Validierung",
            true,
            false,
            true,
            true,
            false,
            false,
            "plan_or_nightly",
            "Nightly",
            "Re-Validierung planen",
            "Schwere Läufe nur im Nightly-Fenster.",
            "reports/knowledge_validation_audit/knowledge_validation_audit.json");
    }

    private static string BuildNextNightlyWindowLabel(ScheduleTimeControlStatus timeControl)
    {
        var nextStart = CalculateNextNightlyStartUtc(timeControl);
        if (nextStart is null)
        {
            return "Nightly";
        }

        return $"wartet auf Nightly bis {nextStart.Value.ToLocalTime():HH:mm}";
    }

    private static DateTimeOffset? CalculateNextNightlyStartUtc(ScheduleTimeControlStatus timeControl)
    {
        if (!timeControl.NightlyWindow.Enabled || string.IsNullOrWhiteSpace(timeControl.NightlyWindow.Start))
        {
            return null;
        }

        var zone = ResolveTimeZone(timeControl.TimeZone);
        var currentLocal = TimeZoneInfo.ConvertTime(timeControl.CurrentUtc, zone);
        if (!TimeOnly.TryParse(timeControl.NightlyWindow.Start, out var startTime))
        {
            startTime = new TimeOnly(23, 0);
        }

        var candidateLocal = currentLocal.Date + startTime.ToTimeSpan();
        if (candidateLocal <= currentLocal.DateTime)
        {
            candidateLocal = candidateLocal.AddDays(1);
        }

        return new DateTimeOffset(candidateLocal, zone.GetUtcOffset(candidateLocal));
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId)
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private static IReadOnlyList<string> BuildWarnings(bool inNightlyWindow, bool resourceHealthy)
    {
        var warnings = new List<string>();
        if (!inNightlyWindow)
        {
            warnings.Add("wartet_auf_nightly");
        }
        if (!resourceHealthy)
        {
            warnings.Add("resourceguard_signal");
        }
        return warnings;
    }

    private void WriteReport(NightlyWorkAreaRunnerReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "autonomous_improvement_queue");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "nightly_work_area_status.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "nightly_work_area_status.md"), markdown);
        }
    }

    private static string BuildMarkdown(NightlyWorkAreaRunnerReport report)
    {
        var lines = new List<string>
        {
            "# Nightly Work Area Status",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- In Nightly Window: {report.InNightlyWindow}",
            $"- Resource Healthy: {report.ResourceHealthy}",
            string.Empty,
            "## Re-Validierung",
            $"- Status: {report.Revalidation.Status}",
            $"- Nächstes Fenster: {report.Revalidation.NextExecutionWindow}",
            $"- Nächste Ausführung UTC: {report.Revalidation.NextExecutionAtUtc?.ToString("O") ?? "-"}",
            $"- Result: {report.Revalidation.Result}",
        };

        return string.Join(Environment.NewLine, lines);
    }
}
