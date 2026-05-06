#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"
SESSION_NAME="${JARVIS_SESSION_NAME:-jarvis}"

cd "$PROJECT_ROOT"

mkdir -p logs memory data

if [ -d "venv" ]; then
  source venv/bin/activate
elif [ -d ".venv" ]; then
  source .venv/bin/activate
fi

if ! command -v tmux >/dev/null 2>&1; then
  echo "tmux is not installed."
  echo "Install with: sudo apt install -y tmux"
  exit 1
fi

if tmux has-session -t "$SESSION_NAME" 2>/dev/null; then
  echo "Jarvis background session already running: $SESSION_NAME"
  echo "Attach with: tmux attach -t $SESSION_NAME"
  exit 0
fi

tmux new-session -d -s "$SESSION_NAME" -n jarvis

tmux send-keys -t "$SESSION_NAME:jarvis" "cd '$PROJECT_ROOT'" C-m

if [ -d "venv" ]; then
  tmux send-keys -t "$SESSION_NAME:jarvis" "source venv/bin/activate" C-m
elif [ -d ".venv" ]; then
  tmux send-keys -t "$SESSION_NAME:jarvis" "source .venv/bin/activate" C-m
fi

tmux send-keys -t "$SESSION_NAME:jarvis" "echo 'Jarvis background session ready.'" C-m
tmux send-keys -t "$SESSION_NAME:jarvis" "echo 'Use: python3 agents/jarvis_core.py --speak \"Hallo Jarvis\"'" C-m

echo "Jarvis background session started: $SESSION_NAME"
echo "Attach: tmux attach -t $SESSION_NAME"
echo "Stop:   tmux kill-session -t $SESSION_NAME"
