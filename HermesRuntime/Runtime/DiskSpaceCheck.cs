namespace Hermes.Runtime;

public sealed record DiskSpaceCheck(
    bool IsOk,
    long FreeMb,
    long MinimumFreeMb,
    string DriveName,
    string? Warning)
{
    public static DiskSpaceCheck Failed(string warning) =>
        new(IsOk: false, FreeMb: 0, MinimumFreeMb: 0, DriveName: "unknown", Warning: warning);
}
