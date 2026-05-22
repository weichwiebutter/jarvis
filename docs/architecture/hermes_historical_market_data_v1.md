# Hermes Historical Market Data Import Foundation v1

## Ziel

Hermes bekommt eine minimale lokale Grundlage, um historische Candle-Daten strukturiert abzulegen. Diese Daten sind fuer spaetere Replay-, Feature-Export-, Backtest- und Learning-Prozesse gedacht.

v1 ist bewusst nur ein Demo-/Fixture-Import:

- keine Broker-Trades
- keine Orders
- keine Live-Trading-Logik
- keine APIs oder WebSockets
- keine ML-Optimierung
- keine automatische Trading-Entscheidung

## Komponenten

- `MarketDataCandle`: einzelner OHLCV-Candle-Datensatz.
- `MarketDataImportJob`: lokaler Importauftrag mit Symbolen, Timeframes und Quelle.
- `HistoricalDataImportService`: erzeugt in v1 deterministische Demo-Candles und schreibt sie als JSONL.

## Unterstuetzte Maerkte

Startumfang:

- `XAUUSD`
- `EURUSD`
- `GER40`

Start-Timeframes:

- `H4`
- `H1`
- `M15`
- `M5`

## Speicherstruktur

```text
data/
  market_data/
    candles/
      XAUUSD/
        H4.candles.jsonl
        H1.candles.jsonl
        M15.candles.jsonl
        M5.candles.jsonl
      EURUSD/
      GER40/
```

Die Struktur ist so angelegt, dass spaeter cTrader-Exportdaten importiert oder normalisiert werden koennen, ohne die Feature-/Replay-Schicht neu zu schneiden.

## Candle-Format

JSONL, eine Candle pro Zeile:

```json
{"timestamp_utc":"2026-05-22T08:00:00+00:00","open":2392.4,"high":2397.2,"low":2389.8,"close":2394.1,"volume":1214,"symbol":"XAUUSD","timeframe":"M15"}
```

Felder:

- `timestamp_utc`
- `open`
- `high`
- `low`
- `close`
- `volume`
- `symbol`
- `timeframe`

## Events

Der Import publiziert Runtime Events:

- `HistoricalImportStarted`
- `HistoricalImportCompleted`

Beide Events tragen Safety-Kontext:

- `noAutoTrading = true`
- `humanReviewRequired = true`

## CLI

Read-only Anzeige:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- market-data
```

Die CLI liest nur lokale JSONL-Dateien unter `data/market_data/candles/` und zeigt Dateipfade, Zeilenanzahl und letzte Candle-Zeilen an.

## Safety

Historical Market Data v1 ist reine Datenvorbereitung:

- keine Orderausfuehrung
- keine Broker-Anbindung
- keine Live-Signale
- keine Strategie-Freigabe
- keine Risikoerhoehung
- keine automatische Lernuebernahme

`no_auto_trading` bleibt weiterhin aktiv und `human_review_required` bleibt Grundlage fuer alle spaeteren Trading-/Learning-Schritte.

## Spaetere Erweiterungen

Moegliche naechste Schritte:

- cTrader CSV/Export-Parser
- Symbol-/Timeframe-Normalisierung
- Candle-Deduplizierung
- Import-Manifeste mit Hashes
- Datenqualitaetspruefung
- ReplayManifest-Verknuepfung
- FeatureExport direkt aus gespeicherten Candles
- Storage-Retention-Anbindung

Diese Erweiterungen sollen read-only/importorientiert bleiben, bis explizit eine separate Trading-Ausfuehrungsphase freigegeben wird.
