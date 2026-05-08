#!/usr/bin/env python3
"""
Agent Creation Request

Structured request for creating a new specialist agent when Hermes detects
that no existing agent capability is sufficient.

Important:
- Hermes may propose new agents.
- Hermes must not write files directly.
- Agent creation always requires approval.
- Actual file creation happens later through CodingAgent/Executor.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class AgentCreationRequest:
    ok: bool
    proposed_agent_name: str
    proposed_domain: str
    reason: str
    responsibilities: list[str]
    non_responsibilities: list[str]
    suggested_module_path: str
    suggested_class_name: str
    required_capabilities: list[str]
    required_tools: list[str] = field(default_factory=list)
    requires_approval: bool = True
    approval_reason: str = "Creating a new agent changes the system architecture."
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def normalize_agent_name(raw: str) -> str:
    value = raw.strip().lower()
    replacements = {
        " ": "_",
        "-": "_",
        ".": "_",
        "/": "_",
    }

    for source, target in replacements.items():
        value = value.replace(source, target)

    value = "".join(char for char in value if char.isalnum() or char == "_")

    if not value.endswith("_agent"):
        value = f"{value}_agent"

    return value


def class_name_from_agent_name(agent_name: str) -> str:
    parts = agent_name.replace("_agent", "").split("_")
    return "".join(part.capitalize() for part in parts if part) + "Agent"


def infer_agent_request(task: str) -> dict[str, Any]:
    text = task.strip()
    lower = text.lower()

    domain = "custom"
    agent_base = "custom"

    responsibilities = [
        "Understand tasks in its specialized domain.",
        "Prepare structured plans and recommendations.",
        "Return JSON-compatible results.",
        "Mark approval-sensitive actions.",
        "Write logs for traceability.",
    ]

    required_capabilities = [
        "task_understanding",
        "structured_planning",
        "safe_output_generation",
    ]

    required_tools: list[str] = []

    if any(x in lower for x in ["voice", "sprache", "mikrofon", "tts", "whisper", "wake word", "stimme", "audio"]):
        domain = "voice"
        agent_base = "voice"
        responsibilities.extend([
            "Plan and manage voice input/output workflows.",
            "Handle STT/TTS integration planning.",
            "Coordinate wake-word and audio visualization features.",
        ])
        required_capabilities.extend([
            "voice_input_planning",
            "tts_planning",
            "stt_planning",
            "wake_word_planning",
            "audio_ui_planning",
        ])
        required_tools.extend([
            "whisper",
            "edge_tts",
            "browser_microphone",
        ])

    elif any(x in lower for x in ["reddit", "subreddit", "sentiment", "social"]):
        domain = "social_research"
        agent_base = "social_research"
        responsibilities.extend([
            "Plan social media research workflows.",
            "Handle Reddit API research planning.",
            "Respect API rate limits and content policies.",
        ])
        required_capabilities.extend([
            "reddit_api_planning",
            "sentiment_research",
            "source_filtering",
            "rate_limit_handling",
        ])
        required_tools.extend([
            "reddit_api",
            "cache",
        ])

    elif any(x in lower for x in ["ui", "oberfläche", "glass", "neon", "orb", "dashboard", "gradio"]):
        domain = "ui"
        agent_base = "ui"
        responsibilities.extend([
            "Plan UI/UX improvements.",
            "Prepare dashboard and interface specifications.",
            "Coordinate visual layout, status panels, and user controls.",
        ])
        required_capabilities.extend([
            "ui_planning",
            "dashboard_design",
            "gradio_ui_planning",
            "visual_system_design",
        ])
        required_tools.extend([
            "gradio",
            "html_css",
        ])

    elif any(x in lower for x in ["home assistant", "smarthome", "iot", "licht", "sensor"]):
        domain = "home_automation"
        agent_base = "home_automation"
        responsibilities.extend([
            "Plan home automation integrations.",
            "Prepare safe command routing for smart devices.",
            "Respect manual approval for physical-world actions.",
        ])
        required_capabilities.extend([
            "home_assistant_planning",
            "iot_action_planning",
            "physical_action_safety",
        ])
        required_tools.extend([
            "home_assistant_api",
        ])

    proposed_agent_name = normalize_agent_name(agent_base)
    suggested_class_name = class_name_from_agent_name(proposed_agent_name)

    request = AgentCreationRequest(
        ok=True,
        proposed_agent_name=proposed_agent_name,
        proposed_domain=domain,
        reason=f"No existing agent matched the requested capability: {text}",
        responsibilities=responsibilities,
        non_responsibilities=[
            "Do not execute shell commands directly.",
            "Do not write files directly.",
            "Do not push to Git.",
            "Do not call paid APIs without explicit approval.",
            "Do not bypass Executor or approval policies.",
        ],
        suggested_module_path=f"agents/{domain}/{proposed_agent_name}.py",
        suggested_class_name=suggested_class_name,
        required_capabilities=sorted(set(required_capabilities)),
        required_tools=sorted(set(required_tools)),
        metadata={
            "source": "agent_creation_request",
            "original_task": text,
            "human_in_the_loop": True,
        },
    )

    return request.to_dict()


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Agent Creation Request")
    parser.add_argument("task", nargs="*", help="Task requiring a new agent")
    args = parser.parse_args()

    task = " ".join(args.task).strip()

    if not task:
        print("Kein Task angegeben.")
        return 1

    result = infer_agent_request(task)
    print(json.dumps(result, indent=2, ensure_ascii=False))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
