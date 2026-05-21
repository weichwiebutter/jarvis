# Hermes Feature Logging v1

Status: implemented foundation  
Scope: local demo feature and signal export  
Runtime: HermesRuntime

## Goal

Hermes Feature Logging v1 creates a small local feature/signals dataset that can
later feed backtesting, prediction review, and learning workflows.

This is not trading execution. The data is deterministic demo/mock data and is
written only to local JSONL files.

## Non-goals

- No broker connection
- No cTrader connection
- No real market data
- No orders
- No auto-trading
- No ML optimization
- No backtest execution
- No WebSocket/API layer

## Components

### FeatureVector

Fields:

- `timestamp_utc`
- `symbol`
- `timeframe`
- `session`
- `h4_regime`
- `h1_bias`
- `m15_setup`
- `m5_trigger`
- `adx`
- `atr`
- `rsi`
- `structure_state`
- `pattern_candidate`
- `signal_score`
- `spread`

### SignalResult

Fields:

- `timestamp_utc`
- `symbol`
- `direction`
- `signal_type`
- `score`
- `confidence`
- `theoretical_entry`
- `theoretical_stop`
- `theoretical_target`
- `reason_codes`

### FeatureExportService

`FeatureExportService` writes demo rows for:

- `XAUUSD`
- `EURUSD`
- `GER40`

Output paths:

```text
data/exports/features/*.features.jsonl
data/exports/signals/*.signals.jsonl
```

The signal rows are theoretical analysis artifacts only. They do not trigger
orders and do not create broker actions.

## Events

Feature Logging v1 uses existing worker lifecycle events and adds:

- `FeatureExportStarted`
- `FeatureExportCompleted`
- `SignalResultExported`

`SignalResultExported` includes safety markers:

- `noAutoTrading = true`
- `humanReviewRequired = true`

## CLI

Read-only commands:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- features
dotnet run --project ./cli/Hermes.Cli.csproj -- signals
```

The CLI reads local JSONL files only. It does not start/stop the runtime, delete
files, place trades, or call external services.

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
dotnet run --project ./cli/Hermes.Cli.csproj -- features
dotnet run --project ./cli/Hermes.Cli.csproj -- signals
find ./data/exports -maxdepth 4 -type f | sort
```

Expected result:

- feature JSONL files exist under `data/exports/features/`
- signal JSONL files exist under `data/exports/signals/`
- CLI shows recent feature and signal rows
- runtime events include `SignalResultExported`
- `no_auto_trading` remains active
- `human_review_required` remains active
