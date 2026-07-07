using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace Hermes.Runtime;

public sealed record PaperSignalSkipDiagnosticItem(
    string SignalId,
    string Asset,
    string Timeframe,
    string SetupId,
    string SetupName,
    string Direction,
    bool EntryPresent,
    bool DirectionPresent,
    bool StopLossPresent,
    bool TakeProfitPresent,
    bool PaperEntryEnabledPresent,
    bool PaperEntryEnabledDefaulted,
    bool PaperEntryEnabledValue,
    string? DefaultReason,
    string ReleaseMode,
    string SafetyFlagsSummary,
    string MappingStatus,
    string SkipReason,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Warnings);

public sealed record PaperSignalSkipDiagnosticsReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int EmbeddedSignalCount,
    int SkippedSignalCount,
    int PaperEntryDisabledCount,
    IReadOnlyList<PaperSignalSkipDiagnosticItem> Signals,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperSignalSkipDiagnosticsService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperSignalSkipDiagnosticsService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_signal_skip_diagnostics");
    public string ReportPath => Path.Combine(Root, "paper_signal_skip_diagnostics.json");
    public string MarkdownPath => Path.Combine(Root, "paper_signal_skip_diagnostics.md");

    public PaperSignalSkipDiagnosticsReport Run()
    {
        Directory.CreateDirectory(Root);

        var warnings = new List<string>();
        var recommendations = new List<string>();
        var embeddedPackagePath = Path.Combine(_storagePaths.Root, "reports", "cloud_embedded_release_package", "cloud_embedded_release_package.json");
        var packageJson = ReadEmbeddedPackageJson(embeddedPackagePath, warnings);
        if (packageJson is null)
        {
            recommendations.Add("regenerate the cloud embedded release package before diagnosing signal skips");
            var empty = BuildReport(Array.Empty<PaperSignalSkipDiagnosticItem>(), warnings, recommendations, "embedded_package_missing");
            WriteReport(empty);
            return empty;
        }

        var package = TryParsePackage(packageJson, warnings);
        if (package is null)
        {
            recommendations.Add("ensure the embedded package JSON is parseable and contains embedded_strategy_json");
            var empty = BuildReport(Array.Empty<PaperSignalSkipDiagnosticItem>(), warnings, recommendations, "embedded_package_unparseable");
            WriteReport(empty);
            return empty;
        }

        var engine = new PaperDecisionEngine();
        var candidates = engine.ParseSignalCandidates(package, out var parseWarnings);
        warnings.AddRange(parseWarnings);

        var embeddedSignals = ExtractEmbeddedSignals(package, warnings);
        var items = new List<PaperSignalSkipDiagnosticItem>();
        var paperEntryDisabledCount = 0;

        foreach (var candidate in candidates)
        {
            var embeddedSignal = embeddedSignals.FirstOrDefault(signal =>
                string.Equals(signal.SignalId, candidate.SignalId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(signal.SetupId, candidate.SetupId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(signal.Asset, candidate.Asset, StringComparison.OrdinalIgnoreCase));

            var missingFields = new List<string>();
            var paperEntryEnabledDefaulted = false;
            string? defaultReason = null;
            if (embeddedSignal is null)
            {
                missingFields.Add("embedded_signal_missing");
            }
            else
            {
                if (!embeddedSignal.EntryPresent) missingFields.Add("entry_missing");
                if (!embeddedSignal.DirectionPresent) missingFields.Add("direction_missing");
                if (!embeddedSignal.StopLossPresent) missingFields.Add("stop_loss_missing");
                if (!embeddedSignal.TakeProfitPresent) missingFields.Add("take_profit_missing");
                if (!embeddedSignal.PaperEntryEnabledPresent)
                {
                    paperEntryEnabledDefaulted = embeddedSignal.PaperEntryEnabledValue;
                    defaultReason = paperEntryEnabledDefaulted ? "safe_paper_only_signal" : "unsafe_paper_only_signal";
                }
            }

            if (!candidate.PaperEntryEnabled)
            {
                paperEntryDisabledCount += 1;
                if (!paperEntryEnabledDefaulted && !missingFields.Contains("paper_entry_enabled_missing", StringComparer.OrdinalIgnoreCase))
                {
                    missingFields.Add("paper_entry_enabled_false");
                }
            }

            var entryPresent = embeddedSignal?.EntryPresent ?? false;
            var directionPresent = embeddedSignal?.DirectionPresent ?? false;
            var stopLossPresent = embeddedSignal?.StopLossPresent ?? false;
            var takeProfitPresent = embeddedSignal?.TakeProfitPresent ?? false;
            var paperEntryEnabledPresent = embeddedSignal?.PaperEntryEnabledPresent ?? false;
            var paperEntryEnabledValue = embeddedSignal?.PaperEntryEnabledValue ?? false;
            var releaseMode = package.ReleaseMode.ToString();
            var safetyFlagsSummary = BuildSafetySummary(package.SafetyFlags);
            var mappingStatus = embeddedSignal is null ? "signal_not_mapped" : "mapped";
            var skipReason = !candidate.PaperEntryEnabled ? "paper_entry_disabled" : "ok";

            items.Add(new PaperSignalSkipDiagnosticItem(
                SignalId: candidate.SignalId,
                Asset: candidate.Asset,
                Timeframe: candidate.Timeframe,
                SetupId: candidate.SetupId,
                SetupName: candidate.SetupName,
                Direction: candidate.Direction,
                EntryPresent: entryPresent,
                DirectionPresent: directionPresent,
                StopLossPresent: stopLossPresent,
                TakeProfitPresent: takeProfitPresent,
                PaperEntryEnabledPresent: paperEntryEnabledPresent,
                PaperEntryEnabledDefaulted: paperEntryEnabledDefaulted,
                PaperEntryEnabledValue: paperEntryEnabledValue,
                DefaultReason: defaultReason,
                ReleaseMode: releaseMode,
                SafetyFlagsSummary: safetyFlagsSummary,
                MappingStatus: mappingStatus,
                SkipReason: skipReason,
                MissingFields: missingFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings: candidate.ValidationWarnings.ToList()));
        }

        if (items.Count == 0)
        {
            warnings.Add("no_signal_candidates");
        }

        var report = BuildReport(
            items,
            warnings,
            recommendations,
            items.Count == 0 ? "no_signals" : "diagnosed");
        WriteReport(report);
        return report;
    }

    public PaperSignalSkipDiagnosticsReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperSignalSkipDiagnosticsReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    private PaperSignalSkipDiagnosticsReport BuildReport(
        IReadOnlyList<PaperSignalSkipDiagnosticItem> signals,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> recommendations,
        string status)
    {
        var disabledCount = signals.Count(signal => string.Equals(signal.SkipReason, "paper_entry_disabled", StringComparison.OrdinalIgnoreCase));
        return new PaperSignalSkipDiagnosticsReport(
            ReportVersion: "paper_signal_skip_diagnostics_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            EmbeddedSignalCount: signals.Count,
            SkippedSignalCount: disabledCount,
            PaperEntryDisabledCount: disabledCount,
            Signals: signals,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private static string? ReadEmbeddedPackageJson(string path, List<string> warnings)
    {
        if (!File.Exists(path))
        {
            warnings.Add("embedded_release_package_missing");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetRawText();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"embedded_release_package_read_failed:{ex.GetType().Name}");
            return null;
        }
    }

    private static CloudEmbeddedReleasePackage? TryParsePackage(string packageJson, List<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(packageJson);
            return new CloudEmbeddedReleasePackage
            {
                BotReleaseId = ReadString(document.RootElement, "bot_release_id"),
                BotVersion = ReadString(document.RootElement, "bot_version"),
                StrategyPackageVersion = ReadString(document.RootElement, "strategy_package_version"),
                SchemaVersion = ReadString(document.RootElement, "schema_version"),
                PackageJson = packageJson,
                EmbeddedManifestJson = ReadOptionalString(document.RootElement, "embedded_manifest_json"),
                EmbeddedStrategyJson = ReadOptionalString(document.RootElement, "embedded_strategy_json"),
                EmbeddedChecksum = ReadOptionalString(document.RootElement, "embedded_checksum"),
                ChartAnnotationSpecJson = ReadOptionalString(document.RootElement, "chart_annotation_spec_json"),
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"embedded_package_parse_failed:{ex.GetType().Name}");
            return null;
        }
    }

    private static IReadOnlyList<EmbeddedSignalSnapshot> ExtractEmbeddedSignals(CloudEmbeddedReleasePackage package, List<string> warnings)
    {
        var snapshots = new List<EmbeddedSignalSnapshot>();
        if (string.IsNullOrWhiteSpace(package.EmbeddedStrategyJson))
        {
            warnings.Add("embedded_strategy_json_missing");
            return snapshots;
        }

        try
        {
            using var document = JsonDocument.Parse(package.EmbeddedStrategyJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                warnings.Add("embedded_strategy_assets_missing");
                return snapshots;
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                snapshots.Add(new EmbeddedSignalSnapshot(
                    SignalId: BuildSignalId(package, ReadString(asset, "asset"), ReadString(asset, "setup_id")),
                    Asset: ReadString(asset, "asset"),
                    Timeframe: ReadString(asset, "timeframe"),
                    SetupId: ReadString(asset, "setup_id"),
                    SetupName: ReadString(asset, "setup_name"),
                    Direction: ReadString(asset, "direction"),
                    EntryPresent: HasText(asset, "entry_logic"),
                    DirectionPresent: HasString(asset, "direction"),
                    StopLossPresent: HasText(asset, "stop_loss_logic"),
                    TakeProfitPresent: HasText(asset, "take_profit_logic"),
                    PaperEntryEnabledPresent: HasBool(asset, "paper_entry_enabled"),
                    PaperEntryEnabledValue: DeterminePaperEntryEnabled(package, asset)));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"embedded_strategy_parse_failed:{ex.GetType().Name}");
        }

        return snapshots;
    }

    private static string BuildSignalId(CloudEmbeddedReleasePackage package, string asset, string setupId)
        => string.Join(':', [package.BotReleaseId, asset, string.IsNullOrWhiteSpace(setupId) ? "signal" : setupId]);

    private static bool HasString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString());

    private static bool HasBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False);

    private static bool HasText(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array && property.GetArrayLength() > 0;

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static string ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool DeterminePaperEntryEnabled(CloudEmbeddedReleasePackage package, JsonElement asset)
    {
        if (package.ReleaseMode != ReleaseMode.PaperOnly)
        {
            return false;
        }

        if (HasBool(asset, "paper_entry_enabled"))
        {
            return ReadBool(asset, "paper_entry_enabled");
        }

        if (!package.SafetyFlags.NoAutoTrading ||
            !package.SafetyFlags.HumanReviewRequired ||
            package.SafetyFlags.BrokerTradingEnabled ||
            package.SafetyFlags.LiveTradingEnabled ||
            package.SafetyFlags.OrderApiEnabled ||
            !package.SafetyFlags.PaperMode ||
            !string.Equals(package.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasText(asset, "entry_logic")
            && HasString(asset, "direction")
            && HasText(asset, "stop_loss_logic")
            && HasText(asset, "take_profit_logic");
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string BuildSafetySummary(SafetyFlags safety)
        => string.Join("; ", new[]
        {
            $"no_auto_trading={safety.NoAutoTrading.ToString().ToLowerInvariant()}",
            $"human_review_required={safety.HumanReviewRequired.ToString().ToLowerInvariant()}",
            $"broker_orders_enabled={safety.BrokerTradingEnabled.ToString().ToLowerInvariant()}",
            $"live_trading_enabled={safety.LiveTradingEnabled.ToString().ToLowerInvariant()}",
            $"order_api_enabled={safety.OrderApiEnabled.ToString().ToLowerInvariant()}",
            $"paper_mode={safety.PaperMode.ToString().ToLowerInvariant()}",
            $"broker_action={safety.BrokerAction}",
        });

    private void WriteReport(PaperSignalSkipDiagnosticsReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperSignalSkipDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Signal Skip Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- embedded_signal_count: {report.EmbeddedSignalCount}");
        sb.AppendLine($"- skipped_signal_count: {report.SkippedSignalCount}");
        sb.AppendLine($"- paper_entry_disabled_count: {report.PaperEntryDisabledCount}");
        sb.AppendLine();
        foreach (var signal in report.Signals)
        {
            sb.AppendLine($"## {signal.SignalId}");
            sb.AppendLine($"- asset: {signal.Asset}");
            sb.AppendLine($"- timeframe: {signal.Timeframe}");
            sb.AppendLine($"- setup_id: {signal.SetupId}");
            sb.AppendLine($"- setup_name: {signal.SetupName}");
            sb.AppendLine($"- direction: {signal.Direction}");
            sb.AppendLine($"- entry_present: {signal.EntryPresent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- direction_present: {signal.DirectionPresent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- stop_loss_present: {signal.StopLossPresent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- take_profit_present: {signal.TakeProfitPresent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- paper_entry_enabled_present: {signal.PaperEntryEnabledPresent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- paper_entry_enabled_defaulted: {signal.PaperEntryEnabledDefaulted.ToString().ToLowerInvariant()}");
            sb.AppendLine($"- paper_entry_enabled_value: {signal.PaperEntryEnabledValue.ToString().ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(signal.DefaultReason))
            {
                sb.AppendLine($"- default_reason: {signal.DefaultReason}");
            }
            sb.AppendLine($"- release_mode: {signal.ReleaseMode}");
            sb.AppendLine($"- mapping_status: {signal.MappingStatus}");
            sb.AppendLine($"- skip_reason: {signal.SkipReason}");
            sb.AppendLine($"- safety_flags: {signal.SafetyFlagsSummary}");
            sb.AppendLine($"- missing_fields: {string.Join(", ", signal.MissingFields.DefaultIfEmpty("none"))}");
            sb.AppendLine($"- warnings: {string.Join(", ", signal.Warnings.DefaultIfEmpty("none"))}");
        }

        if (report.Signals.Count == 0)
        {
            sb.AppendLine("- none");
        }

        return sb.ToString();
    }

    private sealed record EmbeddedSignalSnapshot(
        string SignalId,
        string Asset,
        string Timeframe,
        string SetupId,
        string SetupName,
        string Direction,
        bool EntryPresent,
        bool DirectionPresent,
        bool StopLossPresent,
        bool TakeProfitPresent,
        bool PaperEntryEnabledPresent,
        bool PaperEntryEnabledValue);
}
