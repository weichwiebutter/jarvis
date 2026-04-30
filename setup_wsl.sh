#!/bin/bash
set -e

echo "=== JARVIS WSL SETUP ==="

sudo apt update
sudo apt upgrade -y

sudo apt install -y git curl wget build-essential python3 python3-pip python3-venv ffmpeg unzip zstd

echo "Installing Python venv..."
cd ~/jarvis
python3 -m venv .venv
source .venv/bin/activate

echo "Installing Python requirements..."
pip install --upgrade pip
pip install -r requirements.txt

echo "Installing Ollama..."
if ! command -v ollama >/dev/null 2>&1; then
  curl -fsSL https://ollama.com/install.sh | sh
else
  echo "Ollama already installed."
fi

echo "Pulling Ollama models..."
ollama pull llama3.2:3b
ollama pull qwen3:0.6b
ollama pull qwen3.5:4b

echo "Creating folders..."
mkdir -p data logs obsidian/MarketBriefings obsidian/System config

if [ ! -f obsidian/System/feedback_rules.md ]; then
  echo "- Kurz und direkt" > obsidian/System/feedback_rules.md
  echo "- Keine KI-Floskeln" >> obsidian/System/feedback_rules.md
  echo "- Bias immer begründen" >> obsidian/System/feedback_rules.md
  echo "- Keine erfundenen Daten" >> obsidian/System/feedback_rules.md
fi

chmod +x run_market_briefing.sh

echo "=== WSL SETUP DONE ==="
