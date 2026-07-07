using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BridgeDiagnosticsReport(
    string BridgeName,
    string ProcessExpected,
    bool ProcessRunning,
    int? ProcessId,
    string ConfiguredHost,
    int ConfiguredPort,
    string HealthEndpoint,
    DateTimeOffset? LastSuccessfulHeartbeatUtc,
    string? LastFailure,
    int FailureCount,
    string RecommendedAction,
    bool CanStart,
    bool CanStop,
    bool CanRestart,
    string Status,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings);

public sealed class BridgeDiagnosticsService
{
    private readonly string _runtimeRoot;
    private readonly string _reportRoot;

    public BridgeDiagnosticsService(string runtimeRoot)
    {
        _runtimeRoot = runtimeRoot;
        _reportRoot = Path.Combine(runtimeRoot, ".codex_artifacts", "reports", "bridge_diagnostics");
    }

    public string ReportPath => Path.Combine(_reportRoot, "bridge_diagnostics.json");
    public string MarkdownPath => Path.Combine(_reportRoot, "bridge_diagnostics.md");

    public BridgeDiagnosticsReport Diagnose(string? host = null, int? port = null)
    {
        var configuredHost = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host!;
        var configuredPort = port.GetValueOrDefault(8787);
        var healthEndpoint = $"http://{configuredHost}:{configuredPort}/bridge/health";
        var processExpected = $"dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge --url http://{configuredHost}:{configuredPort}/";

        var warnings = new List<string>();
        var process = FindBridgeProcess(configuredHost, configuredPort);
        var processRunning = process is not null;
        var processId = process?.Id;
        DateTimeOffset? lastSuccessfulHeartbeatUtc = null;
        string? lastFailure = null;
        var failureCount = 0;
        var status = "offline";

        if (TryProbeHealth(healthEndpoint, out var healthStatus, out var heartbeatUtc, out var probeWarning))
        {
            status = healthStatus;
            lastSuccessfulHeartbeatUtc = heartbeatUtc;
            failureCount = 0;
            if (!string.IsNullOrWhiteSpace(probeWarning))
            {
                warnings.Add(probeWarning);
            }
        }
        else
        {
            failureCount = processRunning ? 1 : 1;
            lastFailure = processRunning
                ? "Bridge-Probe fehlgeschlagen trotz laufendem Prozess."
                : "Bridge-Probe fehlgeschlagen: Prozess nicht aktiv.";
            warnings.Add(lastFailure);
        }

        var recommendedAction = !processRunning
            ? "bridge-start"
            : failureCount > 0
                ? "bridge-restart"
                : "bridge-health";

        var report = new BridgeDiagnosticsReport(
            BridgeName: "Hermes Read-only Bridge",
            ProcessExpected: processExpected,
            ProcessRunning: processRunning,
            ProcessId: processId,
            ConfiguredHost: configuredHost,
            ConfiguredPort: configuredPort,
            HealthEndpoint: healthEndpoint,
            LastSuccessfulHeartbeatUtc: lastSuccessfulHeartbeatUtc,
            LastFailure: lastFailure,
            FailureCount: failureCount,
            RecommendedAction: recommendedAction,
            CanStart: !processRunning,
            CanStop: processRunning,
            CanRestart: true,
            Status: status,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: warnings);

        Persist(report);
        return report;
    }

