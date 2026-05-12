#!/usr/bin/env python3
"""
Hermes Runtime Events

Defines a compact runtime event structure for future Jarvis live UI panels.
This module does not start loops, threads, services, WebSockets, or write
runtime files.
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


RUNTIME_EVENT_CATEGORIES = [
    "routing",
    "agent",
    "runtime",
    "learning",
    "voice",
    "trading",
    "warning",
    "system",
]

RUNTIME_EVENT_SEVERITIES = [
    "info",
    "success",
    "warning",
    "critical",
]


@dataclass(frozen=True)
class RuntimeEvent:
    event_id: str
    timestamp: str
    source: str
    category: str
    severity: str
    message: str
    metadata: dict[str, Any] = field(default_factory=dict)
    requires_attention: bool = False


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _validate_runtime_event_value(
    field_name: str,
    value: str,
    allowed_values: list[str],
) -> None:
    if value not in allowed_values:
        allowed = ", ".join(allowed_values)
        raise ValueError(f"Invalid {field_name}: {value!r}. Allowed values: {allowed}")


def create_runtime_event(
    source: str,
    category: str,
    severity: str,
    message: str,
    metadata: dict[str, Any] | None = None,
    requires_attention: bool | None = None,
) -> RuntimeEvent:
    _validate_runtime_event_value(
        "category",
        category,
        RUNTIME_EVENT_CATEGORIES,
    )
    _validate_runtime_event_value(
        "severity",
        severity,
        RUNTIME_EVENT_SEVERITIES,
    )

    attention_required = (
        severity in {"warning", "critical"}
        if requires_attention is None
        else requires_attention
    )

    return RuntimeEvent(
        event_id=f"evt_{uuid4().hex}",
        timestamp=utc_now(),
        source=source,
        category=category,
        severity=severity,
        message=message,
        metadata=metadata.copy() if metadata else {},
        requires_attention=attention_required,
    )


def serialize_runtime_event(event: RuntimeEvent) -> dict[str, Any]:
    return asdict(event)


def example_runtime_events() -> list[RuntimeEvent]:
    return [
        create_runtime_event(
            source="hermes_router",
            category="routing",
            severity="success",
            message="Adaptive routing used.",
            metadata={
                "adaptive_routing": {
                    "used": True,
                    "source": "routing_hints",
                    "fallback_active": False,
                },
            },
        ),
        create_runtime_event(
            source="hermes_trading_analyst",
            category="trading",
            severity="info",
            message="Trading analysis requested.",
            metadata={
                "symbol": "XAUUSD",
                "timeframe": "M15",
                "analysis_only": True,
                "no_auto_trading": True,
            },
        ),
        create_runtime_event(
            source="ollama",
            category="runtime",
            severity="success",
            message="Ollama available.",
            metadata={
                "provider": "ollama",
                "status": "available",
            },
        ),
        create_runtime_event(
            source="jarvis_voice",
            category="voice",
            severity="info",
            message="Voice runtime planned.",
            metadata={
                "wake_word": "planned",
                "microphone_accessed": False,
                "service_started": False,
            },
        ),
        create_runtime_event(
            source="hermes_learning_memory",
            category="learning",
            severity="success",
            message="Learning memory loaded.",
            metadata={
                "memory_available": True,
                "read_only": True,
            },
        ),
    ]


def main() -> int:
    events = [
        serialize_runtime_event(event)
        for event in example_runtime_events()
    ]
    print(json.dumps(events, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
