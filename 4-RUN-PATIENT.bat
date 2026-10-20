@echo off
title Wasfa - Patient Emergency & Med Record App
cd /d "%~dp0apps\patient"

echo ===================================================
echo     Starting Wasfa Patient App (Port 8082)
echo ===================================================
echo.
echo URL: http://localhost:8082/
echo.

start "" cmd /c "timeout /t 2 >nul && start http://localhost:8082/"
call npx -y serve build/web -l 8082 --single

echo.
pause
