# cTrader Cloud Embedded Export Flow V1

## Ziel
HermesPaperBot soll in cTrader Cloud lauffähig werden, ohne dass die Runtime dauerhaft von lokalen Release-Bundle-Dateien abhängt.

## Architekturentscheidung
- HermesRuntime bleibt die Release Authority.
- Der Cloud Bot nutzt `RuntimeMode=cloud_embedded_bundle`.
- Ein lokaler Bundle-Pfad ist für den Cloud-Betrieb nicht nötig.
- Ein dauerhafter HermesRuntime-Betrieb ist für die Cloud-Ausführung nicht erforderlich.

## Aktueller Input
Der Generator nutzt aktuell als Quelle:
- `system_b_handoff_bundle`

## Output-Artefakte
Der Export erzeugt:
- `/mnt/d/HermesData/reports/cloud_embedded_release_package/cloud_embedded_release_package.json`
- `/mnt/d/HermesData/reports/cloud_embedded_release_package/cloud_embedded_release_package.md`

## CLI
Der Export wird über folgende CLI ausgeführt:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- cloud-embedded-release-package
```

## Safety
Der Export ist strikt paper-only:
- `release_mode = paper_only`
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `broker_action=none`

## Einschränkungen
- keine Orders
- keine cTrader Order API
- kein Broker-Zugriff
- das embedded package ist noch nicht kryptografisch signiert
- `ctrader_bot_release_bundle` soll später als dedizierte Quelle priorisiert werden

## Kurzablauf
1. HermesRuntime liest das vorhandene Handoff-Bundle.
2. Der Export validiert Safety und Pflichtdaten.
3. Ein cloud-kompatibles Embedded Package wird erzeugt.
4. JSON- und Markdown-Artefakte werden lokal geschrieben.
5. cTrader Cloud kann daraus später ein eingebettetes Runtime-Paket verwenden.

## Offene Punkte
- Dedizierte Priorisierung von `ctrader_bot_release_bundle` als Eingabequelle
- Kryptografische Signierung des Embedded Packages
- Erweiterte Drift-/Compatibility-Prüfung für den Cloud-Betrieb
- Klare Rollback- und Update-Regeln für Cloud-Bot-Versionen

