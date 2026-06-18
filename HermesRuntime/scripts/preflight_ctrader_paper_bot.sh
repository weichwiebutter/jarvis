#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/ctrader/HermesPaperBot"
GUARD_SCRIPT="$ROOT_DIR/scripts/check_ctrader_paper_bot_forbidden_refs.sh"

fail() {
  echo "FAIL: cTrader paper bot preflight failed"
  exit 1
}

if [[ ! -d "$TARGET_DIR" ]]; then
  fail
fi

required_paths=(
  "$TARGET_DIR/README.md"
  "$TARGET_DIR/HermesPaperBot.cs"
  "$TARGET_DIR/Models"
  "$TARGET_DIR/Services"
  "$TARGET_DIR/tests/forbidden_references_check.md"
)

for path in "${required_paths[@]}"; do
  if [[ ! -e "$path" ]]; then
    fail
  fi
done

find "$TARGET_DIR" -type f | sort

if [[ ! -x "$GUARD_SCRIPT" ]]; then
  fail
fi

if ! bash "$GUARD_SCRIPT"; then
  fail
fi

echo "PASS: cTrader paper bot preflight passed"
exit 0
