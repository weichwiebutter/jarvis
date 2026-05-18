#!/usr/bin/env python3
"""
Hermes Foundation Registry

Builds a read-only registry for Hermes/Jarvis foundation status modules.
This module does not import or execute foundation builders, start services,
open network connections, run schedulers, or write runtime files.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any


FOUNDATION_MODULES: list[dict[str, Any]] = [
    {
        "key": "runtime_supervisor",
        "display_name": "Runtime Supervisor",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_runtime_supervisor",
        "ui_panel_name": "runtime_supervisor_panel",
        "safety_level": "guarded_future_runtime",
        "planned_capabilities": [
            {
                "key": "heartbeat",
                "label": "Heartbeat",
                "status": "planned",
                "read_only": True,
                "requires_review": False,
            },
            {
                "key": "scheduler",
                "label": "Scheduler / Agent Jobs",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "zombie_protection",
                "label": "Zombie Protection",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "resource_limits",
                "label": "Resource Limits",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "shared_memory",
        "display_name": "Shared Memory / Multi-PC",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_shared_memory_status",
        "ui_panel_name": "shared_memory_panel",
        "safety_level": "read_only_foundation",
        "planned_capabilities": [
            {
                "key": "sync_strategy",
                "label": "Approved Memory Sync Strategy",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "local_only_paths",
                "label": "Local-only Path Guardrails",
                "status": "planned",
                "read_only": True,
                "requires_review": False,
            },
            {
                "key": "approval_workflow",
                "label": "Learning Approval Workflow",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "skills",
        "display_name": "Skills System",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_skills_status",
        "ui_panel_name": "skills_panel",
        "safety_level": "read_only_foundation",
        "planned_capabilities": [
            {
                "key": "skill_registry",
                "label": "Skill Registry",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "skill_review_workflow",
                "label": "Skill Review Workflow",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "skill_categories",
                "label": "Planned Skill Categories",
                "status": "planned",
                "read_only": True,
                "requires_review": False,
            },
        ],
    },
    {
        "key": "skill_generator",
        "display_name": "Skill Generator",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_skill_generator_status",
        "ui_panel_name": "skill_generator_panel",
        "safety_level": "guarded_future_runtime",
        "planned_capabilities": [
            {
                "key": "source_specs",
                "label": "Tool/API Spec Intake",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "draft_skill_docs",
                "label": "Draft Skill Documentation",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "safety_flags",
                "label": "Generated Safety Flags",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "research_discovery",
        "display_name": "Research Discovery",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_research_discovery_status",
        "ui_panel_name": "research_discovery_panel",
        "safety_level": "read_only_foundation",
        "planned_capabilities": [
            {
                "key": "source_monitoring",
                "label": "Read-only Source Monitoring",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "idea_extraction",
                "label": "Idea Extraction",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "weekly_reports",
                "label": "Curated Weekly Reports",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "cost_optimization",
        "display_name": "Cost / Token Optimization",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_cost_optimization_status",
        "ui_panel_name": "cost_optimization_panel",
        "safety_level": "read_only_foundation",
        "planned_capabilities": [
            {
                "key": "provider_strategy",
                "label": "Provider Strategy",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "fast_mode_policy",
                "label": "Fast Mode Policy",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "cost_dashboards",
                "label": "Cost Dashboards",
                "status": "planned",
                "read_only": True,
                "requires_review": False,
            },
        ],
    },
    {
        "key": "mcp_tools",
        "display_name": "MCP / Tools",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_mcp_tool_status",
        "ui_panel_name": "mcp_tools_panel",
        "safety_level": "guarded_future_runtime",
        "planned_capabilities": [
            {
                "key": "tool_registry",
                "label": "Tool Registry",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "permission_model",
                "label": "Permission Model",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "mcp_gateway",
                "label": "MCP Gateway",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "reflective_learning",
        "display_name": "Reflective Learning",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_reflective_learning_status",
        "ui_panel_name": "reflective_learning_panel",
        "safety_level": "guarded_future_runtime",
        "planned_capabilities": [
            {
                "key": "post_task_review",
                "label": "Post-task Review",
                "status": "planned",
                "read_only": True,
                "requires_review": False,
            },
            {
                "key": "candidate_generation",
                "label": "Learning/Skill Candidate Generation",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "approval_queue",
                "label": "Approval Queue Integration",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
        ],
    },
    {
        "key": "trading_intelligence",
        "display_name": "Trading Intelligence",
        "status": "planned/foundation",
        "source_module": "agents.core.hermes_trading_intelligence_status",
        "ui_panel_name": "trading_intelligence_panel",
        "safety_level": "high_risk_disabled",
        "planned_capabilities": [
            {
                "key": "quote_pipeline",
                "label": "cTrader QUOTE Pipeline",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "prediction_learning",
                "label": "Prediction Feedback Learning",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "feature_engine",
                "label": "Trading Feature Engine",
                "status": "planned",
                "read_only": True,
                "requires_review": True,
            },
            {
                "key": "no_auto_trading",
                "label": "No Auto Trading Guardrail",
                "status": "active_policy",
                "read_only": True,
                "requires_review": False,
            },
        ],
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _registry_entry(module: dict[str, Any]) -> dict[str, Any]:
    return {
        "key": module["key"],
        "display_name": module["display_name"],
        "status": module["status"],
        "source_module": module["source_module"],
        "ui_panel_name": module["ui_panel_name"],
        "safety_level": module["safety_level"],
        "planned_capabilities": [
            capability.copy() for capability in module["planned_capabilities"]
        ],
    }


def build_foundation_registry() -> dict[str, Any]:
    """
    Return the central Hermes/Jarvis foundation module registry.

    The returned data is static metadata for future UI, API, and Control Center
    rendering. It performs no imports of the registered modules, no external
    calls, no service starts, no scheduler activity, and no runtime writes.
    """

    modules = [_registry_entry(module) for module in FOUNDATION_MODULES]

    return {
        "generated_at": utc_now(),
        "status": "planned/foundation",
        "read_only": True,
        "foundation_only": True,
        "module_count": len(modules),
        "modules": modules,
        "index": {module["key"]: module["ui_panel_name"] for module in modules},
        "safety_levels": sorted({module["safety_level"] for module in modules}),
        "external_access_performed": False,
        "services_started": False,
        "runtime_loops_started": False,
        "runtime_files_written": False,
        "warnings": [
            "foundation_registry_static_metadata_only",
            "foundation_registry_does_not_import_status_builders",
            "foundation_registry_no_external_access",
            "foundation_registry_no_runtime_writes",
            "foundation_registry_no_services_started",
        ],
    }


def main() -> int:
    print(json.dumps(build_foundation_registry(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
