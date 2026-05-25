namespace Hermes.Runtime;

public static class StrategyMutationEngine
{
    public static IReadOnlyList<int> FastEmaCandidates(int seed) =>
        NeighborInts(seed, [6, 8, 9, 10, 12, 14, 16, 18]);

    public static IReadOnlyList<int> SlowEmaCandidates(int seed) =>
        NeighborInts(seed, [18, 21, 24, 30, 34, 40, 55, 72]);

    public static IReadOnlyList<double> RiskRewardCandidates(double seed) =>
        NeighborDoubles(seed, [1.2, 1.4, 1.6, 1.8, 2.0, 2.2, 2.5]);

    public static IReadOnlyList<double> StopLossAtrCandidates(double seed) =>
        NeighborDoubles(seed, [0.8, 1.0, 1.2, 1.5, 1.8, 2.0]);

    public static double ExplorationRatio(StrategyResearchMemory memory)
    {
        var tested = Math.Max(1, memory.VariantsTested);
        var top = memory.TopVariants.Count(result => result.Fitness.Score >= 0.7);
        var rejected = memory.RejectedVariants.Count(result => result.Fitness.Score < 0.45);
        return Math.Clamp(0.35 + rejected / (double)tested - top / (double)tested * 0.1, 0.2, 0.55);
    }

    private static IReadOnlyList<int> NeighborInts(int seed, IReadOnlyList<int> values)
    {
        return values
            .OrderBy(value => Math.Abs(value - seed))
            .Take(4)
            .ToList();
    }

    private static IReadOnlyList<double> NeighborDoubles(double seed, IReadOnlyList<double> values)
    {
        return values
            .OrderBy(value => Math.Abs(value - seed))
            .Take(4)
            .ToList();
    }
}
