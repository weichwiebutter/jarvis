# cTrader Cloud AccessRights & cBot Attribute Decision V1

## Ziel

Für den Cloud Paper Bot soll die minimale cTrader-Deklaration vorab festgelegt werden.
Die Zielkonfiguration muss cloud-fähig, paper-only und unabhängig von lokalen Bundle-Dateien sein.

## AccessRights-Entscheidung

Für den Cloud Embedded Mode ist `AccessRights.None` die bevorzugte Zielentscheidung.

Begründung:

- kein lokales File-Bundle erforderlich
- kein Internetzugriff erforderlich
- kein externer Prozesszugriff erforderlich
- keine Broker-/Order-Funktionen werden benötigt
- das Embedded Package ist bereits im Bot-Code enthalten
- die Cloud-Runtime soll möglichst wenig Rechte besitzen

Nicht verwenden für den Cloud-Zielmodus:

- `AccessRights.FileSystem`
- `AccessRights.Internet`
- `AccessRights.FullAccess`

Ausnahme:

- `AccessRights.FileSystem` bleibt nur für lokale Entwicklungs- oder VPS-Modi relevant
- nicht für den Cloud-Zielmodus

## cBot Attribute

Für die spätere echte cTrader-Datei ist wahrscheinlich eine minimale Robot-Deklaration nötig, zum Beispiel:

- `[Robot(...)]`
- `TimeZone`
- `AccessRights=None`

V1 dokumentiert diese Attributklasse nur als Zielbild.
Eine Implementierung wird hier ausdrücklich nicht erzeugt.

## Erlaubte spätere cAlgo-Flächen

Für den späteren Cloud Paper Bot sind nur folgende Flächen als zulässige Ziel-API dokumentiert:

- `OnStart`
- `OnTimer`
- `OnStop`
- `OnException`
- `Timer.Start`
- `Timer.Stop`
- `Print`
- optional `Server.Time`
- optional `Symbol`, `Bid`, `Ask`, `Spread` nur für reine Paper-Beobachtung

Diese Flächen dürfen nur Lifecycle und Beobachtung unterstützen.
Sie dürfen niemals Trading Operations auslösen.

## Verbotene Flächen

Die folgenden Flächen bleiben dauerhaft verboten:

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

## Entscheidung

Der V1 Cloud Paper Bot soll später auf folgendem Zielbild beruhen:

- `AccessRights.None`
- `EmbeddedReleasePackage.g.cs`
- `RuntimeMode=cloud_embedded_bundle`
- `broker_action=none`

## Offene Punkte

- exakte cBot-Klassendeklaration
- `TimeZone`-Wahl
- Timer-Intervall
- ob `Print` für Statusausgaben genügt
- ob Cloud Logs exportierbar sind
- wie spätere Plattformattribute ohne Trading-Risiko gehalten werden
