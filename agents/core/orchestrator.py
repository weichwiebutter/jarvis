#!/usr/bin/env python3
"""
Jarvis Orchestrator V1

Central routing layer for specialized Jarvis agents.

Role:
- classify user tasks
- choose responsible agent domain
- prepare structured orchestration result
- keep Jarvis Core clean
"""

from __future__ import annotations

import json
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import Optional


@dataclass
class OrchestrationResult:
    ok: bool
    timestamp: str
    task: str
    domain: str
    agent_module: str
    agent_class: str
    confidence: float
    reason: str
    error: Optional[str] = None


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


AGENT_MAP = {
    "business": {
        "module": "agents.business.business_agent",
        "class": "BusinessAgent",
    },
    "coding": {
        "module": "agents.coding.coding_agent",
        "class": "CodingAgent",
    },
    "research": {
        "module": "agents.research.research_agent",
        "class": "ResearchAgent",
    },
    "trading": {
        "module": "agents.trading.trading_agent",
        "class": "TradingAgent",
    },
    "memory": {
        "module": "agents.memory.memory_agent",
        "class": "MemoryAgent",
    },
    "office": {
        "module": "agents.office.office_agent",
        "class": "OfficeAgent",
    },
    "improvement": {
        "module": "agents.improvement.improvement_agent",
        "class": "ImprovementAgent",
    },
    "core": {
        "module": "agents.core.jarvis_core",
        "class": "JarvisCore",
    },
}


DOMAIN_KEYWORDS = {
    "coding": [
        "code",
        "python",
        "script",
        "debug",
        "fehler",
        "klasse",
        "funktion",
        "import",
        "api",
        "json",
        "bash",
    ],
    "research": [
        "recherchiere",
        "suche",
        "quelle",
        "news",
        "internet",
        "analyse",
        "vergleich",
    ],
    "trading": [
        "aktie",
        "börse",
        "markt",
        "trading",
        "portfolio",
        "kurs",
        "etf",
        "crypto",
        "bitcoin",
    ],
    "memory": [
        "merk dir",
        "speichere",
        "erinnere",
        "memory",
        "gedächtnis",
        "obsidian",
    ],
    "office": [
        "email",
        "briefing",
        "dokument",
        "pdf",
        "excel",
        "powerpoint",
        "aufgabenliste",
        "termin",
    ],
    "business": [
        "strategie",
        "geschäft",
        "kunde",
        "angebot",
        "prozess",
        "planung",
        "roadmap",
    ],
    "improvement": [
        "verbessere",
        "optimiere",
        "refactor",
        "aufräumen",
        "struktur",
        "qualität",
    ],
}


def normalize(text: str) -> str:
    return text.strip().lower()


def classify_task(task: str) -> OrchestrationResult:
    text = normalize(task)

    best_domain = "office"
    best_score = 0
    matched_terms: list[str] = []

    for domain, keywords in DOMAIN_KEYWORDS.items():
        score = 0
        local_matches: list[str] = []

        for keyword in keywords:
            if keyword in text:
                score += 1
                local_matches.append(keyword)

        if score > best_score:
            best_score = score
            best_domain = domain
            matched_terms = local_matches

    if best_score == 0:
        best_domain = "office"
        confidence = 0.35
        reason = "Keine eindeutigen Keywords erkannt, Standardroute office."
    else:
        confidence = min(0.95, 0.45 + best_score * 0.15)
        reason = "Erkannt über Keywords: " + ", ".join(matched_terms)

    agent = AGENT_MAP[best_domain]

    return OrchestrationResult(
        ok=True,
        timestamp=utc_now(),
        task=task,
        domain=best_domain,
        agent_module=agent["module"],
        agent_class=agent["class"],
        confidence=confidence,
        reason=reason,
    )


def orchestrate(task: str) -> dict:
    result = classify_task(task)
    return asdict(result)


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="Jarvis Orchestrator")
    parser.add_argument("task", nargs="*", help="Task to classify")
    parser.add_argument("--json", action="store_true", help="Print JSON output")

    args = parser.parse_args()
    task = " ".join(args.task).strip()

    if not task:
        print("Kein Task angegeben.")
        return 1

    result = orchestrate(task)

    if args.json:
        print(json.dumps(result, indent=2, ensure_ascii=False))
    else:
        print(f"Domain: {result['domain']}")
        print(f"Agent: {result['agent_module']}.{result['agent_class']}")
        print(f"Confidence: {result['confidence']}")
        print(f"Reason: {result['reason']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
