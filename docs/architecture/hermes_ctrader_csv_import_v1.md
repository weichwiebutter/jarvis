# Hermes cTrader CSV Import v1

## Ziel

Hermes kann historische Candle-CSV-Exporte lokal importieren und intern als JSONL ablegen. Der Import ist eine reine Datenvorbereitung fuer Replay, Feature Generation, Backtests und spaetere Learning-Prozesse.

Nicht enthalten:

- keine Orders
- keine Trading-Ausfuehrung
- keine Live-API
- keine Broker-Verbindung
- keine OAuth- oder Secret-Verarbeitung

## Komponenten

- `CTraderCsvCandleImporter`: liest lokale CSV-Dateien und schreibt normalisierte Candles.
- `MarketDataImportFormat`: markiert Importquellen wie `DemoFixture` oder `CTraderCsv`.
- `ImportValidationResult`: fasst Spaltenvalidierung, fehlerhafte Zeilen, Zeitraum und Warnungen zusammen.

## CLI

Beispiel:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- import-csv --symbol XAUUSD --timeframe M5 --file path/to/file.csv
```

Der Befehl:

- liest nur die angegebene lokale Datei
- validiert Header und Zeilen
- schreibt JSONL-Candles unter `data/market_data/candles/{symbol}/{timeframe}/`
- kopiert die Rohdatei optional nach `data/market_data/raw_imports/{symbol}/{timeframe}/`
- publiziert Runtime-Events in den lokalen EventStore

## Unterstuetzte CSV-Spalten

Zeitspalte, eine davon:

- `Time`
- `Timestamp`
- `Date`

Preis-/Volumenspalten:

- `Open`
- `High`
- `Low`
- `Close`
- `Volume` oder `TickVolume`

Die Header-Erkennung ignoriert Gross-/Kleinschreibung sowie Leerzeichen/Unterstriche.

## Speicherstruktur

```text
data/
  market_data/
    candles/
      XAUUSD/
        M5/
          ctrader_csv_<timestamp>_<guid>.candles.jsonl
    raw_imports/
      XAUUSD/
        M5/
          ctrader_csv_<timestamp>_<guid>.csv
```

Das interne Candle-Format bleibt JSONL mit:

- `timestamp_utc`
- `open`
- `high`
- `low`
- `close`
- `volume`
- `symbol`
- `timeframe`

## Validierung

Der Import meldet:

- fehlende Pflichtspalten
- ungueltige Zahlen
- ungueltige Zeitstempel
- Zeilenanzahl
- importierte Zeilen
- ungueltige Zeilen
- Zeitraum von/bis
- Future-Daten als Warning

Future-Daten blockieren den Import nicht, werden aber sichtbar gemacht.

## Events

Der Import publiziert:

- `HistoricalImportStarted`
- `HistoricalImportCompleted`
- `HistoricalImportFailed`

Alle Events behalten Safety-Kontext:

- `noAutoTrading = true`
- `humanReviewRequired = true`

## Integration

`FeatureGenerationService` liest weiterhin die alten Demo-Dateien unter:

```text
data/market_data/candles/{symbol}/{timeframe}.candles.jsonl
```

und zusaetzlich neue Importdateien unter:

```text
data/market_data/candles/{symbol}/{timeframe}/*.jsonl
```

Damit kann nach einem CSV-Import direkt ausgefuehrt werden:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- generate-features
```

## Safety

cTrader CSV Import v1 ist nur lokaler Dateiimport. Er kann keine Orders erzeugen, keine Broker-Kommandos senden und keine Live-Verbindung starten.
