@echo off
setlocal
set "HOOK=%~1"
if "%HOOK%"=="" exit /b 0

set "GIT_BASH=%ProgramFiles%\Git\bin\bash.exe"
if exist "%GIT_BASH%" (
  "%GIT_BASH%" "%~dp0%HOOK%"
  exit /b %ERRORLEVEL%
)

where bash >nul 2>&1
if %ERRORLEVEL%==0 (
  bash "%~dp0%HOOK%"
  exit /b %ERRORLEVEL%
)

exit /b 1
