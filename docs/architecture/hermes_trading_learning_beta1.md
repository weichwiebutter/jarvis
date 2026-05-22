# Hermes Trading Learning Beta 1

## Ziel

Trading Learning Beta 1 ist eine minimale Verbindungsschicht ueber der vorhandenen lokalen Research-Pipeline.

Ein einzelner CLI-Befehl startet:

```text
MarketData
-> FeatureGeneration
-> SignalGeneration/Export
-> OutcomeTracking
-> BacktestStub
-> BetaReport
```

## CLI

Start:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- run-beta-learning
```

Status:

```bash
dotnet run --project ./cli/Hermes.Cli.csproj -- beta-status
```

## Report

Beta-Reports liegen unter:

```text
data/reports/beta/
```

Der Report enthaelt Candles, Features, Signals, Outcomes, Backtest-Stub-Count, Warnings, Laufzeit und `learning_ready`.

## Safety

- keine Broker-Orders
- keine Live-Trading-Ausfuehrung
- keine Positionsverwaltung
- keine Auto-Execution am Markt
- `no_auto_trading` bleibt aktiv
- Ergebnisse sind lokale Lern-/Evaluationsdaten
