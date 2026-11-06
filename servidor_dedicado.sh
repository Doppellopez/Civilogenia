#!/bin/sh
# Servidor dedicado de Civilogenia en Linux. Copia este archivo junto a Civilogenia.x86_64 (carpeta ExeLinux).
cd "$(dirname "$0")"
chmod +x ./Civilogenia.x86_64
exec ./Civilogenia.x86_64 -batchmode -dedicated -port 7777 -players 1 -rivals 3 -difficulty 1 -logFile servidor.log
