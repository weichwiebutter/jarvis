# Jarvis Learning UI v1

Status: Draft / UX Architecture  
Scope: Future Learning, Feedback, Approval, Memory, and Runtime Event UI  
Current implementation status: not implemented  
Gradio status: developer/test UI only

## Purpose

This document defines the target shape for a future Jarvis Learning UI v1. The
Learning UI is not just a chat window. It is a Learning and Approval Control
Center where Hermes can later expose feedback, mistakes, routing decisions,
memory candidates, skill ideas, and trading prediction outcomes for controlled
human review.

No implementation is part of this document. It does not change runtime logic,
does not start services, does not add WebSockets, and does not persist
learnings.

## Core Positioning

Jarvis is the local UI, runtime, voice, status, and control layer. Hermes is the
brain, planner, routing, learning, and orchestration layer.

The Learning UI should make Hermes' learning process visible and controllable:

- What happened?
- Which route, model, and agent were used?
- Was the answer useful?
- Did the routing fail?
- What should Hermes learn from this?
- What must be approved before it becomes memory, a routing hint, or a skill?

The UI must support learning without hidden automation. Hermes may propose
learning candidates, but Frank must approve durable changes.

## Main Areas

### A. Conversation / Voice Panel

The central interaction panel remains the primary place for conversation, but
it is not the entire learning system.

It should show:

- Chat messages.
- Voice input and output state.
- Current user context.
- Active task summary.
- Attached files or runtime references later.
- Current conversation/session id later.
- Pending approval prompts if a reply proposes durable learning.

Learning-related behavior:

- A task result can be marked for feedback directly from the conversation.
- Voice interactions should generate the same reviewable learning context as
  typed interactions.
- Conversation context should be visible enough to judge whether a learning
  candidate is valid.

### B. Hermes Brain Panel

The Hermes Brain panel explains why Hermes behaved the way it did.

It should show:

- Routing decision.
- Used model.
- Used provider.
- Used agent.
- Domain classification.
- Confidence.
- Decision reasons.
- Safety gates.
- Approval requirement.
- Fallbacks used.

Learning-related behavior:

- Users can flag wrong routing directly from this panel.
- Low-confidence decisions can be marked as learning candidates.
- The UI should show whether a decision was based on rules, routing hints,
  memory, model classification, or fallback behavior.

### C. Feedback Panel

The Feedback panel turns task outcomes into structured review data.

It should ask:

- Was the answer helpful?
- Was the route wrong?
- Was the selected agent appropriate?
- Was the selected model/provider appropriate?
- Did the answer miss context?
- Did the task require a different workflow?
- Is there a manual improvement suggestion?
- Should this become a routing hint, memory candidate, or skill idea?

Suggested controls:

- Helpful / not helpful.
- Correct route / wrong route.
- Correct agent / wrong agent.
- Confidence adjustment.
- Manual rating.
- Free-form improvement note.
- Mark as recurring pattern.
- Mark as one-off issue.

### D. Learning Queue

The Learning Queue is the staging area before anything becomes durable
knowledge.

It should show:

- New learnings.
- Failure patterns.
- Routing hints.
- New skill ideas.
- Memory candidates.
- Model/provider routing suggestions.
- Retry strategy suggestions.
- Confidence tuning suggestions.
- Trading prediction feedback candidates.

Each queue item should include:

- Source task or conversation.
- Generated timestamp.
- Candidate type.
- Summary.
- Evidence/context.
- Proposed target: memory, routing hint, skill candidate, roadmap, archive.
- Risk level.
- Required reviewer.
- Current status.

Queue states:

- `candidate_created`
- `queued_for_review`
- `needs_more_context`
- `approved`
- `rejected`
- `deferred`
- `persisted_if_approved`

### E. Approval Center

The Approval Center is the explicit gate before Hermes can persist learning or
activate future skills.

Available actions:

- `approve`
- `reject`
- `defer`
- `persist_to_memory`
- `convert_to_skill_candidate`
- `convert_to_routing_hint`
- `archive`

Approval rules:

- Durable memory requires approval.
- Shared memory requires approval.
- Routing hints require approval.
- Skill candidates require review before activation.
- Generated skills are never auto-active.
- Trading learning never enables trade execution.

The UI should show exactly what will happen before approval. No approval action
should silently trigger unrelated runtime changes.

### F. Activity Timeline

