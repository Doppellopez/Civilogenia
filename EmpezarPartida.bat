@echo off
rem Empieza la partida del servidor con los jugadores que haya ya en la sala, sin esperar al resto.
rem Copia este archivo junto a Civilogenia.exe (carpeta Exe). Hace falta al menos un jugador conectado.
cd /d "%~dp0"
echo empezar> empezar.txt
echo Orden de inicio enviada. Si hay al menos un jugador en la sala, la partida empieza en unos segundos.
timeout /t 5 >nul
