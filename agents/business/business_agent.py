#!/usr/bin/env python3
"""
Jarvis Business Agent V1

Role:
    Prepares business-related tasks for Jarvis.

Important:
    - Does NOT contact leads directly
    - Does NOT send emails
    - Does NOT modify CRM systems
    - Does NOT execute subprocess
    - Prepares structured business plans for Hermes / Executor / future tools
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

BUSINESS_LOG = LOG_DIR / "business_agent.log"


@dataclass
class BusinessRequest:
    task: str
    context: Optional[str] = None
    target_market: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class BusinessPlan:
    business_type: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    steps: List[str]
    expected_outputs: List[str]
    decision_questions: List[str]
    safety_checks: List[str]


@dataclass
class BusinessResult:
    ok: bool
    timestamp: str
    task: str
    plan: BusinessPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def classify_business_type(task: str) -> str:
    text = task.lower()

    if any(word in text for word in ["lead", "kunde", "kunden", "prospect"]):
        return "lead_research"

    if any(word in text for word in ["idee", "chance", "opportunity", "marktchance"]):
        return "opportunity_analysis"

    if any(word in text for word in ["wettbewerber", "competitor", "vergleich"]):
        return "competitive_analysis"

    if any(word in text for word in ["angebot", "proposal", "pitch"]):
        return "proposal_preparation"

    if any(word in text for word in ["strategie", "positionierung", "go-to-market"]):
        return "strategy"

    return "general_business"


def detect_risk(task: str) -> tuple[str, bool, Optional[str]]:
    text = task.lower()

    sensitive_terms = [
        "mail senden",
        "email senden",
        "kontakt aufnehmen",
        "anrufen",
        "crm ändern",
        "angebot senden",
        "vertrag",
        "preis zusagen",
        "zahlung",
        "rechnung",
    ]

    for term in sensitive_terms:
        if term in text:
            return "high", True, f"Business task contains approval-sensitive action: {term}"

    medium_terms = [
        "angebot",
        "preis",
        "kunde",
        "lead",
        "kontakt",
    ]

    for term in medium_terms:
        if term in text:
            return "medium", True, f"Business task may affect external communication or commercial positioning: {term}"

    return "low", False, None


def build_plan(request: BusinessRequest) -> BusinessPlan:
    business_type = classify_business_type(request.task)
    risk_level, requires_approval, approval_reason = detect_risk(request.task)

    steps = [
        "Business-Ziel und gewünschtes Ergebnis klären.",
        "Relevanten Markt, Kundentyp oder Use Case definieren.",
        "Annahmen, Chancen und Risiken getrennt erfassen.",
        "Falls Recherche nötig ist: Research Agent vorbereiten.",
        "Optionen bewerten nach Aufwand, Nutzen, Risiko und Zeithorizont.",
        "Konkrete nächste Schritte ableiten.",
        "Vor externer Kommunikation oder kommerziellen Zusagen Freigabe einholen.",
    ]

    expected_outputs = [
        "Kurz-Zusammenfassung der Business-Chance.",
        "Zielgruppe / Marktsegment.",
        "Nutzenversprechen.",
        "Risiken und offene Fragen.",
        "Priorisierte nächste Schritte.",
        "Entwurf für Mail / Angebot / Pitch nur als Vorschlag.",
    ]

    decision_questions = [
        "Ist das Ziel Umsatz, Lernen, Netzwerk oder Validierung?",
        "Welche Zielgruppe ist am wahrscheinlichsten erreichbar?",
        "Welche Annahme muss zuerst überprüft werden?",
        "Welcher nächste Schritt ist klein genug, um ihn sofort zu testen?",
        "Ist externe Kommunikation nötig oder reicht interne Vorbereitung?",
    ]

    safety_checks = [
        "Keine E-Mails ohne Freigabe senden.",
        "Keine CRM-Änderungen ohne Freigabe.",
        "Keine Preis- oder Vertragszusagen ohne Freigabe.",
        "Unsichere Marktannahmen klar als Annahmen markieren.",
        "Bei externen Quellen: Quellen und Aktualität dokumentieren.",
    ]

    return BusinessPlan(
        business_type=business_type,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        steps=steps,
        expected_outputs=expected_outputs,
        decision_questions=decision_questions,
        safety_checks=safety_checks,
    )


def build_output(result: BusinessResult) -> str:
    plan = result.plan

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    outputs = "\n".join(f"- {item}" for item in plan.expected_outputs)
    questions = "\n".join(f"- {question}" for question in plan.decision_questions)
    checks = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein, solange nur intern analysiert und vorbereitet wird."
    )

    return (
        "Business Agent Plan\n\n"
        f"Business Type: {plan.business_type}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Expected Outputs:\n{outputs}\n\n"
        f"Decision Questions:\n{questions}\n\n"
        f"Safety Checks:\n{checks}"
    )


def log_result(result: BusinessResult) -> None:
    ensure_dirs()

    with BUSINESS_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class BusinessAgent:
    def handle(self, request: BusinessRequest) -> BusinessResult:
        try:
            plan = build_plan(request)

            result = BusinessResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "business_agent",
                    "execution_performed": False,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = BusinessPlan(
                business_type="unknown",
                risk_level="medium",
                requires_approval=True,
                approval_reason="Business agent failed during planning.",
                steps=[],
                expected_outputs=[],
                decision_questions=[],
                safety_checks=["Manual review required."],
            )

            result = BusinessResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Business planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Business Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Business task",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Additional context",
    )

    parser.add_argument(
        "--market",
        default=None,
        help="Target market or segment",
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
                    "error": "No business task provided.",
                    "example": "python3 agents/business_agent.py 'prüfe eine neue Geschäftsidee für lokale AI-Agenten'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = BusinessAgent()
    result = agent.handle(
        BusinessRequest(
            task=task,
            context=args.context,
            target_market=args.market,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())

