@echo off
setlocal

echo Busy Accounting Stock Monitor Uninstallation
echo ============================================

set "SERVICE_NAME=Busy Accounting Stock Monitor"

REM Check if service exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Service "%SERVICE_NAME%" not found.
    echo Uninstallation cancelled.
    pause
    exit /b 0
)

echo Stopping service...
sc stop "%SERVICE_NAME%" >nul 2>&1

timeout /t 3 /nobreak >nul

echo Removing service...
sc delete "%SERVICE_NAME%" >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo Service "%SERVICE_NAME%" removed successfully.
) else (
    echo Failed to remove service. It may require administrator privileges.
)

echo.
echo Uninstallation completed.
echo.

pause