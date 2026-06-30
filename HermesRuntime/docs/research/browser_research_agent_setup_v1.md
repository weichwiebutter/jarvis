# Browser Research Agent Setup V1

## Ziel

Der Browser Research Agent sammelt Research-Quellen über einen lokalen Browser, ohne Search-API-Key und ohne Fake-Quellen.
Er erzeugt nur kontrollierte Import-Kandidaten mit `human_review_status=pending`.

## Voraussetzungen

- `node` installiert
- `playwright` Node-Paket installiert, wenn kein expliziter Browser-Pfad genutzt wird
- ein lokaler Browser vorhanden, z. B. Chromium oder Google Chrome
- keine Trading- oder Broker-Berechtigungen erforderlich

## Expliziter Browser-Pfad

Hermes kann bevorzugt einen expliziten Browserpfad verwenden:

- `HERMES_BROWSER_EXECUTABLE_PATH`
- optional `HERMES_BROWSER_CHANNEL`

`HERMES_BROWSER_CHANNEL` ist ein Diagnose-/Ausrichtungswert für spätere Browser-Setups und wird im Status mit angezeigt.

Beispielpfade unter Windows/WSL:

- `/mnt/c/Program Files/Google/Chrome/Application/chrome.exe`
- `/mnt/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe`
- `/mnt/c/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe`

Wenn `HERMES_BROWSER_EXECUTABLE_PATH` gesetzt und die Datei vorhanden ist, nutzt Hermes diesen Browser direkt.
Das vermeidet Playwright-Download-Pfade und umgeht kaputte Snap-Installationen.

## Erwartete Befehle

- `hermes browser-research-status`
- `hermes browser-research-fetch --max-items 5 --dry-run`
- `hermes browser-research-fetch --max-items 5 --apply`

## Sicherheitsregeln

- `research_only=true`
- keine Trading-Ausführung
- keine Broker-Aktion
- keine cTrader-Änderung
- keine Trusted-Promotion
- keine Fake-Quellen
- nur Import-Kandidaten mit `human_review_status=pending`

## Human-Review-Flow

1. Browser Research Agent findet Suchergebnisse.
2. Ergebnisse werden als Import-Kandidaten exportiert.
3. Ein Mensch prüft URL, Domain, Auszug und Unabhängigkeit.
4. Erst nach Freigabe kann ein Import-Service die Quellenverknüpfung weiterverarbeiten.

## Wenn der Browser fehlt

Wenn `node`, `playwright` oder ein Browser nicht verfügbar ist, meldet Hermes:

- `blocked_browser_runtime_missing`
- keine Fake-Daten
- Installationshinweis im Statusreport

## Bekannte Snap-Probleme

Wenn `/snap/bin/chromium` nur mit einem Snap-Fehler startet, markiert Hermes den Zustand als:

- `broken_snap_chromium`

In diesem Fall bitte einen expliziten Browserpfad setzen.
