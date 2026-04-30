#!/usr/bin/env python3
"""
Jarvis Report Reader Tool

Finds the latest report and creates a short speaking-friendly summary via the
local OpenJarvis chat endpoint. If the local LLM call fails, it returns a safe
fallback summary instead of blocking the autopilot run.
"""

from __future__ import annotations

from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any, Dict

import requests


@dataclass
class ReportSummaryResult:
    ok: bool
    report_path: str | None
    summary: str
    message: str
    error: str | None = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


def summarize_report(config: Dict[str, Any], report_path: str | None) -> ReportSummaryResult:
    if not report_path:
        return ReportSummaryResult(
            ok=False,
            report_path=None,
            summary="Kein Bericht gefunden.",
            message="No report path provided.",
        )

    path = Path(report_path).expanduser().resolve()
    if not path.exists():
        return ReportSummaryResult(
            ok=False,
            report_path=str(path),
            summary="Der Bericht wurde nicht gefunden.",
            message="Report file does not exist.",
        )

    content = path.read_text(encoding="utf-8", errors="replace")
    backend_cfg = config.get("backend", {})
    chat_url = backend_cfg.get("chat_url", "http://127.0.0.1:8000/v1/chat/completions")
    model = backend_cfg.get("model", "llama3.2:3b")

    prompt = f"""
Fasse diesen Jarvis-Marktbericht auf Deutsch in maximal 6 kurzen Sätzen zusammen.
Stil: Trading-Desk, ruhig, klar, sprechbar.
Keine Markdown-Zeichen, keine Dateipfade, keine Tabellen, keine Rohdatenkolonnen.
Keine neuen Fakten hinzufügen.

BERICHT:
{content[:12000]}
""".strip()

    try:
        response = requests.post(
            chat_url,
            json={
                "model": model,
                "temperature": 0.2,
                "messages": [
                    {
                        "role": "system",
                        "content": "Du erstellst kurze, sprechbare deutsche Zusammenfassungen.",
                    },
                    {"role": "user", "content": prompt},
                ],
            },
            timeout=180,
        )
        response.raise_for_status()
        data = response.json()
        summary = data["choices"][0]["message"]["content"].strip()
        return ReportSummaryResult(
            ok=True,
            report_path=str(path),
            summary=summary,
            message="Report summarized successfully.",
        )
    except Exception as exc:
        fallback = "Der Marktbericht wurde erstellt, aber die gesprochene Zusammenfassung konnte nicht erzeugt werden."
        return ReportSummaryResult(
            ok=False,
            report_path=str(path),
            summary=fallback,
            message="Local LLM summary failed.",
            error=str(exc),
        )
