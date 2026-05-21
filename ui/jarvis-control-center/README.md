# Jarvis Control Center Prototype

First separated React/Vite prototype for the future Jarvis Control Center.

This prototype is intentionally isolated from the existing Gradio test UI and from
`HermesRuntime`. It never sends commands to the runtime and does not write files.

## Start

```bash
cd ui/jarvis-control-center
npm install
npm run dev
```

## Scope

- No API calls
- No WebSockets
- No cTrader connection
- No live quotes
- No runtime writes
- Read-only browser fetch for `HermesRuntime/data/reports/runtime_health.json`

## Runtime Health JSON

In Vite dev mode, the Runtime Health panel tries to load the real file through a
read-only `/@fs/...` URL configured in `vite.config.js`:

```text
HermesRuntime/data/reports/runtime_health.json
```

This is a browser fetch of an existing JSON file. It does not start
`HermesRuntime`, does not open a backend API, and does not write to runtime data.
The UI also performs optional read-only file probes for the matching runtime
event-store JSONL file and the latest replay manifest, so the Event Store and
Replay Manifest indicators can become active when those files are reachable.

If the browser cannot access that path, for example in a static build or a
stricter browser/server context, the UI falls back to
`src/fixtures/runtimeHealthMock.ts` and shows a visible fallback warning.

## Read-only Runtime Data Adapter

Runtime-facing file reads are centralized in:

```text
src/data/runtimeDataAdapter.ts
```

The adapter is browser-only and read-only. It prepares access to:

- `HermesRuntime/data/reports/runtime_health.json`
- `HermesRuntime/data/setup_watch/setup_watch.json`
- runtime JSONL event files under `HermesRuntime/data/events/runtime/`
- future queue/job snapshots under `HermesRuntime/data/jobs/`

`RuntimeHealthPanel` and `SetupWatchPanel` both load through
`loadRuntimeData()` from the adapter. `EventTimelinePanel` uses
`loadRuntimeTimelineEvents()` and prepares read-only JSONL events for timeline
display. `JobsQueuePanel` uses `loadRuntimeJobs()` and currently falls back to
fixtures unless a read-only queue snapshot is available. The normalized runtime
shape is:

```ts
{
  runtimeHealth,
  setupWatches,
  dataSource: 'live_file' | 'fixture' | 'unavailable',
  warnings,
}
```

Runtime timeline events are normalized to:

```ts
{
  id,
  time,
  eventType,
  category,
  severity,
  source,
  description,
}
```

The event loader currently derives the JSONL file name from the Runtime Health
timestamp and attempts to read:

```text
HermesRuntime/data/events/runtime/yyyy-MM-dd.runtime.jsonl
```

Supported timeline event types include `RuntimeStarted`, `StorageInitialized`,
`SnapshotCreated`, `ReplayManifestCreated`, `SetupWatchCreated`,
`SetupWatchUpdated`, `LearningCandidateCreated`, `JobStarted`, `JobCompleted`,
and `RuntimeStopped`. Unknown event types are still displayed defensively as
runtime events when a valid JSONL line can be parsed.

Runtime jobs are normalized into status buckets:

```ts
{
  pending,
  running,
  completed,
  failed,
  quarantined,
}
```

Each job entry is prepared for `job_id`, `job_type`, `priority`, `status`,
`created_at_utc`, `requested_by`, `resource_profile`, retry limits, optional
result/error fields, and parameters. The current Vite prototype points at a
future read-only snapshot path:

```text
HermesRuntime/data/jobs/jobs.index.json
```

The real HermesRuntime queue stores manifests across `pending/`, `running/`,
`completed/`, `failed/`, and `quarantined/` directories. Browsers cannot safely
enumerate those local directories, so this prototype intentionally uses
`src/fixtures/runtimeJobsMock.ts` until a queue index export, read-only localhost
bridge, or Tauri file access is available.

The adapter uses Vite `/@fs/...` URLs in development, never sends commands to
`HermesRuntime`, never opens a backend API, never writes runtime files, and does
not use WebSockets. If a browser or static hosting context blocks local file
access, the UI keeps working through fixture fallback data:

- `src/fixtures/runtimeHealthMock.ts`
- `src/fixtures/setupWatchMock.ts`
- `src/fixtures/runtimeJobsMock.ts`
- existing mock event data in `src/fixtures/controlCenterMockData.ts`

This means the React prototype can show real local JSON when reachable, but it
must always remain functional without those files.

Direct local JSON access is not fully reliable in a browser because static
builds, stricter dev servers, browser sandboxing, CORS rules, or missing files
can block `file` or `/@fs` reads. The current safe interim path is fixture
fallback with a small "Demo-/Fixture-Daten aktiv" hint in the UI instead of a
blocking error.

The later production path should be either:

- a tiny localhost-only read-only backend bridge that exposes approved JSON
  snapshots and recent JSONL events only, or
- Tauri file access with explicit local permissions.

Both future options must stay read-only from the Control Center perspective.
