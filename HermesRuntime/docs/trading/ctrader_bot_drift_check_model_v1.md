# cTrader Bot Drift Check Model V1

## Purpose

This document defines how HermesRuntime checks whether later cTrader bot logic is sufficiently aligned with the validated HermesRuntime strategy before a bot release candidate may become `paper_ready`.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## Drift Definition

Drift means that the cTrader bot version behaves differently from the validated HermesRuntime strategy, even though both originate from the same strategy package.

## Drift Categories

The drift check must cover at least these categories:

- Entry Rule Drift
- Exit Rule Drift
- Stop Loss Drift
- Take Profit Drift
- Invalidation Drift
- Session Filter Drift
- Spread Filter Drift
- Risk Profile Drift
- Symbol Mapping Drift
- Timeframe Mapping Drift
- Timezone Drift
- TickSize/PipSize/Rounding Drift
- Data Source Drift
- Parameter Default Drift
- Version Drift
- Safety Flag Drift

## Drift Severity

Severity levels:

- `none`
- `low`
- `medium`
- `high`
- `blocking`

### Blocking Criteria

Drift is blocking if any of the following are true:

- safety flags differ
- entry or exit rules do not match unambiguously
- risk limits are riskier
- symbol or timeframe mapping is unknown
- `TickSize` / `PipSize` is not checked
- the cTrader bot supports a different schema
- `forbidden_capabilities` are missing

## Drift Checklist

Planned artifact:

- `ctrader_bot_drift_checklist.md`

### Required Areas

- Strategy Package Identity
- Bot Version Identity
- Schema Compatibility
- Entry Logic Mapping
- Exit Logic Mapping
- Risk Mapping
- Session Mapping
- Spread Mapping
- Symbol Mapping
- Timeframe Mapping
- Timezone Mapping
- Precision Mapping
- Safety Mapping
- Forbidden Capabilities
- Known Differences
- Human Review Notes

## Drift Summary JSON

Planned artifact:

- `ctrader_bot_drift_summary.json`

### Required Fields

- `drift_check_id`
- `generated_at`
- `bot_release_id`
- `bot_version`
- `strategy_package_id`
- `strategy_package_version`
- `schema_version`
- `overall_drift_severity`
- `blocking_drift_found`
- `checked_categories[]`
- `known_differences[]`
- `unresolved_questions[]`
- `human_review_required`
- `paper_ready_allowed`

## Release Gate

A release candidate may become `paper_ready` only if all of the following are true:

- `blocking_drift_found = false`
- `overall_drift_severity <= medium`
- safety mapping is fully checked
- symbol mapping is checked
- timeframe mapping is checked
- precision mapping is checked
- `forbidden_capabilities` are complete
- human review status exists

## Known Differences

Small differences may be allowed if they are explicitly documented.

Examples:

- cTrader uses Bid/Ask instead of midprice
- rounding to `TickSize`
- server time instead of local time

Every known difference must include:

- severity
- expected impact

## Drift Test Types

These test types are planned for later implementation:

- Static Mapping Review
- Synthetic Signal Replay
- Historical Bar Replay
- Boundary Condition Tests
- Spread Scenario Tests
- Session Boundary Tests
- Timezone Boundary Tests
- Precision/Rounding Tests

This document defines the test plan only. It does not implement the tests.

## Safety Behavior

If drift is blocking:

- set `release_status=blocked_drift`
- set `paper_ready_allowed=false`
- keep `broker_action=none`
- do not activate the release
- require human review

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

- Should drift checks run only at release time or also after bundle import?
- Which categories are required for every strategy package versus only for trading strategies?
- Should low-severity known differences be versioned in a reusable registry?
- How should synthetic replay outputs be compared against HermesRuntime outputs?
- Should precision checks be bundle-specific or asset-specific?
- How many known differences are acceptable before a release is blocked by policy?

## Summary

V1 blocks release candidates when drift is safety-critical or semantically ambiguous.

Small, documented differences may be tolerated only when severity and expected impact are explicit.
