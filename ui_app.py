#!/usr/bin/env python3
"""
Jarvis UI App V3

Jarvis = Oberfläche
Hermes = Gehirn
Ollama = lokale GPU-Modelle
OpenRouter = externe Modellschicht

Features:
- Chat mit Jarvis
- optionale Sprachausgabe
- Browser-Mikrofon über Gradio
- Audio -> Whisper -> Jarvis
- System Start / Stop / Status
"""

from __future__ import annotations

import json
import subprocess
import tempfile
from datetime import datetime
from pathlib import Path
from typing import Optional, Tuple

import gradio as gr
import whisper


PROJECT_ROOT = Path(__file__).resolve().parent

JARVIS_CORE = PROJECT_ROOT / "agents" / "jarvis_core.py"
BACKGROUND_SERVICE = PROJECT_ROOT / "service" / "background_service.py"
START_SCRIPT = PROJECT_ROOT / "scripts" / "start_jarvis_ui.sh"
STOP_SCRIPT = PROJECT_ROOT / "scripts" / "stop_jarvis.sh"

LOG_FILE = PROJECT_ROOT / "logs" / "ui_app.log"

DEFAULT_WHISPER_MODEL = "base"
_whisper_model = None


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


def build_app() -> gr.Blocks:
    with gr.Blocks(title="Jarvis Control Center") as app:
        gr.Markdown(
            """
            # Jarvis Control Center

            **Jarvis = Oberfläche**  
            **Hermes = Gehirn**  
            **Ollama = lokale GPU-Modelle**  
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
            - Jarvis Core routet hybrid: lokal via Ollama oder extern via Hermes/OpenRouter.
            - Sprachausgabe läuft über Edge-TTS.
            - Spracheingabe läuft über Browser-Mikrofon und Whisper.
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
