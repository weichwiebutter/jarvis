# Hermes cTrader Open API Connector Foundation v1

## Ziel

Diese Foundation bereitet einen read-only cTrader Open API Connector fuer Hermes vor. Der aktuelle Stand unterstuetzt OAuth-URL-Erzeugung, manuellen Authorization-Code-Exchange, lokale Token-Ablage und einen echten read-only Historical-Download-Pfad ueber die cTrader JSON Open API. Wenn lokale Auth-Voraussetzungen fehlen, bricht der echte Download klar ab; Stub-Daten sind nur noch als expliziter Foundation-/Fallback-Modus gekennzeichnet.

Erlaubt in v1:

- Connector-Konfiguration vorbereiten
- Symbol-Mapping sichtbar machen
- Health-Status anzeigen
- OAuth Redirect-Code manuell gegen Tokens tauschen
- Tokens lokal in `data/auth/ctrader_tokens.json` speichern
- historische cTrader Trendbars/Candles read-only abrufen
- Candles als `MarketDataCandle` lokal speichern

Nicht erlaubt:

- Orders
- Trading-Ausfuehrung
- Positionsverwaltung
- Live-Trading-Logik
- Broker-Schreibzugriffe
- Secrets oder Tokens im Repo

## Module

### CTraderOpenApiConfig / Loader

Liest die spaetere Connector-Konfiguration. Aktuell wird zuerst `config/ctrader.openapi.local.json` gesucht, sonst `config/ctrader.openapi.example.json` verwendet.

Die lokale Datei ist in `.gitignore` eingetragen und darf niemals committet werden.

Der Loader liefert ein `CTraderOpenApiConfigLoadResult` mit:

- genutztem Config-Pfad
- `local_config_loaded`
- `local_config_missing`
- `example_config_loaded`
- Warnungen

Wenn `config/ctrader.openapi.local.json` fehlt, bleibt der Stub aktiv und die CLI meldet klar: keine echten cTrader-Daten.

### Auth / Token Store

`CTraderOAuthUrlBuilder`, `CTraderTokenExchangeClient` und `CTraderTokenStore` bilden die minimale OAuth-Grundlage.

Der Ablauf:

1. `ctrader-auth-url` erzeugt die cTrader OAuth-URL.
2. Frank oeffnet die URL manuell im Browser.
3. Der Redirect-Code wird manuell mit `ctrader-auth-code --code <CODE>` uebergeben.
4. Hermes tauscht den Code gegen Tokens und speichert sie lokal in `data/auth/ctrader_tokens.json`.

Tokens werden nicht ausgegeben und nicht in Events geschrieben.

Spaetere lokale Config-Felder:

- `oauth_authorize_url`
- `token_endpoint_url`
- `client_secret`
- `auth_mode`
- `token_cache_path`
- `scopes`

`token_cache_path` darf nur auf die lokale, nicht versionierte Ablage `data/auth/ctrader_tokens.json` zeigen. Tokens gehoeren nie ins Repo.

### CTraderOAuthUrlBuilder

Erzeugt eine OAuth-URL aus:

- `client_id`
- `redirect_uri`
- `oauth_authorize_url`
- `environment`
- `scopes`

Die CLI oeffnet keinen Browser automatisch. Der Benutzer oeffnet die URL manuell, meldet sich bei cTrader an und kopiert spaeter den Redirect-Code.

### CTraderTokenStore / CTraderAuthStatus

Der TokenStore liest und schreibt Tokens lokal. CLI-Ausgaben zeigen nur Status, Pfad und Ablaufzeit, niemals Access- oder Refresh-Tokens.

Erlaubter Token-Pfad:

```text
data/auth/ctrader_tokens.json
```

Wenn die Datei fehlt, ist der Status `not_authenticated`.

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

Die lokalen Symbol-IDs bleiben Stub-/Anzeige-Werte. Beim echten Download werden broker-spezifische Symbol-IDs read-only aus der cTrader Symbol-Liste geladen und mit Hermes-Symbolen gematcht.

### CTraderHistoricalDataRequest

Beschreibt einen historischen Download:

- Symbol
- Timeframe
- Von UTC
- Bis UTC

### CTraderOpenApiHistoricalDataClient

Echter read-only Client fuer historische Trendbars:

- verbindet per JSON WebSocket mit `demo.ctraderapi.com:5036` oder `live.ctraderapi.com:5036`
- autorisiert die App mit `client_id` und `client_secret`
- autorisiert das Konto mit lokal gespeichertem Access Token
- laedt Symbol-Liste read-only
- ruft `ProtoOAGetTrendbarsReq` ab
- normalisiert cTrader Trendbars zu `MarketDataCandle`

Der Client enthaelt keine Order-, Positions- oder Trading-Kommandos.

### CTraderHistoricalDataClientStub

