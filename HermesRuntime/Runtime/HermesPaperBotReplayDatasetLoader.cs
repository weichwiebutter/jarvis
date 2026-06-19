namespace Hermes.Runtime;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Loads historical replay datasets defensively from CSV or JSON.
/// </summary>
public sealed record HermesPaperBotReplayDatasetLoadResult(
    bool Success,
    string Status,
    string Reason,
    string DatasetPath,
    int BarsTotal,
    int BarsValid,
    int BarsSkipped,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ReplayBar> Bars);

/// <summary>
/// Defensive replay dataset loader.
/// </summary>
public sealed class HermesPaperBotReplayDatasetLoader
{
    /// <summary>
    /// Finds candidate datasets for the requested asset and timeframe.
    /// </summary>
    public IReadOnlyList<string> Discover(string? asset, string? timeframe)
    {
        if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(timeframe))
        {
            return [];
        }

        var normalizedAsset = asset.Trim().ToUpperInvariant();
        var normalizedTimeframe = timeframe.Trim().ToUpperInvariant();
        var roots = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "replay_datasets"),
            Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "replay_datasets"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "market_data"),
            Path.Combine(Directory.GetCurrentDirectory(), "data"),
        };

        var candidates = new List<string>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => IsSupportedFile(path))
                .Where(path => Path.GetFileName(path).Contains(normalizedAsset, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(path).Contains(normalizedTimeframe, StringComparison.OrdinalIgnoreCase));
            candidates.AddRange(files);
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => new FileInfo(path).Length)
            .ThenByDescending(path => File.GetLastWriteTimeUtc(path))
            .ToArray();
    }

    /// <summary>
    /// Loads bars from CSV or JSON.
    /// </summary>
    public HermesPaperBotReplayDatasetLoadResult Load(string? datasetPath)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return Failed(string.Empty, "dataset_path_missing", []);
        }

        if (!File.Exists(datasetPath))
        {
            return Failed(datasetPath, "dataset_file_missing", ["dataset_file_missing"]);
        }

        var extension = Path.GetExtension(datasetPath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => LoadCsv(datasetPath),
            ".json" => LoadJson(datasetPath),
            _ => Failed(datasetPath, "dataset_format_unsupported", ["dataset_format_unsupported"]),
        };
    }

    private static HermesPaperBotReplayDatasetLoadResult LoadCsv(string datasetPath)
    {
        var warnings = new List<string>();
        var bars = new List<ReplayBar>();
        var lines = File.ReadAllLines(datasetPath);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index]?.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (index == 0 && line.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columns = line.Split(',');
            if (columns.Length < 6)
            {
                warnings.Add($"csv_row_invalid:{index + 1}");
                continue;
            }

            if (!TryParseBar(columns, out var bar))
            {
                warnings.Add($"csv_row_invalid:{index + 1}");
                continue;
            }

            bars.Add(bar);
        }

        return new HermesPaperBotReplayDatasetLoadResult(
            Success: bars.Count > 0,
            Status: bars.Count > 0 ? "loaded" : "blocked",
            Reason: bars.Count > 0 ? "dataset_loaded" : "dataset_no_valid_bars",
            DatasetPath: datasetPath,
            BarsTotal: lines.Length,
            BarsValid: bars.Count,
            BarsSkipped: Math.Max(lines.Length - bars.Count, 0),
            Warnings: warnings,
            Bars: bars);
    }

    private static HermesPaperBotReplayDatasetLoadResult LoadJson(string datasetPath)
    {
        var warnings = new List<string>();
        var bars = new List<ReplayBar>();

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(datasetPath));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Failed(datasetPath, "dataset_json_not_array", ["dataset_json_not_array"]);
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!TryParseBar(item, out var bar))
                {
                    warnings.Add("json_row_invalid");
                    continue;
                }

                bars.Add(bar);
            }
        }
        catch (Exception ex)
        {
            return Failed(datasetPath, $"dataset_json_parse_failed:{ex.GetType().Name}", [$"dataset_json_parse_failed:{ex.GetType().Name}"]);
        }

        return new HermesPaperBotReplayDatasetLoadResult(
            Success: bars.Count > 0,
            Status: bars.Count > 0 ? "loaded" : "blocked",
            Reason: bars.Count > 0 ? "dataset_loaded" : "dataset_no_valid_bars",
            DatasetPath: datasetPath,
            BarsTotal: warnings.Count + bars.Count,
            BarsValid: bars.Count,
            BarsSkipped: warnings.Count,
            Warnings: warnings,
            Bars: bars);
    }

    private static bool TryParseBar(string[] columns, out ReplayBar bar)
    {
        bar = new ReplayBar();
        if (!DateTimeOffset.TryParse(columns[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return false;
        }

        if (!TryParseDecimal(columns[1], out var open) ||
            !TryParseDecimal(columns[2], out var high) ||
            !TryParseDecimal(columns[3], out var low) ||
            !TryParseDecimal(columns[4], out var close) ||
            !TryParseDecimal(columns[5], out var spread))
        {
            return false;
        }

        bar = new ReplayBar
        {
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Spread = spread,
        };
        return true;
    }

    private static bool TryParseBar(JsonElement item, out ReplayBar bar)
    {
        bar = new ReplayBar();
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetString(item, "timestamp", out var timestampText) ||
            !DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return false;
        }

        if (!TryGetDecimal(item, "open", out var open) ||
            !TryGetDecimal(item, "high", out var high) ||
            !TryGetDecimal(item, "low", out var low) ||
            !TryGetDecimal(item, "close", out var close) ||
            !TryGetDecimal(item, "spread", out var spread))
        {
            return false;
        }

        bar = new ReplayBar
        {
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Spread = spread,
        };
        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryGetString(JsonElement item, string name, out string value)
    {
        if (item.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDecimal(JsonElement item, string name, out decimal value)
    {
        if (item.TryGetProperty(name, out var element))
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0m;
        return false;
    }

    private static HermesPaperBotReplayDatasetLoadResult Failed(string datasetPath, string reason, string[] warnings)
        => new(false, "blocked", reason, datasetPath, 0, 0, 0, warnings, []);

    private static bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".csv" or ".json";
    }
}
