# Hermes Trading Analyst Agent Roadmap

Status: Konzept und Roadmap. Dieses Dokument beschreibt die spaetere
Analyse-Komponente. Es enthaelt keine ausfuehrbare Trading-Logik, keine
cTrader-Anbindung, keine Order-Funktion und keine API-Schluessel.

## Ziel

Der Hermes Trading Analyst Agent soll spaeter als Analyse-, Bewertungs- und
Lernschicht fuer ausgewaehlte Maerkte dienen. Der Agent beobachtet Marktstruktur,
Zeitzonen, relevante Timeframes und technische Setups, erstellt daraus
nachvollziehbare Prognosen und bewertet diese Prognosen spaeter objektiv.

Jarvis stellt dabei die UI, Statusansicht und Kontrolle bereit. Hermes bewertet,
lernt aus Rueckmeldungen und bereitet Alerts oder Analyseausgaben vor. Trading
bleibt human-in-the-loop.

## Klare Abgrenzung

Erlaubt:

- Marktanalyse
- Bias-Einschaetzung
- Pattern-Erkennung
- Confidence-Bewertung
- Prognosen mit Ablaufzeit
- Alerts und Review-Hinweise
- spaetere Lern- und Statistikfunktionen
- manuelle Entscheidungsunterstuetzung

Nicht erlaubt:

- automatische Trades
- automatische Orderausfuehrung
- cTrader-Order-Funktionen
- versteckte Broker-Automation
- API-Key-Verwaltung in diesem Agent
- Umgehung menschlicher Freigabe

Jede spaetere Integration muss Analyse und Orderausfuehrung strikt trennen. Der
Trading Analyst darf Signale liefern, aber keine Positionen oeffnen, schliessen
oder veraendern.

## Unterstuetzte Maerkte

Initiale Fokusmaerkte:

- XAUUSD
- EURUSD
- GER40

Diese Auswahl soll bewusst klein bleiben, damit Hermes zunaechst Symbolverhalten,
Sessions, Volatilitaet und Pattern-Qualitaet sauber lernen kann.

## Rollenverteilung

cTrader:

- Datenquelle fuer Charts, Kerzen, Marktpreise und Pattern-Kontext
- Quelle fuer spaetere manuell oder technisch exportierte Chart-/Pattern-Daten
- keine Orderausfuehrung durch Hermes
- keine automatische Broker-Aktion

Hermes/Python:

- Analysebackend
- Multi-Timeframe-Bewertung
- Pattern-Gewichtung
- Confidence Scoring
- Prognoseerstellung
- Prediction Feedback Loop
- Lern- und Statistikschicht

Jarvis:

- Trading Panel
- Statusanzeige
- Alert-Darstellung
- History- und Trefferquotenansicht
- menschliche Kontrolle und Freigabe

## Multi-Timeframe-Logik

Der Analyst soll spaeter immer top-down arbeiten. Niedrigere Timeframes duerfen
ein Setup nur bestaetigen oder praezisieren, aber nicht isoliert die gesamte
Markteinschaetzung bestimmen.

HTF: W1, D1, H4

- struktureller Bias
- wichtige Zonen
- Trend- oder Range-Kontext
- groessere Rejection- und Breakout-Bereiche

MTF: H1, M15

- Setup-Entwicklung
- Pullback- oder Continuation-Kontext
- Breakout-/False-Break-Bewertung
- Uebergang von Zone zu konkretem Setup

LTF: M5, M1

- Trigger-Kontext
- Pattern-Feinbestaetigung
- Timing-Qualitaet
- kurzfristige Invalidierung

Grundregel:

- HTF definiert Bias und relevante Zone.
- MTF definiert Setup-Qualitaet.
- LTF definiert Timing und kurzfristige Gueltigkeit.

## Pattern

Initiale Pattern-Liste:

- Rejection
- False Break
- Engulfing
- Morning Star
- Evening Star

Jedes Pattern soll spaeter nicht nur als isoliertes Kerzenmuster bewertet werden,
sondern im Kontext von Symbol, Session, Timeframe, Zone, Trendstruktur und
vorherigem Marktverhalten.

Beispielhafte Kontextfragen:

- Tritt das Pattern an einer HTF-Zone auf?
- Bestaetigt oder widerspricht es dem HTF-Bias?
- Ist die MTF-Struktur bereits vorbereitet?
- Entsteht das Pattern waehrend einer relevanten Session?
- Gibt es eine klare Invalidierungszone?

## Confidence Score 0-12

Der Confidence Score soll spaeter eine kompakte, nachvollziehbare Bewertung des
Setups liefern. Er ist kein Garant fuer Gewinnwahrscheinlichkeit, sondern eine
qualitative Punktzahl fuer Setup-Guete und Kontextuebereinstimmung.

Vorgeschlagene Bewertung:

