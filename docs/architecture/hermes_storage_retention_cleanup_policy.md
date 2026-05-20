# Hermes Storage Retention & Cleanup Policy

Status: proposed policy  
Scope: documentation only  
Applies to: HermesRuntime storage, backtesting, replay, feature exports, research outputs

## Purpose

Hermes wird kuenftig Backtesting-, Replay-, Feature-, Tick-, Candle-,
Research- und Learning-Daten erzeugen. Diese Daten duerfen nicht unkontrolliert
im Projektordner oder auf der System-SSD wachsen.

Diese Policy definiert Datenklassen, Retention-Regeln, Storage-Pressure-Schutz
und spaetere Komponenten. Sie ist bewusst keine Implementierung.

## Non-Goals

- Keine Cleanup-Logik in der Runtime.
- Keine Dateien loeschen.
- Keine Services starten.
- Keine Archivierung ausfuehren.
- Keine Backtests, Replays oder Research-Jobs ausfuehren.
- Keine Aenderung an `HermesRuntime/`.

## Core Principles

- Der Projektordner ist kein Massendatenspeicher.
- Aktive Arbeitsdaten bleiben lokal schnell erreichbar.
- Historische Massendaten werden in Archive verschoben.
- Approved Learning, Modelle und Cluster duerfen niemals automatisch geloescht
  werden.
- Cleanup muss planbar, auditierbar und approval-aware sein.
- Emergency cleanup darf nur risikoarme temporare Daten entfernen.
- Bei Storage Pressure wird neue Arbeit gestoppt, nicht blind Speicher
  freigeraeumt.

## Data Classes

| Data class | Examples | Sensitivity | Default handling |
| --- | --- | --- | --- |
| temp/cache | runtime cache, downloaded transient metadata, intermediate scratch files | low | aggressiv loeschen |
| runtime logs | local runtime logs, worker logs, diagnostic text | medium | zeitlich begrenzen |
| runtime events | JSONL event stream under `data/events/` | medium/high | rotieren, komprimieren, archivieren |
| audit logs | approvals, safety gates, human decisions | high | lang halten, nicht still loeschen |
| snapshots | runtime snapshots and manifests | medium | rotieren, letzte valide Snapshots behalten |
| replay manifests | metadata for replay inputs and versions | high | lange behalten, klein, audit-relevant |
| replay data | generated replay inputs/outputs, replay artifacts | high volume | zeitlich begrenzen und archivieren |
| feature exports | exported feature JSONL/CSV data | high volume | aktive Runs kurz halten, relevante Sets archivieren |
| raw tick data | quote/tick history, cTrader raw exports | very high volume | niemals im Projektordner massenhaft speichern |
| candle data | OHLCV/M1/M5/H1/H4/D1 datasets | high volume | komprimieren, versionieren, archivieren |
| backtest runs | per-run metrics, trades, equity curves, logs | high volume | schlechte/irrelevante Runs begrenzen |
| walk-forward runs | segment results, train/test windows, OOS stats | high volume/high value | laenger halten, archivieren |
| research reports | overnight reports, hypotheses, summaries | medium | failed/irrelevant begrenzen, approved behalten |
| approved learning sets | human-approved datasets and labels | high value | niemals automatisch loeschen |
| approved models/clusters | approved model artifacts, pattern clusters, scoring versions | high value | niemals automatisch loeschen |

## Suggested Retention Rules

These values are initial defaults for a future policy manager. They must remain
configurable and must not be hard-coded into runtime behavior without review.

| Data class | Active retention | Archive retention | Automatic delete allowed |
| --- | --- | --- | --- |
| temp/cache | 1-7 days | none | yes |
| runtime logs | 14-30 days | 90-180 days compressed | yes, after archive window |
| runtime events | 30-90 days active | 1-3 years compressed | only after archive and policy review |
| audit logs | 1 year active | long-term/NAS backup | no silent delete |
| snapshots | keep last 20-50 valid snapshots | monthly checkpoint snapshots | yes, except protected snapshots |
| replay manifests | 180 days active | long-term compressed | no silent delete |
| replay data | 14-60 days active | archive if linked to approved report | yes, if unapproved/expired |
| feature exports | 14-60 days active | archive if linked to accepted run | yes, if transient |
| raw tick data | external data store only | HDD/NAS long-term | no deletion without explicit policy |
| candle data | active windows on SSD | HDD/NAS compressed archive | only duplicate/derived copies |
| backtest runs | 30-90 days active | selected runs archived | yes, failed/irrelevant only |
| walk-forward runs | 90-180 days active | selected runs archived | limited, with report retention |
| research reports | 30-180 days active | approved reports archived | yes, failed/irrelevant only |
| approved learning sets | indefinite | replicated backup | never automatic |
| approved models/clusters | indefinite | replicated backup | never automatic |

