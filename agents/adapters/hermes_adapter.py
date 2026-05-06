#!/usr/bin/env python3
"""
Jarvis Hermes Adapter V2

Hermes is the planning and coordination brain behind Jarvis.

Role:
    - Receive user request from Jarvis Core
    - Load relevant memory context
    - Classify domain
    - Build multi-step plan
    - Select specialized agent
    - Identify approval requirements
    - Return structured plan to Jarvis

Important:
    - Hermes does NOT execute tasks directly
    - Hermes does NOT call subprocess
    - Hermes does NOT write files
    - Hermes does NOT perform Git actions
    - Hermes prepares plans only
    - Executor remains the only execution layer
"""

from __future__ import annotations

import argparse
import importlib
import json
import sys
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


LOG_DIR = PROJECT_ROOT / "logs"
HERMES_LOG = LOG_DIR / "hermes_adapter.log"


DOMAIN_AGENT_MAP = {
    "memory": "memory_agent",
    "coding": "coding_agent",
    "research": "research_agent",
    "business": "business_agent",
    "office": "office_agent",
    "trading": "trading_agent",
    "improvement": "improvement_agent",
    "system": "system_agent",
}


DOMAIN_KEYWORDS = {
    "memory": [
        "merk dir",
        "merke dir",
        "speichere",
        "remember",
        "notiere",
        "was weißt du",
        "was weisst du",
        "memory",
        "gedächtnis",
        "obsidian",
    ],
    "coding": [
        "code",
        "coding",
        "python",
        "script",
        "bug",
        "refactor",
        "github",
        "commit",
        "push",
        "datei",
        "opencode",
        "cursor",
    ],
    "research": [
        "research",
        "recherche",
        "quelle",
        "quellen",
        "reddit",
        "news",
        "paper",
        "studie",
        "trend",
    ],
    "business": [
        "business",
        "geschäft",
        "idee",
        "chance",
        "lead",
        "kunde",
        "angebot",
        "strategie",
        "marktchance",
    ],
    "office": [
        "todo",
        "notiz",
        "planung",
        "termin",
        "kalender",
        "mail",
        "organisieren",
        "zusammenfassen",
    ],
    "trading": [
        "trading",
        "trade",
        "markt",
        "börse",
        "gold",
        "forex",
        "xauusd",
        "aktie",
        "szenario",
        "bias",
        "risiko",
    ],
    "improvement": [
        "verbessern",
        "optimieren",
        "self improvement",
        "logs",
        "fehler",
        "review",
        "qualität",
    ],
    "system": [
        "system",
        "status",
        "backup",
        "recovery",
        "setup",
        "diagnose",
        "health",
    ],
}


SENSITIVE_ACTIONS = [
    "löschen",
    "delete",
    "commit",
    "push",
    "deploy",
    "installieren",
    "update",
    "überschreiben",
    "senden",
    "mail senden",
    "kaufen",
    "verkaufen",
    "order",
    "broker",
    "bezahlen",
    "api key",
    "secret",
]


@dataclass
class HermesRequest:
    user_input: str
    domain: Optional[str] = None
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class HermesMemoryContext:
    loaded: bool
    query: Optional[str]
    entries: List[Dict[str, Any]]
    error: Optional[str] = None


@dataclass
class HermesStep:
    step_id: int
    title: str
    agent: str
    action: str
    requires_approval: bool
    approval_reason: Optional[str] = None


@dataclass
class HermesPlan:
    objective: str
    domain: str
    primary_agent: str
    memory_context: HermesMemoryContext
    steps: List[HermesStep]
    approval_required: bool
    approval_summary: Optional[str]
    execution_allowed: bool
    notes: List[str]


@dataclass
class HermesResult:
    ok: bool
    timestamp: str
    request: str
    plan: HermesPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def normalize(text: str) -> str:
    return text.strip().lower()


def detect_domain(user_input: str, forced_domain: Optional[str] = None) -> str:
    if forced_domain:
        return forced_domain.strip().lower()

    text = normalize(user_input)

    best_domain = "office"
    best_hits = 0

    for domain, keywords in DOMAIN_KEYWORDS.items():
        hits = sum(1 for keyword in keywords if keyword in text)

        if hits > best_hits:
            best_domain = domain
            best_hits = hits

    return best_domain


