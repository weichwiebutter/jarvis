# cTrader PaperBot Confidence Metadata Retest

Retest-Datum: 2026-07-07

## Ziel

Verifizieren, dass der exportierte cTrader PaperBot nach dem Confidence-Metadata-Fix nicht mehr mit fehlenden EURUSD-Confidence-Werten läuft.

## Verwendeter Export

- `HermesPaperBot_20260707_202043.algo`
- Export-Manifest: `D:\Bot\ctrader_export_manifest.json`

## Beobachtungen

### OnTimer / Runtime Status

Der aktuelle PaperBot-Runtime-Status ist bereit und sicher:

- `signal_count=3`
- `package_loaded=true`
- `signal_package_loaded=true`
- `chart_annotation_spec_loaded=true`
- `broker_action=none`
- `kill_switch_active=false` *(nicht als eigenes Feld im aktuellen JSON ausgegeben; aus dem sicheren Runtime-Status und `broker_action=none` konsistent)*

### PaperBot Runtime Self Check

Letzter Runtime-Selbsttest ist grün:

- `embedded_release_package_parseable=true`
- `signal_package_loaded=true`
- `chart_annotation_spec_loaded=true`
- `safety_flags_active=true`
- `cloud_mode=true`
- `broker_action_none=true`
- `runtime_ready=true`

### Signal Explainability

Der EURUSD-Signalpfad zeigt jetzt keine fehlenden Confidence-Metadaten mehr:

- `confidence_source=embedded_confidence_baseline`
- `confidence=0.919`
- `confidence_threshold=0.6`
- `missing_confidence_fields=[]`
- `confidence_blockers=["paper_entry_disabled"]`
- `decision_reason=paper_entry_disabled`
- `lifecycle_state=invalidated`

### Paper Runtime Step

Aktueller Report zeigt weiterhin einen sicheren, nicht-tradenden Zustand:

- `market_context_loaded=true`
- `broker_action=none`
- `paper_decision_summary=evaluated=1; actionable=1; waiting=0; watching=0; would_trigger=1; active=0; completed=1; invalidated=0; expired=0; skipped=0`

## Ergebnis

Der Confidence-Metadaten-Fix ist im exportierten Paket sichtbar.  
EURUSD läuft nicht mehr mit `actual_confidence=0`; die Confidence stammt aus dem eingebetteten Signal-Asset und ist im Explain-Report nachvollziehbar.

## Safety

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `broker_action=none`

## Hinweis

Die verbleibende Blockierung ist fachlich nicht mehr ein fehlender Confidence-Wert, sondern der vorhandene Paper-Entry-Schutz (`paper_entry_disabled`).
