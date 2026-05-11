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

import sys
from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


ADAPTIVE_CONFIDENCE_THRESHOLD = 0.35
AGENT_DOMAINS = {
    "memory",
    "office",
    "research",
    "coding",
    "business",
    "trading",
    "improvement",
}


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


def _adaptive_normalize(text: str) -> str:
    return (
        text.strip()
        .lower()
        .replace("ä", "ae")
        .replace("ö", "oe")
        .replace("ü", "ue")
        .replace("ß", "ss")
    )


def _adaptive_tokens(text: str) -> set[str]:
    normalized = _adaptive_normalize(text)
    raw_tokens = "".join(char if char.isalnum() else " " for char in normalized).split()

    return {token for token in raw_tokens if len(token) >= 3}


def _adaptive_hint_score(task: str, hint: dict[str, Any]) -> float:
    task_text = _adaptive_normalize(task)
    objective = str(hint.get("objective_contains", "")).strip()
    objective_text = _adaptive_normalize(objective)

    if not objective_text:
        return 0.0

    if objective_text in task_text or task_text in objective_text:
        return 1.0

    task_tokens = _adaptive_tokens(task)
    objective_tokens = _adaptive_tokens(objective)

    if not task_tokens or not objective_tokens:
        return 0.0

    overlap = len(task_tokens & objective_tokens)
    union = len(task_tokens | objective_tokens)

    return overlap / union if union else 0.0


def get_adaptive_routing_recommendation(task: str) -> dict[str, Any]:
    """
    Read persisted adaptive routing hints without calling adaptive fallback routing.

    hermes_adaptive_routing.recommend_adaptive_route() calls decide_route() for
    fallback decisions, so the router uses the read-only profile API directly to
    avoid recursion.
    """

    try:
        from agents.core.hermes_adaptive_routing import build_adaptive_routing_profile

        profile = build_adaptive_routing_profile()
        profile_summary = {
            "has_learning_data": bool(profile.get("has_learning_data", False)),
            "history_counts": profile.get("history_counts", {}),
            "domain_counts": profile.get("domain_counts", {}),
            "agent_counts": profile.get("agent_counts", {}),
            "approval_policy_counts": profile.get("approval_policy_counts", {}),
        }

        if not profile.get("has_learning_data"):
            return {
                "ok": True,
                "used": False,
                "source": "fallback_router_no_learning_data",
                "reason": "No persisted Hermes learning data available.",
                "profile_summary": profile_summary,
            }

        scored_hints = [
            (score, hint)
            for hint in profile.get("routing_hints", [])
            for score in [_adaptive_hint_score(task, hint)]
            if score > 0
        ]
        scored_hints.sort(key=lambda item: item[0], reverse=True)

        if not scored_hints:
            return {
                "ok": True,
                "used": False,
                "source": "fallback_router_no_adaptive_match",
                "reason": "Learning data exists, but no adaptive hint matched this task.",
                "profile_summary": profile_summary,
            }

        best_score, best_hint = scored_hints[0]

        if best_score < ADAPTIVE_CONFIDENCE_THRESHOLD:
            return {
                "ok": True,
                "used": False,
                "source": "fallback_router_low_adaptive_confidence",
                "reason": "Best adaptive hint did not pass the confidence threshold.",
                "matched_hint": best_hint,
                "matched_hint_score": round(best_score, 3),
                "profile_summary": profile_summary,
            }

        preferred_domain = best_hint.get("preferred_domain")
        preferred_agent = best_hint.get("preferred_agent")
        approval_policy = best_hint.get("approval_policy")

        if not any([preferred_domain, preferred_agent, approval_policy]):
            return {
                "ok": True,
                "used": False,
                "source": "fallback_router_unusable_adaptive_hint",
                "reason": "Adaptive hint matched but did not contain usable routing fields.",
                "matched_hint": best_hint,
                "matched_hint_score": round(best_score, 3),
                "profile_summary": profile_summary,
            }

        support = sum(
            1
            for _, hint in scored_hints
            if hint.get("preferred_domain") == preferred_domain
            or hint.get("preferred_agent") == preferred_agent
            or hint.get("approval_policy") == approval_policy
        )
        confidence = min(0.95, 0.45 + best_score * 0.35 + min(support, 5) * 0.03)

        return {
            "ok": True,
            "used": True,
            "source": "adaptive_learning_history",
            "preferred_domain": preferred_domain,
            "preferred_agent": preferred_agent,
            "approval_policy": approval_policy,
            "confidence": round(confidence, 3),
            "matched_hint": best_hint,
            "matched_hint_score": round(best_score, 3),
            "supporting_matches": support,
            "profile_summary": profile_summary,
        }

    except Exception as exc:
        return {
            "ok": False,
            "used": False,
            "source": "adaptive_routing_error",
            "warning": f"Adaptive routing failed; using normal router fallback: {exc}",
        }


