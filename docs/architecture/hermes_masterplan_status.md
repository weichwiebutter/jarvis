# Hermes Masterplan Status

Stand: 2026-05-26

Ziel: kurzer Abgleich von Masterplan/TODO gegen den aktuellen
Implementierungsstand. Dieser Status ist eine Dokumentationssicht; es wurden
keine Code-, UI-, Runtime- oder Service-Aenderungen vorgenommen.

## Gepruefte Quellen

- `README.md`
- `AGENTS.md`
- `docs/Masterplan/Jarvis_Masterplan_V6_Hermes_AI_OS.md`
- `docs/architecture/future_hermes_todo.md`
- `docs/hermes_trading_analyst_roadmap.md`
- `docs/architecture/hermes_beta3_operational_readiness.md`
- `docs/architecture/hermes_beta3_scheduler_supervisor.md`
- `HermesRuntime/`
- `ui/jarvis-control-center/`
- vorhandene Reports unter `/mnt/d/HermesData/`

## Kurzfazit

Hermes ist aktuell keine Trading-Ausfuehrung, sondern eine lokale
Research-/Learning-Plattform mit Runtime Foundation, CLI, Supervisor/Scheduler,
Nightly Beta 3, cTrader-History-Import, Strategy Research, Pattern Catalog,
Regime Intelligence und sichtbarer React Control Center Foundation.

Der wichtigste offene Punkt ist nicht mehr "Grundlage bauen", sondern
"Qualitaet und Betrieb haerten": realistischere Validierung, kontrollierter
Nachtbetrieb, stabile read-only UI-Bridge und klares Bot-Candidate-Gating.

## Statusuebersicht

