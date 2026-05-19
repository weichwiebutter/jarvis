# Hermes Runtime v1 - Sprint 6 ReplayManifest + RuntimeHealth

Status: implemented foundation  
Scope: manifest and health reporting only  
Runtime version: 1.0.0-sprint6

## Goal

Sprint 6 adds two runtime visibility primitives:

- `ReplayManifest` for documenting future replay inputs without running a replay
- `RuntimeHealth` for writing the current local runtime state to a small JSON report

This sprint does not add a replay player, backtest engine, trading logic, WebSocket layer, API layer, broker connection, or auto-trading.

## Non-goals

- No real replay execution
- No backtesting
- No trading signals
- No market-data playback
- No cTrader or broker integration
- No WebSockets
- No APIs
- No auto-trading

## ReplayManifest

`ReplayManifest` captures the metadata needed to identify a future replay run:

- `ReplayId`
- `ReplayType`
- `Symbol`
- `Timeframe`
- `FromUtc`
- `ToUtc`
- `DataHash`
- `RuntimeVersion`
- `FeatureSchemaVersion`
- `ModelVersion`
- `ClusterVersion`
- `ParametersHash`
- `InputFiles`

Sprint 6 creates a demo manifest under:

```text
data/replays/manifests/
```

The manifest references the latest local feature export file when one exists. If no export exists, it records a placeholder input marker. No input data is replayed.

## ReplayManifestService

`ReplayManifestService`:

- creates `data/replays/manifests/`
- creates one demo replay manifest per runtime run
- writes the manifest as JSON
- publishes `ReplayManifestCreated`

The event explicitly states that no replay was executed.

## RuntimeHealth

`RuntimeHealth` writes the runtime status to:

```text
data/reports/runtime_health.json
```

Fields:

- `TimestampUtc`
- `RuntimeState`
- `SafeMode`
- `NoAutoTrading`
- `HumanReviewRequired`
- `FreeDiskGb`
- `PendingJobs`
- `RunningJobs`
- `FailedJobs`
- `QuarantinedJobs`
- `LastSnapshotId`
- `LastError`

`NoAutoTrading` is always `true` in this phase. `HumanReviewRequired` is always `true`.

## RuntimeHost flow

Sprint 6 extends startup/shutdown as follows:

1. Runtime initializes storage, event bus, snapshots, and queue.
2. Demo feature export job is created if needed.
3. `WorkerHost` runs once.
4. `ReplayManifestService` creates a demo replay manifest.
5. Runtime snapshot is written.
6. `RuntimeHealthService` writes `runtime_health.json`.
7. Runtime publishes stop event and exits.

There is no loop and no background process.

## Runtime events

Sprint 6 adds:

- `ReplayManifestCreated`

Existing worker, queue, snapshot, and runtime events remain unchanged.

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
find ./data/replays -maxdepth 4 -type f | sort
find ./data/reports -maxdepth 4 -type f | sort
cat ./data/reports/runtime_health.json
tail -n 50 ./data/events/runtime/*.jsonl
```

Expected result:

- runtime starts and exits cleanly
- a replay manifest exists under `data/replays/manifests/`
- `runtime_health.json` exists
- `ReplayManifestCreated` is present in runtime JSONL
- health contains `safe_mode`, `no_auto_trading`, and `human_review_required`
- no replay is executed
