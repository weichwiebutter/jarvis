using System.Text.Json;

namespace Hermes.Runtime;

public sealed record NightlyResearchConfig(
    bool Enabled,
    int StartHour,
    int EndHour,
    double MaxRuntimeHours,
    int SleepSecondsBetweenIterations,
    int MaxIdleIterations,
    IReadOnlyList<string> AllowedSymbols,
    IReadOnlyList<string> AllowedTimeframes)
{
    public static NightlyResearchConfig Default =>
        new(
            Enabled: true,
            StartHour: 23,
            EndHour: 5,
            MaxRuntimeHours: 6,
            SleepSecondsBetweenIterations: 60,
            MaxIdleIterations: 10,
            AllowedSymbols: ["XAUUSD", "EURUSD", "GER40"],
            AllowedTimeframes: ["M5", "M15", "H1"]);

    public static NightlyResearchConfig LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<NightlyResearchConfig>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Default;
        }
    }

    public bool IsInAllowedWindow(DateTimeOffset timestamp)
    {
        if (!Enabled)
        {
            return false;
        }

        var hour = timestamp.LocalDateTime.Hour;
        return StartHour <= EndHour
            ? hour >= StartHour && hour < EndHour
            : hour >= StartHour || hour < EndHour;
    }
}
