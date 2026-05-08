#!/usr/bin/env python3
"""
Hermes Planner

Multi-step planning layer for Jarvis/Hermes.

Purpose:
- Hermes receives an objective
- checks existing agent capabilities
- creates delegation steps for matching agents
- proposes new agent creation if no capability exists
- never writes files directly
- always keeps human-in-the-loop active for system changes

Jarvis = interface/runtime/control
Hermes = brain/planning/delegation/learning
Agents = specialists
Executor = approved action layer
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from pathlib import Path
import sys
from typing import Any
PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.capability_registry import (
    detect_capability_gap,
    find_best_agent_for_task,
)
from agents.core.agent_creation_request import infer_agent_request
from agents.core.delegation_contract import DelegationContract, DelegationStep


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class HermesPlannerResult:
    ok: bool
    objective: str
    mode: str
    capability_gap_detected: bool
    recommended_action: str
    delegation_contract: dict[str, Any] | None = None
    agent_creation_request: dict[str, Any] | None = None
    reasoning: str = ""
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def build_delegation_for_existing_agent(
    objective: str,
    best_match: dict[str, Any],
) -> dict[str, Any]:
    agent = best_match.get("agent") or {}
    domain = str(agent.get("domain", "office"))

    contract = DelegationContract(
        source="hermes_planner",
        objective=objective,
        created_by="hermes",
        execution_policy="human_approval_required",
        steps=[
            DelegationStep(
                step_id=1,
                domain=domain,
                task=objective,
                agent=str(agent.get("name", "")),
                context={
                    "source": "hermes_planner",
                    "matched_agent": agent,
                    "matched_capabilities": best_match.get("matched", []),
                    "match_score": best_match.get("score", 0),
                },
                requires_approval=True,
                approval_reason="Hermes planner delegation requires approval.",
            )
        ],
        metadata={
            "planner": "hermes_planner",
            "best_match": best_match,
            "human_in_the_loop": True,
        },
    )

    return contract.to_dict()


def plan_objective(objective: str) -> dict[str, Any]:
    objective = objective.strip()

    if not objective:
        return HermesPlannerResult(
            ok=False,
            objective="",
            mode="error",
            capability_gap_detected=False,
            recommended_action="none",
            reasoning="No objective provided.",
        ).to_dict()

    gap = detect_capability_gap(objective)

    if gap.get("gap_detected"):
        creation_request = infer_agent_request(objective)

        return HermesPlannerResult(
            ok=True,
            objective=objective,
            mode="agent_creation_request",
            capability_gap_detected=True,
            recommended_action="request_new_agent_approval",
            delegation_contract=None,
            agent_creation_request=creation_request,
            reasoning=(
                "No existing agent capability matched the objective. "
                "Hermes proposes creating a new specialist agent. "
                "No files are written without approval."
            ),
            metadata={
                "capability_gap": gap,
                "human_in_the_loop": True,
            },
        ).to_dict()

    best_match = gap.get("best_match") or find_best_agent_for_task(objective)
    contract = build_delegation_for_existing_agent(objective, best_match)

    return HermesPlannerResult(
        ok=True,
        objective=objective,
        mode="delegation_contract",
        capability_gap_detected=False,
        recommended_action="execute_delegation_with_approval",
        delegation_contract=contract,
        agent_creation_request=None,
        reasoning=(
            "Existing agent capability matched the objective. "
            "Hermes created a delegation contract for approval-controlled execution."
        ),
        metadata={
            "capability_gap": gap,
            "best_match": best_match,
            "human_in_the_loop": True,
        },
    ).to_dict()


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Hermes Planner")
    parser.add_argument("objective", nargs="*", help="Objective to plan")
    args = parser.parse_args()

    objective = " ".join(args.objective).strip()

    if not objective:
        print("Kein Objective angegeben.")
        return 1

    result = plan_objective(objective)
    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
