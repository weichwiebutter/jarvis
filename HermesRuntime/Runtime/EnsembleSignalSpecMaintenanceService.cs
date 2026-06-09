using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EnsembleSignalSpecValidationResult(
    string PackageId,
    int MembersTotal,
    int SpecsPresent,
    int SpecsExported,
    IReadOnlyList<string> MissingSpecs,
    IReadOnlyList<string> ExportedCandidates,
    IReadOnlyList<string> Blockers,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

internal sealed record EnsemblePackageMemberRef(
    string CandidateId,
    string SignalSpecPath,
    string CertificationReportPath);

internal sealed record EnsembleSignalPackageRef(
    string PackageId,
    IReadOnlyList<EnsemblePackageMemberRef> Members,
    IReadOnlyList<EnsemblePackageMemberRef> MemberSignalSpecs);

public sealed class EnsembleSignalSpecMaintenanceService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public EnsembleSignalSpecMaintenanceService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public EnsembleSignalSpecValidationResult ExportMissingSpecs()
    {
        var package = LoadPackage();
        var blockers = ValidatePackage(package);
        var exported = new List<string>();
        var missing = new List<string>();
        if (blockers.Count == 0)
        {
            var research = new ScalpingResearchService(_storagePaths, _runtimeRoot);
            foreach (var member in package!.Members)
            {
                if (File.Exists(member.SignalSpecPath))
                {
                    continue;
                }

                research.ExportSignalAgentSpec(member.CandidateId);
                exported.Add(member.CandidateId);
            }

            foreach (var member in package.Members.Where(member => !File.Exists(member.SignalSpecPath)))
            {
                missing.Add(member.CandidateId);
            }
        }

        return BuildResult(package, exported, missing, blockers);
    }

    public EnsembleSignalSpecValidationResult ValidateSpecs()
    {
        var package = LoadPackage();
        var blockers = ValidatePackage(package);
        var exported = new List<string>();
        var missing = new List<string>();
        if (blockers.Count == 0)
        {
            missing.AddRange(package!.Members.Where(member => !File.Exists(member.SignalSpecPath)).Select(member => member.CandidateId));
        }

        return BuildResult(package, exported, missing, blockers);
    }

    private EnsembleSignalPackageRef? LoadPackage()
    {
        var path = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot).SignalAgentJsonPath;
        return File.Exists(path)
            ? JsonSerializer.Deserialize<EnsembleSignalPackageRef>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private static List<string> ValidatePackage(EnsembleSignalPackageRef? package)
    {
        var blockers = new List<string>();
        if (package is null)
        {
            blockers.Add("ensemble_signal_agent_package_missing");
            return blockers;
        }

        if (package.Members.Count == 0)
        {
            blockers.Add("ensemble_members_missing");
        }

        foreach (var member in package.Members)
        {
            if (!File.Exists(member.CertificationReportPath))
            {
                blockers.Add($"certification_report_missing:{member.CandidateId}");
            }
        }

        return blockers;
    }

    private static EnsembleSignalSpecValidationResult BuildResult(
        EnsembleSignalPackageRef? package,
        IReadOnlyList<string> exported,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> blockers)
    {
        var members = package?.Members ?? [];
        var specsPresent = members.Count(member => File.Exists(member.SignalSpecPath));
        return new EnsembleSignalSpecValidationResult(
            PackageId: package?.PackageId ?? "missing_package",
            MembersTotal: members.Count,
            SpecsPresent: specsPresent,
            SpecsExported: exported.Count,
            MissingSpecs: missing,
            ExportedCandidates: exported,
            Blockers: blockers,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }
}
