#!/usr/bin/env python3
"""
Manual Assist Layer

Creates structured prompts for browser/manual providers.

Use cases:
- ChatGPT browser manual assist
- Gemini browser manual assist
- Copilot browser manual assist
- Codex CLI coding worker

Jarvis = UI/runtime/control
Hermes = brain/planner/delegation
Manual Assist = cost-saving human-supervised provider bridge
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class ManualAssistPrompt:
    ok: bool
    provider: str
    task: str
    title: str
    prompt: str
    instructions: list[str]
    expected_return: str
    requires_manual_copy: bool = True
    metadata: dict[str, Any] = field(default_factory=dict)
    timestamp: str = field(default_factory=utc_now)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def build_codex_prompt(task: str, context: dict[str, Any] | None = None) -> str:
    context = context or {}

    return f"""You are Codex working inside the Jarvis project repository.

Project rules:
- Jarvis is UI/runtime/control.
- Hermes is the brain/planner/delegation layer.
- Codex is a coding worker, not the system brain.
- Do not run git push.
- Do not edit secrets or config/settings.env.
- Do not modify runtime data in logs/, memory/, .hermes/, data/, obsidian/.
- Do not delete files unless explicitly instructed.
- Keep changes small and reviewable.
- Prefer complete-file fixes when needed.
- Run validation after Python edits.

Task:
{task}

Context:
{context}

Required validation:
python3 -m py_compile ui_app.py
python3 -m py_compile agents/core/*.py
python3 -m py_compile service/background_service.py

Return:
- changed files
- validation results
- short summary
- suggested git commands
"""


def build_chatgpt_prompt(task: str, context: dict[str, Any] | None = None) -> str:
    context = context or {}

    return f"""You are assisting with the Jarvis/Hermes AI assistant project.

Architecture:
- Jarvis = UI, voice, runtime, status, control.
- Hermes = brain, planner, learning agent, delegation layer.
- Ollama = local models.
- OpenRouter/OpenAI/Gemini = optional API providers.
- Codex = coding worker.
- All execution is approval-controlled.

Task:
{task}

Context:
{context}

Please provide:
1. concise analysis
2. recommended approach
3. risks
4. concrete next steps
5. if code is needed, provide complete files or exact patches only
"""


def build_gemini_prompt(task: str, context: dict[str, Any] | None = None) -> str:
    context = context or {}

    return f"""Analyze this Jarvis/Hermes project task with focus on long-context reasoning and alternatives.

System roles:
- Jarvis is the interface/runtime.
- Hermes is the planner/delegation brain.
- Specialist agents handle domain work.
- Executor performs approved actions only.

Task:
{task}

Context:
{context}

Return:
- architecture assessment
- alternative approaches
- implementation plan
- hidden risks
- recommended next step
"""


def build_copilot_prompt(task: str, context: dict[str, Any] | None = None) -> str:
    context = context or {}

    return f"""Help with a coding task in the Jarvis project.

Rules:
- Keep the current architecture intact.
- Do not introduce secrets.
- Avoid unnecessary dependencies.
- Provide small reviewable changes.
- Prefer Python 3.12 compatible code.

Task:
{task}

Context:
{context}

Return:
- proposed code changes
- files affected
- tests to run
"""


def build_manual_assist(
    provider: str,
    task: str,
    context: dict[str, Any] | None = None,
) -> dict[str, Any]:
    provider = provider.strip().lower()
    context = context or {}

    if provider == "codex_cli":
        prompt = build_codex_prompt(task, context)
        title = "Codex CLI Coding Task"
        instructions = [
            "Open a separate WSL terminal.",
            "Run: cd ~/jarvis",
            "Run: codex",
            "Paste the generated prompt into Codex.",
            "Review changed files before committing.",
        ]
        expected_return = "Codex summary, changed files, validation output, git status."

    elif provider == "chatgpt_manual":
        prompt = build_chatgpt_prompt(task, context)
        title = "ChatGPT Manual Assist"
        instructions = [
            "Open ChatGPT in the browser.",
            "Paste the generated prompt.",
            "Copy the response back into Jarvis/Hermes.",
        ]
        expected_return = "Analysis, recommendations, complete files or exact patches."

    elif provider == "gemini_manual":
        prompt = build_gemini_prompt(task, context)
        title = "Gemini Manual Assist"
        instructions = [
            "Open Gemini in the browser.",
            "Paste the generated prompt.",
            "Copy the response back into Jarvis/Hermes.",
        ]
        expected_return = "Architecture assessment, alternatives, risks, recommended plan."

    elif provider == "copilot_manual":
        prompt = build_copilot_prompt(task, context)
        title = "Copilot Manual Assist"
        instructions = [
            "Open Copilot in the browser or IDE.",
            "Paste the generated prompt.",
            "Copy the proposed changes back for review.",
        ]
        expected_return = "Coding suggestions, affected files, tests to run."

    else:
        return ManualAssistPrompt(
            ok=False,
            provider=provider,
            task=task,
            title="Unknown Provider",
            prompt="",
            instructions=[],
            expected_return="",
            metadata={
                "error": f"Unsupported manual provider: {provider}",
            },
        ).to_dict()

    return ManualAssistPrompt(
        ok=True,
        provider=provider,
        task=task,
        title=title,
        prompt=prompt,
        instructions=instructions,
        expected_return=expected_return,
        metadata={
            "source": "manual_assist",
            "human_in_the_loop": True,
            "context": context,
        },
    ).to_dict()


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Jarvis Manual Assist Layer")
    parser.add_argument("task", nargs="*", help="Task for manual assist")
    parser.add_argument(
        "--provider",
        default="chatgpt_manual",
        choices=[
            "chatgpt_manual",
            "gemini_manual",
            "copilot_manual",
            "codex_cli",
        ],
        help="Manual provider",
    )

    args = parser.parse_args()
    task = " ".join(args.task).strip()

    if not task:
        print("Kein Task angegeben.")
        return 1

    result = build_manual_assist(
        provider=args.provider,
        task=task,
        context={
            "project": "Jarvis/Hermes",
            "mode": "manual_assist",
        },
    )

    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
