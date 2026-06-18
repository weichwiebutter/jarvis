# cTrader Bot Release Candidate Contract V1

## Purpose

This document defines the contract for turning a validated strategy package into a cTrader bot release candidate.

It is specification only.
It contains no cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

The long-term model is:
- HermesRuntime develops and certifies
- cTrader runs a versioned bot release
- HermesRuntime does not need to run permanently

## Definitions

### strategy_package

A validated strategy artifact produced by HermesRuntime that contains signal logic, safety constraints, and validation metadata.

### ensemble_signal_agent_package.json

The structured export file used as the main V1 strategy handoff and paper-test input.

### certified_strategy_package

A strategy package that has passed all required validation, review, confidence, and safety checks for release-candidate consideration.

### bot_release_candidate

A specific cTrader bot release artifact derived from exactly one certified strategy package.

### bot_version

The version of the cTrader bot artifact itself, independent from the strategy package version.

### release_manifest

The metadata file that describes a bot release candidate and its validation state.

### rollback_version

The previous paper-only bot version to which the release can safely return.

### paper_only_release

A release candidate that can operate only in paper/simulation mode.

### demo_candidate_release

A future release type that may be designed for demo-only usage if separately approved.

### live_forbidden_release

A release that is explicitly forbidden from live execution.

## Release Candidate Preconditions

A bot release candidate may only be created if all of the following are present:
- strategy package exists
- schema is valid
- safety flags are complete
- backtest exists
- backtest quality audit exists
- OOS exists or is explicitly marked pending
- forward evidence exists or is explicitly marked pending
- confidence score exists
- human review status exists
- `no_auto_trading=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`

If any required condition is missing, the candidate remains blocked.

## Release Candidate Status Model

Allowed statuses:
- `draft`
- `blocked_missing_validation`
- `paper_ready`
- `paper_running`
- `paper_observed`
- `demo_spec_ready`
- `demo_blocked`
- `deprecated`
- `rollback_available`

### V1 Restriction

V1 may reach at most:
- `paper_ready`

V1 must not reach:
- demo approval
- live approval
- execution approval

## Release Manifest

Planned manifest file:
- `ctrader_bot_release_manifest.json`

### Required Fields

- `bot_release_id`
- `bot_version`
- `strategy_package_version`
- `source_package_id`
- `source_research_run_id`
- `generated_at`
- `certified_at` nullable
- `release_status`
- `release_mode = paper_only`
- `compatible_schema_version`
- `supported_assets`
- `supported_timeframes`
- `included_setups`
- `risk_profile`
- `safety_flags`
- `validation_summary`
- `confidence_summary`
- `human_review_summary`
- `forbidden_capabilities`
- `rollback_version`
- `artifact_checksums`

## Forbidden Capabilities

Every release candidate must declare these forbidden capabilities:
- `execute_market_order`
- `place_limit_order`
- `place_stop_order`
- `modify_position`
- `close_position`
- `cancel_pending_order`
- `access_live_trading`
- `broker_order_api`
- `external_network_access`

## Bot Versioning Model

- `bot_version` should use semantic versioning, for example `0.1.0-paper`
- `strategy_package_version` is separate from `bot_version`
- `schema_version` is separate from both
- one bot version references exactly one strategy package
- multiple bot versions may originate from the same strategy package
- every new strategy package version requires a new review decision

## Rollback Contract

- `rollback_version` is optional
- rollback may only target a previous `paper_only` version
- rollback must not activate demo or live rights
- rollback must have its own manifest
- rollback reason must be logged

## Drift Check Contract

Before a candidate can become `paper_ready`, the drift check must be documented.

The drift checklist must compare HermesRuntime and cTrader behavior for:
- entry rules
- exit rules
- session filters
- spread filters
- risk profile
- timeframe mapping
- symbol mapping
- timezone
- rounding, tick size, and pip size

If drift is not checked, the candidate cannot be marked `paper_ready`.

## Planned Artifacts

- `ctrader_bot_release_manifest.json`
- `ctrader_bot_release_summary.md`
- `ctrader_bot_drift_checklist.md`
- `ctrader_bot_release_notes.md`
- `ctrader_bot_rollback_plan.md`

## Safety Invariants

These flags are mandatory:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

Any future deviation requires:
- a separate specification
- explicit human approval

## Release Workflow

1. HermesRuntime produces a strategy package
2. the package is certified
3. drift is checked
4. a bot release candidate is created
5. a release manifest is written
6. human review approves or blocks
7. paper-only release is published
8. later updates produce new bot versions or new strategy packages

## Paper-Only Rule

V1 release candidates are paper-only.

They may:
- consume validated signals
- log simulated decisions
- observe safety state

They may not:
- trade
- access the order API
- connect to live or demo execution

## Open Questions

- Which artifact is the canonical source of truth for the release manifest?
- Should drift checks be manual, automated, or both?
- Should a candidate be blocked until all pending evidence is closed, or only until the missing items are explicitly marked pending?
- How should rollback manifests be stored and indexed?
- How should `bot_version` naming evolve across paper, demo-spec, and future live-forbidden releases?

## Summary

The release candidate contract keeps paper-only bot releases auditable and versioned.
It separates strategy certification from bot versioning and preserves strict safety gating.
