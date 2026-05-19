# Hermes Runtime v1 - Sprint 3 SnapshotManager

Status: Implemented foundation draft
Scope: Runtime snapshots, manifests, validation, hash checking, quarantine
Current implementation: `HermesRuntime/Runtime/`

## Purpose

Sprint 3 ergaenzt die lokale Hermes Runtime um einen kleinen SnapshotManager.
Snapshots sollen den Runtime-Zustand file-based festhalten, validierbar sein
und beschaedigte Dateien erkennen, ohne Queue, Worker, WebSockets, APIs,
Trading-Logik oder KI-Logik einzufuehren.

## Non-Goals

- Keine Trading-Logik.
- Keine Queue.
- Keine Worker.
- Keine WebSockets.
- Keine APIs.
- Keine Broker-Anbindung.
- Kein Auto-Trading.
- Kein Replay-System.

## Implemented Components

- `RuntimeSnapshot`
- `SnapshotManifest`
- `SnapshotHealth`
- `SnapshotManager`
- `SnapshotValidator`
- `SnapshotLoadResult`
- `SnapshotWriteResult`
- `SnapshotValidationResult`

## Snapshot Location

Snapshots werden unterhalb des Runtime-Storage gespeichert:

```text
HermesRuntime/data/snapshots/runtime/
```

Pro Snapshot entstehen zwei Dateien:

```text
<snapshot_id>.snapshot.json
<snapshot_id>.manifest.json
```

Beschaedigte oder ungueltige Snapshots werden nach:

```text
HermesRuntime/data/snapshots/runtime/quarantine/
```

verschoben.

## RuntimeSnapshot Fields

Ein Runtime Snapshot enthaelt:

- `SnapshotId`
- `CreatedAtUtc`
- `RuntimeVersion`
- `RuntimeMode`
- `State`
- `Health`
- `LastEventId`
- `Sha256Hash`

`State` enthaelt den aktuellen `RuntimeState`. `Health` enthaelt den
SafeMode- und Disk-Space-Kontext.

## SnapshotManifest Fields

Das Manifest enthaelt:

- `ManifestVersion`
- `SnapshotId`
- `CreatedAtUtc`
- `RuntimeVersion`
- `RuntimeMode`
- `SnapshotPath`
- `SnapshotBytes`
- `Sha256Hash`

## Hash Model

Der SHA256-Hash wird ueber den kanonischen Snapshot-Inhalt berechnet, wobei
`Sha256Hash` im Snapshot selbst fuer die Hash-Berechnung auf `null` gesetzt
wird. Danach wird derselbe Hash in Snapshot und Manifest gespeichert.

Der Validator prueft:

- Manifest ist lesbar.
- Snapshot-Datei existiert.
- Snapshot ist lesbar.
- `SnapshotId` stimmt zwischen Manifest und Snapshot ueberein.
- Manifest-Hash stimmt.
- Snapshot-Hash stimmt.

## RuntimeHost Integration

Beim Start:

1. Storage wird initialisiert.
2. EventStore und EventBus werden eingerichtet.
3. `SnapshotManager.LoadLastValidSnapshot()` prueft den letzten validen
   Snapshot.
4. Ungueltige Snapshots werden in `quarantine/` verschoben.
5. Bei Fehlern wird `SnapshotValidationFailed` publiziert.

Beim Shutdown:

1. RuntimeState wird auf stopped gesetzt.
2. `SnapshotManager.WriteRuntimeSnapshot(...)` schreibt Snapshot und Manifest.
3. Snapshot wird validiert.
4. `SnapshotCreated` wird publiziert.
5. Bei Validierungsfehler wird `SnapshotValidationFailed` publiziert.
6. `RuntimeStopped` wird publiziert.
7. EventStore wird geflusht.

## Event Types

Sprint 3 ergaenzt:

- `SnapshotCreated`
- `SnapshotValidationFailed`

Bestehende Events bleiben:

- `RuntimeStarted`
- `StorageInitialized`
- `RuntimeSafeModeEnabled`
- `RuntimeStopped`

## Test

Aus `HermesRuntime/`:

```bash
dotnet run --project ./Hermes.Runtime.csproj
find ./data/snapshots -maxdepth 4 -type f | sort
find ./data/events -maxdepth 4 -type f | sort
tail -n 20 ./data/events/runtime/*.jsonl
```

## Acceptance Criteria

- `dotnet run` funktioniert.
- RuntimeSnapshot wird gespeichert.
- SnapshotManifest wird gespeichert.
- Hash ist enthalten.
- `SnapshotCreated` Event wird geschrieben.
- Runtime beendet sauber.

## Safety

Sprint 3 fuehrt keine Trading-, Broker-, Queue-, Worker-, API- oder
WebSocket-Funktionalitaet ein. Snapshots sind lokale Statusdateien, keine
Commands.
