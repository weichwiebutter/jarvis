# cTrader QUOTE Bridge Architecture
Version: 1.0
Status: FOUNDATION
System: Jarvis Hybrid Architecture

---

# 1. Ziel

Dieses Dokument beschreibt die geplante cTrader QUOTE Bridge als naechsten
Live-Daten-Schritt fuer Jarvis und Hermes.

Die Bridge ist ausschliesslich als Read-only-Datenquelle fuer Livekurse geplant.
Sie implementiert keine echte Verbindung, speichert keine Zugangsdaten, sendet
keine Orders und aktiviert keine Trade-Funktionen.

---

# 2. Klare Abgrenzung

Erlaubt:

- Architektur der QUOTE-Verbindung dokumentieren
- Livekurs-Datenfluss planen
- erste Symbole definieren
- Jarvis Home Dashboard als Konsument beschreiben
- Hermes Trading Analyst als Konsument beschreiben
- spaeteres Prediction Feedback Learning vorbereiten

Nicht erlaubt:

- echte cTrader-Verbindung herstellen
- Zugangsdaten speichern
- Orders platzieren
- Positionen oeffnen, schliessen oder veraendern
- TRADE-Verbindung aktivieren
- Runtime-Dateien schreiben
- Hintergrundservices starten

Grundregel:

```text
QUOTE = read-only live market data
TRADE = disabled until explicit human approval
```

---

# 3. Verbindungsmodell

## QUOTE-Verbindung

Die QUOTE-Verbindung ist fuer Livekurse vorgesehen.

Zweck:

- aktuelle Bid/Ask-Preise lesen
- Symbolstatus beobachten
- Preisaktualitaeten fuer UI und Analyse bereitstellen
- spaetere Marktfeed-Historie vorbereiten

Die QUOTE Bridge darf keine Order-API kapseln und keine Trading-Aktion
ausloesen. Ihr Verantwortungsbereich endet beim Empfang, Normalisieren und
Bereitstellen von Marktdaten.

---

## TRADE-Verbindung

Die TRADE-Verbindung bleibt vorerst deaktiviert.

Status:

```text
trade_connection: disabled
no_auto_trading: true
human_review_required: true
```

Die TRADE-Seite darf erst nach expliziter Freigabe geplant oder implementiert
werden. Bis dahin gilt:

- keine Order-Funktionen
- keine Account-Aktionen
- keine Positionsverwaltung
- keine automatischen Broker-Kommandos
- keine versteckte Vorbereitung von Auto-Trading

---

# 4. Erste Symbole

Initiale Livekurs-Symbole:

- XAUUSD
- EURUSD

Diese Auswahl bleibt bewusst klein. XAUUSD und EURUSD reichen fuer die erste
Dashboard- und Analyst-Integration aus, ohne die Datenarchitektur zu
ueberladen.

Weitere Symbole koennen spaeter ergaenzt werden, wenn Datenformat, Safety,
Anzeige und Feedback-Loop stabil sind.

---

# 5. Datenkonsumenten

## Jarvis Home Dashboard

Das Jarvis Home Dashboard soll die QUOTE Bridge spaeter als Live-Datenquelle
nutzen.

Moegliche Anzeige:

- Symbol
- Bid
- Ask
- Spread
- letzter Update-Zeitpunkt
- Verbindungsstatus
- Datenalter

Das Dashboard zeigt Marktdaten und Status. Es darf keine Order-Controls aus der
QUOTE Bridge ableiten.

---

## Hermes Trading Analyst

Der Hermes Trading Analyst soll die QUOTE Bridge spaeter als Marktdatenquelle
fuer Analysekontext nutzen.

Moegliche Nutzung:

- aktueller Preisbezug fuer Szenarien
- Symbolkontext fuer XAUUSD und EURUSD
- Spread- und Datenqualitaetsbewertung
- kurzfristige Marktbewegungen als Analyseinput
- Vorbereitung fuer spaetere Prediction-Auswertung

Der Trading Analyst bleibt eine Analyse- und Review-Schicht. Er darf Signale,
Szenarien und Confidence-Werte erzeugen, aber keine Orders ausfuehren.

---

## Prediction Feedback Learning

Prediction Feedback Learning ist eine spaetere Ausbaustufe.

Die QUOTE Bridge kann dafuer kuenftig objektive Preisreferenzen liefern:

