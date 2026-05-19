# Jarvis Control Center UI Roadmap

Status: Draft / UI Roadmap and Prototype Plan
Scope: Visible Jarvis Control Center roadmap, UI phases, and panel plan
Current implementation status: not implemented
Gradio status: developer/test UI only

## Purpose

Jarvis braucht frueher eine sichtbare Control-Center-Oberflaeche, weil Hermes
ueber Jarvis lernen, Feedback erhalten und Entscheidungen sichtbar machen soll.
Die UI ist damit nicht nur ein spaeter Polish-Schritt, sondern ein Kontrollraum
fuer Learning, Trading-Analyse, Runtime Events, Agent Activity, Memory und
Approvals.

Dieses Dokument definiert eine Roadmap und einen UI-Prototyp-Plan. Es
implementiert keine UI.

## Non-Goals

- Keine finale React-Implementierung.
- Keine finale Tauri-Implementierung.
- Keine cTrader-Verbindung.
- Keine Backtests.
- Keine Runtime-Dateien.
- Keine Services.
- Keine Orders.
- Keine Commits oder Pushes.

## Core Positioning

Gradio bleibt Dev/Test UI fuer Statuschecks, JSON-Inspektion und sichere
Foundation-Validierung. Gradio ist nicht die finale Jarvis-Oberflaeche und
nicht das Ziel fuer das sichtbare Control Center.

Ziel ist ein sichtbares Jarvis Control Center:

- dunkel
- modular
- AI-first
- statusorientiert
- approval-aware
- trading-safety-first
- geeignet fuer Lernen, Review und Runtime-Ueberblick

Die UI soll zentrale Systementscheidungen sichtbar machen, bevor spaeter eine
vollwertige React/Tauri-Desktop-App entsteht.

## Control Room Scope

Das Jarvis Control Center ist Kontrollraum fuer:

- Learning
- Trading Setup Watch
- Backtesting Reports
- Approval Queue
- Runtime Events
- Agent Activity
- Memory
- Kosten / Provider
- Hermes Brain Status
- Voice Runtime

## Roadmap Phases

### Phase 1: Visual Control Center Prototype

Ziel: Ein sichtbarer, nicht-finaler Prototyp, der Jarvis erstmals wie ein
Control Center wirken laesst.

Inhalte:

- dunkles Dashboard
- grosse Statuskarten
- XAUUSD / EURUSD / Wetter
- Hermes Status
- Ollama Status
- aktive Agenten
- Taskline
- Trading Watch Panel
- Runtime Events
- Learning Queue Mock

Prototyp-Regeln:

- Mock- und Read-only-Daten sind erlaubt.
- Keine Runtime-Aktionen.
- Keine cTrader-Verbindung.
- Keine Backtests.
- Keine Broker- oder Order-Funktionen.
- `no_auto_trading` muss sichtbar sein.

Akzeptanz:

- optischer Fortschritt ist sofort erkennbar
- Hauptpanels sind als Layout sichtbar
- keine Raw-JSON-first Erfahrung
- Safety- und Approval-Zustaende sind prominent

### Phase 2: Learning / Approval UI

Ziel: Hermes Learning sichtbar und kontrollierbar machen.

Inhalte:

- Feedback Buttons
- Learning Candidates
- Approval Queue
- Reflective Learning Panel
- Memory Approval
- Routing-Feedback
- Skill- und Memory-Kandidaten

Prototyp-Regeln:

- Learnings werden nur vorgeschlagen.
- Persistenz braucht Approval.
- Keine versteckten Memory Updates.
- Shared Memory bleibt approval-basiert.

Akzeptanz:

- Feedback kann einem Task oder einer Antwort zugeordnet werden
- Learning Candidates zeigen Quelle, Evidenz, Ziel und Risiko
- Approval Center zeigt, was bei Freigabe passieren wuerde

### Phase 3: Trading Learning Center

Ziel: Trading Backtests, Prediction Feedback und Setup Watch Ergebnisse
sichtbar machen.

Inhalte:

- Backtest Runs
- Strategy Comparison
- Prediction Feedback
- Setup Watch Results
- Confidence Calibration
- Risk / Safety Summary
- No-Trade-Zonen
- beste und schlechteste Setups
- Learning-Vorschlaege

Prototyp-Regeln:

- nur historische oder gespeicherte Daten spaeter
- keine Orders
- keine Broker-Trade-Verbindung
- keine cTrader TRADE-Verbindung
- keine automatische Strategie-Aktivierung
- Frank entscheidet, was dauerhaft gelernt wird

Akzeptanz:

