#!/usr/bin/env bash
set -uo pipefail

PROJECT_ROOT="${JARVIS_HOME:-$HOME/jarvis}"
CONFIG_PATH="${JARVIS_STARTUP_CONFIG:-$PROJECT_ROOT/config/jarvis.startup.json}"
DATA_ROOT="${HERMES_DATA_ROOT:-/mnt/d/HermesData}"
BRIDGE_PID_FILE="$DATA_ROOT/reports/bridge/readonly_bridge.pid"
UI_PID_FILE="$DATA_ROOT/reports/control_center/control_center.pid"
STATUS_REPORT="$DATA_ROOT/reports/startup/jarvis_startup_status.json"
MASTER_STATUS_FILE="$DATA_ROOT/reports/master-status/master_status.json"
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

BRIDGE_PORT="$(json_value bridge_port 8787)"

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

port_available() {
  local url="$1"
  curl -fsS "$url" >/dev/null 2>&1
}

supervisor_running() {
  pgrep -f "Hermes.Cli.*supervisor-start" >/dev/null 2>&1 \
    || pgrep -f "dotnet.*Hermes.Cli.csproj -- supervisor-start" >/dev/null 2>&1
}

last_update() {
  local file="$1"
  if [ -f "$file" ]; then
    stat -c '%y' "$file" 2>/dev/null || echo "-"
  else
    echo "-"
  fi
}

echo "Jarvis Startup Status"
echo "---------------------"
echo "Config                $CONFIG_PATH"
echo "Data Root             $DATA_ROOT"

if supervisor_running; then
  echo "Supervisor            running"
else
  echo "Supervisor            stopped"
fi

if port_available "http://127.0.0.1:${BRIDGE_PORT}/bridge/health"; then
  echo "Read-only Bridge      running http://127.0.0.1:${BRIDGE_PORT}"
elif process_running_from_pid_file "$BRIDGE_PID_FILE"; then
  echo "Read-only Bridge      process_running_not_ready"
else
  echo "Read-only Bridge      stopped"
fi

if port_available "http://127.0.0.1:${CONTROL_CENTER_PORT}/"; then
  echo "Control Center        running http://127.0.0.1:${CONTROL_CENTER_PORT}"
elif process_running_from_pid_file "$UI_PID_FILE"; then
  echo "Control Center        process_running_not_ready"
else
  echo "Control Center        stopped"
fi

if [ -f "$MASTER_STATUS_FILE" ]; then
  echo "Master Status File    present"
else
  echo "Master Status File    missing"
fi

echo "Master Last Update    $(last_update "$MASTER_STATUS_FILE")"
echo "Startup Report        $STATUS_REPORT"
echo "Startup Last Update   $(last_update "$STATUS_REPORT")"
echo "Safety                no_auto_trading=true human_review_required=true broker_orders_enabled=false live_trading_enabled=false"
