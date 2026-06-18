# cTrader Bot Release Bundle Format V1

## Purpose

This document defines the canonical release bundle format for cTrader bot handoff artifacts.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## Architecture Decision

V1 uses a flat directory as the canonical working format.

Optional later:
- a `.zip` archive may be produced for transport

Canonical validation always happens on the extracted flat directory, not on the zip archive.

## Why Flat Directory Is Canonical

Flat Directory is the V1 canonical format because it is:
- easier to inspect
- easier to diff
- easier for operators to review manually
- less prone to packaging errors
- easier to validate file-by-file
- more transparent during release review

Zip is only a transport artifact and not the source of truth.

## Canonical Bundle Structure

```text
ctrader_bot_release_bundle/
├── ctrader_bot_release_manifest.json
├── ctrader_bot_release_summary.md
├── ctrader_bot_drift_checklist.md
├── ctrader_bot_release_notes.md
├── ctrader_bot_rollback_plan.md
├── ensemble_signal_agent_package.json
├── ensemble_signal_agent_package.schema.json
├── checksums.json
└── provenance.json
```

Optional later:
- `ctrader_bot_release_bundle.zip`

## `checksums.json`

`checksums.json` is generated only by HermesRuntime.
cTrader may read it, but must never write it.

### Checksum Rules

- checksum algorithm: SHA-256
- checksums cover all required artifacts except `checksums.json` itself
- each artifact entry must include:
  - `path`
  - `sha256`
  - `size_bytes`
  - `generated_at`
  - `required`

### Scope

Checksums apply to:
- manifest
- release summary
- drift checklist
- release notes
- rollback plan
- signal package
- schema
- provenance

If markdown summaries are present, they are also checksum-validated.

## Full-Bundle Validation

V1 validates the full bundle, not only the manifest.

Required checks:
- manifest checksum
- signal package checksum
- schema checksum
- provenance checksum
- all required artifact checksums

## `provenance.json`

`provenance.json` is the provenance anchor of the release bundle.

### Required Fields

- `provenance_id`
- `generated_at`
- `generated_by = HermesRuntime`
- `source_system = SystemA/HermesRuntime`
- `source_repo`
- `source_commit_sha` nullable
- `source_branch` nullable
- `source_research_run_id`
- `source_strategy_package_id`
- `source_strategy_package_version`
- `bot_release_id`
- `bot_version`
- `schema_version`
- `operator_review_required`
- `human_review_status`
- `no_auto_trading`
- `broker_orders_enabled`
- `live_trading_enabled`
- `order_api_enabled`
- `paper_mode`

## Manifest Provenance Rules

Every main artifact in the bundle must either contain or reference:
- `bot_release_id`
- `bot_version`
- `strategy_package_version`
- `generated_at`
- `source_system`
- `safety_flags`

## Bundle Validation Rules

A bundle is valid only if all of the following are true:
- all required files are present
- `checksums.json` is valid
- all SHA-256 checks match
- `provenance.json` is valid
- manifest `release_mode == paper_only`
- safety flags are correct
- schema is compatible
- all forbidden capabilities are present
- no required artifacts are missing

## cTrader Behavior on Bundle Error

If validation fails, the bot must:
- set `bundle_rejected`
- activate `kill_switch_active=true`
- keep `broker_action=none`
- produce no paper decision
- write a log entry
- continue using `last_valid_release_bundle` if available
- otherwise set `disabled_until_valid_bundle`

## Write Permissions

### HermesRuntime may write:

- all bundle artifacts

### cTrader may write:

- no bundle artifacts
- only local logs outside the release bundle

## Safety Invariants

These flags remain mandatory:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Open Implementation Questions

- Will the zip later be generated automatically from the flat directory?
- Where will `last_valid_release_bundle` be stored?
- How will cTrader import a release bundle?
- Will cTrader validate manifest-first or checksum-first?
- How will old bundles be archived?
- How many rollback versions should remain locally available?

## Summary

V1 uses a flat directory as the canonical release bundle format.
Zip is optional transport only.
HermesRuntime remains the authoritative author of the bundle, and cTrader remains a consumer and local logger only.
