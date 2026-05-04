import argparse
import json
import subprocess
from datetime import datetime
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path("/home/home/jarvis")
RUN_LOG = PROJECT_ROOT / "logs" / "executor_runs.jsonl"
STATE_FILE = PROJECT_ROOT / "memory" / "executor_runs.json"


ALLOWED_TASKS = {
    "system_status": {
        "description": "Check basic Jarvis system status.",
        "command": ["bash", "-lc", "cd /home/home/jarvis && git status --short && test -d OpenJarvis && echo 'OpenJarvis directory OK' || echo 'OpenJarvis directory missing'"],
        "requires_confirmation": False,
    },
    "morning_briefing": {
        "description": "Run Jarvis morning briefing autopilot.",
        "command": ["bash", "-lc", "cd /home/home/jarvis && ./scripts/run_briefing_autopilot.sh morning"],
        "requires_confirmation": False,
    },
    "midday_briefing": {
        "description": "Run Jarvis midday briefing autopilot.",
        "command": ["bash", "-lc", "cd /home/home/jarvis && ./scripts/run_briefing_autopilot.sh midday"],
        "requires_confirmation": False,
    },
    "git_status": {
        "description": "Show current git status.",
        "command": ["bash", "-lc", "cd /home/home/jarvis && git status"],
        "requires_confirmation": False,
    },
}


def now_iso() -> str:
    return datetime.now().isoformat(timespec="seconds")


def ensure_dirs() -> None:
    RUN_LOG.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)


def write_jsonl(record: dict[str, Any]) -> None:
    ensure_dirs()
    with RUN_LOG.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")


def write_state(record: dict[str, Any]) -> None:
    ensure_dirs()
    with STATE_FILE.open("w", encoding="utf-8") as f:
        json.dump(record, f, ensure_ascii=False, indent=2)


def list_tasks() -> dict[str, Any]:
    return {
        "ok": True,
        "tasks": {
            name: {
                "description": task["description"],
                "requires_confirmation": task["requires_confirmation"],
            }
            for name, task in ALLOWED_TASKS.items()
        },
    }


def run_task(task_name: str, confirmed: bool = False) -> dict[str, Any]:
    started_at = now_iso()

    if task_name not in ALLOWED_TASKS:
        result = {
            "agent": "executor_agent",
            "task": task_name,
            "started_at": started_at,
            "finished_at": now_iso(),
            "ok": False,
            "message": "Unknown task.",
            "error": f"Task '{task_name}' is not allowed.",
        }
        write_jsonl(result)
        write_state(result)
        return result

    task = ALLOWED_TASKS[task_name]

    if task["requires_confirmation"] and not confirmed:
        result = {
            "agent": "executor_agent",
            "task": task_name,
            "started_at": started_at,
            "finished_at": now_iso(),
            "ok": False,
            "requires_confirmation": True,
            "message": "Task requires confirmation.",
            "error": None,
        }
        write_jsonl(result)
        write_state(result)
        return result

    try:
        completed = subprocess.run(
            task["command"],
            cwd=str(PROJECT_ROOT),
            capture_output=True,
            text=True,
            timeout=300,
            check=False,
        )

        result = {
            "agent": "executor_agent",
            "task": task_name,
            "started_at": started_at,
            "finished_at": now_iso(),
            "ok": completed.returncode == 0,
            "returncode": completed.returncode,
            "stdout": completed.stdout.strip(),
            "stderr": completed.stderr.strip(),
            "message": "Task completed." if completed.returncode == 0 else "Task failed.",
            "error": None if completed.returncode == 0 else completed.stderr.strip(),
        }

    except Exception as e:
        result = {
            "agent": "executor_agent",
            "task": task_name,
            "started_at": started_at,
            "finished_at": now_iso(),
            "ok": False,
            "message": "Executor crashed.",
            "error": str(e),
        }

    write_jsonl(result)
    write_state(result)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description="Jarvis Executor Agent")
    parser.add_argument("--list", action="store_true", help="List allowed tasks")
    parser.add_argument("--task", type=str, help="Task name to run")
    parser.add_argument("--confirmed", action="store_true", help="Confirm protected task")

    args = parser.parse_args()

    if args.list:
        print(json.dumps(list_tasks(), ensure_ascii=False, indent=2))
        return

    if not args.task:
        print(json.dumps({"ok": False, "error": "Use --list or --task TASK_NAME"}, ensure_ascii=False, indent=2))
        return

    result = run_task(args.task, confirmed=args.confirmed)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
