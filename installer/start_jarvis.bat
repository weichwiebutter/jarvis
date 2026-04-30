@echo off
title Jarvis Launcher

echo Starte OpenJarvis Backend...
start "Jarvis Backend" wsl.exe bash -lc "cd ~/jarvis/OpenJarvis && OLLAMA_MODEL=llama3.2:3b ./scripts/quickstart.sh"

echo Warte auf Backend...
timeout /t 10 /nobreak >nul

echo Oeffne OpenJarvis UI...
start http://127.0.0.1:5173

echo Starte Voice Client...
start "Jarvis Voice" cmd /k "cd /d C:\jarvis-voice && .venv\Scripts\activate && python voice_test.py"

echo Fertig.
pause
