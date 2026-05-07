#!/usr/bin/env python3
"""
Delegation Contract

Structured instruction format produced by Hermes and executed by Jarvis Runtime.

Jarvis = interface/control/runtime
Hermes = brain/delegation/learning
Agents = specialists
Executor = approved actions only
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any, Optional


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class DelegationStep:
    step_id: int
    domain: str
    task: str
    agent: Optional[str] = None
    context: dict[str, Any] = field(default_factory=dict)
    requires_approval: bool = True
    approval_reason: Optional[str] = None


@dataclass
class DelegationContract:
    source: str
    objective: str
    created_by: str = "hermes"
    execution_policy: str = "human_approval_required"
    steps: list[DelegationStep] = field(default_factory=list)
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def build_single_step_contract(
    objective: str,
    domain: str,
    task: str,
    source: str = "hermes",
    requires_approval: bool = True,
    approval_reason: Optional[str] = None,
    context: Optional[dict[str, Any]] = None,
) -> dict[str, Any]:

    contract = DelegationContract(
        source=source,
        objective=objective,
        steps=[
            DelegationStep(
                step_id=1,
                domain=domain,
                task=task,
                context=context or {},
                requires_approval=requires_approval,
                approval_reason=approval_reason,
            )
        ],
    )

    return contract.to_dict()
