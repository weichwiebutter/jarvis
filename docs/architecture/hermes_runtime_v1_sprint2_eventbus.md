# Hermes Runtime v1 - Sprint 2 EventBus & JSONL EventStore

Status: Implemented foundation draft
Scope: Local EventBus, append-only JSONL EventStore, runtime lifecycle events
Current implementation: `HermesRuntime/Runtime/`

## Purpose

Sprint 2 erweitert die Minimal Runtime um einen kleinen lokalen EventBus und
einen append-only JSONL EventStore. Ziel ist eine stabile Runtime-Grundlage fuer
Lifecycle-Events, ohne Worker, Queue-Systeme, WebSockets, APIs, Trading-Logik
oder KI-Logik einzufuehren.

## Non-Goals

- Keine Trading-Logik.
- Keine Queue-Implementierung.
- Keine echten Worker.
- Keine WebSockets.
- Keine APIs.
- Keine Broker-Anbindung.
- Kein Auto-Trading.
- Kein Replay-System.
- Keine UI-Anbindung.

## Implemented Components

- `EventEnvelope`
- `EventType`
- `EventSeverity`
- `EventBus`
- `EventStore`
- `JsonlLogger`

## EventEnvelope

Jedes Event wird als `EventEnvelope` transportiert.

Felder:

- `EventId`
- `TimestampUtc`
- `EventType`
- `Source`
- `Severity`
- `CorrelationId`
- `RuntimeVersion`
- `Payload`

Regeln:

- `EventId` wird automatisch erzeugt.
- `TimestampUtc` nutzt UTC.
- `EventType` und `Severity` werden als Strings serialisiert.
- `Payload` bleibt JSON-kompatibel.

## Event Types

Sprint 2 schreibt diese Events:

- `RuntimeStarted`
- `StorageInitialized`
- `RuntimeSafeModeEnabled`
- `RuntimeStopped`

`RuntimeSafeModeEnabled` wird nur geschrieben, wenn SafeMode aktiv ist.

## EventBus

Der EventBus ist bewusst klein:

- In-Memory Subscriber-Liste.
- Keine Queue.
- Keine Worker.
- Kein Background Thread.
- `Publish` faengt Handler-Exceptions ab, damit ein fehlerhafter Handler die
  Runtime nicht stoppt.

Der EventBus ist damit fuer Sprint 2 ausreichend, aber noch keine
Infrastruktur fuer asynchrone Verarbeitung.

## EventStore

Der EventStore subscribed auf den EventBus und schreibt Events append-only in
JSONL.

Pfad:

```text
HermesRuntime/data/events/runtime/yyyy-MM-dd.runtime.jsonl
```

Eigenschaften:

- append-only
- eine JSON-Zeile pro Event
- UTF-8 ohne BOM
- Flush beim Shutdown
- lokales file-based storage

## RuntimeHost Integration

`RuntimeHost` publiziert:

1. `RuntimeStarted`
2. `StorageInitialized`
3. `RuntimeSafeModeEnabled`, falls SafeMode aktiv ist
4. `RuntimeStopped`

Danach wird der EventStore geflusht und disposed.

## Test

Aus `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
```

Danach pruefen:

```bash
ls data/events/runtime/
tail -n 5 data/events/runtime/*.runtime.jsonl
```

## Acceptance Criteria

- `dotnet run` funktioniert.
- JSONL-Datei wird erzeugt.
- mehrere Events werden gespeichert.
- Datei ist lesbar.
- Runtime beendet sauber.
- keine Exceptions beim normalen Ablauf.

## Safety

Sprint 2 fuehrt keine Trading-, Broker-, Worker-, Queue-, API- oder
WebSocket-Funktionalitaet ein. Events sind Statusobjekte, keine Commands.