## Retention Detail

### temp/cache

- Cleared aggressively.
- Safe target for automatic emergency cleanup.
- No human approval required if files are explicitly marked temporary.
- Must never contain approved learning data, broker credentials, audit logs, or
  final model artifacts.

### runtime logs

- Time-bounded by default.
- Older logs should be compressed before deletion.
- Error logs related to incidents should be promoted to audit/research records
  before cleanup.

### runtime events

- JSONL event streams are append-only while active.
- Older event files should be compressed and moved to archive storage.
- Runtime events linked to approvals, safety gates, model changes, or learning
  decisions must remain reconstructable.

### audit logs

- Human approval, rejection, override, safety gate, and learning decisions are
  audit data.
- Audit logs must not be silently deleted.
- Any future deletion must require explicit policy, report, and approval.

### snapshots

- Keep a rolling set of recent valid snapshots.
- Keep periodic checkpoint snapshots for recovery history.
- Corrupted snapshots should be quarantined or marked, not silently deleted.
- Snapshots referenced by incident reports must be protected.

### replay manifests and replay data

- Replay manifests are small and should outlive raw replay output.
- Replay data can be large and should be removed or archived based on whether a
  run was approved, cited, or still under review.
- Replay artifacts used for published research reports must remain traceable.

### feature exports

- Feature exports are derived data and can grow quickly.
- Transient feature exports from failed or duplicate runs can expire.
- Feature exports tied to approved learning sets must be promoted and protected.

### raw tick data and candle data

- Raw tick data belongs in a dedicated quote data store, not the repo.
- Candle data should be partitioned by symbol, timeframe, and date range.
- Duplicate derived candle data can be regenerated and may be cleaned under
  policy.
- XAUUSD, EURUSD, GER40, US500, and Forex-Majors should be partitioned
  separately to avoid accidental cross-market cleanup.

### backtest and walk-forward runs

- Failed, duplicate, or irrelevant runs should be capped.
- Walk-forward and out-of-sample runs have higher long-term value than simple
  exploratory runs.
- Runs used in decisions, reports, or learning approvals must be protected.
- Every retained run should have enough metadata to identify inputs,
  parameters, feature schema, model version, and result summary.

### research reports

- Failed overnight research jobs should expire quickly.
- Morning reports reviewed by Frank should be retained longer.
- Approved research reports should be archived with linked run IDs and learning
  decisions.

### approved learning sets, models, and clusters

- Never automatically delete.
- Require explicit human approval for archive migration.
- Require checksums and version metadata.
- Require backup before any manual removal.

## Storage Pressure Protection

Hermes must degrade safely when disk space becomes constrained.

### Thresholds

Initial suggested thresholds:

- Warning threshold: free disk below 20 percent or below 100 GB.
- Critical threshold: free disk below 10 percent or below 50 GB.
- Emergency threshold: free disk below 5 percent or below 20 GB.

Exact numbers should live in a future `StorageProfile`/retention policy config
and may differ per machine.

### Warning Behavior

When warning threshold is reached:

- Publish storage pressure warning event.
- Show warning in `RuntimeHealth`.
- Show warning in Jarvis Control Center.
- Stop scheduling large optional research batches.
- Prefer compressed/archive storage for new reports.
- Require review before starting large replay/backtest batches.

### Critical Behavior

When critical threshold is reached:

- Stop new research jobs.
- Disable replay generation.
- Disable feature export jobs that create large outputs.
- Prevent new backtest/walk-forward runs.
- Keep runtime read-only health reporting alive if possible.
- Enter safe mode if configured by `DiskSpaceGuard`.
- Surface `safe_mode`, `last_error`, and storage pressure in `RuntimeHealth`.

### Emergency Behavior

Emergency cleanup is allowed only for explicitly temporary data:

- `temp/`
- `cache/`
- incomplete scratch files marked as transient

Emergency cleanup must not delete:

- audit logs
- runtime events
- snapshots
- replay manifests
- raw tick/candle source data
- research reports under review
- approved learning sets
- approved models/clusters

## Tiered Storage

Hermes storage should be tiered by access pattern and data value.

