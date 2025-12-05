# Busy Accounting Stock Monitor Installation Script
# PowerShell script for installing the Windows Service with configuration

param(
    [switch]$Uninstall = $false
)

function Write-Header {
    Write-Host "Busy Accounting Stock Monitor Installation" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host ""
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Check if running as administrator
if (-not (Test-Administrator)) {
    Write-Host "This script must be run as Administrator." -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as administrator'." -ForegroundColor Red
    Read-Host "Press any key to exit"
    exit 1
}

Write-Header

if ($Uninstall) {
    $serviceName = "Busy Accounting Stock Monitor"
    
    # Check if service exists
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    if ($service) {
        Write-Host "Stopping service: $serviceName" -ForegroundColor Yellow
        Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
        
        Write-Host "Waiting for service to stop..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3
        
        Write-Host "Removing service: $serviceName" -ForegroundColor Yellow
        sc delete $serviceName
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Service removed successfully." -ForegroundColor Green
        } else {
            Write-Host "Failed to remove service. It may require administrator privileges." -ForegroundColor Red
        }
    } else {
        Write-Host "Service '$serviceName' not found." -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Uninstallation completed." -ForegroundColor Green
    Read-Host "Press any key to exit"
    exit 0
}

# Configuration setup
Write-Host "Running configuration setup..." -ForegroundColor Yellow
Write-Host ""

# Get the directory where this script is located
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$setupDir = Join-Path $scriptDir "Setup"
$setupExe = Join-Path $setupDir "Setup.exe"
$watcherServiceDir = Join-Path $scriptDir "WatcherService"
$watcherServiceExe = Join-Path $watcherServiceDir "WatcherService.exe"

# Check if setup executable exists
if (-not (Test-Path $setupExe)) {
    Write-Host "Setup executable not found at: $setupExe" -ForegroundColor Red
    Write-Host "Please build the Setup project first." -ForegroundColor Red
    Read-Host "Press any key to exit"
    exit 1
}

# Run the configuration setup
Start-Process -FilePath $setupExe -Wait -WorkingDirectory $setupDir

Write-Host ""
Write-Host "Configuration completed. Installing Windows Service..." -ForegroundColor Yellow
Write-Host ""

$serviceName = "Busy Accounting Stock Monitor"

# Check if service already exists and remove it
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    Start-Sleep -Seconds 3
    
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    sc delete $serviceName
    Start-Sleep -Seconds 3
}

# Install the service
Write-Host "Installing service: $serviceName" -ForegroundColor Yellow
$installResult = sc create $serviceName binPath= $watcherServiceExe start= auto DisplayName= "Busy Accounting Stock Monitor"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service installed successfully." -ForegroundColor Green
    
    # Start the service
    Write-Host "Starting service..." -ForegroundColor Yellow
    Start-Service -Name $serviceName
    
    if ($?) {
        Write-Host "Service started successfully." -ForegroundColor Green
    } else {
        Write-Host "Failed to start service. Check the configuration." -ForegroundColor Red
    }
} else {
    Write-Host "Failed to install service. Check permissions and paths." -ForegroundColor Red
    Write-Host "Error code: $LASTEXITCODE" -ForegroundColor Red
}

Write-Host ""
Write-Host "Installation completed." -ForegroundColor Green
Write-Host ""

# Show service status
$serviceStatus = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($serviceStatus) {
    Write-Host "Service Status: $($serviceStatus.Status)" -ForegroundColor Cyan
    Write-Host "Service Name: $($serviceStatus.Name)" -ForegroundColor Cyan
}

Read-Host "Press any key to exit"