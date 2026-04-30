#!/usr/bin/env python3
"""
Jarvis Briefing Autopilot Agent

Purpose:
- Run autonomous scheduled market briefings.
- Keep Jarvis local-first.
- Bridge the current Intent -> Tool architecture toward Goal -> Plan -> Tool chain -> Validation -> Memory.

Usage:
  python agents/briefing_agent.py --mode morning
  python agents/briefing_agent.py --mode midday
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Any, Dict

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools.market_briefing_tool import run_market_briefing
from tools.report_reader_tool import summarize_report
from tools.system_status_tool import start_backend


DEFAULT_CONFIG_PATH = ROOT / "config" / "jarvis.yaml"


def expand_path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def parse_scalar(value: str) -> Any:
    value = value.strip()
    if value.startswith('"') and value.endswith('"'):
        return value[1:-1]
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1]
    if value.lower() == "true":
        return True
    if value.lower() == "false":
        return False
    if value.lower() in {"null", "none"}:
        return None
    try:
        return int(value)
    except ValueError:
        return value


def load_simple_yaml(path: Path) -> Dict[str, Any]:
    """
    Minimal YAML reader for Jarvis config.
    Supports the simple nested key/value structure used in config/jarvis.yaml.
    Avoids adding PyYAML as a dependency.
    """
    root: Dict[str, Any] = {}
    stack: list[tuple[int, Dict[str, Any] | list[Any]]] = [(-1, root)]

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        if not raw_line.strip() or raw_line.lstrip().startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip(" "))
        line = raw_line.strip()

        while stack and indent <= stack[-1][0]:
            stack.pop()

        parent = stack[-1][1]

        if line.startswith("- "):
            item = parse_scalar(line[2:])
            if isinstance(parent, list):
                parent.append(item)
            continue

        if ":" not in line:
            continue

        key, value = line.split(":", 1)
        key = key.strip()
        value = value.strip()

        if not isinstance(parent, dict):
            continue

        if value == "":
            new_container: Dict[str, Any] | list[Any]
            new_container = [] if key in {"require_confirmation_for"} else {}
            parent[key] = new_container
            stack.append((indent, new_container))
        else:
            parent[key] = parse_scalar(value)

    return root


def load_config(config_path: Path) -> Dict[str, Any]:
    if not config_path.exists():
        raise FileNotFoundError(f"Config not found: {config_path}")
    return load_simple_yaml(config_path)


def ensure_runtime_dirs(config: Dict[str, Any]) -> None:
    paths = [
        config.get("logging", {}).get("run_log", "~/jarvis/logs/agent_runs.jsonl"),
        config.get("logging", {}).get("openjarvis_log", "~/jarvis/logs/openjarvis.log"),
        config.get("memory", {}).get("preferences_file", "~/jarvis/memory/preferences.json"),
        config.get("memory", {}).get("last_runs_file", "~/jarvis/memory/last_runs.json"),
    ]
    for value in paths:
        path = expand_path(value)
        path.parent.mkdir(parents=True, exist_ok=True)


def read_json_file(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return default


def write_json_file(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def append_run_log(config: Dict[str, Any], event: Dict[str, Any]) -> None:
    run_log = expand_path(config.get("logging", {}).get("run_log", "~/jarvis/logs/agent_runs.jsonl"))
    run_log.parent.mkdir(parents=True, exist_ok=True)
    with open(run_log, "a", encoding="utf-8") as handle:
        handle.write(json.dumps(event, ensure_ascii=False) + "\n")


def update_last_runs(config: Dict[str, Any], mode: str, event: Dict[str, Any]) -> None:
    last_runs_path = expand_path(
        config.get("memory", {}).get("last_runs_file", "~/jarvis/memory/last_runs.json")
    )
    data = read_json_file(last_runs_path, {})
    data[mode] = {
        "timestamp": event.get("finished_at"),
        "ok": event.get("ok"),
        "report_path": event.get("report_path"),
        "summary": event.get("summary"),
        "message": event.get("message"),
    }
    write_json_file(last_runs_path, data)


def run_agent(mode: str, config_path: Path) -> int:
    started_at = datetime.now().isoformat(timespec="seconds")
    config = load_config(config_path)
    ensure_runtime_dirs(config)

    event: Dict[str, Any] = {
        "agent": "briefing_autopilot",
        "mode": mode,
        "started_at": started_at,
        "finished_at": None,
        "ok": False,
        "report_path": None,
        "summary": None,
        "steps": [],
        "message": None,
    }

    try:
        backend_result = start_backend(config)
        event["steps"].append({"name": "backend_status", **backend_result.to_dict()})
        if not backend_result.ok:
            event["message"] = backend_result.message
            return_code = 10
        else:
            briefing_result = run_market_briefing(config, mode)
            event["steps"].append({"name": "market_briefing", **briefing_result.to_dict()})
            event["report_path"] = briefing_result.report_path

            if not briefing_result.ok:
                event["message"] = briefing_result.message
                return_code = 20
            else:
                summary_result = summarize_report(config, briefing_result.report_path)
                event["steps"].append({"name": "report_summary", **summary_result.to_dict()})
                event["summary"] = summary_result.summary
                event["ok"] = True
                event["message"] = "Briefing autopilot completed successfully."
                return_code = 0

    except Exception as exc:
        event["message"] = "Briefing autopilot failed with an unhandled exception."
        event["error"] = str(exc)
        return_code = 99
    finally:
        event["finished_at"] = datetime.now().isoformat(timespec="seconds")
        append_run_log(config, event)
        update_last_runs(config, mode, event)

    print(json.dumps(event, ensure_ascii=False, indent=2))
    return return_code


def main() -> int:
    parser = argparse.ArgumentParser(description="Jarvis Briefing Autopilot Agent")
    parser.add_argument("--mode", choices=["morning", "midday"], required=True)
    parser.add_argument(
        "--config",
        default=str(DEFAULT_CONFIG_PATH),
        help="Path to Jarvis config YAML.",
    )
    args = parser.parse_args()
    return run_agent(args.mode, Path(args.config).expanduser().resolve())


if __name__ == "__main__":
    raise SystemExit(main())
