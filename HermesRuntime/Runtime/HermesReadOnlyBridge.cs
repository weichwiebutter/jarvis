using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hermes.Runtime;

public sealed class HermesReadOnlyBridge
{
    private const string BridgeVersion = "hermes_readonly_bridge_v1";
    private const int MaxArrayItems = 25;

    private static readonly IReadOnlyList<ReportDefinition> Reports =
    [
        new("runtimeHealth", "Runtime Health", "/runtime/health", "reports/runtime_health.json"),
        new("setupWatch", "Setup Watch", "/runtime/setup-watch", "setup_watch/setup_watch.json"),
        new("supervisorState", "Supervisor State", "/runtime/supervisor", "reports/supervisor/supervisor_state.json"),
        new("schedulerState", "Scheduler State", "/runtime/scheduler", "reports/supervisor/scheduler_state.json"),
        new("resourceStatus", "Resource Status", "/runtime/resource", "reports/resource/resource_status.json"),
        new("storageStatus", "Storage Status", "/runtime/storage", "reports/storage/storage_status.json"),
        new("cleanupPlan", "Cleanup Plan", "/runtime/cleanup-plan", "reports/storage/cleanup_plan.json"),
        new("nightlyState", "Nightly State", "/runtime/nightly", "reports/nightly_beta3/nightly_state.json"),
        new("researchInsights", "Research Insights", "/reports/research-insights", "strategy_research/research_insights.json"),
        new("robustStrategies", "Robuste Strategien", "/reports/robust-strategies", "strategy_research/robust_strategies.json"),
        new("overfitReport", "Overfit Report", "/reports/overfit-report", "strategy_research/overfit_report.json"),
        new("regimeSummary", "Regime Summary", "/reports/regime-summary", "reports/regimes/regime_summary.json"),
        new("strategyRegimePerformance", "Strategy Regime Performance", "/reports/strategy-regime-performance", "reports/regimes/strategy_regime_performance.json"),
        new("regimeDistribution", "Regime Distribution", "/reports/regime-distribution", "reports/regimes/regime_distribution.json")
    ];

    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "client_secret",
        "api_key",
        "apikey",
        "authorization",
        "access_token",
        "refresh_token",
        "password",
        "token"
    };

    private readonly StoragePaths _storagePaths;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    public HermesReadOnlyBridge(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public async Task RunAsync(string urlPrefix, CancellationToken cancellationToken)
    {
        var normalizedPrefix = NormalizePrefix(urlPrefix);
        using var listener = new HttpListener();
        listener.Prefixes.Add(normalizedPrefix);
        listener.Start();

        Console.WriteLine("Hermes Read-Only Bridge");
        Console.WriteLine("-----------------------");
        Console.WriteLine($"Listening              {normalizedPrefix}");
        Console.WriteLine($"Storage Root           {DisplayPath(_storagePaths.Root)}");
        Console.WriteLine("Mode                   read-only");
        Console.WriteLine("Safety                 no_auto_trading=true, human_review_required=true");
        Console.WriteLine("Stop                   Ctrl+C");

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
        }
    }

    public BridgeResponseModel CreateHealthResponse()
    {
        var index = BuildReportIndex();
        var health = new BridgeHealthSnapshot(
            Status: "available",
            BridgeVersion: BridgeVersion,
            StartedAtUtc: _startedAtUtc,
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportsConfigured: Reports.Count,
            ReportsAvailable: index.Reports.Count(report => report.Available),
            Endpoints: Reports.Select(report => report.Endpoint)
                .Append("/bridge/health")
                .Append("/reports")
                .Append("/operator/dashboard")
                .Order()
                .ToArray());

        return Ok(health);
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            ApplyHeaders(context.Response, context.Request);

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod != "GET")
            {
                await WriteJsonAsync(
                    context.Response,
                    Error("method_not_allowed", "Only GET requests are allowed."),
                    HttpStatusCode.MethodNotAllowed);
                return;
            }

            var path = (context.Request.Url?.AbsolutePath ?? "/").TrimEnd('/');
            path = string.IsNullOrWhiteSpace(path) ? "/" : path;

            var response = path switch
            {
                "/bridge/health" => CreateHealthResponse(),
                "/reports" => Ok(BuildReportIndex()),
                "/operator/dashboard" => BuildOperatorDashboardResponse(),
                _ => BuildReportResponse(path)
            };

            var statusCode = response.Status == "not_found"
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK;
            await WriteJsonAsync(context.Response, response, statusCode);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(
                context.Response,
                Error("bridge_error", $"Read-only bridge error: {ex.Message}"),
                HttpStatusCode.InternalServerError);
        }
    }

    private BridgeResponseModel BuildOperatorDashboardResponse()
    {
        var warnings = new List<string>();
        var dashboard = new Dictionary<string, object?>
        {
            ["reportIndex"] = BuildReportIndex()
        };

        foreach (var report in Reports)
        {
            var result = TryReadReport(report);
            dashboard[report.Key] = result.Data;
            warnings.AddRange(result.Warnings);
        }

        return new BridgeResponseModel(
            Status: "available",
            DataSource: "readonly_bridge",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: dashboard,
            Warnings: warnings);
    }

    private BridgeResponseModel BuildReportResponse(string path)
    {
        var report = Reports.FirstOrDefault(item =>
            string.Equals(item.Endpoint, path, StringComparison.OrdinalIgnoreCase));

        if (report is null)
        {
            return Error("not_found", $"Endpoint is not whitelisted: {path}");
        }

        var result = TryReadReport(report);
        return new BridgeResponseModel(
            Status: result.Available ? "available" : "unavailable",
            DataSource: result.Available ? "readonly_bridge" : "unavailable",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: result.Data,
            Warnings: result.Warnings);
    }

    private ReportIndex BuildReportIndex()
    {
        return new ReportIndex(
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Reports: Reports.Select(report =>
            {
                var path = GetWhitelistedPath(report.RelativePath);
                var info = File.Exists(path) ? new FileInfo(path) : null;

                return new ReportIndexItem(
                    Key: report.Key,
                    Label: report.Label,
                    Endpoint: report.Endpoint,
                    Available: info?.Exists == true,
                    UpdatedAtUtc: info?.Exists == true ? info.LastWriteTimeUtc : null,
                    SizeBytes: info?.Exists == true ? info.Length : null);
            }).ToArray());
    }

    private ReportReadResult TryReadReport(ReportDefinition report)
    {
        var path = GetWhitelistedPath(report.RelativePath);
        if (!File.Exists(path))
        {
            return new ReportReadResult(
                Available: false,
                Data: null,
                Warnings: [$"{report.Label} nicht gefunden oder noch nicht erzeugt."]);
        }

        try
        {
            var json = File.ReadAllText(path);
            var node = JsonNode.Parse(json);
            Sanitize(node);

            return new ReportReadResult(
                Available: true,
                Data: node,
                Warnings: []);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new ReportReadResult(
                Available: false,
                Data: null,
                Warnings: [$"{report.Label} nicht lesbar: {ex.Message}"]);
        }
    }

    private string GetWhitelistedPath(string relativePath)
    {
        var root = Path.GetFullPath(_storagePaths.Root);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(fullPath, root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Report path escaped Hermes data root.");
        }

        return fullPath;
    }

    private static void Sanitize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var key in jsonObject.Select(property => property.Key).ToArray())
                {
                    if (IsSecretKey(key))
                    {
                        jsonObject[key] = "[redacted]";
                        continue;
                    }

                    if (IsPathKey(key))
                    {
                        jsonObject[key] = jsonObject[key] is JsonArray
                            ? new JsonArray("[redacted_path]")
                            : "[redacted_path]";
                        continue;
                    }

                    Sanitize(jsonObject[key]);
                }
                break;
            case JsonArray jsonArray:
                var originalCount = jsonArray.Count;
                foreach (var item in jsonArray.Take(MaxArrayItems).ToArray())
                {
                    Sanitize(item);
                }

                while (jsonArray.Count > MaxArrayItems)
                {
                    jsonArray.RemoveAt(jsonArray.Count - 1);
                }

                if (originalCount > MaxArrayItems)
                {
                    jsonArray.Add(new JsonObject
                    {
                        ["truncated"] = true,
                        ["omitted_count"] = originalCount - MaxArrayItems
                    });
                }
                break;
        }
    }

    private static bool IsSecretKey(string key)
    {
        var normalized = key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return SecretKeys.Any(secretKey =>
            string.Equals(normalized, secretKey, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(secretKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPathKey(string key)
    {
        var normalized = key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized == "path"
            || normalized.EndsWith("_path", StringComparison.Ordinal)
            || normalized.EndsWith("_paths", StringComparison.Ordinal)
            || normalized == "protected_paths"
            || normalized == "input_files";
    }

    private static BridgeResponseModel Ok(object data)
    {
        return new BridgeResponseModel(
            Status: "available",
            DataSource: "readonly_bridge",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: data,
            Warnings: []);
    }

    private static BridgeResponseModel Error(string status, string warning)
    {
        return new BridgeResponseModel(
            Status: status,
            DataSource: "unavailable",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: null,
            Warnings: [warning]);
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        BridgeResponseModel model,
        HttpStatusCode statusCode)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(response.OutputStream, model, JsonDefaults.WriteOptions);
        response.Close();
    }

    private static void ApplyHeaders(HttpListenerResponse response, HttpListenerRequest request)
    {
        var origin = request.Headers["Origin"];
        response.Headers["Access-Control-Allow-Origin"] =
            IsLocalOrigin(origin) ? origin! : "http://127.0.0.1:5173";
        response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        response.Headers["Cache-Control"] = "no-store";
    }

    private static bool IsLocalOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        return origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePrefix(string urlPrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(urlPrefix)
            ? "http://127.0.0.1:8787/"
            : urlPrefix.Trim();

        return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    private static string DisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record ReportDefinition(
        string Key,
        string Label,
        string Endpoint,
        string RelativePath);

    private sealed record ReportReadResult(
        bool Available,
        object? Data,
        IReadOnlyList<string> Warnings);
}
