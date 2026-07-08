# GER40 Chart Annotation Gap Trace

## Ergebnis

GER40 hat aktuell **keine passende Chart Annotation**, weil die zugrunde liegende Chart-Annotation-Quelle im Source Bundle **keinen GER40-Eintrag** enthält.

## Beleg 1: Source Handoff Bundle

Im Source Bundle:

- [`/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle/ensemble_signal_agent_package.json`](/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle/ensemble_signal_agent_package.json)

enthält `assets` nur:

- EURUSD
- GER40
- XAUUSD

Aber der aktuelle eingebettete Chart-Annotation-Export enthält nur:

- EURUSD
- XAUUSD

Für **GER40** ist dort **kein Annotation-Eintrag vorhanden**.

## Beleg 2: Chart Annotation Spec

Aktuelle eingebettete Chart Annotation Spec:

- [`ctrader/HermesPaperBot/Generated/EmbeddedReleasePackage.g.cs`](/home/home/jarvis/HermesRuntime/ctrader/HermesPaperBot/Generated/EmbeddedReleasePackage.g.cs)

Die Annotation-Liste enthält:

- EURUSD
- XAUUSD

Kein GER40-Eintrag ist vorhanden.

## Asset-/setup_id-Mapping

GER40 im Source Package:

- `asset = GER40`
- `setup_id = ger40_range_breakout_m5`
- `confidence_baseline = 0.8046`
- `readiness = bot_ready`

Damit ist GER40 zwar **bot_ready**, aber die Chart-Annotation-Suche findet keinen passenden Symbol-Eintrag.

## Warum GER40 trotz `bot_ready` keine Entry/SL/TP-Felder bekommt

Der Export-Mapping-Pfad kann Entry/SL/TP nur dann aus der Chart-Annotation übernehmen, wenn ein passender Chart-Annotation-Fallback gefunden wird.

Für GER40 ist das aktuell nicht der Fall:

- kein GER40 in der Chart Annotation Spec
- keine Fallback-Annotation
- deshalb bleiben:
  - `entry_price = null`
  - `stop_loss_price = null`
  - `take_profit_1 = null`
  - `take_profit_2 = null`
  - `invalidation_level = null`
  - `risk_reward = null`

## Asset-Name-Mismatch

Der aktuelle Befund spricht **nicht** für einen `GER40` vs `DE40` vs `GER40.cash`-Mismatch.

Stattdessen ist die Lage:

- GER40 existiert im Source Package
- GER40 ist bot_ready
- aber im Chart-Annotation-Export fehlt ein GER40-Symbol komplett

Das Problem ist also eher **fehlende Datenquelle / fehlende GER40-Annotation** als ein Namens-Mismatch.

## Export-/Annotation-Generator

Der Generator hat aktuell nur Annotationen aus der Chart-Quelle, aber keine GER40-Annotation im Exportmaterial.

Damit überspringt der Generator GER40 nicht wegen eines Bugs in der Zuordnung, sondern weil:

- die Chart Annotation Spec für GER40 nicht vorhanden ist
- deshalb kein Enrichment möglich ist

## Fazit

Die GER40-Gap-Ursache ist:

1. GER40 ist im Source Package vorhanden und `bot_ready`
2. Die Chart Annotation Spec enthält aber **keinen GER40-Eintrag**
3. Der Exportpfad kann deshalb keine Entry-/SL-/TP-Felder übernehmen

