#!/usr/bin/env python3
"""
Hermes Learning & Memory Status

Builds a read-only status object for the future Jarvis Learning & Memory panel.
This module only reads existing .hermes structures and does not create learnings,
write memory files, or modify runtime data.
"""

from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


HERMES_DIR = PROJECT_ROOT / ".hermes"
LEARNING_DIR = HERMES_DIR / "learning"
ROUTING_HINTS_DIR = HERMES_DIR / "routing_hints"
IMPROVEMENTS_DIR = HERMES_DIR / "improvements"

PREVIEW_LIMIT = 5
MAX_PREVIEW_STRING_LENGTH = 180
MAX_PREVIEW_LIST_ITEMS = 5
MAX_PREVIEW_DICT_KEYS = 12
MAX_ITEM_PREVIEW_COUNT = 3

SECRET_KEY_PARTS = {
    "api_key",
    "apikey",
    "authorization",
    "bearer",
    "credential",
    "credentials",
    "password",
    "private_key",
    "secret",
    "token",
}
SECRET_VALUE_PATTERNS = [
    re.compile(r"\bsk-[A-Za-z0-9_\-]{12,}\b"),
    re.compile(r"\bBearer\s+[A-Za-z0-9._\-]{12,}\b", re.IGNORECASE),
    re.compile(r"\b[A-Za-z0-9_\-]{24,}\.[A-Za-z0-9_\-]{12,}\.[A-Za-z0-9_\-]{12,}\b"),
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _relative_path(path: Path) -> str:
    try:
        return str(path.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(path)


def _contains_secret_key(key: Any) -> bool:
    key_text = str(key).strip().lower()
    return any(part in key_text for part in SECRET_KEY_PARTS)


def _redact_secret_values(value: str) -> str:
    redacted = value
    for pattern in SECRET_VALUE_PATTERNS:
        redacted = pattern.sub("[REDACTED]", redacted)
    return redacted


def _truncate(value: str) -> str:
    if len(value) <= MAX_PREVIEW_STRING_LENGTH:
        return value
    return f"{value[:MAX_PREVIEW_STRING_LENGTH]}..."


def _sanitize_preview(value: Any, depth: int = 0) -> Any:
    if depth >= 4:
        return "[TRUNCATED]"

    if isinstance(value, dict):
        sanitized: dict[str, Any] = {}
        for index, (key, item) in enumerate(value.items()):
            if index >= MAX_PREVIEW_DICT_KEYS:
                sanitized["..."] = "truncated"
                break

            key_text = str(key)
            if _contains_secret_key(key_text):
                sanitized[key_text] = "[REDACTED]"
            else:
                sanitized[key_text] = _sanitize_preview(item, depth + 1)

        return sanitized

    if isinstance(value, list):
        sanitized_list = [
            _sanitize_preview(item, depth + 1)
            for item in value[:MAX_PREVIEW_LIST_ITEMS]
        ]
        if len(value) > MAX_PREVIEW_LIST_ITEMS:
            sanitized_list.append("...")
        return sanitized_list

    if isinstance(value, str):
        return _truncate(_redact_secret_values(value))

    if isinstance(value, (bool, int, float)) or value is None:
        return value

    return _truncate(_redact_secret_values(str(value)))


def _safe_item_preview(item: Any, allowed_keys: set[str]) -> Any:
    if not isinstance(item, dict):
        return _sanitize_preview(item)

    return {
        key: _sanitize_preview(value)
        for key, value in item.items()
        if key in allowed_keys and not _contains_secret_key(key)
    }


def _safe_json_preview(payload: Any) -> Any:
    if isinstance(payload, list):
        return {
            "type": "list",
            "count": len(payload),
            "items_preview": [
                _sanitize_preview(item)
                for item in payload[:MAX_ITEM_PREVIEW_COUNT]
            ],
        }

    if not isinstance(payload, dict):
        return _sanitize_preview(payload)

    preview: dict[str, Any] = {
        "type": "dict",
        "top_level_keys": sorted(str(key) for key in payload.keys())[:MAX_PREVIEW_DICT_KEYS],
    }

    for key in ["ok", "status", "objective", "timestamp", "source_feedback_timestamp"]:
        if key in payload and not _contains_secret_key(key):
            preview[key] = _sanitize_preview(payload[key])

    learning_feedback = payload.get("learning_feedback")
    if isinstance(learning_feedback, dict):
        preview["learning_feedback"] = {
            "objective": _sanitize_preview(learning_feedback.get("objective")),
            "executed_steps": learning_feedback.get("executed_steps"),
            "skipped_steps": learning_feedback.get("skipped_steps"),
            "failed_steps": learning_feedback.get("failed_steps"),
            "success_patterns_count": len(learning_feedback.get("success_patterns") or []),
            "failure_patterns_count": len(learning_feedback.get("failure_patterns") or []),
            "recommended_improvements_count": len(
                learning_feedback.get("recommended_improvements") or []
            ),
            "future_routing_hints_count": len(
                learning_feedback.get("future_routing_hints") or []
            ),
        }

        approval_status = learning_feedback.get("approval_status")
        if isinstance(approval_status, dict):
            preview["learning_feedback"]["approval_status"] = {
                "status": _sanitize_preview(approval_status.get("status")),
                "blocked_by_approval": approval_status.get("blocked_by_approval"),
                "approval_required_steps": approval_status.get("approval_required_steps"),
            }

    routing_hints = payload.get("routing_hints")
    if isinstance(routing_hints, list):
        preview["routing_hints_count"] = len(routing_hints)
        preview["routing_hints_preview"] = [
            _safe_item_preview(
                item,
                {
                    "objective_contains",
                    "preferred_domain",
                    "preferred_agent",
                    "approval_policy",
                    "reason",
                },
            )
            for item in routing_hints[:MAX_ITEM_PREVIEW_COUNT]
        ]

    improvements = payload.get("recommended_improvements")
    if isinstance(improvements, list):
        preview["recommended_improvements_count"] = len(improvements)
        preview["recommended_improvements_preview"] = [
            _safe_item_preview(
                item,
                {
                    "area",
                    "recommendation",
                    "reason",
                },
            )
            for item in improvements[:MAX_ITEM_PREVIEW_COUNT]
        ]

    memory_candidates = payload.get("memory_candidates")
    if isinstance(memory_candidates, list):
        preview["memory_candidates_count"] = len(memory_candidates)
        preview["memory_candidates_preview"] = [
            _safe_item_preview(
                item,
                {
                    "category",
                    "title",
                    "requires_approval",
                    "prepared_only",
                },
            )
            for item in memory_candidates[:MAX_ITEM_PREVIEW_COUNT]
        ]

    return preview


def _json_files(directory: Path, warnings: list[str]) -> list[Path]:
    if not directory.exists():
        return []

    if not directory.is_dir():
        warnings.append(f"{_relative_path(directory)} exists but is not a directory.")
        return []

    try:
        files = [path for path in directory.iterdir() if path.is_file() and path.suffix == ".json"]
    except Exception as exc:
        warnings.append(f"Could not list {_relative_path(directory)}: {exc}")
        return []

    return sorted(files, key=lambda path: path.stat().st_mtime, reverse=True)


def _load_json_preview(path: Path, warnings: list[str]) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            payload = json.load(handle)
    except Exception as exc:
        warnings.append(f"Could not load {_relative_path(path)}: {exc}")
        return None

    return _safe_json_preview(payload)


def _file_preview(path: Path, warnings: list[str]) -> dict[str, Any]:
    try:
        stat = path.stat()
        modified_at = datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat()
        size_bytes = stat.st_size
    except Exception as exc:
        warnings.append(f"Could not stat {_relative_path(path)}: {exc}")
        modified_at = None
        size_bytes = None

    return {
        "file": path.name,
        "path": _relative_path(path),
        "modified_at": modified_at,
        "size_bytes": size_bytes,
        "preview": _load_json_preview(path, warnings),
    }


def _area_status(directory: Path, warnings: list[str]) -> dict[str, Any]:
    files = _json_files(directory, warnings)

    return {
        "path": _relative_path(directory),
        "exists": directory.exists(),
        "is_dir": directory.is_dir(),
        "json_files_count": len(files),
        "latest_items_preview": [
            _file_preview(path, warnings)
            for path in files[:PREVIEW_LIMIT]
        ],
    }


def build_learning_memory_status() -> dict[str, Any]:
    warnings: list[str] = []
    learning = _area_status(LEARNING_DIR, warnings)
    routing_hints = _area_status(ROUTING_HINTS_DIR, warnings)
    improvements = _area_status(IMPROVEMENTS_DIR, warnings)

    return {
        "generated_at": utc_now(),
        "memory_available": HERMES_DIR.exists() and HERMES_DIR.is_dir(),
        "learning_available": bool(learning["exists"] and learning["is_dir"]),
        "routing_hints_available": bool(routing_hints["exists"] and routing_hints["is_dir"]),
        "improvements_available": bool(improvements["exists"] and improvements["is_dir"]),
        "counts": {
            "learning": learning["json_files_count"],
            "routing_hints": routing_hints["json_files_count"],
            "improvements": improvements["json_files_count"],
            "total": (
                learning["json_files_count"]
                + routing_hints["json_files_count"]
                + improvements["json_files_count"]
            ),
        },
        "latest_items_preview": {
            "learning": learning["latest_items_preview"],
            "routing_hints": routing_hints["latest_items_preview"],
            "improvements": improvements["latest_items_preview"],
        },
        "paths": {
            ".hermes": {
                "path": _relative_path(HERMES_DIR),
                "exists": HERMES_DIR.exists(),
                "is_dir": HERMES_DIR.is_dir(),
            },
            "learning": {
                "path": learning["path"],
                "exists": learning["exists"],
                "is_dir": learning["is_dir"],
            },
            "routing_hints": {
                "path": routing_hints["path"],
                "exists": routing_hints["exists"],
                "is_dir": routing_hints["is_dir"],
            },
            "improvements": {
                "path": improvements["path"],
                "exists": improvements["exists"],
                "is_dir": improvements["is_dir"],
            },
        },
        "warnings": warnings,
    }


def main() -> int:
    print(json.dumps(build_learning_memory_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
