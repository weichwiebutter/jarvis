# Hermes Runtime Event Architecture

Status: Architektur-Notiz / Future Design  
Scope: Runtime Events, Activity Timeline, UI Panels, Runtime Registry  
Stand: 18. Mai 2026

## Zweck

Diese Notiz dokumentiert die aktuelle Runtime-Event- und Activity-Timeline-
Foundation und beschreibt ein Zielbild fuer eine spaetere einheitliche Hermes /
Jarvis Runtime Event Architecture.

Keine Implementierung ist Teil dieser Notiz.

## Aktueller Stand

### `hermes_runtime_events.py`

`agents/core/hermes_runtime_events.py` definiert eine kompakte read-only
Runtime-Event-Struktur fuer spaetere Live-UI-Panels.

Aktuelle Struktur:

- `event_id`
- `timestamp`
- `source`
- `category`
- `severity`
- `message`
- `metadata`
- `requires_attention`

Aktuelle Kategorien:

- `routing`
- `agent`
- `runtime`
- `learning`
- `voice`
- `trading`
- `warning`
- `system`

Aktuelle Severity-Werte:

- `info`
- `success`
- `warning`
- `critical`

Das Modul erzeugt aktuell Demo-Events ueber `example_runtime_events()` und
serialisiert sie ueber `serialize_runtime_event()`. Es startet keine Loops,
Threads, Services oder WebSockets und schreibt keine Runtime-Dateien.

### `hermes_activity_timeline.py`

`agents/core/hermes_activity_timeline.py` definiert eine separate read-only
Timeline-Struktur fuer spaetere Taskline- und Activity-Panels.

Aktuelle Struktur:

- `entry_id`
- `timestamp`
- `title`
- `description`
- `category`
- `source`
- `status`
- `importance`
- `metadata`

Aktuelle Kategorien:

- `routing`
- `agent`
- `learning`
- `trading`
- `runtime`
- `voice`
- `system`

Aktuelle Status-Werte:

- `planned`
- `active`
- `completed`
- `warning`

Aktuelle Importance-Werte:

- `low`
- `normal`
- `high`

Das Modul erzeugt aktuell Demo-Timeline-Eintraege ueber
`build_demo_activity_timeline()` und serialisiert sie ueber
`serialize_timeline_entry()`. Auch hier gibt es keine Loops, Services,
WebSockets oder Runtime-Schreibzugriffe.

### UI-Status-Integration

`agents/core/hermes_ui_status.py` bindet beide Module defensiv ein:

- `_build_runtime_events_status()` ruft `example_runtime_events()` auf und
  erzeugt Top-Level `runtime_events`.
- `_build_activity_timeline_status()` ruft `build_demo_activity_timeline()` auf
  und erzeugt Top-Level `activity_timeline`.
- `_build_activity_feed_panel()` rendert `runtime_events` als
  `ui_panels.activity_feed_panel`.
- `_build_taskline_panel()` rendert `activity_timeline` als
  `ui_panels.taskline_panel`.

Beide Statusobjekte verwenden aktuell:

- `generated_at`
- `status: planned/live_foundation`
- `read_only: true`
- `warnings`

### Foundation Registry / Tool Registry

`hermes_foundation_registry.py` registriert aktuell die Foundation-Statusmodule
wie Runtime Supervisor, Shared Memory, Skills, Research Discovery, MCP Tools,
Reflective Learning und Trading Intelligence.

Runtime Events und Activity Timeline sind dort noch keine eigenen Registry-
Eintraege.

Im MCP / Tool-Status existiert bereits die geplante Tool-Kategorie
`runtime_status`. Das ist ein natuerlicher spaeterer Anknuepfungspunkt fuer
Runtime Event Registry, Runtime Status Tools und UI Event Panels.

## Analyse

### Doppelte Daten und Ueberschneidungen

Runtime Events und Timeline-Eintraege beschreiben teilweise dieselben
Vorgaenge:

- Routing aktiv / adaptive routing
- Trading request / XAUUSD Analyse
- Ollama runtime state
- Voice runtime planned
- Learning memory loaded
- Agent dashboard initialized

