@echo off
title Wasfa - Run All Services
cd /d "%~dp0"

echo ========================================================
echo       Starting Entire Wasfa Ecosystem
echo ========================================================
echo.
echo [1] Wasfa API:         http://localhost:5000
echo [2] Prescriber Station: http://localhost:4200
echo [3] Pharmacy App:       http://localhost:8081
echo [4] Patient App:        http://localhost:8082
echo.

echo Launching API server...
start "Wasfa API" cmd /k "cd /d ""%~dp0"" && dotnet run --project src/Wasfa.Api"

echo Waiting 3 seconds for API to initialize...
timeout /t 3 /nobreak >nul

echo Launching Prescriber Angular App (Port 4200)...
start "Wasfa Prescriber" cmd /k "cd /d ""%~dp0apps\prescriber"" && npm start"

echo Launching Pharmacy Flutter App (Port 8081)...
start "Wasfa Pharmacy" cmd /k "cd /d ""%~dp0apps\pharmacy"" && start http://localhost:8081/ && npx -y serve build/web -l 8081 --single"

echo Launching Patient Flutter App (Port 8082)...
start "Wasfa Patient" cmd /k "cd /d ""%~dp0apps\patient"" && start http://localhost:8082/ && npx -y serve build/web -l 8082 --single"

echo.
echo ========================================================
echo   All 4 services running smoothly in separate windows!
echo ========================================================
echo.
pause
