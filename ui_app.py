#!/usr/bin/env python3
"""
Jarvis UI App V4

Jarvis = Oberfläche / Voice / Kontrolle
Hermes = Gehirn / lernender Agent / Delegation
Ollama = lokale Modellschicht
OpenRouter = externe Modellschicht

Features:
- Chat mit Jarvis
- Browser-Mikrofon über Gradio
- System Start / Stop / Status
- Delegation-Test über RuntimeRouter / DelegationExecutor
"""

from __future__ import annotations

import json
import subprocess
from datetime import datetime
from pathlib import Path
from typing import Any, Optional, Tuple

import gradio as gr
import whisper


PROJECT_ROOT = Path(__file__).resolve().parent

JARVIS_CORE = PROJECT_ROOT / "agents" / "core" / "jarvis_core.py"
BACKGROUND_SERVICE = PROJECT_ROOT / "service" / "background_service.py"
START_SCRIPT = PROJECT_ROOT / "scripts" / "start_jarvis_ui.sh"
STOP_SCRIPT = PROJECT_ROOT / "scripts" / "stop_jarvis.sh"

LOG_FILE = PROJECT_ROOT / "logs" / "ui_app.log"

DEFAULT_WHISPER_MODEL = "base"
_whisper_model = None

try:
    from agents.core.hermes_ui_status import build_hermes_ui_status as _build_hermes_ui_status
    _HERMES_UI_STATUS_IMPORT_ERROR = ""
except Exception as exc:
    _build_hermes_ui_status = None
    _HERMES_UI_STATUS_IMPORT_ERROR = str(exc)


def ensure_dirs() -> None:
    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)


def log_event(event: dict) -> None:
    ensure_dirs()
    with LOG_FILE.open("a", encoding="utf-8") as file:
        file.write(json.dumps(event, ensure_ascii=False, default=str))
        file.write("\n")


def run_command(command: list[str], timeout: int = 60) -> Tuple[str, str, int]:
    completed = subprocess.run(
        command,
        cwd=str(PROJECT_ROOT),
        capture_output=True,
        text=True,
        timeout=timeout,
        check=False,
    )
    return completed.stdout.strip(), completed.stderr.strip(), completed.returncode


def load_whisper_model(model_name: str = DEFAULT_WHISPER_MODEL):
    global _whisper_model

    if _whisper_model is None:
        _whisper_model = whisper.load_model(model_name)

    return _whisper_model


def transcribe_audio(audio_path: str) -> Tuple[str, str]:
    if not audio_path:
        return "", "Keine Audiodatei erhalten."

    try:
        model = load_whisper_model()
        result = model.transcribe(audio_path, language="de")
        text = str(result.get("text", "")).strip()

        if not text:
            return "", "Kein Text erkannt."

        return text, ""

    except Exception as exc:
        return "", f"Transkription fehlgeschlagen: {exc}"


def run_jarvis(message: str, speak: bool = False) -> Tuple[str, str]:
    message = message.strip()

    if not message:
        return "Bitte gib eine Nachricht ein.", ""

    command = ["python3", str(JARVIS_CORE)]

    if speak:
        command.append("--speak")

    command.append(message)

    started_at = datetime.now().isoformat(timespec="seconds")

    try:
        stdout, stderr, returncode = run_command(command, timeout=900)

        log_event(
            {
                "event": "ui_request",
                "started_at": started_at,
                "finished_at": datetime.now().isoformat(timespec="seconds"),
                "message": message,
                "speak": speak,
                "returncode": returncode,
                "stdout": stdout,
                "stderr": stderr,
            }
        )

        if returncode != 0:
            return "", f"Fehler:\n{stderr or stdout or 'Jarvis Core returned an error.'}"

        return stdout, stderr

    except subprocess.TimeoutExpired:
        return "", "Fehler: Jarvis/Hermes hat zu lange gebraucht."

    except Exception as exc:
        return "", f"Fehler: {exc}"


