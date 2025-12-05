@echo off
setlocal

echo Busy Accounting Stock Monitor Installation
echo ========================================

REM Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"

REM Navigate to the setup directory
cd /d "%SCRIPT_DIR%"

echo.
echo Running configuration setup...
echo.

REM Run the configuration setup
"%SCRIPT_DIR%Setup.exe"

if %ERRORLEVEL% NEQ 0 (
    echo Setup failed. Installation cancelled.
    pause
    exit /b 1
)

echo.
echo Configuration completed. Installing Windows Service...
echo.

REM Install the service using sc command
set "SERVICE_NAME=Busy Accounting Stock Monitor"
set "SERVICE_PATH=%SCRIPT_DIR%..\WatcherService\WatcherService.exe"

REM Check if service already exists and remove it if needed
sc query "%SERVICE_NAME%" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo Stopping existing service...
    sc stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 3 /nobreak >nul
    
    echo Removing existing service...
    sc delete "%SERVICE_NAME%" >nul 2>&1
    timeout /t 3 /nobreak >nul
)

echo Installing service...
sc create "%SERVICE_NAME%" binPath= "%SERVICE_PATH%" start= auto DisplayName= "Busy Accounting Stock Monitor"
if %ERRORLEVEL% EQU 0 (
    echo Service installed successfully.
    
    REM Start the service
    sc start "%SERVICE_NAME%"
    if %ERRORLEVEL% EQU 0 (
        echo Service started successfully.
    ) else (
        echo Failed to start service. Check the configuration.
    )
) else (
    echo Failed to install service. Check permissions and paths.
)

echo.
echo Installation completed.
echo.

pause