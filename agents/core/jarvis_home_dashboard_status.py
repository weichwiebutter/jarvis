#!/usr/bin/env python3
"""
Jarvis Home Dashboard Status

Builds a read-only data model for the future Jarvis Home Dashboard v1.
This module does not connect to cTrader, fetch live quotes, call weather APIs,
start services, or write runtime files.
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


DASHBOARD_VERSION = "v1"
PRIMARY_TILE_TITLES = [
    "XAUUSD",
    "EURUSD",
    "Weather",
    "Active Agents",
    "Taskline",
    "Hermes Status",
    "Ollama Status",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _append_warning(warnings: list[str], warning: str) -> None:
    if warning.strip() and warning not in warnings:
        warnings.append(warning)


def _safe_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _safe_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _import_callable(
    module_name: str,
    function_name: str,
    warnings: list[str],
) -> Callable[..., Any] | None:
    try:
        module = importlib.import_module(module_name)
        function = getattr(module, function_name)
    except Exception as exc:
        _append_warning(warnings, f"{module_name}.{function_name} unavailable: {exc}")
        return None

    if not callable(function):
        _append_warning(warnings, f"{module_name}.{function_name} is not callable.")
        return None

    return function


def _build_market_watch() -> dict[str, Any]:
    return {
        "status": "planned",
        "quote_only": True,
        "no_auto_trading": True,
        "live_quotes_requested": False,
        "orders_enabled": False,
        "symbols": {
            "XAUUSD": {
                "source": "planned_ctrader_quote",
                "live_status": "planned",
                "quote_only": True,
                "last_quote": None,
            },
            "EURUSD": {
                "source": "planned_ctrader_quote",
                "live_status": "planned",
                "quote_only": True,
                "last_quote": None,
            },
        },
    }


def _fallback_weather_status(warnings: list[str]) -> dict[str, Any]:
    return {
        "generated_at": None,
        "status": "planned",
        "location": "Frankfurt,DE",
        "provider": "planned_weather_provider",
        "temperature": None,
        "condition": None,
        "wind": None,
        "api_called": False,
        "warnings": warnings,
        "read_only": True,
    }


def _build_weather_status(warnings: list[str]) -> dict[str, Any]:
    builder = _import_callable(
        "agents.core.jarvis_weather_status",
        "build_weather_status",
        warnings,
    )
    if builder is None:
        return _fallback_weather_status(warnings)

    try:
        weather = builder()
    except Exception as exc:
        warning = f"build_weather_status failed: {exc}"
        _append_warning(warnings, warning)
        return _fallback_weather_status([warning])

    if not isinstance(weather, dict):
        warning = "build_weather_status returned non-dict data."
        _append_warning(warnings, warning)
        return _fallback_weather_status([warning])

    for warning in _safe_list(weather.get("warnings")):
        warning_text = str(warning)
        if warning_text.strip():
            _append_warning(warnings, warning_text)

    return weather


def _build_active_agents(warnings: list[str]) -> dict[str, Any]:
    builder = _import_callable(
        "agents.core.hermes_agent_dashboard",
        "build_agent_dashboard_status",
        warnings,
    )
    if builder is None:
        return {
            "status": "unavailable",
            "agents": [],
            "available_count": 0,
            "planned_count": 0,
            "disabled_count": 0,
            "warnings": warnings,
        }

    try:
        dashboard = builder()
    except Exception as exc:
        warning = f"build_agent_dashboard_status failed: {exc}"
        _append_warning(warnings, warning)
        return {
            "status": "unavailable",
            "agents": [],
            "available_count": 0,
            "planned_count": 0,
            "disabled_count": 0,
            "warnings": [warning],
        }

    if not isinstance(dashboard, dict):
        warning = "build_agent_dashboard_status returned non-dict data."
        _append_warning(warnings, warning)
        return {
            "status": "unavailable",
            "agents": [],
            "available_count": 0,
            "planned_count": 0,
            "disabled_count": 0,
            "warnings": [warning],
        }

    agents = [
        agent
        for agent in _safe_list(dashboard.get("agents"))
        if isinstance(agent, dict)
    ]

    return {
        "status": "available",
        "generated_at": dashboard.get("generated_at"),
        "agents": agents,
        "available_count": sum(1 for agent in agents if agent.get("status") == "available"),
        "planned_count": sum(1 for agent in agents if agent.get("status") == "planned"),
        "disabled_count": sum(1 for agent in agents if agent.get("status") == "disabled"),
        "warnings": [],
        "read_only": True,
    }


def _build_taskline(warnings: list[str]) -> dict[str, Any]:
    timeline_builder = _import_callable(
        "agents.core.hermes_activity_timeline",
        "build_demo_activity_timeline",
        warnings,
    )
    serializer = _import_callable(
        "agents.core.hermes_activity_timeline",
        "serialize_timeline_entry",
        warnings,
    )

    if timeline_builder is None or serializer is None:
        return {
            "status": "planned/live_foundation",
            "entries": [],
            "warnings": warnings,
            "read_only": True,
        }

    try:
        entries = timeline_builder()
    except Exception as exc:
        warning = f"build_demo_activity_timeline failed: {exc}"
        _append_warning(warnings, warning)
        return {
            "status": "planned/live_foundation",
            "entries": [],
            "warnings": [warning],
            "read_only": True,
        }

    if not isinstance(entries, list):
        warning = "build_demo_activity_timeline returned non-list data."
        _append_warning(warnings, warning)
        return {
            "status": "planned/live_foundation",
            "entries": [],
            "warnings": [warning],
            "read_only": True,
        }

    serialized_entries: list[dict[str, Any]] = []
    taskline_warnings: list[str] = []
    for index, entry in enumerate(entries):
        try:
            serialized_entry = serializer(entry)
        except Exception as exc:
            warning = f"serialize_timeline_entry failed for entry {index}: {exc}"
            _append_warning(taskline_warnings, warning)
            _append_warning(warnings, warning)
            continue

        if not isinstance(serialized_entry, dict):
            warning = f"serialize_timeline_entry returned non-dict data for entry {index}."
            _append_warning(taskline_warnings, warning)
            _append_warning(warnings, warning)
            continue

        serialized_entries.append(serialized_entry)

    return {
        "status": "planned/live_foundation",
        "entries": serialized_entries,
        "warnings": taskline_warnings,
        "read_only": True,
    }


def _build_runtime(warnings: list[str]) -> dict[str, Any]:
    builder = _import_callable(
        "agents.core.hermes_runtime_status",
        "build_runtime_status",
        warnings,
    )
    if builder is None:
        return {
            "status": "unavailable",
            "warnings": warnings,
            "read_only": True,
        }

    try:
        runtime = builder()
    except Exception as exc:
        warning = f"build_runtime_status failed: {exc}"
        _append_warning(warnings, warning)
        return {
            "status": "unavailable",
            "warnings": [warning],
            "read_only": True,
        }

    if not isinstance(runtime, dict):
        warning = "build_runtime_status returned non-dict data."
        _append_warning(warnings, warning)
        return {
            "status": "unavailable",
            "warnings": [warning],
            "read_only": True,
        }

    ollama_status = _safe_dict(runtime.get("ollama_status"))
    if ollama_status.get("status") not in {None, "available"}:
        error = ollama_status.get("stderr") or ollama_status.get("error") or "not available"
        _append_warning(warnings, f"ollama unavailable: {error}")

    return {
        **runtime,
        "status": "available",
        "read_only": True,
    }


def _build_online_status(runtime: dict[str, Any]) -> dict[str, Any]:
    hermes_status = _safe_dict(runtime.get("hermes_status"))
    ollama_status = _safe_dict(runtime.get("ollama_status"))

    return {
        "status": "read_only_status_model",
        "hermes_available": hermes_status.get("status") == "available",
        "ollama_available": ollama_status.get("status") == "available",
        "external_market_data_connected": False,
        "weather_api_connected": False,
        "services_started": False,
        "runtime_files_written": False,
    }


def _build_primary_tiles(
    market_watch: dict[str, Any],
    weather: dict[str, Any],
    active_agents: dict[str, Any],
    taskline: dict[str, Any],
    runtime: dict[str, Any],
) -> list[dict[str, Any]]:
    symbols = _safe_dict(market_watch.get("symbols"))
    hermes_status = _safe_dict(runtime.get("hermes_status"))
    ollama_status = _safe_dict(runtime.get("ollama_status"))

    return [
        {
            "tile_id": "xauusd",
            "title": "XAUUSD",
            "status": _safe_dict(symbols.get("XAUUSD")).get("live_status", "planned"),
            "source": _safe_dict(symbols.get("XAUUSD")).get("source", "planned_ctrader_quote"),
            "quote_only": True,
        },
        {
            "tile_id": "eurusd",
            "title": "EURUSD",
            "status": _safe_dict(symbols.get("EURUSD")).get("live_status", "planned"),
            "source": _safe_dict(symbols.get("EURUSD")).get("source", "planned_ctrader_quote"),
            "quote_only": True,
        },
        {
            "tile_id": "weather",
            "title": "Weather",
            "status": weather.get("status", "planned"),
            "source": weather.get("provider", weather.get("source", "planned_weather_provider")),
            "location": weather.get("location"),
            "temperature": weather.get("temperature"),
            "condition": weather.get("condition"),
            "api_called": bool(weather.get("api_called", False)),
        },
        {
            "tile_id": "active_agents",
            "title": "Active Agents",
            "status": active_agents.get("status", "unavailable"),
            "available_count": active_agents.get("available_count", 0),
            "planned_count": active_agents.get("planned_count", 0),
        },
        {
            "tile_id": "taskline",
            "title": "Taskline",
            "status": taskline.get("status", "planned/live_foundation"),
            "entries_count": len(_safe_list(taskline.get("entries"))),
        },
        {
            "tile_id": "hermes_status",
            "title": "Hermes Status",
            "status": hermes_status.get("status", "unavailable"),
            "module": hermes_status.get("module"),
        },
        {
            "tile_id": "ollama_status",
            "title": "Ollama Status",
            "status": ollama_status.get("status", "not_checked"),
            "available": bool(ollama_status.get("available", False)),
        },
    ]


def build_jarvis_home_dashboard_status() -> dict[str, Any]:
    warnings: list[str] = []

    market_watch = _build_market_watch()
    weather = _build_weather_status(warnings)
    active_agents = _build_active_agents(warnings)
    taskline = _build_taskline(warnings)
    runtime = _build_runtime(warnings)
    online_status = _build_online_status(runtime)
    primary_tiles = _build_primary_tiles(
        market_watch=market_watch,
        weather=weather,
        active_agents=active_agents,
        taskline=taskline,
        runtime=runtime,
    )

    return {
        "generated_at": utc_now(),
        "dashboard_version": DASHBOARD_VERSION,
        "online_status": online_status,
        "primary_tiles": primary_tiles,
        "market_watch": market_watch,
        "weather": weather,
        "active_agents": active_agents,
        "taskline": taskline,
        "runtime": runtime,
        "warnings": warnings,
    }


def main() -> int:
    print(json.dumps(build_jarvis_home_dashboard_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
