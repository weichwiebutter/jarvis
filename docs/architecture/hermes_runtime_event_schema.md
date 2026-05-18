# Hermes Runtime Event Schema

Status: Schema-Definition / Future Foundation  
Scope: Runtime Events, Runtime Event Bus, Activity Timeline, UI, spaetere WebSocket-Streams  
Stand: 18. Mai 2026

## Zweck

Dieses Dokument definiert ein standardisiertes Hermes Runtime Event Schema fuer
zukuenftige Runtime-, UI-, Timeline- und WebSocket-Integrationen.

Die aktuelle Implementierung bleibt unveraendert. Es werden keine
Event-Publisher umgebaut, keine Runtime-Loops gestartet, keine Services
gestartet und keine Events persistiert.

## Gepruefte Module

- `agents/core/hermes_runtime_events.py`
- `agents/core/hermes_runtime_event_bus.py`
- `agents/core/hermes_activity_timeline.py`
- `agents/core/hermes_ui_status.py`

Aktuell existieren zwei verwandte Strukturen:

- Runtime Events: `event_id`, `timestamp`, `source`, `category`, `severity`,
  `message`, `metadata`, `requires_attention`
- Activity Timeline Entries: `entry_id`, `timestamp`, `title`,
  `description`, `category`, `source`, `status`, `importance`, `metadata`

Das Runtime Event Schema V1 soll langfristig die gemeinsame Grundlage fuer
Event Bus, UI Panels, Activity Timeline, WebSocket/Event Stream und spaetere
Audit-/Persistence-Schichten werden.

## Standard Event Structure

