namespace Hermes.Runtime;

public interface ICTraderHistoricalDataClient
{
    CTraderConnectionHealth CheckHealth();

    IReadOnlyList<MarketDataCandle> DownloadHistoricalCandles(CTraderHistoricalDataRequest request);
}
