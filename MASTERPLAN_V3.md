# KI-Agent Jarvis – Masterplan V3

Stand: 30. April 2026

## Zielbild

Jarvis soll ein lokaler AI-Agent werden, steuerbar per Sprache, mit automatisierten Market Briefings, strukturierter Wissensablage in Obsidian und später echten Multi-Agent-Funktionen.

Zielarchitektur:

Voice → Whisper → Agent Router → OpenJarvis/Ollama → Tool-Ausführung → Antwort per Sprache

## Aktueller Systemstatus

Das System ist kein vollautonomer Agent, aber ein funktionierendes lokales AI-Agent-Framework mit folgenden Komponenten:

- Windows Voice Client
- faster-whisper mit CUDA GPU
- Windows SAPI Sprachausgabe
- Agent Router mit LLM-basierter Intent-Erkennung
- OpenJarvis Backend auf Port 8000
- Ollama mit llama3.2:3b
- Market Data Script
- Market Briefing Script
- Obsidian-Ablage
- GitHub-Repo für Wiederherstellung und PC2
- WSL-Installer
- Windows Voice Setup

## Funktionierende Komponenten

### Market Briefing

Befehl in WSL:

```bash
cd ~/jarvis
./start_jarvis.sh
