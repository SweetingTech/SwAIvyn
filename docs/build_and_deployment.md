# Build and Deployment Guide

This document explains how to build and deploy SwAIvyn as a self-contained application.

## Quick Start - Complete Setup

For a fresh installation or complete rebuild, use the comprehensive setup script:

```powershell
.\scripts\full-setup.ps1
```

This script performs a complete setup from scratch, including:
1. **Environment validation** (.NET 8.0 SDK, Node.js v18+, npm)
2. **Directory structure creation** (data/, logs/, dist/, etc.)
3. **Database initialization** (tables creation, schema updates)
4. **Dependency installation** (NuGet packages, npm packages)
5. **Frontend build** (Vite build process)
6. **Backend compilation** (self-contained executable)
7. **Asset copying** (SQLite-VSS, configurations, icons)
8. **Character data validation** (GLaDOS, Sherlock, SAM loading)

### Setup Script Parameters

```powershell
.\scripts\full-setup.ps1 [parameters]
```

**Available Parameters:**
- `-Configuration` (Release/Debug): Build configuration (default: Release)
- `-Runtime` (win-x64/win-arm64): Target runtime (default: win-x64)
- `-CleanDatabase`: Remove and recreate database from scratch
- `-SkipBuild`: Only setup dependencies, skip actual build
- `-SkipFrontend`: Skip building the frontend portion

**Examples:**
```powershell
# Complete clean setup with new database
.\scripts\full-setup.ps1 -CleanDatabase

# Development setup without building
.\scripts\full-setup.ps1 -Configuration Debug -SkipBuild

# Backend-only build (for API development)
.\scripts\full-setup.ps1 -SkipFrontend
```

## Build Process Overview

SwAIvyn uses a streamlined build process that packages both the frontend and backend into a single executable.

### Prerequisites

- **.NET 8.0 SDK** or later
- **Node.js v18** or later with npm
- **PowerShell** (Windows PowerShell 5.1 or PowerShell Core 7+)

### Build Scripts

The project includes several build scripts:

- **complete-setup.ps1**: Comprehensive setup and build script (recommended)
- **build-app.ps1**: Legacy build script for frontend and backend only
- **build-frontend.ps1**: Builds only the frontend
- **dev-setup.ps1**: Development environment setup only
- **launch-app.ps1**: Launches the application

### Manual Build Steps

If you need to build manually or understand the process:

#### 1. Environment Setup
```powershell
.\scripts\dev-setup.ps1
```

#### 2. Database Initialization
```powershell
# Build and run database tools
cd tools\CreateTables
dotnet run --configuration Release

cd ..\UpdateDatabase  
dotnet run --configuration Release
cd ..\..
```

#### 3. Frontend Build
```powershell
cd frontend
npm install
npm run build
cd ..
```

#### 4. Backend Build
```powershell
cd backend
dotnet publish SwAIvyn.csproj --configuration Release --runtime win-x64 --self-contained true --output ..\ -p:PublishSingleFile=true
cd ..
```

## Database Setup

The complete setup script automatically handles database initialization, but you can also run it manually:

### Automatic Database Setup (Recommended)
```powershell
.\scripts\complete-setup.ps1
```

### Manual Database Setup
```powershell
# Create database schema
cd tools\CreateTables
dotnet build --configuration Release
dotnet run --configuration Release

# Update database with latest schema
cd ..\UpdateDatabase
dotnet build --configuration Release  
dotnet run --configuration Release
cd ..\..
```

### Database Location
- **Development**: `C:\Users\[username]\Desktop\data\swai-vyn.db`
- **Production**: Configured via `appsettings.json`

The database includes:
- User management tables
- Avatar/Character definitions
- Conversation and message storage
- Settings and preferences
- Chat index for search functionality

### Character Data Loading

Characters are automatically loaded from the filesystem at startup:
- **GLaDOS**: `frontend\AI\GLaDOS\GLaDOS_Character_card.yaml`
- **Sherlock**: `frontend\AI\Sherlock\Sherlock_Character_card.yaml`  
- **Sam**: `frontend\AI\Sam\Sam_Character_card.yaml`

The default character (GLaDOS) is automatically set up during database initialization.

## Troubleshooting Build Issues

### Database Schema Issues

If you encounter database schema errors:
1. Stop the application
2. Run the complete setup script: `.\scripts\complete-setup.ps1 -CleanBuild`
3. This will recreate the database with the correct schema

### Missing Dependencies

**SQLite-VSS Extension Missing:**
- Vector search functionality will be disabled
- Place `sqlite-vss.dll` in the root directory to enable
- The application will run without it but with limited search capabilities

**Character Loading Failures:**
- Check that character YAML files exist in `frontend\AI\[CharacterName]\`
- Verify database schema is up to date
- Check logs in the `logs\` directory for specific errors

### Common Build Problems

1. **Database Column Missing Errors**:
   ```
   SQLite Error 1: 'no such column: a.AlternateGreetings'
   ```
   - **Solution**: Run `.\scripts\complete-setup.ps1 -SkipFrontend -SkipDependencies`

2. **Character Loading Failures**:
   ```
   Error processing character directory
   ```
   - **Solution**: Ensure database schema is updated, run complete setup

3. **Missing .NET SDK**:
   - **Error**: "The term 'dotnet' is not recognized..."
   - **Solution**: Install .NET 8.0 SDK from https://dotnet.microsoft.com/download

4. **Missing Node.js**:
   - **Error**: "The term 'npm' is not recognized..."
   - **Solution**: Install Node.js v18+ from https://nodejs.org/

5. **Build Fails with Errors**:
   - Check the error messages for specific issues
   - Run with `-Verbose` flag for detailed output
   - Verify that all dependencies are installed
   - Try a clean build with `-CleanBuild` flag

### Build Logs

Build logs are output to the console during the build process. For detailed logs:

```powershell
.\scripts\complete-setup.ps1 -Verbose > build.log 2>&1
```

Application runtime logs are stored in:
- `logs\SwAIvyn_[timestamp].log`

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
