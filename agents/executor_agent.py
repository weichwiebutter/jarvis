#!/usr/bin/env python3

import argparse
import json
import os
import re
import subprocess
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any, Optional


PROJECT_ROOT = Path("/home/home/jarvis")
TASKS_FILE = PROJECT_ROOT / "config" / "executor_tasks.json"
RUN_LOG = PROJECT_ROOT / "logs" / "executor_runs.jsonl"
STATE_FILE = PROJECT_ROOT / "memory" / "executor_runs.json"

MEMORY_DIR = PROJECT_ROOT / "memory"
MEMORY_INDEX_FILE = MEMORY_DIR / "memory_index.json"


CATEGORY_FILES = {
    "profile": MEMORY_DIR / "jarvis_profile.json",
    "preferences": MEMORY_DIR / "preferences.json",
    "facts": MEMORY_DIR / "facts.json",
    "learnings": MEMORY_DIR / "learnings.json",
    "tasks": MEMORY_DIR / "tasks_memory.json",
    "decisions": MEMORY_DIR / "decisions.json",
    "system_state": MEMORY_DIR / "system_state_memory.json",
}


SUPPORTED_MEMORY_CATEGORIES = set(CATEGORY_FILES.keys())


def now_iso() -> str:
    return datetime.now().isoformat(timespec="seconds")


def ensure_dirs() -> None:
    RUN_LOG.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    TASKS_FILE.parent.mkdir(parents=True, exist_ok=True)
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)


def clean_output(text: str) -> str:
    if not text:
        return ""
    return re.sub(r"\x1B\[[0-?]*[ -/]*[@-~]", "", text)


def read_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default

    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return default


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False, default=str),
        encoding="utf-8",
    )


def load_tasks() -> dict[str, Any]:
    ensure_dirs()

    if not TASKS_FILE.exists():
        return {}

    with TASKS_FILE.open("r", encoding="utf-8") as f:
        data = json.load(f)

    if not isinstance(data, dict):
        raise ValueError("executor_tasks.json must contain a JSON object.")

    return data


def write_jsonl(record: dict[str, Any]) -> None:
    ensure_dirs()

    with RUN_LOG.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False, default=str) + "\n")


def write_state(record: dict[str, Any]) -> None:
    ensure_dirs()

    with STATE_FILE.open("w", encoding="utf-8") as f:
        json.dump(record, f, ensure_ascii=False, indent=2, default=str)


def task_type(task: dict[str, Any]) -> str:
    return str(task.get("type", "command")).strip().lower()


def list_tasks() -> dict[str, Any]:
    tasks = load_tasks()

    return {
        "ok": True,
        "tasks_file": str(TASKS_FILE),
        "tasks": {
            name: {
                "description": task.get("description", ""),
                "type": task_type(task),
                "provider": task.get("provider", "auto"),
                "model_size": task.get("model_size"),
                "requires_confirmation": bool(task.get("requires_confirmation", False)),
            }
            for name, task in tasks.items()
            if isinstance(task, dict)
        },
    }


def base_result(
    task_name: str,
    started_at: str,
    task: Optional[dict[str, Any]] = None,
) -> dict[str, Any]:
    task = task or {}

    return {
        "agent": "executor_agent",
        "task": task_name,
        "type": task_type(task) if task else None,
        "description": task.get("description", ""),
        "provider": task.get("provider", "auto"),
        "started_at": started_at,
        "finished_at": now_iso(),
        "ok": False,
        "stdout": "",
        "stderr": "",
        "message": "",
        "error": None,
    }


