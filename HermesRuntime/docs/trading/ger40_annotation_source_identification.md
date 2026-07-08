# GER40 Annotation Source Identification

## Kurzfazit

Die valide interne GER40-Spec-Quelle ist das System-B-Hand-off-Bundle:

- [`/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle/ensemble_signal_agent_package.json`](/home/home/jarvis/HermesRuntime/.codex_artifacts/reports/system_b_handoff/system_b_handoff_bundle/ensemble_signal_agent_package.json)

Darin ist GER40 als `bot_ready` vorhanden und trägt die relevante Setup-ID:

- `setup_id = ger40_range_breakout_m5`
- `confidence_baseline = 0.8046`
- `readiness = bot_ready`

## Was die Spec tatsächlich liefert

Die GER40-Spec enthält:

- Asset: `GER40`
- Setup: `ger40_range_breakout_m5`
- Primary Candidate: `scalp_ger40_160c06ea86`
- Backup Candidates: `scalp_ger40_7dc64f1768`, `scalp_ger40_0cabc34acc`
- Entry-/Exit-Logik auf Spezifikationsniveau
- Session-Tags: `london`, `new_york`, `overlap`
- Risikohinweise und `bot_ready`

Damit ist GER40 als **interne Signal-/Spec-Quelle** vorhanden.

## Was fehlt

Im aktuellen Chart-Annotation-Export fehlt jedoch eine GER40-Annotation vollständig:

- [`ctrader/HermesPaperBot/Generated/EmbeddedReleasePackage.g.cs`](/home/home/jarvis/HermesRuntime/ctrader/HermesPaperBot/Generated/EmbeddedReleasePackage.g.cs)

Die eingebettete Chart-Annotation-Spec enthält derzeit nur:

- EURUSD
- XAUUSD

Für GER40 existiert dort kein Annotation-Eintrag, daher können keine chartbasierten Felder abgeleitet werden:

- `entry_price`
- `stop_loss_price`
- `take_profit_1`
- `take_profit_2`
- `invalidation_level`
- `risk_reward`

## Bewertung der möglichen Quellen

### 1. `signal-agent-specs`

Für GER40 ist die relevante interne Spezifikation im System-B-Hand-off-Bundle vorhanden und eindeutig als `ger40_range_breakout_m5` erkennbar.

### 2. `scalping-bot-specs`

Es gibt in der aktuellen Runtime-Sicht keinen separaten GER40-Chart-Annotation-Eintrag aus dieser Quelle.

### 3. `system_b_handoff_bundle`

Das ist die **eigentliche kanonische interne Spec-Quelle** für GER40.

### 4. `chart annotation exports`

Aktuell **kein GER40-Eintrag** vorhanden.

### 5. `reports mit ger40_range_breakout_m5`

Der GER40-Setup ist im Hand-off-Bundle und in den daraus erzeugten Reports sichtbar, aber nicht als Chart-Annotation materialisiert.

## Schlussfolgerung

Es gibt eine **valide interne GER40 Signal-/Spec-Quelle**, aber **keine valide interne GER40 Chart-Annotation-Quelle** im aktuellen Export.

Das heißt:

- **Spec-Quelle vorhanden:** ja, `system_b_handoff_bundle` mit `ger40_range_breakout_m5`
- **Chart-Annotation-Quelle vorhanden:** nein
- **GER40 Entry/SL/TP/Invalidation/RR aktuell ableitbar:** nur mit zusätzlichem Ableitungs- oder Review-Artefakt

## Technische Empfehlung

Wenn GER40 chartfähig gemacht werden soll, muss die bestehende Spec-Quelle aus dem Hand-off-Bundle in eine Chart-Annotation überführt werden.
Ohne diesen Schritt bleibt GER40 im Chart-Export ohne Entry-/SL-/TP-Felder.
