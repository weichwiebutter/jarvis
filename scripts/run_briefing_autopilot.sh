#!/bin/bash
set -euo pipefail

# Force load environment
source ~/.bashrc

MODE="${1:-morning}"
PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"

cd "$PROJECT_ROOT"

mkdir -p logs memory data obsidian/MarketBriefings

if [ -d ".venv" ]; then
  source .venv/bin/activate
fi

python3 agents/briefing_worker.py --mode "$MODE"
