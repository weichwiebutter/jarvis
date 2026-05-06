#!/usr/bin/env python3
"""
Jarvis Memory Write Script

Role:
    Controlled memory writer for Jarvis.

Purpose:
    - Write structured memory entries to JSON files
    - Support preferences, profile, decisions, learnings, facts, tasks
    - Be callable only through Executor tasks later
    - Keep memory writes auditable and append-only by default

Important:
    - No LLM calls
    - No subprocess calls
    - No Git actions
    - No deletion
    - No silent overwrite
"""

from __future__ import annotations

import argparse
import json
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]

MEMORY_DIR = PROJECT_ROOT / "memory"
LOG_DIR = PROJECT_ROOT / "logs"

MEMORY_INDEX_FILE = MEMORY_DIR / "memory_index.json"
MEMORY_WRITE_LOG = LOG_DIR / "memory_write.log"


SUPPORTED_CATEGORIES = {
    "profile",
    "preferences",
    "facts",
    "learnings",
    "tasks",
    "decisions",
    "system_state",
}


CATEGORY_FILES = {
    "profile": MEMORY_DIR / "jarvis_profile.json",
    "preferences": MEMORY_DIR / "preferences.json",
    "facts": MEMORY_DIR / "facts.json",
    "learnings": MEMORY_DIR / "learnings.json",
    "tasks": MEMORY_DIR / "tasks_memory.json",
    "decisions": MEMORY_DIR / "decisions.json",
    "system_state": MEMORY_DIR / "system_state_memory.json",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def read_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        return data
    except Exception:
        return default


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False, default=str),
        encoding="utf-8",
    )


def append_log(record: Dict[str, Any]) -> None:
    ensure_dirs()

    with MEMORY_WRITE_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(record, ensure_ascii=False, default=str))
        file.write("\n")


def load_category_memory(category: str) -> Dict[str, Any]:
    path = CATEGORY_FILES[category]

    data = read_json(
        path,
        {
            "category": category,
            "created_at": utc_now(),
            "updated_at": utc_now(),
            "entries": [],
        },
    )

    if not isinstance(data, dict):
        data = {
            "category": category,
            "created_at": utc_now(),
            "updated_at": utc_now(),
            "entries": [],
        }

    if not isinstance(data.get("entries"), list):
        data["entries"] = []

    if "category" not in data:
        data["category"] = category

    if "created_at" not in data:
        data["created_at"] = utc_now()

    return data


def load_index() -> Dict[str, Any]:
    data = read_json(
        MEMORY_INDEX_FILE,
        {
            "created_at": utc_now(),
            "updated_at": utc_now(),
            "categories": {},
            "entries": [],
        },
    )

    if not isinstance(data, dict):
        data = {
            "created_at": utc_now(),
            "updated_at": utc_now(),
            "categories": {},
            "entries": [],
        }

    if not isinstance(data.get("categories"), dict):
        data["categories"] = {}

    if not isinstance(data.get("entries"), list):
        data["entries"] = []

    return data


def normalize_category(category: str) -> str:
    normalized = category.strip().lower()

    if normalized not in SUPPORTED_CATEGORIES:
        raise ValueError(
            f"Unsupported category: {category}. "
            f"Supported categories: {sorted(SUPPORTED_CATEGORIES)}"
        )

    return normalized


def build_entry(
    category: str,
    title: str,
    content: str,
    source: str,
    confidence: float,
    persistence: str,
    tags: Optional[List[str]] = None,
    metadata: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    if not title.strip():
        raise ValueError("title must not be empty.")

    if not content.strip():
        raise ValueError("content must not be empty.")

    if confidence < 0 or confidence > 1:
        raise ValueError("confidence must be between 0 and 1.")

    return {
        "id": str(uuid.uuid4()),
        "category": category,
        "title": title.strip(),
        "content": content.strip(),
        "source": source.strip() or "unknown",
        "confidence": confidence,
        "persistence": persistence.strip() or "unspecified",
        "tags": tags or [],
        "metadata": metadata or {},
        "created_at": utc_now(),
        "updated_at": utc_now(),
        "status": "active",
    }


def write_memory_entry(entry: Dict[str, Any]) -> Dict[str, Any]:
    ensure_dirs()

    category = normalize_category(entry["category"])
    target_file = CATEGORY_FILES[category]

    memory_data = load_category_memory(category)
    memory_data["entries"].append(entry)
    memory_data["updated_at"] = utc_now()

    write_json(target_file, memory_data)

    index = load_index()
    index["updated_at"] = utc_now()

    index["categories"].setdefault(
        category,
        {
            "file": str(target_file),
            "entry_count": 0,
            "updated_at": utc_now(),
        },
    )

    index["categories"][category]["file"] = str(target_file)
    index["categories"][category]["entry_count"] = len(memory_data["entries"])
    index["categories"][category]["updated_at"] = utc_now()

    index["entries"].append(
        {
            "id": entry["id"],
            "category": category,
            "title": entry["title"],
            "source": entry["source"],
            "created_at": entry["created_at"],
            "file": str(target_file),
        }
    )

    write_json(MEMORY_INDEX_FILE, index)

    result = {
        "ok": True,
        "message": "Memory entry written.",
        "entry_id": entry["id"],
        "category": category,
        "target_file": str(target_file),
        "index_file": str(MEMORY_INDEX_FILE),
        "timestamp": utc_now(),
    }

    append_log(
        {
            "event": "memory_write",
            "result": result,
            "entry": entry,
        }
    )

    return result


def parse_tags(raw_tags: Optional[str]) -> List[str]:
    if not raw_tags:
        return []

    return [
        tag.strip()
        for tag in raw_tags.split(",")
        if tag.strip()
    ]


def parse_metadata(raw_metadata: Optional[str]) -> Dict[str, Any]:
    if not raw_metadata:
        return {}

    data = json.loads(raw_metadata)

    if not isinstance(data, dict):
        raise ValueError("--metadata must be a JSON object.")

    return data


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis controlled memory writer")

    parser.add_argument(
        "--category",
        required=True,
        choices=sorted(SUPPORTED_CATEGORIES),
        help="Memory category",
    )

    parser.add_argument(
        "--title",
        required=True,
        help="Memory entry title",
    )

    parser.add_argument(
        "--content",
        required=True,
        help="Memory entry content",
    )

    parser.add_argument(
        "--source",
        default="manual",
        help="Source of the memory entry",
    )

    parser.add_argument(
        "--confidence",
        type=float,
        default=0.8,
        help="Confidence score from 0 to 1",
    )

    parser.add_argument(
        "--persistence",
        default="long_term",
        help="Persistence type, e.g. short_term, medium_term, long_term",
    )

    parser.add_argument(
        "--tags",
        default=None,
        help="Comma-separated tags",
    )

    parser.add_argument(
        "--metadata",
        default=None,
        help="Optional JSON object metadata",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    try:
        category = normalize_category(args.category)
        tags = parse_tags(args.tags)
        metadata = parse_metadata(args.metadata)

        entry = build_entry(
            category=category,
            title=args.title,
            content=args.content,
            source=args.source,
            confidence=args.confidence,
            persistence=args.persistence,
            tags=tags,
            metadata=metadata,
        )

        result = write_memory_entry(entry)

    except Exception as exc:
        result = {
            "ok": False,
            "message": "Memory write failed.",
            "error": str(exc),
            "timestamp": utc_now(),
        }

        append_log(
            {
                "event": "memory_write_failed",
                "result": result,
            }
        )

        print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
        return 1

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
