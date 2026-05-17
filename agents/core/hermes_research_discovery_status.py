#!/usr/bin/env python3
"""
Hermes Research Discovery Status Foundation

Builds a read-only planning/status object for the future Hermes research and
discovery agent. This module does not call Reddit, GitHub, web, arXiv, or other
APIs, start schedulers, start background loops, start services, or write
runtime files.
"""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


RESEARCH_SOURCES = [
    "Reddit",
    "GitHub",
    "arXiv",
    "MCP ecosystem",
    "Hermes-agent ecosystem",
    "Ollama/OpenRouter news",
    "Trading AI / cTrader topics",
]


MONITORED_TOPICS = [
    "LangGraph",
    "CrewAI",
    "AutoGen",
    "OpenClaw",
    "SWE-Agent/OpenDevin",
    "MCP",
    "local models",
    "agent memory",
    "scheduler/runtime supervisor",
    "skill systems",
    "trading ML",
]


DISCOVERY_PIPELINE_STEPS = [
    "scan",
    "deduplicate",
    "summarize",
    "extract_ideas",
    "score_relevance",
    "propose_for_review",
    "archive_or_promote",
]


REVIEW_WORKFLOW_STATES = [
    "discovered",
    "summarized",
    "reviewed_by_frank",
    "accepted_for_masterplan",
    "rejected_or_archived",
]


PLANNED_REPORT_IDS = [
    "weekly_ai_agent_digest",
    "reddit_hermes_agent_digest",
    "github_agent_tools_watch",
    "trading_ai_watch",
    "local_models_watch",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _research_source(source: str) -> dict[str, Any]:
    return {
        "source": source,
        "status": "planned",
        "read_only": True,
        "api_calls_enabled": False,
        "network_enabled": False,
        "scheduler_enabled": False,
        "requires_human_review": True,
    }


def _pipeline_step(index: int, step: str) -> dict[str, Any]:
    return {
        "step": index,
        "name": step,
        "status": "planned",
        "automated_execution_enabled": False,
        "writes_runtime_files": False,
        "human_review_required": step in {"propose_for_review", "archive_or_promote"},
    }


def _planned_report(report_id: str) -> dict[str, Any]:
    return {
        "report_id": report_id,
        "status": "planned",
        "enabled": False,
        "generated": False,
        "schedule": "planned",
        "read_only": True,
        "requires_sources": True,
        "human_review_required": True,
    }


def build_research_discovery_status() -> dict[str, Any]:
    """
    Return the planned Hermes research/discovery status.

    The returned data is static architecture metadata for future Control Center
    and Masterplan usage. It performs no external queries, no API calls, no
    scheduler work, no background loop, no service start, and no write
    operation.
    """

    return {
        "generated_at": utc_now(),
        "status": "planned/foundation",
        "read_only": True,
        "foundation_only": True,
        "external_queries_performed": False,
        "api_calls_performed": False,
        "scheduler_started": False,
        "background_loops_started": False,
        "runtime_files_written": False,
        "services_started": False,
        "research_sources": [_research_source(source) for source in RESEARCH_SOURCES],
        "monitored_topics": MONITORED_TOPICS.copy(),
        "discovery_pipeline": {
            "status": "planned",
            "auto_run_enabled": False,
            "writes_enabled": False,
            "steps": [
                _pipeline_step(index, step)
                for index, step in enumerate(DISCOVERY_PIPELINE_STEPS, start=1)
            ],
        },
        "review_workflow": {
            "status": "planned",
            "states": REVIEW_WORKFLOW_STATES.copy(),
            "human_review_required": True,
            "review_owner": "Frank",
            "auto_promote_to_masterplan": False,
        },
        "safety_rules": {
            "read_only_research": True,
            "no_auto_code_changes": True,
            "no_auto_installations": True,
            "human_review_required": True,
            "cite_sources_required": True,
        },
        "planned_reports": [
            _planned_report(report_id) for report_id in PLANNED_REPORT_IDS
        ],
        "warnings": [
            "foundation_only_no_external_queries",
            "foundation_only_no_api_calls",
            "foundation_only_no_scheduler_started",
            "foundation_only_no_background_loops",
            "foundation_only_no_runtime_file_writes",
            "foundation_only_no_services_started",
        ],
    }


def main() -> int:
    print(json.dumps(build_research_discovery_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
