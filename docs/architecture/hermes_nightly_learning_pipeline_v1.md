# Hermes Nightly Learning Pipeline v1

## Ziel

Hermes soll lokale historische Marktdaten nachts auswerten und daraus Research-, Lern- und Evaluationsdaten erzeugen.

Diese Pipeline ist ausdruecklich fuer Lernen, Bewertung, Erfahrung und Forschung gedacht. Sie erzeugt keine Orders, verwaltet keine Positionen und fuehrt keine Live-Trades aus.

## Pipeline

```text
MarketData
-> FeatureGeneration
-> SignalGenerationStub
-> OutcomeEvaluation
-> BacktestStub
-> ResearchSummaryReport
```

## Komponenten

### NightlyResearchJob

Beschreibt einen lokalen Research-/Learning-Lauf:

- `run_id`
- geplanter Zeitpunkt
- Startzeit
- ausloesende Quelle
- Modus `demo_nightly_run`
- Ziel-Symbole `XAUUSD`, `EURUSD`, `GER40`
- Ziel-Timeframes `M5`, `M15`, `H1`, `H4`
- `no_auto_trading = true`
- `human_review_required = true`

### ResearchJobScheduleStub

Erzeugt einen Demo-Nightly-Job ohne Cron-, Service- oder Cloud-Abhaengigkeit. Ein echter Scheduler kann spaeter explizit angeschlossen werden.

### ResearchPipelineCoordinator

Orchestriert den lokalen Lauf:

1. Historische Candles aus `data/market_data/candles/` lesen.
2. FeatureVectors nach `data/exports/features/` schreiben.
3. Signal-Stubs nach `data/exports/signals/` schreiben.
4. Outcomes nach `data/reports/outcomes/` bewerten.
5. BacktestStub-Report erzeugen.
6. Nightly- und Research-Summary-Reports schreiben.

### ResearchSummaryReport

Der Summary-Report enthaelt:

- `run_id`
- `started_at_utc`
- `completed_at_utc`
- `symbols_processed`
- `candles_processed`
- `features_generated`
- `signals_generated`
- `outcomes_generated`
- `backtests_generated`
- `reports_generated`
- `warnings`
- `duration_seconds`
- `no_auto_trading`
- Artefaktpfade fuer Features, Signals, Outcomes, Backtest, Nightly Report und Research Report

## Speicherorte

```text
data/reports/nightly/
data/reports/research/
data/exports/features/
data/exports/signals/
data/reports/outcomes/
data/reports/backtests/
```

Aktuelle Report-Aliase:

```text
data/reports/nightly/latest_nightly_research.json
data/reports/research/latest_research_summary.json
```

## Events

Die Pipeline publiziert:

- `NightlyResearchStarted`
- `FeatureGenerationStarted`
- `FeatureGenerationCompleted`
- `SignalGenerationStarted`
- `SignalGenerationCompleted`
- `OutcomeEvaluationStarted`
- `OutcomeEvaluationCompleted`
- `BacktestStarted`
- `BacktestCompleted`
- `NightlyResearchCompleted`
- `NightlyResearchFailed`

## CLI

Nightly Learning lokal starten:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- run-nightly-research
```

Kurzstatus anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- research-status
```

Detailreport anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- research-report
```

## Safety

Fest gesetzte Prinzipien:

- keine Broker-Orders
- keine Positionsverwaltung
- keine Live-Trading-Ausfuehrung
- keine Auto-Execution am Markt
- `no_auto_trading = true`
- `human_review_required = true`

Alle erzeugten Ergebnisse sind lokale Research-/Learning-Kandidaten. Dauerhaftes Lernen oder Regelaktivierung muss spaeter ueber Review und Approval laufen.
