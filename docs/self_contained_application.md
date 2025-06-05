# Self-Contained Application Guide

This document explains how SwAIvyn is packaged as a self-contained application with a DNS-like naming system.

## Single Executable Architecture

SwAIvyn is designed as a self-contained application that:

1. Runs as a single `.exe` file
2. Embeds the frontend React application
3. Serves the UI through an integrated web server
4. Opens a browser window automatically
5. Uses a DNS-like naming system for service discovery

## How It Works

### Backend (ASP.NET Core)

- The backend is compiled as a self-contained application using:
  ```
  dotnet publish -p:PublishSingleFile=true --self-contained true
  ```
- Static files (the frontend) are embedded in the executable
- When launched, it starts a web server on a configurable port
- The server automatically opens a browser window pointing to the application

### Frontend (React)

- The React application is built using Vite
- The build output is copied to the backend's `wwwroot` folder
- The backend serves these files as static content
- All API calls use relative URLs that are handled by the backend

### DNS-like Naming System

Instead of hardcoding ports and URLs, SwAIvyn uses a configuration service that:

1. Provides a central registry of service endpoints
2. Allows services to be referenced by logical names
3. Enables dynamic reconfiguration without code changes
4. Handles service discovery automatically

## Configuration Service

The `ConfigurationService` provides:

- A backend service that exposes endpoint information
- A frontend service that fetches and caches this information
- Fallback to default values if the configuration can't be fetched

### Backend Implementation

```csharp
public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly string _baseUrl;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
    }

    public string GetApiBaseUrl()
    {
        return _baseUrl;
    }

    public string GetSignalRHubUrl(string hubName)
    {
        return $"{_baseUrl}/hubs/{hubName}";
    }

    public Dictionary<string, string> GetAllEndpoints()
    {
        return new Dictionary<string, string>
        {
            { "api", GetApiBaseUrl() },
            { "chatHub", GetSignalRHubUrl("chat") },
            { "voiceHub", GetSignalRHubUrl("voice") },
            { "notificationHub", GetSignalRHubUrl("notification") }
        };
    }
}
```

### Frontend Implementation

```typescript
class ConfigService {
  private static instance: ConfigService;
  private endpoints: Endpoints | null = null;

  public static getInstance(): ConfigService {
    if (!ConfigService.instance) {
      ConfigService.instance = new ConfigService();
    }
    return ConfigService.instance;
  }

  public async getEndpoints(): Promise<Endpoints> {
    if (this.endpoints) {
      return this.endpoints;
    }

    try {
      const response = await fetch('/api/config');
      if (response.ok) {
        return await response.json();
      }
    } catch (error) {
      console.warn('Failed to fetch configuration, using defaults', error);
    }

    // Fallback to default values
    return {
      api: '/api',
      chatHub: '/hubs/chat',
      voiceHub: '/hubs/voice',
      notificationHub: '/hubs/notification'
    };
  }

  // Additional methods to get specific endpoints...
}
```

## Building and Running

### Building the Application

The `build-app.ps1` script:

1. Builds the frontend React application
2. Copies the built files to the backend's `wwwroot` folder
3. Publishes the backend as a self-contained application
4. Places the executable in the `dist` folder
5. Creates a shortcut in the project root

### Running the Application

The `SwAIvyn.cmd` script:

1. Checks if the application is built
2. Builds it if necessary
3. Launches the executable
4. Keeps a console window open while the application is running

## Customization

### Changing the Base URL

To change the base URL:

1. Edit `appsettings.json`
2. Update the `AppSettings:BaseUrl` value
3. Rebuild the application

### Adding New Services

To add a new service:

1. Add the service endpoint to `ConfigurationService.GetAllEndpoints()`
2. Add a corresponding method to get the specific endpoint
3. Update the frontend `ConfigService` to include the new endpoint

## Troubleshooting

### Common Issues

- **Browser doesn't open**: Check if the port is already in use
- **Services can't connect**: Verify the base URL in `appsettings.json`
- **Frontend not loading**: Check if the frontend was built correctly

### Logs

- Application logs are stored in the `logs` folder
- Check the console output for startup errors
