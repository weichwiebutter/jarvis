# Jarvis / Hermes LLM Evaluation

## Ziel

Bewertung von LLMs für Hermes-Entwicklung, Architekturreviews, Codex-Agentenbetrieb und lokale Nutzung.

---

## Sonnet 4.5 (OpenRouter)

Status: Referenzmodell

Stärken:
- Versteht Hermes-Architektur sehr gut
- Gute Codex-Agent-Kompatibilität
- Liefert umsetzbare Architekturarbeit

Schwächen:
- Hohe Kosten
- Große Analysen verbrauchen viele Tokens

Bewertung:
- Architektur: 9.5/10
- Agentenbetrieb: 10/10

---

## Qwen2.5-Coder 14B (lokal)

Status: Ollama + Codex funktionsfähig

Stärken:
- Lokal
- Kostenlos
- Gute allgemeine Coding-Fähigkeiten

Schwächen:
- Schwaches Hermes-Verständnis
- Halluziniert Architekturdetails
- Agentenmodus problematisch

Bewertung:
- Architektur: 3/10
- Agentenbetrieb: 2/10

---

## GPT-OSS 20B (lokal)

Status: Ollama + Codex funktionsfähig

Stärken:
- Bessere Analysen als Qwen
- Gute Zweitmeinung

Schwächen:
- Sehr generische Antworten
- Erkennt Projektkontext nur teilweise

Bewertung:
- Architektur: 6.5/10
- Agentenbetrieb: 3/10

---

## Kimi K2.6 (OpenRouter)

Status:
- Verbindung erfolgreich
- Mehrere Tests durch Provider-Überlastung unterbrochen

Bewertung:
- Noch offen

---

## Groq

Status:
- API-Key funktioniert
- Modellliste abrufbar
- Codex 0.137 Responses API aktuell nicht kompatibel

Bewertung:
- Noch offen

---

## Fazit

Aktuelle Reihenfolge:

1. Sonnet 4.5
2. GPT-OSS 20B lokal
3. Qwen2.5-Coder 14B lokal
4. Kimi (offen)
5. Groq (offen)
