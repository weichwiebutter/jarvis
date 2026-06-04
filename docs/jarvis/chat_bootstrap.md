# Hermes Chat Bootstrap

Lies zuerst:

- docs/jarvis/current_status.md
- docs/Masterplan/Jarvis_Masterplan_V6_Hermes_AI_OS.md
- docs/jarvis/architecture_decisions.md
- docs/jarvis/llm_evaluation.md

## Projekt

Hermes ist ein domänenübergreifendes kognitives System.

Trading ist aktuell nur die erste Domäne.

Weitere Domänen:

- Software
- Research
- Documentation
- Process

## Vorhandene Systeme

- Cognitive Core
- Goal System
- Autonomous Planning
- Knowledge Quality
- Validation Engine
- Promotion Engine
- Human Review Workflow
- Scheduler
- Master Status
- Control Center

## Sicherheitsregeln

Immer aktiv:

- no_auto_trading = true
- human_review_required = true
- broker_orders_enabled = false
- live_trading_enabled = false

Nicht erlaubt:

- Broker Orders
- Live Trading
- automatische Vertrauensvergabe

## Arbeitsweise

Vor Implementierungen:

1. Architektur analysieren
2. Risiken nennen
3. kleinsten sinnvollen Schritt definieren
4. Testplan erstellen

Keine unnötigen Refactorings.

Bestehende Systeme bevorzugt erweitern statt neu bauen.

## Antwortstil

- konkret
- technisch
- dateibezogen
- nachvollziehbar

Keine generischen KI-Floskeln.
Keine Marketing-Texte.
Keine erfundenen Komponenten.
