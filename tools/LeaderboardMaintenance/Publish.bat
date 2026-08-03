@echo off
setlocal
cd /d "%~dp0"

echo Publishing LeaderboardMaintenance.exe ...
dotnet publish -c Release -r win-x64 --self-contained false -o "%~dp0publish"
if errorlevel 1 (
  echo Publish failed.
  pause
  exit /b 1
)

REM Copy official levels next to the exe so double-click works from publish\
if exist "%~dp0..\..\Content\OfficialLevels" (
  xcopy /E /I /Y "%~dp0..\..\Content\OfficialLevels" "%~dp0publish\OfficialLevels\" >nul
)
if exist "%~dp0..\..\steam_appid.txt" (
  copy /Y "%~dp0..\..\steam_appid.txt" "%~dp0publish\steam_appid.txt" >nul
)

echo.
echo Done: %~dp0publish\LeaderboardMaintenance.exe
echo Double-click that exe anytime. Do NOT ship it with the game.
pause
