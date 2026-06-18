# cTrader Paper Bot V1 Architecture

## Goal

This document defines the first architecture specification for a future cTrader cBot/Algo that consumes validated System A signal packages in paper/simulation mode only.

It is not a trading implementation.
It is not an order system.
It is not a live or demo execution design.

## Architecture Decision

V1 should run as a cTrader cBot/Algo and load the signal package from a local read-only JSON file.

Primary signal source:
- `ensemble_signal_agent_package.json`

The package must remain replaceable at any time without requiring a bot rebuild.

## Why Local JSON Instead of API

V1 intentionally avoids local HTTP APIs and remote APIs.

Reasons:
- cTrader cBot already has direct access to symbol, price, spread, and session context
- HermesRuntime remains a separate research system
- a JSON file is simpler and safer to swap
- no network or API complexity
- no extra order attack surface
- easier validation and rollback

## System Boundary

### System A / HermesRuntime

System A:
- generates `ensemble_signal_agent_package.json`
- validates and certifies signal content
- writes new versions atomically
- never trades directly through cTrader

### cTrader Paper Bot

The bot:
- reads `ensemble_signal_agent_package.json` read-only
- validates schema, version, and safety flags
- keeps `last_valid_package` if a new file is invalid
- simulates paper decisions
- writes logs
- never submits orders

## Hot-Swap Concept

The signal package path must be configurable:
- `signal_package_path`

The bot periodically checks the file, for example every X seconds.
It compares:
- `package_version`
- `generated_at`

The bot only adopts a new package when it is fully valid.
If the file is invalid, the bot continues using `last_valid_package`.

## Atomic Package Replacement

HermesRuntime should use an atomic file replacement flow:

1. Write `ensemble_signal_agent_package.json.tmp`
2. Validate the package internally
3. Replace the live file atomically with `ensemble_signal_agent_package.json`

This keeps the file replaceable without requiring bot restarts or rebuilds.

## Required Validation Rules

The bot must reject any package that is missing or invalid in the following fields:
- `schema_version`
- `generated_at`
- `package_id`
- `source_system`
- `safety_flags`

The following safety conditions must be true:
- `source_system == HermesRuntime/SystemA`
- `broker_orders_enabled == false`
- `live_trading_enabled == false`
- `order_api_enabled == false`
- `no_auto_trading == true`
- `human_review_required == true`
- `paper_mode == true`

If any required safety condition is violated, the bot must not use the package.

## Fallback Rules

- file missing: `bot_status = waiting_for_signal_package`
- file invalid: `bot_status = package_rejected_using_last_valid`
- no `last_valid_package`: `bot_status = disabled_until_valid_package`
- package expired: `bot_status = package_expired`
- safety flag violated: `kill_switch_active = true`

## Kill Switch Rules

The kill switch must activate when:
- a required safety flag is violated
- the package is structurally invalid in a way that makes safe paper evaluation impossible
- the bot detects an unsafe or inconsistent package state

Kill switch behavior:
- stop adopting new packages
- retain the last valid package only for safe read-only reference
- log the kill switch event
- never place orders

## Logging

The architecture should support these append-only logs:
- `package_reload_log.jsonl`
- `package_validation_log.jsonl`
- `paper_decision_log.jsonl`
- `kill_switch_events.jsonl`

Logs should record:
- timestamps
- package identifiers
- validation outcome
- reason for acceptance or rejection
- fallback state
- kill switch events

## Paper-Only Operation

The bot may only:
- consume validated signal packages
- evaluate paper or simulation decisions
- log state transitions
- observe price and spread context

The bot must never:
- place broker orders
- place demo orders
- use the cTrader Order API
- enable live trading
- enable any hidden execution path

## Required Safety Flags

