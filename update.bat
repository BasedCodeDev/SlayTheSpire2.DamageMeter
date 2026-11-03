@echo off
rem Close the game, then run update.bat to pull, build, check, install and launch.
rem Options are forwarded to deploy.ps1, e.g. update.bat -GameDir "D:\Games\Slay the Spire 2"
rem update.bat -WhatIf still pulls changes, then previews deployment.
setlocal
pushd "%~dp0" || exit /b 1

echo Pulling the latest changes...
git pull --ff-only
if errorlevel 1 goto pull_failed

echo Running deployment...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\deploy.ps1" %*
set "result=%errorlevel%"
if not "%result%"=="0" echo Deployment failed.
popd
exit /b %result%

:pull_failed
set "result=%errorlevel%"
echo Update failed. Deployment was not run. Resolve the Git error above and try again.
popd
exit /b %result%
