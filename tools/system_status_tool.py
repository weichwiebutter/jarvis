#!/usr/bin/env python3
"""
Jarvis System Status Tool

Checks and optionally starts the local OpenJarvis backend in WSL.
This tool is intentionally local-first and does not call cloud services.
"""

from __future__ import annotations

import subprocess
import time
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any, Dict

import requests


@dataclass
class SystemStatusResult:
    ok: bool
    backend_running: bool
    backend_started: bool
    health_url: str
    message: str
    error: str | None = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


def expand_path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def is_backend_running(health_url: str, timeout_seconds: int = 5) -> bool:
    try:
        response = requests.get(health_url, timeout=timeout_seconds)
        return response.status_code < 500
    except requests.RequestException:
        return False


def start_backend(config: Dict[str, Any]) -> SystemStatusResult:
    backend_cfg = config.get("backend", {})
    logging_cfg = config.get("logging", {})

    health_url = backend_cfg.get("health_url", "http://127.0.0.1:8000/v1/models")
    openjarvis_dir = expand_path(backend_cfg.get("openjarvis_dir", "~/jarvis/OpenJarvis"))
    start_command = backend_cfg.get(
        "start_command", "OLLAMA_MODEL=llama3.2:3b ./scripts/quickstart.sh"
    )
    timeout_seconds = int(backend_cfg.get("startup_timeout_seconds", 90))
    openjarvis_log = expand_path(logging_cfg.get("openjarvis_log", "~/jarvis/logs/openjarvis.log"))
    openjarvis_log.parent.mkdir(parents=True, exist_ok=True)

    if is_backend_running(health_url):
        return SystemStatusResult(
            ok=True,
            backend_running=True,
            backend_started=False,
            health_url=health_url,
            message="OpenJarvis backend is already running.",
        )

    if not bool(backend_cfg.get("autostart", True)):
        return SystemStatusResult(
            ok=False,
            backend_running=False,
            backend_started=False,
            health_url=health_url,
            message="OpenJarvis backend is not running and autostart is disabled.",
        )

    if not openjarvis_dir.exists():
        return SystemStatusResult(
            ok=False,
            backend_running=False,
            backend_started=False,
            health_url=health_url,
            message="OpenJarvis directory not found.",
            error=str(openjarvis_dir),
        )

    with open(openjarvis_log, "ab") as log_file:
        subprocess.Popen(
            ["bash", "-lc", start_command],
            cwd=str(openjarvis_dir),
            stdout=log_file,
            stderr=subprocess.STDOUT,
            start_new_session=True,
        )

    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if is_backend_running(health_url):
            return SystemStatusResult(
                ok=True,
                backend_running=True,
                backend_started=True,
                health_url=health_url,
                message="OpenJarvis backend started successfully.",
            )
        time.sleep(2)

    return SystemStatusResult(
        ok=False,
        backend_running=False,
        backend_started=True,
        health_url=health_url,
        message="OpenJarvis backend did not become ready within timeout.",
        error=f"Check log: {openjarvis_log}",
    )
