#!/usr/bin/env python3
"""
Jarvis Memory Agent V3

Role:
    Decides whether a user request should write memory or read memory.

Important:
    - Does NOT write memory directly
    - Does NOT read memory directly
    - Does NOT modify files directly
    - Does NOT call subprocess
    - Does NOT call LLMs directly
    - Produces executor task envelopes only

Flow:
    "Merk dir: ..."              -> executor task memory_write
    "Was weißt du über Voice?"   -> executor task memory_read_voice / memory_read_all
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]
LOG_DIR = PROJECT_ROOT / "logs"
MEMORY_LOG = LOG_DIR / "memory_agent.log"


SUPPORTED_CATEGORIES = {
    "profile",
    "preferences",
    "facts",
    "learnings",
    "tasks",
    "decisions",
    "system_state",
}


@dataclass
class MemoryRequest:
    task: str
    category: Optional[str] = None
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class MemoryResult:
    ok: bool
    timestamp: str
    task: str
    decision: str
    output: Any
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def normalize(text: str) -> str:
    return text.strip().lower()


def detect_write_intent(task: str) -> bool:
    text = normalize(task)

    triggers = [
        "merk dir",
        "merke dir",
        "speichere",
        "notiere",
        "remember",
        "ab jetzt",
        "immer",
        "jarvis soll sich merken",
        "soll sich merken",
    ]

    return any(trigger in text for trigger in triggers)


def detect_read_intent(task: str) -> bool:
    text = normalize(task)

    triggers = [
        "was weißt du",
        "was weisst du",
        "was ist gespeichert",
        "was hast du gespeichert",
        "zeige memory",
        "zeige gedächtnis",
        "lies memory",
        "lies gedächtnis",
        "erinnere dich",
        "was weiß jarvis",
        "was weisst jarvis",
        "memory zu",
        "gedächtnis zu",
    ]

    return any(trigger in text for trigger in triggers)


def classify_category(task: str, explicit_category: Optional[str]) -> str:
    if explicit_category:
        category = normalize(explicit_category)
        if category in SUPPORTED_CATEGORIES:
            return category

    text = normalize(task)

    if any(word in text for word in ["immer", "ab jetzt", "präferenz", "preference", "stil", "style"]):
        return "preferences"

    if any(word in text for word in ["entscheidung", "beschluss", "festgelegt", "regel"]):
        return "decisions"

    if any(word in text for word in ["gelernt", "lernen", "fehler", "pattern", "erfahrung"]):
        return "learnings"

    if any(word in text for word in ["profil", "über mich", "ich bin", "user"]):
        return "profile"

    if any(word in text for word in ["task", "aufgabe", "nächster schritt"]):
        return "tasks"

    if any(word in text for word in ["system", "status", "state", "log"]):
        return "system_state"

    return "facts"


def clean_write_content(task: str) -> str:
    replacements = [
        "merk dir:",
        "merk dir",
        "merke dir:",
        "merke dir",
        "speichere:",
        "speichere",
        "notiere:",
        "notiere",
        "remember:",
        "remember",
        "jarvis soll sich merken:",
        "jarvis soll sich merken",
        "soll sich merken:",
        "soll sich merken",
    ]

    cleaned = task.strip()
    lower = cleaned.lower()

    for item in replacements:
        if lower.startswith(item):
            cleaned = cleaned[len(item):].strip()
            break

    return cleaned.strip(" .")


def extract_query(task: str) -> str:
    text = task.strip()

    replacements = [
        "was weißt du über",
        "was weisst du über",
        "was weißt du zu",
        "was weisst du zu",
        "was ist gespeichert zu",
        "was hast du gespeichert zu",
        "zeige memory zu",
        "zeige gedächtnis zu",
        "lies memory zu",
        "lies gedächtnis zu",
        "memory zu",
        "gedächtnis zu",
    ]

    lower = text.lower()

    for item in replacements:
        if item in lower:
            start = lower.find(item)
            query = text[start + len(item):].strip(" ?:.,")
            if query:
                return query

    important_terms = [
        "voice",
        "sprache",
        "whisper",
        "tts",
        "obsidian",
        "memory",
        "gedächtnis",
        "jarvis",
        "hermes",
        "coding",
        "code",
        "briefing",
        "github",
    ]

    for term in important_terms:
        if term in lower:
            return term

    return ""


def build_title(category: str, content: str) -> str:
    if category == "preferences":
        return "User Preference"

    if category == "decisions":
        return "Architecture Decision"

    if category == "profile":
        return "User Profile Memory"

    if category == "learnings":
        return "Learning"

    if category == "tasks":
        return "Task Context"

    if category == "system_state":
        return "System State Memory"

    words = content.split()
    short = " ".join(words[:6]) if words else "Memory"
    return short


def build_tags(category: str, task: str) -> List[str]:
    tags = ["memory", category]

    text = normalize(task)

    if "voice" in text or "sprache" in text or "whisper" in text or "tts" in text:
        tags.append("voice")

    if "coding" in text or "code" in text or "datei" in text:
        tags.append("coding")

    if "jarvis" in text:
        tags.append("jarvis")

    if "hermes" in text:
        tags.append("hermes")

    if "obsidian" in text:
        tags.append("obsidian")

    if "github" in text or "git" in text:
        tags.append("git")

    return sorted(set(tags))


def build_write_payload(request: MemoryRequest) -> Dict[str, Any]:
    category = classify_category(request.task, request.category)
    content = clean_write_content(request.task)

    if not content:
        content = request.task.strip()

    return {
        "category": category,
        "title": build_title(category, content),
        "content": content,
        "source": "memory_agent",
        "confidence": 0.9,
        "persistence": "long_term",
        "tags": build_tags(category, request.task),
        "metadata": {
            "source_agent": "memory_agent",
            "original_task": request.task,
            "context": request.context,
            **request.metadata,
        },
    }


def build_write_result(request: MemoryRequest) -> MemoryResult:
    payload = build_write_payload(request)

    return MemoryResult(
        ok=True,
        timestamp=utc_now(),
        task=request.task,
        decision="create_memory_entry",
        output={
            "type": "executor_task",
            "task_name": "memory_write",
            "payload": payload,
            "confirmed": bool(request.metadata.get("confirmed", False)),
            "requires_approval": True,
        },
        metadata={
            "source": "memory_agent",
            "execution_performed": False,
            "memory_detected": True,
            "memory_action": "write",
            "executor_required": True,
            "human_in_the_loop": True,
        },
    )


def build_read_result(request: MemoryRequest) -> MemoryResult:
    query = extract_query(request.task)

    if query and any(term in normalize(query) for term in ["voice", "sprache", "whisper", "tts"]):
        task_name = "memory_read_voice"
        payload = {}
    elif query:
        task_name = "memory_read_all"
        payload = {
            "query": query,
        }
    else:
        task_name = "memory_read_all"
        payload = {}

    return MemoryResult(
        ok=True,
        timestamp=utc_now(),
        task=request.task,
        decision="read_memory",
        output={
            "type": "executor_task",
            "task_name": task_name,
            "payload": payload,
            "confirmed": True,
            "requires_approval": False,
        },
        metadata={
            "source": "memory_agent",
            "execution_performed": False,
            "memory_detected": True,
            "memory_action": "read",
            "executor_required": True,
            "query": query,
        },
    )


def build_memory_result(request: MemoryRequest) -> MemoryResult:
    if detect_write_intent(request.task):
        return build_write_result(request)

    if detect_read_intent(request.task):
        return build_read_result(request)

    return MemoryResult(
        ok=True,
        timestamp=utc_now(),
        task=request.task,
        decision="no_memory_needed",
        output="Keine Memory-Aktion erkannt.",
        metadata={
            "source": "memory_agent",
            "execution_performed": False,
            "memory_detected": False,
        },
    )


def log_result(result: MemoryResult) -> None:
    ensure_dirs()

    with MEMORY_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class MemoryAgent:
    def handle(self, request: MemoryRequest) -> MemoryResult:
        try:
            result = build_memory_result(request)

        except Exception as exc:
            result = MemoryResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                decision="memory_planning_failed",
                output=None,
                error=str(exc),
                metadata={
                    "source": "memory_agent",
                    "execution_performed": False,
                },
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Memory Agent V3")

    parser.add_argument(
        "task",
        nargs="*",
        help="Memory-related user statement",
    )

    parser.add_argument(
        "--category",
        default=None,
        choices=sorted(SUPPORTED_CATEGORIES),
        help="Optional memory category",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Optional context",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    task = " ".join(args.task).strip()

    if not task:
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": "No memory task provided.",
                    "example": "python3 agents/memory_agent.py 'Was weißt du über Voice?'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = MemoryAgent()
    result = agent.handle(
        MemoryRequest(
            task=task,
            category=args.category,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
