# cTrader Bot Paper Runtime Scope V1

## Purpose

This document defines the exact functional scope of the first cTrader Paper Runtime version.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## In-Scope Functions

The first Paper Runtime may only perform the following functions:

- Bundle Import
- Bundle Validation
- Bundle Activation
- `last_valid_release_bundle` fallback
- Bot Configuration Validation
- Runtime Market Context Read
- Spread Check
- Session Check
- Signal/Setup Evaluation from the active bundle
- Paper Decision Calculation
- Paper Lifecycle Status
- State Transition Logging
- Paper Decision Logging
- Error Logging
- Kill-Switch Handling
- Local Runtime Summary

## Out-of-Scope Functions

The first Paper Runtime must not perform any of the following:

- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`
- `CancelPendingOrder`
- Position Management
- Pending Order Management
- Account Risk Mutation
- Strategy Mutation
- Backtesting
- OOS
- Forward Learning
- Release Manifest Mutation
- Safety Flag Mutation
- External Network Calls
- Secrets Access

## Paper Decision Outputs

Allowed paper decision outputs:

- `would_wait`
- `would_enter_long`
- `would_enter_short`
- `would_skip`
- `would_invalidate`
- `would_expire`
- `would_block_by_safety`
- `would_block_by_spread`
- `would_block_by_session`
- `would_block_by_config`
- `would_block_by_drift`

Every output must include:

- `broker_action=none`

## Runtime Inputs

### Allowed Inputs

- `active_release_bundle`
- `last_valid_release_bundle`
- local cBot parameters
- current symbol
- current timeframe
- bid
- ask
- spread
- server time
- tick size
- pip size

### Not Allowed as Decision Inputs

- live order state for decisions
- account balance for risk mutation
- external APIs
- secrets

## Runtime Logs

Planned logs:

- `bundle_import_log.jsonl`
- `bot_state_transition_log.jsonl`
- `paper_decision_log.jsonl`
- `runtime_observation_log.jsonl`
- `local_error_log.jsonl`
- `kill_switch_events.jsonl`
- `bot_runtime_summary.json`

## cBot Lifecycle Mapping

### `OnStart()`

- validate config
- validate `last_valid_release_bundle`
- start timer

### `OnTimer()`

- check import inbox
- validate bundle
- execute the Paper Runtime step

### `OnTick()`

- update market context only
- keep logic lightweight

### `OnBar()`

- optional setup evaluation when bar-based

### `OnStop()`

- write summary

### `OnException()`

- activate kill switch
- write error log

## Kill-Switch Scope

Kill-switch is active when any of the following occur:

- invalid configuration
- safety flag violation
- invalid bundle without fallback
- blocking drift
- forbidden capability detected
- manual kill switch
- repeated validation failures
- unexpected exception

## Release Mode Limit

V1 accepts only:

- `release_mode = paper_only`

All other values must result in:

- `rejected_release_mode`
- `kill_switch_active=true`
- `broker_action=none`

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

- Should `OnBar()` be enabled only for bar-based bundles or always available as optional logic?
- Should the runtime summary be written on every timer tick or only on stop and state changes?
- Should repeated validation failures have a fixed threshold before kill-switch activation?
- Should `release_mode` rejection be logged differently from bundle validation failures?
- Should `would_block_by_drift` be emitted only when drift is blocking or also for medium drift with policy limits?

## Summary

V1 is a paper-only runtime that can import, validate, activate, observe, and summarize bundles while refusing all trading, mutation, and research functions.

It is limited to safe local evaluation and logging, with kill-switch protection for any invalid or unsafe state.