- Prediction -> Outcome -> Bewertung -> Learning Candidate ist sichtbar
- Setup Watch Status ist sichtbar: `watching`, `armed`, `triggered`,
  `expired`
- Confidence, SL/TP, Invalidation und Risk/Safety sind sichtbar
- `no_auto_trading`, `read_only_backtesting` und `human_review_required` sind
  sichtbar

### Phase 4: Live Runtime UI

Ziel: Die UI wird an echte Runtime-Status- und Event-Flows angebunden, sobald
die Event-Schicht stabil genug ist.

Inhalte:

- Event Stream
- WebSocket spaeter
- Activity Timeline live
- Agent status live
- Voice alerts
- Runtime warnings
- Approval Events

Prototyp-Regeln:

- WebSocket ist spaeter, nicht Phase-1-Pflicht.
- Event Stream bleibt statusorientiert.
- Events sind keine Commands.
- Secrets und rohe Tool-Ausgaben duerfen nicht ungefiltert erscheinen.

Akzeptanz:

- Runtime Events koennen in Activity Feed und Timeline dargestellt werden
- Agentenaktivitaet ist sichtbar
- Warnings und blocked states sind prominent

### Phase 5: Final React / Tauri UI

Ziel: Aus dem validierten Prototyp entsteht die hochwertige lokale Desktop-UI.

Zielqualitaet:

- futuristische Oberflaeche
- animiert
- hochwertig
- AI-first
- lokale Desktop-App
- klare Informationshierarchie
- Control-Center-Feeling

Technikoptionen:

- React / Vite
- Tauri
- FastAPI spaeter
- WebSocket / Event Stream spaeter
- lokale Event- und Approval-Schichten spaeter

Diese Phase ist keine aktuelle Implementierungsfreigabe.

## Central UI Panels

### Home Dashboard

Erster Blick auf Systemzustand und Prioritaeten.

Zeigt:

- Hermes Status
- Ollama Status
- aktive Agenten
- laufende Tasks
- XAUUSD / EURUSD / Wetter
- Runtime Warnings
- Trading Watch Summary
- Approval Queue Count
- Provider / Cost Summary

### Trading Watch

Analyse- und Setup-Watch-Panel, keine Order-Oberflaeche.

Zeigt:

- Symbol
- Timeframe
- Setup Status
- Long- / Short-Szenario
- Trigger-Bedingungen
- Entry-Zone
- Confidence
- Stop-Loss-Vorschlag
- Take-Profit- / Zielzonen
- Invalidation-Level
- `no_auto_trading`

### Backtest Center

UI fuer spaetere Backtest-Reports und Overnight Research Ergebnisse.

Zeigt:

- Backtest Runs
- Strategy Comparison
- Market Regime Analysis
- Winrate
- Profit Factor
- Drawdown
- Confidence vs Outcome
- beste Setups
- schlechteste Setups
- No-Trade-Zonen
- Learning-Vorschlaege

### Learning Queue

Staging-Bereich fuer Lernvorschlaege.

Zeigt:

- Learning Candidates
- Quelle
- Evidenz
- vorgeschlagener Zielort
- Risiko-Level
- Reviewer
- Status

### Approval Center

Gatekeeper fuer dauerhafte Aenderungen.

Zeigt:

- offene Approvals
- Auswirkungen einer Freigabe
- Zielstore
- Safety Flags
- Approve / Reject / Defer
- blocked states

### Hermes Brain

Erklaert Entscheidungen und Routing.

Zeigt:

- Intent
- Domain
- Route
- Agent
- Model / Provider
- Confidence
- Decision reasons
- Safety Gates

### Agent Dashboard

Zeigt Hermes-Agenten und spaetere Workflow-Ketten.

Zeigt:

- aktive Agenten
- geplante Agenten
- Agent Rollen
- Task Ownership
- Agent Chains
- Approval Requirements
- letzte Aktionen

### Runtime Events

Status- und Timeline-Panel fuer Systemereignisse.

Zeigt:

- Runtime Events
- Activity Timeline
- Warnings
- Failures
- Recoveries
- blocked states
- Approval Events

### Cost / Provider

Macht Cloud- und Modellnutzung sichtbar.

Zeigt:

- aktueller Provider
- aktuelles Modell
- local vs cloud
- OpenRouter Fallback Status
- Fast Mode Policy
- Kostenwarnungen spaeter
- keine versteckten Cloud-Aufrufe

### Voice Runtime

Zeigt Voice-Status ohne versteckte Audioaktionen.

Zeigt:

- Microphone State
- Wake-word State spaeter
- STT Status
- TTS Status
- Voice Provider
- Privacy Mode
- Voice Alerts spaeter

