#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"
SESSION_NAME="${JARVIS_UI_SESSION_NAME:-jarvis-ui}"

cd "$PROJECT_ROOT"

mkdir -p logs memory data scripts service

if ! command -v tmux >/dev/null 2>&1; then
  echo "tmux is not installed."
  echo "Install with: sudo apt install -y tmux"
  exit 1
fi

if tmux has-session -t "$SESSION_NAME" 2>/dev/null; then
  echo "Jarvis UI session already running: $SESSION_NAME"
  echo "Open:   http://127.0.0.1:7860"
  echo "Attach: tmux attach -t $SESSION_NAME"
  echo "Stop:   tmux kill-session -t $SESSION_NAME"
  exit 0
fi

tmux new-session -d -s "$SESSION_NAME" -n ui

tmux send-keys -t "$SESSION_NAME:ui" "cd '$PROJECT_ROOT'" C-m

if [ -d "venv" ]; then
  tmux send-keys -t "$SESSION_NAME:ui" "source venv/bin/activate" C-m
elif [ -d ".venv" ]; then
  tmux send-keys -t "$SESSION_NAME:ui" "source .venv/bin/activate" C-m
fi

tmux send-keys -t "$SESSION_NAME:ui" "python3 ui_app.py" C-m

tmux new-window -t "$SESSION_NAME" -n service

tmux send-keys -t "$SESSION_NAME:service" "cd '$PROJECT_ROOT'" C-m

if [ -d "venv" ]; then
  tmux send-keys -t "$SESSION_NAME:service" "source venv/bin/activate" C-m
elif [ -d ".venv" ]; then
  tmux send-keys -t "$SESSION_NAME:service" "source .venv/bin/activate" C-m
fi

tmux send-keys -t "$SESSION_NAME:service" "python3 service/background_service.py --heartbeat 10" C-m

echo "Jarvis system started in background."
echo "Open UI:     http://127.0.0.1:7860"
echo "Attach:      tmux attach -t $SESSION_NAME"
echo "Windows:     ui + service"
echo "Status:      python3 service/background_service.py --status"
echo "Stop all:    tmux kill-session -t $SESSION_NAME"
