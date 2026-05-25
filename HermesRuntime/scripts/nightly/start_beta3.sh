#!/usr/bin/env bash
set -euo pipefail

LOG_DIR="/mnt/d/HermesData/logs"
LOG_FILE="${LOG_DIR}/nightly_beta3.log"
RUNTIME_DIR="${HOME}/jarvis/HermesRuntime"

mkdir -p "${LOG_DIR}"

{
  echo "[$(date -Is)] Hermes Nightly Beta3 start launcher invoked."

  if pgrep -f "dotnet.*Hermes.Cli.csproj -- run-nightly-beta3" >/dev/null 2>&1; then
    echo "[$(date -Is)] Existing run-nightly-beta3 process detected; launcher exits."
    exit 0
  fi

  cd "${RUNTIME_DIR}"

  if [ -f "${RUNTIME_DIR}/.venv/bin/activate" ]; then
    # Optional only; dotnet does not require it, but this keeps parity with the dev shell.
    # shellcheck disable=SC1091
    source "${RUNTIME_DIR}/.venv/bin/activate"
  elif [ -f "${RUNTIME_DIR}/../.venv/bin/activate" ]; then
    # shellcheck disable=SC1091
    source "${RUNTIME_DIR}/../.venv/bin/activate"
  fi

  current_hour="$(date +%H)"
  current_minute="$(date +%M)"
  if [ "${current_hour}" -eq 22 ] && [ "${current_minute}" -lt 60 ]; then
    seconds_until_23=$(( (23 * 3600) - (10#${current_hour} * 3600 + 10#${current_minute} * 60 + 10#$(date +%S)) ))
    if [ "${seconds_until_23}" -gt 0 ] && [ "${seconds_until_23}" -le 600 ]; then
      echo "[$(date -Is)] Waiting ${seconds_until_23}s for configured 23:00 nightly window."
      sleep "${seconds_until_23}"
    fi
  fi

  echo "[$(date -Is)] Starting Hermes Nightly Beta3 in WSL."
  dotnet run --project ./cli/Hermes.Cli.csproj -- run-nightly-beta3
  exit_code=$?
  echo "[$(date -Is)] Hermes Nightly Beta3 exited with code ${exit_code}."
  exit "${exit_code}"
} >> "${LOG_FILE}" 2>&1
