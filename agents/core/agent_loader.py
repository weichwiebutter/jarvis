#!/usr/bin/env python3
"""
Dynamic Agent Loader

Lädt Agenten dynamisch zur Laufzeit.
"""

from __future__ import annotations

import importlib
from typing import Any


class AgentLoadError(Exception):
    pass


def load_agent(module_path: str, class_name: str) -> Any:
    """
    Dynamically import and instantiate an agent.
    """

    try:
        module = importlib.import_module(module_path)
    except Exception as exc:
        raise AgentLoadError(
            f"Could not import module '{module_path}': {exc}"
        ) from exc

    try:
        agent_class = getattr(module, class_name)
    except AttributeError as exc:
        raise AgentLoadError(
            f"Class '{class_name}' not found in '{module_path}'"
        ) from exc

    try:
        return agent_class()
    except Exception as exc:
        raise AgentLoadError(
            f"Could not instantiate '{class_name}': {exc}"
        ) from exc
