#!/usr/bin/env python3
"""
Hermes Runtime Event Bus Foundation

Provides a small in-memory event bus for future Hermes/Jarvis runtime event
flows. This module does not start background loops, create threads, open
WebSockets, start services, write runtime files, or persist events.
"""

from __future__ import annotations

import importlib
import json
import sys
from copy import deepcopy
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable
from uuid import uuid4


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


class RuntimeEventBus:
    """In-memory runtime event bus foundation."""

    def __init__(self, initial_events: list[dict[str, Any]] | None = None) -> None:
        self._events: list[dict[str, Any]] = []
        for event in initial_events or []:
            self.publish(event)

    def publish(self, event: dict[str, Any]) -> dict[str, Any]:
        if not isinstance(event, dict):
            raise ValueError("event must be a dict.")

        stored_event = deepcopy(event)
        self._events.append(stored_event)
        return deepcopy(stored_event)

    def list_events(self) -> list[dict[str, Any]]:
        return deepcopy(self._events)

    def clear(self) -> None:
        self._events.clear()


def _load_runtime_event_helpers(
    warnings: list[str],
) -> tuple[Callable[[], list[Any]] | None, Callable[[Any], dict[str, Any]] | None]:
    module_name = "agents.core.hermes_runtime_events"

    try:
        module = importlib.import_module(module_name)
    except Exception as exc:
        warnings.append(f"{module_name} unavailable: {exc}")
        return None, None

    runtime_event_class = getattr(module, "RuntimeEvent", None)
    if runtime_event_class is None:
        warnings.append(f"{module_name}.RuntimeEvent unavailable.")

    example_builder = getattr(module, "example_runtime_events", None)
    serializer = getattr(module, "serialize_runtime_event", None)

    if not callable(example_builder):
        warnings.append(f"{module_name}.example_runtime_events is not callable.")
        example_builder = None

    if not callable(serializer):
        warnings.append(f"{module_name}.serialize_runtime_event is not callable.")
        serializer = None

    return example_builder, serializer


def _fallback_demo_events(warnings: list[str]) -> list[dict[str, Any]]:
    warnings.append("using_fallback_demo_events")
    return [
        {
            "event_id": f"evt_{uuid4().hex}",
            "timestamp": utc_now(),
            "source": "hermes_runtime_event_bus",
            "category": "runtime",
            "severity": "info",
            "message": "Runtime event bus foundation demo event.",
            "metadata": {
                "in_memory_only": True,
                "persistence_enabled": False,
                "services_started": False,
            },
            "requires_attention": False,
        }
    ]


def _build_demo_events(warnings: list[str]) -> list[dict[str, Any]]:
    example_builder, serializer = _load_runtime_event_helpers(warnings)
    if example_builder is None or serializer is None:
        return _fallback_demo_events(warnings)

    try:
        runtime_events = example_builder()
    except Exception as exc:
        warnings.append(f"example_runtime_events failed: {exc}")
        return _fallback_demo_events(warnings)

    if not isinstance(runtime_events, list):
        warnings.append("example_runtime_events returned non-list data.")
        return _fallback_demo_events(warnings)

    serialized_events: list[dict[str, Any]] = []
    for index, event in enumerate(runtime_events):
        try:
            serialized_event = serializer(event)
        except Exception as exc:
            warnings.append(f"serialize_runtime_event failed for event {index}: {exc}")
            continue

        if not isinstance(serialized_event, dict):
            warnings.append(f"serialize_runtime_event returned non-dict data for event {index}.")
            continue

        serialized_events.append(serialized_event)

    if not serialized_events:
        warnings.append("no_serialized_demo_events_available")
        return _fallback_demo_events(warnings)

    return serialized_events


def build_demo_event_bus_status() -> dict[str, Any]:
    warnings = [
        "foundation_only_in_memory_event_bus",
        "foundation_only_no_background_loops",
        "foundation_only_no_threads",
        "foundation_only_no_websockets",
        "foundation_only_no_services_started",
        "foundation_only_no_runtime_file_writes",
        "foundation_only_no_persistence",
    ]
    event_bus = RuntimeEventBus()

    for event in _build_demo_events(warnings):
        try:
            event_bus.publish(event)
        except Exception as exc:
            warnings.append(f"publish failed for demo event: {exc}")

    events = event_bus.list_events()

    return {
        "generated_at": utc_now(),
        "status": "foundation/in_memory",
        "event_count": len(events),
        "events": events,
        "warnings": warnings,
    }


def main() -> int:
    print(json.dumps(build_demo_event_bus_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
