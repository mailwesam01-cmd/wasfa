@echo off
title Wasfa - Prescriber Workstation (Angular)
cd /d "%~dp0apps\prescriber"

echo ===================================================
echo     Starting Prescriber Station (Port 4200)
echo ===================================================
echo.
echo URL: http://localhost:4200/
echo.

start "" cmd /c "timeout /t 5 >nul && start http://localhost:4200/"
call npm start

echo.
pause
