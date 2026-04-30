#!/bin/bash
set -e

MODEL_MAIN="llama3.2:3b"
MODEL_SMALL="qwen3:0.6b"
MODEL_ALT="qwen3.5:4b"

echo "=== JARVIS WSL SETUP v2 ==="

echo "[1/8] Updating system..."
sudo apt update
sudo apt upgrade -y

echo "[2/8] Installing system packages..."
sudo apt install -y \
  git curl wget build-essential \
  python3 python3-pip python3-venv \
  ffmpeg unzip zstd

echo "[3/8] Creating Jarvis folders..."
cd ~/jarvis
mkdir -p data logs config obsidian/MarketBriefings obsidian/System

echo "[4/8] Creating Python venv..."
python3 -m venv .venv
source .venv/bin/activate

echo "[5/8] Installing Python requirements..."
pip install --upgrade pip
pip install -r requirements.txt

echo "[6/8] Installing Ollama if needed..."
if ! command -v ollama >/dev/null 2>&1; then
  curl -fsSL https://ollama.com/install.sh | sh
else
  echo "Ollama already installed."
fi

echo "[7/8] Pulling Ollama models..."
ollama pull "$MODEL_MAIN"
ollama pull "$MODEL_SMALL"
ollama pull "$MODEL_ALT"

echo "[8/8] Preparing OpenJarvis..."
if [ ! -d "OpenJarvis" ]; then
  echo "OpenJarvis is not included in this repo."
  echo "Please clone it manually:"
  echo "cd ~/jarvis"
  echo "git clone <OPENJARVIS_REPO_URL> OpenJarvis"
else
  echo "OpenJarvis folder found."
fi

echo "Preparing feedback rules..."
if [ ! -f obsidian/System/feedback_rules.md ]; then
  cat > obsidian/System/feedback_rules.md <<EOF
- Kurz und direkt
- Keine KI-Floskeln
- Bias immer begründen
- Keine erfundenen Daten
EOF
fi

echo "Making scripts executable..."
chmod +x run_market_briefing.sh || true

echo ""
echo "=== WSL SETUP DONE ==="
echo ""
echo "Next tests:"
echo "1) cd ~/jarvis"
echo "2) ./run_market_briefing.sh"
echo ""
echo "If OpenJarvis is installed:"
echo "cd ~/jarvis/OpenJarvis"
echo "OLLAMA_MODEL=$MODEL_MAIN ./scripts/quickstart.sh"
