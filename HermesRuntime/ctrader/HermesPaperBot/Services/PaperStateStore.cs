namespace HermesPaperBot.Services;

using System;
using System.IO;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Saves and restores the local paper runtime snapshot.
/// </summary>
public sealed class PaperStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly Dictionary<string, PaperStateSnapshot> InMemorySnapshots = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _snapshotPath;
    private readonly PaperSnapshotRecoveryMode _recoveryMode;

    /// <summary>
    /// Creates a new paper state store.
    /// </summary>
    public PaperStateStore(string snapshotPath, PaperSnapshotRecoveryMode recoveryMode = PaperSnapshotRecoveryMode.FreshState)
    {
        _snapshotPath = snapshotPath ?? string.Empty;
        _recoveryMode = recoveryMode;
    }

    /// <summary>
    /// Saves a defensive paper state snapshot.
    /// </summary>
    public bool Save(PaperPortfolioState state)
    {
        if (string.IsNullOrWhiteSpace(_snapshotPath) || state is null)
        {
            return false;
        }

        var snapshot = new PaperStateSnapshot
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            PaperPortfolioState = state,
            ClosedPaperPositions = state.ClosedTrades,
            LastState = "paper_state_saved",
            LastPaperDecision = "would_wait",
            BrokerAction = "none",
        };

        InMemorySnapshots[_snapshotPath] = snapshot;

        try
        {
            var directory = Path.GetDirectoryName(_snapshotPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _snapshotPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            if (File.Exists(_snapshotPath))
            {
                File.Delete(_snapshotPath);
            }

            File.Move(tempPath, _snapshotPath);
        }
        catch
        {
            // cTrader-safe fallback: in-memory snapshot already updated.
        }

        return true;
    }

    /// <summary>
    /// Loads a defensive paper state snapshot.
    /// </summary>
    public PaperStateRestoreResult Load()
    {
        if (string.IsNullOrWhiteSpace(_snapshotPath))
        {
            return new PaperStateRestoreResult
            {
                Success = false,
                State = "snapshot_path_missing",
                Reason = "snapshot_path_missing",
                BrokerAction = "none",
                KillSwitchActive = _recoveryMode == PaperSnapshotRecoveryMode.KillSwitch,
            };
        }

        if (!File.Exists(_snapshotPath))
        {
            if (InMemorySnapshots.TryGetValue(_snapshotPath, out var cachedSnapshot) && IsValid(cachedSnapshot))
            {
                return new PaperStateRestoreResult
                {
                    Success = true,
                    SnapshotValid = true,
                    FreshStateUsed = false,
                    State = "snapshot_restored",
                    Reason = "snapshot_restored_memory",
                    BrokerAction = "none",
                    PaperPortfolioState = cachedSnapshot.PaperPortfolioState,
                };
            }

            return new PaperStateRestoreResult
            {
                Success = true,
                SnapshotValid = false,
                FreshStateUsed = true,
                State = "fresh_state",
                Reason = "snapshot_missing",
                BrokerAction = "none",
            };
        }

        try
        {
            var json = File.ReadAllText(_snapshotPath);
            var snapshot = JsonSerializer.Deserialize<PaperStateSnapshot>(json, JsonOptions);
            if (!IsValid(snapshot))
            {
                return HandleCorruptSnapshot("snapshot_invalid");
            }

            InMemorySnapshots[_snapshotPath] = snapshot!;
            return new PaperStateRestoreResult
            {
                Success = true,
                SnapshotValid = true,
                FreshStateUsed = false,
                State = "snapshot_restored",
                Reason = "snapshot_restored",
                BrokerAction = "none",
                PaperPortfolioState = snapshot!.PaperPortfolioState,
            };
        }
        catch
        {
            if (InMemorySnapshots.TryGetValue(_snapshotPath, out var cachedSnapshot) && IsValid(cachedSnapshot))
            {
                return new PaperStateRestoreResult
                {
                    Success = true,
                    SnapshotValid = true,
                    FreshStateUsed = false,
                    State = "snapshot_restored",
                    Reason = "snapshot_restored_memory",
                    BrokerAction = "none",
                    PaperPortfolioState = cachedSnapshot.PaperPortfolioState,
                };
            }

            return HandleCorruptSnapshot("snapshot_corrupt");
        }
    }

    private PaperStateRestoreResult HandleCorruptSnapshot(string reason)
    {
        if (_recoveryMode == PaperSnapshotRecoveryMode.KillSwitch)
        {
            return new PaperStateRestoreResult
            {
                Success = false,
                SnapshotValid = false,
                CorruptSnapshotDetected = true,
                FreshStateUsed = false,
                KillSwitchActive = true,
                State = "blocked_by_snapshot",
                Reason = reason,
                BrokerAction = "none",
            };
        }

        return new PaperStateRestoreResult
        {
            Success = true,
            SnapshotValid = false,
            CorruptSnapshotDetected = true,
            FreshStateUsed = true,
            KillSwitchActive = false,
            State = "fresh_state",
            Reason = reason,
            BrokerAction = "none",
        };
    }

    private static bool IsValid(PaperStateSnapshot? snapshot)
    {
        return snapshot is not null
            && string.Equals(snapshot.SchemaVersion, "paper_state_snapshot_v1", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(snapshot.GeneratedAtUtc)
            && snapshot.PaperPortfolioState is not null
            && string.Equals(snapshot.BrokerAction, "none", StringComparison.OrdinalIgnoreCase);
    }
}
