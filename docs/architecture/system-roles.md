# Jarvis System Roles
Version: 1.0
Status: ACTIVE

## Core Rule

Jarvis is the interface.
Hermes is the brain.

---

## Jarvis

Jarvis is responsible for:

- UI
- Voice input/output
- Start/stop controls
- Status display
- User interaction
- Passing structured tasks to Hermes
- Showing results
- Enforcing approval boundaries

Jarvis does not make strategic decisions.

---

## Hermes

Hermes is responsible for:

- reasoning
- planning
- learning
- delegation
- deciding between local and external models
- deciding when to use Ollama
- deciding when to use OpenRouter
- deciding when to create or use specialized agents
- preparing execution plans

Hermes is the central cognitive layer.

---

## Ollama

Ollama is used for:

- local inference
- low-cost tasks
- fast responses
- privacy-sensitive local work
- small/medium model execution

Hermes decides when Ollama is sufficient.

---

## OpenRouter

OpenRouter is used for:

- stronger external models
- complex reasoning
- difficult planning
- fallback when local models are insufficient

Hermes decides when OpenRouter is needed.

---

## Specialized Agents

Specialized agents are created or selected by Hermes.

Examples:

- memory_agent
- coding_agent
- research_agent
- trading_agent
- office_agent
- improvement_agent
- executor_agent

Agents are tools/specialists, not the brain.

---

## Executor

Executor performs approved actions.

Executor does not decide.
Executor does not plan.
Executor does not bypass approval.

---

## Final Architecture

User
→ Jarvis
→ Hermes
→ Ollama / OpenRouter / Specialized Agents
→ Executor if approved
→ Jarvis displays result