### SSD: Active Runs

Use SSD for:

- active runtime state
- current queue/job metadata
- active feature exports
- active replay/backtest working sets
- recent snapshots
- recent runtime events

SSD should not store unlimited tick history or years of backtest artifacts.

### HDD: Backtest Archives

Use HDD for:

- compressed historical backtest runs
- older walk-forward outputs
- candle archives
- replay data no longer needed for active work
- selected research artifacts

### NAS: Backup and Long-Term Archive

Use NAS or equivalent backup storage for:

- approved learning sets
- approved models/clusters
- audit logs
- monthly event archives
- major research milestones
- raw source market data backups

NAS is an archive/backup target, not a hot runtime dependency.

### Project Folder Boundary

The git project folder must not become the mass storage root. Large data should
live under configured external storage paths and only small docs, configs,
manifests, and source code should remain in the repo.

## Future Components

### RetentionPolicyManager

Responsible for loading and validating retention policy configuration.

Future responsibilities:

- map data classes to retention windows
- enforce protected classes
- expose dry-run plans
- integrate with `StorageProfile`
- publish retention policy events

### CleanupPlanner

Responsible for planning cleanup actions without executing them immediately.

Future responsibilities:

- scan storage metadata
- classify candidates
- produce dry-run cleanup plans
- estimate freed space
- require approval for non-temp cleanup
- block deletion of protected classes

### ArchiveManager

Responsible for moving approved archive candidates to HDD/NAS tiers.

Future responsibilities:

- archive selected event/log/run folders
- preserve manifests and checksums
- write archive index metadata
- support restore planning

### CompressionManager

Responsible for compression of eligible logs, events, reports, and run outputs.

Future responsibilities:

- compress old JSONL event files
- compress old logs
- compress large run artifacts
- verify compressed checksums
- avoid compressing files still in active use

### StorageDashboardPanel

Jarvis Control Center should eventually show:

- total/free disk by tier
- data-class usage
- warning/critical pressure status
- pending cleanup plan
- archive backlog
- protected learning/model sizes
- last cleanup dry-run
- last archive action

## Integration Points

### HermesRuntime StorageManager

`StorageManager` owns storage path initialization. Future retention work should
not bypass it. Retention policy should use configured storage roots and data
class mappings instead of hard-coded paths.

### DiskSpaceGuard

`DiskSpaceGuard` should remain the early safety layer for free-space checks.
Future retention components can use DiskSpaceGuard signals to decide whether to
warn, block work, enter safe mode, or request emergency cleanup.

### RuntimeHealth

`RuntimeHealth` should expose storage pressure in a compact read-only form:

- free disk
- warning/critical threshold state
- safe mode
- blocked research/replay state
- last cleanup plan summary
- last archive status
- last storage error

### Jarvis Control Center

Jarvis Control Center should visualize retention state without hiding risk:

- no silent cleanup
- no hidden learning deletion
- no hidden model deletion
- clear approval state
- explicit storage pressure warnings
- visible protected data classes

### Backtesting and Research

Backtesting and research jobs must declare expected output class and approximate
storage budget before starting. Under storage pressure, Hermes should prefer:

1. stop new research jobs
2. disable replay generation
3. skip non-critical feature exports
4. use archived historical data where available
5. request manual approval for any non-temp cleanup

## Approval Rules

Allowed without approval:

- cleanup of explicit temp/cache data
- compression of closed runtime logs/events when checksums are retained
- deletion of failed scratch files marked transient

Requires approval:

- deleting completed backtest runs
- deleting replay data
- deleting feature exports not marked transient
- deleting research reports
- moving approved artifacts to long-term archive

Never automatic:

- audit logs
- approved learning sets
- approved models/clusters
- manually protected snapshots
- source market data designated as canonical

## Open Decisions

- Exact retention windows per machine and storage tier.
- Whether raw tick data is stored under a Hermes-managed external root or a
  separate quote-data service root.
- Archive index format.
- Compression format for JSONL events and large run outputs.
- Required UI approval flow before non-temp cleanup.
- Backup verification process for approved learning/model artifacts.

## Masterplan 7 Candidates

- Add `RetentionPolicyManager` as a read-only planner first.
- Add `StorageDashboardPanel` to Jarvis Control Center.
- Extend `RuntimeHealth` with storage pressure flags.
- Add dry-run-only cleanup plans before any destructive action exists.
- Require approval gates before cleanup of research/backtest/replay artifacts.