def validate_task_config(task_name: str, task: dict[str, Any]) -> None:
    if not isinstance(task, dict):
        raise ValueError(f"Task '{task_name}' must be an object.")

    current_type = task_type(task)

    if "requires_confirmation" not in task:
        task["requires_confirmation"] = False

    if current_type == "command":
        if "command" not in task:
            raise ValueError(f"Task '{task_name}' has no command.")
        if not isinstance(task["command"], str):
            raise ValueError(f"Task '{task_name}' command must be a string.")

    elif current_type == "llm":
        model_size = task.get("model_size")
        prompt = task.get("prompt")
        provider = task.get("provider", "auto")

        if model_size not in {"small", "large", "external"}:
            raise ValueError(
                f"Task '{task_name}' model_size must be 'small', 'large', or 'external'."
            )

        if provider not in {"auto", "local", "openrouter"}:
            raise ValueError(
                f"Task '{task_name}' provider must be 'auto', 'local', or 'openrouter'."
            )

        if not isinstance(prompt, str) or not prompt.strip():
            raise ValueError(f"Task '{task_name}' prompt must be a non-empty string.")

        system_prompt = task.get("system_prompt", "")
        if system_prompt is not None and not isinstance(system_prompt, str):
            raise ValueError(f"Task '{task_name}' system_prompt must be a string.")

    elif current_type == "memory_write":
        category = task.get("category")
        if category is not None and category not in SUPPORTED_MEMORY_CATEGORIES:
            raise ValueError(
                f"Task '{task_name}' category must be one of {sorted(SUPPORTED_MEMORY_CATEGORIES)}."
            )

    elif current_type == "memory_read":
        category = task.get("category")
        if category is not None and category not in SUPPORTED_MEMORY_CATEGORIES:
            raise ValueError(
                f"Task '{task_name}' category must be one of {sorted(SUPPORTED_MEMORY_CATEGORIES)}."
            )

    else:
        raise ValueError(f"Task '{task_name}' has unknown type '{current_type}'.")


def build_prompt(task: dict[str, Any], payload: Optional[dict[str, Any]]) -> str:
    system_prompt = task.get("system_prompt", "")
    prompt = task.get("prompt", "")

    parts = []

    if system_prompt:
        parts.append("SYSTEM:")
        parts.append(system_prompt.strip())
        parts.append("")

    parts.append("USER:")
    parts.append(prompt.strip())

    if payload:
        parts.append("")
        parts.append("PAYLOAD:")
        parts.append(json.dumps(payload, ensure_ascii=False, indent=2, default=str))

    return "\n".join(parts).strip() + "\n"


def resolve_local_llm_command(model_size: str) -> tuple[Optional[str], Optional[str]]:
    if model_size == "small":
        env_var = "JARVIS_LLM_SMALL_CMD"
    else:
        env_var = "JARVIS_LLM_LARGE_CMD"

    return os.environ.get(env_var), env_var


def should_use_openrouter(task: dict[str, Any], model_size: str) -> bool:
    provider = task.get("provider", "auto")

    if provider == "openrouter":
        return True

    if provider == "local":
        return False

    if model_size == "external":
        return True

    local_command, _ = resolve_local_llm_command(model_size)
    if not local_command and os.environ.get("OPENROUTER_API_KEY"):
        return True

    return False


def run_command_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
) -> dict[str, Any]:
    command = task["command"]
    timeout = int(task.get("timeout", 300))

    try:
        completed = subprocess.run(
            ["bash", "-lc", command],
            cwd=str(PROJECT_ROOT),
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )

        stdout = clean_output(completed.stdout).strip()
        stderr = clean_output(completed.stderr).strip()

        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": completed.returncode == 0,
                "returncode": completed.returncode,
                "stdout": stdout,
                "stderr": stderr,
                "message": (
                    "Task completed."
                    if completed.returncode == 0
                    else "Task failed."
                ),
                "error": None if completed.returncode == 0 else stderr,
            }
        )
        return result

    except Exception as e:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Executor crashed during command task.",
                "error": str(e),
            }
        )
        return result


def run_local_llm_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
    payload: Optional[dict[str, Any]],
) -> dict[str, Any]:
    model_size = task["model_size"]
    command, env_var = resolve_local_llm_command(model_size)

    if not command:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Local LLM command not configured.",
                "error": f"Missing environment variable: {env_var}",
                "model_size": model_size,
                "llm_env_var": env_var,
                "provider_used": "local",
            }
        )
        return result

    prompt = build_prompt(task, payload)
    timeout = int(task.get("timeout", 300))

    try:
        completed = subprocess.run(
            ["bash", "-lc", command],
            cwd=str(PROJECT_ROOT),
            input=prompt,
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )

        stdout = clean_output(completed.stdout).strip()
        stderr = clean_output(completed.stderr).strip()

        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": completed.returncode == 0,
                "returncode": completed.returncode,
                "stdout": stdout,
                "stderr": stderr,
                "message": (
                    "Local LLM task completed."
                    if completed.returncode == 0
                    else "Local LLM task failed."
                ),
                "error": None if completed.returncode == 0 else stderr,
                "model_size": model_size,
                "llm_env_var": env_var,
                "provider_used": "local",
            }
        )
        return result

    except Exception as e:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Executor crashed during local LLM task.",
                "error": str(e),
                "model_size": model_size,
                "llm_env_var": env_var,
                "provider_used": "local",
            }
        )
        return result