def voice_to_jarvis(audio_path: Optional[str], speak: bool = True) -> Tuple[str, str, str]:
    if not audio_path:
        return "", "", "Bitte zuerst Audio aufnehmen."

    transcript, transcript_error = transcribe_audio(audio_path)

    if transcript_error:
        return "", "", transcript_error

    output, error = run_jarvis(transcript, speak=speak)

    log_event(
        {
            "event": "voice_request",
            "timestamp": datetime.now().isoformat(timespec="seconds"),
            "audio_path": audio_path,
            "transcript": transcript,
            "speak": speak,
            "output": output,
            "error": error,
        }
    )

    return transcript, output, error


def get_service_status() -> str:
    try:
        stdout, stderr, returncode = run_command(
            ["python3", str(BACKGROUND_SERVICE), "--status"],
            timeout=20,
        )

        if returncode != 0:
            return f"Status konnte nicht gelesen werden:\n{stderr or stdout}"

        try:
            data = json.loads(stdout)
            return json.dumps(data, indent=2, ensure_ascii=False)
        except Exception:
            return stdout

    except Exception as exc:
        return f"Fehler beim Status-Check: {exc}"


def _get_hermes_ui_status_payload(optional_task: str = "") -> dict[str, Any]:
    task = (optional_task or "").strip()

    if _build_hermes_ui_status is None:
        return {
            "ok": False,
            "error": "Hermes UI Status konnte nicht importiert werden.",
            "import_error": _HERMES_UI_STATUS_IMPORT_ERROR,
            "system_health": {
                "warnings": [
                    f"agents.core.hermes_ui_status.build_hermes_ui_status unavailable: {_HERMES_UI_STATUS_IMPORT_ERROR}"
                ],
            },
        }

    try:
        status = _build_hermes_ui_status(task or None)
        return {
            "generated_at": status.get("generated_at"),
            "system_health": status.get("system_health"),
            "brain": status.get("brain"),
            "agents": status.get("agents"),
            "runtime": status.get("runtime"),
            "ui_panels": status.get("ui_panels"),
            "learning_memory": status.get("learning_memory"),
            "developer_debug": status.get("developer_debug"),
            "voice": status.get("voice"),
            "trading": status.get("trading"),
        }

    except Exception as exc:
        return {
            "ok": False,
            "error": "Hermes UI Status konnte nicht aufgebaut werden.",
            "exception": str(exc),
            "system_health": {
                "warnings": [f"build_hermes_ui_status failed: {exc}"],
            },
        }


def _safe_status_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def _safe_status_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _status_badge(label: str, value: Any) -> str:
    text = str(value if value is not None else "unknown")
    normalized = text.lower()

    if normalized in {"available", "ok", "ready", "true"}:
        color = "#15803d"
        background = "#dcfce7"
    elif normalized in {"planned", "idle", "read_only", "not_checked"}:
        color = "#1d4ed8"
        background = "#dbeafe"
    elif "warn" in normalized or normalized in {"unavailable", "error", "failed", "false"}:
        color = "#b45309"
        background = "#fef3c7"
    else:
        color = "#374151"
        background = "#f3f4f6"

    return (
        f"<span style='display:inline-block;margin:2px 6px 2px 0;"
        f"padding:3px 8px;border-radius:6px;background:{background};"
        f"color:{color};font-weight:600;font-size:0.9em'>{label}: {text}</span>"
    )


def _format_bool(value: Any) -> str:
    return "true" if bool(value) else "false"


def _format_list(value: Any, limit: int = 8) -> str:
    items = [str(item) for item in _safe_status_list(value)]
    if not items:
        return "-"
    visible = items[:limit]
    suffix = f" (+{len(items) - limit})" if len(items) > limit else ""
    return ", ".join(visible) + suffix


