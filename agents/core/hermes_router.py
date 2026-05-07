#!/usr/bin/env python3
"""
Hermes Router

Dynamic decision engine for Jarvis/Hermes architecture.

Role:
- Hermes decides.
- Jarvis executes safely.
- Ollama/OpenRouter/Agents are selectable routes.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class HermesRouteDecision:
    ok: bool
    task: str
    intent: str
    domain: str
    route: str
    model_preference: str | None
    agent_domain: str
    priority: str
    memory_required: bool
    executor_required: bool
    requires_approval: bool
    approval_reason: str | None
    reasoning: str
    confidence: float
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def normalize(text: str) -> str:
    return text.strip().lower()


def detect_intent(task: str) -> str:
    text = normalize(task)

    if any(x in text for x in ["merk dir", "speichere", "erinnere", "memory", "gedächtnis", "obsidian"]):
        return "memory"

    if any(x in text for x in ["code", "python", "script", "debug", "funktion", "klasse", "api", "json", "bash"]):
        return "coding"

    if any(x in text for x in ["analysiere", "analyse", "bewerte", "vergleich", "einschätzung"]):
        return "analysis"

    if any(x in text for x in ["recherchiere", "suche", "quelle", "news", "reddit", "internet", "web"]):
        return "research"

    if any(x in text for x in ["aktie", "börse", "trading", "markt", "portfolio", "kurs", "crypto", "bitcoin", "gold", "xauusd"]):
        return "trading"

    if any(x in text for x in ["plane", "planung", "roadmap", "architektur", "workflow", "strategie", "masterplan"]):
        return "planning"

    if any(x in text for x in ["voice", "sprache", "mikrofon", "tts", "whisper", "stimme"]):
        return "voice"

    return "chat"


def detect_domain(task: str, intent: str) -> str:
    text = normalize(task)

    if intent == "memory":
        return "memory"

    if intent == "coding":
        return "coding"

    if intent == "research":
        return "research"

    if intent == "trading":
        return "trading"

    if intent == "voice":
        return "improvement"

    if any(x in text for x in ["business", "kunde", "prozess", "strategie", "angebot"]):
        return "business"

    if any(x in text for x in ["pdf", "email", "dokument", "office", "briefing", "powerpoint", "excel"]):
        return "office"

    if any(x in text for x in ["verbessere", "optimiere", "refactor", "aufräumen", "struktur"]):
        return "improvement"

    if intent in {"planning", "analysis"}:
        return "business"

    return "office"


def estimate_complexity(task: str, intent: str, domain: str) -> int:
    text = normalize(task)
    score = 0

    if len(task) > 120:
        score += 1
    if len(task) > 350:
        score += 2
    if len(task) > 800:
        score += 3

    high_terms = [
        "architektur",
        "masterplan",
        "multi-agent",
        "autonomie",
        "deployment",
        "github",
        "security",
        "trading-bot",
        "entscheidung",
        "strategie",
        "system",
        "pipeline",
        "orchestrator",
    ]

    medium_terms = [
        "analysiere",
        "plane",
        "baue",
        "erstelle",
        "integriere",
        "debug",
        "refactor",
        "vergleich",
    ]

    for term in high_terms:
        if term in text:
            score += 2

    for term in medium_terms:
        if term in text:
            score += 1

    if intent in {"planning", "research", "analysis", "trading"}:
        score += 2

    if intent in {"coding", "memory", "voice"}:
        score += 1

    if domain in {"business", "research", "trading"}:
        score += 1

    return min(score, 10)


def decide_route(task: str) -> dict[str, Any]:
    intent = detect_intent(task)
    domain = detect_domain(task, intent)
    complexity = estimate_complexity(task, intent, domain)

    route = "ollama"
    model_preference: str | None = "local_small"
    agent_domain = domain
    priority = "normal"
    memory_required = False
    executor_required = False
    requires_approval = False
    approval_reason: str | None = None
    reasoning_parts: list[str] = []

    if intent == "memory":
        route = "agent"
        model_preference = None
        agent_domain = "memory"
        memory_required = True
        executor_required = True
        requires_approval = True
        approval_reason = "Memory write/read may affect persistent state."
        reasoning_parts.append("Memory intent detected; delegate to memory agent.")

    elif intent == "coding":
        route = "agent"
        model_preference = "local_large"
        agent_domain = "coding"
        requires_approval = True
        approval_reason = "Coding tasks may modify files or propose execution."
        reasoning_parts.append("Coding intent detected; delegate to coding agent.")

    elif intent == "research":
        route = "openrouter"
        model_preference = "external_reasoning"
        agent_domain = "research"
        memory_required = True
        requires_approval = False
        reasoning_parts.append("Research intent detected; external reasoning may be useful.")

    elif intent == "trading":
        route = "agent"
        model_preference = "external_reasoning"
        agent_domain = "trading"
        memory_required = True
        executor_required = False
        requires_approval = True
        approval_reason = "Trading tasks must remain advisory; no direct order execution."
        reasoning_parts.append("Trading intent detected; advisory-only agent route.")

    elif intent in {"planning", "analysis"} or complexity >= 6:
        route = "openrouter"
        model_preference = "external_reasoning"
        agent_domain = domain
        memory_required = True
        requires_approval = complexity >= 7
        approval_reason = "High-complexity planning should request confirmation before execution." if requires_approval else None
        reasoning_parts.append("Planning/analysis or high complexity; route to stronger external model.")

    elif intent == "voice":
        route = "agent"
        model_preference = "local_large"
        agent_domain = "improvement"
        requires_approval = True
        approval_reason = "Voice/system changes require approval."
        reasoning_parts.append("Voice/system intent detected; delegate to improvement agent.")

    else:
        route = "ollama"
        model_preference = "local_small"
        agent_domain = "office"
        reasoning_parts.append("Simple request; local Ollama route is sufficient.")

    if complexity >= 8:
        priority = "high"
    elif complexity <= 2:
        priority = "low"

    confidence = min(0.95, 0.45 + complexity * 0.05)
    if intent != "chat":
        confidence += 0.15
    confidence = min(confidence, 0.98)

    decision = HermesRouteDecision(
        ok=True,
        task=task,
        intent=intent,
        domain=domain,
        route=route,
        model_preference=model_preference,
        agent_domain=agent_domain,
        priority=priority,
        memory_required=memory_required,
        executor_required=executor_required,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        reasoning=" ".join(reasoning_parts),
        confidence=confidence,
        metadata={
            "complexity_score": complexity,
            "router": "hermes_router",
            "jarvis_role": "interface_runtime_control",
            "hermes_role": "brain_decision_delegation",
        },
    )

    return decision.to_dict()


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Hermes Router")
    parser.add_argument("task", nargs="*", help="Task to route")
    args = parser.parse_args()

    task = " ".join(args.task).strip()

    if not task:
        print("Kein Task angegeben.")
        return 1

    decision = decide_route(task)
    print(json.dumps(decision, indent=2, ensure_ascii=False))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