Ein Runtime Event ist ein JSON-kompatibles Dict:

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_...",
  "timestamp": "2026-05-18T12:00:00+00:00",
  "source": "hermes_runtime_supervisor",
  "category": "runtime",
  "severity": "info",
  "event_type": "runtime_start",
  "message": "Runtime supervisor status generated.",
  "metadata": {},
  "requires_attention": false
}
```

### Required Keys

- `event_id`: Eindeutige Event-ID, bevorzugt Prefix `evt_`.
- `timestamp`: UTC ISO-8601 Timestamp mit Zeitzonen-Offset.
- `source`: Stabiler technischer Ursprung des Events.
- `category`: Grobe fachliche Kategorie.
- `severity`: Sichtbarkeits- und Prioritaetsstufe.
- `message`: Kurzer menschenlesbarer Satz.
- `metadata`: JSON-kompatibles Dict fuer strukturierte Zusatzdaten.
- `requires_attention`: Boolean fuer UI-/Review-Priorisierung.

### Optional Keys

- `schema_version`: Empfohlen ab V1, Default `hermes.runtime_event.v1`.
- `event_type`: Spezifischer Event-Typ innerhalb der Kategorie.
- `task_id`: Spaeter fuer Taskline, Scheduler und Agent-Jobs.
- `correlation_id`: Spaeter fuer zusammenhaengende Events ueber Module hinweg.
- `parent_event_id`: Spaeter fuer Event-Ketten und Debug-Fluesse.
- `agent_id`: Falls das Event einem Hermes-Agenten zugeordnet ist.
- `session_id`: Spaeter fuer UI-/Chat-/Voice-Sessions.
- `user_visible`: Expliziter UI-Hinweis, wenn ein Event sichtbar gerendert werden soll.
- `audit_required`: Spaeter fuer sicherheitsrelevante Audit Logs.
- `redaction_applied`: True, falls sensible Inhalte vor Ausgabe entfernt wurden.

## Severity Levels

V1 bleibt kompatibel mit `hermes_runtime_events.py`:

- `info`: Normaler Status oder neutraler Hinweis.
- `success`: Erfolgreicher Abschluss oder positive Runtime-Pruefung.
- `warning`: Problem, Risiko oder Review-Hinweis; UI soll es sichtbar machen.
- `critical`: Kritischer Zustand; UI soll es prominent anzeigen.

Regel: `warning` und `critical` setzen standardmaessig
`requires_attention: true`, sofern ein Publisher nichts anderes begruendet.

## Event Categories

Aktuell implementierte Kategorien:

- `routing`
- `agent`
- `runtime`
- `learning`
- `voice`
- `trading`
- `warning`
- `system`

Zielkategorien fuer V1-Erweiterung:

- `task`
- `memory`
- `research`
- `skill`
- `approval`
- `tool`

Hinweis: Die Zielkategorien sind fuer zukuenftige Integration dokumentiert.
Bestehende Publisher werden durch dieses Dokument nicht erweitert.

## Timestamp Format

- Format: UTC ISO-8601 mit Zeitzonen-Offset.
- Beispiel: `2026-05-18T12:00:00+00:00`
- Keine lokalen Zeitstrings ohne Offset.
- Keine nativen `datetime`-Objekte in JSON-Ausgaben.

## Source Identifiers

`source` ist ein stabiler technischer Identifier, kein langer UI-Text.

Beispiele:

- `hermes_router`
- `hermes_runtime_supervisor`
- `hermes_runtime_event_bus`
- `hermes_activity_timeline`
- `hermes_trading_analyst`
- `hermes_research_discovery`
- `hermes_skills_status`
- `jarvis_voice`
- `jarvis_ui`
- `ollama`

Konvention:

- lowercase
- snake_case
- keine Pfade
- keine Secrets
- keine nutzerspezifischen lokalen Rechnernamen

## Correlation And Task IDs

Fuer spaetere Runtime- und WebSocket-Fluesse sind optionale IDs vorgesehen:

- `task_id`: Gruppiert Events zu einem Task oder Agent-Job.
- `correlation_id`: Gruppiert Events ueber Module hinweg, z. B. Chat-Request,
  Routing, Tool-Auswahl und UI-Antwort.
- `parent_event_id`: Erlaubt einfache Event-Hierarchien.

Diese Felder sind optional, bis Scheduler, Agent Lifecycle und Event Stream
standardisiert sind.

## UI Visibility Rules

UI-Komponenten sollen Events nach Schwere und Aufmerksamkeit priorisieren:

- `critical`: immer sichtbar, prominent, Review erforderlich.
- `warning`: sichtbar in Warnings/Signals und Activity Feed.
- `success`: sichtbar in Timeline/Debug, nicht als Alarm.
- `info`: sichtbar in Activity Feed oder Debug, aggregierbar.

`requires_attention: true` hebt ein Event in Approval Queue, Runtime Warnings
oder Control-Center-Signalen hervor.

Trading-Events muessen `no_auto_trading` und `human_review_required` in
`metadata` oder Safety-Kontext sichtbar halten, sobald sie Signale oder
Prediction-Ergebnisse betreffen.

## Privacy And Safety Rules

Runtime Events duerfen keine Secrets enthalten:

- keine API Keys
- keine Tokens
- keine `.env.local`-Inhalte
- keine Broker-Credentials
- keine privaten Chat-Inhalte ohne ausdruecklichen Debug-/Audit-Zweck
- keine ungekuerzten externen Payloads

Sensible Werte muessen vor UI, WebSocket oder Persistenz redacted werden.
Wenn Redaction angewendet wurde, soll `redaction_applied: true` gesetzt werden.

Events duerfen keine versteckten Actions ausloesen. Sie sind Status- und
Signalobjekte, keine Command-Objekte.

## Future WebSocket Compatibility

Runtime Events sollen spaeter direkt ueber WebSocket/Event Stream sendbar sein:

- JSON-kompatibel
- keine Python-spezifischen Objekte
- stabile Keys
- kleine Payloads
- `metadata` begrenzen und bei Bedarf truncaten
- optionale `schema_version` fuer Client-Kompatibilitaet
- optionale `correlation_id` fuer Client-seitige Gruppierung

WebSocket-Streams sollen nur bereits freigegebene oder redacted Events senden.

## Future Persistence Compatibility

Spaetere Persistenz kann auf demselben Dict-Schema basieren:

- append-only Event Store moeglich
- SQLite/Event Store spaeter moeglich
- Audit Log spaeter moeglich
- `event_id` als Primaerschluessel-Kandidat
- `timestamp`, `category`, `severity`, `source`, `task_id` als Index-Kandidaten

Persistence ist nicht Teil der aktuellen Foundation. Vor Persistenz braucht es:

- Retention Policy
- Redaction Policy
- Disk Limits
- Audit-/Privacy Review
- klare Trennung zwischen Runtime Events und Long-Term Memory

## Beispiel-Events

### runtime_start

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_runtime_start_001",
  "timestamp": "2026-05-18T12:00:00+00:00",
  "source": "hermes_runtime_supervisor",
  "category": "runtime",
  "severity": "info",
  "event_type": "runtime_start",
  "message": "Runtime supervisor foundation status generated.",
  "metadata": {
    "read_only": true,
    "services_started": false
  },
  "requires_attention": false
}
```

