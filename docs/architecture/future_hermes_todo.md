# Future Hermes Todo / Roadmap Intake

Status: Konzept- und Sammeldokument fuer Masterplan 6.

Zweck: Neue externe Architekturideen, Runtime-Konzepte, Skills,
Dashboard-Ideen, Research-Agent-Konzepte und Trading-Learnings sollen hier
kuratiert gesammelt werden, ohne sie direkt zu implementieren.

Nicht-Ziele:

- keine Runtime-Dateien aendern
- keine Services starten
- keine externen Repositories klonen
- keine echten Web-, Reddit-, GitHub- oder arXiv-Abfragen implementieren
- keine API-Keys, Secrets oder `.env.local` erfassen
- keine autonomen Installationen
- keine Commits oder Pushes

Leitregel:

Hermes darf Ideen, Reports, Skill-Vorschlaege und Memory-Vorschlaege
vorbereiten. Dauerhafte Uebernahme, Aktivierung oder Codeaenderung bleibt
human-in-the-loop und reviewpflichtig.

---

## Priorisierung fuer Masterplan 6

### MUST

- Multi-PC Shared Learning
- Runtime Supervisor
- Memory Architecture
- Research / Discovery Agent
- Cost-aware Codex / OpenRouter / Ollama Strategy
- Trading `no_auto_trading` Safety
- Trading Setup Watch
- Trading Prediction Feedback Loop
- Trading Continuous Backtesting
- Trading Safety Gates
- Jarvis Learning UI
- Runtime Event Standardisierung
- `human_review_required` fuer Trading und riskante Runtime-Aktionen
- Multi-Agent Workflow Architecture
- External Pattern Review

### SHOULD

- Hermes Skills System
- Skill Registry
- Skill Review Workflow
- Skill Generator
- MCP Gateway
- Agent Dashboard Erweiterung
- Reflective Learning Phase
- Obsidian Knowledge Integration
- Market Regime Detection
- Strategy Library
- Signal Score System
- Risk Agent
- Fish Audio / Fish Speech Evaluation
- Unsloth / Local Fine-Tuning Evaluation
- Anthropic Plugin Pattern Review
- Skill / Connector / Sub-Agent Pattern

### LATER

- automatische Skill-Generierung aus Apify / MCP
- WebSocket Live Runtime
- Cross-device Memory Sync Engine
- Agent-to-Agent Consensus
- advanced Token Dashboard
- Trading ML Training Pipeline
- Paper Trading
- Demo Trading
- Micro Live Trading mit Approval
- Optional Autotrading
- Local Fine-Tuning
- Voice Personality Layer
- Full MCP Connector Marketplace

---

## A) Multi-PC Hermes Architecture

Zielbild: Zwei oder mehr PCs koennen dieselben freigegebenen Erfahrungen und
Dokumente nutzen, ohne lokale Runtime-, Cache- oder Secret-Daten zu vermischen.

Grundsaetze:

- Code und Dokumentation laufen ueber GitHub.
- Obsidian- und Wissensnotizen sollen synchronisierbar bleiben.
- Approved Learnings sollen synchronisierbar werden.
- Runtime, Logs, Cache und Secrets bleiben lokal.
- `.env.local` wird niemals synchronisiert.
- `.hermes/runtime` bleibt lokal.
- Ein kuenftiger `memory_shared/` Layer kann genehmigte Learnings aufnehmen.

Offene Architekturfragen:

- Welche Inhalte gehoeren in `memory_shared/` statt in `.hermes/` oder
  `memory/`?
- Wie werden Konflikte zwischen zwei PCs geloest?
- Wie wird sichtbar, welche Learnings lokal, shared oder veraltet sind?
- Welche Review-Regeln gelten vor Sync und vor Import?

## B) Shared Learning System

Ziel: Hermes soll verwertbare Erfahrungen teilen koennen, aber niemals rohe
Runtime-Daten unkontrolliert synchronisieren.

Regeln:

