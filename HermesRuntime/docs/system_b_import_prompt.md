# System B Import Prompt V1

## Zweck

Diese Anleitung beschreibt, wie das von System A exportierte System-B-Handoff-Bundle auf dem Deutschland-PC / Nous Hermes Agent gelesen, validiert und in der Signal-UI angezeigt wird.

System B ist ein Anzeige- und Review-System. Es handelt niemals.

## Voraussetzungen auf System B

- Lokaler read-only Zugriff auf den Bundle-Ordner
- JSON-Parsing für `ensemble_signal_agent_package.json`
- JSON-Schema-Validierung gegen `ensemble_signal_agent_package.schema.json`
- Anzeige von Signal-Setups in einer bestehenden Placeholder-UI
- Keine Broker-Anbindung, keine Order-Funktionen, keine cTrader Order API

## Zu kopierende Dateien

Kopiere den kompletten Bundle-Ordner unverändert:

- `README.md`
- `ensemble_signal_agent_package.json`
- `ensemble_signal_agent_package.schema.json`
- `system_b_signal_agent_export_contract.md`
- `portfolio_summary.json`
- `portfolio_summary.md`
- `bundle-manifest.json`
- `IMPORT_INSTRUCTIONS.md`

## Empfohlener Zielpfad auf System B

Empfohlenes Zielverzeichnis:

- `~/NousHermes/data/system_b_handoff/system_b_handoff_bundle/`

Alternativ kann jeder lokal lesbare Ordner verwendet werden, solange die Bundle-Struktur unverändert bleibt.

## Validierung

System B soll zuerst das JSON gegen das Schema validieren:

1. `ensemble_signal_agent_package.schema.json` laden
2. `ensemble_signal_agent_package.json` laden
3. Pflichtfelder prüfen
4. Sicherheitsflags prüfen
5. Readiness pro Asset prüfen

Erlaubte Paket- und Setup-Readiness:

- `portfolio_ready`
- `signal_ready`
- `setup_ready`
- `bot_ready`

Nur als Warnung anzeigen:

- `needs_more_validation`
- `data_ready_only`
- `missing_data`
- `quote_mapping_pending`

## UI-Befüllung

### Signal-UI

Für jedes Setup:

- Asset → `asset`
- Timeframe → `timeframe`
- Setup → `setup_name`
- Direction → `direction`
- Entry-Level / Entry-Zone → `entry_logic`
- Stop-Loss → `stop_loss_logic`
- Take-Profit → `take_profit_logic`
- Invalidation → `invalidation_logic`
- Confidence → `confidence_baseline`
- Status → `readiness`

### Ensemble-/Portfolio-UI

Zeige:

- Paket geladen
- Package Version
- Assets im Paket
- Setup Count je Asset
- Primary Setup je Asset
- Backup Candidates
- Human Review Required

### Safety Panel

Immer sichtbar:

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `research_only=true`

## Welche Assets angezeigt werden sollen

Anzeigen:

- `GER40`
- `XAUUSD`
- `EURUSD`

Darstellung:

- `GER40` als `portfolio_ready` / `bot_ready`
- `XAUUSD` als `portfolio_ready` / `bot_ready`
- `EURUSD` als `needs_more_validation` Warnung

## Umgang mit EURUSD

EURUSD ist aktuell nicht vollständig freigegeben.
System B soll:

- EURUSD anzeigen
- als `needs_more_validation` markieren
- keine Handelsfunktion anbieten
- keine Priorisierung gegenüber GER40 oder XAUUSD vornehmen

## Sicherheitsregeln

System B darf ausschließlich anzeigen und prüfen.

Verboten:

- keine Orders
- keine Broker-Funktion
- keine cTrader Order API
- keine Demo-Orders
- keine Live-Orders
- keine automatische Ausführung
- keine Order-Buttons

## Fehlverhalten / Fallback

- Wenn das JSON fehlt: `package_missing`
- Wenn das JSON ungültig ist: `package_invalid`
- Wenn einzelne Setup-Felder fehlen: Warnung anzeigen, Paket nicht abbrechen
- Wenn `quote_mapping_pending` vorkommt: Warnung anzeigen, nicht blockieren

## Copy/Paste Prompt für Nous Hermes Agent

> Lade dieses lokale Trading-Signal-Paket read-only, validiere es gegen das Schema, zeige GER40 und XAUUSD als Signal-Setups an, markiere EURUSD als needs_more_validation, und biete keinerlei Handelsausführung an.

