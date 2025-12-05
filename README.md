# Busy Accounting Stock Monitor Service

This Windows Service monitors the Busy Accounting Software database and sends real-time stock and price updates to an API endpoint.

## Features
- Monitors stock levels and product prices from Busy database
- Uses incremental updates to minimize database load
- Runs as a Windows Service for continuous operation
- Sends data to API endpoint in JSON format
- Configurable polling interval
- Interactive setup wizard for configuration during installation

## Prerequisites

- .NET 10.0 Runtime
- Access to Busy Accounting database
- API endpoint to receive data updates

## Quick Installation

1. Run `build.bat` to compile the service and setup tool
2. Run `install.bat` or `install.ps1` as Administrator
3. Follow the interactive setup wizard to configure your database and API settings
4. The service will be installed and started automatically

## Manual Configuration

If you prefer to manually configure the service, update the `appsettings.json` file with your specific settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_AWS_SERVER_IP,3011;Database=BusyDatabase;User Id=your_username;Password=your_password;TrustServerCertificate=True;"
  },
  "ApiSettings": {
    "Endpoint": "http://your-api-endpoint/api/stock/update"
  },
  "PollingInterval": {
    "Seconds": 30
  }
}
```

### Connection String Parameters:
- `Server`: IP address and port (3011) of your Busy database server
- `Database`: Name of the Busy database
- `User Id`: Database username
- `Password`: Database password

### API Settings:
- `Endpoint`: The URL where product data should be sent

### Polling Interval:
- `Seconds`: How often (in seconds) to check for database changes

## Installation Using Setup Wizard

1. Publish the applications:
   ```bash
   build.bat
   ```

2. Run the installation script as Administrator:
   ```cmd
   install.bat
   ```
   Or using PowerShell:
   ```powershell
   .\install.ps1
   ```

3. The setup wizard will guide you through:
   - Database server configuration (IP, port 3011)
   - Database name, user and password
   - Connection test
   - API endpoint configuration
   - Polling interval settings

4. The service will be installed and started automatically

## Running for Development

To run without installing as a service:
```bash
dotnet WatcherService.dll
```

## Service Management

- Start: `sc start "Busy Accounting Stock Monitor"`
- Stop: `sc stop "Busy Accounting Stock Monitor"`
- Remove: Use `uninstall.bat` or `.\install.ps1 -Uninstall`

## Logs

The service logs to the Windows Event Log. You can also configure file logging by updating the logging configuration in appsettings.json.

## Troubleshooting

If the service fails to start:
1. Check the Windows Event Log for error messages
2. Verify the database connection string
3. Ensure the API endpoint is accessible
4. Check file permissions on the service executable
5. Run the service in console mode first to see detailed error messages

## Data Schema

The service sends an array of product objects with the following properties:
```json
[
  {
    "code": 1152,
    "itemName": "Product Name",
    "printName": "Print Name",
    "salePrice": 19.99,
    "costPrice": 15.50,
    "totalAvailableStock": 100,
    "lastModified": "2023-12-05T10:30:00Z"
  }
]
```

## Security Considerations

- Store database credentials securely
- Use HTTPS for API endpoints
- Implement API authentication and authorization
- Regularly update the service to address security vulnerabilities
- Run the service under a dedicated, limited-privilege account