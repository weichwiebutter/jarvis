# Jarvis UI v1 Design Specification

Status: Draft / Design Specification  
Scope: Future high-quality local Jarvis UI  
Current implementation status: not implemented  
Gradio status: developer/test UI only

## Core Positioning

- Gradio UI = Dev/Test UI.
- Final Jarvis UI = futuristic local AI control center.
- This document is documentation only and does not define an implementation
  change.

## Purpose

This document defines the target design for the future Jarvis UI v1. It is a
product and architecture reference for a later implementation phase.

The current Gradio UI is explicitly only a developer and test surface. It is
useful for validating status modules, manually triggering checks, and exposing
raw JSON during development. It is not the final Jarvis user experience.

The final UI should become a futuristic local AI control center: modern, dark,
animated, modular, high-quality, AI-first, and inspired by the Jarvis /
Iron-Man control-room feeling. The interface should feel like a serious local
command center, not a simple form-based admin panel.

## Design Principles

- Local-first: Jarvis should feel like a local personal AI command center.
- AI-first: Hermes brain state, active agents, model routing, and memory should
  be first-class UI concepts.
- Human-in-the-loop: approvals, warnings, and blocked actions must be obvious.
- High signal density: important status should always be visible without
  hiding everything behind raw JSON.
- Modular panels: each system area should be independently readable and later
  replaceable.
- Dark premium look: restrained dark surfaces, luminous accents, precise
  typography, and subtle motion.
- No hidden automation: the UI must never imply that unsafe autonomous actions
  are happening silently.

## Current Gradio UI Position

The existing Gradio app remains the development and test UI.

It should continue to support:

- Manual status checks.
- Developer-facing JSON inspection.
- Hermes/Jarvis foundation panel validation.
- Safe test controls during development.

It should not be treated as:

- The final Jarvis interface.
- The long-term control center.
- The production UX architecture.
- The animation or visual design baseline.

## Target Experience

The final UI should feel like a live local AI operations cockpit.

Target qualities:

- Modern.
- Dark.
- Animated.
- Modular.
- High-quality.
- AI-first.
- Jarvis/Iron-Man-inspired.
- Control-center-oriented.

The UI should make the user feel that Hermes is actively observing, reasoning,
and coordinating the local system while still requiring explicit human approval
for important actions.

## Main Areas

### Home Dashboard

The first screen should provide the system overview at a glance.

It should show:

- Hermes status.
- Ollama status.
- Active agents.
- Running tasks.
- Market watch.
- Weather.
- Warnings and signals.
- Runtime health.
- Cost/provider status.

### Chat / Conversation

The central interaction area should support natural conversation with Jarvis.

It should include:

- User message input.
- Streaming or staged assistant response later.
- Task context.
- Attached status signals.
- Approval prompts when needed.
- Voice interaction hooks.

### Hermes Brain Panel

The Hermes Brain panel should expose the current reasoning and routing state.

It should show:

- Current route.
- Intent.
- Domain.
- Confidence.
- Selected agent.
- Approval requirement.
- Safety decision.
- Model/provider routing.

### Agent Dashboard

The agent dashboard should show the local agent fleet.

It should include:

- Available agents.
- Planned agents.
- Active agents.
- Agent roles.
- Current task ownership.
- Agent status.
- Visible agent chains / workflow flow.
- Approval requirements.
- Recent agent actions.

### Runtime Control

Runtime controls should make system state visible without hiding risk.

It should show:

- Runtime health.
- Background service status.
- Scheduler status.
- Heartbeat.
- Resource limits.
- Cleanup state.
- Start/stop/restart actions only with confirmation.

### Voice Interface

Voice should feel like a primary Jarvis capability, not an addon.

It should show:

- Microphone state.
- Wake-word state.
- Transcription state.
- TTS state.
- Audio visualizer later.
- Voice provider status.
- Privacy mode.

### Trading Panel

The trading panel should be analysis-first and safety-first.

It should show:

- XAUUSD live quote.
- EURUSD live quote.
- GER40 later.
- Timeframes.
- Analysis mode.
- Setup Watch state.
- Trigger conditions.
- Entry zone.
- Confidence / probability.
- Stop-loss suggestion.
- Take-profit / target zones.
- Invalidation level.
- Prediction feedback status.
- Prediction -> outcome -> evaluation -> learning state.
- Pattern signals.
- `no_auto_trading` visibly and permanently.
- Trade execution disabled until explicit approval.

### Taskline / Activity Feed

The taskline should show what Jarvis/Hermes is doing or planning.

It should include:

- Current tasks.
- Recent events.
- Queued actions.
- Approval waits.
- Research reports.
- Scheduler jobs later.
- Runtime warnings.

### Learning & Memory

Memory should be visible, auditable, and approval-based.

It should show:

- Local learning status.
- Approved memory status.
- Routing hints.
- Skill candidates.
- Shared memory candidates.
- Obsidian knowledge links later.
- What is local-only vs shared.
- What is pending Frank approval.

### Developer Debug

Developer/debug panels should remain available but not dominate the product UI.

They should include:

- Raw status JSON.
- Module availability.
- Import warnings.
- CLI checks.
- Runtime diagnostics.
- Event logs.
- Test surfaces.

### Skills / Tools

Skills and tools should be visible as reviewed capabilities, not hidden
automation.

It should show:

- Skill registry status.
- Active, approved, proposed, and deprecated skills.
- Tool registry status.
- Read-only vs write-capable tools.
- Permission scope.
- Safety flags.
- Review owner.
- MCP/tool gateway status later.

