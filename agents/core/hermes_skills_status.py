#!/usr/bin/env python3
"""
Hermes Skills Status Foundation

Builds a read-only planning/status object for the future Hermes skills system.
This module does not execute skills, clone external repositories, generate
skills automatically, start services, or write runtime files.
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


SKILL_ROOT_CANDIDATES = [
    ".hermes/skills/",
    "skills/",
    "docs/skills/",
    "memory_shared/shared_skills/",
]


PLANNED_SKILL_CATEGORIES = [
    "architecture",
    "debugging",
    "trading",
    "runtime",
    "ui",
    "codex_workflows",
    "deployment",
    "research",
]


SKILL_REVIEW_STATES = [
    "proposed",
    "reviewed_by_frank",
    "approved",
    "active",
    "deprecated",
]


EXTERNAL_PATTERN_SOURCES = [
    "https://github.com/wondelai/skills",
    "Apify Actors",
    "MCP Tools",
    "local CLI tools",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _skill_root_candidate(path: str) -> dict[str, Any]:
    return {
        "path": path,
        "status": "candidate",
        "read_only_status_only": True,
        "auto_create_enabled": False,
        "auto_write_enabled": False,
    }


def _external_pattern_source(source: str) -> dict[str, Any]:
    return {
        "source": source,
        "usage": "pattern_inspiration_only",
        "clone_allowed": False,
        "auto_import_allowed": False,
        "auto_activation_allowed": False,
        "license_review_required": True,
        "human_review_required": True,
    }


def build_skills_status() -> dict[str, Any]:
    """
    Return the planned Hermes skills-system status.

    The returned data is static architecture metadata for future Control Center
    and Masterplan usage. It performs no skill execution, no repository clone,
    no automatic generation, no service start, and no write operation.
    """

    return {
        "generated_at": utc_now(),
        "status": "planned/foundation",
        "read_only": True,
        "foundation_only": True,
        "skills_executed": False,
        "external_repos_cloned": False,
        "skills_generated": False,
        "runtime_files_written": False,
        "services_started": False,
        "skill_root_candidates": [
            _skill_root_candidate(path) for path in SKILL_ROOT_CANDIDATES
        ],
        "planned_skill_categories": PLANNED_SKILL_CATEGORIES.copy(),
        "skill_registry": {
            "status": "planned",
            "versioning_required": True,
            "metadata_required": True,
            "owner_required": True,
            "safety_flags_required": True,
            "auto_discovery_enabled": False,
            "auto_activation_enabled": False,
        },
        "skill_review_workflow": {
            "status": "planned",
            "states": SKILL_REVIEW_STATES.copy(),
            "human_review_required": True,
            "activation_requires_approval": True,
            "deprecated_skills_must_remain_auditable": True,
        },
        "skill_safety": {
            "generated_skills_not_auto_active": True,
            "human_review_required": True,
            "no_unreviewed_execution": True,
            "read_only_first": True,
            "external_sources_pattern_only": True,
            "no_secret_capture": True,
        },
        "external_pattern_sources": [
            _external_pattern_source(source) for source in EXTERNAL_PATTERN_SOURCES
        ],
        "warnings": [
            "foundation_only_no_skill_execution",
            "foundation_only_no_external_repo_clone",
            "foundation_only_no_auto_skill_generation",
            "foundation_only_no_runtime_file_writes",
            "foundation_only_no_services_started",
        ],
    }


def main() -> int:
    print(json.dumps(build_skills_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
