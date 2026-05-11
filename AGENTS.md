# Jarvis Development Rules for Codex

## Project roles

- Jarvis is the UI, runtime, voice, status and control layer.
- Hermes is the brain, planner, learning agent and delegation layer.
- Ollama provides local models.
- OpenRouter/OpenAI/Gemini may be used only through explicit provider layers.
- Browser-based ChatGPT/Gemini/Copilot are manual-assist modes, not scraped automation.
- Codex is a coding worker, not the system brain.

## Safety rules

Codex must not:
- run git push
- delete files without explicit approval
- edit config/settings.env
- expose secrets or API keys
- modify runtime data in logs/, memory/, .hermes/, data/, obsidian/
- install large dependencies without asking
- change architecture beyond the requested scope

Codex should:
- make complete-file changes when requested
- keep changes small and reviewable
- run py_compile after Python edits
- explain changed files
- show suggested git commands but not push
- preserve human-in-the-loop approval

## Current architecture

Important files:
- agents/core/hermes_router.py
- agents/core/hermes_decision.py
- agents/core/hermes_planner.py
- agents/core/capability_registry.py
- agents/core/agent_creation_request.py
- agents/core/delegation_contract.py
- agents/core/delegation_executor.py
- agents/core/runtime_router.py
- agents/core/executor_bridge.py
- ui_app.py

## Standard validation

After Python changes run:

python3 -m py_compile ui_app.py
python3 -m py_compile agents/core/*.py
python3 -m py_compile service/background_service.py
