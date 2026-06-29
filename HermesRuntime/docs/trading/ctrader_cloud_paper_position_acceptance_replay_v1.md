# cTrader Cloud Paper Position Acceptance Replay V1

## Ziel
Nachweisen, dass die Cloud-Paper-Position-State-Kette vollständig funktioniert:

`Embedded Signal -> Paper Entry -> Hold -> TP/SL/Expiry -> Log/Summary`

Die Ausführung bleibt dabei strikt paper-only.

## In-Scope

- Embedded Signal lesen
- Paper-Entry für Long/Short
- Hold über aufeinanderfolgende Runtime-Steps
- virtuelles Schließen bei TP/SL/Expiry
- Restore einer offenen Paper-Position
- Logging und Summary-Updates
- Harness-basierte Prüfung der Abläufe

## Out-of-Scope

- ExecuteMarketOrder
- PlaceLimitOrder
- PlaceStopOrder
- ModifyPosition
- ClosePosition
- CancelPendingOrder
- Positions
- PendingOrders
- Account
- TradeResult
- TradeOperation
- echte cTrader-Cloud-Kompilierung
- echte Broker-/Demo-/Live-Ausführung

## Safety-Invariants

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Replay-Szenarien

- `long_entry_then_hold`
- `long_entry_then_take_profit`
- `long_entry_then_stop_loss`
- `short_entry_then_hold`
- `short_entry_then_take_profit`
- `short_entry_then_stop_loss`
- `expired_position_closes`
- `missing_sl_tp_blocks_entry`
- `low_confidence_blocks_entry`
- `spread_too_high_blocks_entry`
- `restore_open_position_then_close`

## Akzeptanzkriterien

- Alle Szenarien laufen im Harness ohne Broker-Aktion.
- Alle Outputs enthalten `broker_action=none`.
- Open-, Hold- und Close-Zustände werden korrekt dargestellt.
- Restore einer offenen Position funktioniert defensiv.
- Logs und Summary bleiben konsistent.

## Erwartete Outputs

- `PaperDecision`
- `PaperPositionOpen`
- `PaperPositionStatus`
- `PaperExitReason`
- `RMultiple`
- `PositionId`
- `SignalSeen`
- `SignalDirection`
- `SignalConfidence`
- `SignalExpired`
- `broker_action=none`

## Bekannte Grenzen

- Das ist ein Harness-/Core-Nachweis.
- Es ist noch kein echter cTrader Cloud SDK Compile.
- Ein echter Compile-Test muss später in cTrader Algo erfolgen.
- Die Dokumentation ersetzt keinen Plattformtest.

## Echter cTrader-Compile bleibt separat

Die finale Verifikation für den Cloud-Wrapper bleibt ein eigenständiger Schritt in der echten cTrader-/cAlgo-Umgebung.
Diese Acceptance Replay V1 belegt nur, dass die HermesPaperBot-Core-Kette und die Safety-Logik korrekt zusammenspielen.
