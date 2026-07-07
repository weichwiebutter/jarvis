using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PaperBotRuntimeSelfCheckReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    bool EmbeddedReleasePackagePresent,
    bool EmbeddedReleasePackageParseable,
    bool SignalPackagePresent,
    bool SignalPackageLoaded,
    bool ChartAnnotationSpecPresent,
    bool ChartAnnotationSpecLoaded,
    bool SafetyFlagsActive,
    bool CloudMode,
    bool BrokerActionNone,
    bool RuntimeReady,
    string? BotReleaseId,
    string? BotVersion,
    string? StrategyPackageVersion,
    string? ReleaseMode,
    string? EmbeddedChecksum,
    string EmbeddedReleasePackagePath,
    string EmbeddedSourcePath,
    string SignalReaderPath,
    string ChartAnnotationReaderPath,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperBotRuntimeSelfCheckService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperBotRuntimeSelfCheckService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "startup_runtime_self_check");
    public string ReportPath => Path.Combine(Root, "startup_runtime_self_check.json");
    public string MarkdownPath => Path.Combine(Root, "startup_runtime_self_check.md");

    public PaperBotRuntimeSelfCheckReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperBotRuntimeSelfCheckReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    public PaperBotRuntimeSelfCheckReport Run()
    {
        Directory.CreateDirectory(Root);

        var generator = new CloudEmbeddedReleasePackageGeneratorService(_storagePaths, _runtimeRoot);
        var embeddedPackagePath = generator.OutputJsonPath;
        var embeddedSourcePath = generator.OutputSourcePath;
        var signalReaderPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Services", "SignalPackageReader.cs");
        var chartAnnotationReaderPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Services", "EmbeddedChartAnnotationSpecReader.cs");

        var embeddedReleasePackagePresent = File.Exists(embeddedPackagePath);
        var embeddedReleasePackageParseable = TryLoadEmbeddedPackage(embeddedPackagePath, out var package);
        var embeddedSourceExists = File.Exists(embeddedSourcePath);
        var signalReaderExists = File.Exists(signalReaderPath);
        var chartAnnotationReaderExists = File.Exists(chartAnnotationReaderPath);

        var signalPackagePresent = embeddedReleasePackageParseable && !string.IsNullOrWhiteSpace(package?.EmbeddedStrategyJson);
        var signalPackageLoaded = signalPackagePresent && TryParseJson(package?.EmbeddedStrategyJson);
        var chartAnnotationSpecPresent = embeddedReleasePackageParseable && !string.IsNullOrWhiteSpace(package?.ChartAnnotationSpecJson);
        var chartAnnotationSpecLoaded = chartAnnotationSpecPresent && TryParseJson(package?.ChartAnnotationSpecJson);

        var safetyFlagsActive = embeddedReleasePackageParseable
            && package is not null
            && string.Equals(package.ReleaseMode, "paper_only", StringComparison.OrdinalIgnoreCase)
            && package.SafetyFlags.NoAutoTrading
            && package.SafetyFlags.HumanReviewRequired
            && !package.SafetyFlags.BrokerOrdersEnabled
            && !package.SafetyFlags.LiveTradingEnabled
            && !package.SafetyFlags.OrderApiEnabled
            && package.SafetyFlags.PaperMode
            && string.Equals(package.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase);

        var cloudMode = embeddedReleasePackageParseable
            && package is not null
            && string.Equals(package.ReleaseMode, "paper_only", StringComparison.OrdinalIgnoreCase)
            && safetyFlagsActive
            && signalPackageLoaded
            && chartAnnotationSpecLoaded;

        var brokerActionNone = embeddedReleasePackageParseable
            && package is not null
            && string.Equals(package.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase);

        var runtimeReady = embeddedReleasePackagePresent
            && embeddedReleasePackageParseable
            && embeddedSourceExists
            && signalReaderExists
            && chartAnnotationReaderExists
            && signalPackageLoaded
            && chartAnnotationSpecLoaded
            && safetyFlagsActive
            && cloudMode
            && brokerActionNone;

        var warnings = new List<string>();
        if (!embeddedReleasePackagePresent) warnings.Add("embedded_release_package_missing");
        if (embeddedReleasePackagePresent && !embeddedReleasePackageParseable) warnings.Add("embedded_release_package_parse_failed");
        if (!signalPackagePresent) warnings.Add("signal_package_missing");
        if (signalPackagePresent && !signalPackageLoaded) warnings.Add("signal_package_parse_failed");
        if (!chartAnnotationSpecPresent) warnings.Add("chart_annotation_spec_missing");
        if (chartAnnotationSpecPresent && !chartAnnotationSpecLoaded) warnings.Add("chart_annotation_spec_parse_failed");
        if (!safetyFlagsActive) warnings.Add("safety_flags_not_active");
        if (!cloudMode) warnings.Add("cloud_mode_not_confirmed");
        if (!brokerActionNone) warnings.Add("broker_action_not_none");
        if (!embeddedSourceExists) warnings.Add("embedded_release_source_missing");
        if (!signalReaderExists) warnings.Add("signal_package_reader_missing");
        if (!chartAnnotationReaderExists) warnings.Add("chart_annotation_reader_missing");

        var recommendations = new List<string>();
        if (!runtimeReady)
        {
            recommendations.Add("ensure the cloud embedded release package includes embedded_strategy_json, chart_annotation_spec_json, and safety flags");
            recommendations.Add("regenerate the embedded package and verify the generated cTrader source before startup");
        }

        var report = new PaperBotRuntimeSelfCheckReport(
            ReportVersion: "paperbot_runtime_self_check_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: runtimeReady ? "ready" : "not_ready",
            EmbeddedReleasePackagePresent: embeddedReleasePackagePresent,
            EmbeddedReleasePackageParseable: embeddedReleasePackageParseable,
            SignalPackagePresent: signalPackagePresent,
            SignalPackageLoaded: signalPackageLoaded,
            ChartAnnotationSpecPresent: chartAnnotationSpecPresent,
            ChartAnnotationSpecLoaded: chartAnnotationSpecLoaded,
            SafetyFlagsActive: safetyFlagsActive,
            CloudMode: cloudMode,
            BrokerActionNone: brokerActionNone,
            RuntimeReady: runtimeReady,
            BotReleaseId: package?.BotReleaseId,
            BotVersion: package?.BotVersion,
            StrategyPackageVersion: package?.StrategyPackageVersion,
            ReleaseMode: package?.ReleaseMode,
            EmbeddedChecksum: package?.EmbeddedChecksum,
            EmbeddedReleasePackagePath: embeddedPackagePath,
            EmbeddedSourcePath: embeddedSourcePath,
            SignalReaderPath: signalReaderPath,
            ChartAnnotationReaderPath: chartAnnotationReaderPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static bool TryLoadEmbeddedPackage(string path, out EmbeddedPackageSnapshot? package)
    {
        package = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            package = new EmbeddedPackageSnapshot(
                BotReleaseId: ReadString(document.RootElement, "bot_release_id"),
                BotVersion: ReadString(document.RootElement, "bot_version"),
                StrategyPackageVersion: ReadString(document.RootElement, "strategy_package_version"),
                ReleaseMode: ReadString(document.RootElement, "release_mode"),
                EmbeddedChecksum: ReadString(document.RootElement, "embedded_checksum"),
                SafetyFlags: ReadSafetyFlags(document.RootElement),
                EmbeddedStrategyJson: ReadString(document.RootElement, "embedded_strategy_json"),
                ChartAnnotationSpecJson: ReadString(document.RootElement, "chart_annotation_spec_json"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static EmbeddedSafetyFlags ReadSafetyFlags(JsonElement root)
    {
        var flags = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("safety_flags", out var safetyFlags) && safetyFlags.ValueKind == JsonValueKind.Object
            ? safetyFlags
            : default;

        return new EmbeddedSafetyFlags(
            NoAutoTrading: ReadBool(flags, "no_auto_trading"),
            HumanReviewRequired: ReadBool(flags, "human_review_required"),
            BrokerOrdersEnabled: ReadBool(flags, "broker_orders_enabled"),
            LiveTradingEnabled: ReadBool(flags, "live_trading_enabled"),
            OrderApiEnabled: ReadBool(flags, "order_api_enabled"),
            PaperMode: ReadBool(flags, "paper_mode"),
            BrokerAction: ReadString(flags, "broker_action") ?? string.Empty);
    }

    private static bool ReadBool(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : false;

    private static bool TryParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record EmbeddedPackageSnapshot(
        string? BotReleaseId,
        string? BotVersion,
        string? StrategyPackageVersion,
        string? ReleaseMode,
        string? EmbeddedChecksum,
        EmbeddedSafetyFlags SafetyFlags,
        string? EmbeddedStrategyJson,
        string? ChartAnnotationSpecJson);

    private sealed record EmbeddedSafetyFlags(
        bool NoAutoTrading,
        bool HumanReviewRequired,
        bool BrokerOrdersEnabled,
        bool LiveTradingEnabled,
        bool OrderApiEnabled,
        bool PaperMode,
        string BrokerAction);

    private static string BuildMarkdown(PaperBotRuntimeSelfCheckReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PaperBot Runtime Self Check");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- runtime_ready: {report.RuntimeReady.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Checks");
        sb.AppendLine($"- embedded_release_package_present: {report.EmbeddedReleasePackagePresent.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- embedded_release_package_parseable: {report.EmbeddedReleasePackageParseable.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- signal_package_present: {report.SignalPackagePresent.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- signal_package_loaded: {report.SignalPackageLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- chart_annotation_spec_present: {report.ChartAnnotationSpecPresent.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- chart_annotation_spec_loaded: {report.ChartAnnotationSpecLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- safety_flags_active: {report.SafetyFlagsActive.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- cloud_mode: {report.CloudMode.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- broker_action_none: {report.BrokerActionNone.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Package");
        sb.AppendLine($"- bot_release_id: {report.BotReleaseId ?? string.Empty}");
        sb.AppendLine($"- bot_version: {report.BotVersion ?? string.Empty}");
        sb.AppendLine($"- strategy_package_version: {report.StrategyPackageVersion ?? string.Empty}");
        sb.AppendLine($"- release_mode: {report.ReleaseMode ?? string.Empty}");
        sb.AppendLine($"- embedded_checksum: {report.EmbeddedChecksum ?? string.Empty}");
        sb.AppendLine();
        sb.AppendLine("## Paths");
        sb.AppendLine($"- embedded_release_package: {report.EmbeddedReleasePackagePath}");
        sb.AppendLine($"- embedded_source: {report.EmbeddedSourcePath}");
        sb.AppendLine($"- signal_reader: {report.SignalReaderPath}");
        sb.AppendLine($"- chart_annotation_reader: {report.ChartAnnotationReaderPath}");
        sb.AppendLine();
        sb.AppendLine("## Warnings");
        foreach (var warning in report.Warnings)
        {
            sb.AppendLine($"- {warning}");
        }
        if (report.Warnings.Count == 0)
        {
            sb.AppendLine("- none");
        }
        sb.AppendLine();
        sb.AppendLine("## Recommendations");
        foreach (var recommendation in report.Recommendations)
        {
            sb.AppendLine($"- {recommendation}");
        }
        if (report.Recommendations.Count == 0)
        {
            sb.AppendLine("- none");
        }

        return sb.ToString();
    }
}
