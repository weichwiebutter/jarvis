namespace Hermes.Runtime;

public static class StrategyVariantGenerator
{
    public static IReadOnlyList<StrategyVariant> PrioritizeSeeds(StrategyResearchMemory memory)
    {
        var stableTop = memory.TopVariants
            .Where(result => result.Fitness.Score < 0.995 || result.LossCount > 0)
            .OrderByDescending(result => result.Fitness.Score)
            .ThenByDescending(result => result.TradeCount)
            .Take(8)
            .Select(result => result.Variant)
            .ToList();

        if (stableTop.Count > 0)
        {
            return stableTop;
        }

        return memory.TopVariants
            .OrderByDescending(result => result.TradeCount)
            .Take(8)
            .Select(result => result.Variant)
            .ToList();
    }
}
