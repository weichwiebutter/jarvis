#!/usr/bin/env bash
set -euo pipefail

LOG_DIR="/mnt/d/HermesData/logs"
LOG_FILE="${LOG_DIR}/hermes_supervisor.log"
RUNTIME_DIR="${HOME}/jarvis/HermesRuntime"

mkdir -p "${LOG_DIR}"

{
  echo "[$(date -Is)] Hermes Supervisor launcher invoked."

  if pgrep -f "dotnet.*Hermes.Cli.csproj -- supervisor-start" >/dev/null 2>&1; then
    echo "[$(date -Is)] Existing Hermes Supervisor process detected; launcher exits."
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

  echo "[$(date -Is)] Starting Hermes Supervisor in WSL."
  dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --max-runtime-minutes 1440
  exit_code=$?
  echo "[$(date -Is)] Hermes Supervisor exited with code ${exit_code}."
  exit "${exit_code}"
} >> "${LOG_FILE}" 2>&1