Erzeugt deterministische Demo-Candles fuer historische Download-Anfragen. Der Stub gibt immer sichtbar aus, dass keine echten cTrader-Daten geladen wurden.

### ICTraderHistoricalDataClient

Read-only Interface fuer den spaeteren echten Historical-Download:

- `CheckHealth()`
- `DownloadHistoricalCandles(request)`

Der aktuelle Stub implementiert dieses Interface. Ein echter Client darf spaeter nur historische/read-only Marktdaten liefern und keine Order-, Positions- oder Trading-Methoden enthalten.

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
- `oauth_authorize_url`
- `environment`
- `account_id`
- `no_orders: true`
- `read_only_market_data: true`
- `stub_mode: true`
- `auth_mode: not_configured`
- `token_cache_path: null`
- `scopes: ["accounts"]`

Keine echten Secrets in Beispiel- oder Dokumentationsdateien.

Eine spaetere lokale Datei kann so vorbereitet werden:

```json
{
  "client_id": "local-client-id",
  "client_secret": "local-client-secret",
  "redirect_uri": "http://127.0.0.1:17890/callback",
  "oauth_authorize_url": "https://id.ctrader.com/my/settings/openapi/grantingaccess/",
  "token_endpoint_url": "https://openapi.ctrader.com/apps/token",
  "environment": "demo",
  "openapi_json_host": null,
  "openapi_json_port": 5036,
  "openapi_timeout_seconds": 20,
  "account_id": "local-account-id",
  "no_orders": true,
  "read_only_market_data": true,
  "stub_mode": false,
  "auth_mode": "oauth",
  "token_cache_path": "./data/auth/ctrader_tokens.json",
  "scopes": ["accounts"],
  "allowed_symbols": ["XAUUSD", "EURUSD", "GER40", "US500"],
  "allowed_timeframes": ["H4", "H1", "M15", "M5"]
}
```

`client_secret` gehoert nur in die lokale, ignorierte Datei. Beispiel- und Dokumentationsdateien duerfen keine echten Secrets enthalten.

Der aktuelle TokenStore verwendet weiterhin nur:

```text
data/auth/ctrader_tokens.json
```

`client_secret` darf in CLI-Ausgaben, Events, Logs und Dokumentation nicht erscheinen.

Scopes mit Order-/Trading-Bezug werden in OAuth v1 als nicht erlaubt behandelt. Fuer Beta 1 wird der offizielle `accounts` Scope genutzt, weil cTrader darueber den Zugriff auf Konten und Market-Data-Anfragen autorisiert.

## CLI

Health:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-health
```

Symbol-Mapping:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-symbols
```

OAuth URL anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-auth-url
```

Auth-/Token-Status anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-auth-status
```

Authorization-Code gegen Tokens tauschen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-auth-code --code <CODE>
```

Historische Candles read-only laden:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol XAUUSD --timeframe M5 --from 2025-01-01 --to 2025-01-02
```

Wichtig: Wenn `config/ctrader.openapi.local.json` mit `auth_mode: "oauth"` vorhanden ist, versucht `download-history` den echten read-only Pfad. Fehlt `data/auth/ctrader_tokens.json`, bricht der Befehl klar mit `cTrader OAuth token missing` ab und erzeugt keine falsch deklarierten echten Daten.

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
- keine Tokens oder Secrets

## Safety

Foundation v1 erzwingt:

- keine Order-Kommandos
- keine Broker-Schreibzugriffe
- keine Positionen
- keine Trading-Ausfuehrung
- keine Secrets im Repo
- keine Tokens in Logs, Events, CLI oder UI
- klare Stub-Kennzeichnung

Der Connector endet bei lokal gespeicherten Marktdaten. Alles danach laeuft ueber die bestehende Hermes MarketData-/Feature-Pipeline.

## Datenfluss

```text
CLI download-history
-> ICTraderHistoricalDataClient
-> CTraderHistoricalDataClientStub fallback
-> CTraderTrendbarImporter
-> Hermes MarketDataCandle JSONL
-> data/market_data/candles/
-> FeatureGeneration
```

Spaeter ersetzt ein echter read-only Client nur den Stub-Client. Der Importpfad und das interne `MarketDataCandle`-Format bleiben gleich. Echte Candles werden weiterhin nach `data/market_data/candles/{symbol}/{timeframe}/` geschrieben und danach von FeatureGeneration/Beta-Learning gelesen.

## Naechste Schritte

1. Secret-Konzept finalisieren.
2. Auth-Health ohne Download implementieren.
3. Echte Symbol-Metadaten read-only laden.
4. Broker-spezifische Symbol-IDs validieren.
5. Historischen Trendbar-Download read-only anbinden.
6. Data-Quality-Validation erweitern.
7. FeatureGeneration/Backtest-Pipeline gegen echte historische Daten testen.
