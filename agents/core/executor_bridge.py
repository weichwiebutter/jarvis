#!/usr/bin/env python3
"""
Executor Bridge

Responsible for safely processing executor_task outputs
coming from delegated agent execution.

Important architecture rules:
- Hermes decides and plans
- Agents prepare structured tasks
- Executor Bridge validates
- Executor executes only approved actions
"""

from __future__ import annotations

from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class ExecutorBridgeResult:
    ok: bool
    timestamp: str
    accepted: bool
    requires_approval: bool
    executed: bool
    task_type: str | None
    task_name: str | None
    payload: dict[str, Any] | None
    error: str | None = None


class ExecutorBridge:
    """
    Validates executor tasks before actual execution.
    """

    ALLOWED_TYPES = {
        "executor_task",
    }

    ALLOWED_TASKS = {
        "memory_write",
        "system_status",
        "briefing_generate",
        "market_briefing",
    }

    def validate(self, task_data: dict[str, Any]) -> ExecutorBridgeResult:
        task_type = task_data.get("type")
        task_name = task_data.get("task_name")
        payload = task_data.get("payload")

        if task_type not in self.ALLOWED_TYPES:
            return ExecutorBridgeResult(
                ok=False,
                timestamp=utc_now(),
                accepted=False,
                requires_approval=True,
                executed=False,
                task_type=task_type,
                task_name=task_name,
                payload=payload,
                error=f"Task type not allowed: {task_type}",
            )

        if task_name not in self.ALLOWED_TASKS:
            return ExecutorBridgeResult(
                ok=False,
                timestamp=utc_now(),
                accepted=False,
                requires_approval=True,
                executed=False,
                task_type=task_type,
                task_name=task_name,
                payload=payload,
                error=f"Task name not allowed: {task_name}",
            )

        requires_approval = bool(
            task_data.get("requires_approval", True)
        )

        return ExecutorBridgeResult(
            ok=True,
            timestamp=utc_now(),
            accepted=True,
            requires_approval=requires_approval,
            executed=False,
            task_type=task_type,
            task_name=task_name,
            payload=payload,
            error=None,
        )

    def process(
        self,
        task_data: dict[str, Any],
        approve: bool = False,
    ) -> dict[str, Any]:

        validation = self.validate(task_data)

        if not validation.ok:
            return asdict(validation)

        if validation.requires_approval and not approve:
            return asdict(validation)

        result = asdict(validation)
        result["executed"] = True

        return result


def process_executor_task(
    task_data: dict[str, Any],
    approve: bool = False,
) -> dict[str, Any]:

    bridge = ExecutorBridge()

    return bridge.process(
        task_data=task_data,
        approve=approve,
    )


if __name__ == "__main__":
    import json

    demo_task = {
        "type": "executor_task",
        "task_name": "memory_write",
        "payload": {
            "category": "learnings",
            "content": "Hermes ist das Gehirn.",
        },
        "requires_approval": True,
    }

    result = process_executor_task(
        demo_task,
        approve=False,
    )

    print(json.dumps(result, indent=2, ensure_ascii=False))
