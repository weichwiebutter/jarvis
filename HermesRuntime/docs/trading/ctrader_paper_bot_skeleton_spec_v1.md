# cTrader Paper Bot Skeleton Spec V1

## Purpose

This document defines the proposed skeleton for the first Hermes cTrader Paper Bot implementation.

It is specification only.
It does not define cBot code.
It does not use the cTrader Order API.
It does not allow trading operations.
It does not permit demo or live execution.

## Proposed Future Project Structure

```text
ctrader/HermesPaperBot/
├── HermesPaperBot.cs
├── Models/
│   ├── BotConfiguration.cs
│   ├── ReleaseBundleManifest.cs
│   ├── ProvenanceInfo.cs
│   ├── ChecksumEntry.cs
│   ├── PaperDecision.cs
│   ├── BotState.cs
│   └── RuntimeMarketContext.cs
└── Services/
    ├── ConfigurationValidator.cs
    ├── ReleaseBundleImporter.cs
    ├── ReleaseBundleValidator.cs
    ├── ChecksumValidator.cs
    ├── DriftGuard.cs
    ├── SafetyGate.cs
    ├── MarketContextReader.cs
    ├── SessionFilter.cs
    ├── SpreadFilter.cs
    ├── PaperDecisionEngine.cs
    ├── KillSwitch.cs
    ├── PaperLogger.cs
    └── RuntimeSummaryWriter.cs
```

## Main cBot Responsibility

`HermesPaperBot.cs` must only orchestrate lifecycle events later:

- `OnStart`
- `OnTimer`
- `OnTick`
- optional `OnBar`
- `OnStop`
- `OnException`

No business logic should live directly in the main cBot class.

## Service Responsibilities

### `ConfigurationValidator.cs`

- Purpose: validate local bot configuration
- Inputs: `BotConfiguration`
- Outputs: validation result, errors
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `ReleaseBundleImporter.cs`

- Purpose: detect, read, and activate a release bundle from the inbox
- Inputs: bundle path, local runtime state
- Outputs: active bundle candidate, import status
- may write?: yes, only local runtime state and logs
- may modify bundle artifacts?: no
- may use Order API?: no

### `ReleaseBundleValidator.cs`

- Purpose: validate bundle structure, release mode, and content consistency
- Inputs: manifest, provenance, bundle files
- Outputs: validation result
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `ChecksumValidator.cs`

- Purpose: verify full-bundle checksums
- Inputs: `checksums.json`, bundle files
- Outputs: checksum validation result
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `DriftGuard.cs`

- Purpose: assess drift against the validated HermesRuntime strategy
- Inputs: manifest, strategy identity, mapping data
- Outputs: drift severity, blocking state
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `SafetyGate.cs`

- Purpose: enforce mandatory safety flags and kill-switch rules
- Inputs: bundle safety flags, local config, runtime conditions
- Outputs: safety status, allow/deny decision
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `MarketContextReader.cs`

- Purpose: read current market context only
- Inputs: symbol, timeframe, platform runtime values
- Outputs: `RuntimeMarketContext`
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `SessionFilter.cs`

- Purpose: determine whether the current market session is allowed
- Inputs: market context, bundle/session rules
- Outputs: session allow/deny result
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `SpreadFilter.cs`

- Purpose: determine whether spread is acceptable
- Inputs: current spread, bundle rules, local overrides
- Outputs: spread allow/deny result
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `PaperDecisionEngine.cs`

- Purpose: compute paper-only decisions from validated inputs
- Inputs: active bundle, market context, filters, safety state
- Outputs: `PaperDecision`
- may write?: no
- may modify bundle artifacts?: no
- may use Order API?: no

### `KillSwitch.cs`

- Purpose: set and maintain kill-switch state
- Inputs: invalid config, safety failures, drift, exceptions
- Outputs: kill-switch state
- may write?: yes, only local runtime logs/state
- may modify bundle artifacts?: no
- may use Order API?: no

### `PaperLogger.cs`

- Purpose: write paper runtime logs
- Inputs: decisions, transitions, errors, observations
- Outputs: log entries
- may write?: yes
- may modify bundle artifacts?: no
- may use Order API?: no

### `RuntimeSummaryWriter.cs`

- Purpose: write a compact runtime summary
- Inputs: current state, decisions, errors, bundle status
- Outputs: summary artifact
- may write?: yes
- may modify bundle artifacts?: no
- may use Order API?: no

## Dependency Direction

Dependency direction must remain:

```text
HermesPaperBot
→ Services
→ Models
```

Services must not have cyclic dependencies.

## Forbidden References

The future codebase must not contain references to:

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

## Minimal Paper Runtime Loop

### Pseudoflow

`OnTimer` should later perform:

1. validate config
2. check import
3. validate bundle
4. enforce safety
5. read market context
6. run filters
7. compute paper decision
8. write logs
9. update summary

## Logging Responsibility

Only the following components may write logs:

- `PaperLogger`
- `RuntimeSummaryWriter`

Bundle artifacts must remain read-only.

## Testability

The services should later be isolated and testable with cases such as:

- bundle validator with a valid bundle
- bundle validator with checksum mismatch
- safety gate with a wrong flag
- paper decision engine with spread block
- kill switch on invalid config

## Safety Invariants

These values remain mandatory:

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Open Implementation Questions

- Should `KillSwitch` own the persisted state file or only the in-memory state?
- Should `RuntimeSummaryWriter` emit on every timer cycle or only on changes?
- Should filter services return booleans only, or richer decision objects?
- Should `DriftGuard` be run before or after full bundle activation?
- Should the main cBot class keep a single runtime state object or delegate all state to services?

## Summary

V1 is a thin orchestration shell around isolated services and read-only models.

The main cBot class must stay small, and all logging, validation, drift checks, and paper decisions must remain service-driven and paper-only.
