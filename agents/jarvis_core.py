#!/usr/bin/env python3
"""
Jarvis Core - Hermes Integration (Clean Version)

Jarvis = Interface
Hermes = echtes Gehirn (CLI)

Features:
✔ Hermes korrekt via "chat" Command aufgerufen
✔ Text Output
✔ Optional Voice Output (Edge TTS)
✔ Logging
✔ Fehlerbehandlung
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional


# Paths
PROJECT_ROOT = Path(__file__).resolve().parents[1]
LOG_DIR = PROJECT_ROOT / "logs"
LOG_FILE = LOG_DIR / "jarvis_core.log"


# =========================
# Data Model
# =========================

@dataclass
class JarvisResult:
    ok: bool
    timestamp: str
    user_input: str
    hermes_output: str
    spoken: bool
    error: Optional[str] = None


# =========================
# Helpers
# =========================

def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)


def log_result(result: JarvisResult) -> None:
    ensure_dirs()
    with LOG_FILE.open("a", encoding="utf-8") as f:
        f.write(json.dumps(asdict(result), ensure_ascii=False))
        f.write("\n")


# =========================
# Hermes Integration
# =========================

def run_hermes(user_input: str, timeout: int = 600):
    """
    Correct Hermes CLI usage:
    hermes -z "prompt" chat
    """
    try:
        cmd = ["hermes", "-z", user_input, "chat"]

        result = subprocess.run(
            cmd,
            cwd=str(PROJECT_ROOT),
            capture_output=True,
            text=True,
            timeout=timeout,
        )

        stdout = result.stdout.strip()
        stderr = result.stderr.strip()

        if result.returncode != 0:
            return False, stdout, stderr or f"Hermes error ({result.returncode})"

        return True, stdout, None

    except FileNotFoundError:
        return False, "", "Hermes command not found (check PATH)"

    except subprocess.TimeoutExpired:
        return False, "", "Hermes timed out"

    except Exception as e:
        return False, "", str(e)


# =========================
# Voice Output
# =========================

def speak_text(text: str):
    if not text.strip():
        return False, "No text"

    try:
        with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as tmp:
            audio_path = Path(tmp.name)

        # Generate audio
        tts = subprocess.run(
            [
                "edge-tts",
                "--voice", "de-DE-ConradNeural",
                "--text", text,
                "--write-media", str(audio_path),
            ],
            capture_output=True,
            text=True,
        )

        if tts.returncode != 0:
            return False, tts.stderr

        # Play audio
        play = subprocess.run(
            ["mpg123", "-q", str(audio_path)],
            capture_output=True,
            text=True,
        )

        audio_path.unlink(missing_ok=True)

        if play.returncode != 0:
            return False, play.stderr

        return True, None

    except Exception as e:
        return False, str(e)


# =========================
# Core Logic
# =========================

def handle(user_input: str, speak: bool = False) -> JarvisResult:
    ok, output, error = run_hermes(user_input)

    spoken = False

    if ok and speak:
        spoken, speak_err = speak_text(output)
        if speak_err:
            error = f"TTS Error: {speak_err}"

    result = JarvisResult(
        ok=ok and (not speak or spoken),
        timestamp=utc_now(),
        user_input=user_input,
        hermes_output=output,
        spoken=spoken,
        error=error,
    )

    log_result(result)
    return result


# =========================
# CLI
# =========================

def build_parser():
    parser = argparse.ArgumentParser(description="Jarvis Core")

    parser.add_argument("input", nargs="*", help="User input")

    parser.add_argument("--speak", action="store_true")

    parser.add_argument("--json", action="store_true")

    return parser


def main():
    parser = build_parser()
    args = parser.parse_args()

    user_input = " ".join(args.input).strip()

    if not user_input:
        print("Kein Input.")
        return 1

    result = handle(user_input, speak=args.speak)

    if args.json:
        print(json.dumps(asdict(result), indent=2, ensure_ascii=False))
    else:
        print(result.hermes_output)

        if result.error:
            print(f"\n[Fehler] {result.error}", file=sys.stderr)

    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
