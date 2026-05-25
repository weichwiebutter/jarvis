using System.Text.Json;

namespace Hermes.Runtime;

public sealed class NightlyResearchService
{
    private const string StateVersion = "nightly_research_state_v1";

    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;

    public NightlyResearchService(StoragePaths storagePaths, string configPath)
    {
        _storagePaths = storagePaths;
        _configPath = configPath;
    }

    public string StateDirectory => Path.Combine(_storagePaths.Root, "reports", "nightly_beta3");

    public string StatePath => Path.Combine(StateDirectory, "nightly_state.json");

    public NightlyResearchConfig LoadConfig() => NightlyResearchConfig.LoadOrDefault(_configPath);

    public NightlyResearchState LoadState()
    {
        if (!File.Exists(StatePath))
        {
            return EmptyState("not_started");
        }

        try
        {
            return JsonSerializer.Deserialize<NightlyResearchState>(
                File.ReadAllText(StatePath),
                JsonDefaults.SnapshotReadOptions) ?? EmptyState("not_started");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptyState("state_unreadable");
        }
    }

    public NightlyResearchState WriteState(NightlyResearchState state)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        return state;
    }

    public NightlyResearchState EmptyState(string status) =>
        new(
            StateVersion: StateVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: string.Empty,
            StartedAtUtc: null,
            DeadlineUtc: null,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            NextAction: "wait_for_nightly_window",
            LastCheckpointPath: null,
            LastAutopilotReportPath: null,
            LastError: null,
            NoAutoTrading: true,
            HumanReviewRequired: true);

    public NightlyResearchState CreateRunState(
        string runId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineUtc,
        string status,
        string nextAction)
    {
        return new NightlyResearchState(
            StateVersion: StateVersion,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: 0,
            IdleIterations: 0,
            WorkPerformed: 0,
            NextAction: nextAction,
            LastCheckpointPath: null,
            LastAutopilotReportPath: null,
            LastError: null,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }
}
