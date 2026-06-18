# cTrader Bot Operating Model V1

## Purpose

This document defines the operating model for cTrader bot evolution in HermesRuntime.

It is an architecture and operating model only.
It does not define any bot implementation.
It does not include cBot code.
It does not use the cTrader Order API.
It does not place broker, demo, or live orders.

## Strategic Context

HermesRuntime is the research, optimization, and bot-development platform.
cTrader executes a reviewed bot version.

The file `ensemble_signal_agent_package.json` remains useful in V1, but it is not necessarily a permanent runtime dependency.

In the long term, the preferred model is a versioned cTrader bot that can run independently of HermesRuntime.

## Three Operating Models

### Model A: Runtime Signal Consumer

HermesRuntime runs regularly and writes `ensemble_signal_agent_package.json`.
The cTrader bot reads the file locally.

Characteristics:
- tight coupling to HermesRuntime
- fast iteration
- good for paper and hot-swap tests
- good for validation of file reloads and safety gates
- research and runtime stay connected

### Model B: Local API Consumer

HermesRuntime runs locally as an API.
The cTrader bot queries `localhost`.

Characteristics:
- still coupled to HermesRuntime availability
- more moving parts than file-based exchange
- higher complexity
- more maintenance surface
- easier to drift into runtime-service dependency

### Model C: Standalone cTrader Bot Version

HermesRuntime develops and certifies strategies.
A versioned bot artifact is produced.
cTrader executes the bot independently.
HermesRuntime is only needed for updates and improvements.

Characteristics:
- lowest runtime dependency on HermesRuntime
- best long-term operational separation
- best fit for a stable bot release line
- easiest to support durable execution and rollback
- lowest drift risk once the bot version is frozen and validated

## Model Comparison

### Operating Dependency on PC / HermesRuntime

- Model A: high
- Model B: high
- Model C: low

### Security

- Model A: good if read-only and safety-guarded
- Model B: weaker because it introduces a local service surface
- Model C: best long-term if the bot artifact is self-contained and reviewed

### Complexity

- Model A: low to medium
- Model B: high
- Model C: medium

### Updateability

- Model A: very high
- Model B: high
- Model C: high through version replacement, not live coupling

### Debuggability

- Model A: excellent
- Model B: good but more moving parts
- Model C: good for stable releases, less direct during research

### Paper-Test Suitability

- Model A: excellent
- Model B: good
- Model C: good if the release pipeline is mature

### Demo-Bot Suitability

- Model A: good as a research transition model
- Model B: acceptable but unnecessary complexity
- Model C: preferred long term

### Later Live-Eignung

- Model A: limited, because it keeps runtime coupling
- Model B: limited, because API dependency persists
- Model C: best, because it separates strategy release from research runtime

### Drift Risk Between Research and Bot

- Model A: medium to high
- Model B: medium
- Model C: lowest when the release pipeline is well controlled

### Wartbarkeit

- Model A: good for early testing
- Model B: weakest
- Model C: best for long-lived maintenance

## Architecture Decision

### Short-Term V1

Model A remains allowed for paper and hot-swap tests.

`ensemble_signal_agent_package.json` remains:
- an exchange artifact
- a handoff bundle
- a versioning artifact
- a paper-test input
- an optional hot-swap test format

No execution is enabled.
No orders are allowed.
No cTrader Order API is used.

### Long-Term Target

Model C is preferred.

The cTrader bot should eventually carry a versioned, reviewed strategy or strategy package.
HermesRuntime does not need to run continuously once the bot version is released.
Updates happen through a new bot version or a new validated strategy package.

## Hybrid Roadmap

### Phase 1: Paper Consumer With JSON

Goals:
- hot-swap testing
- state machine testing
- logging testing
- safety testing

Status:
- allowed in V1

### Phase 2: Standalone Paper Bot

Goals:
- transfer validated signal rules into bot configuration or bot code
- remove the need for HermesRuntime as a permanent runtime dependency
- keep paper-only operation

Status:
- preferred next architectural step

### Phase 3: Demo Execution Spec

Goals:
- define a separate `ExecutionAdapter` layer
- if later approved, allow demo-only execution
- preserve safety gates

Status:
- future only

### Phase 4: Versioned Bot Release Pipeline

Goals:
- HermesRuntime creates a bot release candidate
- human review approves it
- cTrader bot version is replaced
- rollback remains possible

Status:
- preferred long-term release model

## Role of `ensemble_signal_agent_package.json`

The file is not only a runtime signal file.

It should be treated as:
- export contract
- bot configuration source
- paper-test input
- handoff bundle
- versioning artifact
- future code/config generation base

V1 still allows the file to be swapped at any time.
But Model C does not depend on HermesRuntime running continuously once a validated bot version exists.

## Versioning Terms

- `strategy_package_version`
- `bot_version`
- `generated_at`
- `certified_at`
- `source_research_run_id`
- `compatible_schema_version`
- `rollback_version`

## Safety Invariants

These flags remain mandatory:
- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`

Any deviation later requires:
- its own specification
- human approval

## Forbidden API Surfaces in V1

The V1 bot must not use:
- `ExecuteMarketOrder`
- `PlaceLimitOrder`
- `PlaceStopOrder`
- `ModifyPosition`
- `ClosePosition`
- `CancelPendingOrder`
- `Positions.Modify`
- `PendingOrders`
- Trading Operations

If execution is ever added, it must be isolated in a separate `ExecutionAdapter`.

## Open Questions

- When should JSON hot-swap be replaced by bot-versioning?
- How will a bot release candidate be created?
- Will the strategy package later become config-only or code-generated?
- Where will rollback live?
- How will drift between HermesRuntime backtests and cBot behavior be checked?
- How will a demo bot later be separated from the paper bot specification?

## Summary

Short-term V1 may use Model A for paper and hot-swap tests.
Long-term, Model C is the preferred target:
HermesRuntime develops and certifies the strategy, cTrader runs a versioned bot, and HermesRuntime becomes an update source rather than a permanent runtime dependency.
