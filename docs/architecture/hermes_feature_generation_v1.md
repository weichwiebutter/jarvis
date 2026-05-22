# Hermes Feature Generation v1

## Ziel

Hermes erzeugt erste einfache FeatureVectors aus lokal gespeicherten historischen Candle-Daten. Diese Schicht ist die Bruecke zwischen `data/market_data/candles/` und spaeteren Backtest-, Replay- und Learning-Prozessen.

v1 ist bewusst lokal und deterministisch:

- keine Broker-Anbindung
- keine Orders
- keine Live-Trading-Logik
- keine ML-Optimierung
- keine automatische Strategie-Freigabe

## Komponenten

- `FeatureGenerationJob`: beschreibt einen lokalen Feature-Generation-Lauf.
- `FeatureGenerationService`: liest Candle-JSONL-Dateien, berechnet einfache Features und schreibt neue Feature-JSONL-Dateien.
- `GeneratedFeatureVector`: v1-Feature-Zeile, abgeleitet aus einem Candle.

## Eingabe

Quelle:

```text
data/
  market_data/
    candles/
      XAUUSD/
      EURUSD/
      GER40/
```

Unterstuetzt werden:

- Symbole: `XAUUSD`, `EURUSD`, `GER40`
- Timeframes: `M5`, `M15`, `H1`, `H4`

## Feature-Schema v1

Eine Feature-Zeile enthaelt:

- `timestamp_utc`
- `symbol`
- `timeframe`
- `close`
- `simple_return`
- `candle_range`
- `body_size`
- `direction`
- `mock_session`
- `mock_regime`
- `mock_signal_score`

`mock_session`, `mock_regime` und `mock_signal_score` sind bewusst einfache Demo-Ableitungen. Sie sind keine produktiven Trading-Signale.

## Ausgabe

Feature-Dateien werden nach `data/exports/features/` geschrieben:

```text
data/
  exports/
    features/
      feature_generation_YYYYMMDDHHMMSSfff_<guid>.features.jsonl
```

Format: JSONL, eine Feature-Zeile pro Candle.

## Runtime-Flow

Der Runtime-Demo-Lauf fuehrt die Schritte lokal aus:

1. historische Demo-Candles erzeugen
2. FeatureVectors aus den Candle-Dateien generieren
3. ReplayManifest kann die neueste Feature-Datei referenzieren

Es wird kein Replay ausgefuehrt und keine Trading-Aktion gestartet.

## Events

Feature Generation v1 publiziert:

- `FeatureGenerationStarted`
- `FeatureGenerationCompleted`

Beide Events behalten den Safety-Kontext:

- `noAutoTrading = true`
- `humanReviewRequired = true`

## CLI

Feature-Generation lokal starten:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- generate-features
```

Letzte Feature-Datei read-only anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- features
```

`hermes features` erkennt sowohl alte Demo-Feature-Exports als auch neue aus MarketData generierte Features.

## Safety

Feature Generation v1 ist reine Datenverarbeitung:

- keine Orderausfuehrung
- keine Broker-Verbindung
- keine Live-Feeds
- keine Modelloptimierung
- keine automatische Lernuebernahme
- keine Runtime-Steuerung durch die UI

Die erzeugten FeatureVectors sind Lern- und Backtest-Kandidaten. Dauerhafte Nutzung oder Lernen bleibt human-review-pflichtig.
