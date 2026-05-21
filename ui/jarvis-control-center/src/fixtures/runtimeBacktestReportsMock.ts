export const runtimeBacktestReportsMock = {
  report_files: [
    'HermesRuntime/data/reports/backtests/bt_demo_fixture_xauusd_trend_pullback.backtest.json',
  ],
  reports: [
    {
      run_id: 'bt_demo_fixture_xauusd_trend_pullback',
      symbol: 'XAUUSD',
      timeframe: 'M5',
      strategy_name: 'DemoTrendPullback',
      status: 'completed_demo',
      started_at_utc: '2026-05-21T13:27:59Z',
      completed_at_utc: '2026-05-21T13:27:59Z',
      trade_count: 12,
      winrate: 0.58,
      profit_factor: 1.42,
      max_drawdown: 0.064,
      expectancy: 0.37,
      no_auto_trading: true,
      notes:
        'Demo-Backtest-Report aus lokalem Stub. Keine echte Marktdaten-Wiedergabe, keine Orders, keine Brokerverbindung.',
    },
  ],
} as const;
