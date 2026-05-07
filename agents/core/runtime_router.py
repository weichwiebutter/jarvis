from __future__ import annotations

from types import SimpleNamespace
from typing import Any, Dict

from agents.core.agent_loader import load_agent


ROUTE_MAP = {
    "memory": ("agents.memory.memory_agent", "MemoryAgent"),
    "office": ("agents.office.office_agent", "OfficeAgent"),
    "research": ("agents.research.research_agent", "ResearchAgent"),
    "coding": ("agents.coding.coding_agent", "CodingAgent"),
    "business": ("agents.business.business_agent", "BusinessAgent"),
    "trading": ("agents.trading.trading_agent", "TradingAgent"),
    "improvement": ("agents.improvement.improvement_agent", "ImprovementAgent"),
}


class RuntimeRouter:
    """
    Hermes decides.
    RuntimeRouter executes delegation.
    """

    def __init__(self):
        self.loaded_agents = {}

    def get_agent(self, domain: str):
        if domain not in ROUTE_MAP:
            raise ValueError(f"Unknown domain: {domain}")

        if domain not in self.loaded_agents:
            module_path, class_name = ROUTE_MAP[domain]
            self.loaded_agents[domain] = load_agent(module_path, class_name)

        return self.loaded_agents[domain]

    def build_request(self, task: str, context: Dict[str, Any]):
        return SimpleNamespace(
            task=task,
            context=context,
            metadata=context,
            category=context.get("category", "learnings"),
            title=context.get("title", "Runtime Router Memory"),
            source=context.get("source", "runtime_router"),
        )

    def execute(
        self,
        domain: str,
        task: str,
        context: Dict[str, Any] | None = None,
    ):
        agent = self.get_agent(domain)
        context = context or {}
        request = self.build_request(task, context)

        if hasattr(agent, "handle"):
            return agent.handle(request)

        if hasattr(agent, "run"):
            try:
                return agent.run(request)
            except TypeError:
                return agent.run(task)

        if hasattr(agent, "execute"):
            try:
                return agent.execute(request)
            except TypeError:
                return agent.execute(task)

        raise RuntimeError(
            f"Agent for domain '{domain}' has no supported method: handle(), run(), execute()."
        )