- 0-2: sehr schwach, keine verwertbare Prognose
- 3-5: niedrig, nur Beobachtung
- 6-8: mittel, Alert moeglich
- 9-10: hoch, starke Analyseausgabe
- 11-12: sehr hoch, seltenes High-Quality-Setup

Moegliche Score-Komponenten:

- HTF-Bias klar und konsistent
- Preis reagiert an relevanter HTF-Zone
- MTF-Setup bestaetigt den Bias
- LTF-Pattern bestaetigt Timing
- Pattern historisch fuer Symbol/Session stark
- klare Invalidierung vorhanden
- gueltige Ablaufzeit definiert
- keine widerspruechliche Struktur

## Prediction Feedback Loop

Der Prediction Feedback Loop ist ein Kernbestandteil des spaeteren Analysten.
Hermes soll nicht nur Signale erzeugen, sondern deren Qualitaet objektiv messen
und daraus lernen.

### Prognoseerstellung

Hermes erstellt fuer ein Setup eine Prognose:

- Richtung: up, down oder neutral
- Symbol
- Timeframe
- Confidence Score
- Setup-Kontext
- relevante HTF-Zone
- MTF-Setup
- LTF-Pattern
- Session-Kontext
- Invalidierungsbedingung
- Ablaufzeit
- Erstellungszeitpunkt

Eine Prognose muss eine Ablaufzeit haben. Ohne Ablaufzeit kann spaeter nicht
objektiv bewertet werden, ob die Prognose rechtzeitig, zu spaet oder gar nicht
eingetroffen ist.

### Speicherung

Hermes speichert spaeter pro Prediction mindestens:

- prediction_id
- created_at
- expires_at
- symbol
- direction
- primary_timeframe
- htf_context
- mtf_context
- ltf_context
- pattern
- confidence
- setup_context
- invalidation_context
- session
- status
- evaluation_result
- evaluated_at

Diese Daten gehoeren in eine dedizierte Prediction-/Learning-Schicht, nicht in
Runtime-Logs oder UI-Zustand.

### Objektive Pruefung

Nach Ablauf oder bei Invalidierung prueft Hermes spaeter objektiv:

- Wurde die prognostizierte Richtung erreicht?
- Wurde sie innerhalb der Ablaufzeit erreicht?
- Wurde das Setup vorher invalidiert?
- War die Bewegung erst nach Ablauf korrekt?
- War die Prognose neutral und blieb der Markt neutral genug?
- Hat das Pattern in diesem Kontext tatsaechlich einen Mehrwert geliefert?

### Bewertungsstatus

Erlaubte Feedback-Ergebnisse:

- correct: Prognose trat innerhalb der definierten Ablaufzeit ein.
- wrong: Prognose war objektiv falsch.
- expired: Ablaufzeit erreicht, ohne klares Ergebnis.
- invalidated: Setup wurde vor Ziel-/Richtungsbestaetigung ungueltig.
- late_correct: Prognose wurde spaeter korrekt, aber nach Ablaufzeit.

### Lernen aus Feedback

Hermes soll spaeter aus diesen Ergebnissen lernen:

- Pattern-Gewichtung pro Symbol
- Pattern-Gewichtung pro Session
- Symbol-Verhalten
- Session-Verhalten
- Timeframe-Qualitaet
- Zuverlaessigkeit von HTF-Zonen
- Qualitaet bestimmter MTF-Setups
- Timing-Staerke bestimmter LTF-Pattern
- Confidence-Kalibrierung
- typische Gruende fuer Invalidierungen
- Unterschied zwischen correct und late_correct

Beispiel:

Wenn False Breaks auf XAUUSD waehrend London/New York haeufig correct sind,
aber auf GER40 im gleichen LTF-Kontext oft invalidated werden, soll Hermes die
Pattern-Gewichtung symbol- und sessionspezifisch anpassen.

## Vorgeschlagene spaetere Modulstruktur

Die folgenden Dateien sind nur als spaetere Zielstruktur vorgesehen. Dieses
Roadmap-Dokument implementiert sie nicht.

- `agents/trading/hermes_trading_analyst.py`
- `agents/trading/pattern_detector.py`
- `agents/trading/timeframe_analyzer.py`
- `agents/trading/confidence_scorer.py`
- `agents/trading/ctrader_bridge.py`
- `agents/trading/prediction_store.py`
- `agents/trading/prediction_feedback.py`
- `agents/trading/trading_learning_store.py`

Vorgeschlagene Verantwortlichkeiten:

- `hermes_trading_analyst.py`: Orchestrierung der Analyse, Zusammenfuehrung der
  Timeframe-, Pattern-, Confidence- und Feedback-Komponenten.
- `pattern_detector.py`: spaetere Erkennung und Normalisierung von Rejection,
  False Break, Engulfing, Morning Star und Evening Star.
