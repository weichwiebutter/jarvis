#!/bin/bash

set -e

echo "===================================="
echo " JARVIS MARKET BRIEFING"
echo "===================================="

cd ~/jarvis

echo "[1/5] Checking OpenJarvis backend..."

if ! curl -s http://127.0.0.1:8000/v1/models >/dev/null; then
  echo ""
  echo "FEHLER: OpenJarvis Backend läuft nicht."
  echo ""
  echo "Starte zuerst:"
  echo "cd ~/jarvis/OpenJarvis"
  echo "OLLAMA_MODEL=llama3.2:3b ./scripts/quickstart.sh"
  echo ""
  exit 1
fi

echo "[2/5] Activating Python venv..."
source .venv/bin/activate

echo "[3/5] Updating market data..."
python scripts/market_data.py

echo "[4/5] Generating market briefing..."
python scripts/market_briefing.py --mode morning

echo "[5/5] Done."
echo "Briefing saved in:"
echo "~/jarvis/obsidian/MarketBriefings/"
