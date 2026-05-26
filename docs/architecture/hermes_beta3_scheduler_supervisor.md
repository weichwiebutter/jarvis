# Hermes Beta 3 Scheduler & Supervisor Foundation

Status: Foundation v1, keine Trading-Ausfuehrung.

## Ziel

Windows soll langfristig nur noch den Hermes Supervisor starten. Die eigentlichen Hermes-Zeitplaene liegen in `HermesRuntime/config/schedules.json` und koennen ohne neue Windows Scheduled Tasks angepasst werden.

Zielarchitektur:

```text
Windows Autostart / ein Windows Task
-> WSL start_supervisor.sh
-> hermes supervisor-start
-> HermesInternalScheduler liest config/schedules.json
-> bekannte interne Hermes-Jobs werden kontrolliert gestartet
```

## Komponenten

- `HermesSupervisor`: langlebiger Prozess, schreibt Heartbeat und State, prueft Stop-Requests.
- `HermesInternalScheduler`: liest `config/schedules.json`, berechnet naechste Starts und fuehrt nur erlaubte interne Jobtypen.
- `SupervisorProcessManager`: verwaltet Background-PID, Stale-PID-Erkennung und Logrotation.
- `SupervisorHeartbeat`: aktueller Lebenszeichen-Status fuer Control Center/CLI.
- `HermesSupervisorState`: persistenter Supervisor-State unter `/mnt/d/HermesData/reports/supervisor/supervisor_state.json`.
- `ScheduledJobState`: persistenter Scheduler-State unter `/mnt/d/HermesData/reports/supervisor/scheduler_state.json`.

## Background Mode

Der bevorzugte Dauerbetriebsmodus ist:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --background
```

Der Background-Start:

- startet denselben Supervisor detached/nohup-artig.
- kehrt sofort zurueck.
- schreibt die PID nach `/mnt/d/HermesData/reports/supervisor/supervisor.pid`.
- schreibt Logs nach `/mnt/d/HermesData/logs/supervisor.log`.
- rotiert `supervisor.log`, wenn die Datei groesser als 50 MB ist.
- startet keinen zweiten Supervisor, wenn ein aktiver PID/Heartbeat erkannt wird.
- erkennt stale PID-Dateien nach Crash/Reboot und ueberschreibt sie beim naechsten Start.

Der Foreground-Modus bleibt fuer Debugging erhalten:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --max-runtime-minutes 5
```

## Erlaubte Jobtypen

Der Scheduler erlaubt keine freien Shell-Kommandos aus Config. `command` ist nur eine lesbare Zuordnung zu bekannten internen Hermes-Aktionen.

Erlaubt:

- `nightly_beta3_research`
- `storage_hygiene`
- `research_insights`
- `health_snapshot`
- `market_data_refresh`
- `strategy_discovery`
- `walkforward_validation`

Nicht erlaubt:

- freie Shell-Kommandos
- beliebige `dotnet`-Kommandos
- externe Programme
- Order-/Trading-Kommandos

## Config

Aktive Config:

- `HermesRuntime/config/schedules.json`

Vorlage:

- `HermesRuntime/config/schedules.example.json`

Beispieljobs:

- `nightly_beta3_research`: Fenster `23:00-05:00`, ruft intern die bestehende Nightly-Beta3-Orchestrierung auf.
- `storage_hygiene`: taeglich `05:15`, erzeugt nur sicheren Cleanup-Plan.
- `research_insights`: taeglich `05:30`, aktualisiert Research Insights.
- `health_snapshot`: alle 60 Minuten, schreibt ResourceGuard-Status.
- `market_data_refresh`: initial deaktiviert.

## Safety

Vor jedem Job:

- `ResourceGuard` pruefen.
- `StorageHygieneService` fuer Storage-Status/Cleanup-Plan nutzen.
- Bei kritischer Last Jobs pausieren oder ueberspringen.
- Keine aggressiven Retries.
- Keine Trading-Ausfuehrung.
- `no_auto_trading=true`.
- `human_review_required=true`.

Beta-3-Architekturregeln:

- Masterplan/TODO zuerst beachten.
- Bestehende Supervisor-/Scheduler-Architektur erweitern, nicht ersetzen.
- Keine unnoetigen Refactors.
- Keine Parallel-Systeme fuer Scheduler, Supervisor, Storage oder Reporting.
- Bestehende CLI-Kommandos, Configs und Reports kompatibel halten.
- Jede neue Funktion muss Dauerbetrieb, Recovery, ResourceGuard,
  StorageHygiene, Logging und technische Schuld beruecksichtigen.
- Keine isolierten Trading-Hacks; Trading bleibt in Research, Learning,
  Safety und Review eingebettet.

Future Trading Control Layer:

- Auto-Trading Toggle
- Paper/Demo Mode
- Risk Limits
- Volume- / Lot-Limits
- Strategy Whitelist
- Symbol Whitelist
- Emergency Stop

## Windows/WSL

Neuer bevorzugter Launcher:

- `scripts/nightly/start_supervisor.sh`

Windows Task Installation:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/windows/install_supervisor_task.ps1
```

Der Task startet `wsl.exe` und ruft `~/jarvis/HermesRuntime/scripts/nightly/start_supervisor.sh` auf. Bestehende Nightly-Beta3-Start/Stop-Skripte bleiben kompatibel, sollen aber langfristig durch den Supervisor-Weg ersetzt werden.

## CLI

Neue Befehle:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- scheduler-status
dotnet run --project ./cli/Hermes.Cli.csproj -- scheduler-jobs
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-status
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --max-runtime-minutes 5
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --background
dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-stop-request
```

## Kompatibilitaet

Die bestehende Nightly-Beta3-Architektur wird nicht ersetzt. Der Supervisor ruft fuer `nightly_beta3_research` weiterhin die vorhandene `run-nightly-beta3`-Orchestrierung auf und nutzt deren Checkpoints, Stop-Request-Datei, ResourceGuard-Integration und Reports weiter.
