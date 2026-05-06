#!/usr/bin/env python3
"""
Jarvis Improvement Agent V1

Role:
    Analyzes system status, logs, recurring errors, and improvement opportunities.

Important:
    - Does NOT modify files directly
    - Does NOT commit or push
    - Does NOT execute subprocess
    - Does NOT call tools directly
    - Produces improvement proposals only
    - Human approval required before any change
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

STATE_FILE = MEMORY_DIR / "state.json"
IMPROVEMENT_LOG = LOG_DIR / "improvement_agent.log"


@dataclass
class ImprovementRequest:
    task: str
    scope: str = "system"
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ImprovementFinding:
    title: str
    severity: str
    evidence: str
    recommendation: str
    requires_approval: bool


@dataclass
class ImprovementPlan:
    improvement_type: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    findings: List[ImprovementFinding]
    steps: List[str]
    expected_outputs: List[str]
    safety_checks: List[str]


@dataclass
class ImprovementResult:
    ok: bool
    timestamp: str
    task: str
    plan: ImprovementPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def read_json_file(path: Path) -> Optional[Dict[str, Any]]:
    if not path.exists():
        return None

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(data, dict):
            return data
    except Exception:
        return None

    return None


def classify_improvement_type(task: str) -> str:
    text = task.lower()

    if any(word in text for word in ["log", "logs", "fehler", "error", "crash"]):
        return "log_analysis"

    if any(word in text for word in ["performance", "langsam", "speed"]):
        return "performance_review"

    if any(word in text for word in ["architektur", "struktur", "design"]):
        return "architecture_review"

    if any(word in text for word in ["code", "refactor", "qualität"]):
        return "code_quality_review"

    if any(word in text for word in ["agent", "neuer agent", "missing agent"]):
        return "agent_gap_analysis"

    return "general_improvement"


def detect_risk(task: str) -> tuple[str, bool, Optional[str]]:
    text = task.lower()

    high_risk_terms = [
        "ändern",
        "datei ändern",
        "löschen",
        "delete",
        "commit",
        "push",
        "deploy",
        "install",
        "update",
        "rewrite",
        "überschreiben",
    ]

    for term in high_risk_terms:
        if term in text:
            return "high", True, f"Improvement task contains change-sensitive term: {term}"

    return "medium", True, "Improvement proposals may lead to system changes and require approval."


def inspect_state() -> List[ImprovementFinding]:
    findings: List[ImprovementFinding] = []
    state = read_json_file(STATE_FILE)

    if state is None:
        findings.append(
            ImprovementFinding(
                title="Mission Control state missing or unreadable",
                severity="medium",
                evidence=f"{STATE_FILE} does not exist or is invalid JSON.",
                recommendation="Ensure all agents write status updates to memory/state.json.",
                requires_approval=False,
            )
        )
        return findings

    mission_control = state.get("mission_control", {})
    agents = state.get("agents", {})
    tasks = state.get("tasks", {})

    if not isinstance(mission_control, dict):
        findings.append(
            ImprovementFinding(
                title="Invalid mission_control structure",
                severity="medium",
                evidence="mission_control is missing or not an object.",
                recommendation="Normalize state.json schema.",
                requires_approval=True,
            )
        )

    if not isinstance(agents, dict) or not agents:
        findings.append(
            ImprovementFinding(
                title="No agent state recorded",
                severity="low",
                evidence="state.json contains no agent status information.",
                recommendation="Make sure each agent writes lifecycle status.",
                requires_approval=False,
            )
        )

    if not isinstance(tasks, dict):
        findings.append(
            ImprovementFinding(
                title="Invalid tasks structure",
                severity="medium",
                evidence="tasks field is missing or invalid.",
                recommendation="Normalize tasks section in state.json.",
                requires_approval=True,
            )
        )

    last_event = mission_control.get("last_event") if isinstance(mission_control, dict) else None
    if last_event is None:
        findings.append(
            ImprovementFinding(
                title="No last_event recorded",
                severity="low",
                evidence="mission_control.last_event is empty.",
                recommendation="Ensure Jarvis Core and agents write meaningful events.",
                requires_approval=False,
            )
        )

    return findings


def inspect_logs() -> List[ImprovementFinding]:
    findings: List[ImprovementFinding] = []

    if not LOG_DIR.exists():
        findings.append(
            ImprovementFinding(
                title="Logs directory missing",
                severity="medium",
                evidence=f"{LOG_DIR} does not exist.",
                recommendation="Ensure logging directory is created during setup.",
                requires_approval=False,
            )
        )
        return findings

    log_files = list(LOG_DIR.glob("*.log")) + list(LOG_DIR.glob("*.jsonl"))

    if not log_files:
        findings.append(
            ImprovementFinding(
                title="No log files found",
                severity="medium",
                evidence="logs/ contains no .log or .jsonl files.",
                recommendation="Ensure all agents log runs.",
                requires_approval=False,
            )
        )
        return findings

    for log_file in log_files:
        try:
            text = log_file.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue

        lower = text.lower()

        if "traceback" in lower or '"ok": false' in lower or "error" in lower:
            findings.append(
                ImprovementFinding(
                    title=f"Potential errors found in {log_file.name}",
                    severity="medium",
                    evidence="Log contains traceback, ok=false, or error markers.",
                    recommendation="Review this log and classify recurring failures.",
                    requires_approval=False,
                )
            )

    return findings


def build_plan(request: ImprovementRequest) -> ImprovementPlan:
    improvement_type = classify_improvement_type(request.task)
    risk_level, requires_approval, approval_reason = detect_risk(request.task)

    findings: List[ImprovementFinding] = []

    if request.scope in {"system", "state", "all"}:
        findings.extend(inspect_state())

    if request.scope in {"system", "logs", "all"}:
        findings.extend(inspect_logs())

    if not findings:
        findings.append(
            ImprovementFinding(
                title="No obvious issues found",
                severity="info",
                evidence="State and logs did not reveal immediate structural problems.",
                recommendation="Continue with planned roadmap: Mission Control UI, Hermes adapter, agent factory.",
                requires_approval=False,
            )
        )

    steps = [
        "Systemzustand und Logs lesen.",
        "Fehler, Lücken und wiederkehrende Muster identifizieren.",
        "Findings nach Schweregrad priorisieren.",
        "Verbesserungsvorschläge erstellen.",
        "Bei Code- oder Strukturänderungen Coding Agent / Cursor vorbereiten.",
        "Vor jeder Änderung explizite Freigabe einholen.",
        "Nach Umsetzung Tests und Review durchführen.",
        "Commit/Push nur nach separater Freigabe.",
    ]

    expected_outputs = [
        "Liste erkannter Probleme oder Lücken.",
        "Priorisierte Verbesserungsvorschläge.",
        "Risiko- und Freigabehinweise.",
        "Nächste empfohlene Schritte.",
        "Optional: Spezifikation für Coding Agent oder Agent Factory.",
    ]

    safety_checks = [
        "Keine Dateien ohne Freigabe ändern.",
        "Keine Commits oder Pushes ohne Freigabe.",
        "Keine Secrets in Logs oder Prompts kopieren.",
        "Fehler klar von Annahmen trennen.",
        "Vorschläge immer nachvollziehbar begründen.",
    ]

    return ImprovementPlan(
        improvement_type=improvement_type,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        findings=findings,
        steps=steps,
        expected_outputs=expected_outputs,
        safety_checks=safety_checks,
    )


def build_output(result: ImprovementResult) -> str:
    plan = result.plan

    findings = "\n".join(
        f"- [{finding.severity}] {finding.title}: {finding.recommendation}"
        for finding in plan.findings
    )

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    checks = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein."
    )

    return (
        "Improvement Agent Report\n\n"
        f"Improvement Type: {plan.improvement_type}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Findings:\n{findings}\n\n"
        f"Empfohlene Schritte:\n{steps}\n\n"
        f"Safety Checks:\n{checks}"
    )


def log_result(result: ImprovementResult) -> None:
    ensure_dirs()

    with IMPROVEMENT_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class ImprovementAgent:
    def handle(self, request: ImprovementRequest) -> ImprovementResult:
        try:
            plan = build_plan(request)

            result = ImprovementResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "improvement_agent",
                    "execution_performed": False,
                    "human_in_the_loop": True,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_finding = ImprovementFinding(
                title="Improvement agent failed",
                severity="high",
                evidence=str(exc),
                recommendation="Manual review required.",
                requires_approval=True,
            )

            fallback_plan = ImprovementPlan(
                improvement_type="unknown",
                risk_level="high",
                requires_approval=True,
                approval_reason="Improvement agent failed during analysis.",
                findings=[fallback_finding],
                steps=[],
                expected_outputs=[],
                safety_checks=["Manual review required."],
            )

            result = ImprovementResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Improvement analysis failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Improvement Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Improvement task",
    )

    parser.add_argument(
        "--scope",
        default="system",
        choices=["system", "state", "logs", "all"],
        help="Scope to inspect",
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
                    "error": "No improvement task provided.",
                    "example": "python3 agents/improvement_agent.py 'analysiere Logs und schlage Verbesserungen vor'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = ImprovementAgent()
    result = agent.handle(
        ImprovementRequest(
            task=task,
            scope=args.scope,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
