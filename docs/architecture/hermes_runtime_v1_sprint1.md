# Hermes Minimal Viable Runtime v1 - Sprint 1 Foundation

Status: Implemented foundation draft
Scope: Local file-based Hermes runtime skeleton
Current implementation: `HermesRuntime/`

## Purpose

Sprint 1 legt eine kleine stabile Grundlage fuer eine lokale Hermes Runtime.
Die Runtime ist bewusst minimal: Sie laedt Konfiguration, initialisiert lokale
Storage-Ordner, prueft freien Speicher, schreibt Start-/Stop-Events und einen
Runtime Snapshot.

## Non-Goals

- Keine Trading-Logik.
- Keine KI-Logik.
- Keine Broker-Anbindung.
- Kein Auto-Trading.
- Kein Queue-System.
- Keine echten Worker.
- Keine Replay Engine.
- Kein Backtesting.
- Kein Learning.
- Kein WebSocket.
- Keine UI-Anbindung.

## Project Location

```text
HermesRuntime/
  Hermes.Runtime.csproj
  Program.cs
  Runtime/
  config/
```

## Runtime Flow

1. `RuntimeConfig` laden.
2. `StorageProfile` laden.
3. Storage-Pfade berechnen.
4. Ordnerstruktur erzeugen.
5. freien Speicher pruefen.
6. `RuntimeStarted` Event als JSONL schreiben.
7. `RuntimeSnapshot` als JSON schreiben.
8. `RuntimeStopped` Event als JSONL schreiben.
9. sauber beenden.

## Storage Structure

Die Runtime erzeugt bei normalem Start lokal:

```text
HermesRuntime/data/
  events/
  snapshots/
  logs/
  cache/
  archive/
```

`HermesRuntime/data/` ist ueber `HermesRuntime/.gitignore` ausgeschlossen.

## Configuration

Runtime config:

```text
HermesRuntime/config/hermes.runtime.json
```

Storage profile:

```text
HermesRuntime/config/storage.profile.json
```

Das Default-Profil nutzt `../data` relativ zum Config-Ordner, also
`HermesRuntime/data/`.

## Safe Mode

Wenn die Storage-Initialisierung fehlschlaegt und
`safeModeOnStorageFailure: true` gesetzt ist, versucht die Runtime auf
`HermesRuntime/data/safemode/` auszuweichen und markiert den Snapshot sowie
Events mit `safe_mode: true`.

Wenn das Storage-Profil fehlt oder unlesbar ist, nutzt die Runtime ebenfalls
ein lokales SafeMode-Default-Profil unter `HermesRuntime/data/safemode/`.

Wenn die Disk-Space-Pruefung fehlschlaegt oder zu wenig Speicher findet,
wird ebenfalls `safe_mode: true` gesetzt. Die Runtime soll dabei nicht heimlich
Worker starten oder riskante Aktionen ausfuehren.

## Events

Events werden als JSONL geschrieben:

```text
HermesRuntime/data/events/YYYYMMDD.runtime.events.jsonl
```

Sprint 1 schreibt:

- `runtime_started`
- `runtime_stopped`

Event-Schema:

- `schema_version`
- `event_id`
- `timestamp`
- `source`
- `category`
- `severity`
- `event_type`
- `message`
- `metadata`
- `requires_attention`

## Snapshot

Snapshot-Datei:

```text
HermesRuntime/data/snapshots/runtime_snapshot.json
```

Der Snapshot enthaelt:

- Runtime-Name
- Environment
- Storage-Profil
- Storage-Pfade
- Start-/Stop-Zeit
- SafeMode-Status
- Disk-Space-Status

## Start

Vom Repo-Root:

```bash
dotnet run --project HermesRuntime/Hermes.Runtime.csproj
```

Oder aus dem Projektordner:

```bash
cd HermesRuntime
dotnet run
```

Optional mit expliziter Config:

```bash
dotnet run --project HermesRuntime/Hermes.Runtime.csproj -- --config HermesRuntime/config/hermes.runtime.json
```

## Acceptance Criteria

- `dotnet run` startet die Runtime.
- fehlende Ordner werden erzeugt.
- `RuntimeStarted` Event wird gespeichert.
- Snapshot wird gespeichert.
- `RuntimeStopped` Event wird gespeichert.
- keine Exceptions beim normalen Start.
- SafeMode bei ungueltigem Storage ist moeglich.
- alles bleibt lokal/offline.

## Current Validation Note

In dieser Arbeitsumgebung ist `dotnet` nicht installiert. Die Projektdateien
sind erstellt, aber der lokale Run muss auf einem Rechner mit installiertem
.NET SDK ausgefuehrt werden.
