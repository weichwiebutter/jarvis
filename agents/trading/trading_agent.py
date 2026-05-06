#!/usr/bin/env python3
"""
Jarvis Trading Agent V1

Role:
    Prepares trading-related analysis tasks.

Important:
    - Does NOT place trades
    - Does NOT connect to brokers
    - Does NOT give financial advice
    - Does NOT execute subprocess
    - Prepares structured analysis plans for Hermes / Executor / future tools
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

TRADING_LOG = LOG_DIR / "trading_agent.log"


@dataclass
class TradingRequest:
    task: str
    market: Optional[str] = None
    timeframe: Optional[str] = None
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class TradingPlan:
    analysis_type: str
    risk_level: str
    requires_approval: bool
    approval_reason: Optional[str]
    steps: List[str]
    expected_outputs: List[str]
    decision_questions: List[str]
    safety_checks: List[str]


@dataclass
class TradingResult:
    ok: bool
    timestamp: str
    task: str
    plan: TradingPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def classify_analysis_type(task: str) -> str:
    text = task.lower()

    if any(word in text for word in ["briefing", "morgen", "morning", "marktbericht"]):
        return "market_briefing"

    if any(word in text for word in ["szenario", "scenario", "wenn", "if"]):
        return "scenario_analysis"

    if any(word in text for word in ["risk", "risiko", "verlust", "drawdown"]):
        return "risk_review"

    if any(word in text for word in ["watchlist", "beobachten", "screening"]):
        return "watchlist_preparation"

    if any(word in text for word in ["setup", "entry", "exit", "level"]):
        return "setup_review"

    return "general_market_analysis"


def detect_risk(task: str) -> tuple[str, bool, Optional[str]]:
    text = task.lower()

    forbidden_or_sensitive = [
        "order",
        "kaufen",
        "verkaufen",
        "buy",
        "sell",
        "broker",
        "position eröffnen",
        "position schließen",
        "trade ausführen",
        "stop loss setzen",
        "take profit setzen",
    ]

    for term in forbidden_or_sensitive:
        if term in text:
            return "high", True, f"Trading task contains action requiring explicit approval: {term}"

    medium_terms = [
        "entry",
        "exit",
        "positionsgröße",
        "hebel",
        "leverage",
        "signal",
    ]

    for term in medium_terms:
        if term in text:
            return "medium", True, f"Trading task may influence trading decision: {term}"

    return "low", False, None


def build_plan(request: TradingRequest) -> TradingPlan:
    analysis_type = classify_analysis_type(request.task)
    risk_level, requires_approval, approval_reason = detect_risk(request.task)

    steps = [
        "Analyseziel klären: Markt, Zeithorizont und gewünschte Entscheidungsvorbereitung.",
        "Relevante Datenquellen bestimmen: Preis, Volatilität, Makro, News, Sentiment.",
        "Strukturierte Marktanalyse vorbereiten.",
        "Szenarien formulieren: bullisch, bärisch, neutral.",
        "Risiken und Invalidierungsbedingungen markieren.",
        "Keine Order- oder Handlungsempfehlung ausgeben.",
        "Falls Ausführung gewünscht ist: explizite Nutzerfreigabe verlangen.",
    ]

    expected_outputs = [
        "Kurzüberblick.",
        "Marktkontext.",
        "Szenarien.",
        "Risiken.",
        "Invalidierungsbedingungen.",
        "Entscheidungsfragen.",
        "Disclaimer: Analyse, keine Anlageberatung.",
    ]

    decision_questions = [
        "Welcher Markt und welcher Zeithorizont sind relevant?",
        "Soll die Analyse nur vorbereiten oder eine bestehende Idee prüfen?",
        "Welche Daten fehlen für eine belastbare Einschätzung?",
        "Welche Annahme würde das Szenario ungültig machen?",
        "Ist vor einer Aktion eine explizite Freigabe nötig?",
    ]

    safety_checks = [
        "Keine automatische Orderausführung.",
        "Keine Broker-Verbindung ohne Freigabe.",
        "Keine Kauf- oder Verkaufsempfehlung.",
        "Datenunsicherheit klar markieren.",
        "Trading-bezogene Aussagen als Szenarien formulieren.",
    ]

    return TradingPlan(
        analysis_type=analysis_type,
        risk_level=risk_level,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        steps=steps,
        expected_outputs=expected_outputs,
        decision_questions=decision_questions,
        safety_checks=safety_checks,
    )


def build_output(result: TradingResult) -> str:
    plan = result.plan

    steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.steps))
    outputs = "\n".join(f"- {item}" for item in plan.expected_outputs)
    questions = "\n".join(f"- {question}" for question in plan.decision_questions)
    checks = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein, solange nur analysiert und vorbereitet wird."
    )

    return (
        "Trading Agent Plan\n\n"
        f"Analysis Type: {plan.analysis_type}\n"
        f"Risk Level: {plan.risk_level}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Expected Outputs:\n{outputs}\n\n"
        f"Decision Questions:\n{questions}\n\n"
        f"Safety Checks:\n{checks}"
    )


def log_result(result: TradingResult) -> None:
    ensure_dirs()

    with TRADING_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class TradingAgent:
    def handle(self, request: TradingRequest) -> TradingResult:
        try:
            plan = build_plan(request)

            result = TradingResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "trading_agent",
                    "execution_performed": False,
                    "financial_advice": False,
                    "hermes_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = TradingPlan(
                analysis_type="unknown",
                risk_level="high",
                requires_approval=True,
                approval_reason="Trading agent failed during planning.",
                steps=[],
                expected_outputs=[],
                decision_questions=[],
                safety_checks=["Manual review required.", "No trading action allowed."],
            )

            result = TradingResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Trading planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Trading Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Trading or market analysis task",
    )

    parser.add_argument(
        "--market",
        default=None,
        help="Market or instrument",
    )

    parser.add_argument(
        "--timeframe",
        default=None,
        help="Timeframe",
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
                    "error": "No trading task provided.",
                    "example": "python3 agents/trading_agent.py 'erstelle eine Szenarioanalyse für Gold'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = TradingAgent()
    result = agent.handle(
        TradingRequest(
            task=task,
            market=args.market,
            timeframe=args.timeframe,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
