# Forbidden References Check

The later codebase must not contain references to:

- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`
- `CancelPendingOrder`
- `PendingOrders`
- `Positions.Modify`
- `TradeResult`
- `TradeOperation`

If any forbidden reference is found:

- build/review is blocked
- `release_status=blocked_forbidden_reference`
- `broker_action=none`

This is a policy document only.
