#!/usr/bin/env python3
"""
Jarvis Briefing Worker V2

Role:
    Builds structured briefing content.

Important:
    - This is NOT an orchestrator.
    - It does NOT call executor_agent.
    - It does NOT call LLMs directly.
    - It prepares stable structure for later LLM refinement.
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import List, Dict, Any


SUPPORTED_MODES = {"morning", "midday", "evening", "custom"}


@dataclass
class BriefingSection:
    title: str
    purpose: str
    content: str


@dataclass
class BriefingOutput:
    mode: str
    timestamp: str
    summary: str
    disclaimer: str
    sections: List[BriefingSection]
    llm_prompt: str
    markdown: str


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def disclaimer() -> str:
    return (
        "Dies ist eine Analyse- und Entscheidungsunterstützung. "
        "Keine Anlageberatung, keine Kauf- oder Verkaufsempfehlung, "
        "keine automatische Orderausführung."
    )


def build_morning_sections() -> List[BriefingSection]:
    return [
        BriefingSection(
            title="Executive Summary",
            purpose="Schneller Überblick vor Handelsstart.",
            content=(
                "Marktdaten sind noch nicht angebunden. Der Worker stellt daher "
                "eine stabile Struktur bereit. Später werden hier Gold, Forex, "
                "Dollar, Renditen, Makrotermine und Risikofaktoren zusammengeführt."
            ),
        ),
        BriefingSection(
            title="Gold / XAUUSD",
            purpose="Bias, Schlüsselzonen und Risikokontext vorbereiten.",
            content=(
                "Noch keine Live-Daten. Zielstruktur: Bias bullisch / bärisch / neutral, "
                "wichtige Unterstützungen und Widerstände, Volatilität, Dollar- und "
                "Renditeeinfluss."
            ),
        ),
        BriefingSection(
            title="Forex",
            purpose="Wichtige FX-Paare strukturiert bewerten.",
            content=(
                "Noch keine Live-Daten. Zielpaare: EUR/USD, USD/CHF, EUR/CHF, GBP/USD, "
                "USD/JPY. Bewertung nach Trend, Dollar-Stärke, Risikoappetit und Makro."
            ),
        ),
        BriefingSection(
            title="Makro heute",
            purpose="Event-Risiko für den Tag identifizieren.",
            content=(
                "Noch kein Kalender angebunden. Später: CPI, NFP, Fed, EZB, SNB, PMI, "
                "GDP und relevante Reden mit Uhrzeit und erwarteter Marktwirkung."
            ),
        ),
        BriefingSection(
            title="Szenarien",
            purpose="If/Then-Denken statt Vorhersage.",
            content=(
                "Bullisch: Risikoappetit steigt, Dollar schwächer, Renditen fallen. "
                "Bärisch: Dollar stärker, Renditen steigen, Risikoaversion nimmt ab. "
                "Neutral: Seitwärtsphase, keine klaren Impulse, Fokus auf Levels."
            ),
        ),
        BriefingSection(
            title="Risiken",
            purpose="Fehlerquellen und Unsicherheiten sichtbar machen.",
            content=(
                "Ohne echte Daten keine harte Aussage. Risiken: Datenlücken, verspätete "
                "News, Makrotermine, Liquiditätswechsel, Modellhalluzinationen."
            ),
        ),
        BriefingSection(
            title="Fragen für Frank",
            purpose="Entscheidungsvorbereitung vor Handelsstart.",
            content=(
                "1. Gibt es heute Termine, die Positionsgröße reduzieren sollten? "
                "2. Ist der Markt trendig oder range-gebunden? "
                "3. Welche Annahme würde den heutigen Bias sofort ungültig machen?"
            ),
        ),
    ]


def build_midday_sections() -> List[BriefingSection]:
    return [
        BriefingSection(
            title="Session Pulse",
            purpose="Lage während der laufenden Session prüfen.",
            content="Noch keine Intraday-Daten. Später: Bewegung seit Open, Volatilität, Marktbreite.",
        ),
        BriefingSection(
            title="Was hat sich geändert?",
            purpose="Morgenannahmen gegen aktuelle Lage prüfen.",
            content="Vergleich von Morning Bias mit aktueller Preisstruktur.",
        ),
        BriefingSection(
            title="Nachmittags-Szenarien",
            purpose="Entscheidungspunkte für die zweite Tageshälfte.",
            content="If/Then-Struktur für Fortsetzung, Umkehr oder Seitwärtsmarkt.",
        ),
        BriefingSection(
            title="Risiko-Check",
            purpose="Überhandelung und schlechte Setups vermeiden.",
            content="Fokus auf Event-Risiko, Volatilität und unklare Signale.",
        ),
    ]


def build_evening_sections() -> List[BriefingSection]:
    return [
        BriefingSection(
            title="Tagesrückblick",
            purpose="Lernen aus dem Handelstag.",
            content="Später: Abgleich zwischen Szenarien, tatsächlicher Bewegung und Entscheidungen.",
        ),
        BriefingSection(
            title="Fehler und Erkenntnisse",
            purpose="Self-Improvement vorbereiten.",
            content="Welche Annahmen waren falsch? Welche Signale waren nützlich?",
        ),
        BriefingSection(
            title="Vorbereitung Morgen",
            purpose="Nächsten Tag strukturieren.",
            content="Watchlist, offene Makrothemen, mögliche Szenarien.",
        ),
    ]


def build_custom_sections() -> List[BriefingSection]:
    return [
        BriefingSection(
            title="Custom Briefing",
            purpose="Freier Briefing-Modus.",
            content="Custom-Modus ist vorbereitet, aber noch nicht spezialisiert.",
        )
    ]


def sections_for_mode(mode: str) -> List[BriefingSection]:
    if mode == "morning":
        return build_morning_sections()
    if mode == "midday":
        return build_midday_sections()
    if mode == "evening":
        return build_evening_sections()
    return build_custom_sections()


def build_summary(mode: str) -> str:
    if mode == "morning":
        return "Morning Briefing Struktur erstellt. Datenintegration und LLM-Veredelung folgen."
    if mode == "midday":
        return "Midday Briefing Struktur erstellt. Fokus: Lageabgleich und Risikoprüfung."
    if mode == "evening":
        return "Evening Briefing Struktur erstellt. Fokus: Review und Vorbereitung."
    return "Custom Briefing Struktur erstellt."


def build_llm_prompt(mode: str, sections: List[BriefingSection]) -> str:
    section_text = "\n\n".join(
        f"{idx + 1}. {section.title}\n"
        f"Zweck: {section.purpose}\n"
        f"Inhalt: {section.content}"
        for idx, section in enumerate(sections)
    )

    return f"""
