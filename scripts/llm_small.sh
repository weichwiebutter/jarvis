#!/bin/bash
set -euo pipefail

MODEL="${JARVIS_LLM_SMALL_MODEL:-llama3.2:3b}"

PROMPT="$(cat)"

ollama run "$MODEL" "$PROMPT" 2>/dev/null
