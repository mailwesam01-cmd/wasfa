@echo off
title Wasfa - Backend API (ASP.NET Core FHIR)
cd /d "%~dp0"

echo ===================================================
echo     Starting Wasfa Backend Server (Port 5000)
echo ===================================================
echo.
echo URL: http://localhost:5000/
echo.

start "" cmd /c "timeout /t 3 >nul && start http://localhost:5000/"
dotnet run --project src/Wasfa.Api

echo.
pause