def _format_hermes_ui_status_markdown(status: dict[str, Any]) -> str:
    system_health = _safe_status_dict(status.get("system_health"))
    brain = _safe_status_dict(status.get("brain"))
    agents = _safe_status_dict(status.get("agents"))
    runtime = _safe_status_dict(status.get("runtime"))
    learning_memory = _safe_status_dict(status.get("learning_memory"))
    voice = _safe_status_dict(status.get("voice"))
    trading = _safe_status_dict(status.get("trading"))
    warnings = [str(w) for w in _safe_status_list(system_health.get("warnings")) if str(w).strip()]

    runtime_hermes = _safe_status_dict(runtime.get("hermes_status"))
    runtime_ollama = _safe_status_dict(runtime.get("ollama_status"))
    runtime_memory = _safe_status_dict(runtime.get("memory_status"))
    runtime_voice = _safe_status_dict(runtime.get("voice_status"))

    agent_items = [
        agent for agent in _safe_status_list(agents.get("agents"))
        if isinstance(agent, dict)
    ]
    available_agents = [
        str(agent.get("agent_id", agent.get("name", "unknown")))
        for agent in agent_items
        if agent.get("status") == "available"
    ]
    planned_agents = [
        str(agent.get("agent_id", agent.get("name", "unknown")))
        for agent in agent_items
        if agent.get("status") == "planned"
    ]

    voice_status = _safe_status_dict(voice.get("voice_status"))
    voice_stack = _safe_status_dict(voice.get("planned_stack"))

    trading_feedback = _safe_status_dict(trading.get("prediction_feedback_learning"))
    trading_ctrader = _safe_status_dict(trading.get("ctrader_integration"))

    warning_block = (
        "\n".join(f"- <span style='color:#b45309;font-weight:600'>{warning}</span>" for warning in warnings)
        if warnings
        else "- Keine Warnungen."
    )

    return f"""
### Hermes Status Snapshot

**Generated:** `{status.get("generated_at") or "-"}`

#### System Health
{_status_badge("Hermes", system_health.get("hermes_available"))}
{_status_badge("Ollama", system_health.get("ollama_available"))}
{_status_badge("Memory", system_health.get("memory_available"))}
{_status_badge("Agents available", system_health.get("agents_available_count", 0))}
{_status_badge("Agents planned", system_health.get("agents_planned_count", 0))}

**Warnings**
{warning_block}

#### Hermes Brain
{_status_badge("Route", brain.get("route", brain.get("status", "idle")))}
{_status_badge("Domain", brain.get("domain", "-"))}
{_status_badge("Intent", brain.get("intent", "-"))}
{_status_badge("Confidence", brain.get("confidence", "-"))}
{_status_badge("Approval", _format_bool(brain.get("requires_approval")))}

#### Runtime Status
{_status_badge("Hermes module", runtime_hermes.get("status", "-"))}
{_status_badge("Ollama", runtime_ollama.get("status", "-"))}
{_status_badge("Memory", runtime_memory.get("status", "-"))}
{_status_badge("Voice", runtime_voice.get("status", "-"))}

#### Agent Dashboard
- Available: {_format_list(available_agents)}
- Planned: {_format_list(planned_agents)}

#### Learning / Memory
{_status_badge("Memory", learning_memory.get("memory_available"))}
{_status_badge("Learning", learning_memory.get("learning_available"))}
{_status_badge("Routing hints", learning_memory.get("routing_hints_available"))}
{_status_badge("Improvements", learning_memory.get("improvements_available"))}
- Counts: `{json.dumps(_safe_status_dict(learning_memory.get("counts")), ensure_ascii=False)}`

#### Voice Status
{_status_badge("Voice", voice_status.get("status", "-"))}
{_status_badge("Microphone", _safe_status_dict(voice.get("microphone_status")).get("status", "-"))}
{_status_badge("Transcription", _safe_status_dict(voice.get("transcription_status")).get("status", "-"))}
{_status_badge("TTS", _safe_status_dict(voice.get("tts_status")).get("status", "-"))}
- Planned stack: `{json.dumps(voice_stack, ensure_ascii=False)}`

#### Trading Panel Status
{_status_badge("Status", trading.get("status", "planned"))}
{_status_badge("Analysis only", _format_bool(trading.get("analysis_only", True)))}
{_status_badge("No auto trading", _format_bool(trading.get("no_auto_trading", True)))}
{_status_badge("Human review", _format_bool(trading.get("human_review_required", True)))}
- Markets: {_format_list(trading.get("supported_markets"))}
- Timeframes: `{json.dumps(_safe_status_dict(trading.get("planned_timeframes")), ensure_ascii=False)}`
- Patterns: {_format_list(trading.get("planned_patterns"))}
- Prediction feedback: `{trading_feedback.get("status", "-")}`
- cTrader integration: `{trading_ctrader.get("status", "-")} / {trading_ctrader.get("mode", "-")}`
"""


