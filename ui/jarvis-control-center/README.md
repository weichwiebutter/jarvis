# Jarvis Control Center Prototype

First separated React/Vite prototype for the future Jarvis Control Center.

This prototype is intentionally isolated from the existing Gradio test UI and from
`HermesRuntime`. It uses local mock data only.

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
- No direct reads from `HermesRuntime`

The Runtime Health panel uses `src/fixtures/runtimeHealthMock.ts`. The fixture
mirrors the shape of `HermesRuntime/data/reports/runtime_health.json` as example
data, but the current prototype does not read that file.
