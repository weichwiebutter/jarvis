#!/usr/bin/env python3
"""
Hermes Voice Status

Builds a read-only voice status object for the future Jarvis Voice Interface.
This module does not access microphones, record audio, start wake word
listeners, start services, or write runtime files.
"""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


PLANNED_STACK = {
    "wake_word": "planned",
    "whisper": "planned",
    "edge_tts": "planned",
    "local_offline_voice": "planned",
    "streaming_audio": "planned",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _relative_path(path: Path) -> str:
    try:
        return str(path.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(path)


def _artifact_status(path: Path) -> dict[str, Any]:
    return {
        "path": _relative_path(path),
        "exists": path.exists(),
        "is_file": path.is_file(),
    }


def build_voice_status() -> dict[str, Any]:
    voice_client = PROJECT_ROOT / "voice_client.py"
    voice_requirements = PROJECT_ROOT / "installer" / "requirements_voice.txt"

    voice_artifacts = {
        "voice_client": _artifact_status(voice_client),
        "voice_requirements": _artifact_status(voice_requirements),
    }

    return {
        "generated_at": utc_now(),
        "voice_status": {
            "status": "planned",
            "configured": False,
            "enabled": False,
            "read_only": True,
            "services_started": False,
            "audio_access_performed": False,
            "artifacts": voice_artifacts,
            "note": "Voice interface planning status only; no voice service is started.",
        },
        "wake_word_status": {
            "status": PLANNED_STACK["wake_word"],
            "enabled": False,
            "active": False,
            "service_started": False,
            "note": "Wake word detection is planned and is not started by this status check.",
        },
        "microphone_status": {
            "status": "not_checked",
            "enabled": False,
            "accessed": False,
            "recording": False,
            "note": "Microphone hardware is intentionally not probed or accessed.",
        },
        "transcription_status": {
            "status": PLANNED_STACK["whisper"],
            "provider": "whisper",
            "enabled": False,
            "active": False,
            "offline_capable": True,
            "note": "Transcription backend is planned; no audio is processed here.",
        },
        "tts_status": {
            "status": PLANNED_STACK["edge_tts"],
            "provider": "edge_tts",
            "enabled": False,
            "active": False,
            "note": "Text-to-speech is planned; no audio output is generated here.",
        },
        "audio_visualizer_status": {
            "status": "planned",
            "enabled": False,
            "active": False,
            "streaming_audio": PLANNED_STACK["streaming_audio"],
            "note": "Audio visualization is planned and receives no live audio in this module.",
        },
        "planned_stack": PLANNED_STACK.copy(),
        "warnings": [],
    }


def main() -> int:
    print(json.dumps(build_voice_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
