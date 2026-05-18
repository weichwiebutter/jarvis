# Hermes UI Status Refactor Notes

Status: Refactor-Notiz / Planung  
Scope: `agents/core/hermes_ui_status.py`  
Stand: 18. Mai 2026

## Zweck der Datei

`hermes_ui_status.py` baut den zentralen read-only UI-Status fuer Jarvis und
Hermes. Die Datei sammelt Statusdaten aus Hermes-, Jarvis-, Runtime-, Voice-,
Trading-, Learning-, Foundation- und Debug-Modulen und formt daraus ein
einheitliches JSON fuer CLI, Gradio Dev/Test UI und spaetere Control-Center-
Panels.

Wichtig: Das Modul soll Status aggregieren, aber keine Services starten, keine
Runtime-Loops ausloesen, keine Runtime-Dateien schreiben und keine externen
APIs aufrufen.

## Warum die Datei gross geworden ist

Die Datei ist gewachsen, weil immer mehr Foundation- und UI-Panels direkt in
einem zentralen Aggregator sichtbar gemacht wurden:

- defensive Imports pro Statusmodul
- Fallback-Objekte pro Statusbereich
- Status-Builder-Wrapper pro Modul
- Panel-Mapper pro UI-Panel
- zentrale Top-Level-JSON-Struktur
- CLI-Ausgabe fuer schnelle Tests

Diese Struktur war pragmatisch und sicher fuer inkrementelle Foundation-Arbeit,
erzeugt aber Boilerplate und lange Parameterlisten.

## Bereits abgesichert

### Schema-Test

`tests/test_hermes_ui_status_schema.py` dokumentiert die erwartete
Mindeststruktur:

- wichtige Top-Level-Keys
- `ui_panels` als Dict
- alle zentralen Panelnamen
- `system_health.warnings`
- Trading-Aufruf mit `build_hermes_ui_status("Analysiere XAUUSD auf M15")`

Damit koennen spaetere Refactors schneller erkennen, wenn Keys oder Panels
versehentlich verschwinden.

### Defensive Helper

Ein interner Helper `_call_status_builder(...)` wurde eingefuehrt, um
wiederholte defensive Import-/Build-/Fallback-Logik fuer mehrere Foundation-
Statusmodule zu reduzieren.

Aktuell davon abgedeckt:

- `cost_optimization`
- `skill_generator`
- `mcp_tools`
- `reflective_learning`
- `trading_intelligence`
- `foundation_registry`

### Schrittweiser Refactor

Der bisherige Refactor wurde bewusst klein gehalten:

- keine JSON-Struktur geaendert
- keine Top-Level-Keys entfernt
- keine Panelnamen geaendert
- keine CLI-Funktion geaendert
- keine Produktivlogik ausgelagert

## Warum jetzt nicht aggressiv weiter refactoren

`hermes_ui_status.py` ist ein zentraler Vertrag zwischen Statusmodulen, Gradio
Dev/Test UI und spaeterem Jarvis Control Center. Ein grosser Umbau koennte
stille Regressionen erzeugen, selbst wenn Python-Syntax und einfache Tests
weiterhin funktionieren.

Aktuell ist wichtiger:

- JSON-Kompatibilitaet stabil halten
- Panelnamen stabil halten
- Refactor-Schritte einzeln pruefbar machen
- Foundation-Status weiter nutzbar halten
- spaetere UI-Abhaengigkeiten nicht brechen

## Zukuenftige Schritte

### 1. Panel Registry

Eine interne Panel Registry koennte Panelnamen, Formatter-Funktionen und
Statusquellen zentral beschreiben. Dadurch muessten neue Panels nicht mehr an
mehreren Stellen manuell eingetragen werden.

### 2. Zentralisierte Warning-Sammlung

Warnings sollten aus allen Statusbereichen einheitlich gesammelt werden, statt
pro Statusgruppe manuell in `system_health["warnings"]` zusammengefuehrt zu
werden.

Ziel:

- ein Helper fuer Warning-Merge
- Deduplizierung an einer Stelle
- Foundation-Warnings global sichtbar machen

### 3. Foundation Registry driven Panels

`hermes_foundation_registry.py` kann spaeter als Quelle fuer Foundation-Panels
dienen:

- `key`
- `source_module`
- `ui_panel_name`
- `safety_level`
- `planned_capabilities`

Die Foundation-Panels koennten dadurch registry-driven aufgebaut werden, ohne
jedes neue Foundation-Modul manuell in viele Funktionen einzubauen.

### 4. Auslagerung nach `hermes_ui_panels.py`

Panel-Formatter koennten in ein separates Modul wandern:

- `_build_chat_panel`
- `_build_runtime_control_panel`
- `_build_trading_intelligence_panel`
- `_build_foundation_registry_panel`
- weitere Panel-Mapper

`hermes_ui_status.py` bliebe dann staerker ein Orchestrator.

### 5. Status Registry

Eine Status Registry koennte Modulname, Builder-Funktion, Fallback-Funktion und
Top-Level-Key pro Statusbereich definieren. Das wuerde die wiederkehrende
Build-Logik weiter reduzieren.

## Risiken

### JSON-Kompatibilitaet

Bestehende UI- und CLI-Verbraucher koennen konkrete Keys erwarten. Entfernte
oder umbenannte Keys waeren ein stiller Breaking Change.

### UI-Abhaengigkeiten

Die Gradio Dev/Test UI und spaetere Control-Center-Panels koennen Panelnamen
oder verschachtelte Felder direkt referenzieren.

### Stille Key-Regressionen

Ein Refactor kann erfolgreich laufen und trotzdem Felder verlieren, wenn nur
Syntax oder CLI-Ausgabe getestet wird. Deshalb ist der Schema-Test vor weiteren
Umbauten wichtig.

## Empfohlene Reihenfolge

1. Schema-Test weiter ausbauen, bevor groessere Strukturveraenderungen kommen.
2. Warning-Merge zentralisieren.
3. Foundation-Statusmodule registry-driven bauen.
4. Foundation-Panels registry-driven rendern.
5. Panel-Formatter nach `hermes_ui_panels.py` auslagern.
6. Optional eine Status Registry fuer alle Statusbereiche einfuehren.

Jeder Schritt sollte einzeln erfolgen und danach mindestens den Schema-Test,
die UI-Status-CLI und den Trading-Task-Aufruf ausfuehren.
