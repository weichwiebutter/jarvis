# Hermes Runtime v1 - Sprint 4 QueueManager

Status: implemented foundation  
Scope: local file-based queue only  
Runtime version: 1.0.0-sprint4

## Goal

Sprint 4 adds the first local QueueManager foundation to Hermes Runtime. The queue is intentionally simple, file-based, append-visible, and offline. It exists so later runtime layers can create jobs without introducing workers, external message queues, APIs, trading logic, or broker integration.

## Non-goals

- No trading logic
- No worker execution
- No external message queue
- No WebSocket or API layer
- No broker or cTrader connection
- No auto-trading

## Queue storage

Queue files live under the configured storage root:

```text
data/jobs/
  pending/
  running/
  completed/
  failed/
  quarantined/
```

Each job is stored as a JSON manifest:

```text
{job_id}.job.json
```

Completed or failed jobs can also write:

```text
{job_id}.result.json
```

## JobManifest

`JobManifest` defines the job request:

- `JobId`
- `JobType`
- `Priority`
- `Status`
- `CreatedAtUtc`
- `RequestedBy`
- `ResourceProfile`
- `MaxRuntimeMinutes`
- `MaxRetries`
- `RetryCount`
- `Parameters`

## JobResult

`JobResult` defines terminal job output metadata:

- `JobId`
- `Status`
- `StartedAtUtc`
- `CompletedAtUtc`
- `OutputPath`
- `ErrorMessage`
- `Metrics`

## QueueManager operations

Implemented operations:

- `Enqueue`
- `TryDequeue`
- `MarkRunning`
- `MarkCompleted`
- `MarkFailed`
- `Quarantine`
- `GetJobs`

`TryDequeue` is available as a local primitive, but Sprint 4 does not call it from `RuntimeHost`. No worker is started, and no pending job is executed by the runtime.

## RuntimeHost integration

At startup, `RuntimeHost` now:

- creates the queue directory structure
- creates one demo `runtime.demo.noop` pending job for visibility if no demo job exists yet
- publishes a `JobCreated` runtime event when the demo job is created
- includes `QueueStatus` in the runtime snapshot

The demo job is explicitly non-executable in Sprint 4. It is there to prove that file-based job creation, event publication, and snapshot queue status work end to end.

## Runtime events

Sprint 4 adds:

- `JobCreated`

The event payload includes the job id, type, priority, status, requested-by metadata, retry settings, and the current queue status.

## Snapshot integration

Runtime snapshots include `QueueStatus`:

- `Pending`
- `Running`
- `Completed`
- `Failed`
- `Quarantined`
- `Total`

This makes queue state visible without requiring a live UI, WebSocket, or worker subsystem.

## Safety

Sprint 4 preserves the current runtime safety boundaries:

- no trading jobs are created
- no broker connection exists
- no worker is started
- no queued job is executed automatically
- queue state is local and file-based
- runtime shutdown remains synchronous and clean

## Validation

Run from `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
find ./data/jobs -maxdepth 4 -type f | sort
find ./data/events -maxdepth 4 -type f | sort
tail -n 30 ./data/events/runtime/*.jsonl
```

Expected result:

- runtime starts and exits cleanly
- `data/jobs/` directories exist
- a pending demo job is visible
- `JobCreated` is present in the runtime JSONL event file
- runtime snapshot contains queue status
- no job is moved to `running/`, `completed/`, or `failed/` by the host
