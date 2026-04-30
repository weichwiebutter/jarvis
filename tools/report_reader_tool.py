from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Any

import requests


@dataclass
class ToolResult:
    name: str
    ok: bool
    report_path: str
    summary: Optional[str]
    message: str
    error: Optional[str] = None

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "ok": self.ok,
            "report_path": self.report_path,
            "summary": self.summary,
            "message": self.message,
            "error": self.error,
        }


def summarize_report(arg1: Any, arg2: Any = None) -> ToolResult:
    try:
        if isinstance(arg1, dict):
            config = arg1
            report_path = arg2
        else:
            report_path = arg1
            config = arg2 if isinstance(arg2, dict) else None

        if report_path is None:
            raise ValueError("report_path is missing")

        report_path = str(Path(str(report_path)).expanduser())

        with open(report_path, "r", encoding="utf-8") as f:
            report_text = f.read()

        model = "llama3.2:3b"

        if config:
            backend_config = config.get("backend", {})
            model = backend_config.get("model", model)

        prompt = f"""
Du bist ein professioneller Trading-Desk Analyst.

Aufgabe:
Erstelle eine gesprochene Kurzfassung des folgenden Marktberichts auf Deutsch.

Strikte Regeln:
- Antworte nur mit Fließtext.
- Kein JSON.
- Kein Tool-Aufruf.
- Keine Funktionsnamen.
- Keine Parameter.
- Keine Einleitung.
- Keine Entschuldigung.
- Kein Hinweis auf Übersetzung.
- Kein Hinweis auf Zusammenfassung.
- Maximal 6 Sätze.
- Direkt mit der Markteinschätzung starten.
- Fokus auf Gold, Dollar, Renditen, Risiko und Bias.

Marktbericht:
{report_text}
"""

        response = requests.post(
            "http://127.0.0.1:11434/api/generate",
            json={
                "model": model,
                "prompt": prompt,
                "stream": False,
                "options": {
                    "temperature": 0.2
                }
            },
            timeout=120,
        )

        response.raise_for_status()
        data = response.json()

        summary = data.get("response", "").strip()

        if not summary:
            raise ValueError("Ollama returned empty summary")

        return ToolResult(
            name="report_summary",
            ok=True,
            report_path=report_path,
            summary=summary,
            message="Report summarized successfully.",
            error=None,
        )

    except Exception as e:
        return ToolResult(
            name="report_summary",
            ok=False,
            report_path=str(arg2 if isinstance(arg1, dict) else arg1),
            summary=None,
            message="Failed to summarize report.",
            error=str(e),
        )
