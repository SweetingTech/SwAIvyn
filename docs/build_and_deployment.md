# Build and Deployment Guide

This document explains how to build and deploy SwAIvyn as a self-contained application.

## Build Process

SwAIvyn uses a streamlined build process that packages both the frontend and backend into a single executable.

### Prerequisites

- .NET 7 SDK
- Node.js and npm
- PowerShell

### Build Scripts

The project includes several build scripts:

- **build-app.ps1**: Main build script that builds both frontend and backend
- **build-frontend.ps1**: Builds only the frontend
- **launch-app.ps1**: Launches the application

### Building the Complete Application

To build the complete application:

```powershell
.\scripts\build-app.ps1
```

This script:

1. Creates a `dist` directory in the project root
2. Builds the frontend React application
3. Copies the frontend build to the backend's `wwwroot` folder
4. Publishes the backend as a self-contained application
5. Places the executable in the `dist` folder
6. Creates a shortcut in the project root

### Build Parameters

The `build-app.ps1` script accepts several parameters:

- **Configuration**: Build configuration (`Release` by default)
- **Runtime**: Target runtime (`win-x64` by default)
- **SkipFrontend**: Skip building the frontend (`false` by default)
- **OutputDir**: Output directory (`dist` by default)

Example with custom parameters:

```powershell
.\scripts\build-app.ps1 -Configuration Debug -Runtime win-x64 -OutputDir "output"
```

## Deployment

### Single-Machine Deployment

For deploying on a single machine:

1. Build the application using `build-app.ps1`
2. Copy the entire `dist` folder to the target machine
3. Run `SwAIvyn.exe` from the `dist` folder

### Creating an Installer

To create an installer:

1. Build the application using `build-app.ps1`
2. Use a tool like InnoSetup or NSIS to create an installer
3. Include the contents of the `dist` folder
4. Add any necessary registry entries or shortcuts

Example InnoSetup script (basic outline):

```
[Setup]
AppName=SwAIvyn
AppVersion=1.0
DefaultDirName={pf}\SwAIvyn
DefaultGroupName=SwAIvyn
OutputDir=installer

[Files]
Source: "dist\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\SwAIvyn"; Filename: "{app}\SwAIvyn.exe"
Name: "{commondesktop}\SwAIvyn"; Filename: "{app}\SwAIvyn.exe"
```

### Windows Service Installation

To install as a Windows service:

1. Build the application
2. Use the provided `install-service.ps1` script:

```powershell
.\scripts\install-service.ps1 -BinaryPath "C:\Path\To\SwAIvyn.exe"
```

This script:
- Creates a Windows service named "SwAIvyn"
- Sets it to start automatically
- Configures it to run the application

## Configuration

### Application Settings

The application settings are stored in `appsettings.json`:

```json
{
  "AppSettings": {
    "BaseUrl": "http://localhost:5000",
    "DataDirectory": "../data",
    "OllamaApiUrl": "http://localhost:11434",
    "LmStudioApiUrl": "http://localhost:5000",
    "Neo4jUri": "http://localhost:7474",
    "Neo4jUser": "neo4j",
    "Neo4jPassword": "password",
    "Neo4jBoltPort": 7687,
    "Neo4jHttpPort": 7474,
    "Neo4jEmbedded": false,
    "RequireNeo4j": false
  }
}
```

### Environment-Specific Settings

For environment-specific settings, use:
- `appsettings.Development.json` for development
- `appsettings.Production.json` for production

These files override the settings in the base `appsettings.json` file.

## Troubleshooting Build Issues

### Common Build Problems

1. **Missing .NET SDK**:
   - Error: "The term 'dotnet' is not recognized..."
   - Solution: Install .NET 7 SDK

2. **Missing Node.js**:
   - Error: "The term 'npm' is not recognized..."
   - Solution: Install Node.js and npm

3. **Build Fails with Errors**:
   - Check the error messages for specific issues
   - Verify that all dependencies are installed
   - Check that the project files are not corrupted

### Build Logs

Build logs are output to the console during the build process. For more detailed logs:

```powershell
.\scripts\build-app.ps1 > build.log 2>&1
```

## Continuous Integration

For CI/CD pipelines, you can use the build scripts directly:

### GitHub Actions Example

```yaml
name: Build SwAIvyn

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v2

    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 7.0.x

    - name: Setup Node.js
      uses: actions/setup-node@v2
      with:
        node-version: '16'

    - name: Build
      run: .\scripts\build-app.ps1

    - name: Upload artifact
      uses: actions/upload-artifact@v2
      with:
        name: SwAIvyn
        path: dist/
```

## Release Process

1. Update version number in `backend/SwAIvyn.csproj`
2. Build the application
3. Test the built executable
4. Create a release package (zip or installer)
5. Publish the release