def run_openrouter_llm_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
    payload: Optional[dict[str, Any]],
) -> dict[str, Any]:
    prompt = build_prompt(task, payload)
    model = task.get("model") or os.environ.get(
        "OPENROUTER_DEFAULT_MODEL",
        "openai/gpt-4o-mini",
    )

    try:
        from agents.tool_adapters.openrouter_adapter import run_openrouter

        adapter_result = run_openrouter(prompt=prompt, model=model)

        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": bool(adapter_result.get("ok")),
                "stdout": adapter_result.get("output", ""),
                "stderr": "",
                "message": (
                    "OpenRouter LLM task completed."
                    if adapter_result.get("ok")
                    else "OpenRouter LLM task failed."
                ),
                "error": None if adapter_result.get("ok") else adapter_result.get("error"),
                "model_size": task.get("model_size"),
                "model": model,
                "provider_used": "openrouter",
                "adapter_result": adapter_result,
            }
        )
        return result

    except Exception as e:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Executor crashed during OpenRouter LLM task.",
                "error": str(e),
                "model_size": task.get("model_size"),
                "model": model,
                "provider_used": "openrouter",
            }
        )
        return result


def run_llm_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
    payload: Optional[dict[str, Any]],
) -> dict[str, Any]:
    model_size = task["model_size"]

    if should_use_openrouter(task, model_size):
        return run_openrouter_llm_task(task_name, task, started_at, payload)

    return run_local_llm_task(task_name, task, started_at, payload)


def normalize_memory_payload(task: dict[str, Any], payload: Optional[dict[str, Any]]) -> dict[str, Any]:
    payload = payload or {}

    category = payload.get("category") or task.get("category")
    title = payload.get("title") or task.get("title")
    content = payload.get("content") or task.get("content")
    source = payload.get("source") or task.get("source") or "executor_memory_write"
    confidence = payload.get("confidence", task.get("confidence", 0.8))
    persistence = payload.get("persistence") or task.get("persistence") or "long_term"
    tags = payload.get("tags") or task.get("tags") or []
    metadata = payload.get("metadata") or task.get("metadata") or {}

    if isinstance(tags, str):
        tags = [tag.strip() for tag in tags.split(",") if tag.strip()]

    if not isinstance(tags, list):
        raise ValueError("tags must be a list or comma-separated string.")

    if not isinstance(metadata, dict):
        raise ValueError("metadata must be an object.")

    if category not in SUPPORTED_MEMORY_CATEGORIES:
        raise ValueError(
            f"Unsupported memory category: {category}. "
            f"Supported: {sorted(SUPPORTED_MEMORY_CATEGORIES)}"
        )

    if not isinstance(title, str) or not title.strip():
        raise ValueError("Memory title is required.")

    if not isinstance(content, str) or not content.strip():
        raise ValueError("Memory content is required.")

    confidence_float = float(confidence)

    if confidence_float < 0 or confidence_float > 1:
        raise ValueError("confidence must be between 0 and 1.")

    return {
        "category": category,
        "title": title.strip(),
        "content": content.strip(),
        "source": str(source),
        "confidence": confidence_float,
        "persistence": str(persistence),
        "tags": tags,
        "metadata": metadata,
    }


def load_category_memory(category: str) -> dict[str, Any]:
    path = CATEGORY_FILES[category]

    data = read_json(
        path,
        {
            "category": category,
            "created_at": now_iso(),
            "updated_at": now_iso(),
            "entries": [],
        },
    )

    if not isinstance(data, dict):
        data = {
            "category": category,
            "created_at": now_iso(),
            "updated_at": now_iso(),
            "entries": [],
        }

    if not isinstance(data.get("entries"), list):
        data["entries"] = []

    data.setdefault("category", category)
    data.setdefault("created_at", now_iso())
    data["updated_at"] = now_iso()

    return data


def load_memory_index() -> dict[str, Any]:
    data = read_json(
        MEMORY_INDEX_FILE,
        {
            "created_at": now_iso(),
            "updated_at": now_iso(),
            "categories": {},
            "entries": [],
        },
    )

    if not isinstance(data, dict):
        data = {
            "created_at": now_iso(),
            "updated_at": now_iso(),
            "categories": {},
            "entries": [],
        }

    if not isinstance(data.get("categories"), dict):
        data["categories"] = {}

    if not isinstance(data.get("entries"), list):
        data["entries"] = []

    return data


