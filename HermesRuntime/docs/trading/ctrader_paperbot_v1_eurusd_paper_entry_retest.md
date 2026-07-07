# cTrader PaperBot EURUSD Paper Entry Retest

Retest-Datum: 2026-07-07

## Ziel

Verifizieren, dass der exportierte cTrader PaperBot EURUSD nicht mehr wegen `paper_entry_disabled` blockiert.

## Export

- `HermesPaperBot_20260707_205019.algo`
- Export-Manifest: `D:\Bot\ctrader_export_manifest.json`

## Ergebnis

### EURUSD Explainability

EURUSD ist jetzt nicht mehr durch fehlende Confidence oder deaktivierte Paper Entry blockiert:

- `confidence=0.919`
- `confidence_source=embedded_confidence_baseline`
- `missing_confidence_fields=[]`
- `confidence_blockers=[]`
- `decision_reason=skipped_session`
- `lifecycle_state=waiting`
- `next_action=wait_for_allowed_session`

Damit ist der frühere Blocker `paper_entry_disabled` verschwunden.

### Runtime Step

Der aktuelle Runtime-Step ist grün und sicher:

- `runtime_ready=true`
- `embedded_package_loaded=true`
- `signal_package_loaded=true`
- `chart_annotation_spec_loaded=true`
- `safety_flags_active=true`
- `cloud_mode=true`
- `broker_action_none=true`
- `market_context_loaded=true`
- `market_symbol=EURUSD`
- `market_timeframe=M5`
- `market_spread_pips=0.1`

## Interpretation

Die Einbettung und das Mapping sind korrekt:

- Confidence ist vorhanden
- Safety ist aktiv
- Paper Entry ist nicht mehr disabled

Der verbleibende Zustand ist nun ein Session-Filter-Ergebnis:

- `skipped_session`
- `waiting`

Das ist ein fachlicher Laufzeitstatus, kein Confidence- oder Safety-Fehler.

## Safety

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `broker_action=none`

## Hinweis

Der Retest zeigt, dass der vorherige `paper_entry_disabled`-Blocker beseitigt wurde.  
Die nächste fachliche Hürde ist jetzt die erlaubte Session, nicht die Confidence-Metadaten.
