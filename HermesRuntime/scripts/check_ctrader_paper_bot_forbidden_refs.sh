#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/ctrader/HermesPaperBot"

FORBIDDEN_REGEX='ExecuteMarketOrder|PlaceLimitOrder|PlaceStopOrder|ModifyPosition|ClosePosition|CancelPendingOrder|PendingOrders|Positions\.Modify|TradeResult|TradeOperation|Account|Positions|Orders|Volume|Symbol\.QuantityToVolumeInUnits'
ALLOWED_PATH_REGEX='^(.*ctrader/HermesPaperBot/tests/forbidden_references_check\.md|.*ctrader/HermesPaperBot/README\.md|.*docs/trading/ctrader_paper_bot_skeleton_safety_audit_v1\.md|.*docs/trading/ctrader_paper_bot_skeleton_spec_v1\.md|.*docs/trading/ctrader_bot_paper_runtime_scope_v1\.md):'

if ! command -v grep >/dev/null 2>&1; then
  echo "FAIL: grep is required"
  exit 1
fi

matches="$(grep -RInE "$FORBIDDEN_REGEX" "$TARGET_DIR" "$ROOT_DIR/docs/trading/ctrader_paper_bot_skeleton_safety_audit_v1.md" "$ROOT_DIR/docs/trading/ctrader_paper_bot_skeleton_spec_v1.md" "$ROOT_DIR/docs/trading/ctrader_bot_paper_runtime_scope_v1.md" 2>/dev/null || true)"

if [[ -z "$matches" ]]; then
  echo "PASS: no forbidden cTrader trading/order references found in HermesPaperBot C# files"
  exit 0
fi

bad_matches="$(printf '%s\n' "$matches" | while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  if [[ "$line" =~ ^.*ctrader/HermesPaperBot/.*\.cs: ]]; then
    printf '%s\n' "$line"
  elif [[ ! "$line" =~ $ALLOWED_PATH_REGEX ]]; then
    printf '%s\n' "$line"
  fi
done)"

if [[ -z "$bad_matches" ]]; then
  echo "PASS: no forbidden cTrader trading/order references found in HermesPaperBot C# files"
  exit 0
fi

echo "FAIL: forbidden cTrader trading/order references detected"
printf '%s\n' "$bad_matches"
exit 1
