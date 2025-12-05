# BASM (Busy Accounting Stock Monitor)

A .NET Worker Service that monitors Busy accounting database and sends stock/product data updates to a remote API endpoint.

## Overview

This application is designed to run as a Windows Service that periodically checks a Busy accounting database for product and stock changes, then sends the updated information to a configured remote API endpoint.

## Features

- Monitors Busy accounting database (Master1 and Tran2 tables)
- Extracts product information (codes, names, prices, stock levels)
- Sends data to remote API at configurable intervals
- Runs as a background Windows Service
- Self-contained deployment with no external dependencies
- Built-in retry mechanism for network failures

## Architecture

- **.NET 10.0** Worker Service
- **Dapper** for database access
- **SQL Server** database connectivity
- **Windows Service** installation capability (Service Name: BASM)
- Configurable polling interval (default 30 seconds)

## Installation

### Prerequisites
- Windows Server with .NET 10.0 runtime (or use self-contained deployment)
- Access to Busy accounting SQL Server database
- Remote API endpoint to receive data

### Deployment Steps

1. Download and extract the published application files
2. Update `appsettings.json` with database connection and API endpoint
3. Run `install.bat` as Administrator to install as Windows Service (BASM)
4. Start the service manually or restart the server

### Configuration

The application is configured via `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=your-database;Integrated Security=True;"
  },
  "ApiSettings": {
    "Endpoint": "https://your-api.com/api/stock/update"
  },
  "PollingInterval": {
    "Seconds": 30
  }
}
```

## Usage

Once installed as a Windows Service, the application runs continuously in the background. It:
- Connects to the configured Busy database
- Queries for product and stock information
- Sends data to the remote API endpoint at configured intervals
- Logs all activity to Windows Event Log

### Service Management Commands

- **Start Service:** `net start BASM`
- **Stop Service:** `net stop BASM`
- **Check Status:** `sc query BASM`

## Uninstallation

Run `uninstall.bat` as Administrator to remove the Windows Service (BASM).

## Security

- Uses Windows Authentication by default for database connections
- Supports SSL for API communication
- No sensitive data stored in configuration

## Monitoring

- Service status visible in Windows Services panel (look for "BASM")
- Application logs available in Windows Event Log under "Application" logs
- Error handling with automatic retry mechanism

## Development

The project includes:
- Main service (ProductWatcherWorker)
- Database access layer (ProductDataService)
- API communication layer (ApiService)
- Setup utility for configuration
- Installation scripts for Windows Service (BASM)

## Enterprise Deployment

- Self-contained deployment package
- No external dependencies required
- Ready for AWS Windows Server deployment
- Production-ready configuration
- Short service name (BASM) for easy management

## Support

For issues or questions, check Windows Event Logs for application errors.

---

*This application was developed to provide seamless integration between Busy accounting systems and remote inventory management solutions.*