@echo off
cd /d "%~dp0"
title Riesgus
set "RIESGUS_VERSION="
if exist "VERSION" for /f "usebackq tokens=1" %%v in ("VERSION") do if not defined RIESGUS_VERSION set "RIESGUS_VERSION=%%v"
if not defined RIESGUS_VERSION (
  echo No se encontro el archivo VERSION en la raiz del repositorio.
  pause
  exit /b 1
)
set "BUILD=Builds\Windows-v%RIESGUS_VERSION%\RiskAI.exe"
if exist "%BUILD%" (
  if not exist "RiskAI\Logs" mkdir "RiskAI\Logs"
  start "Riesgus" "%BUILD%" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "RiskAI\Logs\player-v%RIESGUS_VERSION%.log"
) else (
  echo Riesgus v%RIESGUS_VERSION% no esta compilado. Ejecuta scripts\Unity.ps1 -Action Build.
  pause
)
