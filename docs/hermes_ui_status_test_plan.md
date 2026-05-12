# Hermes UI Status Snapshot Test Plan

## Ziel

Dieser Testplan beschreibt den zentralen read-only Testablauf fuer den Hermes
UI Status Snapshot und die zugehoerigen Statusmodule. Die Tests dienen dazu,
die JSON-Ausgaben fuer spaetere Jarvis UI Panels zu pruefen, ohne Services zu
starten, Agenten auszufuehren oder Runtime-Dateien zu veraendern.

## Sicherheitsregeln

- Alle Befehle sind read-only Statuschecks.
- Keine Services starten oder stoppen.
- Keine Agenten ausfuehren.
- Keine Trading-Orders, keine cTrader-Anbindung und keine API-Keys verwenden.
- Keine Runtime-Schreibzugriffe auf `.hermes/`, `runtime/`, `logs/`, `memory/`,
  `data/` oder `obsidian/`.
- Warnungen, z. B. bei nicht erreichbarem Ollama, duerfen als JSON-Warnings
  erscheinen und sollen nicht zum Crash fuehren.

## Zentrale Testbefehle

```bash
python3 agents/core/hermes_ui_status.py
python3 agents/core/hermes_ui_status.py "Analysiere XAUUSD auf M15"
python3 agents/core/hermes_system_snapshot.py
python3 agents/core/hermes_agent_dashboard.py
python3 agents/core/hermes_runtime_status.py
python3 agents/core/hermes_learning_memory_status.py
python3 agents/core/hermes_developer_debug_status.py
python3 agents/core/hermes_voice_status.py
python3 agents/core/hermes_trading_panel_status.py
```

## Erwartete UI-Panels

Der zentrale UI-Status muss unter `ui_panels` mindestens diese Panels liefern:

- `chat_panel`
- `hermes_brain_panel`
- `agent_dashboard_panel`
- `runtime_control_panel`
- `learning_memory_panel`
- `developer_debug_panel`
- `voice_panel`
- `trading_panel`

## Erwartete Top-Level-Bereiche

`python3 agents/core/hermes_ui_status.py` soll eine JSON-Struktur mit diesen
zentralen Bereichen ausgeben:

- `generated_at`
- `brain`
- `agents`
- `runtime`
- `learning_memory`
- `developer_debug`
- `voice`
- `trading`
- `system_health`
- `ui_panels`

## Routing-Sample

Der Befehl:

```bash
python3 agents/core/hermes_ui_status.py "Analysiere XAUUSD auf M15"
```

soll eine Beispiel-Routingentscheidung enthalten. Erwartet wird, dass die
Trading-Anfrage im Hermes Brain Status als Trading-Kontext sichtbar wird und
das `trading_panel` weiterhin nur Planungs- und Sicherheitsdaten enthaelt.

## Trading Panel Erwartung

Das `trading_panel` muss sichtbar, aber nicht ausfuehrbar sein:

- `status`: `planned`
- `analysis_only`: `true`
- `no_auto_trading`: `true`
- `human_review_required`: `true`
- `can_execute`: `false`, falls im Panel vorhanden
- `supported_markets`: `XAUUSD`, `EURUSD`, `GER40`
- `planned_timeframes`: `HTF`, `MTF`, `LTF`
- `planned_patterns`: `Rejection`, `False Break`, `Engulfing`,
  `Morning Star`, `Evening Star`
- `prediction_feedback_learning.status`: `planned`
- `ctrader_integration.status`: `planned`

## Voice Panel Erwartung

Das `voice_panel` muss sichtbar sein, aber keine Audio-Hardware verwenden:

- Mikrofon nicht aktivieren.
- Kein Audio aufnehmen.
- Kein Wake Word starten.
- `planned_stack` mit geplanten Komponenten anzeigen.

## Akzeptanzkriterien

- Alle Befehle geben gueltiges JSON aus.
- Fehlende optionale Dienste werden defensiv als `warnings` gemeldet.
- Der UI-Status enthaelt alle erwarteten Panels.
- Der Testlauf schreibt keine Runtime-Dateien.
- Der Testlauf startet keine Services, Agenten, Audio-Pipelines oder Trading-
  Integrationen.
