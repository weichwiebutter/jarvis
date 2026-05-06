#!/usr/bin/env python3
"""
Jarvis Agent Factory Agent V1

Role:
    Detects when Jarvis needs a new specialized agent and prepares a specification.

Important:
    - Does NOT create files directly
    - Does NOT call Cursor, OpenCode, Gemini, Ollama, Claude, or any fixed tool directly
    - Does NOT execute subprocess
    - Does NOT modify the system
    - Produces agent specifications only
    - Implementation must later happen through approved Executor/tool-adapter flow

Design Principle:
    Jarvis is tool-agnostic and model-agnostic.
    The Agent Factory describes WHAT should exist.
    Executor / tool adapters decide HOW it is created.
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
AGENTS_DIR = PROJECT_ROOT / "agents"

FACTORY_LOG = LOG_DIR / "agent_factory_agent.log"


KNOWN_AGENT_FILES = {
    "coding_agent.py": "coding",
    "research_agent.py": "research",
    "business_agent.py": "business",
    "office_agent.py": "office",
    "trading_agent.py": "trading",
    "improvement_agent.py": "improvement",
    "briefing_agent.py": "briefing",
    "executor_agent.py": "executor",
    "jarvis_core.py": "core",
    "hermes_adapter.py": "planner",
}


@dataclass
class AgentFactoryRequest:
    task: str
    domain_hint: Optional[str] = None
    context: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ExistingAgent:
    name: str
    domain: str
    path: str
    exists: bool


@dataclass
class ProposedAgent:
    name: str
    filename: str
    domain: str
    purpose: str
    responsibilities: List[str]
    non_responsibilities: List[str]
    inputs: List[str]
    outputs: List[str]
    memory_needs: List[str]
    tools_needed: List[str]
    autonomy_level: int
    approval_required_for: List[str]
    test_plan: List[str]


@dataclass
class AgentFactoryPlan:
    factory_type: str
    existing_agents: List[ExistingAgent]
    gap_detected: bool
    proposed_agent: Optional[ProposedAgent]
    requires_approval: bool
    approval_reason: Optional[str]
    next_steps: List[str]
    safety_checks: List[str]


@dataclass
class AgentFactoryResult:
    ok: bool
    timestamp: str
    task: str
    plan: AgentFactoryPlan
    output: str
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    AGENTS_DIR.mkdir(parents=True, exist_ok=True)


def normalize(text: str) -> str:
    return text.strip().lower()


def inspect_existing_agents() -> List[ExistingAgent]:
    ensure_dirs()

    agents: List[ExistingAgent] = []

    for filename, domain in KNOWN_AGENT_FILES.items():
        path = AGENTS_DIR / filename
        agents.append(
            ExistingAgent(
                name=filename.replace(".py", ""),
                domain=domain,
                path=str(path),
                exists=path.exists(),
            )
        )

    return agents


def infer_domain(task: str, domain_hint: Optional[str]) -> str:
    if domain_hint:
        return normalize(domain_hint).replace(" ", "_")

    text = normalize(task)

    mappings = {
        "voice": ["voice", "sprache", "whisper", "tts", "mikrofon", "wake word"],
        "memory": ["memory", "gedächtnis", "obsidian", "wissen", "präferenz", "lernen"],
        "ui": ["ui", "dashboard", "mission control", "oberfläche", "interface"],
        "scheduler": ["scheduler", "automation", "automatisierung", "cron", "task scheduler"],
        "tool_adapter": ["tool", "adapter", "cursor", "opencode", "gemini", "ollama", "api"],
        "agent_factory": ["agent erstellen", "neuer agent", "agent factory"],
        "security": ["security", "sicherheit", "freigabe", "approval", "rechte"],
    }

    for domain, keywords in mappings.items():
        if any(keyword in text for keyword in keywords):
            return domain

    return "custom"


def existing_agent_for_domain(existing_agents: List[ExistingAgent], domain: str) -> Optional[ExistingAgent]:
    for agent in existing_agents:
        if agent.domain == domain and agent.exists:
            return agent

    return None


def build_agent_name(domain: str) -> tuple[str, str]:
    clean = domain.strip().lower().replace("-", "_").replace(" ", "_")

    if not clean.endswith("_agent"):
        name = f"{clean}_agent"
    else:
        name = clean

    filename = f"{name}.py"
    return name, filename


def propose_agent(task: str, domain: str) -> ProposedAgent:
    name, filename = build_agent_name(domain)

    purpose = (
        f"Specialized Jarvis agent for the '{domain}' domain. "
        "It prepares structured plans and recommendations without executing actions directly."
    )

    responsibilities = [
        "Understand requests in its domain.",
        "Prepare structured plans.",
        "Identify required data, memory, and tools.",
        "Mark approval-sensitive actions.",
        "Return JSON-compatible results to Jarvis Core / Hermes.",
        "Write logs for traceability.",
    ]

    non_responsibilities = [
        "No direct subprocess calls.",
        "No direct file modifications.",
        "No direct Git operations.",
        "No direct LLM or API calls unless explicitly routed through Executor/tool adapters.",
        "No autonomous high-risk actions.",
    ]

    inputs = [
        "task",
        "context",
        "metadata",
        "optional memory/profile data",
    ]

    outputs = [
        "structured plan",
        "risk assessment",
        "approval requirements",
        "recommended next steps",
        "test plan",
    ]

    memory_needs = [
        "memory/state.json for system state",
        "logs for prior outcomes",
        "future Obsidian vault notes for long-term knowledge",
        "future preferences/profile for user-specific behavior",
    ]

    tools_needed = [
        "none directly",
        "future Executor tool adapters as needed",
    ]

    approval_required_for = [
        "file creation",
        "file modification",
        "Git commit",
        "Git push",
        "external communication",
        "paid API usage",
        "deletion or overwrite",
    ]

    test_plan = [
        f"Run: python3 agents/{filename} 'test request'",
        "Verify JSON output.",
        "Verify no subprocess usage.",
        "Verify no direct tool calls.",
        "Verify approval-sensitive requests are marked.",
        "Verify log file is written.",
    ]

    return ProposedAgent(
        name=name,
        filename=filename,
        domain=domain,
        purpose=purpose,
        responsibilities=responsibilities,
        non_responsibilities=non_responsibilities,
        inputs=inputs,
        outputs=outputs,
        memory_needs=memory_needs,
        tools_needed=tools_needed,
        autonomy_level=0,
        approval_required_for=approval_required_for,
        test_plan=test_plan,
    )


def build_plan(request: AgentFactoryRequest) -> AgentFactoryPlan:
    existing_agents = inspect_existing_agents()
    domain = infer_domain(request.task, request.domain_hint)

    existing = existing_agent_for_domain(existing_agents, domain)

    if existing:
        gap_detected = False
        proposed = None
        next_steps = [
            f"Existing agent found for domain '{domain}': {existing.name}.",
            "Route request to existing agent through Jarvis Core / Hermes.",
            "Do not create a new agent unless the existing agent proves insufficient.",
        ]
        requires_approval = False
        approval_reason = None

    else:
        gap_detected = True
        proposed = propose_agent(request.task, domain)
        next_steps = [
            f"No existing agent found for domain '{domain}'.",
            "Review proposed agent specification.",
            "If approved, pass specification to Coding Agent.",
            "Implementation may use any approved tool adapter: Cursor, OpenCode, local model, cloud model, or future system.",
            "Executor writes files only after explicit approval.",
            "Run tests.",
            "Commit/push only after separate approval.",
        ]
        requires_approval = True
        approval_reason = "Creating a new agent requires user approval."

    safety_checks = [
        "Agent Factory does not write files.",
        "Agent Factory does not choose a fixed coding tool.",
        "Jarvis remains model-agnostic and tool-agnostic.",
        "New agents must follow no-direct-execution rules.",
        "Autonomy level starts at 0 until explicitly raised.",
        "Human-in-the-loop remains required for system changes.",
    ]

    return AgentFactoryPlan(
        factory_type="agent_gap_analysis",
        existing_agents=existing_agents,
        gap_detected=gap_detected,
        proposed_agent=proposed,
        requires_approval=requires_approval,
        approval_reason=approval_reason,
        next_steps=next_steps,
        safety_checks=safety_checks,
    )


def build_output(result: AgentFactoryResult) -> str:
    plan = result.plan

    existing = "\n".join(
        f"- {agent.name} ({agent.domain}): {'exists' if agent.exists else 'missing'}"
        for agent in plan.existing_agents
    )

    proposed = "None"

    if plan.proposed_agent:
        agent = plan.proposed_agent
        proposed = (
            f"Name: {agent.name}\n"
            f"Filename: {agent.filename}\n"
            f"Domain: {agent.domain}\n"
            f"Purpose: {agent.purpose}\n"
            f"Autonomy Level: {agent.autonomy_level}\n"
            f"Responsibilities:\n"
            + "\n".join(f"- {item}" for item in agent.responsibilities)
        )

    next_steps = "\n".join(f"{idx + 1}. {step}" for idx, step in enumerate(plan.next_steps))
    safety = "\n".join(f"- {check}" for check in plan.safety_checks)

    approval = (
        f"Ja. Grund: {plan.approval_reason}"
        if plan.requires_approval
        else "Nein."
    )

    return (
        "Agent Factory Report\n\n"
        f"Factory Type: {plan.factory_type}\n"
        f"Gap Detected: {plan.gap_detected}\n"
        f"Freigabe nötig: {approval}\n\n"
        f"Existing Agents:\n{existing}\n\n"
        f"Proposed Agent:\n{proposed}\n\n"
        f"Next Steps:\n{next_steps}\n\n"
        f"Safety Checks:\n{safety}"
    )


def log_result(result: AgentFactoryResult) -> None:
    ensure_dirs()

    with FACTORY_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


class AgentFactoryAgent:
    def handle(self, request: AgentFactoryRequest) -> AgentFactoryResult:
        try:
            plan = build_plan(request)

            result = AgentFactoryResult(
                ok=True,
                timestamp=utc_now(),
                task=request.task,
                plan=plan,
                output="",
                metadata={
                    "source": "agent_factory_agent",
                    "execution_performed": False,
                    "tool_agnostic": True,
                    "model_agnostic": True,
                    "hermes_ready": True,
                    "human_in_the_loop": True,
                },
            )

            result.output = build_output(result)

        except Exception as exc:
            fallback_plan = AgentFactoryPlan(
                factory_type="failed",
                existing_agents=[],
                gap_detected=False,
                proposed_agent=None,
                requires_approval=True,
                approval_reason="Agent Factory failed.",
                next_steps=["Manual review required."],
                safety_checks=["Do not create or modify files automatically."],
            )

            result = AgentFactoryResult(
                ok=False,
                timestamp=utc_now(),
                task=request.task,
                plan=fallback_plan,
                output="Agent Factory failed.",
                error=str(exc),
            )

        log_result(result)
        return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Agent Factory Agent V1")

    parser.add_argument(
        "task",
        nargs="*",
        help="Task or missing capability to analyze",
    )

    parser.add_argument(
        "--domain",
        default=None,
        help="Optional domain hint for proposed agent",
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
                    "error": "No task provided.",
                    "example": "python3 agents/agent_factory_agent.py 'Wir brauchen einen Voice Agent für Whisper und TTS'",
                },
                indent=2,
                ensure_ascii=False,
            )
        )
        return 1

    agent = AgentFactoryAgent()
    result = agent.handle(
        AgentFactoryRequest(
            task=task,
            domain_hint=args.domain,
            context=args.context,
            metadata={"cli": True},
        )
    )

    print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
