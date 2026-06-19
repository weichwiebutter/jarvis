# HermesPaperBot cTrader Cloud Compile Checklist V1

Diese Checkliste beschreibt den ersten echten cTrader-Cloud-Kompilierungstest für den HermesPaperBot.

## Ziel

- cTrader Cloud soll den paper-only Wrapper kompilieren können
- HermesRuntime bleibt die Release Authority
- keine Order-Ausführung
- keine cTrader Order API
- `broker_action=none`
- AccessRights bleiben `None`

## In cTrader Algo zu importierende Dateien

Importiere nur die benötigten HermesPaperBot-Dateien für den Cloud-Wrapper-Test:

- `HermesPaperBotCTraderWrapper.cs`
- `HermesPaperBotCloudHost.cs`
- `HermesPaperBot.cs`
- `Generated/EmbeddedReleasePackage.g.cs`
- `Models/`
- `Services/`

Für den ersten Test soll der Wrapper als eigentliche cBot-Datei dienen.

## Eigentliche cBot-Wrapper-Datei

Die eigentliche Wrapper-Datei ist:

- `ctrader/HermesPaperBot/HermesPaperBotCTraderWrapper.cs`

Der konditionale Branch mit `#if HERMES_CTRADER_WRAPPER` ist der spätere echte cTrader-Weg.
Der lokale `#else`-Stub bleibt für Repo-Builds und Harnesses erhalten.

## Build-Symbol

Für den echten cTrader-Wrapper-Build muss gesetzt sein:

- `HERMES_CTRADER_WRAPPER`

Ohne dieses Symbol kompiliert nur der lokale Stub-Zweig.

## Erwartete AccessRights

Für den Cloud-Paper-Bot:

- `AccessRights.None`

Nicht verwenden:

- `AccessRights.FileSystem`
- `AccessRights.Internet`
- `AccessRights.FullAccess`

## Erwartete erlaubte cAlgo-Flächen

Im echten cTrader-Zweig sind nur diese Flächen vorgesehen:

- `Robot`
- `OnStart`
- `OnTimer`
- `OnStop`
- `OnException`
- `Timer`
- `Print`
- `Symbol.Bid`
- `Symbol.Ask`
- `SymbolName`
- `Server.Time`

## Verbotene Flächen

Diese Flächen bleiben verboten:

- konto-bezogene read-only APIs vermeiden
- positions-bezogene APIs vermeiden
- pending-order APIs vermeiden
- markt-/limit-/stop-order APIs vermeiden
- positionsänderungs APIs vermeiden
- positionsschluss APIs vermeiden
- trade-result / operation APIs vermeiden
- volumen APIs vermeiden

## Bekannte mögliche SDK-Anpassung

- `Bars.TimeFrame` kann je nach SDK-Version anders heißen oder anders verfügbar sein
- falls nötig, muss der Wrapper in der echten cTrader-Umgebung an die dortige API angepasst werden

## Erwartetes Runtime-Verhalten

Beim erfolgreichen Start:

- Start-Print erscheint
- Timer-Print erscheint
- `broker_action=none`
- keine Order-Ausführung
- keine Demo-Ausführung
- keine Live-Ausführung
- hoher Spread blockiert nur die Paper-Entscheidung

## Troubleshooting

### Fehlender TimeFrame

Wenn `Bars.TimeFrame` nicht verfügbar ist, muss die cTrader-API-Variante angepasst werden.

### Fehlendes Build-Symbol

Wenn `HERMES_CTRADER_WRAPPER` fehlt, kompiliert nur der Stub-Zweig.

### Wrapper kompiliert nur Stub

Das ist im Repo-Build erwartbar, wenn cTrader SDK nicht verfügbar ist.

### cAlgo Namespace fehlt

Dann ist der echte cTrader-Zweig nicht in der lokalen Umgebung kompiliert worden.

## Safety-Invariants

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`
