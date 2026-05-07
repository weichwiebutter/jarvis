#!/usr/bin/env python3
"""
Delegation Executor

Executes Hermes delegation contracts through the Jarvis RuntimeRouter.

Flow:
Hermes
→ Delegation Contract
→ Delegation Executor
→ Runtime Router
→ Agent
→ executor_task
→ Executor Bridge

Important:
- Hermes decides.
- Jarvis Runtime executes.
- Approval-sensitive steps are not executed automatically.
- executor_task outputs are validated through ExecutorBridge.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import Any

from agents.core.runtime_router import RuntimeRouter
from agents.core.executor_bridge import process_executor_task


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
    executor_bridge_result: Any = None
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
    executor_tasks_detected: int
    executor_tasks_executed: int
    results: list[DelegationStepResult]


class DelegationExecutor:
    def __init__(self) -> None:
        self.router = RuntimeRouter()

    def _extract_executor_task(self, agent_result: Any) -> dict[str, Any] | None:
        """
        Detect executor_task output from agent results.

        Supports:
        - dataclass-like objects with .output
        - dictionaries with output
        - direct executor_task dict
        """

        if isinstance(agent_result, dict):
            if agent_result.get("type") == "executor_task":
                return agent_result

            output = agent_result.get("output")
            if isinstance(output, dict) and output.get("type") == "executor_task":
                return output

        output = getattr(agent_result, "output", None)
        if isinstance(output, dict) and output.get("type") == "executor_task":
            return output

        return None

    def execute_contract(
        self,
        contract: dict[str, Any],
        approve_all: bool = False,
        approve_executor_tasks: bool = False,
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
                        executor_bridge_result=None,
                        error="Skipped because approval is required.",
                    )
                )
                continue

            try:
                agent_result = self.router.execute(
                    domain=domain,
                    task=task,
                    context=context,
                )

                executor_task = self._extract_executor_task(agent_result)
                executor_bridge_result = None

                if executor_task is not None:
                    executor_bridge_result = process_executor_task(
                        task_data=executor_task,
                        approve=approve_executor_tasks,
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
                        result=agent_result,
                        executor_bridge_result=executor_bridge_result,
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
                        executor_bridge_result=None,
                        error=str(exc),
                    )
                )

        steps_executed = sum(1 for item in results if item.executed)
        steps_skipped = sum(1 for item in results if item.skipped)

        executor_tasks_detected = sum(
            1 for item in results if item.executor_bridge_result is not None
        )

        executor_tasks_executed = sum(
            1
            for item in results
            if isinstance(item.executor_bridge_result, dict)
            and item.executor_bridge_result.get("executed") is True
        )

        errors = [item for item in results if item.error and not item.skipped]

        final = DelegationExecutionResult(
            ok=len(errors) == 0,
            timestamp=utc_now(),
            objective=objective,
            execution_policy=execution_policy,
            steps_total=len(results),
            steps_executed=steps_executed,
            steps_skipped=steps_skipped,
            executor_tasks_detected=executor_tasks_detected,
            executor_tasks_executed=executor_tasks_executed,
            results=results,
        )

        return asdict(final)


def execute_delegation_contract(
    contract: dict[str, Any],
    approve_all: bool = False,
    approve_executor_tasks: bool = False,
) -> dict[str, Any]:
    executor = DelegationExecutor()

    return executor.execute_contract(
        contract=contract,
        approve_all=approve_all,
        approve_executor_tasks=approve_executor_tasks,
    )
