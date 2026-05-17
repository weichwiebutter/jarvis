#!/usr/bin/env python3
"""
Hermes Shared Memory Status Foundation

Builds a read-only planning/status object for future multi-PC shared memory.
This module does not synchronize files, copy files, open network connections,
read secrets, write runtime files, or modify memory stores.
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


LOCAL_ONLY_PATHS = [
    {
        "path": ".hermes/runtime",
        "reason": "Runtime state stays machine-local.",
    },
    {
        "path": "runtime/",
        "reason": "Local process state and transient runtime artifacts are not shared.",
    },
    {
        "path": "logs/",
        "reason": "Logs can contain machine-specific or sensitive operational context.",
    },
    {
        "path": ".env.local",
        "reason": "Local environment files may contain secrets and must never be shared.",
    },
    {
        "path": "cache",
        "reason": "Caches are derived local artifacts and are not source of truth.",
    },
    {
        "path": "local model files",
        "reason": "Model binaries and local model caches remain local to each PC.",
    },
]


SHARED_CANDIDATE_PATHS = [
    {
        "path": "docs/",
        "purpose": "Shared architecture, roadmap, and project documentation.",
    },
    {
        "path": "obsidian/",
        "purpose": "Human-maintained knowledge notes when explicitly shared.",
    },
    {
        "path": "memory_shared/",
        "purpose": "Future store for approved reusable learnings.",
    },
    {
        "path": "approved_learnings/",
        "purpose": "Approved Hermes learnings prepared for multi-PC reuse.",
    },
    {
        "path": "shared_skills/",
        "purpose": "Reviewed shared skill playbooks.",
    },
    {
        "path": "routing_hints_approved/",
        "purpose": "Approved routing hints only, not raw runtime routing history.",
    },
    {
        "path": "trading_patterns_approved/",
        "purpose": "Approved trading pattern summaries with analysis-only safety flags.",
    },
]


APPROVAL_WORKFLOW_STEPS = [
    {
        "step": 1,
        "name": "local_learning_created",
        "description": "Local learning entsteht zuerst lokal.",
    },
    {
        "step": 2,
        "name": "hermes_proposes_persistent_learning",
        "description": "Hermes schlaegt dauerhaftes Learning vor.",
    },
    {
        "step": 3,
        "name": "frank_confirms",
        "description": "Frank bestaetigt die dauerhafte Uebernahme.",
    },
    {
        "step": 4,
        "name": "promote_to_shared_memory",
        "description": "Erst danach in Shared Memory uebernehmen.",
    },
    {
        "step": 5,
        "name": "secondary_pc_syncs_approved_memory",
        "description": "Zweiter PC synchronisiert nur approved memory.",
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _local_only_path(entry: dict[str, str]) -> dict[str, Any]:
    return {
        "path": entry["path"],
        "classification": "local_only",
        "sync_allowed": False,
        "copy_allowed": False,
        "read_only_status_only": True,
        "reason": entry["reason"],
    }


def _shared_candidate_path(entry: dict[str, str]) -> dict[str, Any]:
    return {
        "path": entry["path"],
        "classification": "shared_candidate",
        "sync_allowed": "after_approval_only",
        "auto_sync_enabled": False,
        "requires_human_review": True,
        "purpose": entry["purpose"],
    }


def build_shared_memory_status() -> dict[str, Any]:
    """
    Return the planned Hermes/Jarvis shared-memory status.

    The returned data is static architecture metadata for future Control Center
    and Masterplan usage. It performs no sync, no network access, no file copy,
    no secret read, and no write operation.
    """

    return {
        "generated_at": utc_now(),
        "status": "planned/foundation",
        "read_only": True,
        "foundation_only": True,
        "sync_actions_performed": False,
        "network_connections_opened": False,
        "files_copied": False,
        "runtime_files_written": False,
        "secrets_read": False,
        "sync_strategy": {
            "status": "planned",
            "mode": "approval_based_shared_memory",
            "auto_sync_enabled": False,
            "network_enabled": False,
            "write_enabled": False,
            "shared_runtime_sync_allowed": False,
            "approved_memory_only": True,
            "notes": [
                "Code and documentation may be shared through reviewed Git workflows.",
                "Runtime, logs, cache, secrets, and local model files stay machine-local.",
                "Shared memory accepts only approved durable learnings.",
            ],
        },
        "local_only_paths": [_local_only_path(entry) for entry in LOCAL_ONLY_PATHS],
        "shared_candidate_paths": [
            _shared_candidate_path(entry) for entry in SHARED_CANDIDATE_PATHS
        ],
        "approval_workflow": {
            "status": "planned",
            "human_review_required": True,
            "approver": "Frank",
            "auto_promotion_enabled": False,
            "steps": APPROVAL_WORKFLOW_STEPS.copy(),
        },
        "multi_pc_roles": {
            "primary_pc": {
                "role": "source_of_local_learning_candidates",
                "can_propose_shared_memory": True,
                "can_auto_publish": False,
            },
            "secondary_pc": {
                "role": "consumer_of_approved_shared_memory",
                "syncs_only_approved_memory": True,
                "can_import_runtime_state": False,
            },
            "offline_mode": {
                "status": "planned",
                "uses_local_memory_only": True,
                "queues_sync_proposals": False,
            },
            "conflict_handling": "manual_review_required",
        },
        "warnings": [
            "foundation_only_no_real_sync",
            "foundation_only_no_file_copy",
            "foundation_only_no_network_connections",
            "foundation_only_no_runtime_file_writes",
            "foundation_only_no_secret_reads",
        ],
    }


def main() -> int:
    print(json.dumps(build_shared_memory_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
