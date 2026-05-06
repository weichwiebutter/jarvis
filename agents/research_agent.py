#!/usr/bin/env python3
"""
Jarvis Research Agent V1

Role:
    Prepares research tasks for Jarvis.

Important:
    - Does NOT scrape directly
    - Does NOT call APIs directly
    - Does NOT browse directly
    - Does NOT execute subprocess
    - Prepares structured research plans for Hermes / Executor / future tools
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]
MEMORY_DIR = PROJECT_ROOT / "memory"
LOG_DIR = PROJECT_ROOT / "logs"

RESEARCH_LOG = LOG_DIR / "research_agent.log"


@dataclass
class ResearchRequest:
    topic: str
    sources: List[str] = field(default_factory=list)
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ResearchPlan:
    research_type: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    recommended_sources: List[str]
    steps: List[str]
    expected_outputs: List[str]
    quality_checks: List[str]


@dataclass
class ResearchResult:
    ok: bool
    timestamp: str
    topic: str
    plan: ResearchPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def classify_research_type(topic: str) -> str:
    text = topic.lower()

    if any(word in text for word in ["reddit", "community", "forum"]):
        return "community_signal"

    if any(word in text for word in ["paper", "studie", "research paper", "wissenschaft"]):
        return "paper_research"

    if any(word in text for word in ["news", "nachrichten", "aktuell", "heute"]):
        return "news_research"

    if any(word in text for word in ["markt", "trend", "branche", "wettbewerber"]):
        return "market_research"

    return "general_research"


def detect_risk(topic: str) -> tuple[str, bool, Optional[str]]:
    text = topic.lower()

    approval_terms = [
        "login",
        "account",
        "bezahlen",
        "paid",
        "abo",
        "kontakt aufnehmen",
        "mail senden",
        "scrape",
        "scraping",
    ]

    for term in approval_terms:
        if term in text:
            return "medium", True, f"Research task mentions approval-sensitive action: {term}"

    return "low", False, None


def recommend_sources(research_type: str) -> List[str]:
    if research_type == "community_signal":
        return [
            "Reddit API",
            "öffentliche Community-Quellen",
            "zusammenfassende Trend-Auswertung",
        ]

    if research_type == "paper_research":
        return [
            "Google Scholar / Semantic Scholar",
            "arXiv",
            "offizielle Paper / PDFs",
        ]

    if research_type == "news_research":
        return [
            "offizielle Newsquellen",
            "RSS / geprüfte Quellenliste",
            "mehrere unabhängige Quellen",
        ]

    if research_type == "market_research":
        return [
            "Unternehmenswebsites",
            "Branchenberichte",
            "News",
            "Wettbewerberseiten",
        ]

    return [
        "Websuche",
        "vertrauenswürdige Primärquellen",
        "Quellenvergleich",
    ]


def build_plan(request: ResearchRequest) -> ResearchPlan:
    research_type = classify_research_type(request.topic)
    risk_level, requires_approval, approval_reason = detect_risk(request.topic)

    recommended = request.sources if request.sources else recommend_sources(research_type)

    steps = [
        "Research-Ziel klären und offene Fragen identifizieren.",
        "Geeignete Quellen bestimmen.",
        "Quellen nach Qualität und Aktualität priorisieren.",
        "Informationen sammeln und trennen: Fakten, Meinungen, Annahmen.",
        "Widersprüche und Unsicherheiten markieren.",
        "Ergebnisse zusammenfassen.",
        "Nächste sinnvolle Schritte vorschlagen.",
    ]

    expected_outputs = [
        "Kurz-Zusammenfassung.",
        "Quellenliste.",
        "Kernaussagen.",
        "Unsicherheiten / offene Fragen.",
        "Relevanz für Jarvis / Frank.",
        "Nächste Schritte.",
    ]

    quality_checks = [
        "Keine ungeprüften Behauptungen als Fakten darstellen.",
        "Quellen trennen nach Primärquelle, Sekundärquelle, Community-Meinung.",
        "Aktualität prüfen.",
        "Bei Research mit Finanzbezug: keine Trading-Signale ableiten.",
        "Bei Reddit/Community: nur aggregierte Signale, keine Einzelmeinung übergewichten.",
    ]

    return ResearchPlan(
        research_type=research_type,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        recommended_sources=recommended,
        steps=steps,
        expected_outputs=expected_outputs,
        quality_checks=quality_checks,
    )


def build_output(result: ResearchResult) -> str:
    plan = result.plan

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    sources = "\n".join(f"- {source}" for source in plan.recommended_sources)
    checks = "\n".join(f"- {check}" for check in plan.quality_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein, solange nur recherchiert und zusammengefasst wird."
    )

    return (
        "Research Agent Plan\n\n"
        f"Research Type: {plan.research_type}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Empfohlene Quellen:\n{sources}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Quality Checks:\n{checks}"
    )


def log_result(result: ResearchResult) -> None:
    ensure_dirs()

    with RESEARCH_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class ResearchAgent:
    def handle(self, request: ResearchRequest) -> ResearchResult:
        try:
            plan = build_plan(request)

            result = ResearchResult(
                ok=True,
                timestamp=utc_now(),
                topic=request.topic,
                plan=plan,
                output="",
                metadata={
                    "source": "research_agent",
                    "execution_performed": False,
                    "api_ready": True,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = ResearchPlan(
                research_type="unknown",
                risk_level="medium",
                requires_approval=True,
                approval_reason="Research agent failed during planning.",
                recommended_sources=[],
                steps=[],
                expected_outputs=[],
                quality_checks=["Manual review required."],
            )

            result = ResearchResult(
                ok=False,
                timestamp=utc_now(),
                topic=request.topic,
                plan=fallback_plan,
                output="Research planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Research Agent V1")

    parser.add_argument(
        "topic",
        nargs="*",
        help="Research topic",
    )

    parser.add_argument(
        "--source",
        action="append",
        default=[],
        help="Preferred source. Can be used multiple times.",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Additional context",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    topic = " ".join(args.topic).strip()

    if not topic:
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": "No research topic provided.",
                    "example": "python3 agents/research_agent.py 'recherchiere aktuelle Trends zu lokalen AI Agents'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = ResearchAgent()
    result = agent.handle(
        ResearchRequest(
            topic=topic,
            sources=args.source,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
