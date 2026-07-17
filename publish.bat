@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo Color Blocks Steam publish pipeline
echo.

where powershell >nul 2>&1
if errorlevel 1 (
  echo PUBLISH FAILED
  echo PowerShell not found on PATH.
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
  echo.
  echo PUBLISH FAILED — see messages above.
  exit /b %EXITCODE%
)

exit /b 0
