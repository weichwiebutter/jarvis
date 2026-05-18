from __future__ import annotations

from agents.core.hermes_ui_status import build_hermes_ui_status


REQUIRED_TOP_LEVEL_KEYS = {
    "generated_at",
    "brain",
    "agents",
    "runtime",
    "system_health",
    "ui_panels",
}

REQUIRED_UI_PANELS = {
    "chat_panel",
    "hermes_brain_panel",
    "agent_dashboard_panel",
    "runtime_control_panel",
    "learning_memory_panel",
    "developer_debug_panel",
    "voice_panel",
    "trading_panel",
    "activity_feed_panel",
    "taskline_panel",
    "home_dashboard_panel",
    "runtime_supervisor_panel",
    "shared_memory_panel",
    "skills_panel",
    "research_discovery_panel",
    "cost_optimization_panel",
    "skill_generator_panel",
    "mcp_tools_panel",
    "reflective_learning_panel",
    "trading_intelligence_panel",
    "foundation_registry_panel",
}


def test_hermes_ui_status_schema_contains_required_keys() -> None:
    status = build_hermes_ui_status()

    missing_top_level_keys = REQUIRED_TOP_LEVEL_KEYS - set(status)
    assert not missing_top_level_keys

    ui_panels = status["ui_panels"]
    assert isinstance(ui_panels, dict)

    missing_panels = REQUIRED_UI_PANELS - set(ui_panels)
    assert not missing_panels

    system_health = status["system_health"]
    assert isinstance(system_health, dict)
    assert "warnings" in system_health
    assert isinstance(system_health["warnings"], list)


def test_hermes_ui_status_trading_task_does_not_crash() -> None:
    status = build_hermes_ui_status("Analysiere XAUUSD auf M15")

    assert isinstance(status, dict)
    assert isinstance(status.get("ui_panels"), dict)

    brain = status.get("brain")
    assert isinstance(brain, dict)

    route_markers = [
        brain.get("domain"),
        brain.get("agent_domain"),
        brain.get("intent"),
        brain.get("route"),
    ]
    present_markers = [str(marker).lower() for marker in route_markers if marker]

    if present_markers:
        assert any("trading" in marker for marker in present_markers)
