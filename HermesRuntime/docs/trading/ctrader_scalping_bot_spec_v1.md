# cTrader Scalping Bot Spec V1

## Purpose

This specification describes a future cTrader Scalping Bot as a read-only consumer of validated System A outputs.

System A remains the research and certification system:
- market data collection
- signal generation
- backtest, OOS, and forward validation
- confidence and review workflows
- certification of stable candidates

The bot remains a downstream paper/simulation consumer:
- export consumer of validated signals
- paper/simulation decision helper
- no research logic
- no order execution logic

This spec does not define any bot implementation.

## Hard Safety Contract

The bot must never enable:
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- real-money order placement
- demo orders
- cTrader Order API access

The bot always operates with:
- `no_auto_trading=true`
- `human_review_required=true`
- `paper_mode=true`

Any limit increase changes only:
- simulation behavior
- paper decision selection
- logging
- forward-test observation support

It never authorizes real or demo order execution.

## Safety Profile Model

Risk management is profile-based and configurable.
Profiles define conservative defaults instead of hard-coded universal caps.

### Profiles

#### `safety_validation`

Use for validation of safety rules and early-stage paper checks.

- `max_active_signals_per_asset = 1`
- `max_total_active_signals = 3`
- `max_new_paper_trades_per_hour = 2`
- `max_paper_trades_per_day = 3`

#### `realistic_paper_test`

Use for realistic paper/simulation testing after validation.

- `max_active_signals_per_asset = 3`
- `max_total_active_signals = 10`
- `max_new_paper_trades_per_hour = 5`
- `max_paper_trades_per_day = 20`

#### `demo_candidate`

Use for candidate-level paper evaluation with broader activity.

- `max_active_signals_per_asset = 5`
- `max_total_active_signals = 15`
- `max_new_paper_trades_per_hour = 8`
- `max_paper_trades_per_day = 50`

#### `future_strategy_dependent`

Use for strategies whose safe operating limits are derived from validated evidence.

- no fixed default limits beyond global safety ceilings
- limits are derived from:
  - validated strategy characteristics
  - asset
  - timeframe
  - session behavior
  - forward-test frequency

## Global Safety Parameters

All profiles may be constrained by global safety parameters.

- `max_new_paper_trades_per_hour`
- `max_total_active_signals`
- `max_consecutive_losses`
- `max_daily_r_loss`
- `max_session_r_loss`
- `cooldown_after_loss_minutes`
- `cooldown_after_kill_switch_minutes`

These parameters are configurable and should default conservatively.

## Design Principles

- configurable, not hard-coded
- profile-based, not single-limit based
- conservative at the start
- scalable for realistic scalping frequency
- paper-only under all circumstances
- safety-first with explicit gating

## System A / System B Boundary

The cTrader Scalping Bot is not a research system.
It only consumes validated outputs from System A.

System A produces:
- certified signals
- confidence reports
- review outcomes
- backtest/OOS/forward evidence
- safety metadata

The bot consumes only validated signal artifacts and converts them into:
- paper or simulation decisions
- candidate tracking
- logging and observation support

## Allowed Inputs

The bot may consume:
- validated signal exports
- certified candidate packages
- safety flags
- confidence metadata
- forward-test observation metadata
- asset/timeframe/session descriptors

## Forbidden Capabilities

The bot must not:
- generate research hypotheses
- run backtests
- run OOS validation
- run forward tests as a research engine
- place orders
- access the cTrader Order API
- perform broker execution
- bypass human review

## Limit Interpretation

The following rules are examples of configurable safety controls, not fixed universal bot behavior:

- maximum active paper signals per asset is profile-dependent
- maximum paper trades per day is profile-dependent
- trade pacing is profile-dependent
- cooldown windows are profile-dependent
- loss limits are profile-dependent

## Default Safety Intent

The initial profile should favor:
- low churn
- low exposure
- easy reviewability
- conservative signal acceptance

The specification must remain compatible with higher-frequency paper activity where evidence supports it.

## Paper-Only Clarification

All increases in allowed activity affect only:
- simulation
- paper decisioning
- logging
- forward-test observation

They never unlock:
- broker orders
- demo orders
- live trading
- cTrader Order API

## Suggested Bot Contract Fields

Future bot exports and runtime configuration should expose:
- `safety_profile`
- `max_active_signals_per_asset`
- `max_total_active_signals`
- `max_new_paper_trades_per_hour`
- `max_paper_trades_per_day`
- `max_consecutive_losses`
- `max_daily_r_loss`
- `max_session_r_loss`
- `cooldown_after_loss_minutes`
- `cooldown_after_kill_switch_minutes`
- `no_auto_trading`
- `human_review_required`
- `broker_orders_enabled`
- `live_trading_enabled`
- `order_api_enabled`
- `paper_mode`

## Summary

The cTrader Scalping Bot Spec V1 defines a safe downstream consumer of validated System A signals.
It replaces rigid daily/signal caps with configurable safety profiles and global limits.
It remains paper-only and never authorizes any live or demo order path.
