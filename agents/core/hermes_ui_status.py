#!/usr/bin/env python3
"""
Hermes UI Status

Builds a single read-only JSON status object for future Jarvis UI panels.
This module does not start a server, build a UI, execute agents, or write
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


def _import_snapshot_builder(warnings: list[str]) -> Callable[[str | None], dict[str, Any]] | None:
    module_name = "agents.core.hermes_system_snapshot"
    function_name = "build_hermes_system_snapshot"

    try:
        module = importlib.import_module(module_name)
        builder = getattr(module, function_name)
    except Exception as exc:
        warnings.append(f"{module_name}.{function_name} unavailable: {exc}")
        return None

    if not callable(builder):
        warnings.append(f"{module_name}.{function_name} is not callable.")
        return None

    return builder


def _import_learning_memory_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_learning_memory_status"
    function_name = "build_learning_memory_status"

    try:
        module = importlib.import_module(module_name)
        builder = getattr(module, function_name)
    except Exception as exc:
        warnings.append(f"{module_name}.{function_name} unavailable: {exc}")
        return None

    if not callable(builder):
        warnings.append(f"{module_name}.{function_name} is not callable.")
        return None

    return builder


def _safe_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _safe_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _get_warnings(system_health: dict[str, Any]) -> list[str]:
    return [
        str(warning)
        for warning in _safe_list(system_health.get("warnings"))
        if str(warning).strip()
    ]


def _extract_brain(snapshot: dict[str, Any]) -> dict[str, Any]:
    routing_sample = snapshot.get("routing_sample")
    if not isinstance(routing_sample, dict):
        return {
            "status": "idle",
            "message": "No routing sample requested.",
            "hermes_brain_status": None,
        }

    brain_status = routing_sample.get("hermes_brain_status")
    if isinstance(brain_status, dict):
        return brain_status

    return {
        "status": "unavailable",
        "message": "Routing sample did not include hermes_brain_status.",
        "routing_ok": bool(routing_sample.get("ok", False)),
        "task": routing_sample.get("task"),
    }


def _find_agent_by_domain(agent_dashboard: dict[str, Any], domain: str) -> dict[str, Any]:
    for agent in _safe_list(agent_dashboard.get("agents")):
        if isinstance(agent, dict) and agent.get("domain") == domain:
            return agent

    return {}


def _build_chat_panel(optional_task: str | None, routing_sample: Any) -> dict[str, Any]:
    has_task = bool((optional_task or "").strip())
    routing_available = isinstance(routing_sample, dict) and bool(routing_sample.get("ok", False))

    return {
        "status": "ready",
        "mode": "read_only_status",
        "task_preview": optional_task if has_task else None,
        "routing_sample_available": routing_available,
        "placeholder": "Future chat panel can read this status before rendering.",
    }


def _build_hermes_brain_panel(brain: dict[str, Any], routing_sample: Any) -> dict[str, Any]:
    return {
        "status": "available" if isinstance(routing_sample, dict) else "idle",
        "brain_status_present": bool(brain and brain.get("status") != "idle"),
        "route": brain.get("route"),
        "intent": brain.get("intent"),
        "domain": brain.get("domain"),
        "confidence": brain.get("confidence"),
        "requires_approval": brain.get("requires_approval"),
        "placeholder": "Future Hermes Brain panel can render route, confidence, and safety state.",
    }


def _build_agent_dashboard_panel(agent_dashboard: dict[str, Any], system_health: dict[str, Any]) -> dict[str, Any]:
    agents = _safe_list(agent_dashboard.get("agents"))

    return {
        "status": "available" if agents else "unavailable",
        "agents_total": len(agents),
        "agents_available_count": system_health.get("agents_available_count", 0),
        "agents_planned_count": system_health.get("agents_planned_count", 0),
        "placeholder": "Future Agent Dashboard panel can list available and planned agents.",
    }


def _build_runtime_control_panel(runtime: dict[str, Any], system_health: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": "read_only",
        "hermes_available": bool(system_health.get("hermes_available", False)),
        "ollama_available": bool(system_health.get("ollama_available", False)),
        "memory_available": bool(system_health.get("memory_available", False)),
        "runtime_paths": runtime.get("runtime_paths", {}),
        "controls_enabled": False,
        "placeholder": "Future Runtime Control panel should display status without starting or stopping services.",
    }


def _build_learning_memory_status(warnings: list[str]) -> dict[str, Any]:
    builder = _import_learning_memory_builder(warnings)
    if builder is None:
        return {
            "generated_at": None,
            "memory_available": False,
            "learning_available": False,
            "routing_hints_available": False,
            "improvements_available": False,
            "counts": {},
            "latest_items_preview": {},
            "warnings": warnings,
        }

    try:
        status = builder()
    except Exception as exc:
        warnings.append(f"build_learning_memory_status failed: {exc}")
        return {
            "generated_at": None,
            "memory_available": False,
            "learning_available": False,
            "routing_hints_available": False,
            "improvements_available": False,
            "counts": {},
            "latest_items_preview": {},
            "warnings": [f"build_learning_memory_status failed: {exc}"],
        }

    if not isinstance(status, dict):
        warnings.append("build_learning_memory_status returned non-dict data.")
        return {
            "generated_at": None,
            "memory_available": False,
            "learning_available": False,
            "routing_hints_available": False,
            "improvements_available": False,
            "counts": {},
            "latest_items_preview": {},
            "warnings": ["build_learning_memory_status returned non-dict data."],
        }

    return status


def _learning_memory_root_path(learning_memory: dict[str, Any]) -> str | None:
    paths = _safe_dict(learning_memory.get("paths"))
    hermes_path = paths.get(".hermes")
    if isinstance(hermes_path, dict):
        path = hermes_path.get("path")
        return str(path) if path else None

    return None


def _build_learning_memory_panel(runtime: dict[str, Any], learning_memory: dict[str, Any]) -> dict[str, Any]:
    memory_status = _safe_dict(runtime.get("memory_status"))
    learning_warnings = [
        str(warning)
        for warning in _safe_list(learning_memory.get("warnings"))
        if str(warning).strip()
    ]
    memory_available = bool(
        learning_memory.get("memory_available")
        or memory_status.get("status") == "available"
    )

    if learning_memory.get("learning_available"):
        status = "available"
    elif memory_available:
        status = "memory_available"
    else:
        status = "not_configured"

    return {
        "status": status,
        "memory_available": memory_available,
        "learning_available": bool(learning_memory.get("learning_available", False)),
        "routing_hints_available": bool(learning_memory.get("routing_hints_available", False)),
        "improvements_available": bool(learning_memory.get("improvements_available", False)),
        "path": memory_status.get("path") or _learning_memory_root_path(learning_memory),
        "counts": _safe_dict(learning_memory.get("counts")),
        "latest_items_preview": _safe_dict(learning_memory.get("latest_items_preview")),
        "warnings": learning_warnings,
        "read_only": True,
        "placeholder": "Future Learning/Memory panel can show memory availability and learning status.",
    }


def _build_developer_debug_panel(system_health: dict[str, Any], snapshot: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": "available",
        "warnings": _get_warnings(system_health),
        "snapshot_generated_at": snapshot.get("generated_at"),
        "debug_mode": "read_only",
        "placeholder": "Future Developer Debug panel can inspect warnings and raw snapshot metadata.",
    }


def _build_trading_panel(agent_dashboard: dict[str, Any]) -> dict[str, Any]:
    trading_agent = _find_agent_by_domain(agent_dashboard, "trading")
    safety_flags = _safe_dict(trading_agent.get("safety_flags"))

    return {
        "status": "planned",
        "agent_id": trading_agent.get("agent_id", "trading_agent"),
        "analysis_only": True,
        "no_auto_trading": True,
        "human_review_required": bool(safety_flags.get("human_review_required", True)),
        "can_execute": False,
        "prediction_feedback_learning": "planned",
        "capabilities": _safe_list(trading_agent.get("capabilities")),
        "placeholder": "Future Trading panel can show analysis and prediction feedback only, without order controls.",
    }


def _build_ui_panels(
    optional_task: str | None,
    snapshot: dict[str, Any],
    brain: dict[str, Any],
    learning_memory: dict[str, Any],
) -> dict[str, Any]:
    runtime = _safe_dict(snapshot.get("runtime"))
    agent_dashboard = _safe_dict(snapshot.get("agents"))
    routing_sample = snapshot.get("routing_sample")
    system_health = _safe_dict(snapshot.get("system_health_summary"))

    return {
        "chat_panel": _build_chat_panel(optional_task, routing_sample),
        "hermes_brain_panel": _build_hermes_brain_panel(brain, routing_sample),
        "agent_dashboard_panel": _build_agent_dashboard_panel(agent_dashboard, system_health),
        "runtime_control_panel": _build_runtime_control_panel(runtime, system_health),
        "learning_memory_panel": _build_learning_memory_panel(runtime, learning_memory),
        "developer_debug_panel": _build_developer_debug_panel(system_health, snapshot),
        "trading_panel": _build_trading_panel(agent_dashboard),
    }


def _fallback_status(warnings: list[str], learning_memory: dict[str, Any] | None = None) -> dict[str, Any]:
    learning_memory = learning_memory or {
        "generated_at": None,
        "memory_available": False,
        "learning_available": False,
        "routing_hints_available": False,
        "improvements_available": False,
        "counts": {},
        "latest_items_preview": {},
        "warnings": warnings,
    }
    system_health = {
        "hermes_available": False,
        "ollama_available": False,
        "memory_available": False,
        "agents_available_count": 0,
        "agents_planned_count": 0,
        "warnings": warnings,
    }
    snapshot = {
        "generated_at": None,
        "runtime": {},
        "agents": {"generated_at": None, "agents": []},
        "routing_sample": None,
        "system_health_summary": system_health,
    }
    brain = {
        "status": "unavailable",
        "message": "Hermes system snapshot unavailable.",
    }

    return {
        "generated_at": utc_now(),
        "brain": brain,
        "agents": snapshot["agents"],
        "runtime": snapshot["runtime"],
        "learning_memory": learning_memory,
        "system_health": system_health,
        "ui_panels": _build_ui_panels(None, snapshot, brain, learning_memory),
    }


def build_hermes_ui_status(optional_task: str | None = None) -> dict[str, Any]:
    warnings: list[str] = []
    learning_memory = _build_learning_memory_status(warnings)
    snapshot_builder = _import_snapshot_builder(warnings)
    if snapshot_builder is None:
        return _fallback_status(warnings, learning_memory)

    try:
        snapshot = snapshot_builder(optional_task)
    except Exception as exc:
        warnings.append(f"build_hermes_system_snapshot failed: {exc}")
        return _fallback_status(warnings, learning_memory)

    if not isinstance(snapshot, dict):
        warnings.append("build_hermes_system_snapshot returned non-dict data.")
        return _fallback_status(warnings, learning_memory)

    system_health = _safe_dict(snapshot.get("system_health_summary"))
    existing_warnings = _get_warnings(system_health)
    merged_warnings = existing_warnings + [
        warning for warning in warnings if warning not in existing_warnings
    ]
    for warning in _safe_list(learning_memory.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    system_health["warnings"] = merged_warnings

    brain = _extract_brain(snapshot)
    runtime = _safe_dict(snapshot.get("runtime"))
    agents = _safe_dict(snapshot.get("agents"))

    return {
        "generated_at": utc_now(),
        "brain": brain,
        "agents": agents,
        "runtime": runtime,
        "learning_memory": learning_memory,
        "system_health": system_health,
        "ui_panels": _build_ui_panels(optional_task, snapshot, brain, learning_memory),
    }


def main() -> int:
    optional_task = " ".join(sys.argv[1:]).strip() or None
    print(json.dumps(build_hermes_ui_status(optional_task), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
