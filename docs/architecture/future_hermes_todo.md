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

### LATER

- automatische Skill-Generierung aus Apify / MCP
- WebSocket Live Runtime
- Cross-device Memory Sync Engine
- Agent-to-Agent Consensus
- advanced Token Dashboard
- Trading ML Training Pipeline

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
- Skill Versioning
- Skill Review Workflow
- klare Safety Flags pro Skill
- Aktivierung erst nach Review
- Trennung zwischen vorgeschlagenen, freigegebenen und aktiven Skills

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

## J) Agent Dashboard / Control Interface

Zielbild: Jarvis wird zum lokalen Control Center fuer Hermes, Agenten, Skills,
Memory, Runtime und Trading-Alerts.

Dashboard-Panels:

- lokale Runtime Health
- aktive Agenten
- Skills
- Memory Status
- Sessions
- Files / Runtime Controls
- Taskline
- Activity Feed
- Logs / Audit Trail
- Approval Queue
- Trading Alerts
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
- ChatGPT-Codex primaer
- OpenRouter als Fallback
- Ollama / local fuer kleine Aufgaben
- cost-aware orchestration
- Token Dashboards pruefen
- Modellrouting nach Taskklasse
- Credit-Ueberwachung

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

Ziel: Trading Intelligence bleibt Analyse- und Lernschicht. Automatischer
Handel ist ausgeschlossen, bis explizit und separat freigegeben.

Fokus:

- cTrader QUOTE Feed
- XAUUSD / EURUSD / GER40
- Prediction Feedback Learning
- XGBoost / LightGBM Training Pipeline
- Feature Engine
- Feature Importance
- Session Features London / New York
- `no_auto_trading`
- `human_review_required`
- TRADE-Verbindung bleibt deaktiviert bis explizite Freigabe

Safety:

- QUOTE ist read-only Marktdatenquelle.
- TRADE bleibt deaktiviert.
- Keine Order-Controls im Analysepfad.
- Modelle duerfen Prognosen bewerten, aber keine Orders ausloesen.

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

Regeln:

- beide Repositories nur als Inspirations- / Pattern-Quelle aufnehmen
- nichts direkt kopieren
- Lizenz pruefen
- keine fremden Skills automatisch aktivieren
- keine Repositories klonen ohne explizite Freigabe

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

Für Masterplan 6 übernehmen
