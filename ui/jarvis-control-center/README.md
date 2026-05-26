# Jarvis Control Center Prototype

Separated React/Vite prototype for the future Jarvis Control Center.

The prototype is isolated from the existing Gradio test UI. It does not send
runtime commands, does not write Runtime files, does not connect to brokers, and
keeps future trading controls disabled as UI placeholders.

## Start

Start the read-only Bridge from `HermesRuntime` in one terminal:

```bash
cd HermesRuntime
dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge
```

Start the Control Center in another terminal:

```bash
cd ui/jarvis-control-center
npm install
npm run dev
```

The bridge defaults to:

```text
http://127.0.0.1:8787
```

Override it for Vite if needed:

```bash
HERMES_READONLY_BRIDGE_URL=http://127.0.0.1:8788 npm run dev
```

## Scope

- No trading execution
- No broker actions
- No shell/runtime commands from the UI
- No WebSockets
- No direct `/mnt/d/HermesData` browser file reads
- Read-only localhost Bridge for approved Hermes Runtime reports
- Fixture fallback when the Bridge or a report is unavailable

## Read-only Runtime Data Adapter

Runtime-facing reads are centralized in:

```text
src/data/runtimeDataAdapter.ts
```

The adapter talks to the Hermes Read-only Bridge instead of using Vite `/@fs`
access to Runtime folders. The UI receives wrapped Bridge responses and unwraps
the `data` field before normalizing it for panels.

Primary Bridge endpoints used by the prototype:

- `GET /runtime/health`
- `GET /runtime/setup-watch`
- `GET /runtime/supervisor`
- `GET /runtime/scheduler`
- `GET /runtime/resource`
- `GET /runtime/storage`
- `GET /runtime/cleanup-plan`
- `GET /runtime/nightly`
- `GET /reports/research-insights`
- `GET /reports/robust-strategies`
- `GET /reports/overfit-report`
- `GET /reports/regime-summary`
- `GET /reports/strategy-regime-performance`
- `GET /operator/dashboard`

## Beta 3 Operator Dashboard

The Operator Dashboard is a monitoring-first foundation for Supervisor,
Scheduler, ResourceGuard, StorageHygiene, Nightly and Research status.

Displayed areas:

- Supervisor Status
- Scheduler Status
- Resource Status
- Nightly Status
- Research Summary
- Report Viewer
- Storage / Logs
- disabled Safety Control placeholders for Auto-Trading, Demo/Paper Mode,
  Emergency Stop, Risk Limits, Strategy Whitelist and Symbol Whitelist

The placeholders do not start or stop Hermes, do not execute trades, do not
connect to brokers, and do not expose order actions.

## Fallbacks

If the Bridge is not running or a report has not been generated yet, the UI
falls back to fixtures and shows the normal "Demo-/Fixture-Daten aktiv" hint.
This keeps the prototype usable without starting Hermes Runtime or touching
Runtime data.

Fixture files include:

- `src/fixtures/runtimeHealthMock.ts`
- `src/fixtures/setupWatchMock.ts`
- `src/fixtures/runtimeJobsMock.ts`
- `src/fixtures/runtimeStorageMock.ts`
- `src/fixtures/operatorDashboardMock.ts`
- existing mock event data in `src/fixtures/controlCenterMockData.ts`

## Safety

The Control Center remains read-only:

- no POST/PUT/PATCH/DELETE calls,
- no arbitrary path reads,
- no direct browser access to `/mnt/d/HermesData`,
- no secrets displayed,
- no trading commands,
- no runtime start/stop commands.

Command or trading control paths must be added later as a separate,
approval-aware layer. They must not be mixed into this read-only bridge.
