#!/usr/bin/env python3
"""
Hermes Decision Layer

Converts Hermes routing decisions into executable delegation contracts.

Jarvis = interface/runtime/control
Hermes = brain/decision/delegation
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



def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class HermesDecision:
    ok: bool
    objective: str
    domain: str
    intent: str
    route: str
    model_preference: str | None
    agent_domain: str
    priority: str
    requires_approval: bool
    approval_reason: str | None
    memory_required: bool
    executor_required: bool
    reasoning: str
    steps: list[dict[str, Any]] = field(default_factory=list)
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def build_default_decision(
    objective: str,
    domain: str = "office",
    intent: str = "chat",
    route: str = "ollama",
    agent_domain: str = "office",
    reasoning: str = "Default local-safe decision.",
) -> dict[str, Any]:
    decision = HermesDecision(
        ok=True,
        objective=objective,
        domain=domain,
        intent=intent,
        route=route,
        model_preference=None,
        agent_domain=agent_domain,
        priority="normal",
        requires_approval=True,
        approval_reason="Default safety policy.",
        memory_required=False,
        executor_required=False,
        reasoning=reasoning,
        steps=[
            {
                "step_id": 1,
                "domain": agent_domain,
                "task": objective,
                "requires_approval": True,
                "approval_reason": "Default safety policy.",
                "context": {
                    "source": "hermes_decision",
                },
            }
        ],
    )

    return decision.to_dict()


def build_decision_from_router(task: str) -> dict[str, Any]:
    from agents.core.hermes_router import decide_route

    route = decide_route(task)

    requires_approval = bool(route.get("requires_approval", True))
    approval_reason = route.get("approval_reason")

    decision = HermesDecision(
        ok=bool(route.get("ok", True)),
        objective=task,
        domain=str(route.get("domain", "office")),
        intent=str(route.get("intent", "chat")),
        route=str(route.get("route", "ollama")),
        model_preference=route.get("model_preference"),
        agent_domain=str(route.get("agent_domain", route.get("domain", "office"))),
        priority=str(route.get("priority", "normal")),
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        memory_required=bool(route.get("memory_required", False)),
        executor_required=bool(route.get("executor_required", False)),
        reasoning=str(route.get("reasoning", "Router decision.")),
        steps=[
            {
                "step_id": 1,
                "domain": str(route.get("agent_domain", route.get("domain", "office"))),
                "task": task,
                "requires_approval": requires_approval,
                "approval_reason": approval_reason,
                "context": {
                    "source": "hermes_router",
                    "intent": route.get("intent"),
                    "route": route.get("route"),
                    "model_preference": route.get("model_preference"),
                    "priority": route.get("priority"),
                    "memory_required": route.get("memory_required"),
                    "executor_required": route.get("executor_required"),
                    "confidence": route.get("confidence"),
                    "router_metadata": route.get("metadata", {}),
                },
            }
        ],
        metadata={
            "router_decision": route,
            "source": "hermes_decision",
        },
    )

    return decision.to_dict()


def decision_to_delegation_contract(decision: dict[str, Any]) -> dict[str, Any]:
    from agents.core.delegation_contract import DelegationContract, DelegationStep

    steps = []

    for raw_step in decision.get("steps", []):
        steps.append(
            DelegationStep(
                step_id=int(raw_step.get("step_id", len(steps) + 1)),
                domain=str(raw_step.get("domain", decision.get("agent_domain", "office"))),
                task=str(raw_step.get("task", decision.get("objective", ""))),
                context=raw_step.get("context", {}) or {},
                requires_approval=bool(raw_step.get("requires_approval", True)),
                approval_reason=raw_step.get("approval_reason"),
            )
        )

    contract = DelegationContract(
        source="hermes_decision",
        objective=str(decision.get("objective", "")),
        created_by="hermes",
        execution_policy="human_approval_required",
        steps=steps,
        metadata={
            "route": decision.get("route"),
            "model_preference": decision.get("model_preference"),
            "priority": decision.get("priority"),
            "memory_required": decision.get("memory_required"),
            "executor_required": decision.get("executor_required"),
            "reasoning": decision.get("reasoning"),
            "router_decision": decision.get("metadata", {}).get("router_decision"),
        },
    )

    return contract.to_dict()


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Hermes Decision Layer")
    parser.add_argument("task", nargs="*", help="Task to decide")
    args = parser.parse_args()

    task = " ".join(args.task).strip()

    if not task:
        print("Kein Task angegeben.")
        return 1

    decision = build_decision_from_router(task)
    contract = decision_to_delegation_contract(decision)

    print(json.dumps(
        {
            "decision": decision,
            "contract": contract,
        },
        indent=2,
        ensure_ascii=False,
        default=str,
    ))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
