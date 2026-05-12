#!/usr/bin/env python3
"""
Hermes Runtime Status

Builds a read-only runtime overview for the future Jarvis Control Center.
This module does not start services, stop services, kill processes, or write
runtime files.
"""

from __future__ import annotations

import importlib
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _relative_path(path: Path) -> str:
    try:
        return str(path.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(path)


def _path_status(path: Path) -> dict[str, Any]:
    return {
        "path": _relative_path(path),
        "exists": path.exists(),
        "is_dir": path.is_dir(),
    }


def build_hermes_status() -> dict[str, Any]:
    module_name = "agents.core.hermes_router"

    try:
        importlib.import_module(module_name)
        return {
            "status": "available",
            "module": module_name,
            "importable": True,
        }
    except Exception as exc:
        return {
            "status": "unavailable",
            "module": module_name,
            "importable": False,
            "error": str(exc),
        }


def _parse_ollama_models(output: str) -> list[str]:
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    if len(lines) <= 1:
        return []

    models: list[str] = []
    for line in lines[1:]:
        parts = line.split()
        if parts:
            models.append(parts[0])

    return models


def build_ollama_status() -> dict[str, Any]:
    command = ["ollama", "list"]

    try:
        result = subprocess.run(
            command,
            cwd=PROJECT_ROOT,
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        )
    except FileNotFoundError as exc:
        return {
            "status": "not_configured",
            "checked": True,
            "command": "ollama list",
            "available": False,
            "error": str(exc),
        }
    except subprocess.TimeoutExpired as exc:
        return {
            "status": "unavailable",
            "checked": True,
            "command": "ollama list",
            "available": False,
            "error": f"Command timed out after {exc.timeout} seconds.",
        }
    except Exception as exc:
        return {
            "status": "unavailable",
            "checked": True,
            "command": "ollama list",
            "available": False,
            "error": str(exc),
        }

    stdout = result.stdout.strip()
    stderr = result.stderr.strip()

    return {
        "status": "available" if result.returncode == 0 else "unavailable",
        "checked": True,
        "command": "ollama list",
        "available": result.returncode == 0,
        "returncode": result.returncode,
        "models": _parse_ollama_models(stdout),
        "stdout": stdout,
        "stderr": stderr,
    }


def build_memory_status() -> dict[str, Any]:
    hermes_path = PROJECT_ROOT / ".hermes"
    exists = hermes_path.exists()

    return {
        "status": "available" if exists else "not_configured",
        "path": _relative_path(hermes_path),
        "exists": exists,
        "is_dir": hermes_path.is_dir(),
    }


def build_voice_status() -> dict[str, Any]:
    voice_client = PROJECT_ROOT / "voice_client.py"
    voice_requirements = PROJECT_ROOT / "installer" / "requirements_voice.txt"

    has_voice_artifacts = voice_client.exists() or voice_requirements.exists()

    return {
        "status": "planned" if has_voice_artifacts else "not_configured",
        "configured": False,
        "voice_client_exists": voice_client.exists(),
        "voice_requirements_exists": voice_requirements.exists(),
        "note": "Read-only status only; voice services are not started or checked.",
    }


def build_git_status() -> dict[str, Any]:
    command = ["git", "status", "--short"]

    try:
        result = subprocess.run(
            command,
            cwd=PROJECT_ROOT,
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        )
    except Exception as exc:
        return {
            "status": "unavailable",
            "command": "git status --short",
            "clean": False,
            "error": str(exc),
        }

    lines = [line for line in result.stdout.splitlines() if line.strip()]

    return {
        "status": "available" if result.returncode == 0 else "unavailable",
        "command": "git status --short",
        "clean": result.returncode == 0 and not lines,
        "returncode": result.returncode,
        "changed_files": lines,
        "stderr": result.stderr.strip(),
    }


def build_runtime_paths() -> dict[str, dict[str, Any]]:
    return {
        ".hermes": _path_status(PROJECT_ROOT / ".hermes"),
        "runtime": _path_status(PROJECT_ROOT / "runtime"),
        "logs": _path_status(PROJECT_ROOT / "logs"),
    }


def build_runtime_status() -> dict[str, Any]:
    return {
        "generated_at": utc_now(),
        "hermes_status": build_hermes_status(),
        "ollama_status": build_ollama_status(),
        "memory_status": build_memory_status(),
        "voice_status": build_voice_status(),
        "git_status": build_git_status(),
        "runtime_paths": build_runtime_paths(),
    }


def main() -> int:
    print(json.dumps(build_runtime_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
