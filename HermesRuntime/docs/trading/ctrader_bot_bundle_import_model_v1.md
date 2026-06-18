# cTrader Bot Bundle Import Model V1

## Purpose

This document defines how a later cTrader bot imports, validates, activates, and falls back from a release bundle.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## Scope

The import model applies to the canonical flat release bundle described in:
- `docs/trading/ctrader_bot_release_bundle_format_v1.md`

The import model assumes HermesRuntime remains the authoritative author of bundle artifacts.

## Import Locations

Recommended local folders:

- `release_bundle_inbox/`
- `active_release_bundle/`
- `last_valid_release_bundle/`
- `rejected_release_bundles/`
- `archived_release_bundles/`
- `local_runtime_logs/`

### V1 Recommendation

V1 uses `release_bundle_inbox/` as the import entry point.

The bot watches or checks this inbox and moves a validated bundle into the active location only after all checks pass.

## Import State Machine

The import flow uses these states:

- `waiting_for_bundle`
- `bundle_detected`
- `validating_bundle`
- `bundle_valid`
- `bundle_invalid`
- `activating_bundle`
- `active_bundle_ready`
- `fallback_to_last_valid`
- `disabled_until_valid_bundle`
- `kill_switch_active`

### State Meaning

- `waiting_for_bundle`: no bundle is present
- `bundle_detected`: a candidate bundle was found
- `validating_bundle`: the bundle is under validation
- `bundle_valid`: the bundle passed validation
- `bundle_invalid`: the bundle failed validation
- `activating_bundle`: the bundle is being promoted to active
- `active_bundle_ready`: the bundle is active and usable
- `fallback_to_last_valid`: the new bundle failed, so the last valid bundle is reused
- `disabled_until_valid_bundle`: no valid bundle exists yet
- `kill_switch_active`: a safety violation forced shutdown of bundle use

## Validation Order

Recommended validation order:

1. Required files present?
2. `provenance.json` readable?
3. `ctrader_bot_release_manifest.json` readable?
4. `release_mode == paper_only`?
5. Safety flags correct?
6. `checksums.json` readable?
7. SHA-256 full-bundle check
8. Schema compatible?
9. `forbidden_capabilities` complete?
10. Drift checklist present?
11. Bundle version newer, or explicitly allowed rollback?

## Activation Rules

A bundle may become active only if all of the following are true:

- every validation step passes
- manifest and provenance are consistent
- checksums are valid
- `release_mode == paper_only`
- safety flags are correct
- `bot_version` is compatible
- `schema_version` is compatible
- `forbidden_capabilities` is complete
- no kill switch is active

## `last_valid_release_bundle`

`last_valid_release_bundle` is the previously activated bundle that passed all checks.

### Rules

- it is set only after a completely successful activation
- it is never overwritten by an invalid bundle
- it may continue to be used if a new bundle import fails
- it must contain only `paper_only` content
- it keeps its own checksums
- it must be revalidated at bot start

## Rollback Rules

Rollback is allowed only if all of the following are true:

- `rollback_version` exists in the manifest
- the rollback bundle is available locally
- the rollback bundle validates successfully
- the rollback bundle is `paper_only`
- rollback does not grant new rights
- the rollback reason is logged

Rollback is a controlled fallback to a previously valid bundle, not a permission escalation.

## Error Behavior

### Invalid New Bundle

If the new bundle is invalid:

- set `bundle_rejected`
- log the reason
- continue using `last_valid_release_bundle` if available
- otherwise set `disabled_until_valid_bundle`
- keep `broker_action=none`

### Safety Violation

If a safety violation is detected:

- set `kill_switch_active=true`
- produce no paper decision
- do not activate any bundle
- keep `broker_action=none`
- set `manual_review_required=true`

## Write Permissions

### cTrader may not modify:

- release manifest
- checksums
- provenance
- strategy package
- safety flags
- release status

### cTrader may write only:

- `bundle_import_log.jsonl`
- `bot_state_transition_log.jsonl`
- `paper_decision_log.jsonl`
- `local_error_log.jsonl`
- `runtime_observation_log.jsonl`

## Logging

Every import attempt logs:

- `timestamp`
- `bundle_path`
- `detected_bot_version`
- `detected_release_id`
- `validation_result`
- `failure_reason`
- `activated`
- `fallback_used`
- `kill_switch_active`
- `broker_action=none`

## Import Frequency

V1 supports:

- manual import
- timer-based checks

Preferred:
- `OnTimer()` checks the inbox periodically

Not allowed:
- `OnTick()`-driven import
- duplicate concurrent import attempts while validation is running

## Import Flow Overview

### Text Graph

```text
release_bundle_inbox/
↓
bundle_detected
↓
validating_bundle
↓
gültig?
├─ ja → activating_bundle → active_bundle_ready → last_valid_release_bundle aktualisieren
└─ nein
   ├─ last_valid_release_bundle vorhanden? → fallback_to_last_valid
   └─ nein → disabled_until_valid_bundle
```

### Safety Override

If a safety violation is detected at any point:

```text
beliebiger Zustand → kill_switch_active
```

Safety always takes precedence over fallback behavior.

## Decision Table

| Situation | Ergebnis | last_valid_release_bundle | kill_switch | broker_action |
|---|---|---|---|---|
| gültiges neues Bundle | `active_bundle_ready` | aktualisieren | false | `none` |
| ungültiges neues Bundle mit last_valid | `fallback_to_last_valid` | weiterverwenden | false | `none` |
| ungültiges neues Bundle ohne last_valid | `disabled_until_valid_bundle` | nicht vorhanden | false | `none` |
| Safety-Flag verletzt | `kill_switch_active` | unverändert | true | `none` |
| Checksum mismatch | `bundle_invalid` / `fallback_to_last_valid` oder `disabled_until_valid_bundle` | unverändert | false | `none` |
| Rollback gültig | `active_bundle_ready` | auf Rollback-Version setzen | false | `none` |
| Rollback ungültig | `bundle_invalid` / `fallback_to_last_valid` oder `disabled_until_valid_bundle` | unverändert | false | `none` |
| Bundle-Version älter ohne Rollback-Erlaubnis | `bundle_invalid` | unverändert | false | `none` |

## Clarification

- cTrader never activates a partially valid bundle.
- Invalid bundles never overwrite `last_valid_release_bundle`.
- Safety violations override fallback behavior.
- `broker_action` always remains `none`.
- V1 remains `paper_only`.

## Safety Invariants

These values remain mandatory:

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Open Implementation Questions

- Should import use a manual trigger or a timer-only scan in the first cBot release?
- Where exactly should `last_valid_release_bundle` live on disk?
- Should rollback bundles be stored beside active bundles or in a separate archive?
- Should cTrader validate manifest-first or checksum-first in the final implementation?
- How should failed bundles be archived and retained?
- How many historical rollback bundles should remain available locally?

## Summary

V1 imports bundles through `release_bundle_inbox/`, validates them in a strict order, activates only paper-only bundles, and falls back to `last_valid_release_bundle` if the new bundle fails.

Safety violations always trigger `kill_switch_active=true` and block paper decisions.
