# Hermes Signal Outcome Tracking v1

Status: implemented foundation  
Scope: local demo signal outcome evaluation  
Runtime: HermesRuntime

## Goal

Signal Outcome Tracking v1 evaluates stored demo `SignalResult` rows and writes
structured outcome reports for later learning, prediction feedback, and
confidence calibration.

This is not trading execution. The evaluator reads local JSONL signal exports,
creates deterministic mock outcomes, and writes local reports only.

## Non-goals

- No broker connection
- No cTrader connection
- No orders
- No live trading
- No real market replay
- No ML optimization
- No API or WebSocket layer
- No automatic strategy or model update

## Components

### OutcomeEvaluation

Fields:

- `outcome_id`
- `signal_id`
- `symbol`
- `timeframe`
- `direction`
- `outcome_status`
- `hit_target`
- `hit_stop`
- `expired`
- `invalidated`
- `mfe`
- `mae`
- `final_r`
- `evaluated_at_utc`
- `notes`

### OutcomeTrackerService

`OutcomeTrackerService` reads the latest local signal export from:

```text
data/exports/signals/*.signals.jsonl
```

It writes a demo outcome report to:

```text
data/reports/outcomes/*.outcomes.json
```

Demo mapping:

- `XAUUSD` -> `tp_hit`
- `EURUSD` -> `expired`
- `GER40` -> `partial`

The generated data is intended as reviewable learning input only. It can later
support prediction feedback, confidence calibration, and feature importance
review after human approval.

## Events

Signal Outcome Tracking v1 adds:

- `OutcomeEvaluationStarted`
- `OutcomeEvaluationCompleted`

Both events include safety markers:

- `noAutoTrading = true`
- `humanReviewRequired = true`

## CLI

Read-only command:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- outcomes
```

The CLI reads local report files only. It does not start or stop Runtime, delete
files, place trades, or call external services.

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
dotnet run --project ./cli/Hermes.Cli.csproj -- outcomes
find ./data/reports/outcomes -maxdepth 3 -type f | sort
tail -n 100 ./data/events/runtime/*.jsonl
```

Expected result:

- outcome report exists under `data/reports/outcomes/`
- CLI displays outcomes read-only
- runtime events include `OutcomeEvaluationStarted` and `OutcomeEvaluationCompleted`
- no trading execution occurs
- `no_auto_trading` remains active
- data is structured for later learning and confidence calibration
