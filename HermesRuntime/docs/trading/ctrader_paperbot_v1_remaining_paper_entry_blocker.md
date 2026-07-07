# cTrader PaperBot Remaining Paper Entry Blocker Diagnosis

Datum: 2026-07-07

## Fragestellung

Warum ist das EURUSD-Signal trotz `confidence=0.919` weiterhin `paper_entry_disabled`?

## Diagnose

Der verbleibende Blocker kommt **nicht** von fehlender Confidence.

### Beobachtete Werte

Im aktuellen Explain-Report für EURUSD:

- `confidence = 0.919`
- `confidence_source = embedded_confidence_baseline`
- `missing_confidence_fields = []`
- `confidence_blockers = ["paper_entry_disabled"]`
- `decision_reason = paper_entry_disabled`
- `lifecycle_state = invalidated`

### Tatsächliche Ursache im Signal Package

Das generierte `SignalPackageJson` enthält für EURUSD bereits:

- `paper_entry_enabled = false`
- `signal_status = invalidated`
- `signal_lifecycle_status = invalidated`
- `warnings = ["paper_entry_disabled", "blocked"]`

Damit ist die Entscheidung bereits im Embedded-Signalpfad fest auf `invalidated` gesetzt.  
Der Confidence-Fix hat den Wert korrekt geliefert, aber **den Entry-Disable-Status nicht verändert**.

## Codepfad

Die Blockierung entsteht im Evaluator:

- `Runtime/PaperSignalEvaluationService.cs`
  - markiert ein Signal als invalidated, wenn `candidate.PaperEntryEnabled == false`
  - setzt dann `reason = "paper_entry_disabled"`

Das `paper_entry_enabled=false` stammt aus dem bereits generierten Signal-Package, nicht aus einem Confidence-Mangel.

## Wichtige Abgrenzung

- **Nicht Ursache:** fehlende Confidence-Metadaten
- **Nicht Ursache:** Safety Flags
- **Nicht Ursache:** Release Mode
- **Nicht Ursache:** broker_action
- **Ursache:** `paper_entry_enabled=false` im eingebetteten Signalpfad

## Konsequenz

Für EURUSD ist der aktuelle Zustand fachlich:

- Confidence vorhanden
- Signal geladen
- aber Entry bewusst deaktiviert

Darum bleibt das Signal `invalidated` und wird nicht zu `would_trigger`.

## Safety

Die Safety-Invarianten bleiben unverändert:

- `no_auto_trading=true`
- `human_review_required=true`
- `broker_orders_enabled=false`
- `live_trading_enabled=false`
- `broker_action=none`

## Nächster Schritt

Wenn EURUSD wieder triggerfähig sein soll, müsste die Entry-Freigabe im Embedded Signal Package selbst angepasst werden.  
Das ist **keine** Confidence-Reparatur mehr, sondern eine separate Signal-Freigabe-Änderung.
