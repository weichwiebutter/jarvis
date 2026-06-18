# cTrader Paper Bot V1 Implementation Plan

## Scope

This plan defines the first implementation structure for a future cTrader Paper Bot V1.

It is not an implementation.
It does not contain cBot code.
It does not use the cTrader Order API.
It does not place broker, demo, or live orders.

The bot will later:
- load `ensemble_signal_agent_package.json` read-only
- validate the package
- evaluate paper/simulation decisions
- write logs

## Reference Architecture

This plan follows the architecture defined in:
- `HermesRuntime/docs/trading/ctrader_paper_bot_v1_architecture.md`

## 1. Proposed Project Structure

Suggested future cBot folder layout:

```text
ctrader/HermesPaperBot/
├── HermesPaperBot.cs
├── SignalPackageLoader.cs
├── SignalPackageValidator.cs
├── PaperDecisionEngine.cs
├── SafetyGate.cs
├── SessionFilter.cs
├── SpreadFilter.cs
├── KillSwitch.cs
├── PaperLogger.cs
├── Models/
│   ├── SignalPackage.cs
│   ├── SignalPackageSafetyFlags.cs
│   ├── PaperDecision.cs
│   ├── BotStatusSummary.cs
│   └── ValidationResult.cs
```

This is a planning structure only.

## 2. cBot Lifecycle Mapping

### `OnStart()`

Responsibilities:
- read parameters
- initialize paths
- validate safety configuration
- load the initial signal package
- cache `last_valid_package` if the package is valid
- start timer-based reload checks
- emit startup status

### `OnTimer()`

Responsibilities:
- check for package reloads
- validate the current package file
- compare `package_version`
- compare `generated_at`
- update `last_valid_package` only if the new file is valid
- check market session and spread state
- evaluate the paper decision
- write logs

### `OnTick()`

Responsibilities:
- optional lightweight price-context refresh
- update cached spread or symbol state if needed

Constraints:
- no heavy validation
- no reload orchestration
- no trading operations

### `OnBar()`

Responsibilities:
- optional timeframe-aligned refresh
- optional state snapshot update

Use only if bar cadence is preferable for the selected strategy.

### `OnStop()`

Responsibilities:
- write final summary
- flush logs if needed
- record clean shutdown state

## 3. Package Hot-Swap Plan

### Configurable Inputs

- `signal_package_path`
- `reload_interval_seconds`
- `package_expiry_minutes`
- `validation_strictness`

### Runtime State

- `last_valid_package`
- `last_valid_package_version`
- `last_valid_package_generated_at`
- `last_reload_at`
- `kill_switch_active`

### Reload Rules

When a reload check runs:
- read the current file from `signal_package_path`
- validate schema and required fields
- compare `package_version`
- compare `generated_at`
- accept only fully valid packages
- keep the previous `last_valid_package` if the new file fails validation

### File Missing

If the file does not exist:
- set `bot_status = waiting_for_signal_package`
- continue using `last_valid_package` if available
- if no valid package exists, set `bot_status = disabled_until_valid_package`

### Invalid File

If the file exists but is invalid:
- set `bot_status = package_rejected_using_last_valid`
- keep `last_valid_package`
- log the rejection

### Expired Package

If the package is older than the configured expiry:
- set `bot_status = package_expired`
- do not generate a new paper decision from the expired package
- continue only if policy allows safe fallback to `last_valid_package`

### Safety Flag Violation

If any required safety flag is invalid:
- activate `kill_switch_active = true`
- reject the package
- stop paper decision generation
- log the kill-switch event

## 4. Safety Gate

Every package must satisfy:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`

If any flag is violated:
- `kill_switch_active = true`
- no paper decision
- write a kill-switch log entry

## 5. Forbidden API Surfaces

The V1 code must not contain references to:
- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`
- `CancelPendingOrder`
- `Positions.Modify`
- `PendingOrders`
- trading operations APIs

If execution is introduced later, it must be isolated behind a separate `ExecutionAdapter` layer.

## 6. AccessRights

Recommended for V1:
- `AccessRights.FileSystem`

Not recommended for V1:
- `AccessRights.Internet`
- `AccessRights.FullAccess`, unless later proven strictly necessary

Reason:
- local JSON read/write is the minimum required capability
- internet access is unnecessary
- broader rights increase risk and reduce sandbox isolation

## 7. Paper Decision Engine

The engine may produce only simulated decisions:
- `would_wait`
- `would_enter_long`
- `would_enter_short`
- `would_skip`
- `would_invalidate`
- `would_expire`
- `would_block_by_safety`

Every decision must include:
- `broker_action = none`
- no execution intent
- no trading side effects

The engine should consider:
- validated signal package content
- session filter
- spread filter
- safety gate
- expiry state

## 8. Logging Plan

Planned append-only logs:
- `package_reload_log.jsonl`
- `package_validation_log.jsonl`
- `paper_decision_log.jsonl`
- `kill_switch_events.jsonl`
- `bot_status_summary.json`

Each log should record:
- timestamp
- package id or version
- validation outcome
- status transitions
- paper decision result
- safety state
- kill switch state

## 9. Test Plan

Planned manual validation cases:

1. Load a valid signal package
2. Swap the package file atomically
3. Insert an invalid package
4. Deliberately violate a safety flag
5. Delete the package file
6. Let the package expire
7. Simulate spread too high
8. Simulate blocked session conditions
9. Confirm no order API references exist in the implementation

Expected outcomes:
- valid package accepted
- invalid package rejected and fallback used
- safety violation triggers kill switch
- missing package enters waiting state
- expiry handled without executing orders
- spread/session filters affect paper decision only

## 10. Implementation Notes

### Separation of Concerns

- `SignalPackageLoader` handles file access only
- `SignalPackageValidator` handles schema and safety checks only
- `SafetyGate` evaluates mandatory safety flags
- `SessionFilter` and `SpreadFilter` evaluate market context
- `PaperDecisionEngine` produces simulated decisions only
- `KillSwitch` owns emergency stop state
- `PaperLogger` writes append-only logs

### Package Handling

The package must remain replaceable without rebuilding the bot.
That means the bot should not hardcode package content, trading rules, or asset-specific execution logic.

## 11. Open Technical Questions

- Should reload polling run on `OnTimer()` only, or also on `OnTick()`?
- Should package expiry be measured from `generated_at` or package file timestamp?
- Should `last_valid_package` survive bot restarts via local storage?
- Where should logs be stored relative to the cTrader algo workspace?
- How strict should schema migration be across future package versions?
- Should paper decisions be bar-based, tick-based, or timer-based for V1?

## 12. Non-Goals

This V1 plan explicitly excludes:
- cBot implementation
- cTrader project files
- order execution
- demo orders
- live orders
- broker actions
- internet access
- secrets handling
- remote API integration

## Summary

The V1 implementation should be a local read-only cTrader Paper Bot that consumes validated System A signal packages, maintains a last-valid fallback, writes logs, and never reaches any execution path.
