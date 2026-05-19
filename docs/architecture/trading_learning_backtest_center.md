# Trading Learning / Backtest Center

Status: Draft / UI Architecture Specification
Scope: Future Jarvis Trading Learning, Backtest, Prediction Feedback, and
Approval UI
Current implementation status: not implemented
Gradio status: developer/test UI only

## Purpose

Jarvis soll spaeter ein Trading Learning / Backtest Center bekommen. Dieses
Center macht sichtbar, was Hermes aus Backtests, Prediction Feedback, Setup
Watch Ergebnissen und Trading-Lernkandidaten ableitet.

Das Center ist eine Analyse-, Review- und Approval-Oberflaeche. Es ist keine
Order-Oberflaeche und keine Broker-Steuerung.

## Non-Goals

- Keine UI-Implementierung.
- Keine Backtest-Ausfuehrung.
- Keine cTrader-Verbindung.
- Keine Broker-Trade-Verbindung.
- Keine Orders.
- Keine Runtime-Dateien.
- Keine Services.
- Keine automatische Persistenz von Learnings.
- Keine automatische Strategie-Aktivierung.

## Core Positioning

Jarvis zeigt Trading-Backtests, Prediction Outcomes, Setup Watch Ergebnisse und
Lernkandidaten sichtbar an. Hermes bereitet Analysen und Vorschlaege vor.
Frank entscheidet, welche Learnings dauerhaft uebernommen werden.

Grundregeln:

- `no_auto_trading`
- `read_only_backtesting`
- `human_review_required`
- `no_silent_learning`
- `no_trade_execution`
- `trade_connection_disabled`

## Main Areas

### A. Overnight Research Mode

Der Overnight Research Mode ist ein spaeterer Analysemodus fuer gespeicherte
historische Daten.

Ziel:

- nachts historische oder gespeicherte Marktdaten analysieren
- keine Live-Orders
- keine Broker-Trade-Verbindung
- keine aktive cTrader TRADE-Verbindung
- morgens einen Trading Research Report erzeugen
- Lernkandidaten fuer Review erzeugen
- keine Learnings automatisch persistieren

Morgendlicher Report:

- analysierte Symbole
- analysierter Zeitraum
- getestete Strategien
- beste Setups
- schlechteste Setups
- No-Trade-Zonen
- Marktregime-Zusammenfassung
- Confidence-vs-Outcome-Auswertung
- Risk-/Drawdown-Hinweise
- Learning-Vorschlaege
- offene Approval-Entscheidungen

Frank entscheidet, welche Erkenntnisse dauerhaft als Memory, Routing Hint,
Trading Pattern oder Strategie-Hinweis uebernommen werden.

### B. Backtest Runs

Backtest Runs zeigen einzelne historische Testlaeufe.

Sichtbare Felder:

- Run ID
- Symbol: XAUUSD, EURUSD oder GER40
- Zeitraum
- Timeframe
- Strategie
- Strategieversion spaeter
- Datenquelle
- Ausfuehrungsmodus: `read_only_backtesting`
- Winrate
- Profit Factor
- Drawdown
- Trefferqualitaet
- Anzahl Trades / Setups
- durchschnittliches Risiko-Rendite-Verhaeltnis spaeter
- Ergebnisstatus
- Safety Flags

Backtest Runs duerfen keine Orders ausloesen und keine Live-Regeln aktivieren.

### C. Strategy Comparison

Strategy Comparison vergleicht Strategien ueber Symbole, Zeitraeume,
Timeframes und Marktregime.

Vergleichskandidaten:

- Trend Pullback
- Breakout
- Mean Reversion
- No-Trade-Filter
- spaetere Strategievarianten

Sichtbare Metriken:

- Winrate
- Profit Factor
- Max Drawdown
- Stabilitaet ueber Zeitraeume
- Performance pro Symbol
- Performance pro Session
- Performance pro Marktregime
- Trefferqualitaet
- Confidence Calibration
- beste Setups
- schlechteste Setups

Strategien mit schwacher oder instabiler Performance sollen als Review-Hinweis
markiert werden, nicht automatisch live aktiviert oder gehandelt werden.

### D. Market Regime Analysis

Market Regime Analysis zeigt, in welchem Marktumfeld Strategien funktionieren
oder versagen.

Regime-Kandidaten:

- Trendmarkt
- Seitwaertsmarkt
- hohe Volatilitaet
- News-Markt
- illiquide Marktphasen
- London Session
- New York Session
- Spread- / Liquiditaetsproblem

UI-Ziel:

- pro Regime passende und unpassende Strategien sichtbar machen
- No-Trade-Zonen markieren
- Regime-Wechsel als Analysekontext zeigen
- Gold, Forex und GER40 getrennt bewerten

### E. Prediction Feedback

Prediction Feedback verbindet Prognose, Outcome und Bewertung.

Sichtbare Felder:

- Prediction ID
- Symbol
- Zeitraum / Timestamp
- Timeframe
- erwartete Richtung
- erwartetes Setup
- Confidence
- tatsaechliches Outcome
- Treffer / Fehlprognose / unklar
- Abweichung zwischen Erwartung und Ergebnis
- relevante Features
- Session-Kontext
- Marktregime
- manuelle Bewertung
- Learning-Vorschlag

Ziel ist ein nachvollziehbarer Pfad:

```text
Prediction -> Outcome -> Bewertung -> Learning Candidate -> Approval
```

### F. Setup Watch Results

Setup Watch Results zeigen, welche beobachteten Setups entstanden, getriggert
wurden oder abgelaufen sind.

Statusmodell:

- `watching`
- `armed`
- `triggered`
- `expired`

Sichtbare Felder:

