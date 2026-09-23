@echo off
setlocal EnableExtensions

set "IN=%TEMP%\ptm-cursor-hook-%RANDOM%-%RANDOM%.json"
set "STATE_DIR=%USERPROFILE%\.cursor\claude-mem\Projeto_pt_manager"
set "STATE=%STATE_DIR%\memory-on"

findstr /R /C:".*" > "%IN%"

findstr /I /C:"/mem-off" /C:"/sem-mem" /C:"desliga a memoria" "%IN%" >nul
if %ERRORLEVEL%==0 goto turn_off

findstr /I /C:"/mem-on" /C:"/claude-mem" /C:"liga a memoria" "%IN%" >nul
if %ERRORLEVEL%==0 goto turn_on

if exist "%STATE%" goto run_hooks
goto allow

:turn_off
if exist "%STATE%" del /q "%STATE%" >nul 2>&1
goto allow

:turn_on
if not exist "%STATE_DIR%" mkdir "%STATE_DIR%" >nul 2>&1
echo on> "%STATE%"

:run_hooks
set "GIT_BASH=%ProgramFiles%\Git\bin\bash.exe"
if exist "%GIT_BASH%" goto run_git_bash

where bash >nul 2>&1
if %ERRORLEVEL%==0 goto run_path_bash
goto allow

:run_git_bash
"%GIT_BASH%" "%~dp0session-init.sh" < "%IN%" >nul 2>&1
"%GIT_BASH%" "%~dp0context-inject.sh" < "%IN%"
goto cleanup

:run_path_bash
bash "%~dp0session-init.sh" < "%IN%" >nul 2>&1
bash "%~dp0context-inject.sh" < "%IN%"
goto cleanup

:allow
echo {"continue": true}

:cleanup
if exist "%IN%" del /q "%IN%" >nul 2>&1
exit /b 0
