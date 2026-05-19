#!/usr/bin/env python3
"""
Hermes Runtime v1 Status

Read-only bridge for the real HermesRuntime v1 health report. This module only
reads HermesRuntime/data/reports/runtime_health.json. It does not start the
runtime, call services, open sockets, or write files.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
RUNTIME_HEALTH_PATH = PROJECT_ROOT / "HermesRuntime" / "data" / "reports" / "runtime_health.json"


def _empty_status(status: str, warning: str) -> dict[str, Any]:
    return {
        "status": status,
        "runtime_state": None,
        "safe_mode": None,
        "no_auto_trading": None,
        "human_review_required": None,
        "free_disk_gb": None,
        "pending_jobs": None,
        "running_jobs": None,
        "failed_jobs": None,
        "quarantined_jobs": None,
        "last_snapshot_id": None,
        "last_error": None,
        "source_path": str(RUNTIME_HEALTH_PATH),
        "warnings": [warning],
    }


def build_runtime_v1_status() -> dict[str, Any]:
    """
    Return the latest HermesRuntime v1 health status without side effects.
    """

    if not RUNTIME_HEALTH_PATH.exists():
        return _empty_status(
            "unavailable",
            f"HermesRuntime v1 health file not found: {RUNTIME_HEALTH_PATH}",
        )

    try:
        raw = json.loads(RUNTIME_HEALTH_PATH.read_text(encoding="utf-8"))
    except Exception as exc:
        return _empty_status(
            "error",
            f"HermesRuntime v1 health file could not be read: {exc}",
        )

    if not isinstance(raw, dict):
        return _empty_status(
            "error",
            "HermesRuntime v1 health file did not contain a JSON object.",
        )

    warnings: list[str] = []
    last_error = raw.get("last_error")
    if last_error:
        warnings.append(f"HermesRuntime v1 reported last_error: {last_error}")

    return {
        "status": "available",
        "runtime_state": raw.get("runtime_state"),
        "safe_mode": raw.get("safe_mode"),
        "no_auto_trading": raw.get("no_auto_trading"),
        "human_review_required": raw.get("human_review_required"),
        "free_disk_gb": raw.get("free_disk_gb"),
        "pending_jobs": raw.get("pending_jobs"),
        "running_jobs": raw.get("running_jobs"),
        "failed_jobs": raw.get("failed_jobs"),
        "quarantined_jobs": raw.get("quarantined_jobs"),
        "last_snapshot_id": raw.get("last_snapshot_id"),
        "last_error": last_error,
        "source_path": str(RUNTIME_HEALTH_PATH),
        "warnings": warnings,
    }


def main() -> int:
    print(json.dumps(build_runtime_v1_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
