#!/bin/bash
set -euo pipefail

MODEL="${JARVIS_LLM_LARGE_MODEL:-llama3.2:8b}"

PROMPT="$(cat)"

ollama run "$MODEL" "$PROMPT" 2>/dev/null
