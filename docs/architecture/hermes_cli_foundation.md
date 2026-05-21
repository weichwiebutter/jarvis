# Hermes CLI Foundation

Status: implemented as a small read-only foundation under `HermesRuntime/cli/`.

## Ziel

Die Hermes CLI soll lokale Runtime-Artefakte sichtbar machen, ohne HermesRuntime zu starten, zu stoppen oder zu veraendern. Sie ist ein Diagnose- und Sichtbarkeitswerkzeug fuer lokale Dateien.

## Scope v1

Unterstuetzte Kommandos:

- `hermes health`
- `hermes setup-watch`
- `hermes events recent`
- `hermes jobs`
- `hermes storage`
- `hermes version`

Start ohne Installation:

```bash
cd HermesRuntime
dotnet run --project ./cli/Hermes.Cli.csproj -- health
```

Aus dem Repo-Root:

```bash
dotnet run --project ./HermesRuntime/cli/Hermes.Cli.csproj -- health
```

## Read-only Prinzip

Die CLI liest nur lokale Dateien unter `HermesRuntime/data/` und `HermesRuntime/config/`.

Sie macht nicht:

- Runtime starten,
- Runtime stoppen,
- Dateien loeschen,
- Jobs ausfuehren,
- Worker starten,
- APIs bereitstellen,
- WebSockets oeffnen,
- Trading-Kommandos senden,
- Broker- oder cTrader-Verbindungen oeffnen.

## Gelesene Artefakte

- `data/reports/runtime_health.json`
- `data/setup_watch/setup_watch.json`
- `data/events/runtime/*.runtime.jsonl`
- `data/jobs/*/*.job.json`
- `config/storage.profile.json`
- `Hermes.Runtime.csproj`

## Safety-Anzeigen

`hermes health` zeigt explizit:

- `no_auto_trading`
- `human_review_required`
- `safe_mode`
- Queue-Status
- aktiven Setup-Watch-Status

Jedes Kommando gibt einen Safety-Hinweis aus, dass die CLI read-only ist und keine Trading-Ausfuehrung macht.

## Spaetere Erweiterungen

Moegliche spaetere Erweiterungen bleiben read-only:

- `--json` Ausgabe,
- Filter fuer Events,
- bessere Tabellenansicht,
- Export eines lokalen Diagnose-Snapshots ohne Secrets,
- Integration in das React Control Center als dokumentierter Bedienpfad.

Nicht fuer v1:

- Schreibkommandos,
- Start/Stop/Reload,
- Queue-Ausfuehrung,
- Auto-Trading,
- Agent-Orchestrierung,
- Remote-Zugriff.
