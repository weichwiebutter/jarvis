using System.Runtime.InteropServices;

namespace Hermes.Runtime;

public sealed class DiskSpaceGuard
{
    public DiskSpaceCheck Check(StoragePaths paths, long minimumFreeDiskMb)
    {
        try
        {
            var root = Path.GetPathRoot(paths.Root);
            if (string.IsNullOrWhiteSpace(root))
            {
                return DiskSpaceCheck.Failed("Could not resolve storage drive root.");
            }

            var drive = new DriveInfo(root);
            var freeMb = drive.AvailableFreeSpace / 1024 / 1024;
            var ok = freeMb >= minimumFreeDiskMb;

            return new DiskSpaceCheck(
                ok,
                freeMb,
                minimumFreeDiskMb,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? drive.Name : root,
                ok ? null : $"Free disk space below threshold: {freeMb} MB < {minimumFreeDiskMb} MB");
        }
        catch (Exception ex)
        {
            return DiskSpaceCheck.Failed($"Disk space check failed: {ex.Message}");
        }
    }
}
