# HermesPaperBot

Paper-only skeleton for the first Hermes cTrader bot implementation.

## Scope

- Paper-only skeleton
- No orders
- No cTrader Order API
- No live trading
- No demo orders
- cTrader is only a consumer of a release bundle
- HermesRuntime is the release authority
- Bundle IO is local only
- No network access
- No broker access
- Only `paper_only` bundles are accepted

## Safety Invariants

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Reference Docs

- `docs/trading/ctrader_paper_bot_skeleton_spec_v1.md`
- `docs/trading/ctrader_bot_paper_runtime_scope_v1.md`
- `docs/trading/ctrader_bot_configuration_model_v1.md`
- `docs/trading/ctrader_bot_bundle_import_model_v1.md`
- `docs/trading/ctrader_bot_drift_check_model_v1.md`

## Notes

This directory is a guarded skeleton only.
It intentionally contains no project file, no implementation logic, and no trading capabilities.
Bundle import is local filesystem IO only and never reaches the cTrader or broker APIs.

## Safety Guard

Run the forbidden reference guard before reviewing any later implementation work:

```bash
bash scripts/check_ctrader_paper_bot_forbidden_refs.sh
```

## Preflight

Run the manual preflight before any later cTrader paper bot change:

```bash
bash scripts/preflight_ctrader_paper_bot.sh
```

## Runtime Orchestrator V1

The paper runtime now uses a defensive orchestrator step:

- validate configuration
- import a local bundle
- validate manifest, provenance, checksums, safety, and drift
- evaluate the kill switch
- produce a paper-only decision placeholder

This remains paper-only.
No broker access.
No cTrader Order API.
No live or demo execution.

## In-Memory Harness

The paper runtime can be checked with an in-memory harness in `ctrader/HermesPaperBot/tests/PaperRuntimeOrchestratorHarness.cs`.

- no cTrader runtime
- no broker
- no orders
- only orchestrator safety and validation
- intended for temporary scratch compilation and JSON/text output

## Runtime Logging V1

Local runtime logs are written as JSONL/JSON only.

- append-only JSONL for step logs and kill-switch events
- JSON summary for the current runtime state
- local filesystem only
- no broker action
- no cTrader API
