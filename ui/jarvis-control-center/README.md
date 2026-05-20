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
- future runtime JSONL event files under `HermesRuntime/data/events/runtime/`

`RuntimeHealthPanel` and `SetupWatchPanel` both load through
`loadRuntimeData()` from the adapter. The normalized shape is:

```ts
{
  runtimeHealth,
  setupWatches,
  dataSource: 'live_file' | 'fixture' | 'unavailable',
  warnings,
}
```

The adapter uses Vite `/@fs/...` URLs in development, never sends commands to
`HermesRuntime`, never opens a backend API, never writes runtime files, and does
not use WebSockets. If a browser or static hosting context blocks local file
access, the UI keeps working through fixture fallback data:

- `src/fixtures/runtimeHealthMock.ts`
- `src/fixtures/setupWatchMock.ts`
- existing mock event data in `src/fixtures/controlCenterMockData.ts`

This means the React prototype can show real local JSON when reachable, but it
must always remain functional without those files.

Direct local JSON access is not fully reliable in a browser because static
builds, stricter dev servers, browser sandboxing, CORS rules, or missing files
can block `file` or `/@fs` reads. The current safe interim path is fixture
fallback with a small "Demo-/Fixture-Daten aktiv" hint in the UI instead of a
blocking error.

The later production path should be either:

- a tiny read-only backend bridge that exposes approved JSON snapshots only, or
- Tauri file access with explicit local permissions.

Both future options must stay read-only from the Control Center perspective.
