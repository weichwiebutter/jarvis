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

## Safety Guard

Run the forbidden reference guard before reviewing any later implementation work:

```bash
bash scripts/check_ctrader_paper_bot_forbidden_refs.sh
```
