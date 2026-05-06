#!/usr/bin/env python3
"""
Hermes Contract

Standardized task package between Jarvis Core and Hermes.

Purpose:
- structured delegation
- future multi-agent orchestration
- execution governance
- approval handling
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Optional


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class HermesContract:
    source: str
    task: str

    domain: str
    intent: str

    complexity_score: int

    available_agents: list[str] = field(default_factory=list)
    allowed_tools: list[str] = field(default_factory=list)

    execution_policy: str = "human_approval_required"

    memory_enabled: bool = True
    internet_allowed: bool = False
    filesystem_allowed: bool = False

    preferred_model: Optional[str] = None

    metadata: dict = field(default_factory=dict)

    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict:
        return asdict(self)
