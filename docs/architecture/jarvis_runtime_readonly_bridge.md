# Jarvis Runtime Read-only Bridge

Status: implemented foundation in `HermesRuntime`.

## Ziel

Das React Jarvis Control Center liest Hermes Runtime Reports ueber eine kleine
localhost Bridge. Die UI greift nicht direkt auf `/mnt/d/HermesData` zu und
erhaelt keine Schreib-, Runtime- oder Trading-Kommandos.

## Architektur

```text
Hermes Runtime Reports
-> HermesReadOnlyBridge
-> definierte localhost GET-Endpunkte
-> React Runtime Data Adapter
-> Jarvis Control Center Panels
```

CLI-Start:

```bash
cd HermesRuntime
dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge
```

Default URL:

```text
http://127.0.0.1:8787
```

## Response-Modell

Alle Bridge-Antworten verwenden `BridgeResponseModel`:

```json
{
  "status": "available",
  "data_source": "readonly_bridge",
  "timestamp_utc": "2026-05-26T00:00:00Z",
  "no_auto_trading": true,
  "human_review_required": true,
  "data": {},
  "warnings": []
}
```

## Endpunkte v1

Alle Endpunkte sind `GET` und read-only:

- `GET /bridge/health`
- `GET /reports`
- `GET /operator/dashboard`
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
- `GET /reports/regime-distribution`

## Whitelist Reports

Die Bridge liest nur fest verdrahtete, bekannte Reportpfade unter dem
konfigurierten Hermes Data Root:

- `reports/runtime_health.json`
- `setup_watch/setup_watch.json`
- `reports/supervisor/supervisor_state.json`
- `reports/supervisor/scheduler_state.json`
- `reports/resource/resource_status.json`
- `reports/storage/storage_status.json`
- `reports/storage/cleanup_plan.json`
- `reports/nightly_beta3/nightly_state.json`
- `strategy_research/research_insights.json`
- `strategy_research/robust_strategies.json`
- `strategy_research/overfit_report.json`
- `reports/regimes/regime_summary.json`
- `reports/regimes/strategy_regime_performance.json`
- `reports/regimes/regime_distribution.json`

Fehlende Reports liefern `status: unavailable` mit Warning, nicht eine
schreibende Recovery-Aktion.

## Sicherheitsprinzipien

- Nur localhost.
- Nur `GET` und `OPTIONS`.
- Keine POST/PUT/PATCH/DELETE-Endpunkte.
- Keine freien Shell-Kommandos.
- Keine Runtime-Start-/Stop-Kommandos.
- Keine Trading-, Broker- oder cTrader-Aktions-Endpunkte.
- Keine arbitrary file reads.
- Pfade werden gegen den konfigurierten Data Root normalisiert und begrenzt.
- JSON-Schluessel mit Secret-/Token-/Passwort-Bezug werden vor Ausgabe redacted.
- `no_auto_trading` und `human_review_required` bleiben in jeder Antwort sichtbar.

## UI-Integration

`ui/jarvis-control-center/vite.config.js` zeigt nicht mehr direkt auf
Vite-`/@fs`-Dateipfade. Der React Runtime Data Adapter nutzt die Bridge-URLs und
faellt bei fehlender Bridge oder fehlenden Reports auf Fixtures zurueck.

Die Bridge bleibt Monitoring-/Research-Infrastruktur. Zukuenftige
Command-/Trading-Kontrollen muessen als getrennte, approval-aware Schicht geplant
werden.
