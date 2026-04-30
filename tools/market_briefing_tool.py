#!/usr/bin/env python3
"""
Jarvis Market Briefing Tool

Runs the existing market data and market briefing scripts.
The new external agent mode "midday" is mapped to the existing script mode
"ny_preopen" to remain backward compatible with the current repository.
"""

from __future__ import annotations

import subprocess
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Any, Dict


@dataclass
class MarketBriefingResult:
    ok: bool
    requested_mode: str
    script_mode: str
    report_path: str | None
    stdout: str
    stderr: str
    returncode: int
    message: str

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


def expand_path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def get_mode_config(config: Dict[str, Any], mode: str) -> Dict[str, Any]:
    modes = config.get("briefings", {}).get("modes", {})
    if mode not in modes:
        valid = ", ".join(sorted(modes.keys())) or "morning, midday"
        raise ValueError(f"Unknown briefing mode '{mode}'. Valid modes: {valid}")
    return modes[mode]


def latest_report(output_dir: Path, mode: str, script_mode: str) -> Path | None:
    today = datetime.now().strftime("%Y-%m-%d")
    candidates: list[Path] = []

    if mode == "morning":
        candidates.extend(output_dir.glob(f"{today}_Morning.md"))
    elif mode == "midday":
        candidates.extend(output_dir.glob(f"{today}_NY.md"))
        candidates.extend(output_dir.glob(f"{today}_Midday.md"))

    candidates.extend(output_dir.glob(f"{today}*.md"))

    if not candidates:
        return None
    return max(candidates, key=lambda path: path.stat().st_mtime)


def run_command(command: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        cwd=str(cwd),
        text=True,
        capture_output=True,
        timeout=300,
        check=False,
    )


def run_market_briefing(config: Dict[str, Any], mode: str) -> MarketBriefingResult:
    project_root = expand_path(config.get("system", {}).get("project_root", "~/jarvis"))
    briefings_cfg = config.get("briefings", {})
    mode_cfg = get_mode_config(config, mode)

    market_data_script = project_root / briefings_cfg.get(
        "market_data_script", "scripts/market_data.py"
    )
    briefing_script = project_root / briefings_cfg.get(
        "briefing_script", "scripts/market_briefing.py"
    )
    output_dir = expand_path(briefings_cfg.get("output_dir", "~/jarvis/obsidian/MarketBriefings"))
    output_dir.mkdir(parents=True, exist_ok=True)

    script_mode = mode_cfg.get("briefing_script_mode", mode)

    if not market_data_script.exists():
        return MarketBriefingResult(
            ok=False,
            requested_mode=mode,
            script_mode=script_mode,
            report_path=None,
            stdout="",
            stderr="",
            returncode=2,
            message=f"Market data script not found: {market_data_script}",
        )

    if not briefing_script.exists():
        return MarketBriefingResult(
            ok=False,
            requested_mode=mode,
            script_mode=script_mode,
            report_path=None,
            stdout="",
            stderr="",
            returncode=2,
            message=f"Briefing script not found: {briefing_script}",
        )

    data_run = run_command(["python", str(market_data_script)], project_root)
    if data_run.returncode != 0:
        return MarketBriefingResult(
            ok=False,
            requested_mode=mode,
            script_mode=script_mode,
            report_path=None,
            stdout=data_run.stdout,
            stderr=data_run.stderr,
            returncode=data_run.returncode,
            message="Market data update failed.",
        )

    briefing_run = run_command(
        ["python", str(briefing_script), "--mode", script_mode], project_root
    )
    report = latest_report(output_dir, mode, script_mode)

    ok = briefing_run.returncode == 0 and report is not None
    return MarketBriefingResult(
        ok=ok,
        requested_mode=mode,
        script_mode=script_mode,
        report_path=str(report) if report else None,
        stdout=(data_run.stdout + "\n" + briefing_run.stdout).strip(),
        stderr=(data_run.stderr + "\n" + briefing_run.stderr).strip(),
        returncode=briefing_run.returncode,
        message="Briefing created successfully." if ok else "Briefing run finished without a report file.",
    )
