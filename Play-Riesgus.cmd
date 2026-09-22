@echo off
cd /d "%~dp0"
title Riesgus
set "BUILD=Builds\Windows-v0.29.1\RiskAI.exe"
if exist "%BUILD%" (
  if not exist "RiskAI\Logs" mkdir "RiskAI\Logs"
  start "Riesgus" "%BUILD%" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "RiskAI\Logs\v28-opened.log"
) else (
  echo Riesgus v0.29.1 no esta compilado. Ejecuta scripts\Unity.ps1 -Action Build.
  pause
)
