# cTrader Cloud API Adapter Boundary V1

## Ziel

Die spätere cTrader Cloud Anbindung soll nur den Plattform-Lifecycle und sichere Statusausgaben binden.
Trading Operations bleiben dauerhaft verboten.

Erlaubt wird später nur eine sehr schmale Adapterfläche:

- Lifecycle-Events entgegennehmen
- an `HermesPaperBotCloudHost` delegieren
- Status per `Print` ausgeben
- Zeit-/Marktkontext nur für reine Paper-Beobachtung lesen

Nicht erlaubt bleiben:

- Order-Erzeugung
- Positionsänderungen
- Broker-Aktionen
- Strategielogik in der cTrader-Datei
- Netzwerkzugriffe
- Secrets

## Später erlaubte cTrader-API-Flächen

Die folgende API-Fläche ist für eine spätere, echte cTrader-Cloud-Datei als potenziell zulässig dokumentiert, aber in V1 noch nicht zu implementieren:

- `OnStart`
- `OnTimer`
- `OnStop`
- `OnException`
- `Timer.Start`
- `Timer.Stop`
- `Print`
- optional `Server.Time` nur für Runtime-Zeitstempel
- optional `Symbol`, `Bid`, `Ask`, `Spread` nur für reine Paper-Beobachtung

Wichtig:
Diese APIs dürfen nur zur Ausführung des sicheren Host-Adapters dienen, nicht zur Trading-Entscheidung.

## Verbotene API-Flächen

Folgende Flächen bleiben dauerhaft verboten:

- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`
- `CancelPendingOrder`
- `Positions`
- `PendingOrders`
- `Account`
- `TradeResult`
- `TradeOperation`
- `Volume`
- `Symbol.QuantityToVolumeInUnits`

Jeder spätere Import dieser Flächen in die Bot-Datei blockiert Build oder Review.

## Adapter-Prinzip

Die spätere cTrader-Datei darf nur:

- Lifecycle empfangen
- an `HermesPaperBotCloudHost` delegieren
- Status per `Print` ausgeben
- defensiv Fehler isolieren

Sie darf niemals:

- Business-Logik enthalten
- Release-Artefakte verändern
- Order-API referenzieren
- eigene Strategieregeln implementieren

## Safety Gate vor echter Anbindung

Vor einer echten cTrader-/cAlgo-Anbindung müssen mindestens laufen:

- Forbidden-Reference-Guard
- Preflight
- In-Memory-Harness
- Scratch-Compile
- Human Review

## Cloud-Deployment-Hinweis

Der Cloud-Betrieb nutzt das eingebettete Release Package.

- keine lokalen Bundle-Dateien als Voraussetzung
- HermesRuntime bleibt Release Authority
- HermesRuntime muss nicht dauerhaft laufen
- `broker_action=none` bleibt Pflicht

## Offene Punkte

- exakte `cAlgo.API` Imports
- notwendige cBot-Attribute
- `AccessRights` für Cloud
- Timer-Intervall
- Sichtbarkeit des Runtime-Summaries in der Cloud
- ob `Print` allein für Diagnose reicht oder ein separates Summary-Pattern nötig ist