    public bool Start(string host, int port, out string message)
    {
        if (FindBridgeProcess(host, port) is not null)
        {
            message = "Bridge läuft bereits.";
            return true;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project ./cli/Hermes.Cli.csproj -- readonly-bridge --url http://{host}:{port}/",
                WorkingDirectory = _runtimeRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                message = "Bridge-Prozess konnte nicht gestartet werden.";
                return false;
            }

            var healthEndpoint = $"http://{host}:{port}/bridge/health";
            var ready = WaitForHealth(healthEndpoint, TimeSpan.FromSeconds(5), out var readinessMessage);
            message = ready
                ? $"Bridge-Startprozess mit PID {process.Id} gestartet und bereit."
                : $"Bridge-Startprozess mit PID {process.Id} gestartet, aber noch nicht bereit: {readinessMessage}";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public bool Stop(string host, int port, out string message)
    {
        var processes = FindBridgeProcesses(host, port);
        if (processes.Count == 0)
        {
            message = "Bridge ist nicht aktiv.";
            return true;
        }

        var stopped = 0;
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    stopped++;
                }
            }
            catch
            {
                // defensive no-op
            }
        }

        message = stopped > 0
            ? $"Bridge-Prozess(e) beendet: {stopped}."
            : "Bridge-Prozess konnte nicht beendet werden.";
        return stopped > 0;
    }

    public bool Restart(string host, int port, out string message)
    {
        var stopped = Stop(host, port, out var stopMessage);
        var started = Start(host, port, out var startMessage);
        message = $"stop={stopMessage}; start={startMessage}";
        return stopped && started;
    }

    private bool WaitForHealth(string healthEndpoint, TimeSpan timeout, out string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (TryProbeHealth(healthEndpoint, out _, out _, out var warning))
            {
                message = string.IsNullOrWhiteSpace(warning) ? "available" : warning!;
                return true;
            }

            Thread.Sleep(250);
        }

        message = "timeout";
        return false;
    }

    private bool TryProbeHealth(string url, out string healthStatus, out DateTimeOffset? heartbeatUtc, out string? warning)
    {
        healthStatus = "offline";
        heartbeatUtc = null;
        warning = null;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = client.GetStringAsync(url).GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
            healthStatus = data.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString() ?? "available"
                : "available";
            heartbeatUtc = data.TryGetProperty("timestamp_utc", out var ts) && ts.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(ts.GetString(), out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;
            if (!string.Equals(healthStatus, "available", StringComparison.OrdinalIgnoreCase))
            {
                warning = $"Bridge meldet Status '{healthStatus}'.";
            }

            return true;
        }
        catch (Exception ex)
        {
            warning = $"Bridge ist nicht erreichbar: {ex.Message}";
            return false;
        }
    }

    private Process? FindBridgeProcess(string host, int port)
        => FindBridgeProcesses(host, port).FirstOrDefault();

    private List<Process> FindBridgeProcesses(string host, int port)
    {
        var candidates = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var cmdline = ReadCommandLine(process.Id);
                if (string.IsNullOrWhiteSpace(cmdline))
                {
                    continue;
                }

                var portToken = $":{port}";
                if (cmdline.Contains("readonly-bridge", StringComparison.OrdinalIgnoreCase) &&
                    (cmdline.Contains(portToken, StringComparison.OrdinalIgnoreCase) ||
                     cmdline.Contains($"{host}:{port}", StringComparison.OrdinalIgnoreCase) ||
                     cmdline.Contains("--url", StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(process);
                }
            }
            catch
            {
                // ignore inaccessible processes
            }
        }

        return candidates;
    }

    private static string? ReadCommandLine(int pid)
    {
        var path = $"/proc/{pid}/cmdline";
        if (!File.Exists(path))
        {
            return null;
        }

        var raw = File.ReadAllText(path);
        return raw.Replace('\0', ' ').Trim();
    }

    private void Persist(BridgeDiagnosticsReport report)
    {
        Directory.CreateDirectory(_reportRoot);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));

        var markdown = new StringBuilder();
        markdown.AppendLine("# Bridge Diagnostics");
        markdown.AppendLine();
        markdown.AppendLine($"- bridge_name: {report.BridgeName}");
        markdown.AppendLine($"- status: {report.Status}");
        markdown.AppendLine($"- process_expected: {report.ProcessExpected}");
        markdown.AppendLine($"- process_running: {report.ProcessRunning.ToString().ToLowerInvariant()}");
        markdown.AppendLine($"- process_id: {report.ProcessId?.ToString() ?? "-"}");
        markdown.AppendLine($"- configured_host: {report.ConfiguredHost}");
        markdown.AppendLine($"- configured_port: {report.ConfiguredPort}");
        markdown.AppendLine($"- health_endpoint: {report.HealthEndpoint}");
        markdown.AppendLine($"- last_successful_heartbeat_utc: {report.LastSuccessfulHeartbeatUtc?.ToString("O") ?? "-"}");
        markdown.AppendLine($"- last_failure: {report.LastFailure ?? "-"}");
        markdown.AppendLine($"- failure_count: {report.FailureCount}");
        markdown.AppendLine($"- recommended_action: {report.RecommendedAction}");
        markdown.AppendLine($"- can_start: {report.CanStart.ToString().ToLowerInvariant()}");
        markdown.AppendLine($"- can_stop: {report.CanStop.ToString().ToLowerInvariant()}");
        markdown.AppendLine($"- can_restart: {report.CanRestart.ToString().ToLowerInvariant()}");
        markdown.AppendLine();
        markdown.AppendLine("## Warnings");
        foreach (var warning in report.Warnings)
        {
            markdown.AppendLine($"- {warning}");
        }

        File.WriteAllText(MarkdownPath, markdown.ToString());
    }

}
