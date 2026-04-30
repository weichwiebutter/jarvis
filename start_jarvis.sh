#!/bin/bash
set -e

echo "===================================="
echo " STARTING JARVIS SYSTEM"
echo "===================================="

cd ~/jarvis

echo "[1/3] Checking OpenJarvis backend..."

if curl -s http://127.0.0.1:8000/v1/models >/dev/null; then
  echo "Backend already running."
else
  echo "Starting backend in background..."
  cd ~/jarvis/OpenJarvis
  nohup env OLLAMA_MODEL=llama3.2:3b ./scripts/quickstart.sh > ~/jarvis/logs/openjarvis.log 2>&1 &
  cd ~/jarvis
fi

echo "[2/3] Waiting for backend..."

for i in {1..30}; do
  if curl -s http://127.0.0.1:8000/v1/models >/dev/null; then
    echo "Backend ready."
    break
  fi
  sleep 2
done

if ! curl -s http://127.0.0.1:8000/v1/models >/dev/null; then
  echo "FEHLER: Backend konnte nicht gestartet werden."
  echo "Log:"
  tail -n 40 ~/jarvis/logs/openjarvis.log
  exit 1
fi

echo "[3/3] Running market briefing..."
./run_market_briefing.sh

echo "DONE"
