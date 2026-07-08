# External Bot Parameter Research Note

## Purpose

This note records parameters observed in an external cBot as a **research hypothesis only**.
They are **not** adopted into Hermes trading logic, order logic, or runtime behavior.

## Observed Parameters

- Fast EMA: 8
- Slow EMA: 84
- COG Filter
- RSI: 14
- Margin control
- Max simultaneous positions: 2

## Evaluation

These parameters are treated as a possible filter combination for future research, backtesting, or controlled mutation studies.

### Important constraints

- Research only
- No direct adoption
- No order logic changes
- No broker or trading execution
- No automatic promotion of this parameter set

## Suggested next research use

If this parameter set is explored further, it should be evaluated only in:

- backtest experiments
- mutation/optimization candidates
- guarded research comparisons against existing strategies

## Safety

- no_auto_trading = true
- human_review_required = true
- broker_orders_enabled = false
- live_trading_enabled = false
- broker_action = none
