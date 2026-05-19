# Hermes Runtime v1 - Sprint 5 WorkerHost

Status: implemented foundation  
Scope: one-shot local worker execution  
Runtime version: 1.0.0-sprint5

## Goal

Sprint 5 adds the first minimal WorkerHost to Hermes Runtime. The worker host can process exactly one pending demo feature-export job from the file-based queue, write a small stub export file, publish runtime events, and stop cleanly.

This sprint is intentionally not a full worker system. It proves the local path from queue job to worker execution to completed result without introducing trading logic, replay, backtesting, APIs, WebSockets, broker connections, or auto-trading.

## Non-goals

- No trading logic
- No real market data
- No cTrader or broker connection
- No backtest
- No replay player
- No worker loop
- No WebSocket or API layer
- No auto-trading

## Components

### WorkerHost

`WorkerHost` is a one-shot host. It:

- registers available local workers
- publishes `WorkerStarted`
- publishes `WorkerHeartbeat`
- dequeues only `feature_export.demo`
- marks the job as `running`
- executes the registered worker
- marks the job as `completed` or `failed`
- publishes job and worker lifecycle events
- stops after one attempt

### WorkerRegistry

`WorkerRegistry` maps job types to local worker implementations. Sprint 5 registers only:

- `feature_export.demo` -> `FeatureExportWorker`

### WorkerHeartbeat

`WorkerHeartbeat` captures visible worker state:

- `WorkerId`
- `WorkerName`
- `TimestampUtc`
- `Status`
- `CurrentJobId`
- `QueueStatus`

### FeatureExportWorker Stub

`FeatureExportWorker` writes a small demo feature export file under:

```text
data/exports/features/
```

The output is JSONL and contains stub feature rows only. No real market data, broker data, trading signal, or backtest result is produced.

## Job flow

On runtime startup:

1. `RuntimeHost` initializes storage, event bus, snapshots, and queue.
2. `RuntimeHost` creates a demo `feature_export.demo` job if no pending feature export job exists.
3. `WorkerHost` runs once.
4. The feature export job moves from `pending/` to `running/`.
5. `FeatureExportWorker` writes a stub JSONL export file.
6. The job moves to `completed/`.
7. A result file is written next to the completed job.
8. `WorkerHost` stops.
9. `RuntimeHost` writes the runtime snapshot and stops.

If the worker throws, the job is moved to `failed/` and a failure result is written.

## Runtime events

Sprint 5 adds:

- `WorkerStarted`
- `WorkerHeartbeat`
- `JobStarted`
- `FeatureExportStarted`
- `FeatureExportCompleted`
- `JobCompleted`
- `JobFailed`
- `WorkerStopped`

Existing queue and runtime events remain unchanged.

## Queue safety

The worker host only dequeues `feature_export.demo`. Other pending jobs remain untouched. There is no endless worker loop and no background service.

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
find ./data/jobs -maxdepth 4 -type f | sort
find ./data/exports -maxdepth 4 -type f | sort
find ./data/events -maxdepth 4 -type f | sort
tail -n 40 ./data/events/runtime/*.jsonl
```

Expected result:

- runtime starts and exits cleanly
- one demo feature-export job is processed
- an export file exists under `data/exports/features/`
- the job is in `completed/` or `failed/`
- lifecycle events are visible in runtime JSONL
- no worker keeps running after shutdown
