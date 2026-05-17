#!/usr/bin/env python3
"""
Hermes Cost Optimization Status Foundation

Builds a read-only planning/status object for future Hermes/Jarvis cost and
token optimization. This module does not call OpenRouter, Codex, OpenAI,
Ollama, or other providers, does not invoke models, does not read secrets,
start services, or write runtime files.
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


CODEX_USAGE_STRATEGY = [
    {
        "rule": "chatgpt_codex_primary",
        "description": "ChatGPT-Codex primaer fuer beaufsichtigte Codearbeit.",
    },
    {
        "rule": "fast_mode_default_off",
        "description": "Fast Mode standardmaessig aus.",
    },
    {
        "rule": "fast_mode_large_refactors_only",
        "description": "Fast Mode nur fuer grosse Refactors.",
    },
    {
        "rule": "openrouter_fallback_at_limits",
        "description": "OpenRouter als Fallback bei Limits.",
    },
    {
        "rule": "ollama_local_small_tasks",
        "description": "Ollama/local fuer kleine Aufgaben.",
    },
    {
        "rule": "small_docs_planning_without_fast_mode",
        "description": "Kleine Doku- und Planungstasks ohne Fast Mode.",
    },
]


FAST_MODE_ALLOWED_FOR = [
    "large_refactors",
    "multi_file_architecture_changes",
    "urgent_complex_debugging",
]


FAST_MODE_AVOID_FOR = [
    "docs",
    "roadmap",
    "small modules",
    "status foundations",
]


PROVIDER_PRIORITY = [
    "local_ollama_first",
    "chatgpt_codex_for_complex_code",
    "openrouter_fallback_for_limited_sessions",
    "manual_review_for_costly_tasks",
]


MONITORED_RESOURCES = [
    "ChatGPT Codex weekly credits",
    "Codex daily limits",
    "OpenRouter credits",
    "Ollama availability",
    "local GPU/CPU status later",
]


FUTURE_DASHBOARDS = [
    "Codex usage panel",
    "OpenRouter credit panel",
    "model routing history",
    "provider cost summary",
    "local/cloud ratio",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _monitored_resource(name: str) -> dict[str, Any]:
    return {
        "name": name,
        "status": "planned",
        "live_check_enabled": False,
        "api_calls_enabled": False,
        "secrets_required_for_status": False,
        "read_only": True,
    }


def _future_dashboard(name: str) -> dict[str, Any]:
    return {
        "name": name,
        "status": "planned",
        "read_only": True,
        "requires_runtime_writes": False,
        "requires_secret_access": False,
    }


def build_cost_optimization_status() -> dict[str, Any]:
    """
    Return the planned Hermes/Jarvis cost optimization status.

    The returned data is static architecture metadata for future Control Center
    and Masterplan usage. It performs no provider calls, no model calls, no
    secret reads, no service start, and no write operation.
    """

    return {
        "generated_at": utc_now(),
        "status": "planned/foundation",
        "read_only": True,
        "foundation_only": True,
        "api_calls_performed": False,
        "openrouter_queries_performed": False,
        "codex_queries_performed": False,
        "model_calls_performed": False,
        "secrets_read": False,
        "runtime_files_written": False,
        "services_started": False,
        "codex_usage_strategy": CODEX_USAGE_STRATEGY.copy(),
        "fast_mode_policy": {
            "default": "off",
            "allowed_for": FAST_MODE_ALLOWED_FOR.copy(),
            "avoid_for": FAST_MODE_AVOID_FOR.copy(),
            "requires_human_review": True,
        },
        "provider_priority": PROVIDER_PRIORITY.copy(),
        "cost_controls": {
            "credit_monitoring_required": True,
            "no_hidden_cloud_calls": True,
            "provider_logged": True,
            "model_logged": True,
            "estimated_cost_later": True,
            "human_review_for_expensive_tasks": True,
        },
        "monitored_resources": [
            _monitored_resource(name) for name in MONITORED_RESOURCES
        ],
        "future_dashboards": [_future_dashboard(name) for name in FUTURE_DASHBOARDS],
        "warnings": [
            "foundation_only_no_api_calls",
            "foundation_only_no_openrouter_queries",
            "foundation_only_no_codex_queries",
            "foundation_only_no_model_calls",
            "foundation_only_no_secret_reads",
            "foundation_only_no_runtime_file_writes",
            "foundation_only_no_services_started",
        ],
    }


def main() -> int:
    print(json.dumps(build_cost_optimization_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