def run_memory_write_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
    payload: Optional[dict[str, Any]],
) -> dict[str, Any]:
    try:
        normalized = normalize_memory_payload(task, payload)
        category = normalized["category"]
        target_file = CATEGORY_FILES[category]

        entry = {
            "id": str(uuid.uuid4()),
            "category": category,
            "title": normalized["title"],
            "content": normalized["content"],
            "source": normalized["source"],
            "confidence": normalized["confidence"],
            "persistence": normalized["persistence"],
            "tags": normalized["tags"],
            "metadata": normalized["metadata"],
            "created_at": now_iso(),
            "updated_at": now_iso(),
            "status": "active",
        }

        category_memory = load_category_memory(category)
        category_memory["entries"].append(entry)
        category_memory["updated_at"] = now_iso()

        write_json(target_file, category_memory)

        index = load_memory_index()
        index["updated_at"] = now_iso()
        index["categories"].setdefault(
            category,
            {
                "file": str(target_file),
                "entry_count": 0,
                "updated_at": now_iso(),
            },
        )
        index["categories"][category]["file"] = str(target_file)
        index["categories"][category]["entry_count"] = len(category_memory["entries"])
        index["categories"][category]["updated_at"] = now_iso()

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

        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": True,
                "message": "Memory write completed.",
                "stdout": json.dumps(
                    {
                        "entry_id": entry["id"],
                        "category": category,
                        "target_file": str(target_file),
                        "index_file": str(MEMORY_INDEX_FILE),
                    },
                    indent=2,
                    ensure_ascii=False,
                ),
                "stderr": "",
                "error": None,
                "provider_used": "internal_memory_writer",
                "memory_entry": entry,
            }
        )
        return result

    except Exception as e:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Memory write failed.",
                "error": str(e),
                "provider_used": "internal_memory_writer",
            }
        )
        return result


def normalize_memory_read_payload(task: dict[str, Any], payload: Optional[dict[str, Any]]) -> dict[str, Any]:
    payload = payload or {}

    category = payload.get("category", task.get("category"))
    query = payload.get("query", task.get("query"))
    limit = payload.get("limit", task.get("limit", 20))

    if category is not None:
        category = str(category).strip().lower()
        if category not in SUPPORTED_MEMORY_CATEGORIES:
            raise ValueError(
                f"Unsupported memory category: {category}. "
                f"Supported: {sorted(SUPPORTED_MEMORY_CATEGORIES)}"
            )

    if query is not None:
        query = str(query).strip()
        if not query:
            query = None

    limit_int = int(limit)
    if limit_int < 0:
        raise ValueError("limit must be 0 or greater.")

    return {
        "category": category,
        "query": query,
        "limit": limit_int,
    }


def entry_matches_query(entry: dict[str, Any], query: Optional[str]) -> bool:
    if not query:
        return True

    q = query.lower().strip()

    searchable_parts = [
        str(entry.get("category", "")),
        str(entry.get("title", "")),
        str(entry.get("content", "")),
        str(entry.get("source", "")),
        " ".join(str(tag) for tag in entry.get("tags", []) if isinstance(tag, str)),
    ]

    haystack = " ".join(searchable_parts).lower()

    return q in haystack


def trim_memory_entry(entry: dict[str, Any]) -> dict[str, Any]:
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


def load_memory_entries(category: Optional[str]) -> tuple[list[dict[str, Any]], list[str]]:
    if category:
        data = load_category_memory(category)
        entries = data.get("entries", [])
        return [entry for entry in entries if isinstance(entry, dict)], [category]

    all_entries: list[dict[str, Any]] = []
    categories_read: list[str] = []

    for category_name in sorted(SUPPORTED_MEMORY_CATEGORIES):
        data = load_category_memory(category_name)
        entries = data.get("entries", [])
        all_entries.extend([entry for entry in entries if isinstance(entry, dict)])
        categories_read.append(category_name)

    return all_entries, categories_read


