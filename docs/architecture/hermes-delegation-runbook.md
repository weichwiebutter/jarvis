# Hermes Delegation Runbook
Version: 1.0
Status: ACTIVE
System: Jarvis Hybrid Architecture

---

# 1. Ziel

Hermes ist der zentrale Planungs-, Lern- und Delegations-Agent im Jarvis-System.

Jarvis Core ist NICHT der intelligente Entscheider.
Jarvis Core ist der orchestrierende Runtime-Layer.

Hermes:
- analysiert
- plant
- priorisiert
- delegiert
- bewertet Ergebnisse
- lernt aus Resultaten

Die Fach-Agenten führen spezialisierte Aufgaben aus.

---

# 2. Architektur-Prinzip

## Rollenmodell

### Hermes
Rolle:
- Master Planner
- Delegation Engine
- Cognitive Layer
- Learning Layer

Verantwortlich für:
- Task Zerlegung
- Agent Auswahl
- Risikoanalyse
- Freigabeprüfung
- Langfristige Verbesserung
- Memory-basierte Entscheidungen

Hermes führt selbst KEINE direkten Systemaktionen aus.

---

### Jarvis Core

Rolle:
- Runtime Orchestrator
- Session Controller
- API Entry Point

Verantwortlich für:
- Eingaben entgegennehmen
- Hermes aufrufen
- Delegationspläne ausführen
- Agenten starten
- Ergebnisse sammeln
- Status zurückgeben

Jarvis Core denkt NICHT strategisch.

---

### Fach-Agenten

Beispiele:
- memory_agent
- coding_agent
- office_agent
- research_agent
- trading_agent
- improvement_agent

Verantwortlich für:
- Domain-Aufgaben
- strukturierte Antworten
- sichere Task-Ausführung

Nicht verantwortlich für:
- globale Entscheidungen
- Selbststeuerung
- autonome Systemänderungen

---

# 3. Entscheidungsfluss

## Standardablauf

User
→ Jarvis Core
→ Hermes
→ Delegation Plan
→ Fach-Agent(en)
→ Executor
→ Ergebnis
→ Hermes Bewertung
→ Memory Update
→ Antwort an User

---

# 4. Delegationsregeln

## Hermes MUSS entscheiden bei:

- Multi-Agent-Aufgaben
- Priorisierung
- Langfristplanung
- Architekturänderungen
- Learning-Aufgaben
- Memory-Auswertung
- Risikoanalyse
- Workflow-Optimierung

---

## Jarvis Core darf direkt routen bei:

- einfachen Einzelaufgaben
- Statusabfragen
- Health Checks
- UI Requests
- simplen Memory Reads

---

# 5. Approval-Regeln

## Immer Freigabe nötig bei:

- git push
- git reset
- rm -rf
- Dateien löschen
- Produktionsänderungen
- externe APIs mit Kosten
- Cloud Deployments
- Autonomer Codegenerierung
- Memory-Massenänderungen

---

# 6. Learning-System

Hermes bewertet:

- erfolgreiche Tasks
- Fehler
- Retry-Muster
- Nutzerpräferenzen
- Agent Performance
- Routing Qualität

Speicherorte:
- memory/
- logs/
- obsidian/
- reports/

---

# 7. Memory-Prinzip

Memory ist dauerhaft.

Hermes nutzt:
- vergangene Entscheidungen
- Nutzerpräferenzen
- Projekthistorie
- Fehlerhistorie
- Erfolgsmetriken

Memory darf NIE ungeprüft überschrieben werden.

---

# 8. Agent-Regeln

## Jeder Agent MUSS:

- JSON-kompatible Ergebnisse liefern
- Logs schreiben
- Fehler sauber zurückgeben
- approval_required markieren
- keine versteckten Aktionen ausführen

---

## Kein Agent darf:

- autonom pushen
- autonom löschen
- eigene Prozesse starten
- Sicherheitsregeln umgehen
- sich selbst replizieren

---

# 9. Executor-Regeln

Executor führt nur explizit erlaubte Aktionen aus.

Executor:
- plant NICHT
- priorisiert NICHT
- entscheidet NICHT

Executor führt aus:
- Shell Commands
- Git Commands
- File Writes
- Script Runs

Nur nach Freigabe-Regeln.

---

# 10. Architektur-Zielbild

## Zielzustand

Hermes
↓
Jarvis Core
↓
Specialized Agents
↓
Executor Layer
↓
System Tools

---

# 11. Zukunfts-Erweiterungen

Geplant:
- Voice Supervisor
- Autonomous Retry Layer
- Obsidian Semantic Memory
- Agent Reputation System
- Multi-PC Coordination
- Distributed Executors
- Workflow Marketplace
- Long-term Planning Engine

---

# 12. Sicherheitsprinzip

Human-in-the-loop bleibt dauerhaft aktiv.

Hermes darf:
- empfehlen
- planen
- vorbereiten

Hermes darf NICHT:
- unkontrolliert handeln
- Sicherheitsregeln überschreiben
- Approval umgehen

---

# 13. Betriebsmodus

## Empfohlener Standardmodus

Mode:
HYBRID_SAFE_AUTONOMY

Eigenschaften:
- teilautonom
- delegationsbasiert
- approval-gesteuert
- memory-basiert
- recovery-fähig

---

# 14. Git-Regeln

Vor Architekturänderungen:
- Commit
- Push
- Status prüfen

Nach erfolgreichem Umbau:
- neuer stabiler Snapshot
- Tagging empfohlen

---

# 15. Abschlussdefinition

Jarvis ist:
- modular
- agentenbasiert
- delegationsfähig
- modellagnostisch
- lernfähig
- kontrolliert autonom

Hermes ist das Gehirn.
Jarvis Core ist das Nervensystem.
Agenten sind spezialisierte Fähigkeiten.
Executor ist die Hand.