### warning

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_warning_001",
  "timestamp": "2026-05-18T12:01:00+00:00",
  "source": "hermes_ui_status",
  "category": "warning",
  "severity": "warning",
  "event_type": "warning",
  "message": "Ollama status check unavailable in current sandbox.",
  "metadata": {
    "external_service_started": false,
    "runtime_files_written": false
  },
  "requires_attention": true
}
```

### task_started

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_task_started_001",
  "timestamp": "2026-05-18T12:02:00+00:00",
  "source": "hermes_runtime_supervisor",
  "category": "task",
  "severity": "info",
  "event_type": "task_started",
  "message": "Research discovery scan task entered planned state.",
  "task_id": "task_research_weekly_digest",
  "metadata": {
    "read_only": true,
    "external_queries_performed": false
  },
  "requires_attention": false
}
```

### task_completed

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_task_completed_001",
  "timestamp": "2026-05-18T12:03:00+00:00",
  "source": "hermes_runtime_supervisor",
  "category": "task",
  "severity": "success",
  "event_type": "task_completed",
  "message": "Read-only status aggregation completed.",
  "task_id": "task_status_aggregation",
  "metadata": {
    "files_written": false,
    "services_started": false
  },
  "requires_attention": false
}
```

### routing_decision

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_routing_decision_001",
  "timestamp": "2026-05-18T12:04:00+00:00",
  "source": "hermes_router",
  "category": "routing",
  "severity": "success",
  "event_type": "routing_decision",
  "message": "Task routed to trading domain.",
  "correlation_id": "corr_chat_001",
  "metadata": {
    "domain": "trading",
    "agent_id": "trading_agent",
    "human_review_required": true
  },
  "requires_attention": false
}
```

### trading_signal

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_trading_signal_001",
  "timestamp": "2026-05-18T12:05:00+00:00",
  "source": "hermes_trading_analyst",
  "category": "trading",
  "severity": "warning",
  "event_type": "trading_signal",
  "message": "Trading signal candidate requires human review.",
  "metadata": {
    "symbol": "XAUUSD",
    "timeframe": "M15",
    "signal_type": "candidate",
    "no_auto_trading": true,
    "trade_execution_enabled": false,
    "human_review_required": true
  },
  "requires_attention": true
}
```

### research_discovery

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_research_discovery_001",
  "timestamp": "2026-05-18T12:06:00+00:00",
  "source": "hermes_research_discovery",
  "category": "research",
  "severity": "info",
  "event_type": "research_discovery",
  "message": "New research idea candidate prepared for review.",
  "metadata": {
    "read_only_research": true,
    "source_citation_required": true,
    "human_review_required": true
  },
  "requires_attention": false
}
```

### skill_suggestion

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_skill_suggestion_001",
  "timestamp": "2026-05-18T12:07:00+00:00",
  "source": "hermes_reflective_learning",
  "category": "skill",
  "severity": "info",
  "event_type": "skill_suggestion",
  "message": "Skill candidate generated for review.",
  "metadata": {
    "generated_skills_not_auto_active": true,
    "no_unreviewed_execution": true,
    "human_review_required": true
  },
  "requires_attention": false
}
```

### approval_required

```json
{
  "schema_version": "hermes.runtime_event.v1",
  "event_id": "evt_approval_required_001",
  "timestamp": "2026-05-18T12:08:00+00:00",
  "source": "hermes_control_center",
  "category": "approval",
  "severity": "warning",
  "event_type": "approval_required",
  "message": "Human approval is required before activating this change.",
  "metadata": {
    "review_owner": "Frank",
    "auto_apply_enabled": false,
    "runtime_files_written": false
  },
  "requires_attention": true
}
```

## Nicht-Ziele

- Keine Runtime-Validierung in dieser Phase.
- Keine Anpassung bestehender Event-Publisher.
- Keine Persistenz.
- Keine WebSocket-Implementierung.
- Keine Event-Command-Ausfuehrung.

