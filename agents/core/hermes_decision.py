#!/usr/bin/env python3
"""
Hermes Decision Layer

Defines the structured decision format Hermes should produce.

Jarvis = interface/runtime
Hermes = brain/decision/delegation
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


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
        },
    )

    return contract.to_dict()
