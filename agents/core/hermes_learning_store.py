#!/usr/bin/env python3
"""
Hermes Learning Store

Persists Hermes learning feedback into local .hermes storage only after
explicit approval. Stored payloads are JSON-compatible and sanitized for
secret-like keys.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.hermes_execution_engine import execute_objective
from agents.core.hermes_learning_feedback import build_learning_feedback


HERMES_DIR = PROJECT_ROOT / ".hermes"
LEARNING_DIR = HERMES_DIR / "learning"
ROUTING_HINTS_DIR = HERMES_DIR / "routing_hints"
IMPROVEMENTS_DIR = HERMES_DIR / "improvements"

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


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _timestamp_for_filename() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def _slugify(value: str, fallback: str = "learning") -> str:
    slug = value.strip().lower()
    slug = slug.replace("ä", "ae").replace("ö", "oe").replace("ü", "ue").replace("ß", "ss")
    slug = re.sub(r"[^a-z0-9]+", "-", slug)
    slug = slug.strip("-")

    if not slug:
        slug = fallback

    return slug[:48].strip("-") or fallback


def _contains_secret_key(key: Any) -> bool:
    key_text = str(key).strip().lower()
    return any(part in key_text for part in SECRET_KEY_PARTS)


def _sanitize_for_storage(value: Any) -> Any:
    if isinstance(value, dict):
        sanitized: dict[str, Any] = {}

        for key, item in value.items():
            if _contains_secret_key(key):
                sanitized[str(key)] = "[REDACTED]"
            else:
                sanitized[str(key)] = _sanitize_for_storage(item)

        return sanitized

    if isinstance(value, list):
        return [_sanitize_for_storage(item) for item in value]

    return value


def _write_json(path: Path, payload: Any) -> None:
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False, default=str),
        encoding="utf-8",
    )


def _extract_learning_feedback(feedback: dict[str, Any]) -> dict[str, Any]:
    value = feedback.get("learning_feedback", {})
    return value if isinstance(value, dict) else {}


def _extract_routing_hints(feedback: dict[str, Any]) -> list[Any]:
    hints = feedback.get("routing_hints")

    if isinstance(hints, list):
        return hints

    learning_feedback = _extract_learning_feedback(feedback)
    hints = learning_feedback.get("future_routing_hints", [])

    return hints if isinstance(hints, list) else []


def _extract_recommended_improvements(feedback: dict[str, Any]) -> list[Any]:
    learning_feedback = _extract_learning_feedback(feedback)
    improvements = learning_feedback.get("recommended_improvements", [])

    return improvements if isinstance(improvements, list) else []


def _planned_paths(feedback: dict[str, Any], timestamp: str | None = None) -> dict[str, Path]:
    objective = str(feedback.get("objective", "")).strip()
    filename_timestamp = timestamp or _timestamp_for_filename()
    slug = _slugify(objective)
    prefix = f"{filename_timestamp}-{slug}"

    return {
        "learning_feedback": LEARNING_DIR / f"{prefix}-feedback.json",
        "routing_hints": ROUTING_HINTS_DIR / f"{prefix}-routing-hints.json",
        "recommended_improvements": IMPROVEMENTS_DIR / f"{prefix}-improvements.json",
    }


def save_learning_feedback(feedback: dict, approve: bool = False) -> dict:
    feedback = deepcopy(feedback) if isinstance(feedback, dict) else {}
    timestamp = utc_now()
    filename_timestamp = _timestamp_for_filename()
    paths = _planned_paths(feedback, timestamp=filename_timestamp)
    routing_hints = _extract_routing_hints(feedback)
    improvements = _extract_recommended_improvements(feedback)

    if not approve:
        return {
            "ok": True,
            "status": "approval_required",
            "saved": False,
            "objective": feedback.get("objective", ""),
            "reason": "Learning feedback persistence requires explicit approval.",
            "planned_files": {
                name: str(path.relative_to(PROJECT_ROOT))
                for name, path in paths.items()
            },
            "metadata": {
                "source": "hermes_learning_store",
                "storage_root": str(HERMES_DIR.relative_to(PROJECT_ROOT)),
                "requires_approval": True,
                "human_in_the_loop": True,
            },
            "timestamp": timestamp,
        }

    LEARNING_DIR.mkdir(parents=True, exist_ok=True)
    ROUTING_HINTS_DIR.mkdir(parents=True, exist_ok=True)
    IMPROVEMENTS_DIR.mkdir(parents=True, exist_ok=True)

    sanitized_feedback = _sanitize_for_storage(feedback)
    sanitized_routing_hints = _sanitize_for_storage(
        {
            "objective": feedback.get("objective", ""),
            "routing_hints": routing_hints,
            "source_feedback_timestamp": feedback.get("timestamp"),
        }
    )
    sanitized_improvements = _sanitize_for_storage(
        {
            "objective": feedback.get("objective", ""),
            "recommended_improvements": improvements,
            "source_feedback_timestamp": feedback.get("timestamp"),
        }
    )

    _write_json(paths["learning_feedback"], sanitized_feedback)
    _write_json(paths["routing_hints"], sanitized_routing_hints)
    _write_json(paths["recommended_improvements"], sanitized_improvements)

    return {
        "ok": True,
        "status": "saved",
        "saved": True,
        "objective": feedback.get("objective", ""),
        "saved_files": {
            name: str(path.relative_to(PROJECT_ROOT))
            for name, path in paths.items()
        },
        "counts": {
            "routing_hints": len(routing_hints),
            "recommended_improvements": len(improvements),
            "memory_candidates": len(feedback.get("memory_candidates", []) or []),
        },
        "metadata": {
            "source": "hermes_learning_store",
            "storage_root": str(HERMES_DIR.relative_to(PROJECT_ROOT)),
            "sanitized": True,
            "requires_approval": False,
            "human_in_the_loop": True,
        },
        "timestamp": timestamp,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Hermes Learning Store")
    parser.add_argument(
        "--approve",
        action="store_true",
        help="Persist learning feedback into .hermes storage.",
    )
    parser.add_argument("objective", nargs="*", help="Objective to execute, learn from, and store")
    args = parser.parse_args()

    objective = " ".join(args.objective).strip()
    execution_result = execute_objective(objective)
    feedback = build_learning_feedback(execution_result)
    result = save_learning_feedback(feedback, approve=args.approve)

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