Du bist Jarvis, ein persönliches lokal-first AI-System.

Aufgabe:
Veredle die folgende Briefing-Struktur zu einem klaren, deutschen Briefing.

Regeln:
- Keine erfundenen Fakten.
- Wenn keine Daten vorhanden sind, klar als Platzhalter / Annahme markieren.
- Keine Kauf- oder Verkaufsempfehlungen.
- Keine automatische Trading-Entscheidung.
- Fokus auf Denkstruktur, Szenarien und Entscheidungsfragen.
- Klar, nüchtern, praxisnah schreiben.

Modus:
{mode}

Struktur:
{section_text}
""".strip()


def build_markdown(output: Dict[str, Any]) -> str:
    lines = [
        f"# Jarvis {output['mode'].capitalize()} Briefing",
        "",
        f"Stand: {output['timestamp']}",
        "",
        "## Summary",
        output["summary"],
        "",
        "## Sections",
    ]

    for section in output["sections"]:
        lines.extend(
            [
                "",
                f"### {section['title']}",
                f"**Zweck:** {section['purpose']}",
                "",
                section["content"],
            ]
        )

    lines.extend(
        [
            "",
            "## Disclaimer",
            output["disclaimer"],
        ]
    )

    return "\n".join(lines)


def generate_briefing(mode: str) -> BriefingOutput:
    if mode not in SUPPORTED_MODES:
        raise ValueError(f"Unsupported mode: {mode}")

    sections = sections_for_mode(mode)
    timestamp = utc_now()
    summary = build_summary(mode)
    llm_prompt = build_llm_prompt(mode, sections)

    raw_output = {
        "mode": mode,
        "timestamp": timestamp,
        "summary": summary,
        "disclaimer": disclaimer(),
        "sections": [asdict(section) for section in sections],
    }

    markdown = build_markdown(raw_output)

    return BriefingOutput(
        mode=mode,
        timestamp=timestamp,
        summary=summary,
        disclaimer=disclaimer(),
        sections=sections,
        llm_prompt=llm_prompt,
        markdown=markdown,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Jarvis Briefing Worker V2")
    parser.add_argument(
        "--mode",
        choices=sorted(SUPPORTED_MODES),
        default="morning",
        help="Briefing mode",
    )

    args = parser.parse_args()

    try:
        result = generate_briefing(args.mode)
        print(json.dumps(asdict(result), indent=2, ensure_ascii=False))
        return 0

    except Exception as exc:
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": str(exc),
                    "mode": args.mode,
                    "timestamp": utc_now(),
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
