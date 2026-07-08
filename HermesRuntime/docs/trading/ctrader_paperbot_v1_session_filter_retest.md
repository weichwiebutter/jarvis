# cTrader PaperBot Session Filter Retest

## Retest Summary

Der aktuelle Re-Export wurde erzeugt und der PaperBot-Lauf zeigt für EURUSD keinen Session-Blocker mehr.

## Export

- Export-Datei: `HermesPaperBot_20260708_073744.algo`
- Latest-Datei: `HermesPaperBot_latest.algo`
- Build Stamp: `20260707_timer_diag_v2`

## Runtime / OnTimer Status

Aktueller `paper_runtime_step`-Stand:

- `status = ready`
- `market_context_source = cTrader_read_only_quote`
- `market_symbol = EURUSD`
- `market_timeframe = M5`
- `market_context_loaded = true`
- `embedded_package_loaded = true`
- `signal_package_loaded = true`
- `chart_annotation_spec_loaded = true`
- `safety_flags_active = true`
- `broker_action_none = true`

## Signal Explain Status

Der letzte `paper_signal_explain`-Report zeigt für EURUSD:

- `session_allowed = true`
- `decision_reason = would_trigger`
- `next_action = monitor_for_trigger`
- `lifecycle_state = watching`

Damit ist `skipped_session` für EURUSD im aktuellen Runtime-Pfad nicht mehr aktiv.

## Safety

Weiterhin aktiv:

- `no_auto_trading = true`
- `human_review_required = true`
- `broker_orders_enabled = false`
- `live_trading_enabled = false`
- `research_only = true`
- `broker_action = none`

## Ergebnis

Retest PASS für den Session-Filter-Pfad:

- kein `skipped_session`
- kein Safety-Block
- EURUSD ist auf `watching` / `would_trigger`-Pfad

