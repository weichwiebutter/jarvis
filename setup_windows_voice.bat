@echo off
title Jarvis Voice Setup

echo === JARVIS WINDOWS VOICE SETUP ===

cd /d C:\

if not exist C:\jarvis-voice mkdir C:\jarvis-voice
cd /d C:\jarvis-voice

python -m venv .venv

call .venv\Scripts\activate

python -m pip install --upgrade pip
pip install -r requirements_voice.txt

echo.
echo === VOICE SETUP DONE ===
pause
