#!/usr/bin/env python3
"""
Hermes Developer Debug Status

Builds a read-only developer/debug status object for Jarvis/Hermes. This module
checks module importability only; it does not run agents, start services, or
write runtime files.
"""

from __future__ import annotations

import importlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


DEBUG_MODULES = [
    "hermes_router",
    "hermes_agent_dashboard",
    "hermes_runtime_status",
    "hermes_system_snapshot",
    "hermes_ui_status",
    "hermes_learning_memory_status",
    "hermes_adaptive_routing",
    "hermes_planner",
    "hermes_orchestrator",
    "hermes_execution_engine",
    "hermes_learning_feedback",
]

SUGGESTED_CLI_CHECKS = [
    {
        "name": "hermes_router_sample",
        "command": 'python3 agents/core/hermes_router.py "Analysiere XAUUSD auf M15"',
    },
    {
        "name": "agent_dashboard_status",
        "command": "python3 agents/core/hermes_agent_dashboard.py",
    },
    {
        "name": "runtime_status",
        "command": "python3 agents/core/hermes_runtime_status.py",
    },
    {
        "name": "system_snapshot",
        "command": "python3 agents/core/hermes_system_snapshot.py",
    },
    {
        "name": "ui_status",
        "command": "python3 agents/core/hermes_ui_status.py",
    },
    {
        "name": "learning_memory_status",
        "command": "python3 agents/core/hermes_learning_memory_status.py",
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _module_status(module_short_name: str, warnings: list[str]) -> dict[str, Any]:
    module_name = f"agents.core.{module_short_name}"

    try:
        module = importlib.import_module(module_name)
    except Exception as exc:
        warning = f"{module_name} import failed: {exc}"
        warnings.append(warning)
        return {
            "module": module_short_name,
            "module_path": module_name,
            "importable": False,
            "status": "unavailable",
            "error": str(exc),
        }

    module_file = getattr(module, "__file__", None)
    return {
        "module": module_short_name,
        "module_path": module_name,
        "importable": True,
        "status": "available",
        "file": str(Path(module_file).relative_to(PROJECT_ROOT)) if module_file else None,
    }


def build_developer_debug_status() -> dict[str, Any]:
    warnings: list[str] = []
    module_statuses = [
        _module_status(module_name, warnings)
        for module_name in DEBUG_MODULES
    ]

    return {
        "generated_at": utc_now(),
        "available_debug_modules": module_statuses,
        "available_cli_checks": [
            {
                "name": check["name"],
                "command": check["command"],
                "read_only": True,
            }
            for check in SUGGESTED_CLI_CHECKS
        ],
        "suggested_test_commands": [
            check["command"]
            for check in SUGGESTED_CLI_CHECKS
        ],
        "warnings": warnings,
    }


def main() -> int:
    print(json.dumps(build_developer_debug_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