def run_memory_read_task(
    task_name: str,
    task: dict[str, Any],
    started_at: str,
    payload: Optional[dict[str, Any]],
) -> dict[str, Any]:
    try:
        normalized = normalize_memory_read_payload(task, payload)
        category = normalized["category"]
        query = normalized["query"]
        limit = normalized["limit"]

        entries, categories_read = load_memory_entries(category)

        filtered = [
            entry for entry in entries
            if entry_matches_query(entry, query)
        ]

        filtered = sorted(
            filtered,
            key=lambda item: str(item.get("created_at", "")),
            reverse=True,
        )

        if limit > 0:
            filtered = filtered[:limit]

        read_result = {
            "ok": True,
            "timestamp": now_iso(),
            "category": category,
            "query": query,
            "limit": limit,
            "categories_read": categories_read,
            "count": len(filtered),
            "entries": [trim_memory_entry(entry) for entry in filtered],
            "memory_index_file": str(MEMORY_INDEX_FILE),
        }

        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": True,
                "message": "Memory read completed.",
                "stdout": json.dumps(read_result, indent=2, ensure_ascii=False),
                "stderr": "",
                "error": None,
                "provider_used": "internal_memory_reader",
                "memory_read": read_result,
            }
        )
        return result

    except Exception as e:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Memory read failed.",
                "error": str(e),
                "provider_used": "internal_memory_reader",
            }
        )
        return result


def run_task(
    task_name: str,
    payload: Optional[dict[str, Any]] = None,
    confirmed: bool = False,
) -> dict[str, Any]:
    started_at = now_iso()

    if payload is not None and not isinstance(payload, dict):
        confirmed = bool(payload)
        payload = None

    try:
        tasks = load_tasks()
    except Exception as e:
        result = base_result(task_name, started_at)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Could not load executor tasks.",
                "error": str(e),
            }
        )
        write_jsonl(result)
        write_state(result)
        return result

    if task_name not in tasks:
        result = base_result(task_name, started_at)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Unknown task.",
                "error": f"Task '{task_name}' is not configured in {TASKS_FILE}.",
            }
        )
        write_jsonl(result)
        write_state(result)
        return result

    task = tasks[task_name]

    try:
        validate_task_config(task_name, task)
    except Exception as e:
        result = base_result(task_name, started_at, task if isinstance(task, dict) else {})
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Invalid task configuration.",
                "error": str(e),
            }
        )
        write_jsonl(result)
        write_state(result)
        return result

    requires_confirmation = bool(task.get("requires_confirmation", False))

    if requires_confirmation and not confirmed:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "requires_confirmation": True,
                "message": "Task requires confirmation.",
                "error": None,
            }
        )
        write_jsonl(result)
        write_state(result)
        return result

    current_type = task_type(task)

    if current_type == "command":
        result = run_command_task(task_name, task, started_at)
    elif current_type == "llm":
        result = run_llm_task(task_name, task, started_at, payload)
    elif current_type == "memory_write":
        result = run_memory_write_task(task_name, task, started_at, payload)
    elif current_type == "memory_read":
        result = run_memory_read_task(task_name, task, started_at, payload)
    else:
        result = base_result(task_name, started_at, task)
        result.update(
            {
                "finished_at": now_iso(),
                "ok": False,
                "message": "Unknown task type.",
                "error": f"Unsupported task type: {current_type}",
            }
        )

    write_jsonl(result)
    write_state(result)
    return result


def parse_payload(raw_payload: Optional[str]) -> Optional[dict[str, Any]]:
    if not raw_payload:
        return None

    data = json.loads(raw_payload)

    if not isinstance(data, dict):
        raise ValueError("--payload must be a JSON object.")

    return data


def main() -> None:
    parser = argparse.ArgumentParser(description="Jarvis Executor Agent")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--task", type=str)
    parser.add_argument("--confirmed", action="store_true")
    parser.add_argument("--payload", type=str, default=None)

    args = parser.parse_args()

    if args.list:
        print(json.dumps(list_tasks(), ensure_ascii=False, indent=2, default=str))
        return

    if not args.task:
        print(
            json.dumps(
                {"ok": False, "error": "Use --list or --task TASK_NAME"},
                ensure_ascii=False,
                indent=2,
            )
        )
        return

    try:
        payload = parse_payload(args.payload)
    except Exception as e:
        print(
            json.dumps(
                {"ok": False, "error": f"Invalid payload: {e}"},
                ensure_ascii=False,
                indent=2,
            )
        )
        return

    result = run_task(args.task, payload=payload, confirmed=args.confirmed)
    print(json.dumps(result, ensure_ascii=False, indent=2, default=str))


if __name__ == "__main__":
    main()
