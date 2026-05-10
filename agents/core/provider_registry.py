#!/usr/bin/env python3
"""
Provider Registry

Central registry for model/tool providers used by Hermes.

Jarvis = UI / runtime / control
Hermes = brain / router / delegation
Providers = Ollama, OpenRouter, OpenAI, Gemini, Codex, manual browser assist
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class Provider:
    name: str
    provider_type: str
    mode: str
    cost_profile: str
    automation_level: str
    use_for: list[str]
    avoid_for: list[str] = field(default_factory=list)
    requires_api_key: bool = False
    requires_manual_copy: bool = False
    command_hint: str | None = None
    notes: str = ""

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


PROVIDERS: dict[str, Provider] = {
    "ollama_small": Provider(
        name="ollama_small",
        provider_type="local_model",
        mode="automatic",
        cost_profile="free_local",
        automation_level="full",
        use_for=["simple_chat", "quick_tasks", "privacy_sensitive", "offline"],
        command_hint="ollama run llama3",
        notes="Local low-cost model for simple tasks.",
    ),
    "ollama_large": Provider(
        name="ollama_large",
        provider_type="local_model",
        mode="automatic",
        cost_profile="free_local_gpu",
        automation_level="full",
        use_for=["coding", "analysis", "medium_reasoning", "local_private_work"],
        command_hint="ollama run qwen2.5-coder:7b",
        notes="Local stronger model, preferably GPU.",
    ),
    "openrouter": Provider(
        name="openrouter",
        provider_type="cloud_api",
        mode="automatic",
        cost_profile="paid_usage",
        automation_level="full",
        use_for=["complex_reasoning", "fallback", "multi_model_access"],
        requires_api_key=True,
        notes="Paid external model router. Use only when needed.",
    ),
    "openai_api": Provider(
        name="openai_api",
        provider_type="cloud_api",
        mode="automatic",
        cost_profile="paid_usage",
        automation_level="full",
        use_for=["complex_reasoning", "coding", "tool_calling", "structured_outputs"],
        requires_api_key=True,
        notes="Official API path for OpenAI models.",
    ),
    "gemini_api": Provider(
        name="gemini_api",
        provider_type="cloud_api",
        mode="automatic",
        cost_profile="api_quota_or_paid",
        automation_level="full",
        use_for=["research", "long_context", "multimodal", "analysis"],
        requires_api_key=True,
        notes="Official Google Gemini API path.",
    ),
    "codex_cli": Provider(
        name="codex_cli",
        provider_type="coding_worker",
        mode="semi_automatic",
        cost_profile="included_or_plan_based",
        automation_level="human_supervised",
        use_for=["code_changes", "refactor", "tests", "debugging", "implementation"],
        avoid_for=["general_chat", "trading_decisions", "unapproved_file_delete"],
        command_hint="cd ~/jarvis && codex",
        notes="Coding worker for Jarvis development. Human reviews changes.",
    ),
    "chatgpt_manual": Provider(
        name="chatgpt_manual",
        provider_type="browser_manual_assist",
        mode="manual",
        cost_profile="free_or_subscription",
        automation_level="copy_paste",
        use_for=["complex_reasoning", "second_opinion", "architecture_review"],
        requires_manual_copy=True,
        notes="Hermes prepares prompt; user pastes into ChatGPT and returns answer.",
    ),
    "gemini_manual": Provider(
        name="gemini_manual",
        provider_type="browser_manual_assist",
        mode="manual",
        cost_profile="free_or_subscription",
        automation_level="copy_paste",
        use_for=["research", "long_context", "second_opinion"],
        requires_manual_copy=True,
        notes="Hermes prepares prompt; user pastes into Gemini and returns answer.",
    ),
    "copilot_manual": Provider(
        name="copilot_manual",
        provider_type="browser_manual_assist",
        mode="manual",
        cost_profile="free_or_subscription",
        automation_level="copy_paste",
        use_for=["coding_second_opinion", "microsoft_context", "quick_code_help"],
        requires_manual_copy=True,
        notes="Hermes prepares prompt; user pastes into Copilot and returns answer.",
    ),
}


def list_providers() -> dict[str, Any]:
    return {
        "timestamp": utc_now(),
        "providers": {
            name: provider.to_dict()
            for name, provider in PROVIDERS.items()
        },
    }


def recommend_provider(task: str, intent: str = "", cost_sensitive: bool = True) -> dict[str, Any]:
    text = f"{task} {intent}".lower()

    if any(term in text for term in ["refactor", "bug", "test", "implement", "code", "python", "ui_app.py"]):
        provider = PROVIDERS["codex_cli"]
        reason = "Coding/development task detected; Codex CLI is the preferred supervised coding worker."
    elif any(term in text for term in ["offline", "private", "lokal", "privacy"]):
        provider = PROVIDERS["ollama_large"]
        reason = "Local/private task detected; use Ollama."
    elif any(term in text for term in ["research", "long context", "quelle", "analyse"]):
        provider = PROVIDERS["gemini_manual"] if cost_sensitive else PROVIDERS["gemini_api"]
        reason = "Research/long-context task detected."
    elif any(term in text for term in ["architecture", "architektur", "masterplan", "complex", "strategie"]):
        provider = PROVIDERS["chatgpt_manual"] if cost_sensitive else PROVIDERS["openrouter"]
        reason = "Complex reasoning task detected."
    else:
        provider = PROVIDERS["ollama_small"]
        reason = "Simple task; local small model is sufficient."

    return {
        "ok": True,
        "task": task,
        "intent": intent,
        "cost_sensitive": cost_sensitive,
        "recommended_provider": provider.to_dict(),
        "reason": reason,
        "timestamp": utc_now(),
    }


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Jarvis Provider Registry")
    parser.add_argument("task", nargs="*", help="Task to route to provider")
    parser.add_argument("--list", action="store_true", help="List providers")
    parser.add_argument("--paid-ok", action="store_true", help="Allow paid API recommendation")

    args = parser.parse_args()

    if args.list:
        print(json.dumps(list_providers(), indent=2, ensure_ascii=False))
        return 0

    task = " ".join(args.task).strip()

    if not task:
        print(json.dumps(list_providers(), indent=2, ensure_ascii=False))
        return 0

    result = recommend_provider(
        task=task,
        cost_sensitive=not args.paid_ok,
    )

    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
