#!/usr/bin/env bash
set -uo pipefail

PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"
CONFIG_PATH="${JARVIS_STARTUP_CONFIG:-$PROJECT_ROOT/config/jarvis.startup.json}"
RUNTIME_DIR="$PROJECT_ROOT/HermesRuntime"
DATA_ROOT="${HERMES_DATA_ROOT:-/mnt/d/HermesData}"
REPORT_DIR="$DATA_ROOT/reports/startup"
BRIDGE_PID_FILE="$DATA_ROOT/reports/bridge/readonly_bridge.pid"
UI_PID_FILE="$DATA_ROOT/reports/control_center/control_center.pid"
DEFAULT_LOG="$DATA_ROOT/logs/jarvis_startup.log"
STATUS_REPORT="$REPORT_DIR/jarvis_startup_status.json"
CONTROL_CENTER_PORT="${JARVIS_CONTROL_CENTER_PORT:-5173}"

json_value() {
  local key="$1"
  local fallback="$2"

  if [ ! -f "$CONFIG_PATH" ] || ! command -v python3 >/dev/null 2>&1; then
    printf '%s' "$fallback"
    return
  fi

  python3 - "$CONFIG_PATH" "$key" "$fallback" <<'PY'
import json
import sys

path, key, fallback = sys.argv[1], sys.argv[2], sys.argv[3]
try:
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    value = data.get(key, fallback)
except Exception:
    value = fallback

if isinstance(value, bool):
    print(str(value).lower())
else:
    print(value)
PY
}

LOG_PATH="$(json_value log_path "$DEFAULT_LOG")"
BRIDGE_PORT="$(json_value bridge_port 8787)"

mkdir -p "$(dirname "$LOG_PATH")" "$REPORT_DIR"

log() {
  local message="[$(date -Is)] $*"
  echo "$message"
  echo "$message" >> "$LOG_PATH"
}

terminate_pid_file() {
  local label="$1"
  local pid_file="$2"
  if [ ! -f "$pid_file" ]; then
    log "$label PID file not present."
    return 0
  fi

  local pid
  pid="$(cat "$pid_file" 2>/dev/null || true)"
  if [ -z "$pid" ]; then
    log "$label PID file is empty."
    rm -f "$pid_file"
    return 0
  fi

  if ps -p "$pid" >/dev/null 2>&1; then
    log "Stopping $label pid=$pid."
    /bin/kill "$pid" >/dev/null 2>&1 || true
    for _ in $(seq 1 20); do
      if ! ps -p "$pid" >/dev/null 2>&1; then
        break
      fi
      sleep 0.5
    done
  else
    log "$label pid=$pid is not running."
  fi

  rm -f "$pid_file"
}

stop_matching_processes() {
  local label="$1"
  local pattern="$2"
  local pids
  pids="$(pgrep -f "$pattern" 2>/dev/null || true)"
  if [ -z "$pids" ]; then
    log "No extra $label processes matched."
    return 0
  fi

  for pid in $pids; do
    if [ "$pid" = "$$" ]; then
      continue
    fi
    log "Stopping $label matched pid=$pid."
    /bin/kill "$pid" >/dev/null 2>&1 || true
  done
}

write_status_report() {
  if command -v python3 >/dev/null 2>&1; then
    python3 - "$STATUS_REPORT" <<'PY'
import json
import os
import sys
from datetime import datetime, timezone

path = sys.argv[1]
report = {
    "status_version": "jarvis_startup_status_v1",
    "updated_at_utc": datetime.now(timezone.utc).isoformat(),
    "supervisor": "stop_requested",
    "bridge": "stop_requested",
    "control_center": "stop_requested",
    "no_auto_trading": True,
    "human_review_required": True,
    "broker_orders_enabled": False,
    "live_trading_enabled": False,
}
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as handle:
    json.dump(report, handle, separators=(",", ":"))
PY
  fi
}

log "Jarvis Stop Orchestrator invoked."

terminate_pid_file "React Control Center" "$UI_PID_FILE"
stop_matching_processes "React Control Center" "vite.*127.0.0.1"
stop_matching_processes "React Control Center launcher" "npm run dev -- --host 127.0.0.1"

terminate_pid_file "Hermes Read-only Bridge" "$BRIDGE_PID_FILE"
stop_matching_processes "Hermes Read-only Bridge" "Hermes.Cli.*readonly-bridge --url http://127.0.0.1:${BRIDGE_PORT}/"
stop_matching_processes "Hermes Read-only Bridge launcher" "dotnet run --project .*Hermes.Cli.csproj -- readonly-bridge --url http://127.0.0.1:${BRIDGE_PORT}/"
stop_matching_processes "Hermes Read-only Bridge" "Hermes.Cli.*readonly-bridge"
stop_matching_processes "Hermes Read-only Bridge launcher" "dotnet run --project .*Hermes.Cli.csproj -- readonly-bridge"

if [ -d "$RUNTIME_DIR" ]; then
  log "Requesting safe Hermes Supervisor stop."
  if (cd "$RUNTIME_DIR" && dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-stop-request); then
    log "Supervisor stop request written."
  else
    log "Warning: supervisor-stop-request failed."
  fi
else
  log "Warning: Runtime directory missing: $RUNTIME_DIR"
fi

write_status_report
log "Jarvis stop request complete. Supervisor exits on its next safe loop."
log "Safety: no_auto_trading=true human_review_required=true broker_orders_enabled=false live_trading_enabled=false"
