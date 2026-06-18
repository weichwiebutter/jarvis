# cTrader Cloud Runtime Model V1

## Purpose

This document defines the preferred cloud runtime model for the Hermes paper bot.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## Local Mode vs Cloud Mode

### Local File Bundle Mode

- release package is loaded from local files
- suitable for local development and VPS-style deployment
- bundle import depends on local filesystem availability
- HermesRuntime may be used continuously as the source of the bundle

### Cloud Embedded Bundle Mode

- release package is embedded in the bot configuration or a compact parameter snapshot
- suited for cTrader Cloud runtime
- Cloud runs 24/7 independently from the local PC
- a cloud restart can lose local files
- HermesRuntime remains the release authority, but does not need to run permanently

## Why Local Bundle Files Are Unsuited for Cloud

- cTrader Cloud cannot rely on a developer PC being online
- local files are not a stable source of truth for cloud restarts
- cloud deployments should not depend on a local inbox or local release bundle directory

## Cloud Runtime Shape

Cloud V1 uses:

- embedded manifest/config snapshot
- embedded strategy snapshot or compact equivalent
- local runtime storage only for small state and logs
- no assumption that local bundle files exist

## Safety Invariants

These values remain mandatory:

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Summary

Cloud Mode is the preferred long-term model for independent paper-only operation.

Local File Bundle Mode remains useful for local development, testing, and VPS-style execution.
