#!/usr/bin/env bash
set -euo pipefail

# Jarvis Local Runtime Setup
# Richtet lokale Dateien ein, die bewusst NICHT in GitHub liegen.
#
# Nutzung:
#   cd ~/jarvis
#   chmod +x installer/setup_local_runtime.sh
#   ./installer/setup_local_runtime.sh

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "Jarvis Local Runtime Setup"
echo "Projekt: $PROJECT_ROOT"
echo ""

echo "[1/9] Ordnerstruktur erstellen..."
mkdir -p logs
mkdir -p memory
mkdir -p memory/briefings
mkdir -p data
mkdir -p obsidian
mkdir -p reports
mkdir -p backups
mkdir -p .hermes
mkdir -p config

echo "[2/9] .gitignore Runtime-Regeln sicherstellen..."
touch .gitignore

append_gitignore() {
  local line="$1"
  if ! grep -qxF "$line" .gitignore; then
    echo "$line" >> .gitignore
  fi
}

append_gitignore ""
append_gitignore "# Local runtime files"
append_gitignore "venv/"
append_gitignore ".venv/"
append_gitignore "logs/"
append_gitignore "memory/*.json"
append_gitignore "memory/briefings/"
append_gitignore "data/"
append_gitignore "obsidian/"
append_gitignore "reports/"
append_gitignore "backups/"
append_gitignore ".hermes/"
append_gitignore "config/settings.env"
append_gitignore "*.mp3"
append_gitignore "*.wav"
append_gitignore "__pycache__/"
append_gitignore "*.pyc"

echo "[3/9] Python venv erstellen/prüfen..."
if [ ! -d "venv" ]; then
  python3 -m venv venv
fi

echo "[4/9] venv aktivieren..."
# shellcheck disable=SC1091
source venv/bin/activate

echo "[5/9] pip aktualisieren..."
python3 -m pip install --upgrade pip wheel "setuptools<82"

echo "[6/9] requirements installieren..."
if [ -f "requirements.txt" ]; then
  pip install -r requirements.txt
else
  echo "WARNUNG: requirements.txt nicht gefunden. Installiere Basis-Pakete."
  pip install gradio openai-whisper edge-tts sounddevice requests
fi

echo "[7/9] Systemtools prüfen..."
missing_tools=()

command -v tmux >/dev/null 2>&1 || missing_tools+=("tmux")
command -v mpg123 >/dev/null 2>&1 || missing_tools+=("mpg123")
command -v git >/dev/null 2>&1 || missing_tools+=("git")

if [ "${#missing_tools[@]}" -gt 0 ]; then
  echo ""
  echo "Folgende Systemtools fehlen:"
  printf ' - %s\n' "${missing_tools[@]}"
  echo ""
  echo "Installiere sie unter Ubuntu/WSL mit:"
  echo "sudo apt update && sudo apt install -y tmux mpg123 git"
else
  echo "Systemtools OK."
fi

echo "[8/9] Lokale settings.env Vorlage erstellen..."
if [ ! -f "config/settings.env" ]; then
  cat > config/settings.env << 'EOF'
# Jarvis local settings
# Diese Datei NICHT committen.

JARVIS_ENV=local
JARVIS_HOST=127.0.0.1
JARVIS_PORT=7860

# Optional:
# OPENROUTER_API_KEY=
# REDDIT_CLIENT_ID=
# REDDIT_CLIENT_SECRET=
# REDDIT_USERNAME=
# REDDIT_PASSWORD=
# REDDIT_USER_AGENT=script:jarvis:v1.0 by u/your_username

# Voice defaults
JARVIS_TTS_VOICE=de-DE-ConradNeural
JARVIS_TTS_SPEED=0.9
JARVIS_TTS_PITCH=-12
EOF
  echo "config/settings.env erstellt."
else
  echo "config/settings.env existiert bereits."
fi

echo "[9/9] Ollama/Hermes prüfen..."
if command -v ollama >/dev/null 2>&1; then
  echo "Ollama gefunden:"
  ollama --version || true
else
  echo "WARNUNG: ollama nicht gefunden."
fi

if command -v hermes >/dev/null 2>&1; then
  echo "Hermes gefunden:"
  hermes --version || true
else
  echo "WARNUNG: hermes nicht gefunden."
fi

echo ""
echo "Setup abgeschlossen."
echo ""
echo "Nächste Tests:"
echo "source venv/bin/activate"
echo "python3 -m py_compile ui_app.py"
echo "./scripts/start_jarvis_ui.sh"
echo ""
echo "UI:"
echo "http://127.0.0.1:7860"
