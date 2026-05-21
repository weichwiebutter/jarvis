# Hermes Backtest Job Stub v1

Status: implemented foundation  
Scope: local demo backtest report only  
Runtime: HermesRuntime

## Goal

Backtest Job Stub v1 adds the next local runtime step after Feature Logging v1:
a queued demo backtest job writes a structured report that later UI, CLI,
learning, and research components can inspect.

This is not a real backtest. It does not replay market data and does not execute
trading logic.

## Non-goals

- No broker connection
- No cTrader connection
- No order creation
- No live trading
- No real backtest engine
- No ML optimization
- No API or WebSocket layer
- No automatic strategy approval

## Demo Job

Runtime creates a demo job when no pending demo backtest job exists:

- Symbol: `XAUUSD`
- Timeframe: `M5`
- Period: `Demo`
- Strategy: `DemoTrendPullback`
- Job type: `backtest.demo`

The worker remains a one-shot stub and writes one local report.

## Report Path

```text
data/reports/backtests/*.backtest.json
```

## BacktestReport Fields

- `run_id`
- `symbol`
- `timeframe`
- `strategy_name`
- `status`
- `started_at_utc`
- `completed_at_utc`
- `trade_count`
- `winrate`
- `profit_factor`
- `max_drawdown`
- `expectancy`
- `notes`
- `no_auto_trading`

## Events

Backtest Job Stub v1 adds:

- `BacktestStarted`
- `BacktestCompleted`

Both events are runtime JSONL events. They describe local report creation only
and include no trading action.

## CLI

Read-only command:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- backtests
```

The CLI reads local report files only. It does not start or stop Runtime, delete
files, place trades, or call external services.

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
dotnet run --project ./cli/Hermes.Cli.csproj -- backtests
find ./data/reports/backtests -maxdepth 3 -type f | sort
tail -n 100 ./data/events/runtime/*.jsonl
```

Expected result:

- demo backtest report exists under `data/reports/backtests/`
- CLI displays the report read-only
- runtime events include `BacktestStarted` and `BacktestCompleted`
- `no_auto_trading` remains true
- runtime exits cleanly
