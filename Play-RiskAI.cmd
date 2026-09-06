@echo off
cd /d "%~dp0"
if exist "Builds\Windows-v0.14\RiskAI.exe" (
  start "" "Builds\Windows-v0.14\RiskAI.exe" -screen-width 1600 -screen-height 900 -screen-fullscreen 0
) else (
  echo Build not found. Run scripts\Unity.ps1 -Action Build first.
  pause
)


