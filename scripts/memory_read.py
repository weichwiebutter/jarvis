#!/usr/bin/env python3
"""
Jarvis Memory Read Script

Role:
    Controlled memory reader for Jarvis.

Purpose:
    - Read structured memory entries from JSON files
    - Support category filtering
    - Support simple keyword search
    - Return JSON to stdout
    - Be callable later through Executor tasks

Important:
    - No LLM calls
    - No subprocess calls
    - No Git actions
    - Read-only
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]

MEMORY_DIR = PROJECT_ROOT / "memory"
LOG_DIR = PROJECT_ROOT / "logs"

MEMORY_INDEX_FILE = MEMORY_DIR / "memory_index.json"
MEMORY_READ_LOG = LOG_DIR / "memory_read.log"


CATEGORY_FILES = {
    "profile": MEMORY_DIR / "jarvis_profile.json",
    "preferences": MEMORY_DIR / "preferences.json",
    "facts": MEMORY_DIR / "facts.json",
    "learnings": MEMORY_DIR / "learnings.json",
    "tasks": MEMORY_DIR / "tasks_memory.json",
    "decisions": MEMORY_DIR / "decisions.json",
    "system_state": MEMORY_DIR / "system_state_memory.json",
}


SUPPORTED_CATEGORIES = set(CATEGORY_FILES.keys())


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def read_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default

    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return default


def append_log(record: Dict[str, Any]) -> None:
    ensure_dirs()

    with MEMORY_READ_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(record, ensure_ascii=False, default=str))
        file.write("\n")


def normalize(text: str) -> str:
    return text.strip().lower()


def load_category_entries(category: str) -> List[Dict[str, Any]]:
    if category not in SUPPORTED_CATEGORIES:
        raise ValueError(
            f"Unsupported category: {category}. "
            f"Supported categories: {sorted(SUPPORTED_CATEGORIES)}"
        )

    path = CATEGORY_FILES[category]
    data = read_json(path, {"entries": []})

    if not isinstance(data, dict):
        return []

    entries = data.get("entries", [])

    if not isinstance(entries, list):
        return []

    valid_entries = []

    for entry in entries:
        if isinstance(entry, dict):
            valid_entries.append(entry)

    return valid_entries


def load_all_entries() -> List[Dict[str, Any]]:
    all_entries: List[Dict[str, Any]] = []

    for category in sorted(SUPPORTED_CATEGORIES):
        entries = load_category_entries(category)
        all_entries.extend(entries)

    return all_entries


def entry_matches_query(entry: Dict[str, Any], query: Optional[str]) -> bool:
    if not query:
        return True

    q = normalize(query)

    searchable_parts = [
        str(entry.get("category", "")),
        str(entry.get("title", "")),
        str(entry.get("content", "")),
        str(entry.get("source", "")),
        " ".join(str(tag) for tag in entry.get("tags", []) if isinstance(tag, str)),
    ]

    haystack = normalize(" ".join(searchable_parts))

    return q in haystack


def sort_entries(entries: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    return sorted(
        entries,
        key=lambda item: str(item.get("created_at", "")),
        reverse=True,
    )


def trim_entry(entry: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "id": entry.get("id"),
        "category": entry.get("category"),
        "title": entry.get("title"),
        "content": entry.get("content"),
        "source": entry.get("source"),
        "confidence": entry.get("confidence"),
        "persistence": entry.get("persistence"),
        "tags": entry.get("tags", []),
        "created_at": entry.get("created_at"),
        "updated_at": entry.get("updated_at"),
        "status": entry.get("status", "active"),
        "metadata": entry.get("metadata", {}),
    }


def read_memory(
    category: Optional[str],
    query: Optional[str],
    limit: int,
) -> Dict[str, Any]:
    ensure_dirs()

    if category:
        category = normalize(category)

        if category not in SUPPORTED_CATEGORIES:
            raise ValueError(
                f"Unsupported category: {category}. "
                f"Supported categories: {sorted(SUPPORTED_CATEGORIES)}"
            )

        entries = load_category_entries(category)
        categories_read = [category]

    else:
        entries = load_all_entries()
        categories_read = sorted(SUPPORTED_CATEGORIES)

    filtered = [
        entry for entry in entries
        if entry_matches_query(entry, query)
    ]

    sorted_filtered = sort_entries(filtered)

    if limit > 0:
        sorted_filtered = sorted_filtered[:limit]

    result = {
        "ok": True,
        "timestamp": utc_now(),
        "category": category,
        "query": query,
        "limit": limit,
        "categories_read": categories_read,
        "count": len(sorted_filtered),
        "entries": [trim_entry(entry) for entry in sorted_filtered],
        "memory_index_file": str(MEMORY_INDEX_FILE),
    }

    append_log(
        {
            "event": "memory_read",
            "timestamp": utc_now(),
            "category": category,
            "query": query,
            "limit": limit,
            "count": result["count"],
        }
    )

    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis controlled memory reader")

    parser.add_argument(
        "--category",
        default=None,
        choices=sorted(SUPPORTED_CATEGORIES),
        help="Optional memory category",
    )

    parser.add_argument(
        "--query",
        default=None,
        help="Optional keyword search",
    )

    parser.add_argument(
        "--limit",
        type=int,
        default=20,
        help="Maximum number of entries to return. Use 0 for no limit.",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    try:
        result = read_memory(
            category=args.category,
            query=args.query,
            limit=args.limit,
        )

    except Exception as exc:
        result = {
            "ok": False,
            "timestamp": utc_now(),
            "message": "Memory read failed.",
            "error": str(exc),
        }

        append_log(
            {
                "event": "memory_read_failed",
                "timestamp": utc_now(),
                "error": str(exc),
            }
        )

        print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
        return 1

    print(json.dumps(result, indent=2, ensure_ascii=False, default=str))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
