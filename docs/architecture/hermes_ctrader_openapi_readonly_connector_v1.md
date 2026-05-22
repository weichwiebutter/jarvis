# Hermes cTrader Open API Read-only Connector v1

## Ziel

Hermes soll spaeter historische und optional Live-Marktdaten aus der cTrader Open API read-only abrufen koennen. v1 ist ausschliesslich als Data Connector geplant und darf keine Trading-Ausfuehrung enthalten.

Nicht-Ziele fuer v1:

- keine Orders
- keine Positionsverwaltung
- keine Trading-Kommandos
- keine Auto-Execution
- keine Live-Verbindung in dieser Planungsphase
- keine Secrets oder OAuth-Tokens im Repo

Die klare Trennung lautet:

- Data Connector: Marktdaten lesen, validieren, normalisieren und lokal speichern.
- Trading Execution: nicht Bestandteil von Connector v1 und nicht ueber diesen Codepfad erreichbar.

## cTrader Open API Konzept

cTrader Open API nutzt ein App-/Client-Konzept. Fuer Hermes waere spaeter eine eigene cTrader Open API App notwendig, die nur die minimal erforderlichen Rechte fuer Marktdaten und Kontoinformationen im read-only Umfang erhaelt.

Zu klaerende Punkte vor Implementierung:

- App/Client im cTrader Open API Portal registrieren.
- Client-ID und Redirect-/Auth-Konzept festlegen.
- OAuth-Fluss fuer Token-Erzeugung verstehen und lokal absichern.
- Token-Refresh-Verhalten dokumentieren.
- Rechteumfang auf read-only/market data begrenzen.
- Broker-/Account-spezifische Symbol-IDs pruefen.

In v1 werden keine Tokens, Client-Secrets oder OAuth-Artefakte eingecheckt.

## Secrets und Konfiguration

Spaetere Secrets-Ablage muss getrennt vom Repo erfolgen.

Erlaubte spaetere Optionen:

- lokale Secret-Datei ausserhalb des Repos
- OS-Keychain
- verschluesselter lokaler Secret Store
- `.env` nur lokal und nie committet
- manuelle Token-Eingabe fuer Dev/Test

Verboten:

- Secrets in Git
- OAuth-Tokens in Dokumentation
- Tokens in Runtime Events
- Tokens in Logs
- Tokens in UI-/CLI-Ausgaben

## Read-only Rechte

Connector v1 benoetigt nur Marktdatenzugriff.

Erlaubt:

- Symbol-Metadaten lesen
- historische Trendbars/Candles lesen
- spaeter optional Quote-/Trendbar-Stream read-only pruefen
- Connection Health pruefen

Nicht erlaubt:

- Order erstellen
- Order aendern
- Order schliessen
- Positionen steuern
- Kontorisiko veraendern
- Broker-seitige Schreiboperationen ausfuehren

## Symbol-Mapping

Hermes-interne Symbole:

- `XAUUSD`
- `EURUSD`
- `GER40`
- `US500`

cTrader verwendet broker-/accountabhaengige Symbol-IDs und teils abweichende Namen. Darum braucht der Connector ein Mapping von Hermes-Symbol zu cTrader-Symbol-ID.

Beispielstruktur:

```text
Hermes Symbol -> cTrader Symbol Name -> cTrader Symbol ID
XAUUSD        -> XAUUSD              -> broker_specific_id
EURUSD        -> EURUSD              -> broker_specific_id
GER40         -> GER40 / DE40        -> broker_specific_id
US500         -> US500 / SPX500      -> broker_specific_id
```

Mapping-Regeln:

- Mapping nie hart in Trading-Logik einbauen.
- Broker-spezifische IDs in Konfiguration/Cache halten.
- Symbol-Mapping versionieren.
- Fehlendes Mapping blockiert Download, nicht Runtime.
- UI/CLI muss Mapping-Status sichtbar machen.

## Timeframes

Startumfang:

- `H4`
- `H1`
- `M15`
- `M5`

Die cTrader Trendbar-Granularitaet muss in Hermes-Timeframes uebersetzt werden. Der Connector schreibt am Ende immer `MarketDataCandle`.

## Zielmodule

### CTraderOpenApiConfig

Konfiguration fuer Connector-Endpoint, App-/Client-Datenreferenz, lokale Secret-Quelle, Rate-Limits, erlaubte Symbole und erlaubte Timeframes.

Keine Secrets direkt im Config-Objekt serialisieren, wenn dieses in Logs oder Events gelangen kann.

### CTraderSymbolMapper

Loest Hermes-Symbole auf cTrader-Symbol-IDs auf.

Aufgaben:

- cTrader Symbol-Metadaten lesen
- Mapping pruefen
- bekannte Alias-Namen unterstuetzen
- Mapping-Fehler klar melden

### CTraderHistoricalDataClient

