# Jarvis Gradio UI Dependencies

## Ziel

Diese Datei dokumentiert die Python-Abhaengigkeiten von `ui_app.py`
reproduzierbar, ohne Pakete zu installieren oder Runtime-Dateien zu
veraendern.

## Direkte Imports in `ui_app.py`

Standardbibliothek:

- `json`
- `subprocess`
- `datetime`
- `pathlib`
- `typing`

Direkte Drittanbieter-Abhaengigkeiten:

- `gradio`
- `openai-whisper` importiert als `whisper`

Direkte interne Imports:

- `agents.core.hermes_ui_status`

Optionale interne Imports, erst beim jeweiligen Button-Klick:

- `agents.core.delegation_contract`
- `agents.core.delegation_executor`
- `agents.core.hermes_decision`
- `agents.core.hermes_planner`
- `agents.core.hermes_orchestrator`
- `agents.core.hermes_execution_engine`
- `agents.core.hermes_learning_feedback`
- `agents.core.manual_assist`
- `agents.core.provider_registry`
- `agents.core.model_registry`
- `agents.core.hermes_router`

## Minimale Start-Dependencies

Diese Pakete reichen fuer den Import und Start der Gradio UI, solange keine
Whisper-Transkription genutzt wird:

```bash
python3 -m pip install gradio openai-whisper
```

`openai-whisper` wird aktuell trotzdem beim Start importiert, weil `ui_app.py`
`import whisper` auf Modulebene ausfuehrt. Deshalb ist Whisper auch fuer einen
reinen UI-Start notwendig.

## Voice-/Audio-Dependencies

Die Voice-Funktion in `ui_app.py` nutzt das Browser-Mikrofon ueber Gradio und
uebergibt eine Audiodatei an Whisper. Python greift dabei nicht direkt auf das
lokale Mikrofon zu.

Empfohlene Voice-Pakete:

```bash
python3 -m pip install gradio openai-whisper edge-tts
```

Zusaetzliche Pakete fuer den separaten `voice_client.py`, der direkt auf ein
lokales Mikrofon zugreift:

```bash
python3 -m pip install sounddevice soundfile numpy
```

Bestehende `installer/requirements_voice.txt` nennt ausserdem:

- `faster-whisper`
- `requests`
- `scipy`
- `pyttsx3`
- `keyboard`

Diese Pakete sind fuer `ui_app.py` nicht direkt erforderlich, koennen aber fuer
spaetere oder alternative Voice-Runtimes relevant sein.

## ML-/Whisper-/Torch-Abhaengigkeiten

`openai-whisper` bringt schwere ML-Abhaengigkeiten mit, insbesondere:

- `torch`
- `tiktoken`
- `numba`
- `numpy`
- Whisper-Modell-Downloads zur Laufzeit, z. B. `base`

Systemabhaengigkeit:

```bash
sudo apt update
sudo apt install -y ffmpeg
```

GPU ist fuer das aktuelle UI nicht erforderlich. Falls CUDA/GPU genutzt werden
soll, sollte `torch` separat passend zur Zielplattform installiert werden.

## TTS-Abhaengigkeit

`ui_app.py` ruft `agents/core/jarvis_core.py` mit `--speak` auf. Die eigentliche
TTS-Ausgabe liegt dort und nutzt `edge-tts` als CLI.

```bash
python3 -m pip install edge-tts
```

Optionales Systemtool fuer Audioausgabe, je nach TTS-Playback-Pfad:

```bash
sudo apt install -y mpg123
```

## Vorhandene Requirements-Lage

- `requirements.txt` enthaelt aktuell Basis-/Datenpakete, aber nicht `gradio`,
  `openai-whisper` oder `edge-tts`.
- `installer/setup_local_runtime.sh` installiert `requirements.txt`, wenn die
  Datei existiert. Der Fallback mit `gradio openai-whisper edge-tts
  sounddevice requests` greift daher nur, wenn `requirements.txt` fehlt.
- `installer/requirements_voice.txt` beschreibt eine separate Voice-Liste,
  aber nicht die minimale Gradio-UI-Liste.

## Vorgeschlagene Installationsbefehle

Minimal fuer Gradio UI plus Status-Tab:

```bash
python3 -m venv venv
source venv/bin/activate
python3 -m pip install --upgrade pip wheel "setuptools<82"
python3 -m pip install gradio openai-whisper
```

Mit Sprachantwort via `--speak`:

```bash
python3 -m pip install edge-tts
sudo apt install -y mpg123
```

Mit Whisper/Audio-Systemtools:

```bash
sudo apt update
sudo apt install -y ffmpeg
```

Mit separatem lokalen Voice Client:

```bash
python3 -m pip install sounddevice soundfile numpy
```

## Vorschlag fuer spaeteres `requirements-ui.txt`

Noch nicht erzeugt. Sinnvoll waere eine kleine, getrennte UI-Datei:

```text
gradio
openai-whisper
edge-tts
```

Optional fuer lokalen Mikrofon-Client:

```text
sounddevice
soundfile
numpy
```

Schwere oder plattformspezifische ML/GPU-Pakete wie `torch` sollten nicht
hart gepinnt werden, solange CPU/GPU-Zielumgebung nicht festgelegt ist.
