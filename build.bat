@echo off
setlocal

echo Building Busy Accounting Stock Monitor
echo ======================================

REM Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"

REM Build the main WatcherService
echo.
echo Building WatcherService...
echo.
cd /d "%SCRIPT_DIR%WatcherService"
dotnet publish -c Release -r win-x64 --self-contained --framework net10.0

if %ERRORLEVEL% NEQ 0 (
    echo Failed to build WatcherService
    pause
    exit /b 1
)

REM Build the Setup project
echo.
echo Building Setup project...
echo.
cd /d "%SCRIPT_DIR%Setup"
dotnet publish -c Release -r win-x64 --self-contained --framework net10.0

if %ERRORLEVEL% NEQ 0 (
    echo Failed to build Setup project
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo.
echo To install the service:
echo   1. Run install.bat or install.ps1 as Administrator
echo   2. The setup wizard will guide you through configuration
echo.

pause