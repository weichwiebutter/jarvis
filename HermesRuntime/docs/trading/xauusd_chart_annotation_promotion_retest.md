# XAUUSD Chart Annotation Promotion Retest

## Context
- promotion_decision: approved
- promoted_to_embedded: true
- reviewer: Frank
- review_timestamp_utc: 2026-07-08T08:40:15.9532837+00:00

## Export Retest
- cloud_embedded_release_package: regenerated successfully
- ctrader_export: regenerated successfully
- build_stamp: 20260707_timer_diag_v2

## cTrader Paper Signal Explain
- report: /mnt/d/HermesData/reports/paper_signal_explain/paper_signal_explain.json
- markdown: /mnt/d/HermesData/reports/paper_signal_explain/paper_signal_explain.md
- explained_signals: 3

### XAUUSD
- signal_id: ensemble_signal_agent_package_20260611052025:XAUUSD:scalp_xauusd_1070720f16
- confidence: 0.896
- confidence_threshold: 0.6
- confidence_source: embedded_confidence_baseline
- session_allowed: false
- spread_allowed: true
- entry_condition_met: false
- stop_loss_ready: true
- take_profit_ready: true
- decision_reason: session_not_allowed:other
- lifecycle_state: waiting
- next_action: wait_for_allowed_session
- broker_action: none

## Result
- xauusd_promoted_to_embedded: true
- xauusd_visible_in_export: true
- xauusd_paper_entry_enabled: not confirmed in explain output because session filter still blocked entry
- runtime_status: session-gated waiting

## Notes
- Promotion was applied successfully to the review artifact and embedded package.
- The current cTrader explain path still classifies XAUUSD as `session_not_allowed:other`.
- This retest documents the post-promotion export state; it does not change session logic.
