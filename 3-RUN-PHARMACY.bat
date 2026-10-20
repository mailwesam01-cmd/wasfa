@echo off
title Wasfa - Pharmacy Dispense App
cd /d "%~dp0apps\pharmacy"

echo ===================================================
echo     Starting Wasfa Pharmacy App (Port 8081)
echo ===================================================
echo.
echo URL: http://localhost:8081/
echo.

start "" cmd /c "timeout /t 2 >nul && start http://localhost:8081/"
call npx -y serve build/web -l 8081 --single

echo.
pause
