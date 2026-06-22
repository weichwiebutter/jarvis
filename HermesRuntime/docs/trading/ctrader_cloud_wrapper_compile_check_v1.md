# cTrader Cloud Wrapper Compile Check V1

This checklist documents the first real cTrader Cloud compile test for HermesPaperBot.

## Target file

- `ctrader/HermesPaperBot/HermesPaperBotCTraderWrapper.cs`

## Build symbol

- `HERMES_CTRADER_WRAPPER`

## Expected access rights

- `AccessRights.None`

## Allowed API surface for the wrapper

- `Robot`
- `OnStart`
- `OnTimer`
- `OnStop`
- `OnException`
- `Timer`
- `Print`
- `SymbolName`
- `Symbol.Bid`
- `Symbol.Ask`
- `Server.Time`

## Forbidden API surface

- account-related APIs
- position-related APIs
- pending-order APIs
- order placement APIs
- trade result/operation APIs
- volume APIs

## Known SDK adjustment

- `Bars.TimeFrame` may need replacement depending on the exact SDK version

## Expected runtime behavior

- start print appears
- timer print appears
- `broker_action=none`
- no orders
- a high spread only blocks the paper decision

## Troubleshooting

- missing `HERMES_CTRADER_WRAPPER` build symbol means only the local stub compiles
- missing cAlgo namespace means the real wrapper branch is not compiled in the local environment
- if `Bars.TimeFrame` is unavailable, the wrapper must be adapted in the cTrader environment

## Safety invariants

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`