These flags must remain true everywhere in V1:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`

## Package Compatibility Requirements

The bot architecture should tolerate package evolution without rebuilds by requiring:
- explicit schema versioning
- explicit package versioning
- explicit generated timestamp
- explicit source system declaration
- explicit safety flags

## Open Points For Later Demo-Bot Phase

Future work may define:
- exact cTrader bot project structure
- UI for selected package state
- package expiration policy details
- stricter schema migration rules
- paper decision scoring policy
- forward-observation integration

Future work must still preserve:
- no broker orders
- no live trading
- no cTrader Order API
- no demo order automation without explicit approval

## Summary

V1 is a local read-only cTrader cBot/Algo consumer of `ensemble_signal_agent_package.json`.
It uses a safe hot-swap file workflow, validates every package, falls back to the last valid package, and never enables any order path.

## Official cTrader Algo Documentation Alignment

This section aligns the V1 architecture with the official cTrader Algo documentation as the reference basis for later implementation.

### cBot Lifecycle

Official cTrader lifecycle hooks documented for cBots include:
- `OnStart()`
- `OnTick()`
- `OnStop()`
- `OnBar()`
- `OnException()`

V1 architecture mapping:
- `OnStart()` initializes the bot, loads the first package, validates the schema, and records the initial package state.
- `OnTick()` or a timer-based check may be used for reload polling.
- `OnBar()` is optional for V1 and only relevant if reload checks should align with bar boundaries.
- `OnStop()` writes final runtime state and closes logs.
- `OnException()` records errors and can activate the kill switch if the package or runtime state becomes unsafe.

### cBot Parameters

The bot should expose configurable parameters for:
- `signal_package_path`
- reload interval
- package expiration window
- validation strictness
- log destination
- optional paper decision mode settings

The parameter model should be minimal and stable so the signal package can change without a rebuild.

### AccessRights

Official cTrader documentation defines:
- `AccessRights.None`
- `AccessRights.FileSystem`
- `AccessRights.Internet`

For V1 the recommended target is:
- `AccessRights.FileSystem`

Reason:
- V1 needs minimal local file access for the read-only JSON package and logs.
- Internet access is not required.
- `AccessRights.None` is likely too restrictive if the bot must read and write local logs or the package file location.
- `AccessRights.FullAccess` should be avoided if FileSystem is sufficient.

### Local File Operations

Official cTrader file guidance confirms that algorithms can access local files without elevated permissions and that local storage is available independently of access rights.

V1 file model:
- read `ensemble_signal_agent_package.json` read-only
- optionally write logs in a restricted local folder
- do not rely on network or remote storage

Recommended file behavior:
- minimal read access to the package path
- minimal write access only for append-only logs
- no general filesystem traversal

### Sandboxed File Access

cTrader runs algos in a sandboxed environment and requires declared access rights.

V1 implication:
- keep file access as narrow as possible
- prefer a single configurable package path and a fixed log directory
- avoid any need for broader filesystem permissions

### JSON Dateizugriff

The package is expected to be JSON-based and read-only from the bot side.

V1 expectations:
- parse `ensemble_signal_agent_package.json`
- validate schema and safety flags
- reject partial or malformed content
- retain `last_valid_package` if the current file fails validation

### Timer / Tick / Bar Reload Checks

Official cTrader lifecycle and timer documentation support periodic execution patterns.

V1 recommendation:
- prefer `OnTick()` only if reload polling should be tied to market activity
- prefer timer-based checks if reload polling should be decoupled from tick volume
- `OnBar()` is acceptable only if reload cadence should align with bar formation

Recommended V1 default:
- timer-based reload checks or low-frequency tick checks
- no heavy processing on every tick

### Logging Möglichkeiten

Official cTrader docs show logging via `Print()` and related notification/logging mechanisms.

V1 logging model:
- package reload log
- package validation log
- paper decision log
- kill switch event log

Logging should be read-only for trading behavior and append-only for traceability.

### Abgrenzung zu Trading Operations

Official cTrader trading operation documentation lists:
- market orders
- pending orders
- modifying pending orders and open positions
- closing positions and canceling orders

V1 explicitly knows these APIs but does not use them.

The future execution path must be isolated behind a dedicated Execution Adapter layer.
Only that adapter may ever call:
- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`

The cTrader Paper Bot V1 itself must never call these methods.

The bot may only:
- consume validated JSON packages
- evaluate paper/simulation decisions
- write logs
- observe symbol/price/spread context

It must not:
- send market orders
- place pending orders
- modify positions
- close positions
- cancel orders
- subscribe to execution behavior for trading

### Execution Adapter Boundary

The architecture must keep execution separate from the paper consumer.

Requirements:
- the Paper Bot V1 must not directly reference order execution methods
- the future Execution Adapter is the only component allowed to invoke them
- adapter activation remains a later-stage decision
- the adapter must preserve the same safety flags and review controls

This keeps the paper consumer auditable and prevents execution logic from leaking into V1.

## cTrader Documentation Assumptions For V1

- cBot lifecycle hooks are available as documented.
- parameterization is available through cBot parameters.
- sandboxed file access requires declared access rights.
- local file read/write is possible with the right declared access.
- timer or tick-based reload checks are available.
- logging is possible through standard cTrader log mechanisms.

## Recommended AccessRights For V1

Preferred:
- `AccessRights.FileSystem`

Only if later implementation proves it impossible to keep the package and logs within controlled local file access:
- reevaluate access requirements before considering broader rights

## Risks If FileSystem Or FullAccess Becomes Necessary

If a future implementation cannot remain within minimal file access, the risks increase:
- larger attack surface
- harder reviewability
- weaker sandbox isolation
- more operational complexity
- greater chance of accidental non-paper side effects

If a future Execution Adapter is introduced, it should remain the sole boundary to execution methods and stay separable from the paper consumer.

If `FullAccess` were ever needed, it should be treated as an explicit design exception, not a default.

## Open Implementation Questions

- Should package reload polling use `OnTick()` or a timer for the final cBot implementation?
- Where exactly should logs live relative to the cTrader algo directory?
- What should the package expiration threshold be?
- Should `last_valid_package` persist across bot restarts or only runtime reloads?
- Which exact schema versioning rules will later be enforced?
