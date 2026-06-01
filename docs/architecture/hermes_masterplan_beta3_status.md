# Hermes Masterplan Beta 3 Status

Stand: 2026-06-01

Zweck: kurze, ehrliche Orientierung nach der Multi-Domain Cognitive Expansion.
Dieses Dokument beschreibt Fokus, Reifegrad und naechste Schritte. Es fuehrt
keine Runtime-, UI- oder Service-Aenderungen ein.

## 1. Neuer aktueller Stand

Hermes ist nicht mehr nur eine Trading-Beta. Hermes ist inzwischen eine lokale
Research-, Learning- und Cognitive-Core-Plattform. Trading bleibt wichtig, ist
aber Domaene 1 und nicht Hermes selbst.

| Bereich | Stand |
| --- | --- |
| Cognitive Core | Vorhanden als allgemeine Wissens-, Memory-, Queue- und Insight-Schicht. |
| Autonomous Planning Engine | Erkennt Needs, bewertet Goals und plant erlaubte interne Tasktypen. |
| Controlled Planned Task Execution | Fuehrt geplante Tasks kontrolliert aus einer Whitelist aus. Keine freien Shell-Kommandos. |
| Outcome Feedback Loop | Bewertet ausgefuehrte Tasks, schreibt Outcomes, Planner Feedback und Goal Feedback. |
| Autonomous Learning Loop | Orchestriert `Need -> Plan -> Execute -> Evaluate -> Adjust -> Insights` mit State, Summary, Log und Checkpoints. |
| Meta-Learning / Governance | Bewertet Fortschritt, Domain Health, Lernstrategie und Governance-Regeln. |
| Multi-Domain Foundation | `software`, `documentation`, `process` und `research` sind neben `trading` als aktive Domaenen vorbereitet. |
| Scheduler / Supervisor | Vorhanden. Zeitplaene liegen in `HermesRuntime/config/schedules.json`; neue zeitgesteuerte Jobs sollen keine neuen Windows Tasks brauchen. |
| Nightly Beta 3 | Nutzt Guards, Scheduler/Supervisor und den autonomen Lernloop fuer kontrolliertes Research. |
| Trading Domain | Aktive Domaene 1 mit historischen cTrader-Daten, Feature-/Signal-/Outcome-/Backtest-Stubs, Strategy Research, Quality Gates, Regime Intelligence und Bot-Candidate-Bewertung. |
| Software Domain | Erste lokale Repo-/Architektur-/Test-Knowledge-Items koennen gescannt werden. Noch keine tiefe Codeanalyse. |
| Documentation Domain | Markdown-/README-/Architektur-Dokumente koennen inventarisiert und Doku-Gaps/TODO-Signale erkannt werden. |
| Process Domain | Wiederkehrende Workflows, Checklisten, Risk Points und Automationskandidaten koennen strukturiert gespeichert werden. |
| Research Domain | Kuratierte Quellen-Metadaten und lokale Research-Notizen sind vorbereitet. Keine ungepruefte externe Web-Discovery. |
| Data Lake | Reports, Memory, Strategy Research, Cognitive Core und Logs liegen unter `/mnt/d/HermesData` bzw. Windows-seitig `D:/HermesData`. Das Repo ist kein Massendatenspeicher. |

## 2. Architekturprinzip

Trading ist Domaene 1, nicht Hermes selbst. Das Hermes Brain ist allgemeiner:

`Observe -> Think -> Plan -> Execute -> Evaluate -> Meta Review -> Adapt`

Praktisch bedeutet das:

- Observe: Status, Reports, Domains, Knowledge Sources, Queues, Guards lesen.
- Think: Needs, Goals, Gaps, Risiken und Prioritaeten erkennen.
- Plan: interne, erlaubte Tasks planen.
- Execute: nur kontrollierte interne Tasktypen ausfuehren.
- Evaluate: Task Outcomes und Nutzen bewerten.
- Meta Review: Fortschritt, Redundanz, Domain Health und Governance pruefen.
- Adapt: Planner Feedback, Goal Feedback und Learning Strategy anpassen.

## 3. Was Hermes jetzt kann

- Domains scannen:
  - `trading`
  - `software`
  - `documentation`
  - `process`
  - `research`
