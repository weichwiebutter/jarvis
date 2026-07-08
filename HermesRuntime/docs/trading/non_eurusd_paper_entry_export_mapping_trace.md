# Non-EURUSD Paper Entry Export Mapping Trace

## Ergebnis

GER40 und XAUUSD werden im aktuellen Exportpfad im **Non-Placeholder-Zweig** verarbeitet. Dort wird `paper_entry_enabled=false` gesetzt und die Entry-/SL-/TP-Felder bleiben absichtlich `null`.

## Exakter Codepfad

Quelle:

- [`Runtime/CloudEmbeddedReleasePackageGeneratorService.cs`](/home/home/jarvis/HermesRuntime/Runtime/CloudEmbeddedReleasePackageGeneratorService.cs)

### 1. Nicht-Placeholder-Zweig

Relevant ist der Pfad um Zeile 232:

- `var directPaperEntryEnabled = DeterminePaperEntryEnabled((double)asset.ConfidenceBaseline, null, paperEntryConfidenceThreshold);`
- anschließend werden exportiert:
  - `paper_entry_enabled = directPaperEntryEnabled`
  - `entry_price = null`
  - `stop_loss_price = null`
  - `take_profit_1 = null`
  - `take_profit_2 = null`
  - `invalidation_level = null`
  - `risk_reward = null`

Das ist der Pfad, der für GER40/XAUUSD verwendet wird.

### 2. Warum `paper_entry_enabled=false`

Die Hilfsmethode entscheidet so:

- [`Runtime/CloudEmbeddedReleasePackageGeneratorService.cs#L315-L330`](/home/home/jarvis/HermesRuntime/Runtime/CloudEmbeddedReleasePackageGeneratorService.cs#L315-L330)

Regel:

- wenn `fallbackAnnotation is null` => `false`
- wenn `confidenceBaseline < confidenceThreshold` => `false`
- sonst `true`

Für GER40/XAUUSD wird im aktuellen Exportpfad **kein Chart-Fallback** übergeben, weil der Code im Nicht-Placeholder-Zweig `fallbackAnnotation = null` verwendet.

## Unterschied EURUSD vs GER40/XAUUSD

### EURUSD

Der EURUSD-Eintrag läuft im Placeholder-/Fallback-Zweig:

- `fallback = chartFallbacks.TryGetValue(asset.Asset, out var chartAnnotation) ? chartAnnotation : null;`
- `fallbackConfidence = ... TryParseConfidenceLabel(fallback.Labels) ...`
- `paper_entry_enabled = DeterminePaperEntryEnabled(fallbackConfidence, fallback, paperEntryConfidenceThreshold);`
- `entry_price / stop_loss_price / take_profit_1 / take_profit_2 / invalidation_level / risk_reward` kommen aus dem Chart-Fallback

Damit kann EURUSD im Export `paper_entry_enabled=true` und echte Preisfelder bekommen.

### GER40 / XAUUSD

Für beide greift der direkte Asset-Zweig:

- `paper_entry_enabled = DeterminePaperEntryEnabled((double)asset.ConfidenceBaseline, null, paperEntryConfidenceThreshold)`
- Preisfelder werden dort explizit als `null` exportiert

## Datenbasis im Handoff-Bundle

Im System-B-Source-Package ist das aktuell so enthalten:

- EURUSD:
  - `setup_id = -`
  - `confidence_baseline = 0`
  - `readiness = needs_more_validation`

- GER40:
  - `setup_id = ger40_range_breakout_m5`
  - `confidence_baseline = 0.8046`
  - `readiness = bot_ready`

- XAUUSD:
  - `setup_id = xauusd_micro_trend_continuation_m5`
  - `confidence_baseline = 0.9374`
  - `readiness = bot_ready`

Der Unterschied entsteht also nicht in der Signal-Engine, sondern im **Export-Mapping**:

- EURUSD wird als Fallback-/Chart-Annotierungsfall behandelt.
- GER40/XAUUSD werden als direkte Asset-Einträge exportiert.

## Schlussfolgerung

Die Quelle für die Nicht-EURUSD-Blocker ist der Exportpfad in `CloudEmbeddedReleasePackageGeneratorService`:

1. Non-Placeholder-Zweig setzt `fallbackAnnotation = null`
2. `DeterminePaperEntryEnabled(..., null, ...)` liefert `false`
3. Preisfelder werden im selben Zweig auf `null` gesetzt

Damit sind GER40 und XAUUSD im Export bewusst nicht entry-ready.

