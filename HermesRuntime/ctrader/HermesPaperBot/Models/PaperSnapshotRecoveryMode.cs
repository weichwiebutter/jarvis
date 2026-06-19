namespace HermesPaperBot.Models;

/// <summary>
/// How to handle a corrupt paper snapshot.
/// </summary>
public enum PaperSnapshotRecoveryMode
{
    FreshState,
    KillSwitch,
}
