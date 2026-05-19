namespace Hermes.Runtime;

public sealed record StorageInitializationResult(
    StoragePaths Paths,
    string ProfileName,
    bool SafeMode,
    string? SafeModeReason)
{
    public static StorageInitializationResult Ready(StoragePaths paths, string profileName) =>
        new(paths, profileName, SafeMode: false, SafeModeReason: null);

    public static StorageInitializationResult FromSafeMode(
        StoragePaths paths,
        string profileName,
        string reason) =>
        new(paths, profileName, SafeMode: true, SafeModeReason: reason);
}
