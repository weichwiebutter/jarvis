#!/usr/bin/env python3
"""
Hermes Agent Dashboard

Builds a UI-friendly, read-only overview of known and planned Jarvis/Hermes
agents from the static capability registry.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


from agents.core.capability_registry import AGENT_CAPABILITIES, AgentCapability, utc_now


def _status_for_agent(agent: AgentCapability) -> str:
    metadata = agent.metadata or {}
    raw_status = str(metadata.get("status", "")).lower()

    if metadata.get("disabled") is True or raw_status == "disabled":
        return "disabled"

    if "planned" in raw_status:
        return "planned"

    return "available"


def _safety_flags_for_agent(agent: AgentCapability) -> dict[str, Any]:
    metadata = agent.metadata or {}
    requires_approval_for = list(agent.requires_approval_for)
    human_review_required = bool(
        metadata.get("human_review_required")
        or requires_approval_for
        or agent.safety_level != "none"
    )

    return {
        "safety_level": agent.safety_level,
        "analysis_only": bool(metadata.get("analysis_only", False)),
        "no_auto_trading": bool(metadata.get("no_auto_trading", False)),
        "human_review_required": human_review_required,
        "requires_approval_for": requires_approval_for,
    }


def _dashboard_agent(agent_id: str, agent: AgentCapability) -> dict[str, Any]:
    safety_flags = _safety_flags_for_agent(agent)
    status = _status_for_agent(agent)
    can_execute = bool(agent.can_execute_directly and status == "available")

    return {
        "agent_id": agent_id,
        "name": agent.name,
        "domain": agent.domain,
        "status": status,
        "capabilities": list(agent.capabilities),
        "safety_flags": safety_flags,
        "description": agent.description,
        "can_execute": can_execute,
        "requires_approval": bool(safety_flags["human_review_required"]),
    }


def build_agent_dashboard_status() -> dict[str, Any]:
    """
    Return a stable, UI-friendly agent dashboard status.

    This function only reads the static capability registry. It does not load,
    instantiate, or execute any agent.
    """

    agents = [
        _dashboard_agent(agent_id, agent)
        for agent_id, agent in sorted(AGENT_CAPABILITIES.items())
    ]

    return {
        "generated_at": utc_now(),
        "agents": agents,
    }


def main() -> int:
    print(json.dumps(build_agent_dashboard_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
