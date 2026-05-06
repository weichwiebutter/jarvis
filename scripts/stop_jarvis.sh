#!/usr/bin/env bash
set -euo pipefail

SESSION_NAME="${JARVIS_UI_SESSION_NAME:-jarvis-ui}"

echo "Stopping Jarvis UI and background service..."

if command -v tmux >/dev/null 2>&1; then
  if tmux has-session -t "$SESSION_NAME" 2>/dev/null; then
    tmux kill-session -t "$SESSION_NAME"
    echo "Stopped tmux session: $SESSION_NAME"
  else
    echo "No tmux session found: $SESSION_NAME"
  fi
else
  echo "tmux not installed or not available."
fi

echo "Jarvis stopped."
echo ""
echo "Note:"
echo "- Ollama is not stopped by default."
echo "- Hermes is not stopped separately unless it was started inside the Jarvis tmux session."
echo "- This keeps model services available but frees Jarvis UI/background resources."
