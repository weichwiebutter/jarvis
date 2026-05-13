# OpenRouter Fallback Architecture
Version: 1.0
Status: FOUNDATION
System: Jarvis Hybrid Architecture

---

# 1. Ziel

Dieses Dokument beschreibt OpenRouter als sicheren Fallback fuer Codex und
Hermes, falls ChatGPT/Codex-Limits erreicht werden oder ein staerkeres externes
Modell bewusst benoetigt wird.

Es ist nur ein Architektur- und Config-Plan. Es enthaelt keine echten API-Keys,
keine Secrets, keine Modellaufrufe und keine Runtime-Aenderungen.

---

# 2. Prioritaetenmodell

Grundregel:

```text
Ollama local-first
ChatGPT-Codex primary coding assistant
OpenRouter fallback only
```

## Ollama

Ollama bleibt local-first fuer:

- lokale Inferenz
- privacy-sensitive Aufgaben
- einfache bis mittlere Analyse
- kostensensible Verarbeitung
- schnelle lokale Iterationen

Hermes soll bevorzugt lokale Modelle nutzen, wenn Qualitaet und Kontext dafuer
ausreichen.

## ChatGPT-Codex

ChatGPT-Codex bleibt primaer fuer:

- Coding-Arbeit
- Repository-Analyse
- Refactoring
- Tests und Validierung
- technische Planung im Codekontext

OpenRouter ersetzt Codex nicht als Standardweg.

## OpenRouter

OpenRouter ist nur als Fallback vorgesehen:

- bei ChatGPT/Codex-Limit
- bei bewusst freigegebenem externem Modellbedarf
- bei komplexen Reasoning-Aufgaben, die lokal nicht ausreichen
- bei manueller Eskalation durch den Nutzer

OpenRouter darf nicht still als Default aktiviert werden.

---

# 3. Kosten- und Kreditkontrolle

OpenRouter ist kostenbewusst zu nutzen.

Regeln:

- Credits regelmaessig ueberwachen
- keine unkontrollierten Batch-Aufrufe
- keine versteckten Hintergrund-Requests
- keine automatischen Retry-Schleifen ohne Limit
- Modellwahl bewusst und taskbezogen treffen
- teure Modelle nur bei klarer Begruendung verwenden

Hermes soll spaeter vor kostenrelevanten externen Aufrufen eine klare
Freigabelogik anwenden.

---

# 4. Secret Handling

API-Keys duerfen niemals in Git gespeichert werden.

Erlaubte Orte:

- lokale `.env.local`
- lokale Codex-Konfiguration
- andere explizit lokale, nicht versionierte Secret Stores

Nicht erlaubt:

- Keys in Markdown-Dokumenten
- Keys in Python-Dateien
- Keys in JSON-Beispielen mit echten Werten
- Keys in Logs
- Keys in Screenshots oder Reports
- Keys in Git-History

Platzhalter sind erlaubt, echte Werte nicht.

---

# 5. Geplante lokale Konfiguration

Falls eine `.env.example` verwendet wird, duerfen nur Platzhalter eingetragen
werden:

```env
OPENROUTER_API_KEY=
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_DEFAULT_MODEL=
```

Die lokale echte Konfiguration gehoert in `.env.local`.

Beispielhafte lokale Datei:

```env
OPENROUTER_API_KEY=
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_DEFAULT_MODEL=
```

Die Werte bleiben lokal und werden nicht committed.

---

# 6. Geplante Hermes-Nutzung

Hermes darf OpenRouter spaeter nur ueber eine explizite Provider-Schicht nutzen.

Geplante Anforderungen:

- kein direkter API-Aufruf aus Fach-Agenten
- zentrale Provider-Konfiguration
- klares Routing: local, Codex, OpenRouter
- Kosten- und Limit-Bewertung vor externen Calls
- Audit-Metadaten ohne Secrets
- human-in-the-loop bei kritischen oder teuren Aufgaben

OpenRouter ist ein Werkzeug fuer bewusst eskalierte Modellnutzung, nicht die
System-Intelligenz selbst.

---

# 7. Sicherheitsregeln

- keine echten API-Keys in Git
- keine Secrets in Dokumentation
- keine Modellaufrufe aus dieser Foundation
- keine Runtime-Dateien schreiben
- keine automatischen Requests
- keine versteckte Aktivierung als Default
- externe Modelle nur ueber explizite Provider-Layer
- kostenrelevante Nutzung mit Review und Credit-Ueberwachung

Pflichtprinzip:

```text
fallback_only: true
local_first: true
chatgpt_codex_primary: true
no_keys_in_git: true
human_review_for_costs: true
```

---

# 8. Grundprinzip

OpenRouter ist ein sicherer Fallback, kein Ersatz fuer die lokale und primaere
Arbeitsweise.

```text
Use local models first.
Use ChatGPT-Codex as primary coding assistant.
Use OpenRouter only when explicitly needed.
Keep keys local.
Watch credits.
Never commit secrets.
```
