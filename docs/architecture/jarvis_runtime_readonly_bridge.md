# Jarvis Runtime Read-only Bridge

Status: architecture note, not implemented.

## Ziel

Das React Jarvis Control Center soll spaeter echte HermesRuntime-JSON-Dateien anzeigen koennen, ohne HermesRuntime zu steuern oder Runtime-Daten zu veraendern. Die Bridge ist nur als read-only Verbindung zwischen UI und lokalen Runtime-Artefakten gedacht.

## Warum Browser-Dateizugriff nicht reicht

React laeuft im Browser-Kontext. Lokale Runtime-Dateien sind dort nicht verlaesslich direkt lesbar, weil:

- statische Builds keinen `/@fs` Zugriff haben,
- Browser lokale Dateizugriffe sandboxen,
- CORS- und Server-Regeln lokale Pfade blockieren koennen,
- absolute Dateipfade nicht portabel sind,
- fehlende Dateien oder wechselnde Runtime-Pfade das UI sonst hart brechen wuerden.

Der aktuelle React-Prototyp nutzt deshalb Fixtures und Fallbacks. Das ist korrekt fuer die jetzige Phase: Die UI bleibt stabil, laeuft ohne Runtime-Start, erzeugt keine Schreibzugriffe und kann trotzdem das spaetere Layout testen.

## Bridge-Zielbild

Die Runtime Bridge soll:

- nur lesen,
- keine Commands anbieten,
- keine Schreibzugriffe ausfuehren,
- keine Runtime starten oder stoppen,
- keine Runtime-Konfiguration veraendern,
- keine Trading-Aktionen ausloesen,
- keine Broker- oder cTrader-Verbindung herstellen.

Sie ist ein Anzeige-Adapter, keine Kontroll-API.

## Moegliche Endpunkte v1

Alle Endpunkte sind `GET` und read-only:

- `GET /runtime/health`
- `GET /runtime/setup-watch`
- `GET /runtime/events/recent`
- `GET /runtime/jobs`
- `GET /runtime/storage`
- `GET /runtime/replays`

Die Antworten sollen normalisierte JSON-Strukturen liefern, die zum bestehenden React Runtime Data Adapter passen:

- `runtimeHealth`
- `setupWatches`
- `dataSource`
- `warnings`

## Sicherheitsprinzipien

- Read-only first.
- Nur `localhost`.
- Kein Remote-Zugriff in v1.
- `no_auto_trading` bleibt sichtbar.
- `human_review_required` bleibt sichtbar.
- Keine Secrets ausgeben.
- Keine API Keys, Tokens oder Provider-Konfigurationen anzeigen.
- Keine internen Pfade unnoetig leaken.
- Keine Schreib-, Delete-, Start-, Stop- oder Reload-Endpunkte in v1.
- Keine Order-, Broker-, cTrader- oder Trading-Aktions-Endpunkte.
- Fehler defensiv als `unavailable` oder `fixture` melden, nicht als UI-Crash.

## Spaetere Implementierungsoptionen

Optionen fuer eine spaetere Umsetzung:

- Kleine FastAPI Bridge fuer lokale Entwicklung.
- Kleine .NET Minimal API nahe an HermesRuntime.
- Tauri File Access fuer die spaetere Desktop-App.
- Statischer JSON Export fuer Dev Mode.

## Empfehlung

Fuer v1 sollte zuerst eine kleine read-only localhost Bridge entstehen. Sie kann die bestehenden HermesRuntime JSON-Dateien normalisieren und dem React Control Center stabil bereitstellen, ohne Schreibrechte oder Steuerfunktionen einzufuehren.

Tauri File Access bleibt eine spaetere Option fuer die finale lokale Desktop-App.

## Nicht-Ziele

- Keine Bridge-Implementierung in dieser Spezifikation.
- Keine API- oder Service-Starts.
- Keine HermesRuntime-Aenderungen.
- Keine React-Funktionalitaets-Aenderungen.
- Kein Auto-Trading.
- Keine menschliche Freigabe umgehen.