def detect_sensitive_action(user_input: str) -> tuple[bool, Optional[str]]:
    text = normalize(user_input)

    for action in SENSITIVE_ACTIONS:
        if action in text:
            return True, f"Sensible Aktion erkannt: {action}"

    return False, None


def extract_memory_query(user_input: str) -> Optional[str]:
    text = normalize(user_input)

    important_terms = [
        "voice",
        "sprache",
        "whisper",
        "tts",
        "obsidian",
        "memory",
        "gedächtnis",
        "jarvis",
        "hermes",
        "github",
        "git",
        "coding",
        "code",
        "briefing",
        "openrouter",
        "api",
    ]

    found = [term for term in important_terms if term in text]

    if found:
        return found[0]

    words = [
        word.strip(".,:;!?")
        for word in user_input.split()
        if len(word.strip(".,:;!?")) >= 4
    ]

    if not words:
        return None

    return words[-1]


def load_memory_context(user_input: str) -> HermesMemoryContext:
    query = extract_memory_query(user_input)

    try:
        executor_module = importlib.import_module("agents.executor_agent")
        run_task = getattr(executor_module, "run_task")

        payload: Dict[str, Any] = {
            "limit": 8,
        }

        if query:
            payload["query"] = query

        result = run_task(
            "memory_read",
            payload=payload,
            confirmed=True,
        )

        if not isinstance(result, dict) or not result.get("ok"):
            return HermesMemoryContext(
                loaded=False,
                query=query,
                entries=[],
                error=str(result.get("error") if isinstance(result, dict) else result),
            )

        memory_read = result.get("memory_read", {})
        entries = memory_read.get("entries", [])

        if not isinstance(entries, list):
            entries = []

        return HermesMemoryContext(
            loaded=True,
            query=query,
            entries=entries,
            error=None,
        )

    except Exception as exc:
        return HermesMemoryContext(
            loaded=False,
            query=query,
            entries=[],
            error=str(exc),
        )


def build_steps(
    request: HermesRequest,
    domain: str,
    primary_agent: str,
    memory_context: HermesMemoryContext,
) -> List[HermesStep]:
    sensitive, sensitive_reason = detect_sensitive_action(request.user_input)

    steps: List[HermesStep] = [
        HermesStep(
            step_id=1,
            title="Auftrag erfassen",
            agent="jarvis_core",
            action="User-Auftrag entgegennehmen und an Hermes übergeben.",
            requires_approval=False,
        ),
        HermesStep(
            step_id=2,
            title="Kontext laden",
            agent="hermes_adapter",
            action=(
                "Relevante Memory-Einträge laden."
                if memory_context.loaded
                else "Memory-Kontext konnte nicht geladen werden oder ist leer."
            ),
            requires_approval=False,
        ),
        HermesStep(
            step_id=3,
            title="Domäne bestimmen",
            agent="hermes_adapter",
            action=f"Auftrag wurde der Domäne '{domain}' zugeordnet.",
            requires_approval=False,
        ),
        HermesStep(
            step_id=4,
            title="Spezialagent auswählen",
            agent="hermes_adapter",
            action=f"Primärer Agent: {primary_agent}.",
            requires_approval=False,
        ),
        HermesStep(
            step_id=5,
            title="Fachliche Bearbeitung vorbereiten",
            agent=primary_agent,
            action="Spezialagent erstellt Plan, Analyse oder Executor-Envelope.",
            requires_approval=False,
        ),
        HermesStep(
            step_id=6,
            title="Ergebnis präsentieren",
            agent="jarvis_core",
            action="Jarvis präsentiert Plan, Ergebnis, Risiken und offene Entscheidungen.",
            requires_approval=False,
        ),
    ]

    if sensitive:
        steps.append(
            HermesStep(
                step_id=7,
                title="Freigabe einholen",
                agent="jarvis_core",
                action="Vor sensibler Aktion explizite Nutzerfreigabe einholen.",
                requires_approval=True,
                approval_reason=sensitive_reason,
            )
        )

    return steps


