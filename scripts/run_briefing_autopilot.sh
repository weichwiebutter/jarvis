#!/bin/bash
set -euo pipefail

MODE="${1:-morning}"
PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"

cd "$PROJECT_ROOT"

mkdir -p logs memory data obsidian/MarketBriefings

if [ -d ".venv" ]; then
  source .venv/bin/activate
fi

python agents/briefing_agent.py --mode "$MODE"
