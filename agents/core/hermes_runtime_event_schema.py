#!/usr/bin/env python3
"""
Hermes Runtime Event Schema Constants

Passive schema metadata for future Runtime Event, UI, Timeline, WebSocket, and
persistence integrations. This module does not validate runtime events, start
services, open network connections, write files, or change existing publishers.
"""

from __future__ import annotations

from typing import Final


VALID_SEVERITY_LEVELS: Final[tuple[str, ...]] = (
    "info",
    "success",
    "warning",
    "critical",
)

VALID_EVENT_CATEGORIES: Final[tuple[str, ...]] = (
    "routing",
    "agent",
    "runtime",
    "learning",
    "voice",
    "trading",
    "warning",
    "system",
    "task",
    "memory",
    "research",
    "skill",
    "approval",
    "tool",
)

DEFAULT_EVENT_SCHEMA: Final[dict[str, object]] = {
    "schema_version": "hermes.runtime_event.v1",
    "required_keys": (
        "event_id",
        "timestamp",
        "source",
        "category",
        "severity",
        "message",
        "metadata",
        "requires_attention",
    ),
    "optional_keys": (
        "schema_version",
        "event_type",
        "task_id",
        "correlation_id",
        "parent_event_id",
        "agent_id",
        "session_id",
        "user_visible",
        "audit_required",
        "redaction_applied",
    ),
    "severity_levels": VALID_SEVERITY_LEVELS,
    "event_categories": VALID_EVENT_CATEGORIES,
    "timestamp_format": "UTC ISO-8601 with timezone offset, for example 2026-05-18T12:00:00+00:00",
    "source_identifier_policy": "lowercase snake_case technical identifier; no paths, secrets, or local machine names",
    "notes": (
        "Schema metadata only.",
        "No runtime validation is implemented here.",
        "Existing publishers are not modified by this helper.",
        "Events must remain JSON-compatible for future WebSocket and persistence layers.",
    ),
}
