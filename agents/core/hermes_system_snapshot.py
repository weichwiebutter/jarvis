#!/usr/bin/env python3
"""
Hermes System Snapshot

Aggregates read-only Hermes/Jarvis status data for a future UI snapshot.
This module does not start services, stop services, execute agents, or write
runtime files.
"""

from __future__ import annotations

import importlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _import_function(
    module_name: str,
    function_name: str,
    warnings: list[str],
) -> Callable[..., Any] | None:
    try:
        module = importlib.import_module(module_name)
        function = getattr(module, function_name)
    except Exception as exc:
        warnings.append(f"{module_name}.{function_name} unavailable: {exc}")
        return None

    if not callable(function):
        warnings.append(f"{module_name}.{function_name} is not callable.")
        return None

    return function


def _build_runtime_snapshot(warnings: list[str]) -> dict[str, Any]:
    build_runtime_status = _import_function(
        "agents.core.hermes_runtime_status",
        "build_runtime_status",
        warnings,
    )
    if build_runtime_status is None:
        return {}

    try:
        runtime = build_runtime_status()
    except Exception as exc:
        warnings.append(f"runtime_status failed: {exc}")
        return {}

    if not isinstance(runtime, dict):
        warnings.append("runtime_status returned non-dict data.")
        return {}

    return runtime


def _build_agent_dashboard_snapshot(warnings: list[str]) -> dict[str, Any]:
    build_agent_dashboard_status = _import_function(
        "agents.core.hermes_agent_dashboard",
        "build_agent_dashboard_status",
        warnings,
    )
    if build_agent_dashboard_status is None:
        return {"generated_at": None, "agents": []}

    try:
        dashboard = build_agent_dashboard_status()
    except Exception as exc:
        warnings.append(f"agent_dashboard failed: {exc}")
        return {"generated_at": None, "agents": []}

    if not isinstance(dashboard, dict):
        warnings.append("agent_dashboard returned non-dict data.")
        return {"generated_at": None, "agents": []}

    agents = dashboard.get("agents")
    if not isinstance(agents, list):
        warnings.append("agent_dashboard did not return an agents list.")
        dashboard["agents"] = []

    return dashboard


def _build_routing_sample(
    optional_task: str | None,
    warnings: list[str],
) -> dict[str, Any] | None:
    task = (optional_task or "").strip()
    if not task:
        return None

    decide_route = _import_function(
        "agents.core.hermes_router",
        "decide_route",
        warnings,
    )
    if decide_route is None:
        return {
            "ok": False,
            "task": task,
            "error": "Router unavailable.",
        }

    try:
        routing_sample = decide_route(task)
    except Exception as exc:
        warnings.append(f"routing_sample failed: {exc}")
        return {
            "ok": False,
            "task": task,
            "error": str(exc),
        }

    if not isinstance(routing_sample, dict):
        warnings.append("routing_sample returned non-dict data.")
        return {
            "ok": False,
            "task": task,
            "error": "Router returned non-dict data.",
        }

    return routing_sample


def _status_is_available(status_data: Any) -> bool:
    return isinstance(status_data, dict) and (
        status_data.get("available") is True
        or status_data.get("status") == "available"
        or status_data.get("importable") is True
    )


def _append_runtime_warnings(runtime: dict[str, Any], warnings: list[str]) -> None:
    ollama_status = runtime.get("ollama_status")
    if isinstance(ollama_status, dict) and not _status_is_available(ollama_status):
        detail = ollama_status.get("error") or ollama_status.get("stderr")
        if detail:
            warnings.append(f"ollama unavailable: {detail}")

    git_status = runtime.get("git_status")
    if isinstance(git_status, dict) and git_status.get("status") != "available":
        detail = git_status.get("error") or git_status.get("stderr")
        warnings.append(f"git status unavailable: {detail or 'unknown error'}")


def _build_system_health_summary(
    runtime: dict[str, Any],
    agent_dashboard: dict[str, Any],
    warnings: list[str],
) -> dict[str, Any]:
    agents = agent_dashboard.get("agents", [])
    if not isinstance(agents, list):
        agents = []

    _append_runtime_warnings(runtime, warnings)

    return {
        "hermes_available": _status_is_available(runtime.get("hermes_status")),
        "ollama_available": _status_is_available(runtime.get("ollama_status")),
        "memory_available": _status_is_available(runtime.get("memory_status")),
        "agents_available_count": sum(
            1
            for agent in agents
            if isinstance(agent, dict) and agent.get("status") == "available"
        ),
        "agents_planned_count": sum(
            1
            for agent in agents
            if isinstance(agent, dict) and agent.get("status") == "planned"
        ),
        "warnings": warnings,
    }


def build_hermes_system_snapshot(optional_task: str | None = None) -> dict[str, Any]:
    warnings: list[str] = []

    runtime = _build_runtime_snapshot(warnings)
    agent_dashboard = _build_agent_dashboard_snapshot(warnings)
    routing_sample = _build_routing_sample(optional_task, warnings)

    return {
        "generated_at": utc_now(),
        "runtime": runtime,
        "agents": agent_dashboard,
        "routing_sample": routing_sample,
        "system_health_summary": _build_system_health_summary(
            runtime,
            agent_dashboard,
            warnings,
        ),
    }


def main() -> int:
    optional_task = " ".join(sys.argv[1:]).strip() or None
    snapshot = build_hermes_system_snapshot(optional_task)
    print(json.dumps(snapshot, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