Dabei unterscheiden sich die Felder:

- Runtime Event: `message`, `severity`, `requires_attention`
- Timeline Entry: `title`, `description`, `status`, `importance`

Beide nutzen:

- `timestamp`
- `source`
- `category`
- `metadata`

Aktuell gibt es keine verbindliche Abbildung von Event zu Timeline Entry.
Dadurch koennen spaeter doppelte, leicht abweichende UI-Daten entstehen.

### Event- / Timeline-Fluss heute

Aktueller Flow:

```text
hermes_runtime_events.example_runtime_events()
  -> hermes_ui_status._build_runtime_events_status()
  -> runtime_events
  -> ui_panels.activity_feed_panel

hermes_activity_timeline.build_demo_activity_timeline()
  -> hermes_ui_status._build_activity_timeline_status()
  -> activity_timeline
  -> ui_panels.taskline_panel
```

Dieser Flow ist read-only und demo-orientiert. Es gibt noch keinen zentralen
Event Bus, keinen Event Store, keine Runtime Registry und keine echte Live-
Aggregation.

### Fehlende Standardisierung

Offene Standardisierungsfragen:

- Ist `severity` oder `importance` die fuehrende Prioritaetsachse?
- Wird `status` aus Runtime Events abgeleitet oder separat gepflegt?
- Welche Kategorien sind fuer alle Eventquellen verbindlich?
- Welche Felder muessen fuer Audit und spaetere Persistenz stabil sein?
- Wie werden Duplikate zwischen Event Feed und Timeline vermieden?
- Welche Events duerfen personenbezogene Inhalte oder Taskdaten enthalten?
- Welche Events sind nur transient und welche auditierbar?

## Zielbild

Hermes / Jarvis sollte spaeter eine einheitliche Runtime Event Architecture
haben:

```text
Event Sources
  -> Runtime Event Normalizer
  -> Runtime Event Registry / Contracts
  -> In-memory Event Buffer
  -> Timeline Aggregator
  -> UI Panels / API / optional Stream
  -> optional Audit Log / Event Persistence
```

Grundprinzip:

Ein kanonisches Runtime Event ist die Quelle. Timeline-Eintraege, Activity Feed
Items, Audit-Records und Live-Streams werden daraus abgeleitet.

## Event Sources

Geplante Eventquellen:

- Hermes Router
- Hermes Brain
- Agent Dashboard
- Runtime Supervisor
- Scheduler / Agent Jobs
- Shared Memory / Multi-PC
- Learning / Memory
- Reflective Learning
- Skills System
- Skill Generator
- MCP / Tool Layer
- Research Discovery
- Cost Optimization
- Voice Runtime
- Trading Intelligence
- cTrader QUOTE Bridge
- Trading Setup Watch Agent
- Trading Signal Scoring Agent
- Trading Backtesting Agent
- Prediction Review Agent
- Multi-Agent Workflow Orchestrator
- Jarvis UI / Approval Queue
- Provider / Model Routing
- System Health

Jede Quelle sollte spaeter einen festen `source`-Namen, Owner, Safety Level und
Event-Scope bekommen.

## Event Types

Eine spaetere Event-Typ-Liste koennte diese Klassen enthalten:

- `routing_decision`
- `agent_selected`
- `agent_status_changed`
- `task_created`
- `task_started`
- `task_completed`
- `task_failed`
- `approval_requested`
- `approval_resolved`
- `runtime_heartbeat`
- `scheduler_job_planned`
- `scheduler_job_started`
- `scheduler_job_finished`
- `runtime_warning`
- `tool_registered`
- `tool_invocation_requested`
- `skill_proposed`
- `skill_reviewed`
- `memory_candidate_created`
- `memory_persisted_after_approval`
- `research_report_created`
- `provider_selected`
- `cost_warning`
- `voice_state_changed`
- `trading_analysis_requested`
- `trading_setup_watch_created`
- `trading_setup_watch_status_changed`
- `trading_signal_armed`
- `trading_signal_triggered`
- `trading_signal_expired`
- `quote_check_planned`
- `prediction_feedback_recorded`
- `prediction_outcome_recorded`
- `backtest_started`
- `backtest_completed`
- `strategy_disabled_after_review`
- `risk_reduced_after_review`
- `agent_chain_started`
- `agent_chain_step_completed`
- `agent_chain_blocked_for_approval`