- Preis bei Prognoseerstellung
- Preis bei Ablaufzeit
- Preis bei Invalidierung
- erreichte Richtung innerhalb eines Timeframes
- Datenqualitaet waehrend der Bewertung

Das Feedback Learning bewertet Prognosen. Es handelt nicht automatisch.

---

# 6. Geplante Module

Die folgenden Module sind als zukuenftige Zielstruktur geplant. Dieses Dokument
implementiert sie nicht.

## agents/trading/ctrader_quote_bridge.py

Geplante Rolle:

- cTrader QUOTE-Verbindung kapseln
- Livekurs-Events empfangen
- Rohdaten validieren
- Bid/Ask/Spread normalisieren
- nur Read-only-Marktdaten ausgeben
- keine Order- oder Trade-Methoden enthalten

Safety:

- no_auto_trading
- human_review_required fuer jede Erweiterung Richtung TRADE
- keine Passwortausgabe
- keine Speicherung von Credentials

---

## agents/trading/market_feed_store.py

Geplante Rolle:

- normalisierte Marktdaten intern bereitstellen
- letzten bekannten Preis pro Symbol halten
- Datenalter und Datenqualitaet markieren
- Snapshot-Schnittstelle fuer Dashboard und Analyst vorbereiten

Safety:

- kein Runtime-Dump ungepruefter Rohdaten
- keine sensiblen Zugangsdaten
- keine Orderinformationen
- keine Broker-Aktion

---

## agents/trading/prediction_store.py

Geplante Rolle:

- spaetere Trading-Prognosen strukturiert verwalten
- prediction_id, Symbol, Richtung, Confidence und Ablaufzeit halten
- Status fuer offene, bewertete, abgelaufene oder invalidierte Prognosen
  vorbereiten

Safety:

- Prognosen sind Analyseobjekte, keine Handlungsanweisungen
- kritische Prognosen brauchen menschlichen Review
- keine automatische Orderableitung

---

## agents/trading/prediction_feedback.py

Geplante Rolle:

- Prognosen gegen spaetere Marktdaten bewerten
- correct, wrong, expired, invalidated und late_correct unterscheiden
- Confidence-Kalibrierung vorbereiten
- Accuracy je Symbol und Timeframe auswerten

Safety:

- Feedback ist Lernsignal, kein Trading-Ausloeser
- keine direkte Broker-Verbindung
- keine automatische Strategieaktivierung

---

# 7. Sicherheitsregeln

## Credentials

- Credentials duerfen nur in `.env.local` liegen.
- `.env.local` darf niemals committed werden.
- Keine Zugangsdaten in Markdown-Dokumenten.
- Keine Zugangsdaten in Runtime-Logs.
- Keine Zugangsdaten in Fehlermeldungen.
- Keine Passwoerter in Debug-Ausgaben.

## Logging

- keine Passwoerter in Logs
- keine Tokens in Logs
- keine Account-Details in Logs
- keine rohen Auth-Payloads in Logs
- Marktdaten-Logs nur nach klarer Freigabe und mit begrenztem Umfang

## Trading-Sperre

- keine Orders aus dem QUOTE-Modul
- keine Order-Methoden in `ctrader_quote_bridge.py`
- keine Positionsverwaltung im QUOTE-Pfad
- kein Auto-Trading
- kein stilles Umschalten auf TRADE
- TRADE-Modul bleibt disabled bis explizite Freigabe

## Human Review

Trading-nahe Entscheidungen bleiben human-in-the-loop.

Pflichtmarker:

```text
no_auto_trading: true
human_review_required: true
```

Jede spaetere Erweiterung, die Orders, Accountdaten, Positionsdaten oder
Broker-Aktionen beruehrt, braucht vorab explizite menschliche Freigabe.

---

# 8. Geplanter Datenfluss

```text
cTrader QUOTE
  -> ctrader_quote_bridge.py
  -> market_feed_store.py
  -> Jarvis Home Dashboard
  -> Hermes Trading Analyst
  -> prediction_store.py
  -> prediction_feedback.py
```

Der Datenfluss ist read-only bis zur Analyse- und Feedback-Schicht.

Kein Teil dieses Flusses darf Orders erzeugen oder die TRADE-Verbindung
aktivieren.

---

# 9. Grundprinzip

Die cTrader QUOTE Bridge ist der naechste Live-Daten-Schritt, nicht der Einstieg
in automatisches Trading.

```text
Live prices first.
Analysis second.
Feedback learning later.
Trading actions disabled.
Human review required.
```
