# Jarvis / Hermes Project Overview

## Rollen

- Jarvis ist die UI-, Runtime-, Voice-, Status- und Control-Schicht.
- Hermes ist die Brain-, Router-, Planner-, Learning- und Delegationsschicht.
- Ollama stellt lokale Modelle bereit.
- Externe Provider duerfen nur ueber explizite Provider-Layer genutzt werden.

## Aktueller Hermes/Jarvis-Stand

- Hermes Router mit Adaptive Routing: routet Aufgaben nach Domain, Intent,
  Provider- und Modell-Empfehlung.
- Hermes Brain Status: liefert eine kompakte UI-freundliche Routing- und
  Sicherheitszusammenfassung.
- Agent Dashboard Status: beschreibt bekannte und geplante Agenten inklusive
  Capabilities und Safety Flags.
- Runtime Status: sammelt read-only Status zu Hermes, Ollama, Memory, Voice,
  Git und Runtime-Pfaden.
- System Snapshot: aggregiert Runtime, Agent Dashboard und optional eine
  Router-Beispielentscheidung.
- UI Status Snapshot: zentraler JSON-Status fuer spaetere Jarvis UI Panels.
- Learning/Memory Status: liest vorhandene `.hermes/` Learning-, Routing- und
  Improvement-Strukturen defensiv aus.
- Developer/Debug Status: prueft Debug-Module und dokumentiert CLI-Checks.
- Voice Status: beschreibt den geplanten Voice Stack ohne Mikrofon- oder
  Audiozugriff.
- Trading Panel Status: beschreibt den geplanten Hermes Trading Analyst als
  Analyse-Panel ohne Trading-Automation.

## Schnellstart

### A. Python/Gradio Dev UI

Rolle: Gradio ist die lokale Dev/Test UI fuer Jarvis- und Hermes-Status,
manuelle Tests und bestehende Diagnose-Panels.

```bash
cd ~/jarvis
source .venv/bin/activate
python ui_app.py
```

Dann im Browser oeffnen:

```text
http://127.0.0.1:7860
```

Falls die Umgebung `venv/` statt `.venv/` nutzt:

```bash
source venv/bin/activate
```

### B. HermesRuntime

Rolle: HermesRuntime ist die lokale Runtime Foundation fuer Events, Snapshots,
Queue, Worker-Stubs, Setup-Watch-Demo, RuntimeHealth und JSON/JSONL-Ablage.

```bash
cd ~/jarvis/HermesRuntime
dotnet run --project ./Hermes.Runtime.csproj
```

### C. Hermes CLI

Rolle: Hermes CLI ist eine read-only Dev Console fuer lokale Runtime-Dateien.
Sie startet/stoppt die Runtime nicht und fuehrt keine Trading-Aktionen aus.

```bash
cd ~/jarvis/HermesRuntime
dotnet run --project ./cli/Hermes.Cli.csproj -- health
dotnet run --project ./cli/Hermes.Cli.csproj -- setup-watch
dotnet run --project ./cli/Hermes.Cli.csproj -- events recent
dotnet run --project ./cli/Hermes.Cli.csproj -- jobs
dotnet run --project ./cli/Hermes.Cli.csproj -- storage
dotnet run --project ./cli/Hermes.Cli.csproj -- version
```

### D. React Jarvis Control Center

Rolle: React ist der sichtbare Jarvis Control Center Prototype. Er nutzt
Mock-/Fixture-Daten und read-only lokale JSON-Zugriffe, wenn der Browser diese
im Dev-Modus erlaubt.

```bash
cd ~/jarvis/ui/jarvis-control-center
npm install
npm run dev
```

Dann im Browser oeffnen:

```text
http://127.0.0.1:5173
```

Falls Port `5173` belegt ist, zeigt Vite die tatsaechliche URL im Terminal.

## Sicherheitsprinzipien

- Human-in-the-loop fuer riskante Aktionen und Ausfuehrungsschritte.
- Keine automatischen Commits oder Pushes.
- Keine Runtime-Daten im Git.
- Keine automatischen Trades.
- Statusmodule sind read-only und starten keine Services.
- `no_auto_trading` bleibt aktiv.
- `human_review_required` bleibt aktiv.
- React UI ist read-only und sendet keine Runtime-Kommandos.
- Hermes CLI ist read-only und bietet keine Start-/Stop-/Delete-Kommandos.
- Keine Brokerverbindung und keine cTrader-Anbindung in der aktuellen Phase.
- Trading bleibt Analyse/Alerts only; Orders und Broker-Anbindungen sind nicht
  implementiert.
- Hermes CLI Foundation ist read-only: keine Runtime-Steuerung, keine
  Delete-Kommandos, keine Trading-Kommandos.

## Hermes CLI Foundation

Die erste lokale CLI liegt unter `HermesRuntime/cli/` und liest nur bestehende
Runtime-Dateien.

Beispiele:

```bash
cd HermesRuntime
dotnet run --project ./cli/Hermes.Cli.csproj -- health
dotnet run --project ./cli/Hermes.Cli.csproj -- setup-watch
dotnet run --project ./cli/Hermes.Cli.csproj -- events recent
dotnet run --project ./cli/Hermes.Cli.csproj -- jobs
dotnet run --project ./cli/Hermes.Cli.csproj -- storage
dotnet run --project ./cli/Hermes.Cli.csproj -- version
```

Die CLI startet HermesRuntime nicht. Sie liest `runtime_health.json`,
`setup_watch.json`, Event-JSONL, Job-Manifeste und Storage-Metadaten lokal und
zeigt Safety-Flags wie `no_auto_trading` und `human_review_required` sichtbar an.

## Relevante Testbefehle

```bash
python3 agents/core/hermes_ui_status.py
python3 agents/core/hermes_system_snapshot.py
python3 agents/core/hermes_router.py "Analysiere XAUUSD auf M15"
```

Weitere Status-Checks stehen in:

- `docs/hermes_ui_status_test_plan.md`
- `docs/hermes_trading_analyst_roadmap.md`
- `docs/architecture/system-roles.md`
