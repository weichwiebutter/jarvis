# Hermes cTrader Open API Connector Foundation v1

## Ziel

Diese Foundation bereitet einen spaeteren read-only cTrader Open API Connector fuer Hermes vor. Der aktuelle Stand ist ein sauber markierter Stub: Er oeffnet keine Live-Verbindung, verarbeitet keine OAuth-Tokens und ruft keine echten cTrader-Daten ab.

Erlaubt in v1:

- Connector-Konfiguration vorbereiten
- Symbol-Mapping sichtbar machen
- Health-Status anzeigen
- historische Stub-Candles erzeugen
- Stub-Candles als `MarketDataCandle` lokal speichern

Nicht erlaubt:

- Orders
- Trading-Ausfuehrung
- Positionsverwaltung
- Live-Trading-Logik
- Broker-Schreibzugriffe
- Secrets oder Tokens im Repo

## Module

### CTraderOpenApiConfig

Liest die spaetere Connector-Konfiguration. Aktuell wird zuerst `config/ctrader.openapi.local.json` gesucht, sonst `config/ctrader.openapi.example.json` verwendet.

Die lokale Datei ist in `.gitignore` eingetragen und darf niemals committet werden.

### CTraderConnectionHealth

Beschreibt den aktuellen Connector-Zustand:

- Status
- Environment
- Stub aktiv
- Auth konfiguriert
- Client-ID konfiguriert
- Account-ID konfiguriert
- `no_orders`
- read-only Market Data
- Warnungen

### CTraderSymbolMapper

Stellt Stub-Mappings fuer Hermes-Symbole bereit:

- `XAUUSD`
- `EURUSD`
- `GER40`
- `US500`

Die Symbol-IDs sind bewusst Stub-Werte. Echte broker-spezifische IDs muessen spaeter read-only aus cTrader Symbol-Metadaten geladen und validiert werden.

### CTraderHistoricalDataRequest

Beschreibt einen historischen Download:

- Symbol
- Timeframe
- Von UTC
- Bis UTC

### CTraderHistoricalDataClientStub

Erzeugt deterministische Demo-Candles fuer historische Download-Anfragen. Der Stub gibt immer sichtbar aus, dass keine echten cTrader-Daten geladen wurden.

### CTraderTrendbarImporter

Schreibt heruntergeladene oder im Stub erzeugte Trendbars als `MarketDataCandle` nach:

```text
data/market_data/candles/{symbol}/{timeframe}/
```

## Config

Beispieldatei:

```text
config/ctrader.openapi.example.json
```

Lokale Datei:

```text
config/ctrader.openapi.local.json
```

Die lokale Datei ist ignoriert und darf nicht committet werden.

Beispielwerte:

- `client_id`
- `redirect_uri`
- `environment`
- `account_id`
- `no_orders: true`
- `stub_mode: true`

Keine echten Secrets in Beispiel- oder Dokumentationsdateien.

## CLI

Health:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-health
```

Symbol-Mapping:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-symbols
```

Historische Stub-Daten lokal erzeugen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol XAUUSD --timeframe M5 --from 2025-01-01 --to 2025-01-02
```

Wichtig: `download-history` erzeugt in Foundation v1 Stub-Daten. Es behauptet nicht, echte cTrader-Daten geladen zu haben.

## Events

Die Foundation publiziert lokale Runtime-Events:

- `CTraderConnectorHealthChecked`
- `CTraderHistoricalDownloadStarted`
- `CTraderHistoricalDownloadCompleted`
- `CTraderHistoricalDownloadFailed`

Alle Events bleiben read-only/analysis-only und enthalten Safety-Kontext:

- `noAutoTrading = true`
- `humanReviewRequired = true`
- Stub aktiv

## Safety

Foundation v1 erzwingt:

- keine Order-Kommandos
- keine Broker-Schreibzugriffe
- keine Positionen
- keine Live-Verbindung
- keine Secrets im Repo
- keine Tokens in Logs, Events, CLI oder UI
- klare Stub-Kennzeichnung

Der Connector endet bei lokal gespeicherten Marktdaten. Alles danach laeuft ueber die bestehende Hermes MarketData-/Feature-Pipeline.

## Datenfluss

```text
CLI download-history
-> CTraderHistoricalDataClientStub
-> CTraderTrendbarImporter
-> Hermes MarketDataCandle JSONL
-> data/market_data/candles/
-> FeatureGeneration
```

Spaeter ersetzt ein echter read-only Client nur den Stub-Client. Der Importpfad und das interne `MarketDataCandle`-Format bleiben gleich.

## Naechste Schritte

1. Secret-Konzept finalisieren.
2. Auth-Health ohne Download implementieren.
3. Echte Symbol-Metadaten read-only laden.
4. Broker-spezifische Symbol-IDs validieren.
5. Historischen Trendbar-Download read-only anbinden.
6. Data-Quality-Validation erweitern.
7. FeatureGeneration/Backtest-Pipeline gegen echte historische Daten testen.
