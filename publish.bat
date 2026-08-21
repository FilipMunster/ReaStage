@echo off
rem Wrapper for publish.ps1 so the packaging can be started by double-clicking.
rem Any arguments are passed through, e.g.  publish.bat -Platform linux-x64
setlocal

set "PS=powershell"
where /q pwsh && set "PS=pwsh"

"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
set "EXITCODE=%ERRORLEVEL%"

rem Keep the window open when it failed, so the error stays readable
if not "%EXITCODE%"=="0" pause

exit /b %EXITCODE%
