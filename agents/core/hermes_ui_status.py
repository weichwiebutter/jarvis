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


def _import_developer_debug_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_developer_debug_status"
    function_name = "build_developer_debug_status"

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


def _import_voice_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_voice_status"
    function_name = "build_voice_status"

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


def _import_trading_panel_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_trading_panel_status"
    function_name = "build_trading_panel_status"

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


def _import_home_dashboard_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.jarvis_home_dashboard_status"
    function_name = "build_jarvis_home_dashboard_status"

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


def _import_runtime_supervisor_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_runtime_supervisor"
    function_name = "build_runtime_supervisor_status"

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


def _import_shared_memory_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_shared_memory_status"
    function_name = "build_shared_memory_status"

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


def _import_skills_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_skills_status"
    function_name = "build_skills_status"

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


def _import_research_discovery_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_research_discovery_status"
    function_name = "build_research_discovery_status"

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


def _import_cost_optimization_builder(warnings: list[str]) -> Callable[[], dict[str, Any]] | None:
    module_name = "agents.core.hermes_cost_optimization_status"
    function_name = "build_cost_optimization_status"

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


def _import_runtime_events_builders(
    warnings: list[str],
) -> tuple[Callable[[], list[Any]] | None, Callable[[Any], dict[str, Any]] | None]:
    module_name = "agents.core.hermes_runtime_events"
    example_function_name = "example_runtime_events"
    serialize_function_name = "serialize_runtime_event"

    try:
        module = importlib.import_module(module_name)
        example_builder = getattr(module, example_function_name)
        serializer = getattr(module, serialize_function_name)
    except Exception as exc:
        warnings.append(
            f"{module_name}.{example_function_name}/{serialize_function_name} unavailable: {exc}"
        )
        return None, None

    if not callable(example_builder):
        warnings.append(f"{module_name}.{example_function_name} is not callable.")
        example_builder = None

    if not callable(serializer):
        warnings.append(f"{module_name}.{serialize_function_name} is not callable.")
        serializer = None

    return example_builder, serializer


def _import_activity_timeline_builders(
    warnings: list[str],
) -> tuple[Callable[[], list[Any]] | None, Callable[[Any], dict[str, Any]] | None]:
    module_name = "agents.core.hermes_activity_timeline"
    timeline_function_name = "build_demo_activity_timeline"
    serialize_function_name = "serialize_timeline_entry"

    try:
        module = importlib.import_module(module_name)
        timeline_builder = getattr(module, timeline_function_name)
        serializer = getattr(module, serialize_function_name)
    except Exception as exc:
        warnings.append(
            f"{module_name}.{timeline_function_name}/{serialize_function_name} unavailable: {exc}"
        )
        return None, None

    if not callable(timeline_builder):
        warnings.append(f"{module_name}.{timeline_function_name} is not callable.")
        timeline_builder = None

    if not callable(serializer):
        warnings.append(f"{module_name}.{serialize_function_name} is not callable.")
        serializer = None

    return timeline_builder, serializer


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


def _build_developer_debug_status(warnings: list[str]) -> dict[str, Any]:
    builder = _import_developer_debug_builder(warnings)
    if builder is None:
        return {
            "generated_at": None,
            "available_debug_modules": [],
            "available_cli_checks": [],
            "suggested_test_commands": [],
            "warnings": warnings,
        }

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_developer_debug_status failed: {exc}"
        warnings.append(warning)
        return {
            "generated_at": None,
            "available_debug_modules": [],
            "available_cli_checks": [],
            "suggested_test_commands": [],
            "warnings": [warning],
        }

    if not isinstance(status, dict):
        warning = "build_developer_debug_status returned non-dict data."
        warnings.append(warning)
        return {
            "generated_at": None,
            "available_debug_modules": [],
            "available_cli_checks": [],
            "suggested_test_commands": [],
            "warnings": [warning],
        }

    return status


def _empty_voice_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "voice_status": {
            "status": "unavailable",
            "configured": False,
            "enabled": False,
            "read_only": True,
            "services_started": False,
            "audio_access_performed": False,
        },
        "wake_word_status": {
            "status": "unavailable",
            "enabled": False,
            "active": False,
            "service_started": False,
        },
        "microphone_status": {
            "status": "not_checked",
            "enabled": False,
            "accessed": False,
            "recording": False,
        },
        "transcription_status": {
            "status": "unavailable",
            "enabled": False,
            "active": False,
        },
        "tts_status": {
            "status": "unavailable",
            "enabled": False,
            "active": False,
        },
        "audio_visualizer_status": {
            "status": "unavailable",
            "enabled": False,
            "active": False,
        },
        "planned_stack": {},
        "warnings": warnings,
    }


