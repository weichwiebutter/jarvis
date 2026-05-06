#!/usr/bin/env python3
"""
Jarvis Briefing Agent

Role:
    Orchestrates briefing generation through executor_agent only.

Architecture:
    briefing_agent
      -> executor task: morning_briefing / midday_briefing
      -> worker returns structured JSON
      -> executor task: briefing_refine_large
      -> final refined briefing

Rules:
    - No direct script calls
    - No subprocess calls
    - No direct LLM calls
    - All execution goes through executor_agent
    - Logs are mandatory
    - Shared state is updated through memory/state.json
"""

from __future__ import annotations

import argparse
import importlib
import json
import sys
import traceback
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]

CONFIG_PATH = PROJECT_ROOT / "config" / "executor_tasks.json"

MEMORY_DIR = PROJECT_ROOT / "memory"
STATE_PATH = MEMORY_DIR / "state.json"
BRIEFING_MEMORY_DIR = MEMORY_DIR / "briefings"

LOG_DIR = PROJECT_ROOT / "logs"
LOG_PATH = LOG_DIR / "briefing_agent.log"


SUPPORTED_BRIEFING_TYPES = {
    "morning",
    "midday",
    "evening",
    "custom",
}


TASK_CANDIDATES = {
    "morning": [
        "morning_briefing",
        "market_briefing_morning",
        "briefing_market_morning",
        "morning_market_briefing",
        "generate_morning_briefing",
    ],
    "midday": [
        "midday_briefing",
        "market_briefing_midday",
        "briefing_market_midday",
        "midday_market_briefing",
        "generate_midday_briefing",
    ],
    "evening": [
        "evening_briefing",
        "market_briefing_evening",
        "briefing_market_evening",
        "evening_market_briefing",
        "generate_evening_briefing",
    ],
    "custom": [
        "custom_briefing",
        "market_briefing_custom",
        "briefing_market_custom",
        "custom_market_briefing",
        "generate_custom_briefing",
    ],
}


REFINE_TASK_NAME = "briefing_refine_large"


@dataclass
class BriefingRequest:
    briefing_type: str
    topic: Optional[str] = None
    user_context: Optional[str] = None
    dry_run: bool = False
    refine: bool = True
    metadata: Dict[str, Any] = field(default_factory=dict)

    def validate(self) -> None:
        if self.briefing_type not in SUPPORTED_BRIEFING_TYPES:
            raise ValueError(
                f"Unsupported briefing type: {self.briefing_type}. "
                f"Supported types: {sorted(SUPPORTED_BRIEFING_TYPES)}"
            )


@dataclass
class BriefingResult:
    ok: bool
    briefing_type: str
    task_name: Optional[str]
    started_at: str
    finished_at: str
    output: Any = None
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


