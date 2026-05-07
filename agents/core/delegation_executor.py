#!/usr/bin/env python3
"""
Delegation Executor

Executes Hermes delegation contracts through the Jarvis RuntimeRouter.

Important:
- Hermes decides.
- Jarvis Runtime executes.
- Approval-sensitive steps are not executed automatically.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import Any

from agents.core.runtime_router import RuntimeRouter


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class DelegationStepResult:
    step_id: int
    domain: str
    task: str
    executed: bool
    skipped: bool
    requires_approval: bool
    approval_reason: str | None
    result: Any = None
    error: str | None = None


@dataclass
class DelegationExecutionResult:
    ok: bool
    timestamp: str
    objective: str
    execution_policy: str
    steps_total: int
    steps_executed: int
    steps_skipped: int
    results: list[DelegationStepResult]


class DelegationExecutor:
    def __init__(self) -> None:
        self.router = RuntimeRouter()

    def execute_contract(
        self,
        contract: dict[str, Any],
        approve_all: bool = False,
    ) -> dict[str, Any]:
        objective = str(contract.get("objective", ""))
        execution_policy = str(
            contract.get("execution_policy", "human_approval_required")
        )
        steps = contract.get("steps", [])

        results: list[DelegationStepResult] = []

        for raw_step in steps:
            step_id = int(raw_step.get("step_id", 0))
            domain = str(raw_step.get("domain", ""))
            task = str(raw_step.get("task", ""))
            context = raw_step.get("context", {}) or {}
            requires_approval = bool(raw_step.get("requires_approval", True))
            approval_reason = raw_step.get("approval_reason")

            if requires_approval and not approve_all:
                results.append(
                    DelegationStepResult(
                        step_id=step_id,
                        domain=domain,
                        task=task,
                        executed=False,
                        skipped=True,
                        requires_approval=True,
                        approval_reason=approval_reason,
                        result=None,
                        error="Skipped because approval is required.",
                    )
                )
                continue

            try:
                result = self.router.execute(
                    domain=domain,
                    task=task,
                    context=context,
                )

                results.append(
                    DelegationStepResult(
                        step_id=step_id,
                        domain=domain,
                        task=task,
                        executed=True,
                        skipped=False,
                        requires_approval=requires_approval,
                        approval_reason=approval_reason,
                        result=result,
                        error=None,
                    )
                )

            except Exception as exc:
                results.append(
                    DelegationStepResult(
                        step_id=step_id,
                        domain=domain,
                        task=task,
                        executed=False,
                        skipped=False,
                        requires_approval=requires_approval,
                        approval_reason=approval_reason,
                        result=None,
                        error=str(exc),
                    )
                )

        executed = sum(1 for item in results if item.executed)
        skipped = sum(1 for item in results if item.skipped)
        errors = [item for item in results if item.error and not item.skipped]

        final = DelegationExecutionResult(
            ok=len(errors) == 0,
            timestamp=utc_now(),
            objective=objective,
            execution_policy=execution_policy,
            steps_total=len(results),
            steps_executed=executed,
            steps_skipped=skipped,
            results=results,
        )

        return asdict(final)


def execute_delegation_contract(
    contract: dict[str, Any],
    approve_all: bool = False,
) -> dict[str, Any]:
    executor = DelegationExecutor()
    return executor.execute_contract(
        contract=contract,
        approve_all=approve_all,
    )
