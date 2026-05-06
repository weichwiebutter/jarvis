#!/usr/bin/env python3
"""
OpenRouter Adapter for Jarvis

Role:
    Provides access to external LLMs via OpenRouter API.

Important:
    - Stateless usage
    - No hard dependency in agents
    - Called ONLY via Executor
"""

from __future__ import annotations

import json
import os
import urllib.request


OPENROUTER_API_URL = "https://openrouter.ai/api/v1/chat/completions"


def run_openrouter(prompt: str, model: str = "openai/gpt-4o-mini") -> dict:
    api_key = os.environ.get("OPENROUTER_API_KEY")

    if not api_key:
        return {
            "ok": False,
            "error": "Missing OPENROUTER_API_KEY"
        }

    payload = {
        "model": model,
        "messages": [
            {"role": "user", "content": prompt}
        ],
    }

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }

    try:
        req = urllib.request.Request(
            OPENROUTER_API_URL,
            data=json.dumps(payload).encode("utf-8"),
            headers=headers,
            method="POST",
        )

        with urllib.request.urlopen(req, timeout=60) as resp:
            data = json.loads(resp.read().decode("utf-8"))

        content = data["choices"][0]["message"]["content"]

        return {
            "ok": True,
            "model": model,
            "output": content.strip(),
        }

    except Exception as e:
        return {
            "ok": False,
            "error": str(e),
        }