### Research Discovery

Research discovery should surface curated ideas without autonomous changes.

It should show:

- Planned research sources.
- Monitored topics.
- Weekly digest status.
- Candidate ideas for roadmap/masterplan.
- Source/date metadata.
- Review queue state.
- Accepted, rejected, and archived discoveries.

### Cost Optimization

Cost and token usage should be visible before costly work happens.

It should show:

- Provider/model status.
- Current routing choice.
- ChatGPT Codex usage state later.
- OpenRouter credit state later.
- Ollama/local availability.
- Fast Mode policy.
- Local/cloud ratio.
- Cost warnings and approval requirements.

## Permanently Visible Elements

The final UI should keep the following visible or one-click visible at all
times:

- XAUUSD live price.
- EURUSD live price.
- Weather.
- Active agents.
- Running tasks.
- Hermes status.
- Ollama status.
- Runtime warnings.
- Trading signals.
- Setup Watch / trigger status.
- Approval queue indicator.
- Provider/model status.
- `no_auto_trading` indicator.

## Layout Proposal

The preferred layout is a four-zone control center.

### Left Column: Agent Activity / Taskline

Purpose: show what is happening now.

Content:

- Active agents.
- Running tasks.
- Recent actions.
- Approval queue.
- Activity feed.
- Research summaries later.

### Center: Chat + Voice + Main Interaction

Purpose: primary human/Jarvis interaction.

Content:

- Chat.
- Voice controls.
- Current task context.
- Response stream.
- Approval prompts.
- Main command input.

### Right Column: Hermes Brain + Trading + Model Routing

Purpose: expose reasoning, safety, and live decision context.

Content:

- Hermes Brain state.
- Current route/intent/domain.
- Confidence and approval status.
- Trading signals and quote panels.
- Model/provider routing.
- Cost/credit status.

### Bottom Bar: Runtime, Logs, Memory, System Status

Purpose: operational control and diagnostics.

Content:

- Runtime health.
- Heartbeat.
- Logs/audit trail.
- Memory status.
- Storage/disk limits.
- Warnings.
- Local/cloud ratio later.

## Visual Direction

The design should avoid generic dashboards and marketing-page composition.

Preferred style:

- Dark base surfaces.
- Subtle depth.
- Thin luminous outlines.
- High-contrast status colors.
- Motion for state changes, not decoration.
- Compact cards/panels with clear hierarchy.
- Dense but readable operational layout.
- Monospace for IDs, paths, task IDs, and provider/model names.

Avoid:

- Landing-page hero sections.
- Decorative gradients as the main visual idea.
- Overly large empty cards.
- Toy-like sci-fi visuals.
- Hidden status behind too many tabs.
- Raw JSON as the primary user experience.

## Motion and Interaction

Animation should communicate system state.

Examples:

- Soft pulse for active agents.
- Subtle activity lines for running tasks.
- Highlight transitions for new warnings.
- Status changes animated briefly.
- Voice waveform during listening/speaking.
- Streaming response states later.

Motion should not obscure readability or imply actions that are not happening.

## Safety Principles

The final UI must make safety visible.

Required safety principles:

- Approval requests are visible and prominent.
- No hidden actions.
- No hidden trading learnings or signals.
- `no_auto_trading` is always visible in trading areas.
- Cloud cost and credits are visible before costly work.
- Active providers and models are visible.
- Runtime actions require confirmation.
- Trade execution remains disabled until explicit release.
- Tool execution requires clear permission state.
- Tool and skill execution requires review before activation or execution.
- Memory persistence requires review.
- Skills are not activated automatically.
- External calls and provider switches are visible.

## Approval UX

Approval requests should be treated as first-class UI events.

Each approval item should show:

- Request type.
- Originating agent/module.
- What will happen if approved.
- What files/tools/providers are involved.
- Safety flags.
- Cost/risk hints if available.
- Approve/reject controls.
- Audit trail entry later.

## Later Technical Options

The future implementation can evaluate these options:

- React/Vite for a high-quality frontend.
- Tauri for local desktop packaging.
- FastAPI for a local backend/API layer.
- WebSocket or Event Stream for live runtime telemetry.
- Local event store for audit/log views.
- Gradio remains only the development/test UI.

These are not implementation commitments in this document. They are options for
the later UI architecture phase.

## Relationship to Current Foundation Modules

The final UI should eventually render the read-only foundation modules as
proper panels:

- Runtime Supervisor.
- Shared Memory / Multi-PC.
- Skills System.
- Skill Generator.
- Research Discovery.
- Cost Optimization.
- MCP / Tools.
- Reflective Learning.
- Trading Intelligence.

The current Gradio display of these modules is only a bridge for validation.

## Acceptance Criteria For Future Implementation

The future UI implementation should be considered aligned with this
specification when:

- Gradio is still clearly treated as development/test UI only.
- The main UI feels like a modern local AI control center.
- Permanent status elements are visible.
- Hermes Brain state is understandable without raw JSON.
- Agent activity is visible.
- Approvals are prominent.
- Trading safety is visible.
- Provider/model/cost state is visible.
- Runtime actions are confirmed before execution.
- Advanced raw JSON remains available for debugging but is not the primary UX.

## Non-Goals For This Document

This document does not implement:

- React.
- Tauri.
- FastAPI.
- WebSockets.
- Runtime services.
- New UI code.
- Agent execution.
- External API integration.
