#!/bin/bash

set -e

echo "===================================="
echo " JARVIS MARKET BRIEFING"
echo "===================================="

cd ~/jarvis

echo "[1/4] Activating Python venv..."
source .venv/bin/activate

echo "[2/4] Updating market data..."
python scripts/market_data.py

echo "[3/4] Generating market briefing..."
python scripts/market_briefing.py --mode morning

echo "[4/4] Done."
echo "Briefing saved in:"
echo "~/jarvis/obsidian/MarketBriefings/"
