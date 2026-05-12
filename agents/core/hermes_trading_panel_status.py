#!/usr/bin/env python3
"""
Hermes Trading Panel Status

Builds a read-only planning/status object for the future Hermes Trading
Analyst panel. This module does not implement trading logic, connect to
cTrader, place orders, read API keys, or write runtime files.
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


SUPPORTED_MARKETS = ["XAUUSD", "EURUSD", "GER40"]

PLANNED_TIMEFRAMES = {
    "HTF": ["W1", "D1", "H4"],
    "MTF": ["H1", "M15"],
    "LTF": ["M5", "M1"],
}

PLANNED_PATTERNS = [
    "Rejection",
    "False Break",
    "Engulfing",
    "Morning Star",
    "Evening Star",
]

PREDICTION_OUTCOMES = [
    "correct",
    "wrong",
    "expired",
    "invalidated",
    "late_correct",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def build_trading_panel_status() -> dict[str, Any]:
    return {
        "generated_at": utc_now(),
        "status": "planned",
        "analysis_only": True,
        "no_auto_trading": True,
        "human_review_required": True,
        "supported_markets": SUPPORTED_MARKETS.copy(),
        "planned_timeframes": {
            group: timeframes.copy()
            for group, timeframes in PLANNED_TIMEFRAMES.items()
        },
        "planned_patterns": PLANNED_PATTERNS.copy(),
        "confidence_score": {
            "min": 0,
            "max": 12,
            "alert_threshold": "planned",
        },
        "prediction_feedback_learning": {
            "status": "planned",
            "outcomes": PREDICTION_OUTCOMES.copy(),
        },
        "ctrader_integration": {
            "status": "planned",
            "mode": "external_bridge_planned",
        },
        "warnings": [],
    }


def main() -> int:
    print(json.dumps(build_trading_panel_status(), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
