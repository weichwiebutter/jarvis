#!/usr/bin/env bash
set -uo pipefail

PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"
CONFIG_PATH="${JARVIS_STARTUP_CONFIG:-$PROJECT_ROOT/config/jarvis.startup.json}"
RUNTIME_DIR="$PROJECT_ROOT/HermesRuntime"
CONTROL_CENTER_DIR="$PROJECT_ROOT/ui/jarvis-control-center"
DATA_ROOT="${HERMES_DATA_ROOT:-/mnt/d/HermesData}"
REPORT_DIR="$DATA_ROOT/reports/startup"
BRIDGE_REPORT_DIR="$DATA_ROOT/reports/bridge"
UI_REPORT_DIR="$DATA_ROOT/reports/control_center"
DEFAULT_LOG="$DATA_ROOT/logs/jarvis_startup.log"
BRIDGE_LOG="$DATA_ROOT/logs/hermes_readonly_bridge.log"
UI_LOG="$DATA_ROOT/logs/jarvis_control_center.log"
SUPERVISOR_STOP_FLAG="$DATA_ROOT/reports/supervisor/supervisor_stop_requested.flag"
BRIDGE_PID_FILE="$BRIDGE_REPORT_DIR/readonly_bridge.pid"
UI_PID_FILE="$UI_REPORT_DIR/control_center.pid"
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

is_true() {
  case "${1,,}" in
    true|1|yes|y|on) return 0 ;;
    *) return 1 ;;
  esac
}

LOG_PATH="$(json_value log_path "$DEFAULT_LOG")"
START_SUPERVISOR="$(json_value start_supervisor true)"
START_BRIDGE="$(json_value start_bridge true)"
START_CONTROL_CENTER="$(json_value start_control_center false)"
CONTROL_CENTER_MODE="$(json_value control_center_mode dev)"
BRIDGE_PORT="$(json_value bridge_port 8787)"
WRITE_MASTER_STATUS="$(json_value write_master_status_on_start true)"

mkdir -p "$(dirname "$LOG_PATH")" "$REPORT_DIR" "$BRIDGE_REPORT_DIR" "$UI_REPORT_DIR" "$DATA_ROOT/logs"

log() {
  local message="[$(date -Is)] $*"
  echo "$message"
  echo "$message" >> "$LOG_PATH"
}

port_available() {
  local url="$1"
  curl -fsS "$url" >/dev/null 2>&1
}

process_running_from_pid_file() {
  local pid_file="$1"
  if [ ! -f "$pid_file" ]; then
    return 1
  fi

  local pid
  pid="$(cat "$pid_file" 2>/dev/null || true)"
  if [ -z "$pid" ]; then
    return 1
  fi

  ps -p "$pid" >/dev/null 2>&1
}

start_detached() {
  local log_file="$1"
  shift

  if command -v setsid >/dev/null 2>&1; then
    setsid nohup "$@" </dev/null >> "$log_file" 2>&1 &
  else
    nohup "$@" </dev/null >> "$log_file" 2>&1 &
  fi

  echo $!
}

supervisor_running() {
  pgrep -f "Hermes.Cli.*supervisor-start" >/dev/null 2>&1 \
    || pgrep -f "dotnet.*Hermes.Cli.csproj -- supervisor-start" >/dev/null 2>&1
}

