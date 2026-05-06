#!/usr/bin/env python3
"""
Jarvis Voice Client V1

Purpose:
    Simple microphone -> STT -> Jarvis pipeline.

Flow:
    microphone
    -> Whisper STT
    -> Jarvis Core
    -> optional TTS response

Current scope:
    - push-to-talk style recording
    - no wake word yet
    - local desktop usage

Future:
    - background listening
    - wake word
    - streaming STT
"""

from __future__ import annotations

import argparse
import json
import subprocess
import tempfile
import wave
from datetime import datetime
from pathlib import Path
from typing import Optional

import numpy as np
import sounddevice as sd
import whisper


PROJECT_ROOT = Path(__file__).resolve().parent
JARVIS_CORE = PROJECT_ROOT / "agents" / "jarvis_core.py"

LOG_DIR = PROJECT_ROOT / "logs"
VOICE_LOG = LOG_DIR / "voice_client.log"

DEFAULT_MODEL = "base"

SAMPLE_RATE = 16000
CHANNELS = 1


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def log_event(event: dict) -> None:
    ensure_dirs()

    with VOICE_LOG.open("a", encoding="utf-8") as file:
        file.write(json.dumps(event, ensure_ascii=False, default=str))
        file.write("\n")


def record_audio(duration: int = 5) -> Path:
    print(f"\n[Voice] Aufnahme startet ({duration}s)...")

    recording = sd.rec(
        int(duration * SAMPLE_RATE),
        samplerate=SAMPLE_RATE,
        channels=CHANNELS,
        dtype="int16",
    )

    sd.wait()

    print("[Voice] Aufnahme beendet.")

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
        wav_path = Path(temp_file.name)

    with wave.open(str(wav_path), "wb") as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(recording.tobytes())

    return wav_path


def transcribe_audio(audio_path: Path, model_name: str = DEFAULT_MODEL) -> str:
    print(f"[Voice] Lade Whisper Modell: {model_name}")

    model = whisper.load_model(model_name)

    print("[Voice] Transkribiere Audio...")

    result = model.transcribe(str(audio_path), language="de")

    text = result.get("text", "").strip()

    print(f"[Voice] Transkript: {text}")

    return text


def send_to_jarvis(text: str, speak: bool = True) -> tuple[str, str, int]:
    command = [
        "python3",
        str(JARVIS_CORE),
    ]

    if speak:
        command.append("--speak")

    command.append(text)

    completed = subprocess.run(
        command,
        cwd=str(PROJECT_ROOT),
        capture_output=True,
        text=True,
        timeout=900,
        check=False,
    )

    return (
        completed.stdout.strip(),
        completed.stderr.strip(),
        completed.returncode,
    )


def run_voice_session(duration: int, model_name: str, speak: bool) -> int:
    started_at = datetime.now().isoformat(timespec="seconds")

    audio_path: Optional[Path] = None

    try:
        audio_path = record_audio(duration=duration)

        transcript = transcribe_audio(audio_path, model_name=model_name)

        if not transcript:
            print("[Voice] Kein Text erkannt.")
            return 1

        stdout, stderr, returncode = send_to_jarvis(
            transcript,
            speak=speak,
        )

        log_event(
            {
                "event": "voice_session",
                "started_at": started_at,
                "finished_at": datetime.now().isoformat(timespec="seconds"),
                "transcript": transcript,
                "stdout": stdout,
                "stderr": stderr,
                "returncode": returncode,
            }
        )

        print("\n========== JARVIS ==========\n")
        print(stdout)

        if stderr:
            print("\n========== FEHLER ==========\n")
            print(stderr)

        return returncode

    finally:
        if audio_path and audio_path.exists():
            try:
                audio_path.unlink(missing_ok=True)
            except Exception:
                pass


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Voice Client")

    parser.add_argument(
        "--duration",
        type=int,
        default=5,
        help="Recording duration in seconds.",
    )

    parser.add_argument(
        "--model",
        default=DEFAULT_MODEL,
        help="Whisper model name.",
    )

    parser.add_argument(
        "--no-speak",
        action="store_true",
        help="Disable TTS playback.",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    return run_voice_session(
        duration=args.duration,
        model_name=args.model,
        speak=not args.no_speak,
    )


if __name__ == "__main__":
    raise SystemExit(main())
