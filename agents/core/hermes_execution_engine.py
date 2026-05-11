#!/usr/bin/env python3
"""
Hermes Execution Engine

Controlled execution layer for Hermes orchestration results.

Hermes plans and orchestrates. The DelegationExecutor performs approved step
execution through RuntimeRouter. This engine wraps both sides and returns a
single JSON-compatible execution report.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.delegation_executor import execute_delegation_contract
from agents.core.hermes_orchestrator import orchestrate_objective
from agents.core.runtime_router import RuntimeRouter


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _empty_execution_result(
    *,
    total_steps: int = 0,
    reason: str,
) -> dict[str, Any]:
    return {
        "ok": False,
        "total_steps": total_steps,
        "executed_steps": 0,
        "skipped_steps": total_steps,
        "failed_steps": 0,
        "step_results": [],
        "errors": [],
        "reason": reason,
    }


def _step_metadata(step_result: dict[str, Any]) -> dict[str, Any]:
    return {
        "domain": step_result.get("domain", ""),
        "task": step_result.get("task", ""),
        "approval_required": bool(step_result.get("requires_approval", True)),
        "executed": bool(step_result.get("executed", False)),
    }


def _normalize_step_results(raw_results: list[dict[str, Any]]) -> list[dict[str, Any]]:
    normalized: list[dict[str, Any]] = []

    for item in raw_results:
        error = item.get("error")
        skipped = bool(item.get("skipped", False))

        normalized.append(
            {
                "step_id": item.get("step_id"),
                "metadata": _step_metadata(item),
                "skipped": skipped,
                "failed": bool(error and not skipped),
                "approval_reason": item.get("approval_reason"),
                "result": item.get("result"),
                "executor_bridge_result": item.get("executor_bridge_result"),
                "error": error,
            }
        )

    return normalized


def _summarize_execution(
    *,
    objective: str,
    approve_all: bool,
    orchestration: dict[str, Any],
    execution_result: dict[str, Any],
) -> dict[str, Any]:
    total_steps = int(execution_result.get("total_steps", 0))
    executed_steps = int(execution_result.get("executed_steps", 0))
    skipped_steps = int(execution_result.get("skipped_steps", 0))
    failed_steps = int(execution_result.get("failed_steps", 0))

    if failed_steps:
        status = "failed"
        message = "One or more delegation steps failed."
    elif skipped_steps and not approve_all:
        status = "approval_required"
        message = "Approval is required before execution."
    elif total_steps == 0:
        status = "not_executable"
        message = "No delegation contract was available for execution."
    else:
        status = "completed"
        message = "Delegation contract execution completed."

    return {
        "objective": objective,
        "mode": orchestration.get("mode"),
        "approve_all": approve_all,
        "status": status,
        "total_steps": total_steps,
        "executed_steps": executed_steps,
        "skipped_steps": skipped_steps,
        "failed_steps": failed_steps,
        "message": message,
        "timestamp": utc_now(),
    }


def _build_execution_result(
    *,
    delegation_execution: dict[str, Any],
) -> dict[str, Any]:
    raw_results = delegation_execution.get("results", []) or []
    step_results = _normalize_step_results(raw_results)

    failed_steps = sum(1 for item in step_results if item["failed"])
    errors = [
        {
            "step_id": item.get("step_id"),
            "domain": item["metadata"]["domain"],
            "task": item["metadata"]["task"],
            "error": item.get("error"),
        }
        for item in step_results
        if item["failed"]
    ]

    return {
        "ok": bool(delegation_execution.get("ok", False)),
        "total_steps": int(delegation_execution.get("steps_total", len(step_results))),
        "executed_steps": int(delegation_execution.get("steps_executed", 0)),
        "skipped_steps": int(delegation_execution.get("steps_skipped", 0)),
        "failed_steps": failed_steps,
        "step_results": step_results,
        "errors": errors,
        "delegation_execution": delegation_execution,
    }


def execute_objective(objective: str, approve_all: bool = False) -> dict[str, Any]:
    objective = objective.strip()
    timestamp = utc_now()
    runtime_router = RuntimeRouter()

    if not objective:
        execution_result = _empty_execution_result(
            reason="No objective provided.",
        )
        execution_summary = _summarize_execution(
            objective="",
            approve_all=approve_all,
            orchestration={"mode": "error"},
            execution_result=execution_result,
        )

        return {
            "ok": False,
            "objective": "",
            "orchestration": None,
            "execution_result": execution_result,
            "execution_summary": execution_summary,
            "metadata": {
                "engine": "hermes_execution_engine",
                "runtime_router": runtime_router.__class__.__name__,
                "human_in_the_loop": True,
            },
            "timestamp": timestamp,
        }

    orchestration = orchestrate_objective(objective)
    contract = orchestration.get("delegation_contract")

    if not contract:
        execution_result = _empty_execution_result(
            total_steps=int(orchestration.get("steps_total", 0)),
            reason="No delegation contract was produced by orchestration.",
        )
        execution_summary = _summarize_execution(
            objective=objective,
            approve_all=approve_all,
            orchestration=orchestration,
            execution_result=execution_result,
        )

        return {
            "ok": bool(orchestration.get("ok", False)),
            "objective": objective,
            "orchestration": orchestration,
            "execution_result": execution_result,
            "execution_summary": execution_summary,
            "metadata": {
                "engine": "hermes_execution_engine",
                "runtime_router": runtime_router.__class__.__name__,
                "human_in_the_loop": True,
            },
            "timestamp": timestamp,
        }

    delegation_execution = execute_delegation_contract(
        contract=contract,
        approve_all=approve_all,
        approve_executor_tasks=approve_all,
    )
    execution_result = _build_execution_result(
        delegation_execution=delegation_execution,
    )
    execution_summary = _summarize_execution(
        objective=objective,
        approve_all=approve_all,
        orchestration=orchestration,
        execution_result=execution_result,
    )

    return {
        "ok": bool(execution_result.get("ok", False)),
        "objective": objective,
        "orchestration": orchestration,
        "execution_result": execution_result,
        "execution_summary": execution_summary,
        "metadata": {
            "engine": "hermes_execution_engine",
            "runtime_router": runtime_router.__class__.__name__,
            "approve_all": approve_all,
            "human_in_the_loop": True,
        },
        "timestamp": timestamp,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Hermes Execution Engine")
    parser.add_argument(
        "--approve-all",
        action="store_true",
        help="Execute approval-required delegation steps.",
    )
    parser.add_argument("objective", nargs="*", help="Objective to execute")
    args = parser.parse_args()

    objective = " ".join(args.objective).strip()
    result = execute_objective(
        objective=objective,
        approve_all=args.approve_all,
    )

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