- Nur genehmigte Learnings synchronisieren.
- Keine unkontrollierte Runtime-Synchronisation.
- Skill-Synchronisation nur mit Review.
- Routing-Hints-Synchronisation nur als freigegebene, auditierbare Hinweise.
- Trading-Pattern-Synchronisation nur mit Safety Flags.
- Dauerhafte Uebernahme braucht einen Approval Workflow.

Kandidaten fuer Shared Learning:

- wiederverwendbare Routing Hints
- freigegebene Skill-Versionen
- Trading-Pattern-Auswertungen
- Debugging- und Runtime-Patterns
- Architekturentscheidungen fuer Masterplan / Roadmap

## C) Hermes Research / Discovery Agent

Ziel: Hermes soll regelmaessig externe Ideen beobachten und als kuratierte
Vorschlaege ausgeben. Diese Recherche bleibt read-only und erzeugt keine
Codeaenderungen.

Beobachtungsfelder:

- Reddit-Foren mit AI-Agent-, Local-LLM- und Developer-Automation-Ideen
- LangGraph
- CrewAI
- AutoGen
- MCP
- OpenClaw
- SWE-Agent / OpenDevin
- Ollama-, OpenRouter- und Local-Model-News
- Trading-AI- und cTrader-Themen
- interessante GitHub-Projekte
- arXiv / Paper mit Agent-, Memory-, Tool- oder Runtime-Bezug

Ergebnisse:

- kuratierte Vorschlaege
- Quellenliste mit Datum
- Relevanzbewertung fuer Jarvis / Hermes
- Risiko- und Lizenzhinweise
- Kandidaten fuer Masterplan, Roadmap oder Skill Review

## D) Research Safety

Research-Agenten arbeiten ausschliesslich read-only.

Safety-Regeln:

- keine autonomen Codeaenderungen
- keine autonomen Installationen
- keine ungeprueften Empfehlungen direkt uebernehmen
- `human_review_required`
- Quellen und Datum dokumentieren
- Lizenzlage markieren, wenn externe Projekte als Inspiration dienen
- Keine Secrets, Tokens oder privaten Inhalte sammeln

## E) Future Knowledge Discovery Pipeline

Pipeline-Ziel: Externe Ideen regelmaessig sammeln, deduplizieren, bewerten und
als Kandidaten fuer Masterplan / Roadmap ausgeben.

Pipeline-Schritte:

- Reddit Monitoring
- GitHub Trend Monitoring
- arXiv Monitoring
- MCP Ecosystem Tracking
- Hermes-Agent Ecosystem Tracking
- Duplicate Filtering
- Idea Extraction
- Weekly Summary
- Candidate Ideas fuer Masterplan / Roadmap

Wichtig:

- Jede Quelle braucht Datum und Herkunft.
- Wiederholte Ideen sollen zusammengefuehrt werden.
- Ergebnisse bleiben Vorschlaege, keine automatische Umsetzung.

## F) Runtime Supervisor / Scheduler

Ziel: Hermes braucht spaeter eine kontrollierte Supervisor- und Scheduler-Ebene,
die periodische Aufgaben, Agentenlaufzeiten und Ressourcen begrenzt.

Konzepte:

- periodische Research Tasks
- Background Jobs
- Agent Lifecycle
- Zombie Protection
- Context Lifecycle
- Context Compression
- Resource Limits
- Runtime Cleanup
- Health Checks
- Heartbeat
- Hallucination Gate
- Retry Budget / `max_retries` pro Task
- 5-Minuten-cTrader-QUOTE-Checks spaeter
- Scheduler- / Cron-Struktur
- Cron als Agent-Jobs statt reine Shell-Crons

Safety:

- Jobs starten nicht heimlich Services.
- Jeder Job hat Zweck, Owner, Limit, Retry Budget und Audit Trail.
- Schreibende oder riskante Jobs brauchen Approval.
- Research Jobs bleiben read-only.

## G) Hermes Skills System

