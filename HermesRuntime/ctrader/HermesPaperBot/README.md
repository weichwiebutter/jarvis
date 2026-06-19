# HermesPaperBot

Paper-only skeleton for the first Hermes cTrader bot implementation.

## Scope

- Paper-only skeleton
- No orders
- No cTrader Order API
- No live trading
- No demo orders
- cTrader is only a consumer of a release bundle
- HermesRuntime is the release authority
- Bundle IO is local only
- No network access
- No broker access
- Only `paper_only` bundles are accepted

## Safety Invariants

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `order_api_enabled=false`
- `paper_mode=true`
- `broker_action=none`

## Reference Docs

- `docs/trading/ctrader_paper_bot_skeleton_spec_v1.md`
- `docs/trading/ctrader_bot_paper_runtime_scope_v1.md`
- `docs/trading/ctrader_bot_configuration_model_v1.md`
- `docs/trading/ctrader_bot_bundle_import_model_v1.md`
- `docs/trading/ctrader_bot_drift_check_model_v1.md`

## Notes

This directory is a guarded skeleton only.
It intentionally contains no project file, no implementation logic, and no trading capabilities.
Bundle import is local filesystem IO only and never reaches the cTrader or broker APIs.

## Safety Guard

Run the forbidden reference guard before reviewing any later implementation work:

```bash
bash scripts/check_ctrader_paper_bot_forbidden_refs.sh
```

## Preflight

Run the manual preflight before any later cTrader paper bot change:

```bash
bash scripts/preflight_ctrader_paper_bot.sh
```

## Runtime Orchestrator V1

The paper runtime now uses a defensive orchestrator step:

- validate configuration
- import a local bundle
- validate manifest, provenance, checksums, safety, and drift
- evaluate the kill switch
- produce a paper-only decision placeholder

This remains paper-only.
No broker access.
No cTrader Order API.
No live or demo execution.

## In-Memory Harness

The paper runtime can be checked with an in-memory harness in `ctrader/HermesPaperBot/tests/PaperRuntimeOrchestratorHarness.cs`.

- no cTrader runtime
- no broker
- no orders
- only orchestrator safety and validation
- intended for temporary scratch compilation and JSON/text output

## Runtime Logging V1

Local runtime logs are written as JSONL/JSON only.

- append-only JSONL for step logs and kill-switch events
- JSON summary for the current runtime state
- local filesystem only
- no broker action
- no cTrader API

## Paper State Persistence V1

The virtual paper portfolio is saved as a local snapshot and restored defensively on startup.

- snapshot file: `paper_state_snapshot.json`
- `PaperStateStore` saves and loads the snapshot locally or in cloud-compatible storage
- corrupt snapshots are handled defensively
- recovery can either fall back to a fresh state or activate the kill switch depending on configuration
- restored state still keeps `broker_action=none`
- no broker orders, no demo orders, and no live orders are ever created

## Paper Trading Engine V1

The paper runtime now derives virtual paper-trade steps from the embedded strategy package.

- signal candidates are parsed defensively from the embedded package
- paper-only decisions may include `would_enter_long`, `would_enter_short`, `would_skip`, `would_invalidate`, and `would_expire`
- virtual paper positions are tracked in memory only
- paper-only limits protect the runtime from excessive virtual exposure
- all outputs keep `broker_action=none`
- no broker orders, no demo orders, and no live orders are ever produced

Paper trade state is written to local JSONL/JSON logs only.

## Cloud Runtime V1

The preferred long-term execution model is cTrader Cloud with an embedded release package.

- cloud runtime can run independently of the developer PC
- embedded package avoids dependence on local bundle files
- local file bundles remain useful for development and VPS-style runs
- HermesRuntime stays the release authority
- no orders
- no cTrader Order API
- `broker_action=none` remains mandatory

Cloud mode uses `RuntimeMode=cloud_embedded_bundle` and an embedded release package snapshot instead of a local bundle inbox.

## Cloud Embedded Bootstrap V1

`Generated/EmbeddedReleasePackage.g.cs` is the cloud input source for the bot bootstrap.

- the generated file carries `EmbeddedReleasePackage.PackageJson`
- the bootstrapper deserializes the package into `CloudEmbeddedReleasePackage`
- no local bundle paths are required in cloud mode
- the bot remains paper-only
- no orders and no cTrader Order API are used

## Cloud Entry Skeleton V1

`HermesPaperBot.cs` is structured as a safe cloud entry skeleton.

- `StartPaperRuntime()` prepares cloud configuration in memory
- `RunPaperRuntimeStep()` delegates to the defensive orchestrator
- `StopPaperRuntime()` remains summary-ready and defensive
- `GetLastRuntimeStepResult()` returns the last in-memory step result

This is not a live cTrader host integration yet.
It is only a guarded entry structure for the future cloud runtime path.

## Cloud Market Context Adapter V1

The runtime now accepts a read-only market context object through the safe host/orchestrator path.

- `IMarketContextProvider` supplies `RuntimeMarketContext`
- `StaticMarketContextProvider` is used by harnesses and defensive local runs
- the cloud host can pass symbol, bid, ask, spread, and server time into the paper runtime
- no System A dataset is required for cloud runtime steps
- no orders, no demo orders, and no live orders are ever produced

## Cloud Host Adapter Skeleton

`HermesPaperBotCloudHost.cs` is a separate host adapter skeleton that only delegates to the safe paper bot skeleton.

- no cTrader runtime attribute is added yet
- no platform SDK reference is required in this skeleton step (the real wrapper can be conditionally compiled later)
- `OnStart()`, `OnTimer()`, `OnStop()`, and `OnException(Exception)` only delegate
- order APIs remain forbidden
- a future cTrader Cloud integration may need platform-specific entry wiring, but not here

## Read-Only cTrader Market Context Provider V1

The conditional cTrader wrapper can later read market context without trading actions.

- symbol name, bid, ask, spread, and server time are read-only inputs
- time frame may be read when available
- the provider fills `RuntimeMarketContext`
- the context is passed to the cloud host and paper runtime
- no account, position, pending order, or trade operation APIs are used
- no orders, no demo orders, and no live orders are ever produced

## Future cTrader API Boundary

The real cTrader API is not wired in yet.

- later only lifecycle, timer, and `Print` style diagnostics are expected
- trading operations remain forbidden
- no order API is allowed
- no demo or live execution is allowed
- any future cAlgo.API import must be reviewed against the boundary doc

## Future AccessRights Decision

The Cloud Paper Bot targets `AccessRights.None`.

- Cloud mode should not depend on FileSystem access
- FileSystem is only for local or VPS-style development modes
- Internet and FullAccess are not part of the Cloud target