def _build_voice_status(warnings: list[str]) -> dict[str, Any]:
    builder = _import_voice_builder(warnings)
    if builder is None:
        return _empty_voice_status(warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_voice_status failed: {exc}"
        warnings.append(warning)
        return _empty_voice_status([warning])

    if not isinstance(status, dict):
        warning = "build_voice_status returned non-dict data."
        warnings.append(warning)
        return _empty_voice_status([warning])

    return status


def _empty_trading_panel_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "planned",
        "analysis_only": True,
        "no_auto_trading": True,
        "human_review_required": True,
        "supported_markets": [],
        "planned_timeframes": {},
        "planned_patterns": [],
        "confidence_score": {},
        "prediction_feedback_learning": {
            "status": "planned",
            "outcomes": [],
        },
        "ctrader_integration": {
            "status": "planned",
            "mode": "external_bridge_planned",
        },
        "warnings": warnings,
    }


def _build_trading_panel_status(warnings: list[str]) -> dict[str, Any]:
    builder = _import_trading_panel_builder(warnings)
    if builder is None:
        return _empty_trading_panel_status(warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_trading_panel_status failed: {exc}"
        warnings.append(warning)
        return _empty_trading_panel_status([warning])

    if not isinstance(status, dict):
        warning = "build_trading_panel_status returned non-dict data."
        warnings.append(warning)
        return _empty_trading_panel_status([warning])

    return status


def _empty_runtime_events_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "planned/live_foundation",
        "events": [],
        "warnings": warnings,
        "read_only": True,
    }


def _empty_runtime_supervisor_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "unavailable",
        "read_only": True,
        "foundation_only": True,
        "background_loops_started": False,
        "threads_started": False,
        "services_started": False,
        "runtime_files_written": False,
        "heartbeat": {},
        "scheduler": {},
        "agent_lifecycle": {},
        "zombie_protection": {},
        "context_lifecycle": {},
        "context_compression": {},
        "resource_limits": {},
        "runtime_cleanup": {},
        "planned_jobs": [],
        "warnings": warnings,
    }


def _empty_shared_memory_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "unavailable",
        "read_only": True,
        "foundation_only": True,
        "sync_actions_performed": False,
        "network_connections_opened": False,
        "files_copied": False,
        "runtime_files_written": False,
        "secrets_read": False,
        "sync_strategy": {},
        "local_only_paths": [],
        "shared_candidate_paths": [],
        "approval_workflow": {},
        "multi_pc_roles": {},
        "warnings": warnings,
    }


def _empty_skills_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "unavailable",
        "read_only": True,
        "foundation_only": True,
        "skills_executed": False,
        "external_repos_cloned": False,
        "skills_generated": False,
        "runtime_files_written": False,
        "services_started": False,
        "skill_root_candidates": [],
        "planned_skill_categories": [],
        "skill_registry": {},
        "skill_review_workflow": {},
        "skill_safety": {},
        "external_pattern_sources": [],
        "warnings": warnings,
    }


def _empty_research_discovery_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "unavailable",
        "read_only": True,
        "foundation_only": True,
        "external_queries_performed": False,
        "api_calls_performed": False,
        "scheduler_started": False,
        "background_loops_started": False,
        "runtime_files_written": False,
        "services_started": False,
        "research_sources": [],
        "monitored_topics": [],
        "discovery_pipeline": {},
        "review_workflow": {},
        "safety_rules": {},
        "planned_reports": [],
        "warnings": warnings,
    }


def _empty_cost_optimization_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "unavailable",
        "read_only": True,
        "foundation_only": True,
        "api_calls_performed": False,
        "openrouter_queries_performed": False,
        "codex_queries_performed": False,
        "model_calls_performed": False,
        "secrets_read": False,
        "runtime_files_written": False,
        "services_started": False,
        "codex_usage_strategy": [],
        "fast_mode_policy": {},
        "provider_priority": [],
        "cost_controls": {},
        "monitored_resources": [],
        "future_dashboards": [],
        "warnings": warnings,
    }


def _append_warning(warnings: list[str], warning: str) -> None:
    if warning.strip() and warning not in warnings:
        warnings.append(warning)


