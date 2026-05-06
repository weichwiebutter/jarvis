#!/usr/bin/env python3
"""
Jarvis Coding Agent V1

Role:
    Prepares coding tasks for Jarvis.

Important:
    - Does NOT modify files directly
    - Does NOT call Cursor directly
    - Does NOT commit or push
    - Does NOT run subprocess
    - Prepares structured coding plans for Executor / Cursor / future Hermes workflow
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]
MEMORY_DIR = PROJECT_ROOT / "memory"
LOG_DIR = PROJECT_ROOT / "logs"

CODING_LOG = LOG_DIR / "coding_agent.log"


@dataclass
class CodingRequest:
    task: str
    target_files: List[str] = field(default_factory=list)
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class CodingPlan:
    intent: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    recommended_tool: str
    steps: List[str]
    expected_outputs: List[str]
    safety_checks: List[str]


@dataclass
class CodingResult:
    ok: bool
    timestamp: str
    task: str
    plan: CodingPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def classify_intent(task: str) -> str:
    text = task.lower()

    if any(word in text for word in ["bug", "fix", "fehler", "reparieren"]):
        return "bugfix"

    if any(word in text for word in ["refactor", "aufräumen", "struktur"]):
        return "refactor"

    if any(word in text for word in ["neu", "erstellen", "agent", "modul", "file"]):
        return "create"

    if any(word in text for word in ["test", "pytest", "prüfen"]):
        return "test"

    if any(word in text for word in ["review", "prüfe", "kontrolliere"]):
        return "review"

    return "general_coding"


def detect_risk(task: str) -> tuple[str, bool, Optional[str]]:
    text = task.lower()

    high_risk_terms = [
        "delete",
        "löschen",
        "overwrite",
        "überschreiben",
        "commit",
        "push",
        "deploy",
        "production",
        "secret",
        ".env",
        "token",
        "api key",
    ]

    for term in high_risk_terms:
        if term in text:
            return "high", True, f"Task contains sensitive action or term: {term}"

    medium_risk_terms = [
        "ändern",
        "change",
        "modify",
        "replace",
        "ersetzen",
        "refactor",
    ]

    for term in medium_risk_terms:
        if term in text:
            return "medium", True, f"Task may modify existing code: {term}"

    return "low", False, None


def choose_tool(intent: str) -> str:
    if intent in {"create", "refactor", "bugfix"}:
        return "cursor_or_opencode_via_executor"

    if intent == "review":
        return "static_review_first"

    if intent == "test":
        return "executor_test_task"

    return "coding_agent_plan_only"


def build_plan(request: CodingRequest) -> CodingPlan:
    intent = classify_intent(request.task)
    risk_level, requires_approval, approval_reason = detect_risk(request.task)
    recommended_tool = choose_tool(intent)

    steps = [
        "Auftrag in technische Teilaufgaben zerlegen.",
        "Betroffene Dateien und Systemregeln prüfen.",
        "Architekturregeln anwenden: keine direkten Tool-/Script-Aufrufe aus Agents.",
        "Änderungsvorschlag erstellen.",
        "User-Freigabe vor Dateiänderungen einholen.",
        "Nach Freigabe Umsetzung über Cursor/OpenCode oder Executor vorbereiten.",
        "Tests und Review durchführen.",
        "Commit/Push nur nach expliziter Freigabe.",
    ]

    expected_outputs = [
        "Klare technische Spezifikation.",
        "Liste betroffener Dateien.",
        "Risiko- und Freigabehinweis.",
        "Umsetzungsvorschlag oder Cursor-Auftrag.",
        "Testplan.",
    ]

    safety_checks = [
        "Keine Secrets in Code oder Logs.",
        "Keine automatischen Commits.",
        "Keine automatischen Pushes.",
        "Keine produktiven Änderungen ohne Freigabe.",
        "Komplette Dateien statt Snippets, wenn Code erzeugt wird.",
    ]

    return CodingPlan(
        intent=intent,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        recommended_tool=recommended_tool,
        steps=steps,
        expected_outputs=expected_outputs,
        safety_checks=safety_checks,
    )


def build_output(result: CodingResult) -> str:
    plan = result.plan

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    checks = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein, solange nur geplant oder geprüft wird."
    )

    return (
        f"Coding Agent Plan\n\n"
        f"Intent: {plan.intent}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Recommended Tool: {plan.recommended_tool}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Safety Checks:\n{checks}"
    )


def log_result(result: CodingResult) -> None:
    ensure_dirs()

    with CODING_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class CodingAgent:
    def handle(self, request: CodingRequest) -> CodingResult:
        try:
            plan = build_plan(request)

            result = CodingResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "coding_agent",
                    "execution_performed": False,
                    "cursor_ready": True,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = CodingPlan(
                intent="unknown",
                risk_level="high",
                requires_approval=True,
                approval_reason="Coding agent failed during planning.",
                recommended_tool="manual_review",
                steps=[],
                expected_outputs=[],
                safety_checks=["Manual review required."],
            )

            result = CodingResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Coding planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Coding Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Coding task to prepare",
    )

    parser.add_argument(
        "--file",
        action="append",
        default=[],
        help="Target file. Can be used multiple times.",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Additional context",
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
                    "error": "No coding task provided.",
                    "example": "python3 agents/coding_agent.py 'erstelle einen neuen research agent'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = CodingAgent()
    result = agent.handle(
        CodingRequest(
            task=task,
            target_files=args.file,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
