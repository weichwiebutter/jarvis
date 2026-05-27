namespace Hermes.Runtime;

internal static class QualityGateStrategySelector
{
    public static IReadOnlyList<StrategySimulationReport> LoadTopSimulationReports(
        StoragePaths storagePaths,
        int maxCandidates)
    {
        maxCandidates = Math.Clamp(maxCandidates, 1, 500);
        var simulationService = new RealisticSimulationService(storagePaths);
        var simulations = simulationService.LoadReports();
        if (simulations.Count == 0)
        {
            simulations = simulationService.Run();
        }

        var latestByVariant = simulations
            .GroupBy(report => report.StrategyVariantId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(report => report.CreatedAtUtc).First(),
                StringComparer.Ordinal);

        var selected = new List<StrategySimulationReport>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var walkForward = new WalkForwardValidationService(storagePaths).LoadReport();
        if (walkForward is not null)
        {
            foreach (var assessment in walkForward.Assessments
                         .OrderByDescending(item => ConfidenceRank(item.StrategyConfidence))
                         .ThenByDescending(item => item.WalkForwardConfidence)
                         .ThenByDescending(item => item.RealismScore)
                         .Take(maxCandidates * 2))
            {
                if (latestByVariant.TryGetValue(assessment.StrategyVariantId, out var report)
                    && seen.Add(report.StrategyVariantId))
                {
                    selected.Add(report);
                    if (selected.Count >= maxCandidates)
                    {
                        return selected;
                    }
                }
            }
        }

        selected.AddRange(latestByVariant.Values
            .Where(report => seen.Add(report.StrategyVariantId))
            .OrderByDescending(report => report.Metrics.RobustnessConfidence)
            .ThenByDescending(report => report.Metrics.RealismScore)
            .ThenByDescending(report => report.Metrics.ProfitFactor)
            .Take(maxCandidates - selected.Count));

        return selected;
    }

    public static string Symbol(StrategySimulationReport report) =>
        report.SampleTrades.FirstOrDefault()?.Symbol ?? "UNKNOWN";

    public static string Timeframe(StrategySimulationReport report) =>
        report.SampleTrades.FirstOrDefault()?.Timeframe ?? "UNKNOWN";

    private static int ConfidenceRank(string confidence) =>
        confidence switch
        {
            "robust" => 5,
            "promising" => 4,
            "experimental" => 3,
            "overfit_suspected" => 2,
            "unstable" => 1,
            "rejected" => 0,
            _ => 0
        };
}