| Bereich | Status | Nachweis | Offen / Risiko | Naechster sinnvoller Schritt |
| --- | --- | --- | --- | --- |
| Runtime Foundation | erledigt | `HermesRuntime/RuntimeHost`, EventStore, Snapshots, Queue, Worker-Stubs, Health, Setup Watch, Reports vorhanden. | Keine Aussage aus diesem Check zu aktuellem Build-Zustand, da nicht gebaut. | Kurzen Build-/CLI-Smoke separat ausfuehren, wenn Codefreigabe ansteht. |
| CLI | erledigt | `HermesRuntime/cli/Program.cs` enthaelt Health, Events, Jobs, Storage, cTrader, Research, Supervisor, Scheduler, Regime, Pattern und Report-Befehle. | CLI ist breit, aber nicht in diesem Check ausgefuehrt. | Smoke-Set dokumentiert halten: `supervisor-status`, `scheduler-status`, `resource-status`, `regime-summary`. |
| Supervisor / Scheduler | teilweise erledigt | `HermesSupervisor`, `HermesInternalScheduler`, Background-PID, Heartbeat, Stop-Request, `config/schedules.json`; 5 Jobs konfiguriert, 4 aktiv. | Aktueller Supervisor-State: `stopped_by_stop_request`; Windows Task/echter Dauerbetrieb noch praktisch zu verifizieren. | Einmal `start_supervisor.sh` plus `supervisor-status` pruefen, danach Windows Task validieren. |
| Nightly Beta 3 | teilweise erledigt | `NightlyResearchService`, `run-nightly-beta3`, `nightly_state.json`, Autopilot/Research/Simulation/Walk-Forward Integration vorhanden. | Aktueller State: ausserhalb Nightly Window, `iterations_completed=0`; erster unbeaufsichtigter Nachtlauf noch nicht als stabiler Betrieb belegt. | Ueberwachter 1-Nacht-Lauf mit Supervisor, danach Reports/Logs auswerten. |
| Storage / Retention | erledigt | `storage.profile.json` zeigt auf `/mnt/d/HermesData`; `StorageHygieneService`, CleanupPlan, Retention-Doku vorhanden. | Cleanup-Plan hat 15.872 sichere Kandidaten, aber Apply nicht Teil dieses Checks. | Cleanup-Plan reviewen; nur bei Bedarf `cleanup-apply --safe` separat freigeben. |
| ResourceGuard | erledigt | `ResourceGuard`, Policy und `/mnt/d/HermesData/reports/resource/resource_status.json`; letzter Report: CPU ca. 3.42%, RAM ca. 6.82%, Disk ca. 88.17% frei, Action `continue`. | Grenzwerte muessen bei langen Laeufen beobachtet werden. | ResourceGuard-Werte im Operator Dashboard und Nightly Logs nach erstem Nachtlauf pruefen. |
| cTrader Daten | teilweise erledigt | CSV-Import, OpenAPI Config/Token Store, OAuth URL/Code, chunked `download-history`, Candle-Dateien unter `/mnt/d/HermesData/market_data/candles/`. | Echte Downloads haengen an lokaler Config/Token und API-Verfuegbarkeit; `market_data_refresh` ist im Scheduler deaktiviert. | Datenabdeckung pro Symbol/Timeframe verdichten und deduplizieren, bevor weitere Strategy-Runs bewertet werden. |
| Feature / Outcome / Backtest Pipeline | erledigt | FeatureGeneration, SignalGenerationStub, OutcomeTracker, BacktestWorker, Beta Learning Pipeline und Reports vorhanden. | Noch Stubs/vereinfachte Logik; nicht als produktive Trading-Validierung behandeln. | Pipeline-Ergebnisse nur als Research-Input nutzen und weiter mit realistischer Simulation abgleichen. |
| Strategy Research | teilweise erledigt | StrategyDefinition/Variant/Fitness/Memory, Adaptive Research, Autopilot, Insights und viele Results unter `/mnt/d/HermesData/strategy_research/`. | Insights zeigen weiter unrealistisch perfekte Strategien; Robustheit noch kritisch. | Realism-/Walk-Forward-/Overfit-Penalties priorisieren und Top-Strategien erst nach Validation akzeptieren. |
| Pattern Catalog | erledigt | `StrategyPatternCatalog`, `TradingDeKnowledgeCatalog`, 33 Pattern/Strategy-Eintraege in `pattern_catalog.json`. | Pattern-Regeln sind teils Stub/Metadaten, nicht alle als harte Candle-Logik validiert. | Pattern-Regeln pro Familie schrittweise in realistisch testbare Bedingungen ueberfuehren. |
| Regime Intelligence | teilweise erledigt | `MarketRegimeClassifier`, Regime Reports vorhanden; `regime_summary.json`: 539.175 Features, 244 Snapshots, Regimes wie ranging/trending/low_volatility/breakout. | Regime-Performance ist Research-Orientierung, noch keine harte Strategy-Freigabe. | Strategy-Ranking strikt nach Symbol/Timeframe/Regime/Session trennen. |
| Realistic Simulation / Walk-Forward | teilweise erledigt | BrokerReality, CandleTradeSimulator, RealisticSimulationService, WalkForwardValidationService, OverfitDetector, Reports vorhanden. | Masterplan-Ziel "robuste Netto-Performance" noch nicht voll erreicht; perfekte Winrates bleiben Watchpoint. | Overfit-Suspects und robuste Strategien als Gate fuer Bot Candidates erzwingen. |
| UI / Control Center | teilweise erledigt | React/Vite Control Center mit Runtime, Setup Watch, Research, Storage, Jobs, Events, CLI Mock und Beta-3 Operator Dashboard Foundation. | Browser liest lokale Dateien nur im Dev-Modus via `/@fs`; keine produktive read-only Bridge/Tauri-Anbindung. | Kleine localhost-only read-only Bridge oder Tauri File Access als naechste Integrationsschicht planen. |
| Gradio Dev UI | erledigt fuer Dev/Test | `ui_app.py` und README-Rolle: Dev/Test UI. | Nicht Ziel fuer finales Operator Dashboard. | Gradio stabil halten, neue Operator-Funktionalitaet primar in React weiterfuehren. |
| Safety / Trading-Control | teilweise erledigt | `no_auto_trading` und `human_review_required` in Reports/UI sichtbar; Safety-Gates dokumentiert; Operator UI hat deaktivierte Control-Platzhalter. | Kein echter Trading Control Layer, keine Paper/Demo/Live-Freigabepipeline implementiert. | Erst Control-Layer-Schema fuer Toggle, Whitelists, Limits, Emergency Stop und Audit definieren. |
| Future Scalping Bot | offen / blockiert | Roadmap und Bot-Candidate-Pipeline dokumentiert. | Blockiert durch fehlende robuste OOS/Walk-Forward-Netto-Performance, fehlendes Demo/Paper-Gating und fehlenden Control Layer. | Candidate Pipeline als Report/State implementieren: `research_candidate -> promising -> robust -> demo_bot_candidate`. |
| External Pattern / Discovery | teilweise erledigt | Trusted Strategy Discovery, Spotware/Trading.de Knowledge Catalog und Pattern-Metadaten vorhanden. | Keine ungeprueften Crawler; externe Repos/Code nicht ausfuehren. | Discovery weiter read-only halten und Lizenz-/Risk Flags sichtbar machen. |

