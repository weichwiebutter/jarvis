#!/usr/bin/env python3
"""
Hermes Learning Feedback Loop

Builds structured learning feedback from Hermes execution results.

This module prepares memory_write executor tasks but does not execute them.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict, is_dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.hermes_execution_engine import execute_objective
from agents.core.runtime_router import RuntimeRouter

try:
    from agents.core.memory.memory_agent import MemoryRequest, build_write_result
except ModuleNotFoundError:
    from agents.memory.memory_agent import MemoryRequest, build_write_result


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _as_dict(value: Any) -> dict[str, Any]:
    if isinstance(value, dict):
        return value

    if is_dataclass(value):
        return asdict(value)

    if hasattr(value, "__dict__"):
        return dict(value.__dict__)

    return {}


def _walk_dicts(value: Any) -> list[dict[str, Any]]:
    found: list[dict[str, Any]] = []

    if isinstance(value, dict):
        found.append(value)

        for item in value.values():
            found.extend(_walk_dicts(item))

    elif isinstance(value, list):
        for item in value:
            found.extend(_walk_dicts(item))

    return found


def _extract_provider_model_recommendations(execution_result: dict[str, Any]) -> dict[str, Any]:
    provider_recommendations: list[dict[str, Any]] = []
    model_recommendations: list[dict[str, Any]] = []

    for item in _walk_dicts(execution_result):
        provider = item.get("provider_recommendation")
        model = item.get("model_recommendation")

        if isinstance(provider, dict):
            provider_recommendations.append(provider)

        if isinstance(model, dict):
            model_recommendations.append(model)

    return {
        "provider_recommendations": provider_recommendations,
        "model_recommendations": model_recommendations,
        "provider_recommendations_found": len(provider_recommendations),
        "model_recommendations_found": len(model_recommendations),
    }


def _extract_steps(execution_result: dict[str, Any]) -> list[dict[str, Any]]:
    step_results = (
        execution_result
        .get("execution_result", {})
        .get("step_results", [])
    )

    if isinstance(step_results, list):
        return [_as_dict(item) for item in step_results]

    return []


def _extract_used_agents(execution_result: dict[str, Any]) -> list[dict[str, Any]]:
    agents: dict[str, dict[str, Any]] = {}

    for item in _walk_dicts(execution_result.get("orchestration", {})):
        agent = item.get("matched_agent")

        if isinstance(agent, dict) and agent.get("name"):
            agents[str(agent["name"])] = {
                "name": agent.get("name"),
                "domain": agent.get("domain"),
                "module_path": agent.get("module_path"),
                "class_name": agent.get("class_name"),
            }

    return list(agents.values())


def _approval_status(execution_summary: dict[str, Any], steps: list[dict[str, Any]]) -> dict[str, Any]:
    approval_required = sum(
        1
        for step in steps
        if bool(step.get("metadata", {}).get("approval_required", True))
    )
    approved_execution = bool(execution_summary.get("approve_all", False))

    return {
        "approval_required_steps": approval_required,
        "approve_all": approved_execution,
        "status": execution_summary.get("status"),
        "blocked_by_approval": (
            approval_required > 0
            and not approved_execution
            and int(execution_summary.get("skipped_steps", 0)) > 0
        ),
    }


def _build_patterns(
    objective: str,
    execution_summary: dict[str, Any],
    steps: list[dict[str, Any]],
    used_agents: list[dict[str, Any]],
    approval_status: dict[str, Any],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    executed_steps = int(execution_summary.get("executed_steps", 0))
    skipped_steps = int(execution_summary.get("skipped_steps", 0))
    failed_steps = int(execution_summary.get("failed_steps", 0))

    success_patterns: list[dict[str, Any]] = []
    failure_patterns: list[dict[str, Any]] = []

    if executed_steps > 0:
        success_patterns.append(
            {
                "pattern": "approved_steps_executed",
                "objective": objective,
                "evidence": {
                    "executed_steps": executed_steps,
                    "agents": used_agents,
                },
                "learning": "Approved delegation steps can be routed through the execution engine.",
            }
        )

    if failed_steps > 0:
        failed_step_details = [
            {
                "step_id": step.get("step_id"),
                "metadata": step.get("metadata", {}),
                "error": step.get("error"),
            }
            for step in steps
            if step.get("failed")
        ]

        failure_patterns.append(
            {
                "pattern": "delegation_step_failure",
                "objective": objective,
                "evidence": {
                    "failed_steps": failed_steps,
                    "failed_step_details": failed_step_details,
                },
                "learning": "One or more delegated steps failed and should influence future routing or planning.",
            }
        )

    return success_patterns, failure_patterns


def _build_improvements(
    execution_summary: dict[str, Any],
    provider_model_recommendations: dict[str, Any],
    approval_status: dict[str, Any],
) -> list[dict[str, Any]]:
    improvements: list[dict[str, Any]] = []

    if approval_status["blocked_by_approval"]:
        improvements.append(
            {
                "area": "approval_flow",
                "recommendation": "Expose approval state clearly before running execution steps.",
                "reason": "Execution was blocked by approval requirements.",
            }
        )

    if int(execution_summary.get("failed_steps", 0)) > 0:
        improvements.append(
            {
                "area": "execution_reliability",
                "recommendation": "Review failed step domains and add more specific recovery routing.",
                "reason": "Failed steps were detected in the execution result.",
            }
        )

    if provider_model_recommendations["provider_recommendations_found"] == 0:
        improvements.append(
            {
                "area": "provider_model_traceability",
                "recommendation": "Attach provider/model recommendation metadata to future Hermes execution outputs.",
                "reason": "No provider/model recommendations were present in the execution result.",
            }
        )

    return improvements


def _build_routing_hints(
    objective: str,
    execution_summary: dict[str, Any],
    used_agents: list[dict[str, Any]],
    provider_model_recommendations: dict[str, Any],
) -> list[dict[str, Any]]:
    hints: list[dict[str, Any]] = []

    for agent in used_agents:
        hints.append(
            {
                "objective_contains": objective,
                "preferred_domain": agent.get("domain"),
                "preferred_agent": agent.get("name"),
                "reason": "Agent was selected during orchestration for this objective.",
            }
        )

    if execution_summary.get("status") == "approval_required":
        hints.append(
            {
                "objective_contains": objective,
                "approval_policy": "human_approval_required",
                "reason": "Similar objectives should surface approval requirements before execution.",
            }
        )

    if provider_model_recommendations["provider_recommendations_found"]:
        hints.append(
            {
                "objective_contains": objective,
                "provider_model_recommendations": provider_model_recommendations,
                "reason": "Reuse provider/model recommendations when matching similar objectives.",
            }
        )

    return hints


def _prepare_memory_candidate(
    *,
    objective: str,
    category: str,
    title: str,
    content: str,
    tags: list[str],
) -> dict[str, Any]:
    request = MemoryRequest(
        task=f"Merk dir: {content}",
        category=category,
        context="Hermes learning feedback",
        metadata={
            "source": "hermes_learning_feedback",
            "title": title,
            "tags": tags,
            "objective": objective,
        },
    )
    memory_result = build_write_result(request)
    executor_task = _as_dict(memory_result).get("output", {})

    return {
        "category": category,
        "title": title,
        "content": content,
        "executor_task": executor_task,
        "requires_approval": bool(executor_task.get("requires_approval", True)),
        "prepared_only": True,
    }


def _build_memory_candidates(
    objective: str,
    success_patterns: list[dict[str, Any]],
    failure_patterns: list[dict[str, Any]],
    recommended_improvements: list[dict[str, Any]],
    routing_hints: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []

    for pattern in success_patterns:
        candidates.append(
            _prepare_memory_candidate(
                objective=objective,
                category="learnings",
                title="Hermes Success Pattern",
                content=json.dumps(pattern, ensure_ascii=False, default=str),
                tags=["hermes", "learning", "success"],
            )
        )

    for pattern in failure_patterns:
        candidates.append(
            _prepare_memory_candidate(
                objective=objective,
                category="learnings",
                title="Hermes Failure Pattern",
                content=json.dumps(pattern, ensure_ascii=False, default=str),
                tags=["hermes", "learning", "failure"],
            )
        )

    if recommended_improvements:
        candidates.append(
            _prepare_memory_candidate(
                objective=objective,
                category="tasks",
                title="Hermes Recommended Improvements",
                content=json.dumps(recommended_improvements, ensure_ascii=False, default=str),
                tags=["hermes", "improvement"],
            )
        )

    if routing_hints:
        candidates.append(
            _prepare_memory_candidate(
                objective=objective,
                category="decisions",
                title="Hermes Future Routing Hints",
                content=json.dumps(routing_hints, ensure_ascii=False, default=str),
                tags=["hermes", "routing"],
            )
        )

    return candidates


def build_learning_feedback(execution_result: dict) -> dict:
    execution_result = _as_dict(execution_result)
    objective = str(execution_result.get("objective", "")).strip()
    execution_summary = _as_dict(execution_result.get("execution_summary", {}))
    execution_payload = _as_dict(execution_result.get("execution_result", {}))
    steps = _extract_steps(execution_result)
    used_agents = _extract_used_agents(execution_result)
    provider_model_recommendations = _extract_provider_model_recommendations(execution_result)
    approval_status = _approval_status(execution_summary, steps)
    runtime_router = RuntimeRouter()

    success_patterns, failure_patterns = _build_patterns(
        objective=objective,
        execution_summary=execution_summary,
        steps=steps,
        used_agents=used_agents,
        approval_status=approval_status,
    )
    recommended_improvements = _build_improvements(
        execution_summary=execution_summary,
        provider_model_recommendations=provider_model_recommendations,
        approval_status=approval_status,
    )
    future_routing_hints = _build_routing_hints(
        objective=objective,
        execution_summary=execution_summary,
        used_agents=used_agents,
        provider_model_recommendations=provider_model_recommendations,
    )
    memory_candidates = _build_memory_candidates(
        objective=objective,
        success_patterns=success_patterns,
        failure_patterns=failure_patterns,
        recommended_improvements=recommended_improvements,
        routing_hints=future_routing_hints,
    )

    learning_feedback = {
        "objective": objective,
        "execution_summary": execution_summary,
        "executed_steps": int(execution_payload.get("executed_steps", 0)),
        "skipped_steps": int(execution_payload.get("skipped_steps", 0)),
        "failed_steps": int(execution_payload.get("failed_steps", 0)),
        "provider_model_recommendations": provider_model_recommendations,
        "used_agents": used_agents,
        "approval_status": approval_status,
        "success_patterns": success_patterns,
        "failure_patterns": failure_patterns,
        "recommended_improvements": recommended_improvements,
        "future_routing_hints": future_routing_hints,
    }

    return {
        "ok": bool(execution_result.get("ok", False)),
        "objective": objective,
        "learning_feedback": learning_feedback,
        "memory_candidates": memory_candidates,
        "routing_hints": future_routing_hints,
        "metadata": {
            "source": "hermes_learning_feedback",
            "runtime_router": runtime_router.__class__.__name__,
            "memory_candidates_are_prepared_only": True,
            "memory_candidate_task_type": "memory_write",
            "human_in_the_loop": True,
        },
        "timestamp": utc_now(),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Hermes Learning Feedback")
    parser.add_argument("objective", nargs="*", help="Objective to execute and analyze")
    args = parser.parse_args()

    objective = " ".join(args.objective).strip()
    execution_result = execute_objective(objective)
    feedback = build_learning_feedback(execution_result)

    print(json.dumps(feedback, indent=2, ensure_ascii=False, default=str))
    return 0 if feedback.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
