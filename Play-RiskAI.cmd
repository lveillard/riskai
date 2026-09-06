@echo off
cd /d "%~dp0"
if exist "Builds\Windows-v0.18\RiskAI.exe" (
  if not exist "RiskAI\Logs" mkdir "RiskAI\Logs"
  start "" "Builds\Windows-v0.18\RiskAI.exe" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "RiskAI\Logs\v18-opened.log"
) else (
  echo Build not found. Run scripts\Unity.ps1 -Action Build first.
  pause
)