def decide_route(task: str) -> dict[str, Any]:
    adaptive_recommendation = get_adaptive_routing_recommendation(task)
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

    if adaptive_recommendation.get("used"):
        preferred_domain = adaptive_recommendation.get("preferred_domain")
        approval_policy = adaptive_recommendation.get("approval_policy")

        if preferred_domain in AGENT_DOMAINS:
            domain = str(preferred_domain)
            agent_domain = str(preferred_domain)
            route = "agent"

            if preferred_domain == "memory":
                model_preference = None
                memory_required = True
                executor_required = True
            elif preferred_domain in {"coding", "improvement"}:
                model_preference = "local_large"
            elif preferred_domain in {"research", "business", "trading"}:
                model_preference = "external_reasoning"

            reasoning_parts.append(
                f"Adaptive routing history matched; prefer {preferred_domain}."
            )

        if approval_policy == "human_approval_required":
            requires_approval = True
            approval_reason = approval_reason or "Adaptive routing history recommends human approval."

        confidence = max(
            confidence,
            float(adaptive_recommendation.get("confidence", confidence)),
        )
    elif adaptive_recommendation.get("warning"):
        reasoning_parts.append(str(adaptive_recommendation["warning"]))

    try:
        from agents.core.provider_registry import recommend_provider

        provider_recommendation = recommend_provider(
            task=task,
            intent=intent,
            cost_sensitive=True,
        )
    except Exception as exc:
        provider_recommendation = {
            "ok": False,
            "error": str(exc),
        }

    try:
        from agents.core.model_registry import recommend_model

        model_recommendation = recommend_model(
            task=task,
            intent=intent,
            route=route,
            cost_sensitive=True,
            offline=False,
        )
    except Exception as exc:
        model_recommendation = {
            "ok": False,
            "error": str(exc),
        }

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
            "provider_recommendation": provider_recommendation,
            "jarvis_role": "interface_runtime_control",
            "hermes_role": "brain_decision_delegation",
            "model_recommendation": model_recommendation,
            "adaptive_routing": {
                "checked": True,
                "used": bool(adaptive_recommendation.get("used", False)),
                "source": adaptive_recommendation.get("source"),
                "warning": adaptive_recommendation.get("warning"),
                "reason": adaptive_recommendation.get("reason"),
                "preferred_domain": adaptive_recommendation.get("preferred_domain"),
                "preferred_agent": adaptive_recommendation.get("preferred_agent"),
                "approval_policy": adaptive_recommendation.get("approval_policy"),
                "confidence": adaptive_recommendation.get("confidence"),
                "matched_hint": adaptive_recommendation.get("matched_hint"),
                "matched_hint_score": adaptive_recommendation.get("matched_hint_score"),
                "supporting_matches": adaptive_recommendation.get("supporting_matches"),
                "profile_summary": adaptive_recommendation.get("profile_summary", {}),
                "fallback_active": not bool(adaptive_recommendation.get("used", False)),
            },
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
