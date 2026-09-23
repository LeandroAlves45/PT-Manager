@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "HOOK=%~1"
if "%HOOK%"=="" (
  echo {"continue": true}
  exit /b 0
)

set "STATE=%USERPROFILE%\.cursor\claude-mem\Projeto_pt_manager\memory-on"
if not exist "%STATE%" (
  echo {"continue": true}
  exit /b 0
)

set "IN=%TEMP%\ptm-cursor-hook-tool-%RANDOM%-%RANDOM%.json"
findstr /R /C:".*" > "%IN%"

set "GIT_BASH=%ProgramFiles%\Git\bin\bash.exe"
if exist "%GIT_BASH%" (
  "%GIT_BASH%" "%~dp0%HOOK%" < "%IN%"
  set "HOOK_EXIT=!ERRORLEVEL!"
  del /q "%IN%" >nul 2>&1
  exit /b !HOOK_EXIT!
)
where bash >nul 2>&1
if %ERRORLEVEL%==0 (
  bash "%~dp0%HOOK%" < "%IN%"
  set "HOOK_EXIT=!ERRORLEVEL!"
  del /q "%IN%" >nul 2>&1
  exit /b !HOOK_EXIT!
)
del /q "%IN%" >nul 2>&1
echo {"continue": true}
exit /b 0
