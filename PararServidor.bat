@echo off
rem Cierra el servidor dedicado guardando antes la partida. Copia este archivo junto a Civilogenia.exe (carpeta Exe).
rem El servidor mira cada segundo si existe parar.txt; al verlo, guarda y se cierra.
cd /d "%~dp0"
echo parar> parar.txt
echo Orden de cierre enviada. El servidor guardara la partida y se cerrara en unos segundos.
timeout /t 5 >nul
