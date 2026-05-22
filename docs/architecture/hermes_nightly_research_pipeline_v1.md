# Hermes Nightly Research Pipeline v1

## Ziel

Hermes bekommt eine lokale Nightly-Research-Pipeline, die vorhandene historische Marktdaten in Research- und Learning-Artefakte ueberfuehrt.

Die Pipeline ist lokal und sicher begrenzt:

- keine Orders
- keine Trading-Ausfuehrung
- keine Broker-Schreibzugriffe
- keine ML-Optimierung
- keine Auto-Execution
- keine Live-Trading-Logik

## Pipeline

```text
MarketData
-> FeatureGeneration
-> SignalGenerationStub
-> OutcomeEvaluation
-> BacktestStub
-> Reports
```

## Komponenten

### NightlyResearchJob

Beschreibt einen lokalen Research-Lauf:

- Job-ID
- geplanter Zeitpunkt
- Startzeit
- Ausloeser
- Modus
- `no_auto_trading`
- `human_review_required`

### ResearchJobScheduleStub

Erzeugt einen Demo-`NightlyResearchJob`. Es gibt noch keine Cron-, Service- oder Worker-Abhaengigkeit.

### ResearchPipelineCoordinator

Orchestriert die lokale Pipeline:

1. Features aus `data/market_data/candles/` erzeugen.
2. Signals aus Features per Stub ableiten.
3. Outcomes gegen gespeicherte SignalResults bewerten.
4. BacktestStub laufen lassen.
5. Nightly-Report schreiben.
6. Runtime Events publizieren.

### SignalGenerationStub

Erzeugt theoretische SignalResults aus den neuesten FeatureVectors pro Symbol/Timeframe. Die Signale sind Research-Artefakte und keine Trading-Signale fuer automatische Ausfuehrung.

### NightlyResearchReport

Report-Zusammenfassung mit:

- letzter Lauf
- Anzahl Features
- Anzahl Signals
- Anzahl Outcomes
- Anzahl Backtests
- Dauer
- Artefaktpfade
- Warnings
- Safety Flags

## Speicherorte

Nightly Reports:

```text
data/reports/nightly/
```

Zusatzartefakte:

```text
data/exports/features/
data/exports/signals/
data/reports/outcomes/
data/reports/backtests/
```

Der aktuelle Status wird zusaetzlich als:

```text
data/reports/nightly/latest_nightly_research.json
```

geschrieben.

## Events

Die Pipeline publiziert:

- `NightlyResearchStarted`
- `NightlyResearchCompleted`
- `NightlyResearchFailed`

Bestehende Sub-Pipeline-Events bleiben aktiv:

- `FeatureGenerationStarted`
- `FeatureGenerationCompleted`
- `OutcomeEvaluationStarted`
- `OutcomeEvaluationCompleted`

## CLI

Nightly Research lokal starten:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- run-nightly-research
```

Letzten Status anzeigen:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- research-status
```

## Safety

Die Pipeline setzt und berichtet:

- `no_auto_trading = true`
- `human_review_required = true`

Alle Ergebnisse sind lokale Research-/Learning-Kandidaten. Es gibt keine Orderausfuehrung, keine Positionsverwaltung und keine Broker-Schreiboperation.

## Naechste Schritte

- Scheduling spaeter ueber einen expliziten lokalen Scheduler.
- Data Quality Summary in Nightly Reports aufnehmen.
- Confidence Calibration aus Outcomes berechnen.
- Learning Candidates fuer Jarvis Learning UI erzeugen.
- Approval Queue fuer dauerhaftes Lernen anschliessen.
