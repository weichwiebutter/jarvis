# Trading Intelligence Focus

## System A Role

System A is the HermesRuntime Trading Intelligence engine.
It collects read-only cTrader/Fusion Markets market data, runs multi-asset and multi-timeframe scalping research, performs backtests, walkforward, OOS, Monte Carlo, and cost/slippage/spread validation, certifies candidates, and later produces cTrader bot specifications.
System A does not place orders and does not use the cTrader Order API.

## System B Role

System B is the Nous Hermes Agent on the Deutschland-PC.
It receives exported signal-agent packages from System A and displays signals for human review.
System B is a display/consumption target, not an execution engine.

## Data Flow System A -> System B

1. System A collects read-only live and historical market data from cTrader/Fusion Markets.
2. System A researches and certifies scalping candidates per asset, timeframe, and setup.
3. System A converts certified candidates into signal-agent package artifacts.
4. System A exports `ensemble_signal_agent_package.json`.
5. System B ingests the exported package and shows signals only.

## Persistent Role Of System A

System A remains active even after successful signal-agent or bot candidates.
A bot is not an end state.
A bot is an exported result from System A.
System A remains the learning and optimization instance behind it.

Persistent responsibilities of System A:
- collect new market data
- continue forward-testing existing strategies
- evaluate signal quality objectively
- downgrade weak candidates
- improve strong candidates
- combine multiple strong candidates into ensembles
- compare assets and timeframes
- detect market regimes
- improve confidence calibration
- check for overfitting
- feed demo and forward-test results back into learning
- update signal-agent packages regularly
- derive bot specifications only from stable, validated results

## Export Format

Primary export target: `ensemble_signal_agent_package.json`

Signals in the package should contain:
- `asset`
- `timeframe`
- `setup`
- `direction`
- `entry`
- `sl`
- `tp`
- `invalidation`
- `confidence`
- `status`
- `safety_flags`

## Safety Rules

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- read-only cTrader usage only
- no Broker orders
- no cTrader Order API
- real-money account remains blocked
- demo account may be used later for tests only

## Current Command Focus

- `current-market-snapshot`: read-only market snapshot for priority assets, including `GER40`
- `forward-test-status`: observation-only forward-test status for exported/demo signals
- `latest-demo-signals`: latest signal watch output for human review
- `latest-forward-test-observations`: latest read-only signal outcome observations
- `signal-agent-specs`: exported signal-agent candidate specs
- `scalping-bot-specs`: later-stage cTrader bot specs, still specification-only
- `scalping-multi-asset-roadmap`: asset/data/certification roadmap with `GER40/DE40` priority

## Next Priorities

1. `GER40/DE40` Daten integrieren
2. Signal Watch Engine V1
3. Forward Test Outcome Tracking
4. Multi-Asset Scalping Research
5. Ensemble Signal Agent Package Export
6. cTrader Bot Spec später, weiterhin ohne Orders
