using System.Text.Json;

namespace Hermes.Runtime;

public sealed class SignalGenerationStub
{
    private readonly StoragePaths _storagePaths;

    public SignalGenerationStub(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public SignalGenerationStubResult GenerateSignalsFromFeatures(string featureOutputPath, string researchJobId)
    {
        var warnings = new List<string>();
        var features = ReadFeatures(featureOutputPath);
        if (features.Count == 0)
        {
            warnings.Add("No generated FeatureVectors found; no signals were created.");
        }

        var signals = features
            .GroupBy(feature => new { feature.Symbol, feature.Timeframe })
            .Select(group => group.OrderByDescending(feature => feature.TimestampUtc).First())
            .Select(CreateSignal)
            .ToList();

        var outputDirectory = Path.Combine(_storagePaths.Root, "exports", "signals");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{researchJobId}.signals.jsonl");
        File.WriteAllLines(
            outputPath,
            signals.Select(signal => JsonSerializer.Serialize(signal, JsonDefaults.WriteOptions)));

        return new SignalGenerationStubResult(outputPath, signals.Count, warnings);
    }

    private static IReadOnlyList<GeneratedFeatureVector> ReadFeatures(string featureOutputPath)
    {
        if (!File.Exists(featureOutputPath))
        {
            return [];
        }

        var features = new List<GeneratedFeatureVector>();
        foreach (var line in File.ReadLines(featureOutputPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var feature = JsonSerializer.Deserialize<GeneratedFeatureVector>(line, JsonDefaults.SnapshotReadOptions);
                if (feature is not null)
                {
                    features.Add(feature);
                }
            }
            catch (JsonException)
            {
                // SignalGenerationStub v1 ignores malformed feature rows and keeps the nightly run local.
            }
        }

        return features;
    }

    private static SignalResult CreateSignal(GeneratedFeatureVector feature)
    {
        var direction = feature.Direction switch
        {
            "up" when feature.MockSignalScore >= 0.55 => "long_watch",
            "down" when feature.MockSignalScore >= 0.55 => "short_watch",
            _ => "neutral"
        };

        var riskUnit = Math.Max(feature.CandleRange, Math.Abs(feature.BodySize));
        if (riskUnit <= 0)
        {
            riskUnit = feature.Symbol.Equals("EURUSD", StringComparison.OrdinalIgnoreCase) ? 0.0005 : 1.0;
        }

        var stop = direction == "short_watch"
            ? feature.Close + riskUnit
            : feature.Close - riskUnit;
        var target = direction == "short_watch"
            ? feature.Close - (riskUnit * 1.6)
            : feature.Close + (riskUnit * 1.6);

        return new SignalResult(
            TimestampUtc: feature.TimestampUtc,
            Symbol: feature.Symbol,
            Direction: direction,
            SignalType: "nightly_research_stub",
            Score: feature.MockSignalScore,
            Confidence: Math.Round(Math.Clamp(feature.MockSignalScore * 0.9, 0.1, 0.95), 4),
            TheoreticalEntry: RoundPrice(feature.Symbol, feature.Close),
            TheoreticalStop: RoundPrice(feature.Symbol, stop),
            TheoreticalTarget: RoundPrice(feature.Symbol, target),
            ReasonCodes:
            [
                $"mock_regime:{feature.MockRegime}",
                $"mock_session:{feature.MockSession}",
                $"direction:{feature.Direction}",
                "nightly_research_stub",
                "no_auto_trading"
            ]);
    }

    private static double RoundPrice(string symbol, double value) =>
        symbol.ToUpperInvariant() switch
        {
            "EURUSD" => Math.Round(value, 5),
            "GER40" or "US500" => Math.Round(value, 1),
            _ => Math.Round(value, 2)
        };
}