- Multi-Domain-Gaps erkennen, z. B. Dokumentationsluecken,
  Prozess-Automationskandidaten oder veraltete Research-Quellen.
- Needs aus Systemzustand, Reports, Knowledge Sources, Research Queue,
  ResourceGuard und Storage-Zustand erkennen.
- Goals bewerten und naechste Tasks priorisieren.
- Tasks kontrolliert ausfuehren, sofern sie in der internen Whitelist stehen.
- Outcomes ausgefuehrter Tasks bewerten.
- Feedback persistent speichern:
  - `cognitive_core/task_outcomes.jsonl`
  - `cognitive_core/planner_feedback.json`
  - `cognitive_core/goal_feedback.json`
- Lernstrategie regelbasiert anpassen.
- Domain Health domänenuebergreifend bewerten.
- Knowledge Sources und Knowledge Catalog verwalten.
- Research Queue fuer Discovery, Validation, Simulation, Review und Archive
  nutzen.
- Trading Research kontrolliert ausfuehren und bewerten, einschliesslich
  Strategy Research, Walk-Forward, Overfit-/Realism-/Cost-/Risk-Reports,
  Regime Reports und Bot-Candidate-Gates.
- Nightly-/Supervisor-Betrieb mit ResourceGuard, StorageGuard, Checkpoints,
  Logs und Statusdateien unterstuetzen.

## 4. Was Hermes noch nicht kann

- Hermes ist keine echte allgemeine KI mit tiefem Weltverstaendnis.
- Hermes aendert Code nicht eigenstaendig ohne Codex/User-Auftrag,
  Tests und Review.
- Hermes fuehrt keine autonomen Marktaktionen aus.
- Hermes platziert keine Orders und verwaltet keine Live-Positionen.
- Hermes besitzt noch keine tiefen domänenspezifischen Expertenmodelle fuer
  Software, Dokumentation, Prozess oder allgemeines Research.
- Externe Web-Discovery ist nicht vollstaendig und bleibt whitelist-/metadata-
  basiert. Keine ungeprueften Crawler.
- Fremder Code wird nicht ausgefuehrt.
- Es gibt noch keine vollstaendige Human-Review-Oberflaeche.
- Strategiebewertung ist weiter research-grade, nicht production-grade. Einige
  Resultate koennen unrealistisch oder overfit-anfaellig sein.
- Broker-Reality ist konservativ modelliert, aber noch nicht vollstaendig mit
  echten dynamischen Brokerbedingungen gleichzusetzen.
- Es gibt noch keinen freigegebenen Paper-/Demo-/Live-Trading Control Layer.

## 5. Masterplan ab jetzt

| Phase | Ziel | Status |
| --- | --- | --- |
| Phase 1: Runtime / Storage / Safety | Lokale Runtime, Storage, Events, Jobs, Health, CLI, Reports, Data Lake und Safety-Basics. | Weitgehend umgesetzt. |
| Phase 2: Trading Learning Beta | Historische Marktdaten, Feature-/Signal-/Outcome-/Backtest-Stubs, Strategy Research und Quality Gates. | Umgesetzt, Qualitaet weiter zu haerten. |
| Phase 3: Cognitive Core | Allgemeines Wissens-, Memory-, Queue- und Insight-Modell. | Foundation umgesetzt. |
| Phase 4: Autonomous Planning + Feedback | Needs erkennen, Tasks planen, kontrolliert ausfuehren, Outcomes bewerten, Feedback nutzen. | Foundation umgesetzt. |
| Phase 5: Multi-Domain Foundation | Software-, Documentation-, Process- und Research-Domaenen aktiv vorbereiten. | Foundation umgesetzt. |
| Phase 6: Domain Specialization | Eigene Validatoren, Mappers, Quality Scores und Review-Regeln pro Domaene. | Naechster groesserer Schritt. |
| Phase 7: Human Review / Control Center | Review-Workflow, Freigaben, Status, Erklaerbarkeit und Operator UI. | Teilweise vorbereitet, noch offen. |
| Phase 8: Controlled Automation | Sichere interne Automationsablaeufe mit Governance, Limits, Audit und Human Approval. | Offen. |
| Phase 9: Optionale spaetere Trading Execution | Nur nach langer Validierung, Paper/Demo-Phase, Risk Limits, Whitelists, Emergency Stop und Human Approval. | Nicht umgesetzt und bewusst blockiert. |

