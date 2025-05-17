# DNS-like Naming System

This document explains the DNS-like naming system implemented in SwAIvyn for service discovery.

## Overview

SwAIvyn uses a configuration-based service discovery system that:

1. Eliminates hardcoded ports and URLs
2. Provides logical names for services
3. Centralizes configuration in one place
4. Enables dynamic reconfiguration without code changes

This approach is similar to how DNS resolves domain names to IP addresses, but for internal services.

## Architecture

The naming system consists of:

1. **Backend Configuration Service**: Provides endpoint information
2. **Configuration API**: Exposes configuration to clients
3. **Frontend Configuration Service**: Fetches and caches endpoint information
4. **Service Consumers**: Use logical names instead of hardcoded URLs

## Backend Implementation

### Configuration Service

The `ConfigurationService` class provides methods to get endpoint information:

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

### Configuration API

The `ConfigController` exposes the configuration to clients:

```csharp
[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfigurationService _configService;

    public ConfigController(IConfigurationService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        return Ok(_configService.GetAllEndpoints());
    }
}
```

### Registration in Program.cs

The service is registered in the dependency injection container:

```csharp
// Register the configuration service
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
```

## Frontend Implementation

### Configuration Service

The frontend `ConfigService` fetches and caches endpoint information:

```typescript
class ConfigService {
  private static instance: ConfigService;
  private endpoints: Endpoints | null = null;
  private isLoading = false;
  private loadPromise: Promise<Endpoints> | null = null;

  private constructor() {}

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

    if (this.isLoading) {
      return this.loadPromise!;
    }

    this.isLoading = true;
    this.loadPromise = this.fetchEndpoints();
    
    try {
      this.endpoints = await this.loadPromise;
      return this.endpoints;
    } finally {
      this.isLoading = false;
    }
  }

  public async getApiBaseUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.api;
  }

  public async getChatHubUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.chatHub;
  }

  // Additional methods for other endpoints...

  private async fetchEndpoints(): Promise<Endpoints> {
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
}
```

### Usage in Components

Components use the `ConfigService` to get endpoint URLs:

```typescript
// Example: Using the configuration service in a hook
export function useChatHub() {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);

  useEffect(() => {
    const setupConnection = async () => {
      try {
        const hubUrl = await configService.getChatHubUrl();
        const newConnection = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Information)
          .build();

        setConnection(newConnection);
      } catch (error) {
        console.error('Failed to get chat hub URL:', error);
      }
    };

    setupConnection();
  }, []);

  // Rest of the hook implementation...
}
```

## Configuration

### appsettings.json

The base URL is configured in `appsettings.json`:

```json
{
  "AppSettings": {
    "BaseUrl": "http://localhost:5000",
    "DataDirectory": "../data",
    "AvatarsDirectory": "../data/avatars",
    "UploadsDirectory": "../data/uploads",
    "BackupsDirectory": "../data/backups",
    "ModulesDirectory": "../data/modules",
    "OllamaApiUrl": "http://localhost:11434",
    "LmStudioApiUrl": "http://localhost:5000"
  }
}
```

### Changing the Base URL

To change the base URL:

1. Edit `appsettings.json`
2. Update the `AppSettings:BaseUrl` value
3. Restart the application

## Benefits

This DNS-like naming system provides several benefits:

1. **Centralized Configuration**: All endpoint URLs are defined in one place
2. **Flexibility**: Services can be moved to different ports or hosts without code changes
3. **Resilience**: Fallback to default values if configuration is unavailable
4. **Maintainability**: No hardcoded URLs scattered throughout the codebase
5. **Testability**: Easy to mock or override endpoints for testing

## Adding New Services

To add a new service:

1. Add the service endpoint to `ConfigurationService.GetAllEndpoints()`
2. Add a corresponding method to get the specific endpoint
3. Update the frontend `ConfigService` to include the new endpoint

## Troubleshooting

If services can't connect:

1. Check the base URL in `appsettings.json`
2. Verify that the service is running on the expected port
3. Check the network connectivity between services
4. Look for errors in the application logs