Read-only Client fuer historische Trendbars/Candles.

Aufgaben:

- Auth-Status pruefen
- historische Trendbars abrufen
- Rate-Limits respektieren
- Download in kleine Zeitfenster schneiden
- Verbindungsfehler defensiv behandeln

### CTraderTrendbarImporter

Normalisiert cTrader Trendbars zu `MarketDataCandle` und speichert sie lokal.

Zielpfad:

```text
data/market_data/candles/{symbol}/{timeframe}/
```

### CTraderConnectionHealth

Read-only Health-Modell fuer Auth-, Netzwerk-, Rate-Limit- und Symbol-Mapping-Status.

### CTraderDataQualityValidator

Prueft Datenqualitaet vor Feature Generation.

Checks:

- leere Antworten
- Luecken im Zeitraum
- doppelte Candles
- ungueltige OHLC-Struktur
- Future-Timestamps als Warning
- sehr grosse Spreads/Volatilitaet spaeter nur markieren, nicht handeln

## Sicherheitsregeln

Connector v1 muss diese Flags erzwingen:

- `no_orders_in_connector_v1`
- `read_only_market_data`
- `no_secrets_in_repo`
- `no_auto_execution`
- `no_write_to_broker`
- `rate_limit_respect`
- `connection_failure_safe`

Weitere Regeln:

- Keine Methode im Connector darf Broker-Schreiboperationen enthalten.
- CLI-Kommandos duerfen nur lesen oder lokale Dateien schreiben.
- Fehler beim Connector duerfen keine Trading-Aktion ausloesen.
- Rate-Limit-Fehler stoppen Downloads defensiv.
- Token-/Auth-Fehler werden als Health-Status gemeldet, nicht als Crash-Schleife.

## Datenfluss

```text
cTrader Open API
-> Trendbars/Candles
-> Hermes MarketDataCandle
-> data/market_data/candles/
-> FeatureGeneration
-> Outcomes/Backtests
```

Der Connector endet bei lokalen Marktdaten. Alles danach bleibt in Hermes' lokaler Datenpipeline.

## Spaetere CLI-Kommandos

Geplante read-only Kommandos:

```bash
hermes ctrader-auth status
hermes ctrader-symbols
hermes download-history --symbol XAUUSD --timeframe M5 --from 2026-01-01 --to 2026-02-01
hermes ctrader-health
```

Nicht geplante v1-Kommandos:

- `place-order`
- `close-position`
- `modify-order`
- `start-trading`
- `auto-trade`

## Implementierungsreihenfolge

### Phase 1: CSV Import fertig nutzen

Bestehenden lokalen CSV-Import als sichere Datenbasis verwenden. Reale cTrader-Exporte koennen damit bereits ohne API/Secrets verarbeitet werden.

### Phase 2: Config-/Secrets-Konzept

Festlegen, wo Connector-Konfiguration endet und Secret-Verwaltung beginnt. Keine Implementierung ohne klares Secret-Konzept.

### Phase 3: Auth Health Check

Nur Auth-/Token-Status pruefen. Keine Downloads und keine Trading-Funktionen.

### Phase 4: Symbol Mapping

Symbol-Mapping fuer `XAUUSD`, `EURUSD`, `GER40`, `US500` aufbauen und sichtbar validieren.

### Phase 5: Historical Trendbar Download

Historische Trendbars fuer erlaubte Symbole/Timeframes in begrenzten Zeitfenstern abrufen und lokal speichern.

### Phase 6: Data Quality Validation

Importierte Daten auf Luecken, Duplikate, Zeitbereich und OHLC-Plausibilitaet pruefen.

### Phase 7: Feature Pipeline Integration

Download-Artefakte in `FeatureGenerationService`, Backtest-Stubs und spaetere Research-Jobs einspeisen.

## Beziehung zu bestehenden Modulen

- `CTraderCsvCandleImporter` bleibt der sichere Offline-Einstieg.
- `MarketDataCandle` bleibt das interne Normalformat.
- `FeatureGenerationService` verarbeitet lokale Candles, egal ob CSV oder Open API Ursprung.
- `RuntimeHealth` kann spaeter `ctrader_connection_health` read-only anzeigen.
- Jarvis Control Center darf nur Status und Datenqualitaet anzeigen, keine Connector-Kommandos mit Broker-Schreibwirkung.

## Offene Punkte vor Implementierung

- konkrete cTrader Open API SDK-/Transportwahl
- OAuth-Flow fuer lokale Desktop-/CLI-Nutzung
- Token-Speicherort
- Broker-spezifische Symbol-IDs
- Rate-Limit-Strategie
- maximale Downloadfenster je Timeframe
- Event-Schema fuer Download- und Auth-Health-Events

Bis diese Punkte geklaert sind, bleibt der CSV-Import der empfohlene Weg fuer echte historische Daten.