## Erledigt

- Runtime v1 Foundation mit Events, Snapshots, Queue, Worker-Stubs und Health.
- Hermes CLI Foundation mit breitem lokalen Status-/Research-Befehlsumfang.
- Konfigurierbarer Data Lake unter `/mnt/d/HermesData`.
- cTrader OpenAPI Read-only Foundation inklusive OAuth/Token Store und
  chunked Historical Download.
- Nightly Beta 3, Research Autopilot, Research Memory und Checkpoints.
- ResourceGuard und StorageHygiene mit sicheren Cleanup-Plans.
- Strategy/Pattern Catalog inklusive Trading.de-Knowledge-Metadaten.
- Market Regime Reports und Strategy-Regime-Performance Reports.
- React Control Center Prototype mit Operator Dashboard Foundation.
- Safety Flags `no_auto_trading=true` und `human_review_required=true`
  sind dokumentiert und in Reports/UI sichtbar.

## Teilweise erledigt

- Supervisor/Scheduler ist implementiert, aber aktueller State ist gestoppt;
  echter Windows/WSL-Dauerbetrieb braucht einen praktischen End-to-End-Test.
- Nightly Beta 3 ist orchestriert, aber ein kompletter stabiler Nachtlauf muss
  noch anhand Logs, Checkpoints und Reports bestaetigt werden.
- Strategy Research erzeugt viele Varianten und Insights, aber die Qualitaet
  ist wegen weiterhin sehr perfekter Ergebnisse noch nicht bot-reif.
- UI liest lokale Reports im Vite-Dev-Kontext, aber noch nicht ueber eine
  produktive read-only Bridge.
- Pattern-Regeln sind vorhanden, aber teilweise noch Stub-/Metadatenlogik.

## Offen

- Produktiver read-only Runtime Bridge Layer fuer React/Tauri.
- Harte Bot-Candidate-Pipeline mit Approval- und Audit-State.
- Future Trading Control Layer: Auto-Trading Toggle, Paper/Demo Mode,
  Risk Limits, Lot-/Volume-Limits, Strategy Whitelist, Symbol Whitelist,
  Emergency Stop.
- Paper-/Demo-Trading-Phase, Micro-Live nur mit Approval.
- Vollstaendige robuste Netto-Performance-Bewertung pro Symbol, Timeframe,
  Regime und Session.
- Mehrtaegiger unbeaufsichtigter Supervisor-/Nightly-Betrieb mit Recovery-Nachweis.

## Blockiert

- Scalping Bot ist blockiert, bis robuste OOS-/Walk-Forward-Netto-Performance
  und der Trading Control Layer vorhanden sind.
- Live-/Demo-Execution ist blockiert durch `no_auto_trading`, fehlende
  Freigabepipeline und fehlende Broker-Execution-Schicht.
- Automatischer Market-Data-Refresh ist im Scheduler bewusst deaktiviert.
- UI-Produktivbetrieb ist blockiert, bis lokale Dateizugriffe ueber eine
  sichere read-only Bridge oder Tauri geloest sind.

## Empfohlene naechste 3 Schritte

1. Supervisor/Nightly operativ beweisen: Windows Task oder
   `start_supervisor.sh` starten, 1 Nachtlauf beobachten, danach
   `supervisor-status`, `scheduler-status`, `nightly-status`, Resource- und
   Cleanup-Reports auswerten.
2. Research-Qualitaet haerten: perfekte Winrates konsequent als
   Overfit-/Realism-Watchpoint behandeln, robuste Strategien nur mit
   Walk-Forward/OOS, Kosten, Slippage, Drawdown und Regime-Stabilitaet
   akzeptieren.
3. Control Center sauber anbinden: kleine localhost-only read-only Bridge oder
   Tauri File Access fuer Reports/Logs bauen; keine Commands, keine Orders,
   keine Trading-Ausfuehrung.