def _build_runtime_supervisor_status(warnings: list[str]) -> dict[str, Any]:
    runtime_supervisor_warnings: list[str] = []
    builder = _import_runtime_supervisor_builder(runtime_supervisor_warnings)
    for warning in runtime_supervisor_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_runtime_supervisor_status(runtime_supervisor_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_runtime_supervisor_status failed: {exc}"
        _append_warning(runtime_supervisor_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_runtime_supervisor_status(runtime_supervisor_warnings)

    if not isinstance(status, dict):
        warning = "build_runtime_supervisor_status returned non-dict data."
        _append_warning(runtime_supervisor_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_runtime_supervisor_status(runtime_supervisor_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(runtime_supervisor_warnings, warning_text)

    if runtime_supervisor_warnings:
        status["warnings"] = runtime_supervisor_warnings

    return status


def _build_shared_memory_status(warnings: list[str]) -> dict[str, Any]:
    shared_memory_warnings: list[str] = []
    builder = _import_shared_memory_builder(shared_memory_warnings)
    for warning in shared_memory_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_shared_memory_status(shared_memory_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_shared_memory_status failed: {exc}"
        _append_warning(shared_memory_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_shared_memory_status(shared_memory_warnings)

    if not isinstance(status, dict):
        warning = "build_shared_memory_status returned non-dict data."
        _append_warning(shared_memory_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_shared_memory_status(shared_memory_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(shared_memory_warnings, warning_text)

    if shared_memory_warnings:
        status["warnings"] = shared_memory_warnings

    return status


def _build_skills_status(warnings: list[str]) -> dict[str, Any]:
    skills_warnings: list[str] = []
    builder = _import_skills_builder(skills_warnings)
    for warning in skills_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_skills_status(skills_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_skills_status failed: {exc}"
        _append_warning(skills_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_skills_status(skills_warnings)

    if not isinstance(status, dict):
        warning = "build_skills_status returned non-dict data."
        _append_warning(skills_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_skills_status(skills_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(skills_warnings, warning_text)

    if skills_warnings:
        status["warnings"] = skills_warnings

    return status


def _build_research_discovery_status(warnings: list[str]) -> dict[str, Any]:
    research_discovery_warnings: list[str] = []
    builder = _import_research_discovery_builder(research_discovery_warnings)
    for warning in research_discovery_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_research_discovery_status(research_discovery_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_research_discovery_status failed: {exc}"
        _append_warning(research_discovery_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_research_discovery_status(research_discovery_warnings)

    if not isinstance(status, dict):
        warning = "build_research_discovery_status returned non-dict data."
        _append_warning(research_discovery_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_research_discovery_status(research_discovery_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(research_discovery_warnings, warning_text)

    if research_discovery_warnings:
        status["warnings"] = research_discovery_warnings

    return status


def _build_cost_optimization_status(warnings: list[str]) -> dict[str, Any]:
    cost_optimization_warnings: list[str] = []
    builder = _import_cost_optimization_builder(cost_optimization_warnings)
    for warning in cost_optimization_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_cost_optimization_status(cost_optimization_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_cost_optimization_status failed: {exc}"
        _append_warning(cost_optimization_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_cost_optimization_status(cost_optimization_warnings)

    if not isinstance(status, dict):
        warning = "build_cost_optimization_status returned non-dict data."
        _append_warning(cost_optimization_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_cost_optimization_status(cost_optimization_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(cost_optimization_warnings, warning_text)

    if cost_optimization_warnings:
        status["warnings"] = cost_optimization_warnings

    return status


def _build_runtime_events_status(warnings: list[str]) -> dict[str, Any]:
    runtime_event_warnings: list[str] = []
    example_builder, serializer = _import_runtime_events_builders(runtime_event_warnings)
    for warning in runtime_event_warnings:
        _append_warning(warnings, warning)

    if example_builder is None or serializer is None:
        return _empty_runtime_events_status(runtime_event_warnings)

    try:
        events = example_builder()
    except Exception as exc:
        warning = f"example_runtime_events failed: {exc}"
        _append_warning(runtime_event_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_runtime_events_status(runtime_event_warnings)

    if not isinstance(events, list):
        warning = "example_runtime_events returned non-list data."
        _append_warning(runtime_event_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_runtime_events_status(runtime_event_warnings)

    serialized_events: list[dict[str, Any]] = []
    for index, event in enumerate(events):
        try:
            serialized_event = serializer(event)
        except Exception as exc:
            warning = f"serialize_runtime_event failed for event {index}: {exc}"
            _append_warning(runtime_event_warnings, warning)
            _append_warning(warnings, warning)
            continue

        if not isinstance(serialized_event, dict):
            warning = f"serialize_runtime_event returned non-dict data for event {index}."
            _append_warning(runtime_event_warnings, warning)
            _append_warning(warnings, warning)
            continue

        serialized_events.append(serialized_event)

    return {
        "generated_at": utc_now(),
        "status": "planned/live_foundation",
        "events": serialized_events,
        "warnings": runtime_event_warnings,
        "read_only": True,
    }


def _empty_activity_timeline_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "planned/live_foundation",
        "entries": [],
        "warnings": warnings,
        "read_only": True,
    }


def _build_activity_timeline_status(warnings: list[str]) -> dict[str, Any]:
    timeline_warnings: list[str] = []
    timeline_builder, serializer = _import_activity_timeline_builders(timeline_warnings)
    for warning in timeline_warnings:
        _append_warning(warnings, warning)

    if timeline_builder is None or serializer is None:
        return _empty_activity_timeline_status(timeline_warnings)

    try:
        entries = timeline_builder()
    except Exception as exc:
        warning = f"build_demo_activity_timeline failed: {exc}"
        _append_warning(timeline_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_activity_timeline_status(timeline_warnings)

    if not isinstance(entries, list):
        warning = "build_demo_activity_timeline returned non-list data."
        _append_warning(timeline_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_activity_timeline_status(timeline_warnings)

    serialized_entries: list[dict[str, Any]] = []
    for index, entry in enumerate(entries):
        try:
            serialized_entry = serializer(entry)
        except Exception as exc:
            warning = f"serialize_timeline_entry failed for entry {index}: {exc}"
            _append_warning(timeline_warnings, warning)
            _append_warning(warnings, warning)
            continue

        if not isinstance(serialized_entry, dict):
            warning = f"serialize_timeline_entry returned non-dict data for entry {index}."
            _append_warning(timeline_warnings, warning)
            _append_warning(warnings, warning)
            continue

        serialized_entries.append(serialized_entry)

    return {
        "generated_at": utc_now(),
        "status": "planned/live_foundation",
        "entries": serialized_entries,
        "warnings": timeline_warnings,
        "read_only": True,
    }


def _empty_home_dashboard_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "dashboard_version": "v1",
        "online_status": {
            "status": "unavailable",
            "hermes_available": False,
            "ollama_available": False,
            "external_market_data_connected": False,
            "weather_api_connected": False,
            "services_started": False,
            "runtime_files_written": False,
        },
        "primary_tiles": [],
        "market_watch": {
            "status": "planned",
            "quote_only": True,
            "no_auto_trading": True,
            "live_quotes_requested": False,
            "orders_enabled": False,
            "symbols": {},
        },
        "weather": {
            "status": "planned",
            "source": "planned_weather_provider",
            "api_called": False,
        },
        "active_agents": {
            "status": "unavailable",
            "agents": [],
            "available_count": 0,
            "planned_count": 0,
        },
        "taskline": {
            "status": "planned/live_foundation",
            "entries": [],
        },
        "runtime": {
            "status": "unavailable",
            "read_only": True,
        },
        "warnings": warnings,
        "read_only": True,
    }


def _build_home_dashboard_status(warnings: list[str]) -> dict[str, Any]:
    home_dashboard_warnings: list[str] = []
    builder = _import_home_dashboard_builder(home_dashboard_warnings)
    for warning in home_dashboard_warnings:
        _append_warning(warnings, warning)

    if builder is None:
        return _empty_home_dashboard_status(home_dashboard_warnings)

    try:
        status = builder()
    except Exception as exc:
        warning = f"build_jarvis_home_dashboard_status failed: {exc}"
        _append_warning(home_dashboard_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_home_dashboard_status(home_dashboard_warnings)

    if not isinstance(status, dict):
        warning = "build_jarvis_home_dashboard_status returned non-dict data."
        _append_warning(home_dashboard_warnings, warning)
        _append_warning(warnings, warning)
        return _empty_home_dashboard_status(home_dashboard_warnings)

    for warning in _safe_list(status.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(home_dashboard_warnings, warning_text)
            _append_warning(warnings, warning_text)

    if home_dashboard_warnings:
        status["warnings"] = home_dashboard_warnings

    return status


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


def _build_voice_panel(voice: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": _safe_dict(voice.get("voice_status")).get("status", "unavailable"),
        "voice_status": _safe_dict(voice.get("voice_status")),
        "wake_word_status": _safe_dict(voice.get("wake_word_status")),
        "microphone_status": _safe_dict(voice.get("microphone_status")),
        "transcription_status": _safe_dict(voice.get("transcription_status")),
        "tts_status": _safe_dict(voice.get("tts_status")),
        "audio_visualizer_status": _safe_dict(voice.get("audio_visualizer_status")),
        "planned_stack": _safe_dict(voice.get("planned_stack")),
        "warnings": [
            str(warning)
            for warning in _safe_list(voice.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "placeholder": "Future Voice panel can show planned voice stack status without audio access.",
    }


def _build_developer_debug_panel(
    system_health: dict[str, Any],
    snapshot: dict[str, Any],
    developer_debug: dict[str, Any],
) -> dict[str, Any]:
    debug_warnings = [
        str(warning)
        for warning in _safe_list(developer_debug.get("warnings"))
        if str(warning).strip()
    ]

    return {
        "status": "available",
        "available_debug_modules": _safe_list(developer_debug.get("available_debug_modules")),
        "available_cli_checks": _safe_list(developer_debug.get("available_cli_checks")),
        "suggested_test_commands": _safe_list(developer_debug.get("suggested_test_commands")),
        "warnings": _get_warnings(system_health) + [
            warning for warning in debug_warnings if warning not in _get_warnings(system_health)
        ],
        "snapshot_generated_at": snapshot.get("generated_at"),
        "debug_mode": "read_only",
        "placeholder": "Future Developer Debug panel can inspect warnings and raw snapshot metadata.",
    }


def _build_trading_panel(agent_dashboard: dict[str, Any], trading: dict[str, Any]) -> dict[str, Any]:
    trading_agent = _find_agent_by_domain(agent_dashboard, "trading")
    safety_flags = _safe_dict(trading_agent.get("safety_flags"))
    trading_warnings = [
        str(warning)
        for warning in _safe_list(trading.get("warnings"))
        if str(warning).strip()
    ]

    return {
        "status": trading.get("status", "planned"),
        "analysis_only": bool(trading.get("analysis_only", True)),
        "no_auto_trading": bool(trading.get("no_auto_trading", True)),
        "human_review_required": bool(
            trading.get("human_review_required", safety_flags.get("human_review_required", True))
        ),
        "supported_markets": _safe_list(trading.get("supported_markets")),
        "planned_timeframes": _safe_dict(trading.get("planned_timeframes")),
        "planned_patterns": _safe_list(trading.get("planned_patterns")),
        "confidence_score": _safe_dict(trading.get("confidence_score")),
        "prediction_feedback_learning": _safe_dict(trading.get("prediction_feedback_learning")),
        "ctrader_integration": _safe_dict(trading.get("ctrader_integration")),
        "warnings": trading_warnings,
        "agent_id": trading_agent.get("agent_id", "trading_agent"),
        "can_execute": False,
        "capabilities": _safe_list(trading_agent.get("capabilities")),
        "read_only": True,
        "placeholder": "Future Trading panel can show analysis and prediction feedback only, without order controls.",
    }


def _build_activity_feed_panel(runtime_events: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": runtime_events.get("status", "planned/live_foundation"),
        "events": _safe_list(runtime_events.get("events")),
        "warnings": [
            str(warning)
            for warning in _safe_list(runtime_events.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "placeholder": "Future Activity Feed panel can render runtime events without a background loop.",
    }


def _build_taskline_panel(activity_timeline: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": activity_timeline.get("status", "planned/live_foundation"),
        "entries": _safe_list(activity_timeline.get("entries")),
        "warnings": [
            str(warning)
            for warning in _safe_list(activity_timeline.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "placeholder": "Future Taskline panel can render activity timeline entries without a background loop.",
    }


def _build_home_dashboard_panel(jarvis_home_dashboard: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": "planned/live_foundation",
        "dashboard_version": jarvis_home_dashboard.get("dashboard_version", "v1"),
        "primary_tiles": _safe_list(jarvis_home_dashboard.get("primary_tiles")),
        "market_watch": _safe_dict(jarvis_home_dashboard.get("market_watch")),
        "weather": _safe_dict(jarvis_home_dashboard.get("weather")),
        "active_agents": _safe_dict(jarvis_home_dashboard.get("active_agents")),
        "taskline": _safe_dict(jarvis_home_dashboard.get("taskline")),
        "runtime": _safe_dict(jarvis_home_dashboard.get("runtime")),
        "warnings": [
            str(warning)
            for warning in _safe_list(jarvis_home_dashboard.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "placeholder": "Future Jarvis Home Dashboard can render primary tiles without live quote or weather fetches.",
    }


def _build_runtime_supervisor_panel(runtime_supervisor: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": runtime_supervisor.get("status", "unavailable"),
        "heartbeat": _safe_dict(runtime_supervisor.get("heartbeat")),
        "scheduler": _safe_dict(runtime_supervisor.get("scheduler")),
        "agent_lifecycle": _safe_dict(runtime_supervisor.get("agent_lifecycle")),
        "zombie_protection": _safe_dict(runtime_supervisor.get("zombie_protection")),
        "context_lifecycle": _safe_dict(runtime_supervisor.get("context_lifecycle")),
        "context_compression": _safe_dict(runtime_supervisor.get("context_compression")),
        "resource_limits": _safe_dict(runtime_supervisor.get("resource_limits")),
        "runtime_cleanup": _safe_dict(runtime_supervisor.get("runtime_cleanup")),
        "planned_jobs": _safe_list(runtime_supervisor.get("planned_jobs")),
        "warnings": [
            str(warning)
            for warning in _safe_list(runtime_supervisor.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "controls_enabled": False,
        "background_loops_started": bool(runtime_supervisor.get("background_loops_started", False)),
        "threads_started": bool(runtime_supervisor.get("threads_started", False)),
        "services_started": bool(runtime_supervisor.get("services_started", False)),
        "runtime_files_written": bool(runtime_supervisor.get("runtime_files_written", False)),
        "placeholder": "Future Runtime Supervisor panel can display planned jobs without starting schedulers.",
    }


def _build_shared_memory_panel(shared_memory: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": shared_memory.get("status", "unavailable"),
        "sync_strategy": _safe_dict(shared_memory.get("sync_strategy")),
        "local_only_paths": _safe_list(shared_memory.get("local_only_paths")),
        "shared_candidate_paths": _safe_list(shared_memory.get("shared_candidate_paths")),
        "approval_workflow": _safe_dict(shared_memory.get("approval_workflow")),
        "multi_pc_roles": _safe_dict(shared_memory.get("multi_pc_roles")),
        "warnings": [
            str(warning)
            for warning in _safe_list(shared_memory.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "controls_enabled": False,
        "sync_actions_performed": bool(shared_memory.get("sync_actions_performed", False)),
        "network_connections_opened": bool(shared_memory.get("network_connections_opened", False)),
        "files_copied": bool(shared_memory.get("files_copied", False)),
        "runtime_files_written": bool(shared_memory.get("runtime_files_written", False)),
        "secrets_read": bool(shared_memory.get("secrets_read", False)),
        "placeholder": "Future Shared Memory panel can show multi-PC policy without syncing files.",
    }


def _build_skills_panel(skills: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": skills.get("status", "unavailable"),
        "skill_root_candidates": _safe_list(skills.get("skill_root_candidates")),
        "planned_skill_categories": _safe_list(skills.get("planned_skill_categories")),
        "skill_registry": _safe_dict(skills.get("skill_registry")),
        "skill_review_workflow": _safe_dict(skills.get("skill_review_workflow")),
        "skill_safety": _safe_dict(skills.get("skill_safety")),
        "external_pattern_sources": _safe_list(skills.get("external_pattern_sources")),
        "warnings": [
            str(warning)
            for warning in _safe_list(skills.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "controls_enabled": False,
        "skills_executed": bool(skills.get("skills_executed", False)),
        "external_repos_cloned": bool(skills.get("external_repos_cloned", False)),
        "skills_generated": bool(skills.get("skills_generated", False)),
        "runtime_files_written": bool(skills.get("runtime_files_written", False)),
        "services_started": bool(skills.get("services_started", False)),
        "placeholder": "Future Skills panel can show registry and review status without executing skills.",
    }


def _build_research_discovery_panel(research_discovery: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": research_discovery.get("status", "unavailable"),
        "research_sources": _safe_list(research_discovery.get("research_sources")),
        "monitored_topics": _safe_list(research_discovery.get("monitored_topics")),
        "discovery_pipeline": _safe_dict(research_discovery.get("discovery_pipeline")),
        "review_workflow": _safe_dict(research_discovery.get("review_workflow")),
        "safety_rules": _safe_dict(research_discovery.get("safety_rules")),
        "planned_reports": _safe_list(research_discovery.get("planned_reports")),
        "warnings": [
            str(warning)
            for warning in _safe_list(research_discovery.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "controls_enabled": False,
        "external_queries_performed": bool(
            research_discovery.get("external_queries_performed", False)
        ),
        "api_calls_performed": bool(research_discovery.get("api_calls_performed", False)),
        "scheduler_started": bool(research_discovery.get("scheduler_started", False)),
        "background_loops_started": bool(
            research_discovery.get("background_loops_started", False)
        ),
        "runtime_files_written": bool(research_discovery.get("runtime_files_written", False)),
        "services_started": bool(research_discovery.get("services_started", False)),
        "placeholder": "Future Research Discovery panel can show discovery planning without external queries.",
    }


def _build_cost_optimization_panel(cost_optimization: dict[str, Any]) -> dict[str, Any]:
    return {
        "status": cost_optimization.get("status", "unavailable"),
        "codex_usage_strategy": _safe_list(cost_optimization.get("codex_usage_strategy")),
        "fast_mode_policy": _safe_dict(cost_optimization.get("fast_mode_policy")),
        "provider_priority": _safe_list(cost_optimization.get("provider_priority")),
        "cost_controls": _safe_dict(cost_optimization.get("cost_controls")),
        "monitored_resources": _safe_list(cost_optimization.get("monitored_resources")),
        "future_dashboards": _safe_list(cost_optimization.get("future_dashboards")),
        "warnings": [
            str(warning)
            for warning in _safe_list(cost_optimization.get("warnings"))
            if str(warning).strip()
        ],
        "read_only": True,
        "controls_enabled": False,
        "api_calls_performed": bool(cost_optimization.get("api_calls_performed", False)),
        "openrouter_queries_performed": bool(
            cost_optimization.get("openrouter_queries_performed", False)
        ),
        "codex_queries_performed": bool(
            cost_optimization.get("codex_queries_performed", False)
        ),
        "model_calls_performed": bool(cost_optimization.get("model_calls_performed", False)),
        "secrets_read": bool(cost_optimization.get("secrets_read", False)),
        "runtime_files_written": bool(cost_optimization.get("runtime_files_written", False)),
        "services_started": bool(cost_optimization.get("services_started", False)),
        "placeholder": "Future Cost Optimization panel can show provider policy without cloud calls.",
    }


def _build_ui_panels(
    optional_task: str | None,
    snapshot: dict[str, Any],
    brain: dict[str, Any],
    learning_memory: dict[str, Any],
    developer_debug: dict[str, Any],
    voice: dict[str, Any],
    trading: dict[str, Any],
    runtime_events: dict[str, Any],
    activity_timeline: dict[str, Any],
    jarvis_home_dashboard: dict[str, Any],
    runtime_supervisor: dict[str, Any],
    shared_memory: dict[str, Any],
    skills: dict[str, Any],
    research_discovery: dict[str, Any],
    cost_optimization: dict[str, Any],
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
        "developer_debug_panel": _build_developer_debug_panel(system_health, snapshot, developer_debug),
        "voice_panel": _build_voice_panel(voice),
        "trading_panel": _build_trading_panel(agent_dashboard, trading),
        "activity_feed_panel": _build_activity_feed_panel(runtime_events),
        "taskline_panel": _build_taskline_panel(activity_timeline),
        "home_dashboard_panel": _build_home_dashboard_panel(jarvis_home_dashboard),
        "runtime_supervisor_panel": _build_runtime_supervisor_panel(runtime_supervisor),
        "shared_memory_panel": _build_shared_memory_panel(shared_memory),
        "skills_panel": _build_skills_panel(skills),
        "research_discovery_panel": _build_research_discovery_panel(research_discovery),
        "cost_optimization_panel": _build_cost_optimization_panel(cost_optimization),
    }


def _fallback_status(
    warnings: list[str],
    learning_memory: dict[str, Any] | None = None,
    developer_debug: dict[str, Any] | None = None,
    voice: dict[str, Any] | None = None,
    trading: dict[str, Any] | None = None,
    runtime_events: dict[str, Any] | None = None,
    activity_timeline: dict[str, Any] | None = None,
    jarvis_home_dashboard: dict[str, Any] | None = None,
    runtime_supervisor: dict[str, Any] | None = None,
    shared_memory: dict[str, Any] | None = None,
    skills: dict[str, Any] | None = None,
    research_discovery: dict[str, Any] | None = None,
    cost_optimization: dict[str, Any] | None = None,
) -> dict[str, Any]:
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
    developer_debug = developer_debug or {
        "generated_at": None,
        "available_debug_modules": [],
        "available_cli_checks": [],
        "suggested_test_commands": [],
        "warnings": warnings,
    }
    voice = voice or _empty_voice_status(warnings)
    trading = trading or _empty_trading_panel_status(warnings)
    runtime_events = runtime_events or _empty_runtime_events_status(warnings)
    activity_timeline = activity_timeline or _empty_activity_timeline_status(warnings)
    jarvis_home_dashboard = jarvis_home_dashboard or _empty_home_dashboard_status(warnings)
    runtime_supervisor = runtime_supervisor or _empty_runtime_supervisor_status(warnings)
    shared_memory = shared_memory or _empty_shared_memory_status(warnings)
    skills = skills or _empty_skills_status(warnings)
    research_discovery = research_discovery or _empty_research_discovery_status(warnings)
    cost_optimization = cost_optimization or _empty_cost_optimization_status(warnings)
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
        "developer_debug": developer_debug,
        "voice": voice,
        "trading": trading,
        "runtime_events": runtime_events,
        "activity_timeline": activity_timeline,
        "jarvis_home_dashboard": jarvis_home_dashboard,
        "runtime_supervisor": runtime_supervisor,
        "shared_memory": shared_memory,
        "skills": skills,
        "research_discovery": research_discovery,
        "cost_optimization": cost_optimization,
        "system_health": system_health,
        "ui_panels": _build_ui_panels(
            None,
            snapshot,
            brain,
            learning_memory,
            developer_debug,
            voice,
            trading,
            runtime_events,
            activity_timeline,
            jarvis_home_dashboard,
            runtime_supervisor,
            shared_memory,
            skills,
            research_discovery,
            cost_optimization,
        ),
    }


def build_hermes_ui_status(optional_task: str | None = None) -> dict[str, Any]:
    warnings: list[str] = []
    learning_memory = _build_learning_memory_status(warnings)
    developer_debug = _build_developer_debug_status(warnings)
    voice = _build_voice_status(warnings)
    trading = _build_trading_panel_status(warnings)
    runtime_events = _build_runtime_events_status(warnings)
    activity_timeline = _build_activity_timeline_status(warnings)
    jarvis_home_dashboard = _build_home_dashboard_status(warnings)
    runtime_supervisor = _build_runtime_supervisor_status(warnings)
    shared_memory = _build_shared_memory_status(warnings)
    skills = _build_skills_status(warnings)
    research_discovery = _build_research_discovery_status(warnings)
    cost_optimization = _build_cost_optimization_status(warnings)
    snapshot_builder = _import_snapshot_builder(warnings)
    if snapshot_builder is None:
        return _fallback_status(
            warnings,
            learning_memory,
            developer_debug,
            voice,
            trading,
            runtime_events,
            activity_timeline,
            jarvis_home_dashboard,
            runtime_supervisor,
            shared_memory,
            skills,
            research_discovery,
            cost_optimization,
        )

    try:
        snapshot = snapshot_builder(optional_task)
    except Exception as exc:
        warnings.append(f"build_hermes_system_snapshot failed: {exc}")
        return _fallback_status(
            warnings,
            learning_memory,
            developer_debug,
            voice,
            trading,
            runtime_events,
            activity_timeline,
            jarvis_home_dashboard,
            runtime_supervisor,
            shared_memory,
            skills,
            research_discovery,
            cost_optimization,
        )

    if not isinstance(snapshot, dict):
        warnings.append("build_hermes_system_snapshot returned non-dict data.")
        return _fallback_status(
            warnings,
            learning_memory,
            developer_debug,
            voice,
            trading,
            runtime_events,
            activity_timeline,
            jarvis_home_dashboard,
            runtime_supervisor,
            shared_memory,
            skills,
            research_discovery,
            cost_optimization,
        )

    system_health = _safe_dict(snapshot.get("system_health_summary"))
    existing_warnings = _get_warnings(system_health)
    merged_warnings = existing_warnings + [
        warning for warning in warnings if warning not in existing_warnings
    ]
    for warning in _safe_list(learning_memory.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(developer_debug.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(voice.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(trading.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(runtime_events.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(activity_timeline.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip() and warning_text not in merged_warnings:
            merged_warnings.append(warning_text)
    for warning in _safe_list(jarvis_home_dashboard.get("warnings")):
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
        "developer_debug": developer_debug,
        "voice": voice,
        "trading": trading,
        "runtime_events": runtime_events,
        "activity_timeline": activity_timeline,
        "jarvis_home_dashboard": jarvis_home_dashboard,
        "runtime_supervisor": runtime_supervisor,
        "shared_memory": shared_memory,
        "skills": skills,
        "research_discovery": research_discovery,
        "cost_optimization": cost_optimization,
        "system_health": system_health,
        "ui_panels": _build_ui_panels(
            optional_task,
            snapshot,
            brain,
            learning_memory,
            developer_debug,
            voice,
            trading,
            runtime_events,
            activity_timeline,
            jarvis_home_dashboard,
            runtime_supervisor,
            shared_memory,
            skills,
            research_discovery,
            cost_optimization,
        ),
    }


def main() -> int:
    optional_task = " ".join(sys.argv[1:]).strip() or None
    print(json.dumps(build_hermes_ui_status(optional_task), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