class MissionControlState:
    def __init__(self, state_path: Path = STATE_PATH) -> None:
        self.state_path = state_path
        self.state_path.parent.mkdir(parents=True, exist_ok=True)

    def load(self) -> Dict[str, Any]:
        if not self.state_path.exists():
            return self._default_state()

        try:
            with self.state_path.open("r", encoding="utf-8") as file:
                state = json.load(file)

            if not isinstance(state, dict):
                return self._default_state()

            return self._merge_defaults(state)

        except Exception:
            return self._default_state()

    def save(self, state: Dict[str, Any]) -> None:
        state["updated_at"] = utc_now()
        self.state_path.write_text(
            json.dumps(state, indent=2, ensure_ascii=False, default=str),
            encoding="utf-8",
        )

    def mark_running(self, request: BriefingRequest, task_name: Optional[str]) -> None:
        state = self.load()

        state["agents"]["briefing_agent"] = {
            "status": "running",
            "last_started_at": utc_now(),
            "last_finished_at": None,
            "last_error": None,
        }

        state["tasks"]["briefing"] = {
            "status": "running",
            "briefing_type": request.briefing_type,
            "task_name": task_name,
            "topic": request.topic,
            "started_at": utc_now(),
            "finished_at": None,
            "error": None,
        }

        state["mission_control"]["active_agent"] = "briefing_agent"
        state["mission_control"]["last_event"] = "briefing_started"

        self.save(state)

    def mark_finished(self, result: BriefingResult) -> None:
        state = self.load()

        state["agents"]["briefing_agent"] = {
            "status": "idle" if result.ok else "error",
            "last_started_at": result.started_at,
            "last_finished_at": result.finished_at,
            "last_error": result.error,
        }

        state["tasks"]["briefing"] = {
            "status": "completed" if result.ok else "failed",
            "briefing_type": result.briefing_type,
            "task_name": result.task_name,
            "started_at": result.started_at,
            "finished_at": result.finished_at,
            "error": result.error,
        }

        state["mission_control"]["active_agent"] = None
        state["mission_control"]["last_event"] = (
            "briefing_completed" if result.ok else "briefing_failed"
        )

        state["last_outputs"]["briefing_agent"] = {
            "ok": result.ok,
            "briefing_type": result.briefing_type,
            "task_name": result.task_name,
            "finished_at": result.finished_at,
            "summary_available": result.output is not None,
        }

        self.save(state)

    @staticmethod
    def _default_state() -> Dict[str, Any]:
        now = utc_now()
        return {
            "created_at": now,
            "updated_at": now,
            "mission_control": {
                "active_agent": None,
                "last_event": None,
            },
            "agents": {},
            "tasks": {},
            "last_outputs": {},
            "messages": [],
        }

    def _merge_defaults(self, state: Dict[str, Any]) -> Dict[str, Any]:
        default = self._default_state()

        for key, value in default.items():
            if key not in state:
                state[key] = value

        for nested_key in [
            "mission_control",
            "agents",
            "tasks",
            "last_outputs",
        ]:
            if not isinstance(state.get(nested_key), dict):
                state[nested_key] = {}

        if not isinstance(state.get("messages"), list):
            state["messages"] = []

        return state


class ExecutorAdapter:
    def __init__(self, config_path: Path = CONFIG_PATH) -> None:
        self.config_path = config_path
        self.executor = self._load_executor()

    def _load_executor(self) -> Any:
        module = self._import_executor_module()

        if hasattr(module, "ExecutorAgent"):
            executor_class = getattr(module, "ExecutorAgent")

            for args in (
                {"config_path": str(self.config_path)},
                {"tasks_config_path": str(self.config_path)},
                {},
            ):
                try:
                    return executor_class(**args)
                except TypeError:
                    continue

            return executor_class()

        if hasattr(module, "executor"):
            return getattr(module, "executor")

        return module

    @staticmethod
    def _import_executor_module() -> Any:
        candidates = [
            "agents.executor_agent",
            "executor_agent",
        ]

        last_error: Optional[Exception] = None

        for module_name in candidates:
            try:
                return importlib.import_module(module_name)
            except Exception as exc:
                last_error = exc

        raise ImportError(
            "Could not import executor_agent. Expected module "
            "'agents.executor_agent' or 'executor_agent'."
        ) from last_error

    def run_task(
        self,
        task_name: str,
        payload: Optional[Dict[str, Any]] = None,
    ) -> Any:
        if payload is None:
            payload = {}

        method_candidates = [
            "run_task",
            "execute_task",
            "execute",
            "run",
            "dispatch",
        ]

        for method_name in method_candidates:
            method = getattr(self.executor, method_name, None)

            if callable(method):
                return self._call_method(method, task_name, payload)

        raise AttributeError(
            "executor_agent does not expose a supported method. "
            "Expected one of: run_task, execute_task, execute, run, dispatch."
        )

    @staticmethod
    def _call_method(method: Any, task_name: str, payload: Dict[str, Any]) -> Any:
        call_patterns = [
            lambda: method(task_name=task_name, payload=payload),
            lambda: method(name=task_name, payload=payload),
            lambda: method(task_id=task_name, payload=payload),
            lambda: method(task_name, payload),
            lambda: method(task_name, **payload),
            lambda: method(task_name),
        ]

        last_error: Optional[Exception] = None

        for call in call_patterns:
            try:
                return call()
            except TypeError as exc:
                last_error = exc

        raise TypeError(
            f"Executor method exists but could not be called for task '{task_name}'."
        ) from last_error


