#!/usr/bin/env python3
"""
Capability Registry

Central registry of available Jarvis specialist agents and their capabilities.

Purpose:
- Hermes can check which agents exist
- Hermes can decide whether an existing agent can handle a task
- Hermes can detect capability gaps
- Missing capabilities can trigger an AgentCreationRequest

Jarvis = interface/runtime/control
Hermes = brain/decision/delegation/learning
Agents = specialists
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class AgentCapability:
    name: str
    domain: str
    module_path: str
    class_name: str
    capabilities: list[str]
    description: str
    safety_level: str = "approval_required"
    can_execute_directly: bool = False
    requires_approval_for: list[str] = field(default_factory=list)
    metadata: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


AGENT_CAPABILITIES: dict[str, AgentCapability] = {
    "memory_agent": AgentCapability(
        name="memory_agent",
        domain="memory",
        module_path="agents.memory.memory_agent",
        class_name="MemoryAgent",
        capabilities=[
            "memory_read",
            "memory_write_request",
            "preference_detection",
            "learning_capture",
            "context_recall",
            "obsidian_memory_planning",
        ],
        description="Handles memory, learnings, preferences, and long-term context.",
        requires_approval_for=[
            "memory_write",
            "memory_delete",
            "bulk_memory_update",
        ],
    ),
    "coding_agent": AgentCapability(
        name="coding_agent",
        domain="coding",
        module_path="agents.coding.coding_agent",
        class_name="CodingAgent",
        capabilities=[
            "code_planning",
            "python_planning",
            "debugging",
            "refactor_planning",
            "file_change_proposal",
            "test_plan_generation",
        ],
        description="Prepares coding plans, debugging steps, and safe implementation proposals.",
        requires_approval_for=[
            "file_write",
            "file_delete",
            "code_execution",
            "git_commit",
            "git_push",
        ],
    ),
    "research_agent": AgentCapability(
        name="research_agent",
        domain="research",
        module_path="agents.research.research_agent",
        class_name="ResearchAgent",
        capabilities=[
            "research_planning",
            "source_analysis",
            "web_research_planning",
            "reddit_research_planning",
            "summary_generation",
        ],
        description="Handles research planning, source analysis, and structured summaries.",
        requires_approval_for=[
            "internet_access",
            "external_api_call",
            "data_export",
        ],
    ),
    "trading_agent": AgentCapability(
        name="trading_agent",
        domain="trading",
        module_path="agents.trading.trading_agent",
        class_name="TradingAgent",
        capabilities=[
            "market_analysis",
            "multi_timeframe_analysis",
            "pattern_detection",
            "signal_alerting",
            "prediction_feedback_learning",
            "ctrader_integration_planned",
            "trading_briefing",
            "risk_analysis",
            "portfolio_review",
            "signal_interpretation",
        ],
        description=(
            "Hermes Trading Analyst erstellt Marktanalysen und Signale, "
            "aber fuehrt keine Orders aus."
        ),
        safety_level="analysis_only_human_review",
        can_execute_directly=False,
        requires_approval_for=[
            "broker_connection",
            "ctrader_integration",
            "order_execution",
            "live_trading",
            "paid_market_data",
        ],
        metadata={
            "status": "planned_trading_analyst_capability",
            "analysis_only": True,
            "human_review_required": True,
            "no_auto_trading": True,
            "auto_order_execution_allowed": False,
            "supported_markets": ["XAUUSD", "EURUSD", "GER40"],
            "planned_agent_name": "hermes_trading_analyst",
        },
    ),
    "office_agent": AgentCapability(
        name="office_agent",
        domain="office",
        module_path="agents.office.office_agent",
        class_name="OfficeAgent",
        capabilities=[
            "email_drafting",
            "document_planning",
            "briefing_generation",
            "task_list_generation",
            "office_workflow_planning",
        ],
        description="Handles office-style planning, documents, messages, and productivity tasks.",
        requires_approval_for=[
            "send_email",
            "file_write",
            "external_share",
        ],
    ),
    "business_agent": AgentCapability(
        name="business_agent",
        domain="business",
        module_path="agents.business.business_agent",
        class_name="BusinessAgent",
        capabilities=[
            "strategy_planning",
            "process_analysis",
            "roadmap_planning",
            "decision_support",
            "business_case_planning",
        ],
        description="Handles strategy, business analysis, and roadmap planning.",
        requires_approval_for=[
            "business_decision",
            "external_communication",
            "file_write",
        ],
    ),
    "improvement_agent": AgentCapability(
        name="improvement_agent",
        domain="improvement",
        module_path="agents.improvement.improvement_agent",
        class_name="ImprovementAgent",
        capabilities=[
            "system_improvement",
            "architecture_review",
            "workflow_optimization",
            "quality_analysis",
            "voice_system_planning",
            "ui_improvement_planning",
        ],
        description="Handles system improvement, architecture, UI, voice, and optimization planning.",
        requires_approval_for=[
            "system_change",
            "file_write",
            "service_change",
            "git_commit",
            "git_push",
        ],
    ),
}


def list_capabilities() -> dict[str, Any]:
    return {
        "timestamp": utc_now(),
        "agents": {
            name: capability.to_dict()
            for name, capability in AGENT_CAPABILITIES.items()
        },
    }


def find_agents_for_capability(capability_query: str) -> list[dict[str, Any]]:
    query = capability_query.strip().lower()
    matches: list[dict[str, Any]] = []

    for agent in AGENT_CAPABILITIES.values():
        searchable = " ".join(
            [
                agent.name,
                agent.domain,
                agent.description,
                " ".join(agent.capabilities),
            ]
        ).lower()

        if query in searchable:
            matches.append(agent.to_dict())

    return matches


def find_best_agent_for_task(task: str) -> dict[str, Any]:
    task_lower = task.strip().lower()

    scored: list[tuple[int, AgentCapability, list[str]]] = []

    for agent in AGENT_CAPABILITIES.values():
        score = 0
        matched: list[str] = []

        for capability in agent.capabilities:
            parts = capability.replace("_", " ").split()

            for part in parts:
                if part and part in task_lower:
                    score += 1
                    matched.append(capability)
                    break

        if agent.domain in task_lower:
            score += 2
            matched.append(agent.domain)

        for word in agent.description.lower().split():
            clean = word.strip(".,;:-")
            if len(clean) > 4 and clean in task_lower:
                score += 1
                matched.append(clean)

        if score > 0:
            scored.append((score, agent, matched))

    if not scored:
        return {
            "found": False,
            "agent": None,
            "score": 0,
            "matched": [],
            "reason": "No matching existing agent capability found.",
        }

    scored.sort(key=lambda item: item[0], reverse=True)
    best_score, best_agent, matched = scored[0]

    return {
        "found": True,
        "agent": best_agent.to_dict(),
        "score": best_score,
        "matched": sorted(set(matched)),
        "reason": "Existing agent capability matched.",
    }


def detect_capability_gap(task: str) -> dict[str, Any]:
    best = find_best_agent_for_task(task)

    if best["found"]:
        return {
            "gap_detected": False,
            "best_match": best,
            "recommended_action": "use_existing_agent",
        }

    return {
        "gap_detected": True,
        "best_match": best,
        "recommended_action": "create_agent_request",
    }


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Jarvis Capability Registry")
    parser.add_argument("task", nargs="*", help="Task or capability query")
    parser.add_argument("--list", action="store_true", help="List all capabilities")
    args = parser.parse_args()

    if args.list:
        print(json.dumps(list_capabilities(), indent=2, ensure_ascii=False))
        return 0

    task = " ".join(args.task).strip()

    if not task:
        print(json.dumps(list_capabilities(), indent=2, ensure_ascii=False))
        return 0

    result = detect_capability_gap(task)
    print(json.dumps(result, indent=2, ensure_ascii=False))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