The Activity Timeline makes the learning context auditable.

It should show:

- Tasks.
- Warnings.
- Failures.
- Recoveries.
- Runtime Events.
- Routing decisions.
- Feedback received.
- Reflection generated.
- Candidate queued.
- Approval decision.
- Optional persistence result.

Timeline entries should connect to Runtime Event Bus events where possible and
remain readable for non-developers.

### G. Trading Learning Panel

The Trading Learning Panel is analysis-only and safety-first.

It should show:

- Prediction.
- Symbol.
- Timeframe.
- Entry context if applicable.
- Outcome/result later.
- Confidence.
- Feature notes later.
- Session context later.
- Later manual evaluation.
- Prediction feedback status.
- `no_auto_trading` permanently visible.

Rules:

- No auto-trading.
- No orders.
- No broker execution.
- No hidden trading memory updates.
- Trading prediction feedback requires review before durable learning.
- TRADE connection remains disabled until explicit approval in a separate
  future architecture step.

## Learning Flow

The standard learning flow should be:

1. Task executed.
2. Result evaluated by user or explicit review signal.
3. Hermes reflection generated.
4. Candidate created.
5. Candidate appears in Learning Queue.
6. User approval decision.
7. Optional persistence only after approval.
8. Approved learning may become memory, routing hint, skill candidate, or
   roadmap item.

No durable learning should occur directly from a model response. The UI is the
control point between generated candidates and persistent knowledge.

## Safety Principles

The Learning UI must enforce and visibly communicate:

- `no_auto_learning_without_review`
- `no_hidden_memory_updates`
- `visible_runtime_decisions`
- `visible_model_routing`
- `no_auto_trading`
- `auditability_required`
- `human_review_required`
- `no_unreviewed_skill_activation`
- `no_secret_capture`
- `no_silent_shared_memory_sync`

Safety expectations:

- All durable learning paths are explicit.
- All model/provider routing decisions are visible.
- All approval states are visible.
- Runtime decisions should be explainable.
- Memory writes should show target store and scope before approval.
- Shared memory sync must only use approved memory.
- Secrets and `.env.local` content must never enter learning candidates.

## Future Technical Integration

The Learning UI should later integrate with:

- Runtime Event Bus.
- Foundation Registry.
- Shared Memory.
- Reflective Learning.
- Hermes Skills System.
- Skill Generator.
- MCP / Tool Registry.
- Trading Intelligence.
- Jarvis Control Center approval queue.
- Future FastAPI backend.
- Future WebSocket/Event Stream.
- Future Tauri/React desktop UI.

Possible data surfaces:

- `runtime_event_bus_panel` for event flow.
- `reflective_learning_panel` for candidate generation policy.
- `shared_memory_panel` for local/shared separation.
- `skills_panel` and `skill_generator_panel` for skill candidate review.
- `foundation_registry_panel` for module inventory and safety levels.
- `trading_intelligence_panel` for prediction feedback and no-auto-trading
  guardrails.

## Why Gradio Is Not Enough

The current Gradio UI is useful for development and manual status validation,
but it is not enough for the final Learning UI.

Limitations:

- It is form- and tab-oriented, not a real control center.
- It does not provide a rich persistent activity model.
- It is weak for dense multi-panel workflows.
- It is not ideal for animated state, live event streams, or high-quality voice
  interaction.
- It does not naturally support complex approval queues with timeline context.
- It is not the right long-term foundation for a polished local AI desktop
  experience.

Gradio should remain a developer/test UI for safe inspection, status checks,
and raw JSON validation.

## Why A Desktop Control Center Is Needed

Learning and approval require a UI that can show multiple pieces of state at
once:

- Conversation.
- Voice.
- Brain/routing explanation.
- Model/provider choice.
- Active tasks.
- Runtime events.
- Feedback controls.
- Candidate queue.
- Approval decisions.
- Memory target.
- Trading safety state.

A future Tauri/React desktop control center can provide:

- Persistent local-first experience.
- High-quality dark Jarvis-style interface.
- Live panels without hiding important state.
- Keyboard and voice workflows.
- Explicit approval modals and queues.
- Event-stream visualization.
- Better separation between user-facing controls and developer debug data.

## Non-Goals

- No UI implementation.
- No React/Tauri/FastAPI code.
- No runtime logic changes.
- No WebSocket implementation.
- No learning persistence.
- No service startup.
- No trading execution.