Event Types sollten stabil und maschinenlesbar sein. Display-Texte sollten
daraus abgeleitet werden, nicht umgekehrt.

## Severity Levels

Empfohlene Severity-Skala:

- `debug`: nur Entwicklerdiagnostik
- `info`: normale Statusinformation
- `success`: erfolgreich abgeschlossene Aktion
- `warning`: menschliche Aufmerksamkeit sinnvoll
- `critical`: unmittelbare Aufmerksamkeit erforderlich
- `blocked`: Aktion wurde absichtlich durch Safety / Approval blockiert

Mapping zur heutigen Timeline:

- `debug` -> `importance: low`
- `info` -> `importance: normal`
- `success` -> `importance: normal`
- `warning` -> `importance: high`
- `critical` -> `importance: high`
- `blocked` -> `importance: high`

## Runtime Lifecycle

Runtime Events sollten spaeter den Lebenszyklus von Tasks, Jobs und Agents
abbilden:

1. `planned`
2. `queued`
3. `awaiting_approval`
4. `approved`
5. `running`
6. `succeeded`
7. `failed`
8. `blocked`
9. `cancelled`
10. `archived`

Nicht jede Eventquelle muss jeden Zustand verwenden. Wichtig ist, dass Status-
Uebergaenge nachvollziehbar und auditierbar bleiben.

Trading Setup Watch nutzt zusaetzlich ein fachliches Statusmodell:

1. `watching`
2. `armed`
3. `triggered`
4. `expired`

Diese Statuswerte sind keine Order-Kommandos. Sie dienen nur UI, Review,
Prediction Feedback und spaeterer Analyse.

## Timeline Aggregation

Die Timeline sollte nicht als eigene Primärquelle entstehen, sondern aus
kanonischen Runtime Events aggregiert werden.

Aggregation kann spaeter:

- Events nach Zeit sortieren
- Duplikate zusammenfassen
- technische Details in UI-Texte uebersetzen
- Severity in Importance mappen
- Task- oder Session-Gruppen bilden
- relevante Events fuer Home Dashboard, Activity Feed und Taskline filtern
- Approval-Events prominent markieren

Empfehlung:

- Runtime Event = kanonischer Datensatz
- Timeline Entry = UI-Projektion
- Audit Record = persistente, unveraenderliche Projektion fuer spaeter

## WebSocket- / Stream-Nutzung spaeter

Eine spaetere Live-UI kann Runtime Events ueber WebSocket oder Event Stream
anzeigen.

Wichtig:

- Stream ist optional und spaeter.
- Kein Event Stream startet ohne explizite Runtime-Entscheidung.
- Backpressure / Rate Limits sind erforderlich.
- UI darf nicht annehmen, dass ein Stream immer verfuegbar ist.
- Polling-Fallback bleibt sinnvoll.
- Secrets, Prompt-Inhalte und rohe Tool-Ausgaben duerfen nicht ungefiltert in
  Streams gelangen.

## Runtime Registry Integration

Eine spaetere Runtime Event Registry sollte definieren:

- `event_type`
- `source`
- `owner`
- `schema_version`
- `category`
- `severity_policy`
- `retention_policy`
- `privacy_level`
- `audit_required`
- `ui_visible`
- `stream_allowed`
- `persistence_allowed`

Diese Registry kann mit bestehenden Konzepten verbunden werden:

- Foundation Registry fuer Modul- und Panel-Metadaten
- MCP / Tool Registry fuer Tool-Events
- Runtime Supervisor fuer Job- und Lifecycle-Events
- Cost Optimization fuer Provider- und Cost-Events
- Trading Intelligence fuer Quote- und Prediction-Events