class BriefingAgent:
    def __init__(
        self,
        config_path: Path = CONFIG_PATH,
        state_path: Path = STATE_PATH,
        log_path: Path = LOG_PATH,
        memory_dir: Path = BRIEFING_MEMORY_DIR,
    ) -> None:
        self.config_path = config_path
        self.state = MissionControlState(state_path)
        self.log_path = log_path
        self.memory_dir = memory_dir

        self.log_path.parent.mkdir(parents=True, exist_ok=True)
        self.memory_dir.mkdir(parents=True, exist_ok=True)

        self.available_tasks = self._load_available_tasks()
        self.executor = ExecutorAdapter(config_path=self.config_path)

    def generate(self, request: BriefingRequest) -> BriefingResult:
        request.validate()

        started_at = utc_now()
        task_name = self._resolve_task_name(request.briefing_type)

        self.state.mark_running(request, task_name)

        base_payload = {
            "briefing_type": request.briefing_type,
            "topic": request.topic,
            "user_context": request.user_context,
            "requested_at": started_at,
            "source_agent": "briefing_agent",
            "routing": {
                "entry_point": "jarvis",
                "agent": "briefing_agent",
                "executor_required": True,
                "direct_script_calls_allowed": False,
                "direct_llm_calls_allowed": False,
                "hermes_compatible": True,
            },
            "metadata": request.metadata,
        }

        if request.dry_run:
            result = BriefingResult(
                ok=True,
                briefing_type=request.briefing_type,
                task_name=task_name,
                started_at=started_at,
                finished_at=utc_now(),
                output={
                    "dry_run": True,
                    "resolved_task": task_name,
                    "refine_task": REFINE_TASK_NAME if request.refine else None,
                    "payload": base_payload,
                },
                metadata={
                    "mode": "dry_run",
                    "executor_used": False,
                    "refine_enabled": request.refine,
                },
            )

            self._persist_result(result)
            self.state.mark_finished(result)
            return result

        try:
            if task_name is None:
                raise RuntimeError(
                    f"No executor task found for briefing type "
                    f"'{request.briefing_type}'. Add one of these task IDs to "
                    f"{self.config_path}: {TASK_CANDIDATES[request.briefing_type]}"
                )

            worker_result = self.executor.run_task(task_name, base_payload)

            if not self._executor_result_ok(worker_result):
                result = BriefingResult(
                    ok=False,
                    briefing_type=request.briefing_type,
                    task_name=task_name,
                    started_at=started_at,
                    finished_at=utc_now(),
                    output={
                        "worker_result": worker_result,
                        "refined_result": None,
                    },
                    error=self._executor_error(worker_result),
                    metadata={
                        "executor_used": True,
                        "executor_task": task_name,
                        "refine_enabled": request.refine,
                        "refine_task": None,
                    },
                )
                self._persist_result(result)
                self.state.mark_finished(result)
                return result

            refined_result = None

            if request.refine and REFINE_TASK_NAME in self.available_tasks:
                refine_payload = {
                    "briefing_type": request.briefing_type,
                    "worker_result": worker_result,
                    "worker_stdout_json": self._safe_parse_worker_stdout(worker_result),
                    "base_payload": base_payload,
                }

                refined_result = self.executor.run_task(
                    REFINE_TASK_NAME,
                    refine_payload,
                )

            final_ok = self._executor_result_ok(worker_result)

            if refined_result is not None:
                final_ok = final_ok and self._executor_result_ok(refined_result)

            result = BriefingResult(
                ok=final_ok,
                briefing_type=request.briefing_type,
                task_name=task_name,
                started_at=started_at,
                finished_at=utc_now(),
                output={
                    "worker_result": worker_result,
                    "refined_result": refined_result,
                    "final_text": self._extract_final_text(worker_result, refined_result),
                },
                error=None if final_ok else self._executor_error(refined_result or worker_result),
                metadata={
                    "executor_used": True,
                    "executor_task": task_name,
                    "refine_enabled": request.refine,
                    "refine_task": REFINE_TASK_NAME if refined_result is not None else None,
                },
            )

        except Exception as exc:
            result = BriefingResult(
                ok=False,
                briefing_type=request.briefing_type,
                task_name=task_name,
                started_at=started_at,
                finished_at=utc_now(),
                output={
                    "traceback": traceback.format_exc(),
                },
                error=str(exc),
                metadata={
                    "executor_used": task_name is not None,
                    "executor_task": task_name,
                    "refine_enabled": request.refine,
                },
            )

        self._persist_result(result)
        self.state.mark_finished(result)

        return result

    def _resolve_task_name(self, briefing_type: str) -> Optional[str]:
        candidates = TASK_CANDIDATES.get(briefing_type, [])

        if not candidates:
            return None

        if not self.available_tasks:
            return candidates[0]

        for candidate in candidates:
            if candidate in self.available_tasks:
                return candidate

        return None

    def _load_available_tasks(self) -> set[str]:
        if not self.config_path.exists():
            return set()

        try:
            data = json.loads(self.config_path.read_text(encoding="utf-8"))
        except Exception:
            return set()

        tasks: set[str] = set()

        if isinstance(data, dict):
            if isinstance(data.get("tasks"), dict):
                tasks.update(data["tasks"].keys())

            elif isinstance(data.get("tasks"), list):
                for item in data["tasks"]:
                    if isinstance(item, dict):
                        for key in ("id", "name", "task_name"):
                            value = item.get(key)
                            if isinstance(value, str):
                                tasks.add(value)

            for key, value in data.items():
                if key != "tasks" and isinstance(value, dict):
                    tasks.add(key)

        elif isinstance(data, list):
            for item in data:
                if isinstance(item, dict):
                    for key in ("id", "name", "task_name"):
                        value = item.get(key)
                        if isinstance(value, str):
                            tasks.add(value)

        return tasks

    @staticmethod
    def _executor_result_ok(result: Any) -> bool:
        return isinstance(result, dict) and result.get("ok") is True

    @staticmethod
    def _executor_error(result: Any) -> Optional[str]:
        if isinstance(result, dict):
            return result.get("error") or result.get("message")
        return "Unknown executor error."

    @staticmethod
    def _safe_parse_worker_stdout(worker_result: Any) -> Optional[Dict[str, Any]]:
        if not isinstance(worker_result, dict):
            return None

        stdout = worker_result.get("stdout")

        if not isinstance(stdout, str) or not stdout.strip():
            return None

        try:
            parsed = json.loads(stdout)
        except Exception:
            return None

        if isinstance(parsed, dict):
            return parsed

        return None

    @staticmethod
    def _extract_final_text(worker_result: Any, refined_result: Any) -> str:
        if isinstance(refined_result, dict):
            stdout = refined_result.get("stdout")
            if isinstance(stdout, str) and stdout.strip():
                return stdout.strip()

        if isinstance(worker_result, dict):
            parsed = BriefingAgent._safe_parse_worker_stdout(worker_result)
            if parsed and isinstance(parsed.get("markdown"), str):
                return parsed["markdown"]

            stdout = worker_result.get("stdout")
            if isinstance(stdout, str):
                return stdout.strip()

        return ""

    def _persist_result(self, result: BriefingResult) -> None:
        payload = asdict(result)

        timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        memory_file = self.memory_dir / f"{timestamp}_{result.briefing_type}_briefing.json"

        memory_file.write_text(
            json.dumps(payload, indent=2, ensure_ascii=False, default=str),
            encoding="utf-8",
        )

        with self.log_path.open("a", encoding="utf-8") as file:
            file.write(json.dumps(payload, ensure_ascii=False, default=str))
            file.write("\n")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Jarvis Briefing Agent - executor-based briefing orchestrator"
    )

    parser.add_argument(
        "--type",
        choices=sorted(SUPPORTED_BRIEFING_TYPES),
        default="morning",
        help="Briefing type",
    )

    parser.add_argument(
        "--topic",
        default=None,
        help="Optional briefing topic or focus",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Optional user context",
    )

    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Resolve task and update state without executing",
    )

    parser.add_argument(
        "--no-refine",
        action="store_true",
        help="Skip LLM refinement step",
    )

    return parser


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    request = BriefingRequest(
        briefing_type=args.type,
        topic=args.topic,
        user_context=args.context,
        dry_run=args.dry_run,
        refine=not args.no_refine,
        metadata={
            "cli": True,
            "argv": argv if argv is not None else sys.argv[1:],
        },
    )

    agent = BriefingAgent()
    result = agent.generate(request)

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
