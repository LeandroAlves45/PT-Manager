@echo off
setlocal EnableExtensions

set "ACTION=%~1"
set "STATE_DIR=%PTM_CURSOR_STATE_DIR%"
if not defined STATE_DIR set "STATE_DIR=%USERPROFILE%\.cursor\claude-mem\Projeto_pt_manager"
set "STATE=%STATE_DIR%\memory-on"

if /I "%ACTION%"=="on" goto turn_on
if /I "%ACTION%"=="off" goto turn_off

echo Usage: memory-toggle.cmd on^|off
exit /b 2

:turn_on
if not exist "%STATE_DIR%" mkdir "%STATE_DIR%" >nul 2>&1
if not exist "%STATE_DIR%" (
  echo Failed to create the memory state directory.
  exit /b 1
)
> "%STATE%" echo enabled
echo Memory hooks enabled.
exit /b 0

:turn_off
if exist "%STATE%" del /q "%STATE%" >nul 2>&1
if exist "%STATE%" (
  echo Failed to remove the memory state marker.
  exit /b 1
)
echo Memory hooks disabled.
exit /b 0