- Setup ID
- Symbol
- Timeframe
- Long- oder Short-Szenario
- Trigger-Bedingungen
- Entry-Zone
- Confidence / Wahrscheinlichkeit
- Stop-Loss-Vorschlag
- Take-Profit- / Zielzonen
- Invalidation-Level
- Ergebnis nach Ablauf oder Trigger
- Grund fuer Expiry oder Invalidation
- Learning-Vorschlag

Signal Alerts bleiben reviewbare Hinweise. Sie sind keine Ausfuehrungsbefehle.

### G. Confidence Calibration

Confidence Calibration zeigt, ob Hermes' Wahrscheinlichkeiten zur spaeteren
Realitaet passen.

Sichtbare Auswertungen:

- Confidence vs Outcome
- Trefferquote pro Confidence-Bucket
- Overconfidence-Hinweise
- Underconfidence-Hinweise
- Kalibrierung pro Symbol
- Kalibrierung pro Strategie
- Kalibrierung pro Marktregime
- Kalibrierung pro Session

Ziel ist bessere Bewertung von Setups und Signalen, nicht automatische
Orderausfuehrung.

### H. Learning Candidates

Learning Candidates sind Vorschlaege, die Hermes aus Backtests, Prediction
Feedback und Setup Watch Ergebnissen ableitet.

Kandidatentypen:

- Strategie-Hinweis
- No-Trade-Zone
- Feature-Hinweis
- Confidence-Anpassung
- Risk-Hinweis
- Market-Regime-Hinweis
- Setup-Pattern
- Fehlerpattern
- Research-/Roadmap-Hinweis

Jeder Kandidat soll zeigen:

- Quelle
- Evidenz
- betroffene Symbole
- betroffener Zeitraum
- vorgeschlagener Zielort: Memory, Routing Hint, Trading Pattern, Roadmap oder
  Archiv
- Risiko-Level
- Reviewer
- Status

### I. Approval Queue

Die Approval Queue ist der Gatekeeper vor dauerhaften Trading-Learnings.

Moegliche Aktionen:

- `approve`
- `reject`
- `defer`
- `request_more_context`
- `archive`

Approval-Regeln:

- keine dauerhaften Learnings ohne Review
- keine versteckten Trading-Learnings
- keine automatische Strategie-Aktivierung
- keine Risikoerhoehung aus einem Learning heraus
- Shared Memory nur nach Approval

### J. Risk / Safety Summary

Die Risk / Safety Summary zeigt, ob alle Trading-Sicherheitsregeln eingehalten
werden.

Pflichtanzeigen:

- `no_auto_trading`
- `read_only_backtesting`
- `human_review_required`
- `no_silent_learning`
- `no_risk_increase_after_loss`
- `no_martingale`
- `no_grid`
- `trade_execution_enabled: false`
- `broker_trade_connection_enabled: false`

Spaetere Pflicht-Gates vor Live-Phasen:

- Kill Switch
- Drawdown Limits
- Tagesverlustlimit
- Wochenverlustlimit
- Audit Log
- explizite Live-Approval

## UI Data Summary

Das Center soll mindestens diese Trading-Daten sichtbar machen:

- Symbol: XAUUSD, EURUSD oder GER40
- Zeitraum
- Strategie
- Winrate
- Profit Factor
- Drawdown
- Trefferqualitaet
- Confidence vs Outcome
- beste Setups
- schlechteste Setups
- No-Trade-Zonen
- Learning-Vorschlaege

## Future Integration

Spaetere Integrationspunkte:

- Runtime Event Bus
- Jarvis Learning UI
- Trading Intelligence Status
- cTrader QUOTE Data Store
- Prediction Feedback Loop
- Shared Memory Approval
- Runtime Event Standardisierung fuer Backtest-, Setup-Watch-, Prediction- und
  Approval-Events

## Event Ideas For Later

Moegliche Runtime Event Types:

- `overnight_research_planned`
- `overnight_research_report_created`
- `backtest_run_recorded`
- `strategy_comparison_updated`
- `market_regime_analysis_updated`
- `prediction_feedback_recorded`
- `setup_watch_result_recorded`
- `confidence_calibration_updated`
- `trading_learning_candidate_created`
- `trading_learning_approval_requested`
- `trading_learning_approval_resolved`
- `trading_safety_gate_blocked`

Diese Events sind Status- und Review-Objekte, keine Commands.

## Safety Boundaries

Das Trading Learning / Backtest Center darf niemals:

- Orders platzieren
- Broker-Trades ausloesen
- cTrader TRADE aktivieren
- Strategien automatisch live schalten
- Risiko nach Verlusten erhoehen
- Martingale- oder Grid-Logik empfehlen
- Learnings still persistieren
- Secrets, Broker-Credentials oder `.env.local` Inhalte anzeigen

## Acceptance Criteria For Future Implementation

Eine spaetere Implementierung ist nur dann aligned, wenn:

- Backtests read-only bleiben.
- `no_auto_trading` dauerhaft sichtbar ist.
- Prediction Feedback nachvollziehbar von Prognose zu Outcome zu Learning
  fuehrt.
- Setup Watch Ergebnisse mit Status, Triggern, SL/TP und Invalidation sichtbar
  sind.
- Learning Candidates in der Approval Queue landen.
- Frank vor dauerhafter Uebernahme entscheidet.
- Risk/Safety Summary prominent sichtbar ist.
- Keine Broker-Trade-Verbindung fuer dieses Center erforderlich ist.

## Implementation Status

Dieses Dokument ist nur Spezifikation. Es implementiert keine UI, fuehrt keine
Backtests aus, startet keine Services und veraendert keine Runtime-Daten.