def build_plan(request: HermesRequest) -> HermesPlan:
    domain = detect_domain(request.user_input, request.domain)
    primary_agent = DOMAIN_AGENT_MAP.get(domain, "office_agent")
    memory_context = load_memory_context(request.user_input)

    steps = build_steps(
        request=request,
        domain=domain,
        primary_agent=primary_agent,
        memory_context=memory_context,
    )

    approval_steps = [step for step in steps if step.requires_approval]
    approval_required = bool(approval_steps)

    approval_summary = None
    if approval_required:
        approval_summary = "; ".join(
            step.approval_reason or step.title for step in approval_steps
        )

    notes = [
        "Jarvis bleibt die Schnittstelle zum Nutzer.",
        "Hermes ist die Planungs- und Koordinationsschicht.",
        "Spezialagenten liefern Fachlogik.",
        "Executor bleibt die einzige Ausführungsschicht.",
        "Memory wird von Hermes als Kontext genutzt, nicht von Jarvis Core hart verdrahtet.",
        "Tools und Modelle bleiben austauschbar.",
    ]

    return HermesPlan(
        objective=request.user_input,
        domain=domain,
        primary_agent=primary_agent,
        memory_context=memory_context,
        steps=steps,
        approval_required=approval_required,
        approval_summary=approval_summary,
        execution_allowed=not approval_required,
        notes=notes,
    )


def build_output(result: HermesResult) -> str:
    plan = result.plan

    memory_summary = (
        f"geladen: {len(plan.memory_context.entries)} Einträge"
        if plan.memory_context.loaded
        else f"nicht geladen: {plan.memory_context.error}"
    )

    steps = "\n".join(
        f"{step.step_id}. {step.title} → {step.agent}: {step.action}"
        + (f" [Freigabe: {step.approval_reason}]" if step.requires_approval else "")
        for step in plan.steps
    )

    approval = (
        f"Ja. {plan.approval_summary}"
        if plan.approval_required
        else "Nein, zunächst nur Planung / Vorbereitung."
    )

    notes = "\n".join(f"- {note}" for note in plan.notes)

    return (
        "Hermes Plan\n\n"
        f"Ziel: {plan.objective}\n"
        f"Domäne: {plan.domain}\n"
        f"Primärer Agent: {plan.primary_agent}\n"
        f"Memory-Kontext: {memory_summary}\n"
        f"Freigabe nötig: {approval}\n"
        f"Ausführung erlaubt: {plan.execution_allowed}\n\n"
        f"Schritte:\n{steps}\n\n"
        f"Notizen:\n{notes}"
    )


def log_result(result: HermesResult) -> None:
    ensure_dirs()

    with HERMES_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class HermesAdapter:
    def plan(self, request: HermesRequest) -> HermesResult:
        try:
            plan = build_plan(request)

            result = HermesResult(
                ok=True,
                timestamp=utc_now(),
                request=request.user_input,
                plan=plan,
                output="",
                metadata={
                    "source": "hermes_adapter",
                    "version": "v2",
                    "execution_performed": False,
                    "memory_used": plan.memory_context.loaded,
                    "memory_entries": len(plan.memory_context.entries),
                    "real_hermes_integrated": False,
                    "interface_ready": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_memory = HermesMemoryContext(
                loaded=False,
                query=None,
                entries=[],
                error=str(exc),
            )

            fallback_step = HermesStep(
                step_id=1,
                title="Fehler",
                agent="hermes_adapter",
                action="Planung fehlgeschlagen. Jarvis soll Nutzer informieren.",
                requires_approval=True,
                approval_reason="Hermes planning failed.",
            )

            fallback_plan = HermesPlan(
                objective=request.user_input,
                domain="unknown",
                primary_agent="jarvis_core",
                memory_context=fallback_memory,
                steps=[fallback_step],
                approval_required=True,
                approval_summary="Hermes planning failed.",
                execution_allowed=False,
                notes=["Manual review required."],
            )

            result = HermesResult(
                ok=False,
                timestamp=utc_now(),
                request=request.user_input,
                plan=fallback_plan,
                output="Hermes planning failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Hermes Adapter V2")

    parser.add_argument(
        "request",
        nargs="*",
        help="User request to plan",
    )

    parser.add_argument(
        "--domain",
        default=None,
        help="Optional forced domain",
    )

    parser.add_argument(
        "--context",
        default=None,
        help="Optional context",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    user_input = " ".join(args.request).strip()

    if not user_input:
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": "No request provided.",
                    "example": "python3 agents/hermes_adapter.py 'Plane Voice Interface mit Memory Kontext'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    adapter = HermesAdapter()
    result = adapter.plan(
        HermesRequest(
            user_input=user_input,
            domain=args.domain,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
