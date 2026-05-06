#!/usr/bin/env python3
"""
Jarvis Office Agent V1

Role:
    Prepares office, organization, note, planning, and assistant tasks.

Important:
    - Does NOT send emails
    - Does NOT modify calendars
    - Does NOT delete files
    - Does NOT execute subprocess
    - Prepares structured plans for Hermes / Executor / future tools
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

OFFICE_LOG = LOG_DIR / "office_agent.log"


@dataclass
class OfficeRequest:
    task: str
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class OfficePlan:
    office_type: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    steps: List[str]
    expected_outputs: List[str]
    decision_questions: List[str]
    safety_checks: List[str]


@dataclass
class OfficeResult:
    ok: bool
    timestamp: str
    task: str
    plan: OfficePlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def classify_office_type(task: str) -> str:
    text = task.lower()

    if any(word in text for word in ["todo", "aufgabe", "task", "liste"]):
        return "task_management"

    if any(word in text for word in ["termin", "kalender", "meeting", "besprechung"]):
        return "calendar_planning"

    if any(word in text for word in ["mail", "email", "antwort", "entwurf"]):
        return "email_preparation"

    if any(word in text for word in ["notiz", "zusammenfassung", "protokoll"]):
        return "notes_and_summary"

    if any(word in text for word in ["plan", "planung", "struktur", "priorität"]):
        return "planning"

    return "general_office"


def detect_risk(task: str) -> tuple[str, bool, Optional[str]]:
    text = task.lower()

    high_risk_terms = [
        "mail senden",
        "email senden",
        "termin erstellen",
        "kalender ändern",
        "löschen",
        "delete",
        "absagen",
        "cancel",
        "verschicken",
    ]

    for term in high_risk_terms:
        if term in text:
            return "high", True, f"Office task contains approval-sensitive action: {term}"

    medium_risk_terms = [
        "mail",
        "email",
        "kalender",
        "termin",
        "extern",
    ]

    for term in medium_risk_terms:
        if term in text:
            return "medium", True, f"Office task may affect communication or schedule: {term}"

    return "low", False, None


def build_plan(request: OfficeRequest) -> OfficePlan:
    office_type = classify_office_type(request.task)
    risk_level, requires_approval, approval_reason = detect_risk(request.task)

    steps = [
        "Auftrag und gewünschtes Ergebnis klären.",
        "Relevante Informationen aus Kontext oder Memory sammeln.",
        "Aufgabe strukturieren und priorisieren.",
        "Entwurf, Liste oder Plan vorbereiten.",
        "Offene Fragen markieren.",
        "Vor externen Aktionen Freigabe einholen.",
    ]

    expected_outputs = [
        "Kurze Zusammenfassung.",
        "Strukturierter Plan oder Entwurf.",
        "Priorisierte Aufgabenliste.",
        "Offene Fragen.",
        "Nächster konkreter Schritt.",
    ]

    decision_questions = [
        "Soll das Ergebnis kurz oder ausführlich sein?",
        "Ist die Aufgabe intern oder extern relevant?",
        "Gibt es eine Deadline?",
        "Soll Jarvis nur vorbereiten oder später auch ausführen?",
    ]

    safety_checks = [
        "Keine E-Mails ohne Freigabe senden.",
        "Keine Kalendereinträge ohne Freigabe ändern.",
        "Keine Dateien löschen.",
        "Externe Kommunikation immer als Entwurf vorbereiten.",
        "Unsicherheiten klar markieren.",
    ]

    return OfficePlan(
        office_type=office_type,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        steps=steps,
        expected_outputs=expected_outputs,
        decision_questions=decision_questions,
        safety_checks=safety_checks,
    )


def build_output(result: OfficeResult) -> str:
    plan = result.plan

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    outputs = "\n".join(f"- {item}" for item in plan.expected_outputs)
    questions = "\n".join(f"- {question}" for question in plan.decision_questions)
    checks = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein, solange nur vorbereitet oder strukturiert wird."
    )

    return (
        "Office Agent Plan\n\n"
        f"Office Type: {plan.office_type}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Expected Outputs:\n{outputs}\n\n"
        f"Decision Questions:\n{questions}\n\n"
        f"Safety Checks:\n{checks}"
    )


def log_result(result: OfficeResult) -> None:
    ensure_dirs()

    with OFFICE_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class OfficeAgent:
    def handle(self, request: OfficeRequest) -> OfficeResult:
        try:
            plan = build_plan(request)

            result = OfficeResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "office_agent",
                    "execution_performed": False,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = OfficePlan(
                office_type="unknown",
                risk_level="medium",
                requires_approval=True,
                approval_reason="Office agent failed during planning.",
                steps=[],
                expected_outputs=[],
                decision_questions=[],
                safety_checks=["Manual review required."],
            )

            result = OfficeResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Office planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Office Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Office task",
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
                    "error": "No office task provided.",
                    "example": "python3 agents/office_agent.py 'erstelle mir eine priorisierte Aufgabenliste für heute'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = OfficeAgent()
    result = agent.handle(
        OfficeRequest(
            task=task,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