Ziel: Skills werden als wiederverwendbare Markdown-Playbooks und prozedurales
Gedaechtnis fuer Hermes behandelt.

Skill-Kategorien:

- Trading Skills
- Debugging Skills
- Runtime Skills
- UI Skills
- Deployment Skills
- Architektur- und System-Design-Skills
- Codex-Workflow-Skills

Architekturpunkte:

- Shared Skill Architecture
- Skill Registry
- Tool Registry
- Connector Registry
- Skill Versioning
- Skill Review Workflow
- klare Safety Flags pro Skill
- Aktivierung erst nach Review
- Trennung zwischen vorgeschlagenen, freigegebenen und aktiven Skills
- Skills + Connectors + Sub-Agents + Workflows als spaeteres Hermes-Pattern
- Multi-PC-Synchronisation nur fuer gepruefte und freigegebene Skills

## H) Hermes Skill Generator

Ziel: Aus API-, Tool- und Provider-Spezifikationen koennen spaeter
Agent-Skills vorbereitet werden. Generierte Skills werden niemals automatisch
aktiviert.

Moegliche Quellen:

- Apify Actors
- MCP Tools
- lokale CLI-Tools
- cTrader QUOTE Bridge
- Weather Provider
- Reddit / GitHub Research Tools

Generator-Ausgaben:

- Skill-Doku
- Input-Schema
- Execution Contract
- Testprompts
- Usage Notes
- Safety Flags
- Pagination- und Output-Limit-Hinweise
- Kosten- und Rate-Limit-Metadaten

Regeln:

- `human_review_required` vor Aktivierung
- keine echte API-Nutzung waehrend der Generierung ohne Freigabe
- keine Secrets erfassen
- Kosten- und Rate-Limits sichtbar machen

## I) MCP / Tool Standardisierung

Ziel: Tools sollen spaeter ueber standardisierte Contracts, Rechte und Registry
kontrollierbar sein.

Konzepte:

- zukuenftiges MCP Gateway
- Tool Registry
- Connector Registry
- Skill Registry
- standardisierte Tool Contracts
- Filesystem Tools
- Browser Tools
- Voice Tools
- Runtime Tools
- cTrader MCP spaeter moeglich
- sichere Berechtigungsmodelle
- read-only Tools zuerst
- Hermes spaeter als MCP Client und MCP Server denkbar

Safety:

- minimale Rechte pro Tool
- klare Unterscheidung zwischen read-only, write, execute und external access
- Tool-Ausfuehrung auditierbar machen
- Approval Gates, Safety Flags und Versionierung pro Tool / Skill / Connector

## J) Agent Dashboard / Control Interface

Zielbild: Jarvis wird zum lokalen Control Center fuer Hermes, Agenten, Skills,
Memory, Runtime und Trading-Alerts.

Dashboard-Panels:

- lokale Runtime Health
- aktive Agenten
- sichtbare Agent Chains / Agent-Flows
- Skills
- Memory Status
- Sessions
- Files / Runtime Controls
- Taskline
- Activity Feed
- Logs / Audit Trail
- Approval Queue
- Trading Alerts
- Setup Watch Status
- Trigger-Status fuer Trading-Signale
- Voice Status
- Cost / Credit Panels

Control-Center-Regeln:

- Status zuerst read-only sichtbar machen.
- Riskante Aktionen bleiben approval-gesteuert.
- Runtime Controls muessen klar von Logs, Memory und Config getrennt sein.

## K) Obsidian + Memory Layer

Ziel: Human Knowledge und Machine Runtime Knowledge bleiben getrennt, koennen
aber spaeter bewusst verbunden werden.

Ebenen:

- Obsidian als Human Knowledge Layer
- `.hermes/` als Runtime Learning Layer
- `memory/` als Structured Memory Layer
- Obsidian Knowledge Graph spaeter
- Keep-Alive- / Long-Term-Memory-Konzepte pruefen

Regeln:

- Obsidian bleibt menschlich lesbar und planungsorientiert.
- `.hermes/` bleibt laufzeitnah und lokal.
- `memory/` enthaelt strukturierte, freigegebene Inhalte.
- Keine Vermischung von Human Knowledge und Machine Runtime Knowledge.

## L) Cost / Token Optimization

Ziel: Hermes soll Modell- und Provider-Auswahl kostenbewusst, transparent und
aufgabenabhaengig steuern.

Strategie:

- Fast Mode standardmaessig aus
- Fast Mode nur fuer grosse Refactors
- GPT-5.5 / ChatGPT-Codex primaer als Senior Architect fuer komplexe
  Architektur, Reviews und Multi-File-Umsetzungen
- OpenRouter als Fallback
- Ollama / OSS / Qwen als Local Worker fuer kleine, gut begrenzte Aufgaben
- cost-aware orchestration
- Token Dashboards pruefen
- Modellrouting nach Taskklasse
- Credit-Ueberwachung
- Qwen2.5-Coder ist lokal fuer kleine Aufgaben nutzbar, aber nicht
  verlaesslich genug fuer komplexe Agenten-Workflows
- `gpt-oss:20b` und `qwen2.5-coder` weiter evaluieren
- Beide Codex-Fenster duerfen nie gleichzeitig dieselben Dateien bearbeiten

Offene Punkte:

- Welche Taskklassen duerfen externe Provider nutzen?
- Welche Limits gelten pro Tag, Task und Agent?
- Wie wird Credit-Verbrauch im Dashboard sichtbar?

## M) Memory Architecture

Ziel: Memory wird als kontrollierte Architektur geplant, nicht als Sammlung
unklarer Nebenstores.

Ebenen:

- `.hermes/` = Runtime Learning
- `memory/` = Structured Memory
- `obsidian/` = Human Knowledge Layer
- kuenftiger Memory Manager
- semantic retrieval
- knowledge aging
- Obsidian export
- approval-based persistence
- context compression
- memory prioritization
- archive strategy
- pruning
- disk limits
- keine Orphan- / Shadow-Stores
- Single-Store-Design pruefen

Regeln:

- Dauerhafte Speicherung braucht klare Quelle, Zweck, Scope und Review-Status.
- Memory darf keine rohe Runtime-Kopie werden.
- Shared Memory braucht separate Freigabe.

## N) Trading Intelligence

Ziel: Trading Intelligence bleibt Analyse-, Setup-Watch-, Backtesting- und
Lernschicht. Automatischer Handel ist in der aktuellen Phase ausgeschlossen.

Grundsaetze:

- Hermes erzeugt nicht nur Signale aus Indikatoren, sondern bewertet
  historische Daten, Marktregime, Hypothesen, Prediction Outcomes und
  Safety-Kontext.
- cTrader QUOTE ist die geplante read-only Marktdatenquelle.
- Ziel-Symbole: XAUUSD, EURUSD, GER40, US500 und Forex-Majors.
- TRADE bleibt deaktiviert bis zu einer separaten, explizit freigegebenen
  Zukunftsphase.
- Entscheidungen bleiben bei Frank.
- Composite Trading Indicator liefert spaeter Features, Marktstruktur, Scores
  und nicht-repaintende Signal-Kandidaten.
- Hermes-Agent bleibt die lernende Bewertungs- und Entscheidungsinstanz.
- Trennung: Indicator liefert Features, Hermes lernt und entscheidet.
- Trading Feature Store als spaetere strukturierte Feature-Basis.

### N.1 Continuous Backtesting Intelligence

Hermes soll kontinuierlich historische cTrader-Daten auswerten und daraus
reviewbare Trading-Learnings vorbereiten.

Geplante Faehigkeiten:

- kontinuierliche Backtests fuer XAUUSD, EURUSD, GER40, US500 und Forex-Majors
- automatische Hypothesentests
- Feature-, Session- und Timeframe-Vergleich
- Walk-Forward-Tests
- Out-of-Sample-Tests
- Paper-Trading-Phase
- Live-vs-Backtest-Vergleich
- Demo- / Forward-Tests spaeter
- Prediction Feedback Loop
- Confidence Calibration
- Feature Importance Tracking
- Modellvergleich XGBoost / LightGBM
- schlechte Strategien markieren oder deaktivieren
- Risiko reduzieren, wenn Performance schwach ist
- keine automatische Orderausfuehrung

### N.2 Setup Watch & Signal Alerts

Hermes soll moegliche Long- und Short-Szenarien frueh erkennen, ohne sofort ein
Signal auszuloesen.

Zielbild:

- Vorwarnung: "Setup koennte entstehen."
- typisches Zeitfenster: 10 bis 30 Minuten
- Trigger-Bedingungen sichtbar machen
- Entry-Zone sichtbar machen
- Entry- und Exit-Warnungen sichtbar machen
- Wahrscheinlichkeit / Confidence anzeigen
- Stop-Loss-Vorschlag anzeigen
- Take-Profit- / Zielzonen anzeigen
- Invalidation-Level anzeigen
- No-Trade-Zonen sichtbar machen
- Signal erst ausloesen, wenn Bedingungen eintreten
- nicht-repaintende Signale nur nach Kerzenschluss werten
- Entscheidung bleibt bei Frank
- kein Auto-Trading in der aktuellen Phase

Statusmodell:

- `watching`
- `armed`
- `triggered`
- `expired`

### N.3 Trading Autonomy Roadmap

Autonomie wird stufenweise geplant und bleibt jeweils approval-gesteuert.

Roadmap-Stufen:

1. `analysis_only`
2. `setup_watch`
3. `signal_alerts`
4. `prediction_feedback`
5. `continuous_backtesting`
6. `paper_trading`
7. `demo_trading`
8. `micro_live_with_approval`
9. `approval_required_live_trading`
10. `later_optional_autotrading`

Vollautomatik ist nur eine spaetere Option, keine aktuelle Zielphase.

### N.4 Trading Safety Gates

Aktive und spaetere Safety Gates:

- `no_auto_trading` ist aktuell aktiv.
- Human Approval bleibt Pflicht.
- Kill Switch spaeter verpflichtend.
- Drawdown Limits spaeter verpflichtend.
- Tagesverlustlimit spaeter verpflichtend.
- Wochenverlustlimit spaeter verpflichtend.
- Risiko pro Trade 0,25-1 % als spaetere Richtlinie.
- kein Martingale
- kein Grid
- keine Risikoerhoehung nach Verlust
- keine ungetesteten Regeln live einsetzen
- Audit Log spaeter verpflichtend

### N.5 Trading Agent Modularisierung

Hermes bleibt Orchestrator. Trading-Faehigkeiten werden spaeter in
spezialisierte Agenten getrennt:

- Market Data Agent
- Market Regime Agent
- Setup Watch Agent
- Signal Scoring Agent
- Risk Agent
- Backtesting Agent
- Prediction Review Agent
- News Context Agent
- Research Agent

### N.6 Market Regime Detection

Geplante Regime-Erkennung:

- Trendmarkt
- Seitwaertsmarkt
- hohe Volatilitaet
- News-Markt
- illiquide Marktphasen
- Spread- / Liquiditaetsfilter
- Session-Fokus London / New York

### N.7 Strategy Library

Strategien werden als bewertbare Bibliothek und Pattern-Cluster geplant, nicht
als feste Black-Box-Regeln.

Kandidaten:

- Trend Pullback
- Breakout
- Mean Reversion
- No-Trade-Filter
- Strategie-Bewertung pro Marktregime
- Gold, Indizes und Forex getrennt behandeln
- Pattern-Cluster statt starre Regeln

### N.7a cTrader / C# Zielarchitektur

Zielstruktur:

- Core: gemeinsame Berechnungen, Feature-Definitionen und Signal Contracts
- Indicator: Composite Trading Indicator fuer Visualisierung und Feature-
  Ausgabe
