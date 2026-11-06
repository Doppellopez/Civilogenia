@echo off
title Servidor Civilogenia
rem Servidor dedicado de Civilogenia. Copia este archivo junto a Civilogenia.exe (carpeta Exe) y ejecutalo.
rem Al arrancar pregunta cuantos jugadores (clientes) hay que esperar antes de empezar la partida (1 a 7).
rem Si hay una partida guardada, pregunta si se continua o se empieza una nueva.
rem   ServidorDedicado.bat            pregunta el numero
rem   ServidorDedicado.bat 3          espera a 3 jugadores (no pregunta mas)
rem   ServidorDedicado.bat 3 -newgame -port 7800     el resto de argumentos van tal cual al servidor
rem El resto de la configuracion (IA, dificultad, mapa...) esta en servidor.cfg, que el servidor crea la primera vez.
rem Para cerrarlo con guardado usa PararServidor.bat; para empezar sin esperar a todos, EmpezarPartida.bat.
cd /d "%~dp0"

set JUGADORES=%~1
if "%JUGADORES%"=="" goto preguntar
goto comprobar

:preguntar
set /p JUGADORES=Cuantos jugadores hay que esperar antes de empezar (1-7)? [2]:
if "%JUGADORES%"=="" set JUGADORES=2

:comprobar
set N=0
set /a N=%JUGADORES% 2>nul
if %N% LSS 1 goto numero_malo
if %N% GTR 7 goto numero_malo
goto guardado

:numero_malo
echo Escribe un numero entre 1 y 7.
set JUGADORES=
goto preguntar

:guardado
set EXTRA=
set GUARDADA=%USERPROFILE%\AppData\LocalLow\DefaultCompany\Civilogenia\servidor.json
if not "%~2"=="" goto arrancar
if not exist "%GUARDADA%" goto arrancar
echo Hay una partida guardada del servidor (solo admite los jugadores que tenia al guardarse).
set NUEVA=n
set /p NUEVA=Empezar una partida NUEVA (s) o continuar la guardada (n)? [n]:
if /i "%NUEVA%"=="s" set EXTRA=-newgame

:arrancar
if exist servidor.log del servidor.log
echo Esperando a %N% jugador(es) antes de empezar la partida.
start "" Civilogenia.exe -batchmode -dedicated -players %N% -minplayers %N% -wait 0 %EXTRA% %2 %3 %4 %5 %6 %7 %8 %9 -logFile servidor.log
echo Servidor arrancando... (cierralo con PararServidor.bat)
echo.
powershell -NoProfile -Command "while(-not (Test-Path 'servidor.log')){Start-Sleep 1}; Get-Content 'servidor.log' -Wait -Encoding UTF8 | Where-Object { $_ -match 'Civilogenia' } | ForEach-Object { $_ -replace '^\[Civilogenia\]\[Servidor\] ','' }"
pause
