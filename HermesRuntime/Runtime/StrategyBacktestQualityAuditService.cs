using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestQualityAuditEntry(
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    int TradesSimulated,
    double SampleSizeScore,
    double ConfidenceLevel,
    double StatisticalReliability,
    double PeriodCoverage,
    double MarketRegimeCoverage,
    double ResultStability,
    string QualityClass,
    bool EligibleForOos,
    bool EligibleForWalkForward,
    bool EligibleForForwardTest,
    bool EligibleForCertification,
    bool PassedResearchGate,
    bool PassedOosGate,
    bool PassedCertificationGate,
    IReadOnlyList<string> RootCauses,
    IReadOnlyList<string> Warnings);

public sealed record StrategyBacktestQualityAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int AuditedBacktests,
    int InsufficientSampleCount,
    int LowConfidenceCount,
    int MediumConfidenceCount,
    int HighConfidenceCount,
    int CertificationReadyCount,
    int PassedResearchGateCount,
    int PassedOosGateCount,
    int PassedCertificationGateCount,
    IReadOnlyList<StrategyBacktestQualityAuditEntry> Entries,
    IReadOnlyDictionary<string, int> Thresholds,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    bool FrankRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestQualityAuditService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyBacktestQualityAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_quality");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_backtest_quality_audit.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_backtest_quality_audit.md");

    public StrategyBacktestQualityAuditReport Run()
    {
        Directory.CreateDirectory(Root);

        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var entries = new List<StrategyBacktestQualityAuditEntry>();

        if (latestSuccess is not null)
        {
            entries.Add(BuildEntry(latestSuccess.Job, latestSuccess.Execution));
        }

        var audited = entries.Count;
        var insufficient = entries.Count(entry => entry.QualityClass == "insufficient_sample");
        var low = entries.Count(entry => entry.QualityClass == "low_confidence");
        var medium = entries.Count(entry => entry.QualityClass == "medium_confidence");
        var high = entries.Count(entry => entry.QualityClass == "high_confidence");
        var certificationReady = entries.Count(entry => entry.EligibleForCertification);

        var report = new StrategyBacktestQualityAuditReport(
            ReportVersion: "strategy_backtest_quality_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            AuditedBacktests: audited,
            InsufficientSampleCount: insufficient,
            LowConfidenceCount: low,
            MediumConfidenceCount: medium,
            HighConfidenceCount: high,
            CertificationReadyCount: certificationReady,
            PassedResearchGateCount: entries.Count(entry => entry.PassedResearchGate),
            PassedOosGateCount: entries.Count(entry => entry.PassedOosGate),
            PassedCertificationGateCount: entries.Count(entry => entry.PassedCertificationGate),
            Entries: entries.OrderByDescending(entry => entry.SampleSizeScore).ThenByDescending(entry => entry.ConfidenceLevel).ToList(),
            Thresholds: new Dictionary<string, int>
            {
                ["insufficient_sample_max_trades"] = 29,
                ["low_confidence_min_trades"] = 30,
                ["low_confidence_max_trades"] = 100,
                ["medium_confidence_min_trades"] = 101,
                ["medium_confidence_max_trades"] = 300,
                ["high_confidence_min_trades"] = 301,
            },
            Warnings: audited == 0 ? ["no_successful_backtest_found"] : [],
            OperatorSummary: BuildOperatorSummary(entries),
            FrankRequired: false,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public StrategyBacktestQualityAuditReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestQualityAuditReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategyBacktestQualityAuditEntry BuildEntry(StrategyBacktestJobPlan job, StrategyBacktestResult result)
    {
        var trades = result.TradesSimulated ?? 0;
        var sampleSizeScore = Math.Clamp(trades / 300.0, 0.0, 1.0);
        var confidenceLevel = ComputeConfidenceLevel(trades, result);
        var reliability = Math.Clamp((result.ProfitFactor.HasValue ? 0.4 : 0.1) + sampleSizeScore * 0.4 + confidenceLevel * 0.2, 0.0, 1.0);
        var periodCoverage = ComputePeriodCoverage(job);
        var marketRegimeCoverage = ComputeMarketRegimeCoverage(job);
        var resultStability = ComputeResultStability(result);
        var qualityClass = Classify(trades);
        var eligibleForOos = trades >= 30;
        var eligibleForWalkForward = trades >= 30 && periodCoverage >= 0.5;
        var eligibleForForwardTest = trades >= 30 && confidenceLevel >= 0.5;
        var eligibleForCertification = trades > 300 && confidenceLevel >= 0.75 && reliability >= 0.75;
        var passedResearchGate = trades >= 30;
        var passedOosGate = trades >= 100;
        var passedCertificationGate = trades >= 100 && eligibleForCertification;
        var rootCauses = DetermineRootCauses(job, result, trades);
        var warnings = new List<string>();
        if (result.Warnings.Count > 0)
        {
            warnings.AddRange(result.Warnings);
        }
        if (trades < 30)
        {
            warnings.Add("insufficient_sample");
        }

        return new StrategyBacktestQualityAuditEntry(
            BacktestJobId: job.BacktestJobId,
            StrategyPattern: job.StrategyPattern,
            Asset: job.Asset,
            Timeframe: job.Timeframe,
            TradesSimulated: trades,
            SampleSizeScore: Math.Round(sampleSizeScore, 4),
            ConfidenceLevel: Math.Round(confidenceLevel, 4),
            StatisticalReliability: Math.Round(reliability, 4),
            PeriodCoverage: Math.Round(periodCoverage, 4),
            MarketRegimeCoverage: Math.Round(marketRegimeCoverage, 4),
            ResultStability: Math.Round(resultStability, 4),
            QualityClass: qualityClass,
            EligibleForOos: eligibleForOos,
            EligibleForWalkForward: eligibleForWalkForward,
            EligibleForForwardTest: eligibleForForwardTest,
            EligibleForCertification: eligibleForCertification,
            PassedResearchGate: passedResearchGate,
            PassedOosGate: passedOosGate,
            PassedCertificationGate: passedCertificationGate,
            RootCauses: rootCauses,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static double ComputeConfidenceLevel(int trades, StrategyBacktestResult result)
    {
        if (trades <= 0)
        {
            return 0.0;
        }

        var baseScore = trades switch
        {
            < 30 => 0.15,
            <= 100 => 0.45,
            <= 300 => 0.7,
            _ => 0.9
        };

        var stabilityBonus = result.MaxDrawdown.HasValue && result.MaxDrawdown.Value > -1.0 ? 0.05 : 0.0;
        return Math.Clamp(baseScore + stabilityBonus, 0.0, 1.0);
    }

    private static double ComputePeriodCoverage(StrategyBacktestJobPlan job)
        => job.BacktestPeriod.Contains("historical", StringComparison.OrdinalIgnoreCase) ? 0.6 : 0.3;

    private static double ComputeMarketRegimeCoverage(StrategyBacktestJobPlan job)
        => job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase) ? 0.4 : 0.55;

    private static double ComputeResultStability(StrategyBacktestResult result)
        => result.TradesSimulated is null or 0 ? 0.0 : Math.Clamp(1.0 - Math.Abs(result.MaxDrawdown ?? 0.0) * 0.1, 0.0, 1.0);

    private static string Classify(int trades)
        => trades < 30 ? "insufficient_sample"
            : trades <= 100 ? "low_confidence"
            : trades <= 300 ? "medium_confidence"
            : "high_confidence";

    private static IReadOnlyList<string> DetermineRootCauses(StrategyBacktestJobPlan job, StrategyBacktestResult result, int trades)
    {
        var causes = new List<string>();
        if (trades < 30)
        {
            causes.Add("entry_conditions_too_strict");
            causes.Add("strategy_scope_too_narrow");
        }

        if (result.Warnings.Count > 0 && result.Warnings.Any(warning => warning.Contains("dataset", StringComparison.OrdinalIgnoreCase)))
        {
            causes.Add("dataset_too_small");
        }

        if (!job.BacktestPeriod.Contains("historical", StringComparison.OrdinalIgnoreCase))
        {
            causes.Add("insufficient_history");
        }

        return causes.Count > 0 ? causes.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : ["unknown"];
    }

    private static string BuildOperatorSummary(IReadOnlyList<StrategyBacktestQualityAuditEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "Kein erfolgreicher Backtest vorhanden. Frank nötig: nein.";
        }

        var entry = entries[0];
        return $"{entry.StrategyPattern} · {entry.Asset} {entry.Timeframe}\n\nBacktest technisch erfolgreich.\n\nAussagekraft:\n{entry.QualityClass.Replace('_', ' ')}.\n\nGrund:\nNur {entry.TradesSimulated} Trades gefunden.\n\nEmpfehlung:\nMehr historische Daten oder längerer Testzeitraum erforderlich.\n\nFrank nötig:\nnein";
    }

    private void WriteArtifacts(StrategyBacktestQualityAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyBacktestQualityAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Quality Audit");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Audited backtests: {report.AuditedBacktests}");
        sb.AppendLine($"- Insufficient sample: {report.InsufficientSampleCount}");
        sb.AppendLine($"- Low confidence: {report.LowConfidenceCount}");
        sb.AppendLine($"- Medium confidence: {report.MediumConfidenceCount}");
        sb.AppendLine($"- High confidence: {report.HighConfidenceCount}");
        sb.AppendLine($"- Certification ready: {report.CertificationReadyCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Entries");
        foreach (var entry in report.Entries)
        {
            sb.AppendLine($"- {entry.StrategyPattern} · {entry.Asset} {entry.Timeframe} · trades={entry.TradesSimulated} · class={entry.QualityClass}");
        }
        return sb.ToString();
    }

}