### Memory / Obsidian

Zeigt lokales, freigegebenes und menschliches Wissen getrennt.

Zeigt:

- lokale Learnings
- Approved Memory
- Shared Memory Candidates
- Routing Hints
- Obsidian Links spaeter
- was lokal-only ist
- was Approval braucht

## Design Principles

### Transparency

Die UI zeigt Systemzustand, Entscheidungen, Warnings und blockierte Aktionen
sichtbar an. Kritische Informationen duerfen nicht nur in Raw JSON stehen.

### Visible Decisions

Hermes Routing, Agent-Auswahl, Provider-Auswahl, Safety Gates und Approval
Requirements muessen nachvollziehbar sein.

### Confidence Visible

Confidence ist ein erstklassiges UI-Signal fuer Hermes Brain, Trading Setup
Watch, Prediction Feedback, Learning Candidates und Provider Routing.

### Override / Approval Controls

Frank muss riskante Aktionen, dauerhafte Learnings, Memory-Persistenz,
Skill-Aktivierung und spaetere Trading-Schritte explizit freigeben koennen.

### Visible Failures

Fehler, Fallbacks, blockierte Aktionen, fehlende Provider, unklare Outcomes und
Risk-Warnings werden sichtbar, nicht versteckt.

### Trading Safety Visible

`no_auto_trading`, `human_review_required`, `read_only_backtesting`,
`trade_execution_enabled: false`, SL/TP-Kontext und Invalidation muessen in
Trading-Panels sichtbar bleiben.

### Cloud Cost Visible

Provider, Modell, Fallback, Fast Mode Policy und spaetere Kostenwarnungen sind
sichtbar, bevor teure oder externe Arbeit passiert.

### No Hidden Actions

Die UI darf keine versteckten Actions implizieren oder ausloesen:

- keine versteckten Runtime-Aktionen
- keine versteckten Memory Writes
- keine versteckten Cloud Calls
- keine versteckten Trading-Signale
- keine Orders

## Prototype Plan

### First Visual Target

Ein statischer oder read-only Control-Center-Prototyp soll die Hauptflaechen
frueh sichtbar machen:

```text
Left:   Agent Activity / Taskline
Center: Home Dashboard / Chat / Voice context
Right:  Hermes Brain / Trading Watch / Cost Provider
Bottom: Runtime Events / Memory / Approval Queue
```

### Prototype Data Policy

Phase-1-Daten duerfen Mock-, Demo- oder read-only Foundation-Daten sein.
Wichtig ist, dass Panels, Statushierarchie, Safety Flags und Review-Flows
sichtbar werden.

Nicht erlaubt:

- echte Orders
- echte Broker-Trade-Verbindung
- echte Backtest-Ausfuehrung
- Runtime-Dateischreibzugriffe
- Service-Start
- Secrets

### Visual Priorities

- dunkler Hintergrund
- klare Panel-Hierarchie
- grosse Statuskarten fuer Top-Level-Systemzustand
- kompakte Detailpanels fuer Agenten, Trading und Runtime
- sichtbare Warnfarben fuer Warning / Critical / Blocked
- `no_auto_trading` und Approval-Zustaende permanent sichtbar
- Raw JSON nur als Developer Debug, nicht als Hauptansicht

## Dependencies Before Implementation

Vor einer echten UI-Implementierung sollten geklaert sein:

- Welche Daten kommen aus bestehenden read-only Statusmodulen?
- Welche Panels sind Mock-only in Phase 1?
- Welches minimale Event-Schema wird fuer Activity Feed genutzt?
- Welche Approval-Aktionen sind nur Anzeige und welche spaeter aktiv?
- Welche Trading-Daten sind historisch, gespeichert oder live?
- Wie wird verhindert, dass UI-Prototypen Runtime-Aktionen ausloesen?

## Acceptance Criteria For This Roadmap

Diese Roadmap ist erfuellt, wenn:

- Gradio klar Dev/Test bleibt.
- das Ziel eines sichtbaren Jarvis Control Centers definiert ist.
- UI-Learning als frueher Schwerpunkt beschrieben ist.
- alle fuenf Ausbauphasen dokumentiert sind.
- zentrale Panels und Safety-Prinzipien dokumentiert sind.
- keine Implementierung Teil dieses Dokuments ist.

## Implementation Status

Dieses Dokument ist nur UI-Planung. Es implementiert keine Oberflaeche, startet
keine Services, verbindet cTrader nicht, fuehrt keine Backtests aus und
veraendert keine Runtime-Daten.