## UI- und Event-Panel-Nutzung

Geplante Panels:

- `activity_feed_panel`: chronologischer Event Feed
- `taskline_panel`: verdichtete Task- und Activity Timeline
- `runtime_control_panel`: Runtime Health, Heartbeat, Scheduler, Warnings
- `foundation_registry_panel`: verfuegbare Foundation-Module und Safety Level
- spaeter: Approval Queue Panel
- spaeter: Audit Trail Panel

UI-Regeln:

- Warnings und Critical Events muessen sichtbar sein.
- Approval Requests muessen prominent angezeigt werden.
- Trading Events muessen `no_auto_trading` sichtbar halten.
- Setup-Watch- und Signal-Events muessen Trigger-Bedingungen, Invalidation,
  Confidence und `human_review_required` sichtbar machen, sobald daraus ein
  Review- oder Alert-Zustand entsteht.
- Provider- und Cost-Events muessen Cloud-Nutzung erkennbar machen.
- Technische Raw-Events duerfen in Developer Debug sichtbar bleiben.

## Audit Logs spaeter

Audit Logs sind eine moegliche spaetere Projektion aus Runtime Events.

Auditpflichtige Kandidaten:

- Approval Requests
- Approval Decisions
- Tool-Ausfuehrungsanfragen
- Schreibende Aktionen
- Skill-Aktivierungen
- Memory-Persistenz
- Provider-Wechsel
- Kostenrelevante Aktionen
- Trading-bezogene Entscheidungen
- Setup-Watch-Statuswechsel
- Prediction Outcomes und daraus abgeleitete Learning-Kandidaten
- Risk-Agent-Empfehlungen wie Risiko reduzieren oder Strategie deaktivieren
- Safety Blocks

Audit Logs muessen unveraenderlich, nachvollziehbar und sparsam sein. Sie
duerfen keine Secrets enthalten.

## Event Persistence spaeter

Persistenz ist optional und spaeter.

Moegliche Modelle:

- in-memory ring buffer fuer Live UI
- lokale JSONL-Datei fuer Dev/Test
- SQLite Event Store fuer Control Center
- getrennte Audit-Tabelle fuer freigabepflichtige Aktionen

Offene Entscheidungen:

- Retention pro Eventtyp
- Pruning / Disk Limits
- Privacy-Level pro Event
- Export nach Obsidian nur fuer kuratierte Zusammenfassungen
- keine rohe Runtime-Dauerablage ohne Review

## Safety / Privacy Grenzen

Runtime Events duerfen nicht zur unkontrollierten Runtime-Aufzeichnung werden.

Grenzen:

- keine Secrets
- keine API Keys
- keine `.env.local` Inhalte
- keine rohen Prompt- oder Chatverlaeufe ohne explizite Freigabe
- keine ungefilterten Tool-Ausgaben
- keine personenbezogenen Daten ohne Zweck und Review
- keine Broker- oder Trade-Ausfuehrungsdaten ohne explizite Freigabe
- keine automatische Persistenz riskanter Events

Trading-spezifisch:

- `no_auto_trading` bleibt sichtbar.
- QUOTE-Events sind read-only.
- TRADE-Events bleiben deaktiviert bis explizite Freigabe.
- Prediction Feedback darf analysieren, aber keine Orders ausloesen.

## Empfohlene naechste Schritte

1. Bestehende Demo-Strukturen nicht sofort ersetzen.
2. Einen kanonischen Event-Contract als Dokument oder TypedDict planen.
3. Kategorien, Severity und Lifecycle-Status vereinheitlichen.
4. Timeline als Projektion aus Runtime Events definieren.
5. Runtime Event Registry als read-only Foundation entwerfen.
6. `activity_feed_panel` und `taskline_panel` kompatibel halten.
7. Erst danach ueber WebSocket, Event Store oder Audit Persistence entscheiden.

Jeder spaetere Umsetzungsschritt sollte den bestehenden UI-Status-Schema-Test,
die `hermes_ui_status.py` CLI und den Trading-Task-Aufruf weiter bestehen
lassen.
