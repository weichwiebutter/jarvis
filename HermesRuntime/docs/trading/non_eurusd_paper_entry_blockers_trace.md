# GER40 / XAUUSD Paper Entry Blocker Trace

## Ergebnis

Beide Signale sind weiterhin `invalidated`, weil das Embedded Package für diese Assets **explizit `paper_entry_enabled=false`** exportiert und die Entry-/SL-/TP-Felder nicht vollständig befüllt sind.

## GER40

Aktueller `paper_signal_evaluation`-Eintrag:

- `confidence_baseline = 0.8046`
- `paper_entry_enabled = false`
- `signal_status = invalidated`
- `signal_lifecycle_status = invalidated`
- `paper_decision = would_wait`
- `reason = paper_entry_disabled`
- `session_allowed = true`
- `spread_allowed = true`
- `safety_allowed = true`
- `market_context_compatible = false`
- `signal_expired = false`
- `signal_invalidated = true`
- `warnings = [paper_entry_disabled]`

### Fehlende/leer exportierte Felder

- `entry_price = null`
- `stop_loss_price = null`
- `take_profit_1 = null`
- `take_profit_2 = null`
- `invalidation_level = null`
- `risk_reward = null`

### Einordnung

- **Confidence vorhanden:** ja
- **paper_entry_enabled explizit false:** ja
- **Safety-Grund:** nein
- **Missing Entry/SL/TP:** ja
- **Mapping/Annotation vorhanden:** ja, der Signal-Asset-Eintrag ist im Embedded Package vorhanden

## XAUUSD

Aktueller `paper_signal_evaluation`-Eintrag:

- `confidence_baseline = 0.9374`
- `paper_entry_enabled = false`
- `signal_status = invalidated`
- `signal_lifecycle_status = invalidated`
- `paper_decision = would_wait`
- `reason = paper_entry_disabled`
- `session_allowed = true`
- `spread_allowed = true`
- `safety_allowed = true`
- `market_context_compatible = false`
- `signal_expired = false`
- `signal_invalidated = true`
- `warnings = [paper_entry_disabled]`

### Fehlende/leer exportierte Felder

- `entry_price = null`
- `stop_loss_price = null`
- `take_profit_1 = null`
- `take_profit_2 = null`
- `invalidation_level = null`
- `risk_reward = null`

### Einordnung

- **Confidence vorhanden:** ja
- **paper_entry_enabled explizit false:** ja
- **Safety-Grund:** nein
- **Missing Entry/SL/TP:** ja
- **Mapping/Annotation vorhanden:** ja, der Signal-Asset-Eintrag ist im Embedded Package vorhanden

## Schlussfolgerung

Der Blocker ist **kein Safety-Blocker** und auch kein Session-Blocker.

Die Ursache ist:

1. `paper_entry_enabled=false` wird für GER40 und XAUUSD explizit exportiert.
2. Die Preisfelder sind für beide Assets noch nicht als vollständiger Paper-Entry-Datensatz befüllt.
3. Daher bleibt die Entscheidung `paper_entry_disabled` und der Status `invalidated`.

