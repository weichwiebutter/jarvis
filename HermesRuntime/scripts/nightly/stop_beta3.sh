#!/usr/bin/env bash
set -euo pipefail

LOG_DIR="/mnt/d/HermesData/logs"
STATE_DIR="/mnt/d/HermesData/reports/nightly_beta3"
STOP_FILE="${STATE_DIR}/stop_requested.flag"
LOG_FILE="${LOG_DIR}/nightly_beta3_stop.log"
RUNTIME_DIR="${HOME}/jarvis/HermesRuntime"

mkdir -p "${LOG_DIR}" "${STATE_DIR}"

{
  echo "[$(date -Is)] Hermes Nightly Beta3 stop launcher invoked."
  cd "${RUNTIME_DIR}"
  dotnet run --project ./cli/Hermes.Cli.csproj -- nightly-stop-request
  echo "[$(date -Is)] Stop request written: ${STOP_FILE}"

  for attempt in $(seq 1 60); do
    if ! pgrep -f "dotnet.*Hermes.Cli.csproj -- run-nightly-beta3" >/dev/null 2>&1; then
      echo "[$(date -Is)] No run-nightly-beta3 process is running."
      exit 0
    fi

    echo "[$(date -Is)] Waiting for safe stop (${attempt}/60)."
    sleep 5
  done

  echo "[$(date -Is)] Safe stop timeout reached; sending SIGTERM fallback."
  pkill -TERM -f "dotnet.*Hermes.Cli.csproj -- run-nightly-beta3" || true
  echo "[$(date -Is)] SIGTERM fallback sent if process was still running."
} >> "${LOG_FILE}" 2>&1
