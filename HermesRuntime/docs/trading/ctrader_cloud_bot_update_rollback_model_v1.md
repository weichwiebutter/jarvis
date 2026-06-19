# cTrader Cloud Bot Update & Rollback Model V1

## 1. Grundprinzip

HermesRuntime bleibt die Release Authority.
cTrader Cloud führt nur eine geprüfte Bot-Version mit eingebettetem Release Package aus.
Updates erfolgen durch eine neue Bot-Version oder ein neues eingebettetes Package.
Im Cloud-Betrieb gibt es keine Abhängigkeit von lokalen Bundle-Dateien.

## 2. Versionierungsbegriffe

- `bot_version`: Version der cTrader-Bot-Auslieferung, z. B. `0.1.0-paper`.
- `bot_release_id`: Eindeutige Release-ID des eingebetteten Pakets.
- `strategy_package_version`: Version des zugrunde liegenden Strategy/Signal-Pakets.
- `embedded_package_checksum`: Prüfsumme des eingebetteten Cloud-Pakets.
- `previous_bot_version`: zuletzt aktivierte Bot-Version vor dem Update.
- `rollback_bot_version`: Bot-Version, auf die im Fehlerfall zurückgerollt werden kann.
- `deployed_at`: Zeitpunkt der Cloud-Aktivierung.
- `generated_at`: Zeitpunkt der Erzeugung durch HermesRuntime.
- `human_review_status`: Freigabestatus durch den Menschen, z. B. `pending`, `approved`, `rejected`.
- `release_mode`: Muss für V1 `paper_only` sein.

## 3. Update-Ablauf

HermesRuntime:
1. erzeugt `cloud_embedded_release_package.json`
2. führt Human Review durch
3. bereitet neue cBot-Version oder einen Parameter-Snapshot vor
4. führt Guard, Preflight und Harness aus
5. stoppt die aktuelle cTrader Cloud Instance
6. deployed die neue Version
7. startet die Cloud Instance neu
8. prüft `bot_runtime_summary`
9. behält die alte Version als Rollback-Kandidat

## 4. Rollback-Ablauf

Rollback ist nur erlaubt, wenn:
- die vorherige Version `paper_only` war
- Checksums bekannt sind
- Safety Flags strikt sind
- keine neuen Rechte aktiviert werden
- der Rollback-Grund dokumentiert wird

Rollback-Ablauf:
1. aktuelle Cloud Instance stoppen
2. vorherige Bot-Version bzw. vorheriges Embedded Package aktivieren
3. Cloud Instance starten
4. Runtime Summary prüfen
5. Rollback-Ereignis dokumentieren

## 5. Update-Gates

Vor einem Update müssen erfüllt sein:
- `release_mode = paper_only`
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `forbidden_capabilities` vollständig
- Guard Script PASS
- Preflight PASS
- Harness PASS
- `embedded_checksum` vorhanden
- `human_review_status` vorhanden

## 6. Blocker

Ein Update ist blockiert bei:
- Safety-Flag-Verletzung
- fehlendem `embedded_checksum`
- Harness FAIL
- Guard FAIL
- Preflight FAIL
- unbekannter `bot_version`
- fehlendem Rollback-Kandidaten
- Drift `blocking` oder `high`
- `release_mode` ungleich `paper_only`

## 7. Cloud Runtime Monitoring

Nach dem Update wird geprüft:
- `bot_runtime_summary`
- `kill_switch_active=false`
- `broker_action=none`
- `paper_decision` nicht `blocked_by_config`
- keine Safety-Verletzung
- Cloud Instance läuft

## 8. Artefakte

Geplante Artefakte:
- `cloud_bot_update_manifest.json`
- `cloud_bot_update_notes.md`
- `cloud_bot_rollback_plan.md`
- `cloud_bot_deployment_log.jsonl`
- `cloud_bot_runtime_acceptance_check.md`

## 9. Safety

Immer:
- `broker_action=none`
- keine Orders
- keine cTrader Order API
- keine Demo-/Live-Orders
- kein automatischer Live-Modus

## 10. Offene Punkte

- Wird das Embedded Package als cBot-Code-Konstante oder als cBot-Parameter übernommen?
- Wie wird `bot_runtime_summary` aus cTrader Cloud exportiert?
- Wie lange werden Rollback-Versionen aufbewahrt?
- Wie wird Human Review technisch markiert?
- Brauchen wir signierte Embedded Packages?

