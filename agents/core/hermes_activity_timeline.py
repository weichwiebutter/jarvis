#!/usr/bin/env python3
"""
Hermes Activity Timeline

Defines a compact read-only activity timeline foundation for future Jarvis live
activity panels. This module does not start loops, threads, services,
WebSockets, or write runtime files.
"""

from __future__ import annotations

import json
import sys
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from uuid import uuid4


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


ACTIVITY_TIMELINE_CATEGORIES = [
    "routing",
    "agent",
    "learning",
    "trading",
    "runtime",
    "voice",
    "system",
]

ACTIVITY_TIMELINE_STATUSES = [
    "planned",
    "active",
    "completed",
    "warning",
]

ACTIVITY_TIMELINE_IMPORTANCE_LEVELS = [
    "low",
    "normal",
    "high",
]


@dataclass(frozen=True)
class ActivityTimelineEntry:
    entry_id: str
    timestamp: str
    title: str
    description: str
    category: str
    source: str
    status: str
    importance: str
    metadata: dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _validate_allowed_value(
    field_name: str,
    value: str,
    allowed_values: list[str],
) -> None:
    if value not in allowed_values:
        allowed = ", ".join(allowed_values)
        raise ValueError(f"Invalid {field_name}: {value!r}. Allowed values: {allowed}")


def _validate_required_text(field_name: str, value: str) -> None:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be a non-empty string.")


def create_timeline_entry(
    title: str,
    description: str,
    category: str,
    source: str,
    status: str,
    importance: str = "normal",
    metadata: dict[str, Any] | None = None,
) -> ActivityTimelineEntry:
    _validate_required_text("title", title)
    _validate_required_text("description", description)
    _validate_required_text("source", source)
    _validate_allowed_value("category", category, ACTIVITY_TIMELINE_CATEGORIES)
    _validate_allowed_value("status", status, ACTIVITY_TIMELINE_STATUSES)
    _validate_allowed_value("importance", importance, ACTIVITY_TIMELINE_IMPORTANCE_LEVELS)

    if metadata is not None and not isinstance(metadata, dict):
        raise ValueError("metadata must be a dict when provided.")

    return ActivityTimelineEntry(
        entry_id=f"tl_{uuid4().hex}",
        timestamp=utc_now(),
        title=title.strip(),
        description=description.strip(),
        category=category,
        source=source.strip(),
        status=status,
        importance=importance,
        metadata=metadata.copy() if metadata else {},
    )


def serialize_timeline_entry(entry: ActivityTimelineEntry) -> dict[str, Any]:
    return asdict(entry)


def build_demo_activity_timeline() -> list[ActivityTimelineEntry]:
    return [
        create_timeline_entry(
            title="Hermes adaptive routing active",
            description="Adaptive routing foundation is available for routing decisions.",
            category="routing",
            source="hermes_router",
            status="active",
            importance="high",
            metadata={
                "adaptive_routing": {
                    "used": True,
                    "fallback_active": False,
                },
                "read_only": True,
            },
        ),
        create_timeline_entry(
            title="Trading analysis requested for XAUUSD",
            description="Trading request is represented as analysis-only UI context.",
            category="trading",
            source="hermes_trading_analyst",
            status="active",
            importance="high",
            metadata={
                "symbol": "XAUUSD",
                "analysis_only": True,
                "no_auto_trading": True,
                "human_review_required": True,
            },
        ),
        create_timeline_entry(
            title="Ollama runtime available",
            description="Local model runtime is represented as available demo state.",
            category="runtime",
            source="ollama",
            status="completed",
            importance="normal",
            metadata={
                "provider": "ollama",
                "local": True,
            },
        ),
        create_timeline_entry(
            title="Voice runtime planned",
            description="Voice runtime is planned and not started by the timeline module.",
            category="voice",
            source="jarvis_voice",
            status="planned",
            importance="normal",
            metadata={
                "wake_word": "planned",
                "microphone_accessed": False,
                "service_started": False,
            },
        ),
        create_timeline_entry(
            title="Learning memory loaded",
            description="Learning and memory status can be displayed as read-only timeline context.",
            category="learning",
            source="hermes_learning_memory",
            status="completed",
            importance="normal",
            metadata={
                "memory_available": True,
                "read_only": True,
            },
        ),
        create_timeline_entry(
            title="Agent dashboard initialized",
            description="Known and planned agents can be represented in the activity timeline.",
            category="agent",
            source="hermes_agent_dashboard",
            status="completed",
            importance="normal",
            metadata={
                "planned_agents_visible": True,
                "can_execute": False,
            },
        ),
    ]


def _fallback_warning_entry(error: Exception) -> dict[str, Any]:
    return {
        "entry_id": f"tl_{uuid4().hex}",
        "timestamp": utc_now(),
        "title": "Activity timeline unavailable",
        "description": "Demo activity timeline generation failed.",
        "category": "system",
        "source": "hermes_activity_timeline",
        "status": "warning",
        "importance": "high",
        "metadata": {
            "error": str(error),
            "read_only": True,
        },
    }


def main() -> int:
    try:
        timeline = [
            serialize_timeline_entry(entry)
            for entry in build_demo_activity_timeline()
        ]
    except Exception as exc:
        timeline = [_fallback_warning_entry(exc)]

    print(json.dumps(timeline, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
