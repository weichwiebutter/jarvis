@echo off
title Jarvis Voice Setup

echo === JARVIS WINDOWS VOICE SETUP ===

set SOURCE=%~dp0
set TARGET=C:\jarvis-voice

if not exist %TARGET% mkdir %TARGET%

copy "%SOURCE%requirements_voice.txt" "%TARGET%\requirements_voice.txt" /Y

cd /d %TARGET%

python -m venv .venv
call .venv\Scripts\activate

python -m pip install --upgrade pip
pip install -r requirements_voice.txt

echo.
echo === VOICE SETUP DONE ===
pause
