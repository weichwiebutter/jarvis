#!/usr/bin/env python3
"""
Jarvis Background Service V1

Long-running background service for Jarvis.

Responsibilities:
- start and stop cleanly
- write heartbeat/status
- prepare future voice/event loop
- keep UI, Jarvis Core, Hermes and Voice separated
"""

from __future__ import annotations

import argparse
import json
import signal
import time
import uuid
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional


PROJECT_ROOT = Path(__file__).resolve().parents[1]

LOG_DIR = PROJECT_ROOT / "logs"
STATE_DIR = PROJECT_ROOT / "memory"

SERVICE_LOG = LOG_DIR / "background_service.log"
SERVICE_STATE = STATE_DIR / "background_service_state.json"


@dataclass
class ServiceState:
    ok: bool
    service: str
    status: str
    session_id: str
    started_at: str
    updated_at: str
    heartbeat_count: int
    shutdown_requested: bool
    error: Optional[str] = None


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def ensure_dirs() -> None:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    STATE_DIR.mkdir(parents=True, exist_ok=True)


class JarvisBackgroundService:
    def __init__(self, heartbeat_interval: int = 10) -> None:
        ensure_dirs()
        self.heartbeat_interval = heartbeat_interval
        self.session_id = str(uuid.uuid4())
        self.started_at = utc_now()
        self.shutdown_requested = False
        self.heartbeat_count = 0

    def start(self) -> None:
        self._register_signal_handlers()
        self._log_event("service_started", {"session_id": self.session_id})

        while not self.shutdown_requested:
            self.heartbeat_count += 1
            self._write_state("running")
            self._log_event(
                "heartbeat",
                {
                    "session_id": self.session_id,
                    "heartbeat_count": self.heartbeat_count,
                },
            )
            time.sleep(self.heartbeat_interval)

        self._write_state("stopped")
        self._log_event("service_stopped", {"session_id": self.session_id})

    def stop(self) -> None:
        self.shutdown_requested = True

    def _register_signal_handlers(self) -> None:
        signal.signal(signal.SIGINT, self._handle_signal)
        signal.signal(signal.SIGTERM, self._handle_signal)

    def _handle_signal(self, signum, frame) -> None:
        self._log_event(
            "shutdown_signal_received",
            {
                "session_id": self.session_id,
                "signal": signum,
            },
        )
        self.stop()

    def _write_state(self, status: str, error: Optional[str] = None) -> None:
        state = ServiceState(
            ok=error is None,
            service="jarvis_background_service",
            status=status,
            session_id=self.session_id,
            started_at=self.started_at,
            updated_at=utc_now(),
            heartbeat_count=self.heartbeat_count,
            shutdown_requested=self.shutdown_requested,
            error=error,
        )

        SERVICE_STATE.write_text(
            json.dumps(asdict(state), indent=2, ensure_ascii=False, default=str),
            encoding="utf-8",
        )

    def _log_event(self, event: str, payload: dict) -> None:
        record = {
            "timestamp": utc_now(),
            "event": event,
            "payload": payload,
        }

        with SERVICE_LOG.open("a", encoding="utf-8") as file:
            file.write(json.dumps(record, ensure_ascii=False, default=str))
            file.write("\n")


def read_status() -> dict:
    ensure_dirs()

    if not SERVICE_STATE.exists():
        return {
            "ok": False,
            "status": "not_started",
            "error": "No background service state file found.",
        }

    try:
        data = json.loads(SERVICE_STATE.read_text(encoding="utf-8"))
        if isinstance(data, dict):
            return data
    except Exception as exc:
        return {
            "ok": False,
            "status": "invalid_state",
            "error": str(exc),
        }

    return {
        "ok": False,
        "status": "invalid_state",
        "error": "State file is not a JSON object.",
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Jarvis Background Service")

    parser.add_argument(
        "--status",
        action="store_true",
        help="Print current background service status and exit.",
    )

    parser.add_argument(
        "--heartbeat",
        type=int,
        default=10,
        help="Heartbeat interval in seconds.",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.status:
        print(json.dumps(read_status(), indent=2, ensure_ascii=False, default=str))
        return 0

    service = JarvisBackgroundService(heartbeat_interval=args.heartbeat)

    try:
        service.start()
        return 0
    except Exception as exc:
        ensure_dirs()
        SERVICE_STATE.write_text(
            json.dumps(
                {
                    "ok": False,
                    "service": "jarvis_background_service",
                    "status": "crashed",
                    "updated_at": utc_now(),
                    "error": str(exc),
                },
                indent=2,
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        print(json.dumps({"ok": False, "error": str(exc)}, indent=2, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
