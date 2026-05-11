#!/usr/bin/env python3
"""
Model Registry

Defines known model choices for Hermes provider routing.

Jarvis = UI/runtime/control
Hermes = brain/router/delegation
Providers = Ollama, OpenRouter, OpenAI, Gemini, Codex/manual assist
"""

from __future__ import annotations

from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from typing import Any


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class ModelProfile:
    name: str
    provider: str
    model_id: str
    model_type: str
    cost_profile: str
    strengths: list[str]
    avoid_for: list[str] = field(default_factory=list)
    local: bool = False
    requires_api_key: bool = False
    notes: str = ""

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


MODELS: dict[str, ModelProfile] = {
    "local_small": ModelProfile(
        name="local_small",
        provider="ollama_small",
        model_id="llama3",
        model_type="general",
        cost_profile="free_local",
        strengths=["simple_chat", "status", "quick_tasks"],
        local=True,
        notes="Default small local model.",
    ),
    "local_large": ModelProfile(
        name="local_large",
        provider="ollama_large",
        model_id="qwen2.5-coder:7b",
        model_type="coding_reasoning",
        cost_profile="free_local_gpu",
        strengths=["coding", "analysis", "medium_reasoning", "private_work"],
        local=True,
        notes="Preferred stronger local model for GPU machines.",
    ),
    "codex_worker": ModelProfile(
        name="codex_worker",
        provider="codex_cli",
        model_id="codex_cli",
        model_type="coding_worker",
        cost_profile="included_or_plan_based",
        strengths=["implementation", "refactor", "tests", "debugging"],
        avoid_for=["general_chat", "trading_decisions"],
        notes="Human-supervised coding worker.",
    ),
    "chatgpt_manual_reasoning": ModelProfile(
        name="chatgpt_manual_reasoning",
        provider="chatgpt_manual",
        model_id="chatgpt_browser",
        model_type="manual_reasoning",
        cost_profile="free_or_subscription",
        strengths=["architecture", "planning", "second_opinion", "complex_reasoning"],
        notes="Manual browser assist; no scraping.",
    ),
    "gemini_manual_long_context": ModelProfile(
        name="gemini_manual_long_context",
        provider="gemini_manual",
        model_id="gemini_browser",
        model_type="manual_long_context",
        cost_profile="free_or_subscription",
        strengths=["long_context", "research", "alternatives", "multimodal_review"],
        notes="Manual browser assist; no scraping.",
    ),
    "copilot_manual_code": ModelProfile(
        name="copilot_manual_code",
        provider="copilot_manual",
        model_id="copilot_browser_or_ide",
        model_type="manual_code_assist",
        cost_profile="free_or_subscription",
        strengths=["code_second_opinion", "microsoft_context", "quick_code_help"],
        notes="Manual Copilot assist.",
    ),
    "openrouter_reasoning": ModelProfile(
        name="openrouter_reasoning",
        provider="openrouter",
        model_id="openrouter_best_reasoning",
        model_type="cloud_reasoning",
        cost_profile="paid_usage",
        strengths=["complex_reasoning", "fallback", "large_planning"],
        requires_api_key=True,
        notes="Paid fallback only when approved.",
    ),
    "openai_api_reasoning": ModelProfile(
        name="openai_api_reasoning",
        provider="openai_api",
        model_id="openai_reasoning",
        model_type="cloud_reasoning",
        cost_profile="paid_usage",
        strengths=["tool_calling", "structured_outputs", "complex_reasoning"],
        requires_api_key=True,
        notes="Official API route if key exists.",
    ),
    "gemini_api_long_context": ModelProfile(
        name="gemini_api_long_context",
        provider="gemini_api",
        model_id="gemini_api_model",
        model_type="cloud_long_context",
        cost_profile="quota_or_paid",
        strengths=["long_context", "research", "multimodal", "analysis"],
        requires_api_key=True,
        notes="Official Gemini API route if key exists.",
    ),
}


def list_models() -> dict[str, Any]:
    return {
        "timestamp": utc_now(),
        "models": {name: model.to_dict() for name, model in MODELS.items()},
    }


def recommend_model(
    task: str,
    intent: str = "",
    route: str = "",
    cost_sensitive: bool = True,
    offline: bool = False,
) -> dict[str, Any]:
    text = f"{task} {intent} {route}".lower()

    if offline:
        model = MODELS["local_large"] if any(x in text for x in ["code", "analysis", "planning"]) else MODELS["local_small"]
        reason = "Offline mode; local model required."

    elif any(x in text for x in ["codex", "implement", "refactor", "debug", "py_compile", "test", "code"]):
        model = MODELS["codex_worker"]
        reason = "Coding implementation task; Codex worker preferred."

    elif any(x in text for x in ["long context", "research", "quelle", "alternativen", "multimodal"]):
        model = MODELS["gemini_manual_long_context"] if cost_sensitive else MODELS["gemini_api_long_context"]
        reason = "Research/long-context task."

    elif any(x in text for x in ["architektur", "masterplan", "strategie", "complex", "planning", "entscheidung"]):
        model = MODELS["chatgpt_manual_reasoning"] if cost_sensitive else MODELS["openrouter_reasoning"]
        reason = "Complex planning/reasoning task."

    elif any(x in text for x in ["privacy", "lokal", "private", "offline"]):
        model = MODELS["local_large"]
        reason = "Privacy/local preference detected."

    else:
        model = MODELS["local_small"]
        reason = "Simple task; small local model sufficient."

    return {
        "ok": True,
        "task": task,
        "intent": intent,
        "route": route,
        "cost_sensitive": cost_sensitive,
        "offline": offline,
        "recommended_model": model.to_dict(),
        "reason": reason,
        "timestamp": utc_now(),
    }


def main() -> int:
    import argparse
    import json

    parser = argparse.ArgumentParser(description="Jarvis Model Registry")
    parser.add_argument("task", nargs="*", help="Task to recommend model for")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--paid-ok", action="store_true")
    parser.add_argument("--offline", action="store_true")

    args = parser.parse_args()

    if args.list:
        print(json.dumps(list_models(), indent=2, ensure_ascii=False))
        return 0

    task = " ".join(args.task).strip()

    if not task:
        print(json.dumps(list_models(), indent=2, ensure_ascii=False))
        return 0

    result = recommend_model(
        task=task,
        cost_sensitive=not args.paid_ok,
        offline=args.offline,
    )

    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