- Bot: spaetere kontrollierte Ausfuehrungsschicht, aktuell deaktiviert
- Analyzer: Backtest-, Feature- und Ergebnisanalyse
- Research: Overnight Research, Hypothesentests und Report-Erzeugung

### N.8 Signal Score System

Ein Signal Score soll Faktoren transparent gewichten:

- Trend
- Momentum
- Spread
- ATR
- News-Risiko
- Session
- Volatilitaet
- Higher Timeframe Alignment
- Market Structure
- Rejection Quality

### N.9 Trading Learning UI / Runtime Events

Trading-Learnings muessen in Jarvis sichtbar und reviewbar werden:

- Learning UI zeigt Trading Feedback.
- Trading Learning / Backtest Center zeigt Backtest Runs, Strategy Comparison,
  Prediction Feedback, Setup Watch Results und Confidence Calibration.
- Setup Watch und Trigger-Status sind sichtbar.
- Prediction -> Ergebnis -> Bewertung -> Learning ist nachvollziehbar.
- Approval Center fuer Trading-Learnings.
- keine versteckten Trading-Learnings
- keine versteckten Signale
- Runtime Events fuer Setup Watch, Signal Alerts, Prediction Outcomes,
  Safety Blocks und Approval Requests standardisieren

### N.10 Overnight Research Mode

Spaeterer Analysemodus:

- historische oder gespeicherte Daten testen
- keine Orders
- keine Broker-Trade-Verbindung
- morgens Report erzeugen
- Lernkandidaten zur Freigabe vorbereiten
- Frank entscheidet, was dauerhaft gelernt wird

## O) Reflective Learning Phase

Ziel: Nach Tasks soll Hermes kurze, sichtbare Selbstanalysen erzeugen, um
Patterns, Fehler und Verbesserungen vorzubereiten.

Inhalte:

- kurze Selbstanalyse nach Tasks
- Pattern Extraction
- Skill-Vorschlaege
- Retry-Strategien verbessern
- Routing-Optimierung
- Confidence-Anpassung
- keine automatische Code-Selbstmodifikation

Regeln:

- Reflexion erzeugt Vorschlaege, keine direkten Aenderungen.
- Dauerhafte Learnings brauchen Review.
- Kritische Erkenntnisse werden mit Quelle und Task-Kontext dokumentiert.

## P) Guardrailed Self-Improvement

Ziel: Hermes darf sich verbessern, aber nicht ungeprueft produktiven Code,
Runtime-Regeln oder aktive Skills veraendern.

Erlaubt:

- Vorschlaege erzeugen
- Skills vorschlagen
- Learnings zur Freigabe vorbereiten
- Risiken und Muster markieren

Nicht erlaubt:

- ungeprueft produktiven Code aendern
- Skills automatisch aktivieren
- Runtime-Konfiguration still veraendern
- Freigabe durch Frank umgehen

Freigabe durch Frank bleibt Pflicht.

## Q) External Pattern Review

Ziel: Externe Projekte werden als Inspirations- und Pattern-Quelle beobachtet,
nicht als Copy-Paste-Vorlage.

Zu pruefende Quellen:

- wondelai / Wondel.ai Skills: `https://github.com/wondelai/skills`
- builderz-labs Mission Control: `https://github.com/builderz-labs/mission-control`
- Anthropic Financial Services Pattern
- Anthropic Legal Pattern
- Anthropic Life Sciences Pattern

Regeln:

- diese Quellen nur als Inspirations- / Pattern-Quelle aufnehmen
- nichts direkt kopieren
- Lizenz pruefen
- keine fremden Skills automatisch aktivieren
- keine Repositories klonen ohne explizite Freigabe
- Lizenz pruefen, bevor Patterns dokumentiert oder adaptiert werden
- nur Architektur-, Workflow-, Skill- und Connector-Patterns extrahieren

## R) Wondel.ai Skills als Vorlage

