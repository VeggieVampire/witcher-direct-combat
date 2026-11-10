@echo off
setlocal
set "GAME=%~dp0.."
if not exist "%GAME%\System\witcher.exe" (
  echo Could not find The Witcher in the parent folder.
  pause
  exit /b 1
)
tasklist /FI "IMAGENAME eq DirectCombatController.exe" /FO CSV /NH 2>nul | find /I "DirectCombatController.exe" >nul
if errorlevel 1 start "Witcher Direct Combat" /min "%~dp0DirectCombatController.exe" --slot 0 --duration 86400
tasklist /FI "IMAGENAME eq witcher.exe" /NH 2>nul | find /I "witcher.exe" >nul
if errorlevel 1 start "" /D "%GAME%\System" "%GAME%\System\witcher.exe"
