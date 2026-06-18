# cTrader Paper Bot Compile Safety Plan V1

## Purpose

This note describes how the paper bot skeleton should be compiled safely later, without introducing a real cTrader project file or any trading API dependency.

It is documentation only.

## Current State

The repository intentionally does not yet contain:

- a final `.csproj`
- a `.algo` file
- a cTrader project file
- any cTrader API references

## Safe Compile Approach

Later compile validation should use a temporary scratch project outside the committed repository, for example under:

- `/tmp`
- `.codex_scratch`

The scratch project should:

- reference only the skeleton C# files
- remain temporary
- be deleted or clearly marked temporary after the check

## Required Pre-Build Guard

Before any later build or review:

1. run `bash scripts/check_ctrader_paper_bot_forbidden_refs.sh`
2. run `bash scripts/preflight_ctrader_paper_bot.sh`

## Why No Real cTrader Project Yet

A real cTrader project file is intentionally deferred because:

- the skeleton is not yet an implementation
- the paper-only contract must remain isolated from trading APIs
- the forbidden reference guard must stay easy to run before any future build
- the final cTrader dependency set is not yet required for the skeleton

## Expected Later Dependencies

When a real cTrader implementation is eventually created, it may need:

- cTrader/cAlgo SDK references
- platform-specific project metadata
- a release bundle import path
- logging and summary output paths

Those dependencies are intentionally excluded from V1 skeleton work.

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

V1 compile safety is handled through a temporary scratch build plus the forbidden reference guard.

No persistent cTrader project file should be added until implementation work is intentionally started.
