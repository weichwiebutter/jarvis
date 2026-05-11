#!/usr/bin/env python3
"""
Hermes Adaptive Routing Intelligence

Reads persisted Hermes learning data from .hermes and derives routing
recommendations. This module is read-only: it does not write runtime data.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.hermes_router import decide_route
from agents.core.model_registry import recommend_model
from agents.core.provider_registry import recommend_provider


HERMES_DIR = PROJECT_ROOT / ".hermes"
LEARNING_DIR = HERMES_DIR / "learning"
ROUTING_HINTS_DIR = HERMES_DIR / "routing_hints"
IMPROVEMENTS_DIR = HERMES_DIR / "improvements"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _read_json_file(path: Path) -> tuple[dict[str, Any] | None, str | None]:
    try:
        return json.loads(path.read_text(encoding="utf-8")), None
    except Exception as exc:
        return None, str(exc)


def _latest_json_files(directory: Path, limit: int) -> list[Path]:
    if not directory.exists():
        return []

    files = [path for path in directory.glob("*.json") if path.is_file()]
    files.sort(key=lambda path: path.stat().st_mtime, reverse=True)

    return files[:limit]


def _load_group(name: str, directory: Path, limit: int) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    records: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []

    for path in _latest_json_files(directory, limit):
        payload, error = _read_json_file(path)

        if payload is None:
            errors.append(
                {
                    "group": name,
                    "path": str(path.relative_to(PROJECT_ROOT)),
                    "error": error or "Unknown JSON read error.",
                }
            )
            continue

        records.append(
            {
                "path": str(path.relative_to(PROJECT_ROOT)),
                "payload": payload,
            }
        )

    return records, errors


def load_learning_history(limit: int = 50) -> dict:
    limit = max(1, int(limit))

    learning_records, learning_errors = _load_group("learning", LEARNING_DIR, limit)
    routing_records, routing_errors = _load_group("routing_hints", ROUTING_HINTS_DIR, limit)
    improvement_records, improvement_errors = _load_group("improvements", IMPROVEMENTS_DIR, limit)

    return {
        "ok": True,
        "limit": limit,
        "learning": learning_records,
        "routing_hints": routing_records,
        "improvements": improvement_records,
        "counts": {
            "learning": len(learning_records),
            "routing_hints": len(routing_records),
            "improvements": len(improvement_records),
            "total": len(learning_records) + len(routing_records) + len(improvement_records),
        },
        "errors": learning_errors + routing_errors + improvement_errors,
        "metadata": {
            "source": "hermes_adaptive_routing",
            "storage_root": str(HERMES_DIR.relative_to(PROJECT_ROOT)),
            "read_only": True,
        },
        "timestamp": utc_now(),
    }


def _as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _learning_feedback(payload: dict[str, Any]) -> dict[str, Any]:
    value = payload.get("learning_feedback", {})
    return value if isinstance(value, dict) else {}


def _collect_routing_hints(history: dict[str, Any]) -> list[dict[str, Any]]:
    hints: list[dict[str, Any]] = []

    for record in history.get("routing_hints", []):
        payload = record.get("payload", {})

        for hint in _as_list(payload.get("routing_hints")):
            if isinstance(hint, dict):
                hints.append(
                    {
                        **hint,
                        "source_file": record.get("path"),
                        "source_group": "routing_hints",
                    }
                )

    for record in history.get("learning", []):
        payload = record.get("payload", {})
        feedback = _learning_feedback(payload)
        seen_in_record: set[tuple[str, str, str, str]] = set()

        for hint in _as_list(payload.get("routing_hints")) + _as_list(feedback.get("future_routing_hints")):
            if isinstance(hint, dict):
                key = (
                    str(hint.get("objective_contains", "")),
                    str(hint.get("preferred_domain", "")),
                    str(hint.get("preferred_agent", "")),
                    str(hint.get("approval_policy", "")),
                )

                if key in seen_in_record:
                    continue

                seen_in_record.add(key)
                hints.append(
                    {
                        **hint,
                        "source_file": record.get("path"),
                        "source_group": "learning",
                    }
                )

    return hints


def _collect_used_agents(history: dict[str, Any]) -> list[dict[str, Any]]:
    agents: list[dict[str, Any]] = []

    for record in history.get("learning", []):
        payload = record.get("payload", {})
        feedback = _learning_feedback(payload)

        for agent in _as_list(feedback.get("used_agents")):
            if isinstance(agent, dict):
                agents.append(
                    {
                        **agent,
                        "source_file": record.get("path"),
                    }
                )

    return agents


def _collect_provider_model(history: dict[str, Any]) -> dict[str, Any]:
    providers: list[dict[str, Any]] = []
    models: list[dict[str, Any]] = []

    for record in history.get("learning", []):
        feedback = _learning_feedback(record.get("payload", {}))
        recommendations = feedback.get("provider_model_recommendations", {})

        if isinstance(recommendations, dict):
            providers.extend(_as_list(recommendations.get("provider_recommendations")))
            models.extend(_as_list(recommendations.get("model_recommendations")))

    return {
        "provider_recommendations": [item for item in providers if isinstance(item, dict)],
        "model_recommendations": [item for item in models if isinstance(item, dict)],
    }


def _collect_improvements(history: dict[str, Any]) -> list[dict[str, Any]]:
    improvements: list[dict[str, Any]] = []

    for record in history.get("improvements", []):
        payload = record.get("payload", {})

        for item in _as_list(payload.get("recommended_improvements")):
            if isinstance(item, dict):
                improvements.append(
                    {
                        **item,
                        "source_file": record.get("path"),
                    }
                )

    for record in history.get("learning", []):
        feedback = _learning_feedback(record.get("payload", {}))

        for item in _as_list(feedback.get("recommended_improvements")):
            if isinstance(item, dict):
                improvements.append(
                    {
                        **item,
                        "source_file": record.get("path"),
                    }
                )

    return improvements


def build_adaptive_routing_profile() -> dict:
    history = load_learning_history()
    routing_hints = _collect_routing_hints(history)
    used_agents = _collect_used_agents(history)
    provider_model = _collect_provider_model(history)
    improvements = _collect_improvements(history)

    domain_counts = Counter(
        str(hint.get("preferred_domain"))
        for hint in routing_hints
        if hint.get("preferred_domain")
    )
    agent_counts = Counter(
        str(hint.get("preferred_agent"))
        for hint in routing_hints
        if hint.get("preferred_agent")
    )
    approval_policy_counts = Counter(
        str(hint.get("approval_policy"))
        for hint in routing_hints
        if hint.get("approval_policy")
    )

    for agent in used_agents:
        if agent.get("domain"):
            domain_counts[str(agent["domain"])] += 1

        if agent.get("name"):
            agent_counts[str(agent["name"])] += 1

    return {
        "ok": True,
        "history_counts": history.get("counts", {}),
        "routing_hints": routing_hints,
        "used_agents": used_agents,
        "domain_counts": dict(domain_counts),
        "agent_counts": dict(agent_counts),
        "approval_policy_counts": dict(approval_policy_counts),
        "provider_model_recommendations": provider_model,
        "recommended_improvements": improvements,
        "has_learning_data": bool(history.get("counts", {}).get("total", 0)),
        "history_errors": history.get("errors", []),
        "metadata": {
            "source": "hermes_adaptive_routing",
            "read_only": True,
        },
        "timestamp": utc_now(),
    }


def _normalize(text: str) -> str:
    return (
        text.strip()
        .lower()
        .replace("ä", "ae")
        .replace("ö", "oe")
        .replace("ü", "ue")
        .replace("ß", "ss")
    )


def _tokens(text: str) -> set[str]:
    normalized = _normalize(text)
    raw_tokens = "".join(char if char.isalnum() else " " for char in normalized).split()

    return {token for token in raw_tokens if len(token) >= 3}


def _hint_match_score(task: str, hint: dict[str, Any]) -> float:
    task_text = _normalize(task)
    objective = str(hint.get("objective_contains", "")).strip()
    objective_text = _normalize(objective)

    if not objective_text:
        return 0.0

    if objective_text in task_text or task_text in objective_text:
        return 1.0

    task_tokens = _tokens(task)
    objective_tokens = _tokens(objective)

    if not task_tokens or not objective_tokens:
        return 0.0

    overlap = len(task_tokens & objective_tokens)
    union = len(task_tokens | objective_tokens)

    return overlap / union if union else 0.0


def _fallback_recommendation(task: str, reason: str, profile: dict[str, Any] | None = None) -> dict[str, Any]:
    fallback = decide_route(task)
    preferred_domain = fallback.get("agent_domain") or fallback.get("domain")
    approval_policy = "human_approval_required" if fallback.get("requires_approval") else "none"

    try:
        provider = recommend_provider(
            task=task,
            intent=str(fallback.get("intent", "")),
            cost_sensitive=True,
        )
    except Exception as exc:
        provider = {"ok": False, "error": str(exc)}

    try:
        model = recommend_model(
            task=task,
            intent=str(fallback.get("intent", "")),
            route=str(fallback.get("route", "")),
            cost_sensitive=True,
            offline=False,
        )
    except Exception as exc:
        model = {"ok": False, "error": str(exc)}

    return {
        "ok": True,
        "task": task,
        "preferred_domain": preferred_domain,
        "preferred_agent": f"{preferred_domain}_agent" if preferred_domain else None,
        "approval_policy": approval_policy,
        "confidence": float(fallback.get("confidence", 0.5)),
        "source": reason,
        "fallback_router_decision": fallback,
        "provider_recommendation": provider,
        "model_recommendation": model,
        "adaptive_profile_summary": {
            "has_learning_data": bool((profile or {}).get("has_learning_data", False)),
            "history_counts": (profile or {}).get("history_counts", {}),
        },
        "timestamp": utc_now(),
    }


def recommend_adaptive_route(task: str) -> dict:
    task = task.strip()
    profile = build_adaptive_routing_profile()

    if not task:
        return {
            "ok": False,
            "task": "",
            "preferred_domain": None,
            "preferred_agent": None,
            "approval_policy": "none",
            "confidence": 0.0,
            "source": "error",
            "fallback_router_decision": None,
            "reason": "No task provided.",
            "timestamp": utc_now(),
        }

    if not profile.get("has_learning_data"):
        return _fallback_recommendation(
            task=task,
            reason="fallback_router_no_learning_data",
            profile=profile,
        )

    scored_hints = [
        (score, hint)
        for hint in profile.get("routing_hints", [])
        for score in [_hint_match_score(task, hint)]
        if score > 0
    ]
    scored_hints.sort(key=lambda item: item[0], reverse=True)

    if not scored_hints:
        return _fallback_recommendation(
            task=task,
            reason="fallback_router_no_adaptive_match",
            profile=profile,
        )

    best_score, best_hint = scored_hints[0]
    fallback = decide_route(task)
    preferred_domain = best_hint.get("preferred_domain") or fallback.get("agent_domain") or fallback.get("domain")
    preferred_agent = best_hint.get("preferred_agent") or (f"{preferred_domain}_agent" if preferred_domain else None)
    approval_policy = best_hint.get("approval_policy")

    if not approval_policy:
        approval_policy = "human_approval_required" if fallback.get("requires_approval") else "none"

    support = sum(
        1
        for _, hint in scored_hints
        if hint.get("preferred_domain") == preferred_domain
        or hint.get("preferred_agent") == preferred_agent
        or hint.get("approval_policy") == approval_policy
    )
    confidence = min(0.95, 0.45 + best_score * 0.35 + min(support, 5) * 0.03)

    try:
        provider = recommend_provider(
            task=task,
            intent=str(fallback.get("intent", "")),
            cost_sensitive=True,
        )
    except Exception as exc:
        provider = {"ok": False, "error": str(exc)}

    try:
        model = recommend_model(
            task=task,
            intent=str(fallback.get("intent", "")),
            route=str(fallback.get("route", "")),
            cost_sensitive=True,
            offline=False,
        )
    except Exception as exc:
        model = {"ok": False, "error": str(exc)}

    return {
        "ok": True,
        "task": task,
        "preferred_domain": preferred_domain,
        "preferred_agent": preferred_agent,
        "approval_policy": approval_policy,
        "confidence": round(confidence, 3),
        "source": "adaptive_learning_history",
        "matched_hint": best_hint,
        "matched_hint_score": round(best_score, 3),
        "supporting_matches": support,
        "fallback_router_decision": fallback,
        "provider_recommendation": provider,
        "model_recommendation": model,
        "adaptive_profile_summary": {
            "history_counts": profile.get("history_counts", {}),
            "domain_counts": profile.get("domain_counts", {}),
            "agent_counts": profile.get("agent_counts", {}),
            "approval_policy_counts": profile.get("approval_policy_counts", {}),
        },
        "timestamp": utc_now(),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Hermes Adaptive Routing")
    parser.add_argument("--profile", action="store_true", help="Print adaptive routing profile")
    parser.add_argument("task", nargs="*", help="Task to route adaptively")
    args = parser.parse_args()

    if args.profile:
        result = build_adaptive_routing_profile()
    else:
        task = " ".join(args.task).strip()
        result = recommend_adaptive_route(task)

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