## 6. Naechste sinnvolle Schritte

1. Master Status CLI:
   Ein kompaktes Kommando, das Supervisor, Scheduler, Nightly, Cognitive Core,
   Planning, Outcome Feedback, Storage, ResourceGuard, Domain Health und
   Trading Research in einer Statussicht zusammenfasst.

2. Cognitive Dashboard spaeter im React Control Center:
   Monitoring fuer Domains, Planning Cycle, Research Queue, Feedback, Goals,
   Governance und Safety. Keine Trading-Buttons und keine Schreibkommandos.

3. Human Review Workflow:
   Review-Stati fuer Knowledge Items, Needs, Planned Tasks, Learnings,
   Strategy Candidates, Bot Candidates und spaetere Paper-/Demo-Freigaben.

4. Domain-specific Validators:
   Eigene Validierungslogik fuer Software, Documentation, Process und Research.
   Trading-Validatoren bleiben streng und realistisch.

5. Knowledge Source Scout erweitern:
   Mehr kuratierte Quellen, Source Trust, License Hints, Aktualitaet und
   Risk Flags. Weiterhin metadata-only, keine ungeprueften Crawler.

6. Software-Domain tiefer anbinden:
   Git-/Codeanalyse als sichere Metadaten, Architektur-Map, bekannte Issues,
   Testbefehle, Module, Abhaengigkeiten und Verbesserungskandidaten.

7. Documentation-Domain produktiver nutzen:
   Doku-Differenzen, veraltete Aussagen, TODOs, Masterplan-Luecken und
   Widersprueche erkennen.

8. Process-Domain ausbauen:
   Wiederkehrende Workflows, Checklisten, sichere Automationskandidaten und
   Risiko-Punkte systematisch bewerten.

9. Trading-Domain weiter realistisch validieren:
   Walk-Forward/OOS, Regime-Stabilitaet, Kosten-Sensitivitaet,
   Monte-Carlo/Risk-of-Ruin und Sample-Qualitaet als harte Gates nutzen.

10. Governance-Regeln verschaerfen:
    Keine Endlosschleifen, keine Queue Explosion, keine redundanten Scans,
    keine Priorisierung nach Masse, keine Storage-Explosion.

## 7. Safety-Prinzipien

- Keine freien Shell-Kommandos aus Planner, Scheduler oder Config.
- Kein fremder Code wird ausgefuehrt.
- Keine Broker-Orders.
- Keine autonome Markt-Ausfuehrung.
- `no_auto_trading=true` bleibt Standard.
- `human_review_required=true` bleibt Standard.
- Secrets, Tokens und `.env`-Inhalte nie ins Repo.
- Runtime-, Research- und Massendaten liegen im Data Lake auf
  `D:/HermesData` bzw. `/mnt/d/HermesData`.
- ResourceGuard und StorageGuard muessen vor laengeren Jobs greifen.
- Cleanup darf keine Candles, Research Memory, Strategy Memory, Auth Tokens
  oder freigegebenen Lernartefakte loeschen.
- Trading Execution bleibt blockiert, bis Control Layer, Paper/Demo-Phase,
  Risk Limits, Whitelists, Audit, Emergency Stop und Human Approval vorhanden
  sind.

## 8. Fokus

Hermes Beta 3 ist kein Trading-Bot. Hermes Beta 3 ist eine kontrollierte
Research-, Learning- und Cognitive-Core-Plattform. Trading ist die erste aktive
Domaene, aber das langfristige System ist allgemeiner: Hermes soll Wissen
strukturieren, Bedarfe erkennen, Aufgaben planen, sichere interne Arbeit
ausfuehren, Ergebnisse bewerten und daraus naechste Schritte ableiten.

Der naechste Fokus ist nicht "mehr rechnen", sondern Qualitaet,
Erklaerbarkeit, Betriebssicherheit, Human Review und saubere
domänenspezifische Erweiterbarkeit.
