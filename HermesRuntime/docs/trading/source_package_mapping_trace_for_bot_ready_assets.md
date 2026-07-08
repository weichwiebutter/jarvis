# Source Package Mapping Trace for Bot-Ready Assets

## Ergebnis

EURUSD läuft über den Chart-Fallback, weil es im Source Package als Placeholder exportiert ist.
GER40 und XAUUSD laufen im direkten Asset-Zweig, weil sie **nicht** als Placeholder erkannt werden.

## Entscheidender Schalter

Datei:

- [`Runtime/CloudEmbeddedReleasePackageGeneratorService.cs`](/home/home/jarvis/HermesRuntime/Runtime/CloudEmbeddedReleasePackageGeneratorService.cs)

Entscheidende Prüfung:

- [`Runtime/CloudEmbeddedReleasePackageGeneratorService.cs#L343-L344`](/home/home/jarvis/HermesRuntime/Runtime/CloudEmbeddedReleasePackageGeneratorService.cs#L343-L344)

```csharp
private static bool IsPlaceholderAsset(EnsembleSignalAgentPortfolioPackageEntry asset)
    => string.IsNullOrWhiteSpace(asset.SetupId) || asset.SetupId == "-" || asset.ConfidenceBaseline <= 0;
```

## Warum EURUSD den Chart-Fallback nutzt

Im Source-Bundle ist EURUSD so gespeichert:

- `setup_id = "-"`,
- `primary_candidate = "-"`,
- `confidence_baseline = 0`,
- `readiness = needs_more_validation`

Damit ist EURUSD ein **Placeholder Asset** und fällt in diesen Codepfad:

- `var fallback = chartFallbacks.TryGetValue(asset.Asset, out var chartAnnotation) ? chartAnnotation : null;`
- `fallbackConfidence = ...`
- `paper_entry_enabled = DeterminePaperEntryEnabled(fallbackConfidence, fallback, paperEntryConfidenceThreshold);`
- `entry_price = fallback?.EntryPrice`
- `stop_loss_price = fallback?.StopLoss`
- `take_profit_1 = fallback?.TakeProfit1`
- `take_profit_2 = fallback?.TakeProfit2`
- `invalidation_level = fallback?.InvalidationLevel`
- `risk_reward = fallback?.RiskReward`

## Warum GER40 und XAUUSD keinen Chart-Fallback bekommen

Für GER40 und XAUUSD gilt:

- `setup_id` ist gesetzt
- `confidence_baseline > 0`
- `readiness = bot_ready`

Damit sind beide **keine Placeholder Assets** und landen im direkten Asset-Zweig:

- `var directPaperEntryEnabled = DeterminePaperEntryEnabled((double)asset.ConfidenceBaseline, null, paperEntryConfidenceThreshold);`

In diesem Zweig:

- wird `fallbackAnnotation` bewusst als `null` verwendet
- `paper_entry_enabled` wird dadurch `false`
- Preisfelder werden explizit auf `null` gesetzt

## Unterschied zwischen Chart Annotation Spec und Export-Mapping

Die Chart Annotation Spec enthält aktuell Annotations für:

- EURUSD
- XAUUSD

Der Export nutzt diese Annotationen aber **nur im Placeholder-Zweig**.

Das heißt:

- **EURUSD**: Placeholder + Chart-Fallback => Annotation/Preisfelder werden exportiert
- **XAUUSD**: Chart Annotation vorhanden, aber direktes Asset => Annotation wird vom Exportpfad nicht verwendet
- **GER40**: keine passende Annotation im Fallback-Mapping + direktes Asset => keine Preisfelder

## Warum bot_ready nicht automatisch reicht

`readiness = bot_ready` sorgt nur dafür, dass der Asset-Eintrag im Source Package als reif markiert ist.
Es schaltet **nicht** automatisch den Chart-Fallback frei.

Die Entscheidung für `paper_entry_enabled` und Preisfelder hängt im Generator aktuell an:

1. Placeholder-Erkennung
2. Chart-Fallback-Verfügbarkeit
3. Confidence-Schwelle

## Fazit

Die Ursache ist das Export-Mapping:

- EURUSD ist der einzige Placeholder und bekommt Chart-Fallback + Preisfelder.
- GER40/XAUUSD sind bot-ready, aber nicht als Placeholder markiert und landen deshalb im direkten Asset-Zweig ohne Annotation-/Preis-Export.

