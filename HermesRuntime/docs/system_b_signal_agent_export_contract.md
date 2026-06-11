# System B Signal Agent Export Contract V1

## Zweck

`ensemble_signal_agent_package.json` ist das read-only Exportformat von System A `HermesRuntime` für System B / Nous Hermes Agent.
Es liefert nur freigegebene, zertifizierte oder signal-freigegebene Scalping-Setups zur Anzeige, Prüfung und späteren UI-Darstellung.

System B zeigt Signale an. System B handelt niemals.

## Quelle und Ziel

- Quelle: System A / `HermesRuntime`
- Ziel: System B / Nous Hermes Agent
- Exportdatei: `ensemble_signal_agent_package.json`
- Standard-Exportpfad:
  - primär: `/mnt/d/HermesData/reports/scalping_portfolio/ensemble_export/`
  - fallback: `HermesRuntime/.codex_artifacts/reports/scalping_portfolio/ensemble_export/`

## Sicherheitsmodell

Das Paket ist strikt read-only.

Pflicht:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `research_only=true`

System B darf daraus nur Anzeige- und Review-UI befüllen.
Es darf keine Order-Schaltflächen, keine Broker-Aktionen und keine cTrader Order API auslösen.

## Paketstruktur

Top-level-Felder:
- `package_id`
- `generated_at`
- `package_version`
- `source_system`
- `status`
- `assets`
- `safety_flags`
- `no_auto_trading`
- `human_review_required`
- `broker_orders_enabled`
- `live_trading_enabled`
- `research_only`

Pro Setup innerhalb von `assets`:
- `asset`
- `readiness`
- `setup_id`
- `setup_name`
- `timeframe`
- `direction`
- `primary_candidate`
- `backup_candidates`
- `confidence_baseline`
- `signal_frequency`
- `entry_logic`
- `exit_logic`
- `stop_loss_logic`
- `take_profit_logic`
- `invalidation_logic`
- `market_regime_tags`
- `session_tags`
- `risk_notes`
- `human_review_required`
- `no_auto_trading`
- `broker_orders_enabled`
- `live_trading_enabled`

## Statusmodell

Erlaubte Paket-/Setup-Readiness-Werte:
- `portfolio_ready`
- `signal_ready`
- `setup_ready`
- `bot_ready`

Nicht als handelbar anzeigen:
- `needs_more_validation`
- `data_ready_only`
- `missing_data`
- `quote_mapping_pending`

Wichtig:
- Auch bei `bot_ready` bleibt System B read-only.
- `bot_ready` bedeutet nur: ausreichend stark für Anzeige / Review / spätere Freigabe.
- Keine automatische Order-Ausführung.

## Validierungsregeln

System B oder ein Validator soll prüfen:

1. JSON existiert.
2. `package_version` ist vorhanden.
3. `assets` enthält mindestens einen Eintrag.
4. Sicherheitsflags sind vollständig.
5. `broker_orders_enabled` ist `false`.
6. `live_trading_enabled` ist `false`.
7. `no_auto_trading` ist `true`.
8. `human_review_required` ist `true`.
9. Jeder Setup-Eintrag hat:
   - `asset`
   - `setup_id`
   - `timeframe`
   - `direction`
   - `primary_candidate`
   - `readiness`
   - `entry_logic`
   - `exit_logic`
   - `stop_loss_logic`
   - `take_profit_logic`
   - `invalidation_logic`

Wenn Felder fehlen:
- System B zeigt Warning.
- System B bricht nicht.
- System B markiert das Paket als `needs_validation` oder `partial`.

## UI-Mapping

### Signal Dashboard

Mappe aus dem Setup-Eintrag:
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

### Ensemble Status

Mappe aus dem Paket:
- Paket geladen → Datei erfolgreich geparst
- Package Version → `package_version`
- Assets im Paket → Anzahl der `assets`
- Setup Count → Anzahl der Setup-Einträge je Asset
- Primary Setups → `setup_id` pro Asset
- Backup Candidates → `backup_candidates`
- Human Review Required → `human_review_required`

### Safety Panel

Zeige immer:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `research_only=true`

## Fehler- und Fallback-Verhalten

- Wenn das JSON fehlt: System B zeigt `package_missing`.
- Wenn das JSON ungültig ist: System B zeigt `package_invalid`.
- Wenn einzelne Felder fehlen: System B zeigt die betroffenen Einträge mit Warnungen.
- Wenn das Paket nur in `.codex_artifacts` vorliegt: System B darf diese Fallback-Datei lesen.
- Wenn `quote_mapping_pending` existiert: das ist eine Warnung, kein Blocker für Anzeige.

## Erwartung an System B

System B soll:
- freigegebene Setups anzeigen
- Setup-Hierarchie darstellen
- Confidence / Readiness / Safety sichtbar machen
- keine Handelsaktionen anbieten
- keine Broker-Buttons rendern
- keine Live-Order-Funktion implementieren

