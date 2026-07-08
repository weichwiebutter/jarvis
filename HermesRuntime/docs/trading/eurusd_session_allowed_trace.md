# EURUSD `session_allowed=false` Trace

## Ergebnis

`session_allowed=false` wird im aktuellen PaperBot-Pfad von `PaperSignalEvaluationService` geschrieben und anschließend von `PaperSignalExplainService` nur übernommen.

## Erzeugender Service

- [`Runtime/PaperSignalEvaluationService.cs`](/home/home/jarvis/HermesRuntime/Runtime/PaperSignalEvaluationService.cs)
- Entscheidungsstelle: `SessionFilter().Evaluate(context)`
- Bei `Allowed = false` setzt der Service:
  - `signalStatus = "skipped_session"`
  - `paperDecision = "would_wait"`
  - `reason = sessionResult.Reason`
  - `lifecycleStatus = "waiting"`

## Warum `session_allowed=false`

Die aktuelle Session-Logik ist ein Stub in:

- [`ctrader/HermesPaperBot/Services/SessionFilter.cs`](/home/home/jarvis/HermesRuntime/ctrader/HermesPaperBot/Services/SessionFilter.cs)
- [`ctrader/HermesPaperBot.AlgoProject/Services/SessionFilter.cs`](/home/home/jarvis/HermesRuntime/ctrader/HermesPaperBot.AlgoProject/Services/SessionFilter.cs)

Sie erlaubt nur Kontexte, deren `Source` `"harness"` oder `"paper"` enthält.  
Wenn das nicht zutrifft, liefert sie:

- `Allowed = false`
- `Status = "blocked"`
- `Reason = "blocked_by_skeleton"`

## Verwendete MarketContext.Source

Im aktuellen `paper_runtime_step`-Report ist der Kontext:

- `market_context_source = paper_closed_trade_harness`

## Report-Zeitstempel

Aktuelle Report-Stände:

- `paper_signal_evaluation.json`
  - `updated_at_utc = 2026-07-07T20:50:19.7046815+00:00`
  - enthält für EURUSD:
    - `session_allowed = false`
    - `signal_status = skipped_session`
    - `reason = blocked_by_skeleton`

- `paper_signal_explain.json`
  - `updated_at_utc = 2026-07-07T20:52:53.1788538+00:00`
  - spiegelt dieselben Werte nur erklärend wider:
    - `session_allowed = false`
    - `decision_reason = skipped_session`
    - `next_action = wait_for_allowed_session`

## Staleness-Bewertung

- `paper_signal_evaluation.json` ist jünger als `paper_runtime_step.json`.
- `paper_signal_explain.json` ist noch jünger und basiert auf der Evaluation.
- Die Session-Information ist **nicht stale** im Explain-Pfad; sie stammt aus der aktuellen Evaluation.

## Fazit

`session_allowed=false` wird aktuell **im Evaluation-Report** erzeugt, nicht im Explain-Report.
Die Ursache ist der SessionFilter-Stub, nicht Confidence oder Safety.

