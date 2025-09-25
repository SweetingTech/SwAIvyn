# Neo4j Configuration Guide for SwAIvyn

This document provides information about Neo4j configuration in the SwAIvyn application, including authentication credentials and file locations.

## Overview

SwAIvyn uses Neo4j as its graph database for storing and querying relationships between memory nodes. The application can be configured to use either an embedded Neo4j instance or connect to a remote Neo4j server.

## Configuration Settings

Neo4j configuration is managed through the `appsettings.json` file:

```json
{
  "AppSettings": {
    "Neo4jUri": "http://localhost:7474",
    "Neo4jUser": "neo4j",
    "Neo4jPassword": "password",
    "Neo4jBoltPort": 7687,
    "Neo4jHttpPort": 7474,
    "Neo4jEmbedded": false,
    "RequireNeo4j": false,
    "Neo4jJavaHome": ""
  }
}
```

### Settings Explanation

| Setting | Description | Default Value |
|---------|-------------|---------------|
| `Neo4jUri` | The HTTP URI for connecting to Neo4j | `http://localhost:7474` |
| `Neo4jUser` | Username for Neo4j authentication | `neo4j` |
| `Neo4jPassword` | Password for Neo4j authentication | `password` |
| `Neo4jBoltPort` | Port for Neo4j Bolt protocol | `7687` |
| `Neo4jHttpPort` | Port for Neo4j HTTP API | `7474` |
| `Neo4jEmbedded` | Whether to use embedded Neo4j mode | `false` |
| `RequireNeo4j` | Whether Neo4j is required for application startup | `false` |
| `Neo4jJavaHome` | Optional path to Java home directory for Neo4j | `""` (uses system Java) |

## Authentication

### Default Credentials

The default Neo4j credentials are:
- **Username**: `neo4j`
- **Password**: `password`

These credentials are used for:
1. Connecting to the Neo4j server from the application
2. Logging into the Neo4j Browser interface at `http://localhost:7474/`

### Authentication File Location

When SwAIvyn starts Neo4j, it creates an authentication file at:
```
%AppData%\SwAIvyn\neo4j\conf\auth
```

This file contains the username and password in the format:
```
neo4j:password
```

## Neo4j Directory Structure

SwAIvyn creates and manages the following Neo4j directory structure:

```
%AppData%\SwAIvyn\neo4j\
+-- bin/                  # Neo4j binaries
+-- conf/                 # Configuration files
   +-- neo4j.conf        # Main Neo4j configuration
   +-- auth              # Authentication file
   +-- ...               # Other Neo4j config files
+-- data/                 # Database files
+-- lib/                  # Neo4j libraries
+-- logs/                 # Neo4j log files
+-- plugins/              # Neo4j plugins
```

## Starting Neo4j

SwAIvyn starts Neo4j using a direct Java command:

```
java -cp "%AppData%\SwAIvyn\neo4j\lib\*" -Dbasedir="%AppData%\SwAIvyn\neo4j" org.neo4j.server.startup.Neo4jCommand console
```

This approach:
1. Bypasses PowerShell/batch script issues
2. Works with Java 21 (required for Neo4j 2025.04.0)
3. Allows for better error handling and logging

## Changing Neo4j Credentials

To change the Neo4j credentials:

1. Update the `Neo4jUser` and `Neo4jPassword` settings in `appsettings.json`
2. Delete the existing auth file at `%AppData%\SwAIvyn\neo4j\conf\auth`
3. Restart the application

The application will create a new auth file with the updated credentials.

## Troubleshooting

### Common Neo4j Issues

1. **Authentication Failures**:
   - Verify the credentials in `appsettings.json`
   - Check if the auth file exists and contains the correct credentials
   - Try deleting the auth file and restarting the application

2. **Neo4j Fails to Start**:
   - Check if Java is installed and in the system PATH
   - Verify that the required Java version (21+) is available
   - Check the Neo4j logs in `%AppData%\SwAIvyn\neo4j\logs`

3. **Port Conflicts**:
   - If ports 7474 or 7687 are already in use, change the port settings in `appsettings.json`
   - Verify no other Neo4j instances are running

### Viewing Neo4j Logs

Neo4j logs are stored in:
```
%AppData%\SwAIvyn\neo4j\logs\
```

The application also logs Neo4j-related information in the main application logs.

## Advanced Configuration

For advanced Neo4j configuration, you can modify the Neo4j configuration file directly:
```
%AppData%\SwAIvyn\neo4j\conf\neo4j.conf
```

Note that some settings in this file will be overwritten by SwAIvyn on startup, including:
- Network binding settings (both old `dbms.*` and new `server.*` formats)
- Authentication settings

### Neo4j 2025.04.0 Configuration Notes

Neo4j 2025.04.0 uses different configuration setting names than previous versions. SwAIvyn handles this by:

1. Setting `server.config.strict_validation.enabled=false` to allow deprecated settings
2. Using the new setting format for all configuration:

| Old Setting (Deprecated) | New Setting (2025.04.0) |
|--------------------------|-------------------------|
| `dbms.default_listen_address` | `server.default_listen_address` |
| `dbms.connector.bolt.listen_address` | `server.bolt.listen_address` |
| `dbms.connector.http.listen_address` | `server.http.listen_address` |
| `dbms.windows_service_name` | `server.windows_service_name` |
| `dbms.jvm.additional` | `server.jvm.additional` |

The setting `dbms.security.auth_provider.plugin` has been removed in Neo4j 2025.04.0.

### Troubleshooting Neo4j Startup Issues

If Neo4j fails to start, check the following:

1. **Configuration Validation Errors**:
   - Look for messages like "Configuration file validation failed" in the logs
   - Check for unrecognized settings or deprecated settings
   - The application now sets `server.config.strict_validation.enabled=false` to avoid these errors

2. **Process Termination Issues**:
   - The application now handles process termination more gracefully
   - If you see "The CancellationTokenSource has been disposed" errors, update to the latest version

## References

- [Neo4j Documentation](https://neo4j.com/docs/)
- [Neo4j Configuration Settings](https://neo4j.com/docs/operations-manual/current/configuration/neo4j-conf/)
- [Neo4j Security](https://neo4j.com/docs/operations-manual/current/security/)