Ziel: Die Markdown-Playbook-Konvention kann als Inspiration fuer Hermes Skills
geprueft werden.

Zu pruefen:

- `SKILL.md`- / Markdown-Playbook-Konvention
- Skills fuer Architektur
- Clean Code
- Refactoring
- System Design
- UX
- Domain-Driven Design

Moegliche Hermes-Struktur:

```text
.hermes/skills/
  architecture/
  debugging/
  trading/
  runtime/
  ui/
  codex_workflows/
```

Regeln:

- nur Pattern extrahieren
- Lizenz und Herkunft dokumentieren
- keine fremden Skills automatisch aktivieren

## S) Mission Control als Vorlage fuer Jarvis

Ziel: Mission-Control-Pattern koennen fuer Jarvis Dashboard und Runtime Control
bewertet werden, ohne Jarvis zu ersetzen.

Zu pruefen:

- Agent Fleet View
- Task Dispatching
- Session Monitoring
- Cost Tracking
- Logs / Audit Trail
- SQLite / Event Store
- WebSocket / Live Telemetry spaeter optional

Regeln:

- nur Pattern extrahieren
- Jarvis-Architektur nicht ersetzen
- keine Fremdkomponenten uebernehmen ohne Review

## T) Hermes Guide / Practitioner Reference Patterns

Ziel: Bewaehrte lokale Agent-Setup-Patterns werden als Referenz fuer Hermes
gesammelt und spaeter auf Jarvis uebertragen.

Referenzideen:

- local-first Ollama setup
- Cloud nur optional, als Fallback und kostenbewusst
- Dashboard-Panels: Status, Sessions, Analytics, Logs, Cron, Skills, Config,
  Keys
- Skills als prozedurales Gedaechtnis
- ProviderProfile- / Plugin-Idee
- SOUL.md / AGENTS.md / MEMORY.md Konzepte pruefen
- Messaging Gateway spaeter optional: Telegram, Discord, Slack, Email

Bewertungsfragen:

- Was passt zu Jarvis als lokalem Control Center?
- Welche Pattern sind nur Inspiration und nicht direkt uebernehmbar?
- Welche Config-/Key-Panels duerfen nur sichere Metadaten zeigen?
- Welche Messaging-Kanaele bleiben optional und approval-gesteuert?

## U) Local Fine-Tuning / Unsloth Evaluation

Ziel: Lokales Fine-Tuning bleibt eine spaetere Option und darf erst nach einer
stabilen, geprueften Datenbasis bewertet werden.

Grundsaetze:

- Unsloth / Fine-Tuning als spaetere Option pruefen.
- Kein fruehes Modelltraining auf chaotische oder ungepruefte Daten.
- Dataset-Erstellung bleibt approval-basiert.
- Fine-Tuning darf keine versteckten Runtime- oder Trading-Aenderungen
  aktivieren.

Moegliche spaetere Use Cases:

- Routing-Modell
- Trading-Scoring
- Memory-Ranking
- Voice- / Personality-Verhalten
- Skill-Auswahl

Voraussetzungen:

- gepruefte Prediction History
- Human Feedback
- saubere Labels
- stabile Backtest-Ergebnisse
- Approval-basierte Dataset-Erstellung

## V) Fish Audio / Fish Speech Evaluation

Ziel: Fish Audio / Fish Speech als spaetere Voice-Runtime-Option fuer Jarvis
pruefen.

Zielbild:

- lokale oder streamingfaehige TTS-Stimme fuer Jarvis
- Local-first bevorzugt
- keine Sofort-Implementierung
- erst Voice Runtime stabilisieren

Moegliche Use Cases:

- Trading Alerts
- Runtime Warnings
- Voice Responses
- Setup Watch Vorwarnungen
- Systemstatus

## W) Anthropic Marketplace / Plugin Pattern Review

Ziel: Anthropic-Beispiele fuer spezialisierte Agenten, Skills und Connectoren
als Pattern-Quelle pruefen, ohne Inhalte direkt zu kopieren.

Zu pruefen:

- Financial Services: Referenzagenten, Skills und Data Connectors fuer
  Investment Banking, Equity Research, Private Equity und Wealth Management.
- Legal: spezialisierte Workflows fuer in-house commercial, privacy,
  corporate, litigation, regulatory, AI governance und weitere Rechtsbereiche.
- Life Sciences: MCP-Server und Skills fuer spezialisierte Forschungs- und
  Analysewerkzeuge.

Regeln:

- nichts direkt kopieren
- Lizenz pruefen
- nur Patterns extrahieren
- keine externen Repositories klonen ohne explizite Freigabe
- keine externen API- oder Web-Abfragen automatisch implementieren

## X) Multi-Agent Workflow Architecture

Ziel: Hermes wird nicht als Super-Agent-Monolith geplant, sondern als
sichtbarer Orchestrator spezialisierter Sub-Agents, Skills, Connectors und
Workflows.

Architekturprinzipien:

- keine Super-Agent-Monolithen
- spezialisierte Sub-Agents
- connector-basierte Architektur
- skill-basierte Ausfuehrung
- MCP-kompatible Tool-Schicht
- approval-aware Workflows
- Agent Chains sichtbar machen
- Runtime Events fuer Agent-Flows
- Jarvis Control Center zeigt Agentenaktivitaet

## Y) Connector / Skill / Sub-Agent Pattern

Ziel: Skills, Connectors, Sub-Agents und Workflows werden als konsistentes
Hermes-Pattern geplant.

Bausteine:

- Skill Registry
- Tool Registry
- Connector Registry
- Approval Gates
- Audit Logs
- Safety Flags
- Versionierung
- Multi-PC-Synchronisation nur fuer gepruefte Skills

## Z) Codex / Local Model Strategy

Ziel: Coding- und Modellarbeit bleibt rollenbasiert, kostentransparent und
konfliktarm.

Strategie:

- GPT-5.5 = Senior Architect fuer komplexe Architektur, Reviews und groessere
  Codex-Arbeiten.
- OSS / Qwen = Local Worker fuer kleine, klar begrenzte Aufgaben.
- OpenRouter = Fallback.
- Fast Mode standardmaessig aus.
- Fast Mode nur fuer grosse Refactors.
- Qwen2.5-Coder ist fuer lokale kleine Aufgaben nutzbar, aber nicht
  zuverlaessig genug fuer komplexe Agenten-Workflows.
- `gpt-oss:20b` und `qwen2.5-coder` weiter evaluieren.
- Beide Codex-Fenster nie gleichzeitig dieselben Dateien aendern lassen.

---

## Uebergabe an Masterplan 6

Diese Datei ist die Sammelstelle fuer Kandidaten, die spaeter in Masterplan 6
uebernommen, priorisiert oder verworfen werden.

Fuer die Uebernahme braucht jeder Kandidat:

- klare Kategorie
- Prioritaet: MUST, SHOULD oder LATER
- Safety Flags
- Review-Status
- Quellenhinweis, falls extern inspiriert
- Entscheidung: uebernehmen, zurueckstellen oder verwerfen

## Für Masterplan 7 / nächste Roadmap übernehmen

### MUST

- Trading Setup Watch
- Trading Prediction Feedback Loop
- Trading Continuous Backtesting
- Trading Safety Gates
- Jarvis Learning UI
- Runtime Event Standardisierung
- `no_auto_trading` / `human_review_required`
- Multi-Agent Workflow Architecture

### SHOULD

- Market Regime Detection
- Strategy Library
- Signal Score System
- Risk Agent
- Fish Audio Evaluation
- Unsloth Evaluation
- Anthropic Plugin Pattern Review
- Skill / Connector / Sub-Agent Pattern

### LATER

- Paper Trading
- Demo Trading
- Micro Live Trading mit Approval
- Optional Autotrading
- Local Fine-Tuning
- Voice Personality Layer
- Full MCP Connector Marketplace
- WebSocket Live UI
