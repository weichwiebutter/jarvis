# JARVIS MASTERPLAN V7 - HERMES COGNITIVE CORE

Stand: 5. Juni 2026

Status: Verbindliche Architektur- und Entwicklungsgrundlage für Beta 1.

Quelle: Masterplan V6, docs/jarvis/*, aktuelle Handover-Dokumente, vorhandene Hermes Core Systeme.

---

## 1. Executive Summary

Jarvis entwickelt sich vom lokalen Agenten-Framework zum lokalen AI Operating System mit autonomem kognitiven Kern, evidenzbasiertem Wissensmanagement, Goal-orientierter Planung und Multi-Domain-Fähigkeiten.

Die zentrale Rollenverteilung bleibt:

- **Jarvis** = UI, Runtime, Voice, Status und Control Center
- **Hermes** = Cognitive Core, Brain, Routing, Planning, Learning, Evaluation, Goal Management und Wissensvalidierung
- **Ollama / lokale Modelle** = lokale Modellschicht für einfache, private und kostensensitive Aufgaben
- **Sonnet 4.5** = Referenzmodell für Architektur und komplexe Codex-Agentenarbeit
- **GPT-OSS 20B lokal** = Sparringspartner, Risikoanalyse, Gegenmeinung
- **Qwen2.5-Coder 14B lokal** = schnelle lokale Coding-Aufgaben, aber schwaches Hermes-Verständnis
- **Kimi** = offen wegen OpenRouter-Überlastung
- **Groq** = API funktioniert, aber Codex Responses API nicht kompatibel

Masterplan V7 macht den **Hermes Cognitive Core** sichtbar und dokumentiert das vorhandene autonome System aus:

- Need Detection
- Goal System
- Autonomous Planning
- Task Execution
- Outcome Evaluation
- Feedback Loops
- Knowledge Quality Management
- Validation & Promotion Engine
- Human Review Workflow
- Scheduler & Supervisor
- Master Status & Control Center

Trading ist **nur Domäne 1**. Weitere Domänen: Software, Research, Documentation, Process.

Trading hat **zwei getrennte Ziele**:

**A) Research & Bot Candidate Pipeline** (Daten, Tests, Validation, Scalping Bot Vorbereitung)

**B) Setup Watch / Interface Alerts** (frühzeitige Hinweise auf mögliche Trading-Situationen)

---

## 2. Leitentscheidungen

### 2.1 Systemrollen

- Jarvis ist das Produkt- und Control-Center-Layer
- Hermes ist das Brain und steuert Cognitive Core, Goal Management, Planning, Learning, Validation und Promotion
- Agenten sind spezialisierte Analyse- und Arbeitskontexte
- Tools sind kontrollierte Fähigkeiten mit Contract, Scope und Safety Flags
- Statusmodule sind read-only-first und dürfen keine Runtime-Aktionen auslösen

### 2.2 Local-first, Cloud Fallback

- Lokale Modelle und lokale Runtime haben Priorität
- Cloud wird nur bewusst, sichtbar und kostenbewusst genutzt
- Externe Provider laufen nur über explizite Provider-Layer
- Provider, Modell, Kostenrisiko und Approval-Status müssen sichtbar sein
- Keine versteckten Cloud-Aufrufe

### 2.3 Hybrid-Codex-Workflow

Codex bleibt Coding Worker, nicht System Brain.

Workflow:

1. Hermes/Jarvis definiert Ziel, Safety und Kontext
2. Codex setzt begrenzte Coding- oder Dokumentationsaufgaben um
3. Tests und Diffs werden sichtbar gemacht
4. Commits und Pushes bleiben menschlich kontrolliert

Modusregeln:

- **Sonnet 4.5** primär für komplexe Architektur-, Debugging- und Multi-File-Aufgaben
- **GPT-OSS 20B lokal** für Zweitmeinung, Risikoanalyse, Sparring
- **Qwen2.5-Coder 14B lokal** für kleine lokale Coding-Aufgaben, aber nicht für komplexe Hermes-Workflows
- **Kimi** offen wegen Provider-Überlastung
- **Groq** API funktioniert, aber Codex-Integration noch nicht kompatibel
- Fast Mode standardmäßig aus
- Fast Mode nur für große Refactors oder dringendes komplexes Debugging
- Zwei Codex-Fenster dürfen nicht gleichzeitig dieselben Dateien bearbeiten

### 2.4 Safety-First-Prinzip

- `no_auto_trading = true` dauerhaft
- `human_review_required = true` für alle riskanten Runtime-Aktionen
- `broker_orders_enabled = false` dauerhaft
- `live_trading_enabled = false` dauerhaft
- Control Center bleibt read-only
- Trusted Knowledge nur durch Human Review
- Keine automatische Vertrauensvergabe
- Echtgeldkonto darf nicht automatisch genutzt werden
- Demokonto erst in späterer Phase und nur nach expliziter Freigabe

### 2.5 Evidence-based Knowledge Management

- Alle Learnings sind evidenzbasiert
- Quality Scores steuern Promotion
- Trust Scores steuern Nutzung
- Validation erforderlich vor Promotion
- Trusted Status nur nach Human Review
- Keine automatische Vertrauensvergabe

---

## 3. Hermes Cognitive Core

### 3.1 Übersicht

Der Hermes Cognitive Core ist das autonome kognitive System von Jarvis.

**Zyklus:**

```text
Goal Definition
-> Need Detection
-> Autonomous Planning
-> Task Execution
-> Outcome Evaluation
-> Feedback Loop
-> Knowledge Update
-> Goal Progress Tracking
```

**Komponenten:**

- Need Detection
- Goal System
- Planning System
- Execution Engine
- Evaluation System
- Learning & Feedback
- Knowledge Catalog
- Validation Engine
- Promotion Engine
- Human Review Workflow

---

### 3.2 Need Detection

**Zweck:** Erkennt Bedürfnisse aus Goals, Feedback, Wissensbestand und externen Signalen.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Goal-basierte Need Detection
- Feedback-basierte Need Detection
- Knowledge Gap Detection
- Priority Scoring

**Dateien:**

- `agents/core/hermes_router.py`
- `agents/core/hermes_decision.py`
- `agents/core/hermes_planner.py`

---

### 3.3 Goal System

**Zweck:** Definiert, priorisiert, trackt und erklärt Goals.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Goal Definition
- Goal Priorisierung
- Goal Progress Tracking
- Goal Feedback Loop
- Goal Explainability

**Aktuelles Top Goal:** `improve_trading_robustness`

**Goal States:**

- active
- in_progress
- completed
- blocked

**Dateien:**

- `agents/core/hermes_router.py`
- `agents/core/hermes_decision.py`
- `agents/core/hermes_planner.py`

---

### 3.4 Planning System

**Zweck:** Erzeugt autonome Pläne basierend auf Goals und Needs.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Autonomous Planning Cycle
- Task Priorisierung
- Research Queue Integration
- Explain Plan
- Multi-Domain Planning (geplant)

**Dateien:**

- `agents/core/hermes_planner.py`
- `agents/core/hermes_decision.py`

---

### 3.5 Execution Engine

**Zweck:** Führt Tasks aus, delegiert an spezialisierte Agenten.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Task Execution
- Delegation
- Agent Coordination
- Runtime Event Publishing

**Dateien:**

- `agents/core/hermes_execution_engine.py`
- `agents/core/hermes_orchestrator.py`
- `agents/core/delegation_executor.py`
- `agents/core/executor_bridge.py`

---

### 3.6 Evaluation System

**Zweck:** Bewertet Outcomes und erzeugt Feedback.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Outcome Evaluation
- Quality Scoring
- Success/Failure Detection
- Feedback Generation

**Dateien:**

- `agents/core/hermes_learning_feedback.py`
- `agents/core/hermes_decision.py`

---

### 3.7 Learning & Feedback

**Zweck:** Speichert Learnings, erzeugt Feedback Loops, verbessert Routing und Entscheidungen.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Learning Storage
- Feedback Loop
- Routing Hints
- Improvement Tracking

**Storage:**

- `.hermes/learning/`
- `.hermes/routing_hints/`
- `.hermes/improvements/`

**Dateien:**

- `agents/core/hermes_learning_feedback.py`
- `agents/core/hermes_learning_store.py`
- `agents/core/hermes_learning_memory_status.py`

---

## 4. Knowledge System

### 4.1 Knowledge Catalog

**Zweck:** Zentraler Katalog für alle Learnings mit Metadaten, Evidence, Quality Scores und Trust Scores.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Knowledge Registration
- Evidence Tracking
- Quality Score Calculation
- Trust Score Calculation
- Validation Status Tracking
- Knowledge States: weak, promising, robust, trusted

**Dateien:**

- `agents/core/hermes_learning_store.py`
- `agents/core/hermes_learning_memory_status.py`

---

### 4.2 Knowledge States

**Zweck:** Lifecycle-Modell für Learnings.

**States:**

1. **weak** = neu, wenig Evidence, niedrige Quality
2. **promising** = mehr Evidence, höhere Quality, noch nicht robust
3. **robust** = starke Evidence, hohe Quality, wiederholbar validiert
4. **trusted** = robust + Human Review approved

**Regeln:**

- Promotion basiert auf Evidence und Quality Scores
- Trusted benötigt immer Human Review
- Keine automatische Vertrauensvergabe
- Zurückstufung möglich bei negativem Feedback

---

### 4.3 Evidence Tracking

**Zweck:** Sammelt und bewertet Evidence für Learnings.

**Aktueller Status:** Vorhanden und aktiv.

**Evidence Types:**

- Execution Outcomes
- Test Results
- Backtest Results
- Human Feedback
- Cross-Validation
- Production Usage
- Error Tracking

**Metrics:**

- Evidence Count
- Success Rate
- Failure Rate
- Confidence Score
- Sample Size

---

### 4.4 Quality Scores

**Zweck:** Bewertet Quality von Learnings.

**Aktueller Status:** Vorhanden und aktiv.

**Quality Dimensions:**

- Evidence Count
- Success Rate
- Repeatability
- Sample Diversity
- Validation Coverage
- Documentation Quality
- Explainability

**Quality Ranges:**

- 0.0 - 0.3: low
- 0.3 - 0.6: medium
- 0.6 - 0.8: high
- 0.8 - 1.0: very high

---

### 4.5 Trust Scores

**Zweck:** Bewertet Vertrauenswürdigkeit von Learnings für Production Use.

**Aktueller Status:** Vorhanden und aktiv.

**Trust Dimensions:**

- Quality Score
- Human Review Status
- Production Evidence
- Risk Level
- Domain Criticality
- Explainability
- Fallback Availability

**Trust Ranges:**

- 0.0 - 0.4: untrusted
- 0.4 - 0.7: limited trust
- 0.7 - 0.9: trusted (requires Human Review)
- 0.9 - 1.0: fully trusted (requires Human Review + Production Evidence)

---

## 5. Validation & Promotion System

### 5.1 Validation Engine

**Zweck:** Validiert Learnings vor Promotion.

**Aktueller Status:** Vorhanden und aktiv.

**Validation Types:**

- Domain Validation
- Documentation Validation
- Software Validation
- Process Validation
- Research Validation
- Trading Validation

**Validation Rules:**

- Evidence-basiert
- Quality-gesteuert
- Wiederholbar
- Dokumentiert
- Audit-Trail

**Dateien:**

- Domain Validation Router (geplant, aber Grundstruktur vorhanden)

---

### 5.2 Promotion Engine

**Zweck:** Managed Promotion Pipeline für Knowledge States.

**Aktueller Status:** Vorhanden und aktiv.

**Promotion Pipeline:**

```text
weak
-> [Evidence + Quality Check]
-> promising
-> [Enhanced Evidence + Quality Check]
-> robust
-> [Human Review Required]
-> trusted
```

**Promotion Rules:**

- Evidence-basiert
- Quality-gesteuert
- Trusted nur durch Human Review
- Keine automatische Vertrauensvergabe
- Zurückstufung bei negativem Feedback möglich

**Dateien:**

- `agents/core/hermes_learning_store.py`
- `agents/core/hermes_learning_feedback.py`

---

### 5.3 Human Review Workflow

**Zweck:** Human-in-the-Loop für Trusted Promotion und riskante Aktionen.

**Aktueller Status:** Vorhanden und aktiv.

**Review Types:**

- Trusted Promotion Review
- Risk Action Review
- Trading Action Review
- Architecture Change Review
- Production Deployment Review

**Review Process:**

1. Learning/Action erreicht Review-Schwelle
2. Review Request generiert
3. Human Review Interface zeigt Details
4. Human approved/rejected/deferred
5. Outcome gespeichert
6. Feedback Loop aktualisiert

**Dateien:**

- Human Review Workflow (geplant, aber Grundstruktur vorhanden)
- `agents/core/hermes_ui_status.py` (Control Center)

---

## 6. Scheduler & Supervisor

### 6.1 Scheduler

**Zweck:** Plant und startet periodische und triggered Tasks.

**Aktueller Status:** Vorhanden und aktiv.

**Funktionen:**

- Periodische Tasks
- Event-triggered Tasks
- Priority Scheduling
- Resource Management
- Task Queueing

**Config:**

- `config/schedules.json`

**Dateien:**

- `agents/core/hermes_runtime_supervisor.py`
- Scheduler-Komponente (geplant als dediziertes Modul)

---

### 6.2 Supervisor

**Zweck:** Überwacht Runtime, Recovery, Resource Guards, Storage Hygiene.

**Aktueller Status:** Foundation vorhanden, Ausbau in Beta 1 geplant.

**Funktionen:**

- Runtime Monitoring
- Health Checks
- Resource Guards
- Storage Hygiene
- Logging
- Recovery
- Alert Generation

**Dateien:**

- `agents/core/hermes_runtime_supervisor.py`
- `agents/core/hermes_runtime_status.py`

---

## 7. Master Status & Control Center

### 7.1 Master Status

**Zweck:** Zentrale Read-Only Status Aggregation für UI.

**Aktueller Status:** Vorhanden und aktiv.

**Status Panels:**

- Hermes Core Status
- Goal Status
- Knowledge Health
- Validation Status
- Promotion Status
- Scheduler Status
- Supervisor Status
- Trading Status
- Learning Memory Status
- Runtime Status
- Developer Debug Status
- Voice Status
- MCP Tool Status
- Skills Status
- Research Discovery Status
- Cost Optimization Status
- Trading Intelligence Status
- Shared Memory Status
- Skill Generator Status

**Dateien:**

- `agents/core/hermes_ui_status.py`
- `agents/core/hermes_system_snapshot.py`
- `agents/core/hermes_learning_memory_status.py`
- `agents/core/hermes_runtime_status.py`
- `agents/core/hermes_developer_debug_status.py`
- `agents/core/hermes_voice_status.py`
- `agents/core/hermes_mcp_tool_status.py`
- `agents/core/hermes_skills_status.py`
- `agents/core/hermes_research_discovery_status.py`
- `agents/core/hermes_cost_optimization_status.py`
- `agents/core/hermes_trading_intelligence_status.py`
- `agents/core/hermes_shared_memory_status.py`
- `agents/core/hermes_skill_generator_status.py`

---

### 7.2 Control Center

**Zweck:** Read-Only UI für Jarvis Control Center.

**Aktueller Status:** Vorhanden, read-only.

**Regeln:**

- Read-Only
- Keine Runtime-Kommandos
- Keine Broker-Aktionen
- Keine Schreibzugriffe
- Keine automatischen Aktionen

**UI:**

- Jarvis Control Center (Gradio Dev/Test UI)
- Future: Futuristisches lokales AI Control Center (React/Tauri geplant)

**Dateien:**

- `ui_app.py`

---

## 8. Multi-Domain-Ausrichtung

### 8.1 Übersicht

Hermes entwickelt sich von einer Trading-Domäne zu einem domänenübergreifenden kognitiven System.

**Trading ist nur Domäne 1.**

**Ziel-Domänen:**

1. **Trading** (Research, Bot Candidate Pipeline, Setup Watch)
2. **Software** (Coding, Architecture, Testing, Documentation)
3. **Research** (Discovery, Evaluation, Knowledge Expansion)
4. **Documentation** (Masterplan, Roadmaps, Architecture Decisions)
5. **Process** (Workflows, Automation, Optimization)

**Status:**

- Trading: aktiv, aber begrenzt auf Research und Setup Watch
- Software: teilweise aktiv über Codex-Integration
- Research: Foundation vorhanden, Ausbau geplant
- Documentation: aktiv
- Process: Foundation vorhanden

---

### 8.2 Knowledge Gap Engine

**Zweck:** Erkennt Wissenslücken und schlägt Forschungsthemen vor.

**Aktueller Status:** Noch nicht implementiert.

**Priorität:** Priorität 1 für Beta 1.

**Funktionen (geplant):**

- Knowledge Gap Detection
- Research Topic Suggestion
- Source Suggestion
- Validation Gap Identification
- Cross-Domain Gap Detection

---

### 8.3 Cross-Domain Learning

**Zweck:** Nutzt Learnings domänenübergreifend.

**Aktueller Status:** Noch nicht implementiert.

**Priorität:** Priorität 2 für Beta 1.

**Funktionen (geplant):**

- Cross-Domain Pattern Recognition
- Cross-Domain Validation
- Cross-Domain Evidence Transfer
- Domain-Specific Adaptation

---

## 9. Trading-Ziele

### 9.1 Übersicht

Trading hat **zwei getrennte Ziele**:

**A) Research & Bot Candidate Pipeline**

Ziel: Daten sammeln, Setups testen, Strategien validieren, Prediction Feedback auswerten, robuste Scalping-Bot-Kandidaten entwickeln.

**B) Setup Watch / Interface Alerts**

Ziel: Frühzeitige Hinweise auf mögliche Trading-Situationen im Interface.

**Wichtig:** Beide Ziele sind **analysis_only**. Keine Broker Orders, kein Live Trading, kein Auto Trading.

---

### 9.2 Trading Ziel A: Research & Bot Candidate Pipeline

**Ziel:**

Marktdaten sammeln, Setups testen, Strategien validieren, Prediction Feedback auswerten, robuste Scalping-Bot-Kandidaten entwickeln für späteren cTrader Scalping Bot.

**Wichtig:**

- Es besteht bereits eine Datenverbindung zu cTrader
- cTrader liefert Marktdaten/Quotes/Chartdaten
- Über cTrader besteht ein Fusion Markets Konto
- Es gibt Demo- und Echtgeldkonto
- Das Demokonto soll später für Tests genutzt werden
- Echtgeld bleibt gesperrt, bis Safety Gates, Review und explizite Freigabe vorhanden sind

**Aktueller Status:**

- cTrader CSV Import vorhanden
- cTrader OpenAPI Read-Only Connector geplant
- Quote Bridge geplant
- Trading Learning Beta 1 vorhanden (lokal, offline, read-only)
- Backtest Stub vorhanden
- Research Pipeline vorhanden

**Pipeline:**

```text
MarketData (cTrader)
-> FeatureGeneration
-> SignalGeneration/Export
-> OutcomeTracking
-> BacktestStub
-> BetaReport
-> Research Validation
-> Bot Candidate Promotion
```

**Research Candidate Promotion:**

```text
research_candidate
-> [Evidence + Backtest]
-> promising
-> [Enhanced Evidence + Multi-Symbol Validation]
-> robust
-> [Human Review + Demo Validation Plan]
-> demo_bot_candidate
-> [Demo Account Validation]
-> demo_validation_passed
-> [Human Review + Safety Gates]
-> approved_for_small_live_test
```

**Safety:**

- Keine Broker Orders
- Kein Live Trading
- Kein Auto Trading
- `no_auto_trading = true` dauerhaft
- `broker_orders_enabled = false` dauerhaft
- `live_trading_enabled = false` dauerhaft
- Echtgeldkonto gesperrt bis explizite Freigabe
- Demokonto erst nach expliziter Freigabe und Safety Gates

**Dateien:**

- `docs/architecture/hermes_trading_learning_beta1.md`
- `docs/architecture/hermes_ctrader_csv_import_v1.md`
- `docs/architecture/hermes_ctrader_openapi_readonly_connector_v1.md`
- `docs/architecture/ctrader_quote_bridge.md`

---

### 9.3 Trading Ziel B: Setup Watch / Interface Alerts

**Ziel:**

Jarvis/Hermes soll im Interface frühzeitig auf mögliche Trading-Situationen hinweisen.

**Beispiel:**

"In ca. 15 Minuten könnte bei GER40 ein Long-Setup entstehen, wenn der Kurs 5123 erreicht."

**Interface zeigt:**

- Symbol (z.B. GER40, XAUUSD, EURUSD)
- Richtungsidee: long / short / neutral
- geschätztes Zeitfenster (z.B. "in 15 Minuten", "jetzt", "15:30-16:00")
- Trigger-Level (z.B. 5123.00)
- Bedingung (z.B. "Breakout über 5123 + EMA50 Crossover")
- Confidence (0.0 - 1.0)
- Invalidation-Level (z.B. 5110.00)
- relevante Timeframes (z.B. M5, M15, H1)
- Setup-Status: watching, armed, triggered, expired, invalidated

**Setup Status Model:**

- **watching** = Setup wird beobachtet, Bedingungen noch nicht erfüllt
- **armed** = Bedingungen fast erfüllt, Trigger-Level nahe
- **triggered** = Alert ausgelöst, Setup ist aktiv
- **expired** = Zeitfenster abgelaufen ohne Trigger
- **invalidated** = Bedingungen nicht mehr erfüllt, Setup ungültig

**Wichtig:**

- Diese Hinweise sind **keine Orders**
- Sie sind Setup-Warnungen und Analysehinweise
- Die Entscheidung bleibt beim Menschen
- Kein automatisches Trading
- Kein automatisches Order-Placement
- Keine Broker-Integration für Alerts

**Setup Watch Pipeline:**

```text
market_observation
-> [Pattern Recognition + Regime Detection]
-> possible_setup
-> [Trigger Proximity + Confidence Check]
-> armed_setup
-> [Trigger Condition Met]
-> triggered_alert
-> [Human Review + Action Decision]
-> human_review
-> [Outcome Tracking]
-> outcome_feedback
-> Learning Update
```

**Aktueller Status:**

- Noch nicht implementiert
- Geplant für Beta 1 als Zielbild
- Requires: Market Regime Detection, Pattern Recognition, Signal Score System
- Interface: Control Center Panel (geplant)

**Dateien:**

- Setup Watch System (geplant)
- Trading Intelligence Status (vorhanden: `agents/core/hermes_trading_intelligence_status.py`)

---

### 9.4 Trading Roadmap: Zwei getrennte Stränge

**A) Research / Bot Candidate Pipeline:**

```text
research_candidate
-> promising
-> robust
-> demo_bot_candidate
-> demo_validation
-> approved_for_small_live_test
```

**B) Setup Watch / Interface Alert Pipeline:**

```text
market_observation
-> possible_setup
-> armed_setup
-> triggered_alert
-> human_review
-> outcome_feedback
```

**Wichtig:**

- Beide Stränge bleiben getrennt
- Beide Stränge bleiben analysis_only
- Beide Stränge haben keine Broker-Integration für Orders
- Beide Stränge haben keine Live-Trading-Funktionen
- Beide Stränge haben keine Auto-Trading-Funktionen

---

## 10. Safety-Regeln dauerhaft sichtbar

**Immer aktiv:**

- `no_auto_trading = true`
- `human_review_required = true`
- `broker_orders_enabled = false`
- `live_trading_enabled = false`

**Control Center:**

- Read-Only
- Keine Runtime-Kommandos
- Keine Broker-Aktionen
- Keine Schreibzugriffe

**Knowledge Management:**

- Keine automatischen Broker Orders
- Kein Live Trading
- Keine automatische Trusted-Promotion
- Trusted nur durch Human Review

**cTrader / Fusion Markets:**

- Echtgeldkonto gesperrt bis explizite Freigabe
- Demokonto erst nach expliziter Freigabe und Safety Gates
- Datenverbindung read-only
- Quote Feed read-only
- Keine Order-Integration ohne dedizierte Safety Gates

**Dateien:**

- Safety-Regeln sind in allen relevanten Modulen dokumentiert
- Masterplan V7 macht sie dauerhaft sichtbar

---

## 11. Bekannte Schwächen

**Knowledge System:**

- Trusted Knowledge Bestand = 0
- Validation Backlog vorhanden
- Knowledge Health teilweise critical
- Knowledge Gap Engine fehlt

**Multi-Domain:**

- Multi-Domain Learning noch nicht umgesetzt
- Cross-Domain Validation fehlt
- Domänen außer Trading haben wenig Sources

**Trading:**

- Keine robuste Trading Strategie vorhanden
- Setup Watch noch nicht implementiert
- Market Regime Detection fehlt
- Prediction Feedback Loop noch nicht vollständig
- Continuous Backtesting fehlt

**LLM Evaluation:**

- Qwen2.5-Coder schwaches Hermes-Verständnis
- Kimi offen wegen Provider-Überlastung
- Groq Codex-Integration nicht kompatibel
- Nur Sonnet 4.5 voll zuverlässig für komplexe Aufgaben

---

## 12. Nächste Prioritäten

### 12.1 Priorität 1: Knowledge Gap Engine V1

**Ziel:** Erkennt Wissenslücken und schlägt Forschungsthemen vor.

**Funktionen:**

- Knowledge Gap Detection
- Research Topic Suggestion
- Source Suggestion
- Validation Gap Identification

**Status:** Noch nicht implementiert.

**Beta 1:** Zielbild dokumentiert, Implementierung geplant.

---

### 12.2 Priorität 2: Cross-Domain Learning

**Ziel:** Nutzt Learnings domänenübergreifend.

**Funktionen:**

- Cross-Domain Pattern Recognition
- Cross-Domain Validation
- Cross-Domain Evidence Transfer

**Status:** Noch nicht implementiert.

**Beta 1:** Zielbild dokumentiert, Foundation vorbereitet.

---

### 12.3 Priorität 3: Knowledge Health Verbesserung

**Ziel:** Verbessert Knowledge Health durch mehr Evidence und Validation.

**Maßnahmen:**

- Validation Coverage erhöhen
- Evidence Backlog abarbeiten
- Quality Scores verbessern
- Trusted Knowledge aufbauen

**Status:** In Arbeit.

**Beta 1:** Knowledge Health Monitoring sichtbar im Control Center.

---

### 12.4 Priorität 4: Validation Coverage erhöhen

**Ziel:** Mehr Learnings validieren, Validation Backlog reduzieren.

**Maßnahmen:**

- Validation Engine ausbauen
- Domain Validation Router implementieren
- Automated Validation wo möglich
- Human Review Workflow ausbauen

**Status:** In Arbeit.

**Beta 1:** Validation Status sichtbar im Control Center.

---

### 12.5 Priorität 5: Robuste Strategy Validation

**Ziel:** Trading-Strategien robust validieren.

**Maßnahmen:**

- Continuous Backtesting implementieren
- Prediction Feedback Loop schließen
- Multi-Symbol Validation
- Demo Account Validation vorbereiten

**Status:** Foundation vorhanden, Ausbau geplant.

**Beta 1:** Trading Learning Beta 1 lauffähig, Backtest Stub vorhanden.

---

## 13. LLM Evaluation

### 13.1 Sonnet 4.5 (OpenRouter)

**Status:** Referenzmodell

**Bewertung:**

- Architektur: 9.5/10
- Agentenbetrieb: 10/10

**Stärken:**

- Versteht Hermes-Architektur sehr gut
- Gute Codex-Agent-Kompatibilität
- Liefert umsetzbare Architekturarbeit

**Schwächen:**

- Hohe Kosten
- Große Analysen verbrauchen viele Tokens

**Rolle:** Referenzmodell für Architektur und komplexe Codex-Agentenarbeit.

---

### 13.2 GPT-OSS 20B (lokal)

**Status:** Ollama + Codex funktionsfähig

**Bewertung:**

- Architektur: 6.5/10
- Agentenbetrieb: 3/10

**Stärken:**

- Lokal, kostenlos
- Bessere Analysen als Qwen
- Gute Zweitmeinung

**Schwächen:**

- Sehr generische Antworten
- Erkennt Projektkontext nur teilweise

**Rolle:** Sparringspartner, Risikoanalyse, Gegenmeinung.

---

### 13.3 Qwen2.5-Coder 14B (lokal)

**Status:** Ollama + Codex funktionsfähig

**Bewertung:**

- Architektur: 3/10
- Agentenbetrieb: 2/10

**Stärken:**

- Lokal, kostenlos
- Gute allgemeine Coding-Fähigkeiten
- Schnell

**Schwächen:**

- Schwaches Hermes-Verständnis
- Halluziniert Architekturdetails
- Agentenmodus problematisch

**Rolle:** Local Worker für kleine, klar begrenzte Coding-Aufgaben.

---

### 13.4 Kimi K2.6 (OpenRouter)

**Status:** Verbindung erfolgreich, mehrere Tests durch Provider-Überlastung unterbrochen.

**Bewertung:** Noch offen.

**Rolle:** Offen, weitere Tests erforderlich.

---

### 13.5 Groq

**Status:** API-Key funktioniert, Modellliste abrufbar, Codex 0.137 Responses API aktuell nicht kompatibel.

**Bewertung:** Noch offen.

**Rolle:** Offen, Codex-Integration erforderlich.

---

### 13.6 LLM Strategie

**Aktuelle Reihenfolge:**

1. Sonnet 4.5 (Referenzmodell für Architektur und Codex)
2. GPT-OSS 20B lokal (Sparringspartner, Risikoanalyse)
3. Qwen2.5-Coder 14B lokal (schnelle lokale Coding-Aufgaben)
4. Kimi (offen)
5. Groq (offen)

---

## 14. Arbeitsmodus

### 14.1 Vor jeder größeren Aufgabe

1. Ziel definieren
2. Architektur festlegen
3. Einen großen Implementierungsauftrag erstellen
4. Build/Test
5. Commit-Vorschlag

### 14.2 Vermeiden

- Viele kleine Iterationen
- Fünf aufeinanderfolgende Refactors
- Unnötiger Tokenverbrauch
- Parallel-Systeme bauen
- Bestehende Architektur ersetzen statt erweitern

### 14.3 Standard-Validierung

**Nach Python-Änderungen:**

```bash
python3 -m py_compile ui_app.py
python3 -m py_compile agents/core/*.py
python3 -m py_compile service/background_service.py
```

---

## 15. Beta 1 Einordnung

### 15.1 Beta 1 Ziele

**Für Beta 1 gilt:**

- Jarvis/Hermes Interface stabilisieren
- Voice-Funktion lauffähig vorbereiten
- Statuspanels vollständig und defensiv
- Hermes Brain/Router sichtbar
- Control Center read-only
- Trading weiterhin analysis_only
- cTrader-Datenverbindung als vorhanden dokumentieren
- Keine Orderausführung
- Kein Live Trading
- Fusion Markets Demo/Echtgeld nur dokumentieren, nicht aktivieren
- Setup-Hinweise im Interface als Zielbild aufnehmen

### 15.2 Beta 1 Status

**Vorhanden:**

- Hermes Cognitive Core Foundation
- Goal System
- Planning System
- Execution Engine
- Learning & Feedback
- Knowledge Catalog
- Validation & Promotion Foundation
- Scheduler & Supervisor Foundation
- Master Status & Control Center
- Trading Learning Beta 1 (lokal, read-only)
- cTrader CSV Import
- LLM Evaluation

**Geplant für Beta 1:**

- Knowledge Gap Engine V1
- Cross-Domain Learning Foundation
- Setup Watch Zielbild dokumentiert
- Market Regime Detection spezifiziert
- Signal Score System spezifiziert
- Human Review Workflow ausgebaut
- Validation Coverage erhöht
- Knowledge Health verbessert

**Noch nicht für Beta 1:**

- Demo Account Validation
- Live Trading
- Auto Trading
- Order-Integration
- Broker-Integration für Orders
- Setup Watch vollständig implementiert

---

## 16. Roadmap

### 16.1 MUST (Beta 1)

**Cognitive Core:**

- Knowledge Gap Engine V1
- Cross-Domain Learning Foundation
- Knowledge Health Verbesserung
- Validation Coverage erhöhen
- Human Review Workflow ausbauen

**Trading:**

- Trading Setup Watch Zielbild dokumentiert
- Trading Prediction Feedback Loop spezifizieren
- Trading Continuous Backtesting planen
- Trading Safety Gates konkretisieren
- Jarvis Learning UI mit Trading Feedback verbinden

**Runtime:**

- Runtime Event Standardisierung fortführen
- Supervisor/Scheduler als zentrale Dauerbetriebsarchitektur
- ResourceGuard / StorageHygiene / Logging / Recovery für neue Jobs
- `no_auto_trading` / `human_review_required` dauerhaft sichtbar halten

**Multi-Agent:**

- Multi-Agent Workflow Architecture entwerfen
- Agent Dashboard erweitern

**Memory:**

- Memory Architecture mit klarer Store-Trennung finalisieren
- Multi-PC Shared Learning verbindlich entwerfen

**Research:**

- Research / Discovery Agent als read-only Pipeline planen

**Cost:**

- Cost-aware Codex / OpenRouter / Ollama Strategy im UI sichtbar machen

---

### 16.2 SHOULD

**Trading:**

- Market Regime Detection planen
- Strategy Library entwerfen
- Signal Score System definieren
- Risk Agent spezifizieren

**Skills:**

- Hermes Skills System ausbauen
- Skill Registry definieren
- Skill Review Workflow konkretisieren
- Skill Generator als Draft-Generator spezifizieren

**MCP:**

- MCP Gateway planen

**Learning:**

- Reflective Learning Phase als Approval-Queue-Kandidat ausbauen

**Knowledge:**

- Obsidian Knowledge Integration prüfen

**Voice:**

- Fish Audio / Fish Speech Evaluation für spätere Voice Runtime prüfen

**Fine-Tuning:**

- Unsloth / Local Fine-Tuning Evaluation vorbereiten

**Pattern Review:**

- Anthropic Plugin Pattern Review dokumentieren
- Wondel.ai Skills und Mission Control dokumentiert durchführen
- Skill / Connector / Sub-Agent Pattern konkretisieren

---

### 16.3 LATER

**Trading:**

- Paper Trading
- Demo Trading
- Micro Live Trading mit Approval
- Optional Autotrading
- Dedicated Scalping Bot aus Hermes Research Memory

**Coding:**

- Jarvis Coding Assistant Module mit OpenCode / lokalem Coding-Agent

**Fine-Tuning:**

- Local Fine-Tuning

**Voice:**

- Voice Personality Layer

**MCP:**

- Full MCP Connector Marketplace
- Automatische Skill-Generierung aus Apify / MCP

**Runtime:**

- WebSocket Live Runtime
- Cross-device Memory Sync Engine
- Agent-to-Agent Consensus
- Advanced Token Dashboard

**Trading ML:**

- Trading ML Training Pipeline

**Event Store:**

- Event Store / SQLite für Audit und Live-Telemetry prüfen

**Messaging:**

- Messaging Gateway optional: Telegram, Discord, Slack, Email

---

## 17. Wichtigste Änderungen gegenüber V6

### 17.1 Cognitive Core sichtbar gemacht

Masterplan V7 macht den **Hermes Cognitive Core** explizit sichtbar:

- Need Detection
- Goal System
- Autonomous Planning
- Task Execution
- Outcome Evaluation
- Feedback Loops
- Knowledge Quality Management
- Validation & Promotion Engine
- Human Review Workflow
- Scheduler & Supervisor

### 17.2 Knowledge System detailliert

Masterplan V7 detailliert das **Knowledge System**:

- Knowledge Catalog
- Knowledge States: weak, promising, robust, trusted
- Evidence Tracking
- Quality Scores
- Trust Scores
- Promotion Pipeline
- Validation Engine
- Human Review Workflow

### 17.3 Trading-Ziele getrennt

Masterplan V7 trennt **zwei Trading-Ziele**:

**A) Research & Bot Candidate Pipeline** (Daten, Tests, Validation, Scalping Bot Vorbereitung)

**B) Setup Watch / Interface Alerts** (frühzeitige Hinweise auf mögliche Trading-Situationen)

### 17.4 cTrader Integration dokumentiert

Masterplan V7 dokumentiert **cTrader Integration**:

- Datenverbindung vorhanden
- Fusion Markets Konto vorhanden (Demo + Echtgeld)
- Quote Feed geplant
- Read-Only Connector geplant
- Echtgeld gesperrt bis explizite Freigabe
- Demokonto erst nach Safety Gates

### 17.5 Setup Watch Konzept

Masterplan V7 führt **Setup Watch Konzept** ein:

- Setup Status Model: watching, armed, triggered, expired, invalidated
- Interface zeigt: Symbol, Richtung, Zeitfenster, Trigger-Level, Bedingung, Confidence, Invalidation-Level, Timeframes, Status
- Keine Orders, keine Broker-Integration, analysis_only

### 17.6 LLM Evaluation aktualisiert

Masterplan V7 aktualisiert **LLM Evaluation**:

- Sonnet 4.5 = Referenzmodell
- GPT-OSS 20B lokal = Sparringspartner
- Qwen2.5-Coder 14B lokal = Local Worker, aber schwaches Hermes-Verständnis
- Kimi = offen
- Groq = offen

### 17.7 Beta 1 Scope präzisiert

Masterplan V7 präzisiert **Beta 1 Scope**:

- Cognitive Core Foundation vorhanden
- Knowledge Gap Engine V1 geplant
- Cross-Domain Learning Foundation geplant
- Setup Watch Zielbild dokumentiert
- Trading Learning Beta 1 vorhanden
- Kein Demo Trading, kein Live Trading, kein Auto Trading für Beta 1

### 17.8 Safety-Regeln dauerhaft

Masterplan V7 macht **Safety-Regeln dauerhaft sichtbar**:

- `no_auto_trading = true`
- `human_review_required = true`
- `broker_orders_enabled = false`
- `live_trading_enabled = false`
- Control Center read-only
- Trusted nur durch Human Review

---

## 18. Offene Punkte bis Beta 1

### 18.1 Cognitive Core

- [ ] Knowledge Gap Engine V1 implementieren
- [ ] Cross-Domain Learning Foundation implementieren
- [ ] Human Review Workflow UI ausbauen
- [ ] Validation Coverage erhöhen

### 18.2 Trading

- [ ] Setup Watch System spezifizieren
- [ ] Market Regime Detection spezifizieren
- [ ] Signal Score System spezifizieren
- [ ] Prediction Feedback Loop schließen
- [ ] Continuous Backtesting planen

### 18.3 Runtime

- [ ] Supervisor/Scheduler als zentrale Dauerbetriebsarchitektur ausbauen
- [ ] Runtime Event Standardisierung fortführen
- [ ] ResourceGuard / StorageHygiene / Logging / Recovery für neue Jobs

### 18.4 UI

- [ ] Control Center Panels vollständig
- [ ] Master Status stabil
- [ ] Trading Intelligence Panel (Setup Watch Zielbild)
- [ ] Human Review Interface

### 18.5 Knowledge

- [ ] Trusted Knowledge Bestand aufbauen
- [ ] Validation Backlog reduzieren
- [ ] Knowledge Health verbessern

---

## 19. Empfohlener nächster Implementierungsauftrag

### 19.1 Titel

**Knowledge Gap Engine V1 + Cross-Domain Learning Foundation**

### 19.2 Ziel

Implementiere Knowledge Gap Engine V1 und Cross-Domain Learning Foundation als Priorität 1 und 2 für Beta 1.

### 19.3 Scope

**Knowledge Gap Engine V1:**

- Knowledge Gap Detection
- Research Topic Suggestion
- Source Suggestion
- Validation Gap Identification
- Status Panel für Control Center

**Cross-Domain Learning Foundation:**

- Cross-Domain Pattern Recognition Foundation
- Cross-Domain Validation Foundation
- Domain-Specific Adaptation Foundation
- Status Panel für Control Center

### 19.4 Nicht-Scope

- Keine Runtime-Änderungen an bestehenden Systemen ohne explizite Freigabe
- Keine Services starten
- Keine Secrets lesen
- Keine Broker-/Trading-Aktionen
- Keine Commits oder Pushes

### 19.5 Deliverables

1. `agents/core/hermes_knowledge_gap_engine.py`
2. `agents/core/hermes_knowledge_gap_status.py`
3. `agents/core/hermes_cross_domain_learning.py`
4. `agents/core/hermes_cross_domain_learning_status.py`
5. Integration in `hermes_ui_status.py`
6. Tests: `python3 -m py_compile agents/core/hermes_*.py`
7. Dokumentation: kurze Zusammenfassung der Implementierung
8. Commit-Vorschlag

### 19.6 Testplan

- Module kompilieren
- Status Panels abrufbar
- Control Center zeigt neue Panels
- Keine Runtime-Fehler

---

## 20. Übergabe-Prompt für neuen Chat

```text
Arbeite im Projekt ~/jarvis.

Lies zuerst:
- docs/jarvis/chat_bootstrap.md
- docs/jarvis/current_status.md
- docs/jarvis/architecture_decisions.md
- docs/Masterplan/Jarvis_Masterplan_V7_Hermes_Cognitive_Core.md

Wichtige Rollen:
- Jarvis = UI / Runtime / Voice / Control Center
- Hermes = Cognitive Core / Brain / Routing / Planning / Learning / Validation / Promotion
- Codex = Coding Worker, nicht System Brain

Grundregeln:
- Local-first, Cloud nur als bewusster Fallback
- Gradio bleibt Dev/Test UI
- Statusmodule sind read-only-first
- Masterplan/TODO zuerst beachten
- Bestehende Architektur erweitern, nicht ersetzen
- Keine unnötigen Refactors
- Keine Parallel-Systeme
- Keine Runtime-Dateien ohne expliziten Auftrag ändern
- Keine Secrets lesen oder speichern
- Keine Services starten, außer explizit verlangt
- Keine Commits oder Pushes
- no_auto_trading bleibt Pflicht
- human_review_required für riskante Aktionen

Aktuelle V7-Schwerpunkte:
- Hermes Cognitive Core
- Knowledge Gap Engine V1
- Cross-Domain Learning Foundation
- Setup Watch Zielbild
- Trading Learning Beta 1
- Beta 1 Vorbereitung

Wenn du Code änderst:
- klein und reviewbar arbeiten
- bestehende Architektur respektieren
- Tests ausführen, wenn relevant
- geänderte Dateien und Diff zusammenfassen
```

---

## 21. Nicht-Ziele

Dieser Masterplan erzeugt nicht:

- PDF
- React / Tauri / FastAPI-Code
- neue Services
- neue unkontrollierte Scheduler oder Parallel-Background-Loops
- Brokerverbindungen
- Orders
- echte Web-, Reddit-, GitHub- oder arXiv-Abfragen
- API-Keys oder Secrets
- Commits oder Pushes

Masterplan V7 ist Dokumentation und Architekturgrundlage.

---

## 22. Abnahmeliste

Masterplan V7 gilt als akzeptiert, wenn:

- Jarvis und Hermes Rollen eindeutig getrennt sind
- Hermes Cognitive Core explizit sichtbar ist
- Knowledge System detailliert beschrieben ist
- Knowledge States: weak, promising, robust, trusted beschrieben sind
- Promotion Pipeline evidenzbasiert beschrieben ist
- Trusted nur durch Human Review beschrieben ist
- Trading-Ziele getrennt sind: Research/Bot Candidate Pipeline und Setup Watch/Interface Alerts
- cTrader Integration dokumentiert ist
- Setup Watch Konzept beschrieben ist
- Safety-Regeln dauerhaft sichtbar sind
- LLM Evaluation aktualisiert ist
- Beta 1 Scope präzisiert ist
- Offene Punkte bis Beta 1 gelistet sind
- Empfohlener nächster Implementierungsauftrag vorhanden ist
- Keine Implementierung, Runtime-Änderung, Services, Secrets, Commits oder Pushes Teil dieses Masterplans sind

---

**Ende Masterplan V7**
