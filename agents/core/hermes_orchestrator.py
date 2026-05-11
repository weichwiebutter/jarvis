#!/usr/bin/env python3
"""
Hermes Multi-Step Orchestrator

Turns a larger Hermes objective into approval-controlled delegation steps.

This module only plans. It does not execute actions, write runtime data, or
create agents. Missing capabilities are returned as AgentCreationRequest data
for later human-approved handling.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.agent_creation_request import infer_agent_request
from agents.core.capability_registry import find_best_agent_for_task
from agents.core.delegation_contract import DelegationContract, DelegationStep
from agents.core.hermes_planner import plan_objective


STEP_ORDER = [
    "research",
    "business",
    "coding",
    "memory",
    "improvement",
]

DOMAIN_KEYWORDS = {
    "research": [
        "research",
        "recherche",
        "recherchiere",
        "source",
        "quelle",
        "analysis",
        "analyse",
        "compare",
        "vergleich",
        "internet",
        "web",
    ],
    "business": [
        "business",
        "strategy",
        "strategie",
        "roadmap",
        "planning",
        "planung",
        "plan",
        "process",
        "prozess",
        "scope",
        "ziel",
        "prioritaet",
    ],
    "coding": [
        "build",
        "baue",
        "implement",
        "code",
        "coding",
        "python",
        "script",
        "api",
        "function",
        "funktion",
        "feature",
        "test",
        "debug",
        "fix",
        "repariere",
    ],
    "memory": [
        "memory",
        "gedaechtnis",
        "merk dir",
        "speicher",
        "speichere",
        "obsidian",
        "learning",
        "lernen",
    ],
    "improvement": [
        "improvement",
        "verbessere",
        "optimier",
        "quality",
        "qualitaet",
        "architecture",
        "architektur",
        "ui",
        "interface",
        "dashboard",
        "voice",
        "wake word",
        "audio",
        "visualizer",
        "visualiser",
        "whisper",
        "tts",
        "mikrofon",
        "system",
    ],
}

COMPLEXITY_KEYWORDS = [
    "multi-step",
    "multi step",
    "mehrere",
    "koordiniert",
    "project",
    "projekt",
    "masterplan",
    "roadmap",
    "end-to-end",
    "komplett",
]


@dataclass(frozen=True)
class StepBlueprint:
    domain: str
    task: str
    purpose: str
    match_query: str

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _normalize(text: str) -> str:
    return text.strip().lower().replace("ä", "ae").replace("ö", "oe").replace("ü", "ue")


def _contains_any(text: str, keywords: list[str]) -> bool:
    return any(keyword in text for keyword in keywords)


def _detect_domain_signals(objective: str) -> list[str]:
    text = _normalize(objective)
    signals: list[str] = []

    for domain in STEP_ORDER:
        if _contains_any(text, DOMAIN_KEYWORDS[domain]):
            signals.append(domain)

    return signals


def _is_complex_objective(objective: str, domain_signals: list[str]) -> bool:
    text = _normalize(objective)
    word_count = len(text.split())
    connector_count = text.count(" und ") + text.count(" and ") + text.count(" mit ")
    has_separators = "," in text or ";" in text or ":" in text
    has_complexity_keyword = _contains_any(text, COMPLEXITY_KEYWORDS)

    return (
        len(domain_signals) >= 2
        or has_complexity_keyword
        or (word_count >= 10 and connector_count > 0)
        or (word_count >= 12 and has_separators)
    )


def _build_complex_step_blueprints(objective: str, domain_signals: list[str]) -> list[StepBlueprint]:
    blueprints: list[StepBlueprint] = []

    def add(domain: str, task: str, purpose: str, match_query: str) -> None:
        if not any(step.domain == domain for step in blueprints):
            blueprints.append(
                StepBlueprint(
                    domain=domain,
                    task=task,
                    purpose=purpose,
                    match_query=match_query,
                )
            )

    add(
        "research",
        f"Research constraints, implementation options, dependencies, and risks for: {objective}",
        "Collect the information needed before planning or implementation.",
        "research planning source analysis web research planning summary generation",
    )
    add(
        "business",
        f"Create a phased plan, scope, dependencies, success criteria, and approval checkpoints for: {objective}",
        "Turn the objective into an ordered plan with clear decision points.",
        "business strategy planning roadmap planning decision support process analysis",
    )

    if "coding" in domain_signals:
        add(
            "coding",
            f"Prepare the coding implementation plan, file-change proposal, and validation plan for: {objective}",
            "Map the planned work to code changes without executing them.",
            "coding code planning python planning file change proposal test plan generation",
        )

    if "memory" in domain_signals:
        add(
            "memory",
            f"Plan what requirements, decisions, and follow-up facts should be captured in memory for: {objective}",
            "Prepare approval-controlled memory capture for continuity.",
            "memory write request learning capture context recall obsidian memory planning",
        )

    add(
        "improvement",
        f"Review architecture, system quality, UX, and improvement risks for: {objective}",
        "Check the coordinated plan for quality, safety, and future maintainability.",
        "improvement system improvement architecture review quality analysis voice system planning ui improvement planning",
    )

    return blueprints


def _build_agent_creation_response(
    objective: str,
    missing_task: str,
    planner_result: dict[str, Any],
    reasoning: str,
    metadata: dict[str, Any] | None = None,
) -> dict[str, Any]:
    request = infer_agent_request(missing_task)

    return {
        "ok": True,
        "objective": objective,
        "mode": "agent_creation_request",
        "steps_total": 0,
        "delegation_contract": None,
        "agent_creation_request": request,
        "reasoning": reasoning,
        "metadata": {
            "orchestrator": "hermes_orchestrator",
            "planner_result": planner_result,
            "missing_task": missing_task,
            "human_in_the_loop": True,
            "timestamp": utc_now(),
            **(metadata or {}),
        },
    }


def _single_step_response(objective: str, planner_result: dict[str, Any]) -> dict[str, Any]:
    contract = planner_result.get("delegation_contract")
    request = planner_result.get("agent_creation_request")

    if request:
        return {
            "ok": True,
            "objective": objective,
            "mode": "agent_creation_request",
            "steps_total": 0,
            "delegation_contract": None,
            "agent_creation_request": request,
            "reasoning": planner_result.get("reasoning", "No existing agent matched the objective."),
            "metadata": {
                "orchestrator": "hermes_orchestrator",
                "planner_result": planner_result,
                "human_in_the_loop": True,
                "timestamp": utc_now(),
            },
        }

    steps = (contract or {}).get("steps", [])

    return {
        "ok": bool(planner_result.get("ok", False)),
        "objective": objective,
        "mode": "single_step_delegation_contract",
        "steps_total": len(steps),
        "delegation_contract": contract,
        "agent_creation_request": None,
        "reasoning": planner_result.get(
            "reasoning",
            "Hermes planner produced a single approval-controlled delegation contract.",
        ),
        "metadata": {
            "orchestrator": "hermes_orchestrator",
            "planner_result": planner_result,
            "human_in_the_loop": True,
            "timestamp": utc_now(),
        },
    }


def _build_multi_step_contract(
    objective: str,
    blueprints: list[StepBlueprint],
    matches: list[dict[str, Any]],
    planner_result: dict[str, Any],
) -> dict[str, Any]:
    steps: list[DelegationStep] = []

    for index, (blueprint, match) in enumerate(zip(blueprints, matches), start=1):
        agent = match.get("agent") or {}
        domain = str(agent.get("domain") or blueprint.domain)

        steps.append(
            DelegationStep(
                step_id=index,
                domain=domain,
                task=blueprint.task,
                agent=str(agent.get("name", "")),
                context={
                    "source": "hermes_orchestrator",
                    "parent_objective": objective,
                    "intended_domain": blueprint.domain,
                    "purpose": blueprint.purpose,
                    "match_query": blueprint.match_query,
                    "matched_agent": agent,
                    "matched_capabilities": match.get("matched", []),
                    "match_score": match.get("score", 0),
                },
                requires_approval=True,
                approval_reason=(
                    "Hermes multi-step orchestration requires human approval "
                    f"before executing step {index}."
                ),
            )
        )

    contract = DelegationContract(
        source="hermes_orchestrator",
        objective=objective,
        created_by="hermes",
        execution_policy="human_approval_required",
        steps=steps,
        metadata={
            "orchestrator": "hermes_orchestrator",
            "planner_result": planner_result,
            "step_blueprints": [step.to_dict() for step in blueprints],
            "agent_matches": matches,
            "all_steps_require_approval": True,
            "human_in_the_loop": True,
        },
    )

    return contract.to_dict()


def orchestrate_objective(objective: str) -> dict[str, Any]:
    objective = objective.strip()

    if not objective:
        return {
            "ok": False,
            "objective": "",
            "mode": "error",
            "steps_total": 0,
            "delegation_contract": None,
            "agent_creation_request": None,
            "reasoning": "No objective provided.",
            "metadata": {
                "orchestrator": "hermes_orchestrator",
                "human_in_the_loop": True,
                "timestamp": utc_now(),
            },
        }

    planner_result = plan_objective(objective)
    domain_signals = _detect_domain_signals(objective)
    overall_match = find_best_agent_for_task(objective)

    if not overall_match.get("found") and not domain_signals:
        return _build_agent_creation_response(
            objective=objective,
            missing_task=objective,
            planner_result=planner_result,
            reasoning=(
                "No existing agent capability matched the objective. "
                "Hermes returns an agent creation request instead of a delegation contract."
            ),
            metadata={
                "domain_signals": domain_signals,
                "overall_match": overall_match,
            },
        )

    if not _is_complex_objective(objective, domain_signals):
        return _single_step_response(objective, planner_result)

    blueprints = _build_complex_step_blueprints(objective, domain_signals)
    matches: list[dict[str, Any]] = []

    for blueprint in blueprints:
        match = find_best_agent_for_task(blueprint.match_query)

        if not match.get("found"):
            return _build_agent_creation_response(
                objective=objective,
                missing_task=blueprint.task,
                planner_result=planner_result,
                reasoning=(
                    "A planned orchestration step has no matching existing agent. "
                    "Hermes returns an agent creation request before building a contract."
                ),
                metadata={
                    "domain_signals": domain_signals,
                    "overall_match": overall_match,
                    "step_blueprints": [step.to_dict() for step in blueprints],
                    "unresolved_step": blueprint.to_dict(),
                    "steps_total": len(blueprints),
                },
            )

        matches.append(match)

    contract = _build_multi_step_contract(
        objective=objective,
        blueprints=blueprints,
        matches=matches,
        planner_result=planner_result,
    )

    return {
        "ok": True,
        "objective": objective,
        "mode": "multi_step_delegation_contract",
        "steps_total": len(contract.get("steps", [])),
        "delegation_contract": contract,
        "agent_creation_request": None,
        "reasoning": (
            "Hermes detected a complex objective and split it into coordinated, "
            "approval-controlled delegation steps."
        ),
        "metadata": {
            "orchestrator": "hermes_orchestrator",
            "planner_result": planner_result,
            "domain_signals": domain_signals,
            "overall_match": overall_match,
            "all_steps_require_approval": True,
            "human_in_the_loop": True,
            "timestamp": utc_now(),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Hermes Multi-Step Orchestrator")
    parser.add_argument("objective", nargs="*", help="Objective to orchestrate")
    args = parser.parse_args()

    objective = " ".join(args.objective).strip()
    result = orchestrate_objective(objective)

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
