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

## Sicherheitsprinzipien

- Human-in-the-loop fuer riskante Aktionen und Ausfuehrungsschritte.
- Keine automatischen Commits oder Pushes.
- Keine Runtime-Daten im Git.
- Keine automatischen Trades.
- Statusmodule sind read-only und starten keine Services.
- Trading bleibt Analyse/Alerts only; Orders und Broker-Anbindungen sind nicht
  implementiert.

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
