# cTrader Historical Data Import Plan V1

## Ziel
System A soll read-only historische cTrader-Kerndaten für spätere Analysen und Backtests importierbar machen:
- EURUSD
- XAUUSD
- GER40 / DE40

Zeitrahmen:
- M1
- M5
- M15
- H1

Dieser Plan beschreibt nur die Diagnose- und Zielarchitektur. Es wird kein großer Downloader implementiert.

## Nicht im Scope
- keine Orders
- kein Trading
- keine Broker-API
- keine Demo-/Live-Ausführung
- keine Optimierung der Strategien
- kein automatisches Scheduling großer Downloads

## Verfügbare Importpfade
HermesRuntime hat bereits read-only Import-/Diagnosepfade, die als Grundlage dienen können:
- `download-history`
- `import-ctrader-history`
- `import-csv`

Empfohlene Nutzung:
- `download-history` als kontrollierter read-only cTrader-Export/API-Pfad
- `import-csv` als lokaler Importpfad für manuell exportierte cTrader-Dateien
- `import-ctrader-history` als Alias für denselben historischen Importpfad

## Zielquelle
Für V1 sollte die Quelle priorisiert werden als:
1. cTrader Export / API, wenn read-only verfügbar
2. cTrader CSV-Export, wenn API nicht verfügbar ist
3. lokal bereitgestellte CSV/JSON-Dateien als Importfutter

## Zielspeicherort
Alle importierten historischen Datensätze sollen unter `/mnt/d/HermesData/datasets/` abgelegt werden.

Vorgeschlagene Struktur:

```text
/mnt/d/HermesData/datasets/
├── ctrader/
│   ├── EURUSD/
│   │   ├── M1/
│   │   ├── M5/
│   │   ├── M15/
│   │   └── H1/
│   ├── XAUUSD/
│   │   ├── M1/
│   │   ├── M5/
│   │   ├── M15/
│   │   └── H1/
│   └── GER40/
│       ├── M1/
│       ├── M5/
│       ├── M15/
│       └── H1/
```

Falls das Broker-/Plattform-Namensschema `DE40` statt `GER40` liefert, soll ein kanonischer Alias-Layer verwendet werden:
- `GER40`
- `DE40`
- `GER40.cash`

## Kanonische Dataset-Regel
Der Import soll die Daten nicht als Trading-Orderbasis behandeln, sondern als analytischen Candle-Datensatz.

Pro Dataset:
- asset
- canonical_asset
- timeframe
- source
- market
- date range
- candle count
- import status
- checksum / hash
- file path
- update timestamp

## Empfohlenes Dataset-Schema
### Metadata JSON
```json
{
  "dataset_id": "ctrader_eurusd_m5_20260708",
  "asset": "EURUSD",
  "canonical_asset": "EURUSD",
  "timeframe": "M5",
  "source": "ctrader_export",
  "market": "forex",
  "date_from": "2025-01-01T00:00:00Z",
  "date_to": "2026-01-01T00:00:00Z",
  "candle_count": 123456,
  "file_path": "/mnt/d/HermesData/datasets/ctrader/EURUSD/M5/ctrader_eurusd_m5_20260708.csv",
  "checksum_sha256": "…",
  "imported_at_utc": "2026-07-09T00:00:00Z",
  "last_updated_at_utc": "2026-07-09T00:00:00Z",
  "update_strategy": "append_or_replace_by_timeframe"
}
```

### Candle CSV
Empfohlene Felder:
- `timestamp_utc`
- `open`
- `high`
- `low`
- `close`
- `volume`
- `bid`
- `ask`
- `spread`

Falls cTrader nur OHLC liefert, darf `bid/ask/spread` leer bleiben.

## Update-Strategie
### V1-Regel
Der Import soll pro `asset + timeframe` deterministisch aktualisieren:
- neue Daten an das bestehende Dataset anhängen, wenn sie später als der letzte gespeicherte Zeitstempel sind
- überlappende Bereiche ersetzen, wenn cTrader denselben Zeitraum erneut exportiert
- keine Duplikate pro Timestamp zulassen
- keine Stillstände als Fehler behandeln, sondern als `no_new_data`

### Aktualisierungshäufigkeit
Empfehlung:
- tägliche oder manuelle Imports
- stündliche Imports nur für aktuell getestete Paare

### Retention
Ohne automatische Löschung.
Alte Versionen können archiviert werden, aber nicht automatisch entfernt.

## Import-Plan für Assets und Timeframes
### EURUSD
- M1
- M5
- M15
- H1

### XAUUSD
- M1
- M5
- M15
- H1

### GER40 / DE40
- M1
- M5
- M15
- H1

## cTrader-Quelle / API / CSV Auswahl
### Empfohlene Reihenfolge
1. Export aus cTrader als CSV
2. read-only API-Export, wenn sauber und verfügbar
3. bestehende lokale CSV-Dateien erneut importieren

### Erwartete Eingabeformate
- CSV mit Candle-Zeilen
- JSON-Export mit Candle-Array
- Plattform-spezifische Historienreports

## Speicher- und Metadatenstruktur
Empfohlene Begleitdateien pro Dataset:
- `dataset.json`
- `dataset.md`
- `dataset.csv`
- `dataset.sha256`

## Validierungsregeln
Beim Import prüfen:
- Asset passt zum Zielasset
- Timeframe ist eines der Ziel-Timeframes
- Timestamp-Reihenfolge ist monoton
- Keine Lücken innerhalb des exportierten Bereichs, sofern die Quelle vollständige Historie verspricht
- Keine Trading- oder Order-Abhängigkeit
- Kein Zugriff auf Live-/Demo-/Broker-Order-Surfaces

## Known Mappings
Für GER40 kann die Quelle je nach Exportform als eines der folgenden Labels erscheinen:
- `GER40`
- `DE40`
- `GER40.cash`

Für die Import-Pipeline sollte ein Alias-Mapping verwendet werden, damit dieselbe Marktidentität sauber erkannt wird.

## Offene Fragen
- Welcher cTrader-Export ist in der Zielumgebung tatsächlich verfügbar?
- Liefert die Plattform echte Bid/Ask/Spread-Felder oder nur OHLC?
- Muss `GER40` immer auf `DE40` gemappt werden?
- Soll der Import später als getrennte Dataset-Versionierung oder als Rolling-Update geführt werden?

## CLI-Referenz
Die vorhandenen Import-/Diagnosepfade werden für V1 als Grundlage genutzt:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol EURUSD --timeframe M5 --from 2025-01-01 --to 2025-01-02
dotnet run --project ./cli/Hermes.Cli.csproj -- import-ctrader-history --asset XAUUSD --timeframe H1 --from 2025-01-01 --to 2025-01-02
dotnet run --project ./cli/Hermes.Cli.csproj -- import-csv --symbol GER40 --timeframe M15 --file path/to/file.csv
```

## Safety
Der Import bleibt strikt read-only:
- no_auto_trading=true
- human_review_required=true
- broker_orders_enabled=false
- live_trading_enabled=false
- broker_action=none

## Ergebnis für V1
Für den nächsten Schritt ist ausreichend geklärt:
- welche Assets importiert werden
- welche Timeframes benötigt werden
- wo die Daten liegen sollen
- welches Schema die Daten tragen
- wie Updates ohne Trading-Risiko erfolgen

