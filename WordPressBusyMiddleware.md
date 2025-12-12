# WordPress-Busy Integration Middleware

A middleware application that connects the Busy Accounting Stock Monitor (Watcher Service) with WordPress/WooCommerce, enabling automatic price and inventory synchronization.

## Overview

This application serves as an intermediary between your Busy accounting system and WordPress website. It receives inventory data from the Watcher Service and automatically updates WordPress products:
- Updates prices of existing products using `ItemName` as the matching key
- Creates new products in draft status for manual review before publishing
- Provides a dashboard to monitor all synchronization activities

## Features

- **Automatic Price Updates**: Updates WooCommerce product prices based on Busy `SalePrice` values
- **Inventory Sync**: Updates stock quantities based on Busy `TotalAvailableStock` values  
- **Smart Product Matching**: Uses `ItemName` to match Busy products with WordPress products
- **Draft Creation**: Creates new products in draft status for manual verification
- **Real-time Dashboard**: Monitor all sync operations and troubleshoot issues
- **Error Handling**: Comprehensive logging and retry mechanisms
- **Secure Communication**: API authentication and data validation

## Architecture

```
Busy Database → Watcher Service → Middleware API → WordPress/WooCommerce
                                      ↑              ↓
                                 (Dashboard & UI)
```

## Requirements

### Prerequisites
- .NET 6.0 or higher (for the middleware application)
- WordPress website with WooCommerce plugin
- WordPress Application Password for API access
- Busy Accounting Stock Monitor (Watcher Service) configured
- Access to both systems from the middleware server

### WordPress Setup
1. Install and activate WooCommerce plugin
2. Generate Application Password:
   - Go to Users → Your Profile → Application Passwords
   - Create a new application password for the middleware
   - Grant `read`, `write`, and `manage_woocommerce` permissions

## Installation

### 1. Deploy Middleware Application
```bash
# Clone the repository (replace with actual repository)
git clone <repository-url>
cd wordpress-busy-middleware

# Build the application
dotnet build

# Publish for deployment
dotnet publish -c Release
```

### 2. Configure Application Settings
Update `appsettings.json` with your configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=BusyWordpressMiddleware;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "WordPressSettings": {
    "SiteUrl": "https://your-wordpress-site.com",
    "Username": "your-username",
    "ApplicationPassword": "your-application-password"
  },
  "MiddlewareSettings": {
    "ReceiveEndpoint": "/api/busy/update",
    "AuthKey": "your-secret-auth-key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 3. Configure Watcher Service
Update the Watcher Service `appsettings.json` to point to your middleware:

```json
{
  "ApiSettings": {
    "Endpoint": "https://your-middleware-domain.com/api/busy/update"
  }
}
```

### 4. Run the Application
```bash
# Development
dotnet run

# Production (after publishing)
cd published-folder
dotnet YourApp.dll
```

## Configuration

### WordPress Settings
- `SiteUrl`: Your WordPress site URL (without trailing slash)
- `Username`: WordPress username for API access
- `ApplicationPassword`: WordPress Application Password

### Middleware Settings
- `ReceiveEndpoint`: The endpoint where Watcher Service sends data
- `AuthKey`: Authentication key for securing the endpoint

## Data Flow

1. **Watcher Service**: Polls Busy database and sends product data
2. **Middleware**: Receives data and processes it
3. **Product Matching**: Uses `ItemName` to find existing WordPress products
4. **Update/Creation**: 
   - Updates existing products with new prices and stock
   - Creates new products in draft status for review
5. **WordPress**: Receives updates via REST API

## Product Matching Strategy

The middleware uses the following logic to match Busy products with WordPress products:
1. Uses `ItemName` field from Busy as the primary matching key
2. Performs exact match by comparing with WordPress product titles
3. For new products, creates them with name, price, and stock information
4. All new products are created in "draft" status for manual review

## Dashboard Interface

The middleware provides a web-based dashboard at the root URL (`/`) where you can:
- View real-time sync status
- Monitor successful and failed operations
- See detailed logs of all operations
- Manually trigger resynchronization
- Review and troubleshoot errors

## Security

- All API endpoints require authentication
- WordPress communication uses secure Application Passwords
- HTTPS recommended for all communications
- Rate limiting implemented to prevent abuse
- Input validation on all received data

## Monitoring & Troubleshooting

### Log Files
- Application logs are stored in the configured logging destination
- Detailed logs of all WordPress operations
- Error tracking and debugging information

### Dashboard Monitoring
- Real-time sync status
- Operation history and statistics
- Error reporting and alerts

## API Endpoints

### Receive Data from Watcher Service
- **Method**: POST
- **URL**: `/api/busy/update`
- **Content-Type**: `application/json`
- **Authentication**: API key in headers
- **Request**: Array of product data objects from Busy system

### Dashboard Interface
- **Method**: GET
- **URL**: `/`
- **Description**: Web interface for monitoring and management

## Supported Data Fields

The middleware processes the following fields from Busy:

- `Code`: Product code from Busy system
- `ItemName`: Product name (used for matching)
- `PrintName`: Alternative product name
- `SalePrice`: Price to update in WordPress
- `CostPrice`: Cost price information
- `TotalAvailableStock`: Inventory quantity
- `LastModified`: Timestamp of last change

## WordPress Customization

### Product Status
- Existing products: Updated with new prices and stock
- New products: Created in "draft" status for manual review

### WooCommerce Fields
- Regular price: Updated with `SalePrice` value
- Stock quantity: Updated with `TotalAvailableStock` value
- Product title: Used for matching with `ItemName`

## Troubleshooting

### Common Issues
1. **Authentication failures**: Verify WordPress Application Password permissions
2. **Product matching issues**: Ensure `ItemName` matches WordPress product titles
3. **Sync failures**: Check network connectivity between systems

### Logs
Check application logs for detailed error information and troubleshooting steps.

## Support

For issues or questions, check the application logs and dashboard interface for diagnostic information. Ensure all configuration settings are correct and both systems are accessible.

---

*This application enables seamless integration between Busy accounting systems and WordPress e-commerce websites.*