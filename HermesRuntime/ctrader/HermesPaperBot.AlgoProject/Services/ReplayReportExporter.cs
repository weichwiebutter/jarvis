namespace HermesPaperBot.Services;

using System;
using System.IO;
using System.Text.Json;
using Hermes.Runtime;
using HermesPaperBot.Models;

/// <summary>
/// Exports replay results as JSON and Markdown.
/// </summary>
public sealed class ReplayReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Writes replay report files to the target directory.
    /// </summary>
    public ReplayReportExportResult Export(
        CloudEmbeddedReleasePackage? package,
        ReplayRunResult result,
        string outputDirectory,
        HermesPaperBotReplayDatasetLoadResult? dataset = null,
        bool datasetDiscoveryUsed = false,
        int datasetDiscoveryCandidates = 0,
        string? selectedDatasetPath = null)
    {
        if (result is null || string.IsNullOrWhiteSpace(outputDirectory))
        {
            return new ReplayReportExportResult
            {
                Success = false,
                BrokerAction = "none",
                Warnings = ["replay_export_invalid_inputs"],
            };
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);

            var jsonPath = Path.Combine(outputDirectory, "replay_report.json");
            var markdownPath = Path.Combine(outputDirectory, "replay_report.md");

            var payload = BuildPayload(package, result, dataset, datasetDiscoveryUsed, datasetDiscoveryCandidates, selectedDatasetPath);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, JsonOptions));
            File.WriteAllText(markdownPath, BuildMarkdown(payload));

            return new ReplayReportExportResult
            {
                Success = true,
                ReportDirectory = outputDirectory,
                JsonPath = jsonPath,
                MarkdownPath = markdownPath,
                BrokerAction = "none",
            };
        }
        catch (Exception ex)
        {
            return new ReplayReportExportResult
            {
                Success = false,
                ReportDirectory = outputDirectory,
                BrokerAction = "none",
                Warnings = [$"replay_export_failed:{ex.GetType().Name}"],
            };
        }
    }

    private static object BuildPayload(
        CloudEmbeddedReleasePackage? package,
        ReplayRunResult result,
        HermesPaperBotReplayDatasetLoadResult? dataset,
        bool datasetDiscoveryUsed,
        int datasetDiscoveryCandidates,
        string? selectedDatasetPath) =>
        new
        {
            bot_version = package?.BotVersion ?? string.Empty,
            bot_release_id = package?.BotReleaseId ?? string.Empty,
            strategy_package_version = package?.StrategyPackageVersion ?? string.Empty,
            embedded_checksum = package?.EmbeddedChecksum ?? string.Empty,
            dataset_path = dataset?.DatasetPath ?? string.Empty,
            dataset_discovery_used = datasetDiscoveryUsed,
            dataset_discovery_candidates = datasetDiscoveryCandidates,
            selected_dataset_path = selectedDatasetPath ?? dataset?.DatasetPath ?? string.Empty,
            bars_total = dataset?.BarsTotal ?? 0,
            bars_valid = dataset?.BarsValid ?? 0,
            bars_skipped = dataset?.BarsSkipped ?? 0,
            dataset_warnings = dataset?.Warnings ?? [],
            trades_total = result.Statistics.TradesTotal,
            wins = result.Statistics.Wins,
            losses = result.Statistics.Losses,
            win_rate = result.Statistics.WinRate,
            profit_factor = result.Statistics.ProfitFactor,
            expectancy_r = result.Statistics.ExpectancyR,
            average_r = result.Statistics.AverageR,
            max_drawdown_r = result.Statistics.MaxDrawdownR,
            sample_size_class = result.Statistics.SampleSizeClass,
            quality_class = result.Statistics.QualityClass,
            is_statistically_meaningful = result.Statistics.IsStatisticallyMeaningful,
            warnings = result.Statistics.Warnings,
            broker_action = result.BrokerAction,
            orders_enabled = false,
        };

    private static string BuildMarkdown(object payload)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOptions));
        var root = document.RootElement;

        return string.Join(Environment.NewLine, new[]
        {
            "# HermesPaperBot Replay Report V1",
            string.Empty,
            $"- bot_version: `{GetString(root, "bot_version")}`",
            $"- bot_release_id: `{GetString(root, "bot_release_id")}`",
            $"- strategy_package_version: `{GetString(root, "strategy_package_version")}`",
            $"- embedded_checksum: `{GetString(root, "embedded_checksum")}`",
            $"- dataset_path: `{GetString(root, "dataset_path")}`",
            $"- dataset_discovery_used: `{GetBool(root, "dataset_discovery_used")}`",
            $"- dataset_discovery_candidates: `{GetNumber(root, "dataset_discovery_candidates")}`",
            $"- selected_dataset_path: `{GetString(root, "selected_dataset_path")}`",
            $"- bars_total: `{GetNumber(root, "bars_total")}`",
            $"- bars_valid: `{GetNumber(root, "bars_valid")}`",
            $"- bars_skipped: `{GetNumber(root, "bars_skipped")}`",
            $"- trades_total: `{GetNumber(root, "trades_total")}`",
            $"- wins: `{GetNumber(root, "wins")}`",
            $"- losses: `{GetNumber(root, "losses")}`",
            $"- win_rate: `{GetNumber(root, "win_rate")}`",
            $"- profit_factor: `{GetNumber(root, "profit_factor")}`",
            $"- expectancy_r: `{GetNumber(root, "expectancy_r")}`",
            $"- average_r: `{GetNumber(root, "average_r")}`",
            $"- max_drawdown_r: `{GetNumber(root, "max_drawdown_r")}`",
            $"- sample_size_class: `{GetString(root, "sample_size_class")}`",
            $"- quality_class: `{GetString(root, "quality_class")}`",
            $"- is_statistically_meaningful: `{GetBool(root, "is_statistically_meaningful")}`",
            $"- broker_action: `{GetString(root, "broker_action")}`",
            $"- orders_enabled: `{GetBool(root, "orders_enabled")}`",
            string.Empty,
            "## Warnings",
            RenderWarnings(root),
            string.Empty,
            "## Dataset Warnings",
            RenderDatasetWarnings(root),
        });
    }

    private static string RenderWarnings(JsonElement root)
    {
        if (!root.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() == 0)
        {
            return "- none";
        }

        var lines = new string[warnings.GetArrayLength()];
        var index = 0;
        foreach (var warning in warnings.EnumerateArray())
        {
            lines[index++] = $" - {warning.GetString()}";
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderDatasetWarnings(JsonElement root)
    {
        if (!root.TryGetProperty("dataset_warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() == 0)
        {
            return "- none";
        }

        var lines = new string[warnings.GetArrayLength()];
        var index = 0;
        foreach (var warning in warnings.EnumerateArray())
        {
            lines[index++] = $" - {warning.GetString()}";
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;

    private static string GetNumber(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) ? element.ToString() : string.Empty;

    private static string GetBool(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) ? element.ToString().ToLowerInvariant() : "false";
}