write_status_report() {
  local supervisor_status="$1"
  local bridge_status="$2"
  local control_center_status="$3"
  local master_status_file="$4"
  local bridge_url="http://127.0.0.1:${BRIDGE_PORT}"
  local control_center_url="http://127.0.0.1:${CONTROL_CENTER_PORT}"

  if command -v python3 >/dev/null 2>&1; then
    python3 - "$STATUS_REPORT" "$supervisor_status" "$bridge_status" "$control_center_status" "$master_status_file" "$bridge_url" "$control_center_url" <<'PY'
import json
import os
import sys
from datetime import datetime, timezone

path, supervisor, bridge, control_center, master_status_file, bridge_url, control_center_url = sys.argv[1:]
report = {
    "status_version": "jarvis_startup_status_v1",
    "updated_at_utc": datetime.now(timezone.utc).isoformat(),
    "supervisor": supervisor,
    "bridge": bridge,
    "control_center": control_center,
    "bridge_url": bridge_url,
    "control_center_url": control_center_url,
    "master_status_file": master_status_file,
    "master_status_exists": os.path.exists(master_status_file),
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

log "Jarvis Startup Orchestrator invoked."
log "Project root: $PROJECT_ROOT"
log "Config: $CONFIG_PATH"

supervisor_status="disabled"
bridge_status="disabled"
control_center_status="disabled"
master_status_file="$DATA_ROOT/reports/master-status/master_status.json"

if is_true "$START_SUPERVISOR"; then
  if supervisor_running && [ -f "$SUPERVISOR_STOP_FLAG" ]; then
    log "Supervisor stop request is present; waiting for safe exit before restart."
    for _ in $(seq 1 120); do
      if ! supervisor_running; then
        break
      fi
      sleep 1
    done
  fi

  if supervisor_running; then
    supervisor_status="already_running"
    log "Hermes Supervisor already running."
  else
    log "Starting Hermes Supervisor background mode."
    if (cd "$RUNTIME_DIR" && dotnet run --project ./cli/Hermes.Cli.csproj -- supervisor-start --background); then
      supervisor_status="started"
    else
      supervisor_status="failed"
      log "Warning: Hermes Supervisor start failed."
    fi
  fi
fi

if is_true "$WRITE_MASTER_STATUS"; then
  log "Writing initial Hermes Master Status Snapshot."
  if (cd "$RUNTIME_DIR" && dotnet run --project ./cli/Hermes.Cli.csproj -- write-master-status); then
    log "Master Status Snapshot written: $master_status_file"
  else
    log "Warning: write-master-status failed."
  fi
fi

if is_true "$START_BRIDGE"; then
  bridge_url="http://127.0.0.1:${BRIDGE_PORT}/bridge/health"
  if port_available "$bridge_url"; then
    bridge_status="already_running"
    log "Hermes Read-only Bridge already running on port $BRIDGE_PORT."
  else
    log "Starting Hermes Read-only Bridge on port $BRIDGE_PORT."
    (
      cd "$RUNTIME_DIR" || exit 1
      start_detached "$BRIDGE_LOG" dotnet run --project ./cli/Hermes.Cli.csproj -- readonly-bridge --url "http://127.0.0.1:${BRIDGE_PORT}/" > "$BRIDGE_PID_FILE"
    )

    bridge_status="starting"
    for _ in $(seq 1 20); do
      if port_available "$bridge_url"; then
        bridge_status="started"
        break
      fi
      sleep 0.5
    done

    if [ "$bridge_status" != "started" ]; then
      log "Warning: Bridge did not become ready within startup window."
    fi
  fi
fi

if is_true "$START_CONTROL_CENTER"; then
  if [ "$CONTROL_CENTER_MODE" != "dev" ]; then
    control_center_status="skipped_unsupported_mode"
    log "Control Center mode '$CONTROL_CENTER_MODE' is not supported by startup v1."
  elif port_available "http://127.0.0.1:${CONTROL_CENTER_PORT}/"; then
    control_center_status="already_running"
    log "React Control Center already running on port $CONTROL_CENTER_PORT."
  elif [ ! -d "$CONTROL_CENTER_DIR" ]; then
    control_center_status="missing_directory"
    log "Warning: Control Center directory missing: $CONTROL_CENTER_DIR"
  elif [ ! -d "$CONTROL_CENTER_DIR/node_modules" ]; then
    control_center_status="missing_node_modules"
    log "Warning: node_modules missing. Run npm install in $CONTROL_CENTER_DIR before autostart."
  else
    log "Starting React Control Center dev server on port $CONTROL_CENTER_PORT."
    (
      cd "$CONTROL_CENTER_DIR" || exit 1
      start_detached "$UI_LOG" npm run dev -- --host 127.0.0.1 > "$UI_PID_FILE"
    )

    control_center_status="starting"
    for _ in $(seq 1 20); do
      if port_available "http://127.0.0.1:${CONTROL_CENTER_PORT}/"; then
        control_center_status="started"
        break
      fi
      sleep 0.5
    done

    if [ "$control_center_status" != "started" ]; then
      log "Warning: Control Center did not become ready within startup window."
    fi
  fi
fi

write_status_report "$supervisor_status" "$bridge_status" "$control_center_status" "$master_status_file"

log "Startup result: supervisor=$supervisor_status bridge=$bridge_status control_center=$control_center_status"
log "Startup status report: $STATUS_REPORT"
log "Safety: no_auto_trading=true human_review_required=true broker_orders_enabled=false live_trading_enabled=false"
