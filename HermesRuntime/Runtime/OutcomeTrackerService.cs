using System.Text.Json;

namespace Hermes.Runtime;

public sealed class OutcomeTrackerService
{
    private const string OutcomeSource = "hermes_outcome_tracker";

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public OutcomeTrackerService(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public (string ReportPath, int OutcomeCount) EvaluateDemoOutcomes()
    {
        var signalPath = FindLatestSignalExportPath();
        var startedAtUtc = DateTimeOffset.UtcNow;
        PublishOutcomeEvaluationStarted(signalPath, startedAtUtc);

        var signalRows = ReadSignalRows(signalPath);
        var outcomes = CreateDemoOutcomes(signalRows, startedAtUtc);

        var outputDirectory = Path.Combine(_storagePaths.Root, "reports", "outcomes");
        Directory.CreateDirectory(outputDirectory);

        var reportPath = Path.Combine(
            outputDirectory,
            $"outcomes_demo_{startedAtUtc:yyyyMMddHHmmssfff}.outcomes.json");

        File.WriteAllText(reportPath, JsonSerializer.Serialize(outcomes, JsonDefaults.WriteOptions));
        PublishOutcomeEvaluationCompleted(reportPath, signalPath, outcomes.Count);

        return (reportPath, outcomes.Count);
    }

    private string? FindLatestSignalExportPath()
    {
        var signalDirectory = Path.Combine(_storagePaths.Root, "exports", "signals");
        if (!Directory.Exists(signalDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(signalDirectory, "*.signals.jsonl")
            .OrderBy(File.GetLastWriteTimeUtc)
            .ThenBy(path => path)
            .LastOrDefault();
    }

    private static IReadOnlyList<SignalRow> ReadSignalRows(string? signalPath)
    {
        if (string.IsNullOrWhiteSpace(signalPath) || !File.Exists(signalPath))
        {
            return CreateFallbackSignalRows();
        }

        var rows = new List<SignalRow>();
        foreach (var line in File.ReadLines(signalPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                rows.Add(new SignalRow(
                    TimestampUtc: ReadDateTimeOffset(root, "timestamp_utc", "timestampUtc"),
                    Symbol: ReadString(root, "symbol") ?? "UNKNOWN",
                    Timeframe: ReadString(root, "timeframe") ?? "M15",
                    Direction: ReadString(root, "direction") ?? "neutral"));
            }
            catch (JsonException)
            {
                // Outcome Tracking v1 is a demo evaluator; malformed signal rows are ignored.
            }
        }

        return rows.Count > 0 ? rows : CreateFallbackSignalRows();
    }

    private static IReadOnlyList<SignalRow> CreateFallbackSignalRows()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new SignalRow(now.AddMinutes(-15), "XAUUSD", "M15", "long"),
            new SignalRow(now.AddMinutes(-10), "EURUSD", "M15", "neutral"),
            new SignalRow(now.AddMinutes(-5), "GER40", "M15", "short_watch")
        ];
    }

    private static IReadOnlyList<OutcomeEvaluation> CreateDemoOutcomes(
        IReadOnlyList<SignalRow> signalRows,
        DateTimeOffset evaluatedAtUtc)
    {
        return signalRows
            .Select(row => CreateDemoOutcome(row, evaluatedAtUtc))
            .ToList();
    }

    private static OutcomeEvaluation CreateDemoOutcome(SignalRow row, DateTimeOffset evaluatedAtUtc)
    {
        return row.Symbol.ToUpperInvariant() switch
        {
            "XAUUSD" => BuildOutcome(
                row,
                evaluatedAtUtc,
                outcomeStatus: "tp_hit",
                hitTarget: true,
                hitStop: false,
                expired: false,
                invalidated: false,
                mfe: 1.45,
                mae: -0.22,
                finalR: 1.0,
                notes: "Demo outcome: theoretical target was marked as hit. No order existed."),
            "EURUSD" => BuildOutcome(
                row,
                evaluatedAtUtc,
                outcomeStatus: "expired",
                hitTarget: false,
                hitStop: false,
                expired: true,
                invalidated: false,
                mfe: 0.18,
                mae: -0.16,
                finalR: 0.0,
                notes: "Demo outcome: setup expired without theoretical target or stop."),
            "GER40" => BuildOutcome(
                row,
                evaluatedAtUtc,
                outcomeStatus: "partial",
                hitTarget: false,
                hitStop: false,
                expired: false,
                invalidated: false,
                mfe: 0.62,
                mae: -0.31,
                finalR: 0.35,
                notes: "Demo outcome: partial favorable movement, no live trade or execution."),
            _ => BuildOutcome(
                row,
                evaluatedAtUtc,
                outcomeStatus: "invalidated",
                hitTarget: false,
                hitStop: false,
                expired: false,
                invalidated: true,
                mfe: 0.0,
                mae: 0.0,
                finalR: 0.0,
                notes: "Demo outcome fallback: signal invalidated for review.")
        };
    }

    private static OutcomeEvaluation BuildOutcome(
        SignalRow row,
        DateTimeOffset evaluatedAtUtc,
        string outcomeStatus,
        bool hitTarget,
        bool hitStop,
        bool expired,
        bool invalidated,
        double mfe,
        double mae,
        double finalR,
        string notes)
    {
        var signalId = $"{row.Symbol}_{row.TimestampUtc:yyyyMMddHHmmss}";
        var outcomeId = $"outcome_{signalId}_{outcomeStatus}";

        return new OutcomeEvaluation(
            OutcomeId: outcomeId,
            SignalId: signalId,
            Symbol: row.Symbol,
            Timeframe: row.Timeframe,
            Direction: row.Direction,
            OutcomeStatus: outcomeStatus,
            HitTarget: hitTarget,
            HitStop: hitStop,
            Expired: expired,
            Invalidated: invalidated,
            Mfe: mfe,
            Mae: mae,
            FinalR: finalR,
            EvaluatedAtUtc: evaluatedAtUtc,
            Notes: notes);
    }

    private void PublishOutcomeEvaluationStarted(string? signalPath, DateTimeOffset startedAtUtc)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.OutcomeEvaluationStarted,
            OutcomeSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Signal outcome evaluation started. Demo-only, no trading execution.",
                startedAtUtc,
                signalPath,
                reportDirectory = Path.Combine(_storagePaths.Root, "reports", "outcomes"),
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishOutcomeEvaluationCompleted(
        string reportPath,
        string? signalPath,
        int outcomeCount)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.OutcomeEvaluationCompleted,
            OutcomeSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Signal outcome evaluation completed. Results are local learning candidates only.",
                reportPath,
                signalPath,
                outcomeCount,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(JsonElement root, params string[] names)
    {
        var value = ReadString(root, names);
        return DateTimeOffset.TryParse(value, out var timestamp)
            ? timestamp
            : DateTimeOffset.UtcNow;
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private sealed record SignalRow(
        DateTimeOffset TimestampUtc,
        string Symbol,
        string Timeframe,
        string Direction);
}
