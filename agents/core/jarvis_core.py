#!/usr/bin/env python3
"""
Jarvis Core - Hybrid Smart Routing + Orchestrator

Jarvis = Interface
Hermes = externes Gehirn / selbstlernender Agent
Ollama = lokale Modellschicht
Orchestrator = zentrale Agenten-/Domain-Entscheidung

Modes:
    auto   -> Jarvis entscheidet lokal oder Hermes
    local  -> immer Ollama
    hermes -> immer Hermes
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional


PROJECT_ROOT = Path(__file__).resolve().parents[2]
LOG_DIR = PROJECT_ROOT / "logs"
LOG_FILE = LOG_DIR / "jarvis_core.log"

OLLAMA_URL = "http://127.0.0.1:11434/api/generate"

LOCAL_SMALL_MODEL = "llama3.2:3b"
LOCAL_LARGE_MODEL = "qwen2.5-coder:7b"


@dataclass
class RoutingDecision:
    mode: str
    model: Optional[str]
    reason: str
    complexity_score: int
    intent: str
    domain: str
    agent_module: str
    agent_class: str


@dataclass
class JarvisResult:
    ok: bool
    timestamp: str
    user_input: str
    mode_requested: str
    mode_used: str
    model_used: Optional[str]
    intent: str
    domain: str
    agent_module: str
    agent_class: str
    complexity_score: int
    routing_reason: str
    output: str
    spoken: bool
    error: Optional[str] = None


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def log_result(result: JarvisResult) -> None:
    ensure_dirs()
    with LOG_FILE.open("a", encoding="utf-8") as file:
        file.write(json.dumps(asdict(result), ensure_ascii=False, default=str))
        file.write("\n")


def normalize(text: str) -> str:
    return text.strip().lower()


def load_orchestration(user_input: str) -> dict:
    try:
        from agents.core.orchestrator import orchestrate

        result = orchestrate(user_input)
        if isinstance(result, dict):
            return result

    except Exception as exc:
        return {
            "ok": False,
            "domain": "office",
            "agent_module": "agents.office.office_agent",
            "agent_class": "OfficeAgent",
            "confidence": 0.0,
            "reason": f"Orchestrator failed: {exc}",
        }

    return {
        "ok": False,
        "domain": "office",
        "agent_module": "agents.office.office_agent",
        "agent_class": "OfficeAgent",
        "confidence": 0.0,
        "reason": "Orchestrator returned invalid result.",
    }


def detect_intent(user_input: str) -> str:
    text = normalize(user_input)

    if any(term in text for term in ["code", "python", "script", "debug", "funktion", "klasse", "json", "bash", "datei"]):
        return "coding"

    if any(term in text for term in ["plane", "planung", "roadmap", "architektur", "projekt", "strategie", "system", "agent", "automatisierung", "workflow"]):
        return "planning"

    if any(term in text for term in ["analysiere", "analyse", "bewerte", "vergleiche", "einschätzung", "entscheidung"]):
        return "analysis"

    if any(term in text for term in ["recherchiere", "research", "quelle", "news", "internet", "web"]):
        return "research"

    if any(term in text for term in ["merk dir", "speichere", "memory", "gedächtnis", "obsidian"]):
        return "memory"

    if any(term in text for term in ["voice", "sprache", "whisper", "mikrofon", "tts", "headset"]):
        return "voice"

    return "chat"


def complexity_score(user_input: str, intent: str, domain: str) -> int:
    text = normalize(user_input)
    score = 0
    length = len(user_input)

    if length > 120:
        score += 1
    if length > 350:
        score += 2
    if length > 800:
        score += 3

    high_complexity_terms = [
        "architektur",
        "roadmap",
        "masterplan",
        "multi-agent",
        "selbstlernend",
        "autonomie",
        "background",
        "integration",
        "system",
        "workflow",
        "entscheidung",
        "strategie",
        "github",
        "deployment",
        "backup",
        "security",
        "sicherheit",
        "orchestrator",
    ]

    medium_complexity_terms = [
        "analysiere",
        "plane",
        "baue",
        "erstelle",
        "verbinde",
        "integriere",
        "debug",
        "refactor",
        "optimieren",
        "vergleich",
    ]

    for term in high_complexity_terms:
        if term in text:
            score += 2

    for term in medium_complexity_terms:
        if term in text:
            score += 1

    if intent in {"planning", "research", "analysis"}:
        score += 2

    if intent in {"coding", "voice", "memory"}:
        score += 1

    if domain in {"research", "business", "trading"}:
        score += 1

    return min(score, 10)


def choose_routing(user_input: str, requested_mode: str) -> RoutingDecision:
    orchestration = load_orchestration(user_input)

    domain = str(orchestration.get("domain", "office"))
    agent_module = str(orchestration.get("agent_module", "agents.office.office_agent"))
    agent_class = str(orchestration.get("agent_class", "OfficeAgent"))
    orchestrator_reason = str(orchestration.get("reason", "No orchestrator reason."))

    intent = detect_intent(user_input)
    score = complexity_score(user_input, intent, domain)

    if requested_mode == "hermes":
        return RoutingDecision(
            mode="hermes",
            model=None,
            reason=f"Manuell auf Hermes erzwungen. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    if requested_mode == "local":
        model = LOCAL_LARGE_MODEL if intent in {"coding", "analysis", "planning"} or score >= 4 else LOCAL_SMALL_MODEL
        return RoutingDecision(
            mode="local",
            model=model,
            reason=f"Manuell auf lokal erzwungen. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    if intent in {"planning", "research"}:
        return RoutingDecision(
            mode="hermes",
            model=None,
            reason=f"Planung/Recherche gehört zu Hermes. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    if domain in {"business", "research", "trading"} and score >= 4:
        return RoutingDecision(
            mode="hermes",
            model=None,
            reason=f"Fachdomäne mit höherer Komplexität, Hermes übernimmt. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    if score >= 6:
        return RoutingDecision(
            mode="hermes",
            model=None,
            reason=f"Hohe Komplexität, Hermes übernimmt Planung und Kontext. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    if intent in {"coding", "analysis", "voice", "memory"} or domain in {"coding", "memory", "improvement"} or score >= 3:
        return RoutingDecision(
            mode="local",
            model=LOCAL_LARGE_MODEL,
            reason=f"Mittlere technische Aufgabe, lokales großes Modell reicht. Orchestrator: {orchestrator_reason}",
            complexity_score=score,
            intent=intent,
            domain=domain,
            agent_module=agent_module,
            agent_class=agent_class,
        )

    return RoutingDecision(
        mode="local",
        model=LOCAL_SMALL_MODEL,
        reason=f"Einfache Anfrage, lokales kleines Modell reicht. Orchestrator: {orchestrator_reason}",
        complexity_score=score,
        intent=intent,
        domain=domain,
        agent_module=agent_module,
        agent_class=agent_class,
    )


def run_ollama(user_input: str, model: str, timeout: int = 180) -> tuple[bool, str, Optional[str]]:
    prompt = (
        "Du bist Jarvis, ein deutscher KI-Assistent. "
        "Antworte hilfreich, knapp und strukturiert.\n\n"
        f"Aufgabe:\n{user_input}"
    )

    payload = {
        "model": model,
        "prompt": prompt,
        "stream": False,
    }

    request = urllib.request.Request(
        OLLAMA_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            parsed = json.loads(response.read().decode("utf-8"))

        output = str(parsed.get("response", "")).strip()

        if not output:
            return False, "", "Ollama returned empty response."

        return True, output, None

    except urllib.error.URLError as exc:
        return False, "", f"Ollama not reachable: {exc}"

    except Exception as exc:
        return False, "", str(exc)


def run_hermes(user_input: str, decision: RoutingDecision, timeout: int = 600) -> tuple[bool, str, Optional[str]]:
    prompt = (
        "Du bist Hermes, das Planungs- und Agentengehirn hinter Jarvis.\n"
        "Jarvis hat die Anfrage bereits vorgeroutet.\n\n"
        f"Domain: {decision.domain}\n"
        f"Agent Module: {decision.agent_module}\n"
        f"Agent Class: {decision.agent_class}\n"
        f"Intent: {decision.intent}\n"
        f"Complexity Score: {decision.complexity_score}\n\n"
        f"User Task:\n{user_input}"
    )

    try:
        completed = subprocess.run(
            ["hermes", "-z", prompt, "chat"],
            cwd=str(PROJECT_ROOT),
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )

        stdout = completed.stdout.strip()
        stderr = completed.stderr.strip()

        if completed.returncode != 0:
            return False, stdout, stderr or f"Hermes exited with code {completed.returncode}"

        return True, stdout, None

    except FileNotFoundError:
        return False, "", "Hermes command not found. Run: which hermes"

    except subprocess.TimeoutExpired:
        return False, "", "Hermes timed out."

    except Exception as exc:
        return False, "", str(exc)


def speak_text(text: str) -> tuple[bool, Optional[str]]:
    if not text.strip():
        return False, "No text to speak."

    try:
        with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as temp_file:
            audio_path = Path(temp_file.name)

        tts_completed = subprocess.run(
            [
                "edge-tts",
                "--voice",
                "de-DE-ConradNeural",
                "--text",
                text,
                "--write-media",
                str(audio_path),
            ],
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )

        if tts_completed.returncode != 0:
            return False, tts_completed.stderr.strip() or "edge-tts failed."

        player_completed = subprocess.run(
            ["mpg123", "-q", str(audio_path)],
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )

        try:
            audio_path.unlink(missing_ok=True)
        except Exception:
            pass

        if player_completed.returncode != 0:
            return False, player_completed.stderr.strip() or "mpg123 failed."

        return True, None

    except FileNotFoundError as exc:
        return False, f"Missing command: {exc.filename}"

    except Exception as exc:
        return False, str(exc)


def handle_request(user_input: str, requested_mode: str = "auto", speak: bool = False) -> JarvisResult:
    decision = choose_routing(user_input, requested_mode)

    if decision.mode == "local":
        ok, output, error = run_ollama(
            user_input=user_input,
            model=decision.model or LOCAL_SMALL_MODEL,
        )

        if not ok and requested_mode == "auto":
            fallback_reason = f"Lokales Modell fehlgeschlagen, Fallback zu Hermes. Fehler: {error}"
            decision = RoutingDecision(
                mode="hermes",
                model=None,
                reason=fallback_reason,
                complexity_score=decision.complexity_score,
                intent=decision.intent,
                domain=decision.domain,
                agent_module=decision.agent_module,
                agent_class=decision.agent_class,
            )
            ok, output, error = run_hermes(user_input, decision)

    else:
        ok, output, error = run_hermes(user_input, decision)

    spoken = False

    if ok and speak:
        spoken, speak_error = speak_text(output)
        if speak_error:
            error = f"TTS failed: {speak_error}"

    result = JarvisResult(
        ok=ok and (not speak or spoken),
        timestamp=utc_now(),
        user_input=user_input,
        mode_requested=requested_mode,
        mode_used=decision.mode,
        model_used=decision.model,
        intent=decision.intent,
        domain=decision.domain,
        agent_module=decision.agent_module,
        agent_class=decision.agent_class,
        complexity_score=decision.complexity_score,
        routing_reason=decision.reason,
        output=output,
        spoken=spoken,
        error=error,
    )

    log_result(result)
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Core - Hybrid Smart Routing + Orchestrator")

    parser.add_argument(
        "input",
        nargs="*",
        help="Message for Jarvis",
    )

    parser.add_argument(
        "--mode",
        choices=["auto", "local", "hermes"],
        default="auto",
        help="Routing mode: auto, local, hermes",
    )

    parser.add_argument(
        "--speak",
        action="store_true",
        help="Speak response using Edge TTS.",
    )

    parser.add_argument(
        "--json",
        action="store_true",
        help="Print full JSON result.",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    user_input = " ".join(args.input).strip()

    if not user_input:
        print("Kein Input.")
        return 1

    result = handle_request(
        user_input=user_input,
        requested_mode=args.mode,
        speak=args.speak,
    )

    if args.json:
        print(json.dumps(asdict(result), indent=2, ensure_ascii=False, default=str))
    else:
        if result.output:
            print(result.output)

        if result.error:
            print(f"\n[Jarvis Fehler] {result.error}", file=sys.stderr)

        print(
            "\n[Jarvis Routing] "
            f"mode={result.mode_used} "
            f"domain={result.domain} "
            f"agent={result.agent_class} "
            f"intent={result.intent} "
            f"score={result.complexity_score} "
            + (f"model={result.model_used} " if result.model_used else "")
            + f"reason={result.routing_reason}"
        )

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