def get_hermes_ui_status_for_display(optional_task: str = "") -> str:
    status = _get_hermes_ui_status_payload(optional_task)
    return json.dumps(status, indent=2, ensure_ascii=False, default=str)


def get_hermes_ui_status_panels(optional_task: str = "") -> tuple[str, dict[str, Any]]:
    status = _get_hermes_ui_status_payload(optional_task)
    return _format_hermes_ui_status_markdown(status), status


def start_jarvis_system() -> str:
    try:
        stdout, stderr, returncode = run_command(
            [str(START_SCRIPT)],
            timeout=60,
        )

        if returncode != 0:
            return f"Start fehlgeschlagen:\n{stderr or stdout}"

        return stdout or "Jarvis Startbefehl ausgeführt."

    except Exception as exc:
        return f"Fehler beim Start: {exc}"


def stop_jarvis_system() -> str:
    try:
        stdout, stderr, returncode = run_command(
            [str(STOP_SCRIPT)],
            timeout=60,
        )

        if returncode != 0:
            return f"Stop fehlgeschlagen:\n{stderr or stdout}"

        return stdout or "Jarvis Stopbefehl ausgeführt."

    except Exception as exc:
        return f"Fehler beim Stop: {exc}"


def run_delegation_test(
    domain: str,
    task: str,
    approve_step: bool,
    approve_executor: bool,
) -> str:
    task = task.strip()

    if not task:
        return "Bitte Aufgabe eingeben."

    try:
        from agents.core.delegation_contract import build_single_step_contract
        from agents.core.delegation_executor import execute_delegation_contract

        contract = build_single_step_contract(
            objective=f"UI Delegation Test: {domain}",
            domain=domain,
            task=task,
            requires_approval=True,
            approval_reason="UI Delegation Test requires explicit approval.",
            context={
                "source": "jarvis_ui",
                "category": "learnings",
                "title": "UI Delegation Test",
            },
        )

        result = execute_delegation_contract(
            contract,
            approve_all=approve_step,
            approve_executor_tasks=approve_executor,
        )

        log_event(
            {
                "event": "delegation_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "domain": domain,
                "task": task,
                "approve_step": approve_step,
                "approve_executor": approve_executor,
                "contract": contract,
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"Delegation-Test fehlgeschlagen: {exc}"

def run_hermes_decision_test(
    task: str,
    domain: str,
    intent: str,
    route: str,
    approve_step: bool,
    approve_executor: bool,
) -> str:
    task = task.strip()

    if not task:
        return "Bitte Aufgabe eingeben."

    try:
        from agents.core.hermes_decision import (
            build_default_decision,
            decision_to_delegation_contract,
        )
        from agents.core.delegation_executor import execute_delegation_contract

        decision = build_default_decision(
            objective=task,
            domain=domain,
            intent=intent,
            route=route,
            agent_domain=domain,
            reasoning="UI HermesDecision Test.",
        )

        contract = decision_to_delegation_contract(decision)

        result = execute_delegation_contract(
            contract,
            approve_all=approve_step,
            approve_executor_tasks=approve_executor,
        )

        payload = {
            "decision": decision,
            "contract": contract,
            "execution_result": result,
        }

        log_event(
            {
                "event": "hermes_decision_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "payload": payload,
            }
        )

        return json.dumps(payload, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"HermesDecision-Test fehlgeschlagen: {exc}"

def run_hermes_planner_test(objective: str) -> str:
    objective = objective.strip()

    if not objective:
        return "Bitte Objective eingeben."

    try:
        from agents.core.hermes_planner import plan_objective

        result = plan_objective(objective)

        log_event(
            {
                "event": "hermes_planner_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "objective": objective,
                "result": result,
            }
        )

        return json.dumps(
            result,
            indent=2,
            ensure_ascii=False,
            default=str,
        )

    except Exception as exc:
        return f"HermesPlanner-Test fehlgeschlagen: {exc}"

def run_hermes_orchestrator_test(objective: str) -> str:
    objective = objective.strip()

    if not objective:
        return "Bitte Objective eingeben."

    try:
        from agents.core.hermes_orchestrator import orchestrate_objective

        result = orchestrate_objective(objective)

        log_event(
            {
                "event": "hermes_orchestrator_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "objective": objective,
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"HermesOrchestrator-Test fehlgeschlagen: {exc}"

def run_hermes_execution_test(objective: str, approve_all: bool) -> str:
    objective = objective.strip()

    if not objective:
        return "Bitte Objective eingeben."

    try:
        from agents.core.hermes_execution_engine import execute_objective

        result = execute_objective(
            objective=objective,
            approve_all=approve_all,
        )

        log_event(
            {
                "event": "hermes_execution_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "objective": objective,
                "approve_all": approve_all,
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"HermesExecution-Test fehlgeschlagen: {exc}"

def run_hermes_learning_feedback_test(objective: str) -> str:
    objective = objective.strip()

    if not objective:
        return "Bitte Objective eingeben."

    try:
        from agents.core.hermes_execution_engine import execute_objective
        from agents.core.hermes_learning_feedback import build_learning_feedback

        execution_result = execute_objective(objective)
        result = build_learning_feedback(execution_result)

        log_event(
            {
                "event": "hermes_learning_feedback_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "objective": objective,
                "execution_result": execution_result,
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"HermesLearningFeedback-Test fehlgeschlagen: {exc}"

def run_manual_assist_test(
    provider: str,
    task: str,
) -> str:
    task = task.strip()

    if not task:
        return "Bitte Aufgabe eingeben."

    try:
        from agents.core.manual_assist import build_manual_assist

        result = build_manual_assist(
            provider=provider,
            task=task,
            context={
                "source": "jarvis_ui",
                "project": "Jarvis/Hermes",
            },
        )

        log_event(
            {
                "event": "manual_assist_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "provider": provider,
                "task": task,
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"Manual-Assist-Test fehlgeschlagen: {exc}"

def run_provider_model_test(task: str, paid_ok: bool, offline: bool) -> str:
    task = task.strip()

    if not task:
        return "Bitte Aufgabe eingeben."

    try:
        from agents.core.provider_registry import recommend_provider
        from agents.core.model_registry import recommend_model
        from agents.core.hermes_router import decide_route

        route_decision = decide_route(task)

        provider = recommend_provider(
            task=task,
            intent=route_decision.get("intent", ""),
            cost_sensitive=not paid_ok,
        )

        model = recommend_model(
            task=task,
            intent=route_decision.get("intent", ""),
            route=route_decision.get("route", ""),
            cost_sensitive=not paid_ok,
            offline=offline,
        )

        result = {
            "task": task,
            "paid_ok": paid_ok,
            "offline": offline,
            "route_decision": route_decision,
            "provider_recommendation": provider,
            "model_recommendation": model,
        }

        log_event(
            {
                "event": "provider_model_test",
                "timestamp": datetime.now().isoformat(timespec="seconds"),
                "result": result,
            }
        )

        return json.dumps(result, indent=2, ensure_ascii=False, default=str)

    except Exception as exc:
        return f"Provider/Model-Test fehlgeschlagen: {exc}"

def build_app() -> gr.Blocks:
    with gr.Blocks(title="Jarvis Control Center") as app:
        gr.Markdown(
            """
            # Jarvis Control Center

            **Jarvis = Oberfläche / Voice / Kontrolle**  
            **Hermes = Gehirn / lernender Agent / Delegation**  
            **Ollama = lokale Modellschicht**  
            **OpenRouter = externe Modellschicht**
            """
        )

        with gr.Tab("Chat"):
            with gr.Row():
                with gr.Column(scale=3):
                    user_input = gr.Textbox(
                        label="Auftrag an Jarvis",
                        placeholder="z. B. Plane ein Voice Interface mit Whisper",
                        lines=5,
                    )

                    speak = gr.Checkbox(
                        label="Antwort sprechen",
                        value=False,
                    )

                    submit = gr.Button("An Jarvis senden", variant="primary")

                with gr.Column(scale=4):
                    output = gr.Textbox(
                        label="Antwort",
                        lines=18,
                    )

                    error = gr.Textbox(
                        label="Fehler / Hinweise",
                        lines=6,
                    )

            submit.click(
                fn=run_jarvis,
                inputs=[user_input, speak],
                outputs=[output, error],
            )

        with gr.Tab("Voice"):
            gr.Markdown(
                """
                ## Voice Input

                Diese Funktion nutzt das Browser-Mikrofon.  
                Damit umgehen wir das WSL-Problem, dass Linux dein Windows-Mikrofon nicht sauber sieht.
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    audio_input = gr.Audio(
                        sources=["microphone"],
                        type="filepath",
                        label="Mikrofonaufnahme",
                    )

                    voice_speak = gr.Checkbox(
                        label="Antwort sprechen",
                        value=True,
                    )

                    voice_submit = gr.Button("Voice an Jarvis senden", variant="primary")

                with gr.Column(scale=4):
                    transcript_output = gr.Textbox(
                        label="Erkannter Text",
                        lines=4,
                    )

                    voice_response_output = gr.Textbox(
                        label="Jarvis Antwort",
                        lines=14,
                    )

                    voice_error_output = gr.Textbox(
                        label="Fehler / Hinweise",
                        lines=5,
                    )

            voice_submit.click(
                fn=voice_to_jarvis,
                inputs=[audio_input, voice_speak],
                outputs=[transcript_output, voice_response_output, voice_error_output],
            )

        with gr.Tab("Delegation Test"):
            gr.Markdown(
                """
                ## Hermes / Agent / Executor Test

                Testet die neue sichere Runtime-Kette:

                Hermes Contract → Delegation Executor → Runtime Router → Agent → Executor Bridge
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    delegation_domain = gr.Dropdown(
                        label="Domain / Agent",
                        choices=[
                            "memory",
                            "office",
                            "research",
                            "coding",
                            "business",
                            "trading",
                            "improvement",
                        ],
                        value="memory",
                    )

                    delegation_task = gr.Textbox(
                        label="Delegations-Aufgabe",
                        value="Merk dir: Hermes ist das Gehirn.",
                        lines=4,
                    )

                    approve_step = gr.Checkbox(
                        label="Step-Freigabe erteilen",
                        value=False,
                    )

                    approve_executor = gr.Checkbox(
                        label="Executor-Freigabe erteilen",
                        value=False,
                    )

                    delegation_submit = gr.Button(
                        "Delegation testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    delegation_output = gr.Textbox(
                        label="Delegation Ergebnis JSON",
                        lines=24,
                    )

            delegation_submit.click(
                fn=run_delegation_test,
                inputs=[
                    delegation_domain,
                    delegation_task,
                    approve_step,
                    approve_executor,
                ],
                outputs=[delegation_output],
            )

            gr.Markdown(
                """
                ---
                ## Hermes Decision Test

                Testet die nächste Ebene:

                HermesDecision → DelegationContract → DelegationExecutor → RuntimeRouter → Agent → ExecutorBridge
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    decision_task = gr.Textbox(
                        label="HermesDecision Aufgabe",
                        value="Merk dir: Hermes entscheidet über Agenten.",
                        lines=4,
                    )

                    decision_domain = gr.Dropdown(
                        label="Domain",
                        choices=[
                            "memory",
                            "office",
                            "research",
                            "coding",
                            "business",
                            "trading",
                            "improvement",
                        ],
                        value="memory",
                    )

                    decision_intent = gr.Dropdown(
                        label="Intent",
                        choices=[
                            "chat",
                            "memory",
                            "planning",
                            "research",
                            "coding",
                            "analysis",
                            "voice",
                        ],
                        value="memory",
                    )

                    decision_route = gr.Dropdown(
                        label="Route",
                        choices=[
                            "agent",
                            "ollama",
                            "openrouter",
                            "hermes",
                        ],
                        value="agent",
                    )

                    decision_approve_step = gr.Checkbox(
                        label="Step-Freigabe erteilen",
                        value=False,
                    )

                    decision_approve_executor = gr.Checkbox(
                        label="Executor-Freigabe erteilen",
                        value=False,
                    )

                    decision_submit = gr.Button(
                        "HermesDecision testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    decision_output = gr.Textbox(
                        label="HermesDecision Ergebnis JSON",
                        lines=24,
                    )

            decision_submit.click(
                fn=run_hermes_decision_test,
                inputs=[
                    decision_task,
                    decision_domain,
                    decision_intent,
                    decision_route,
                    decision_approve_step,
                    decision_approve_executor,
                ],
                outputs=[decision_output],
            )

            gr.Markdown(
                """
                ---
                ## Hermes Planner Test

                Testet die Planungs-Ebene:

                Objective → Hermes Planner
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    planner_objective = gr.Textbox(
                        label="Hermes Planner Objective",
                        value="Plane die nächsten Schritte für sichere Delegation.",
                        lines=4,
                    )

                    planner_submit = gr.Button(
                        "Hermes Planner testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    planner_output = gr.Textbox(
                        label="Hermes Planner Ergebnis JSON",
                        lines=24,
                    )

            planner_submit.click(
                fn=run_hermes_planner_test,
                inputs=[planner_objective],
                outputs=[planner_output],
            )

            gr.Markdown(
                """
                ---
                ## Hermes Orchestrator Test

                Testet die Multi-Step-Orchestrierung:

                Objective → Hermes Orchestrator → Delegation Steps
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    orchestrator_objective = gr.Textbox(
                        label="Hermes Orchestrator Objective",
                        value="Baue Voice Interface mit Wake Word, Audio Visualizer und Memory.",
                        lines=4,
                    )

                    orchestrator_submit = gr.Button(
                        "Hermes Orchestrator testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    orchestrator_output = gr.Textbox(
                        label="Hermes Orchestrator Ergebnis JSON",
                        lines=30,
                    )

            orchestrator_submit.click(
                fn=run_hermes_orchestrator_test,
                inputs=[orchestrator_objective],
                outputs=[orchestrator_output],
            )

            gr.Markdown(
                """
                ---
                ## Hermes Execution Engine Test

                Testet die kontrollierte Ausführung:

                Objective → Orchestrator → Execution Engine
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    execution_objective = gr.Textbox(
                        label="Hermes Execution Objective",
                        value="Baue lokalen Privacy-Mode mit Ollama Offline-Fallback.",
                        lines=4,
                    )

                    execution_approve_all = gr.Checkbox(
                        label="Alle Schritte freigeben",
                        value=False,
                    )

                    execution_submit = gr.Button(
                        "Hermes Execution testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    execution_output = gr.Textbox(
                        label="Hermes Execution Ergebnis JSON",
                        lines=30,
                    )

            execution_submit.click(
                fn=run_hermes_execution_test,
                inputs=[
                    execution_objective,
                    execution_approve_all,
                ],
                outputs=[execution_output],
            )

            gr.Markdown(
                """
                ---
                ## Hermes Learning Feedback Test

                Testet den Lern-Feedback-Loop:

                Objective → Execution Engine → Learning Feedback
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    learning_objective = gr.Textbox(
                        label="Hermes Learning Objective",
                        value="Merk dir: Hermes ist das Gehirn.",
                        lines=4,
                    )

                    learning_submit = gr.Button(
                        "Hermes Learning Feedback testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    learning_output = gr.Textbox(
                        label="Hermes Learning Feedback Ergebnis JSON",
                        lines=30,
                    )

            learning_submit.click(
                fn=run_hermes_learning_feedback_test,
                inputs=[learning_objective],
                outputs=[learning_output],
            )

            gr.Markdown(
                """
                ---
                ## Provider / Model Test

                Testet Provider-, Modell- und Router-Empfehlung.
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    provider_model_task = gr.Textbox(
                        label="Provider/Model Aufgabe",
                        value="Empfiehl Provider und Modell für eine lokale Coding-Aufgabe.",
                        lines=4,
                    )

                    provider_model_paid_ok = gr.Checkbox(
                        label="Kostenpflichtige APIs erlauben",
                        value=False,
                    )

                    provider_model_offline = gr.Checkbox(
                        label="Offline / nur lokal",
                        value=False,
                    )

                    provider_model_submit = gr.Button(
                        "Provider/Model testen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    provider_model_output = gr.Textbox(
                        label="Provider/Model Ergebnis JSON",
                        lines=24,
                    )

            provider_model_submit.click(
                fn=run_provider_model_test,
                inputs=[
                    provider_model_task,
                    provider_model_paid_ok,
                    provider_model_offline,
                ],
                outputs=[provider_model_output],
            )


            gr.Markdown(
                """
                ---
                ## Manual Assist Test

                Erstellt Copy/Paste-Prompts für:

                - Codex CLI
                - ChatGPT Browser
                - Gemini Browser
                - Copilot Browser
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    manual_provider = gr.Dropdown(
                        label="Manual Provider",
                        choices=[
                            "codex_cli",
                            "chatgpt_manual",
                            "gemini_manual",
                            "copilot_manual",
                        ],
                        value="codex_cli",
                    )

                    manual_task = gr.Textbox(
                        label="Manual Assist Aufgabe",
                        value="Repariere ui_app.py und führe py_compile aus.",
                        lines=4,
                    )

                    manual_submit = gr.Button(
                        "Manual Assist Prompt erzeugen",
                        variant="primary",
                    )

                with gr.Column(scale=4):
                    manual_output = gr.Textbox(
                        label="Manual Assist Ergebnis JSON",
                        lines=30,
                    )

            manual_submit.click(
                fn=run_manual_assist_test,
                inputs=[manual_provider, manual_task],
                outputs=[manual_output],
            )

        with gr.Tab("Hermes Status"):
            gr.Markdown(
                """
                ## Hermes Control Center

                Read-only Status-Snapshot fuer Hermes Brain, Agent Dashboard,
                Runtime, Learning/Memory, Developer/Debug, Voice und Trading.
                Der Status wird nur manuell aktualisiert.
                """
            )

            with gr.Row():
                with gr.Column(scale=3):
                    hermes_status_task = gr.Textbox(
                        label="Optionaler Routing-Task",
                        value="Analysiere XAUUSD auf M15",
                        placeholder="Optional, z. B. Analysiere XAUUSD auf M15",
                        lines=4,
                    )

                    hermes_status_refresh = gr.Button(
                        "Refresh Hermes Status",
                        variant="primary",
                    )

                    hermes_status_refresh_empty = gr.Button(
                        "Refresh ohne Task",
                    )

                with gr.Column(scale=5):
                    hermes_status_summary = gr.Markdown(
                        value="Noch kein Hermes Status geladen. Klicke auf Refresh.",
                    )

                    with gr.Accordion("Advanced JSON", open=False):
                        hermes_status_json = gr.JSON(
                            label="Hermes UI Status Raw JSON",
                        )

            hermes_status_refresh.click(
                fn=get_hermes_ui_status_panels,
                inputs=[hermes_status_task],
                outputs=[hermes_status_summary, hermes_status_json],
            )

            hermes_status_refresh_empty.click(
                fn=get_hermes_ui_status_panels,
                inputs=[],
                outputs=[hermes_status_summary, hermes_status_json],
            )

        with gr.Tab("System"):
            gr.Markdown("## Jarvis Systemsteuerung")

            with gr.Row():
                start_button = gr.Button("Start Jarvis", variant="primary")
                stop_button = gr.Button("Stop Jarvis", variant="stop")
                status_button = gr.Button("Status prüfen")

            system_output = gr.Textbox(
                label="Systemausgabe",
                lines=18,
            )

            start_button.click(
                fn=start_jarvis_system,
                inputs=[],
                outputs=[system_output],
            )

            stop_button.click(
                fn=stop_jarvis_system,
                inputs=[],
                outputs=[system_output],
            )

            status_button.click(
                fn=get_service_status,
                inputs=[],
                outputs=[system_output],
            )

        gr.Markdown(
            """
            ## Aktueller Systemstatus

            - UI läuft über Gradio.
            - Background Service läuft über tmux.
            - Jarvis Core übergibt strukturierte Aufträge an Hermes.
            - Hermes ist das Gehirn und entscheidet über Ollama, OpenRouter und Agenten.
            - Sprachausgabe läuft über Edge-TTS.
            - Spracheingabe läuft über Browser-Mikrofon und Whisper.
            - Delegation Runtime ist approval-gesteuert.
            """
        )


    return app

def main() -> None:
    app = build_app()
    app.launch(
        server_name="127.0.0.1",
        server_port=7860,
        share=False,
        inbrowser=False,
    )


if __name__ == "__main__":
    main()