- `timeframe_analyzer.py`: HTF/MTF/LTF-Kontext, Bias, Zonen und Setup-Abgleich.
- `confidence_scorer.py`: Berechnung des Confidence Score 0-12.
- `ctrader_bridge.py`: spaetere Daten-/Chart-/Pattern-Schnittstelle zu cTrader,
  ohne Order-Funktionen.
- `prediction_store.py`: Speicherung offener und abgeschlossener Prognosen.
- `prediction_feedback.py`: objektive Auswertung von correct, wrong, expired,
  invalidated und late_correct.
- `trading_learning_store.py`: aggregierte Lernwerte fuer Pattern, Symbol,
  Session und Timeframe.

## Jarvis Trading Panel Idee

Das spaetere Jarvis Trading Panel soll keine Order-Maske sein, sondern eine
kompakte Analyse- und Lernansicht.

Anzeigefelder:

- Symbol
- Bias
- HTF Zone
- MTF Setup
- LTF Pattern
- Confidence
- letzter Check
- naechster Check
- Signal aktiv
- Prediction History
- Trefferquote nach Setup
- Trefferquote nach Session
- Trefferquote nach Timeframe

Moegliche Panel-Struktur:

- Aktuelle Watchlist: XAUUSD, EURUSD, GER40
- Aktiver Bias je Symbol
- offene Predictions mit Ablaufzeit
- zuletzt bewertete Predictions
- Setup-Historie
- Pattern-Qualitaet
- Session-Statistik
- Timeframe-Statistik

Wichtige UI-Regel:

Das Panel darf Analyse, Alert-Status und Trefferquoten anzeigen. Es soll keine
Buttons fuer automatische Buy-/Sell-Orders enthalten.

## Beta-3 Trading Research Regeln

Hermes ist zuerst Research-/Learning-Plattform. Trading ist ein wichtiger
Schwerpunkt, aber nicht das einzige Ziel. Trading-Funktionen duerfen nicht als
isolierte Hacks entstehen, sondern muessen in Research Memory, Runtime Events,
Safety Gates, Approval und Dauerbetrieb passen.

Bewertung:

- robuste Netto-Performance ist wichtiger als reine Winrate
- Zielwerte fuer spaetere Scalping-Bot-Kandidaten: ca. 60-70 % Winrate,
  Profit Factor > 1.4, niedriger Drawdown
- Walk-Forward- und Out-of-Sample-Stabilitaet sind Pflicht
- Broker-Realitaet muss beruecksichtigt werden: Spread, Commission, Slippage,
  Session-Liquiditaet und Fusion-Markets-Parameter

## Future Trading Control Layer

Vor Paper-/Demo-/Live-Bot-Phasen braucht Jarvis/Hermes eine sichtbare
Kontrollschicht:

- Auto-Trading Toggle
- Paper/Demo Mode
- Risk Limits
- Volume- / Lot-Limits
- Strategy Whitelist
- Symbol Whitelist
- Emergency Stop

## Dedicated Scalping Bot Roadmap

Ein spaeterer dedizierter Scalping Bot darf nur aus robustem Hermes Research
Memory abgeleitet werden. Er bleibt eine getrennte Ausfuehrungsschicht und darf
nicht direkt aus Research-Ergebnissen live handeln.

Bot Candidate Pipeline:

```text
research_candidate
-> promising
-> robust
-> demo_bot_candidate
-> demo_validation
-> approved_for_small_live_test
```

## Implementierungsphasen

Phase 1: Dokumentation und Datenmodell

- Ziel, Grenzen und Datenfelder festlegen
- Prediction-Objekt definieren
- Feedback-Status definieren
- keine cTrader-Anbindung

Phase 2: Offline-/Mock-Analyse

- statische Beispieldaten analysieren
- Pattern-Kontext normalisieren
- Confidence Score testbar machen
- Feedback Loop mit gespeicherten Beispielen pruefen

Phase 3: Datenbruecke ohne Orders

- cTrader nur als Daten-/Chart-/Pattern-Quelle anbinden
- keine Order-Endpunkte
- keine API-Key-Dokumentation im Repo
- klare Trennung zwischen Analyse und Ausfuehrung

Phase 4: Jarvis Trading Panel

- kompakte Statusansicht
- offene Predictions
- Prediction History
- Trefferquoten nach Setup, Session und Timeframe

Phase 5: Lernschicht

- Pattern-Gewichte aktualisieren
- Symbol-/Session-/Timeframe-Qualitaet berechnen
- Confidence Score kalibrieren
- Fehlbewertungen sichtbar machen

## Sicherheitsprinzipien

- Keine automatische Orderausfuehrung.
- Keine Broker-Automation.
- Keine API-Schluessel in Code, Logs oder Dokumentation.
- Kein Schreiben in Runtime-Daten durch Planungsdokumente.
- Jede spaetere Ausfuehrung bleibt human-in-the-loop.
- Analyse und Trading-Ausfuehrung bleiben getrennte Systembereiche.
