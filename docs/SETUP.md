# SwAIvyn Setup Scripts

This directory contains setup scripts to get SwAIvyn running on your system.

## Quick Start (Recommended)

For most users, start with the quick setup:

```powershell
.\quick-setup.ps1
```

This will:
- ✅ Check prerequisites 
- 🏗️ Build the application
- 🚀 Create a start script
- ⚡ Get you running in ~2 minutes

### Quick Setup Options

```powershell
# Development setup (keeps source code, enables debugging)
.\quick-setup.ps1 -Dev

# Clean install (removes previous installation)
.\quick-setup.ps1 -Clean

# Both options
.\quick-setup.ps1 -Dev -Clean
```

## Full Setup (Advanced)

For production deployment or advanced configurations:

```powershell
.\setup.ps1
```

### Full Setup Options

```powershell
# Production setup with clean install
.\setup.ps1 -Mode Production -CleanInstall

# Development setup
.\setup.ps1 -Mode Development

# Install as Windows service (requires Admin)
.\setup.ps1 -Mode Service -InstallService

# Custom database location
.\setup.ps1 -DatabasePath "C:\MyData\swai-vyn.db"

# Custom port
.\setup.ps1 -Port 8080

# Skip prerequisite installation
.\setup.ps1 -SkipPrerequisites
```

## Prerequisites

The setup scripts will automatically install missing prerequisites, but you can install them manually:

### Required
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** - For the backend application
- **[Node.js 18+](https://nodejs.org/)** - For the frontend build process

### Optional (for advanced features)
- **[Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)** - For SQLite VSS compilation
- **[Git](https://git-scm.com/)** - For version control

## Manual Installation

If the automated scripts don't work for your environment:

1. **Install prerequisites** (see above)

2. **Build backend:**
   ```powershell
   cd backend
   dotnet restore
   dotnet publish -c Release -o ..\dist\backend
   ```

3. **Build frontend:**
   ```powershell
   npm install
   npm run build
   ```

4. **Setup database:**
   ```powershell
   # Database will be created automatically on first run
   mkdir data
   ```

5. **Start application:**
   ```powershell
   .\dist\backend\SwAIvyn.exe
   ```

## Troubleshooting

### Common Issues

**"dotnet command not found"**
- Install .NET 8 SDK from Microsoft
- Restart PowerShell after installation

**"node command not found"**  
- Install Node.js from nodejs.org
- Restart PowerShell after installation

**"Access denied" during service installation**
- Run PowerShell as Administrator
- Use: `.\setup.ps1 -InstallService`

**SQLite VSS build fails**
- Install Visual Studio Build Tools with C++ workload
- Or skip vector search features (app will still work)

**Port already in use**
- Use custom port: `.\setup.ps1 -Port 8080`
- Or stop other applications using port 5000

### Getting Help

1. **Check logs:** `.\scripts\view-logs.ps1`
2. **Validate setup:** `.\setup.ps1 -Mode Development` (runs validation)
3. **Clean reinstall:** `.\quick-setup.ps1 -Clean`
4. **Manual build:** Follow manual installation steps above

### Advanced Configurations

**Custom database location:**
```powershell
.\setup.ps1 -DatabasePath "D:\SwAIvyn\database.db"
```

**Development with auto-restart:**
```powershell
cd backend
dotnet watch run
```

**Production with HTTPS:**
```powershell
# Edit appsettings.json to configure HTTPS
.\setup.ps1 -Mode Production
```

## Files Created

After setup, you'll have:

```
SwAIvyn/
├── start.cmd              # Quick start script
├── launch.cmd             # Production launcher  
├── launch-dev.cmd         # Development launcher
├── dist/                  # Production build
│   ├── backend/           # Compiled backend
│   └── frontend/          # Built frontend
├── data/                  # Application data
│   ├── swai-vyn.db       # SQLite database
│   ├── avatars/          # Avatar storage
│   └── uploads/          # File uploads
└── logs/                  # Application logs
```

## Next Steps

After setup completes:

1. **Start SwAIvyn:** Run `start.cmd` or `launch.cmd`
2. **Open browser:** Go to `http://localhost:5000`
3. **First-time setup:** Follow the web UI setup wizard
4. **Import AI models:** Configure your preferred AI providers
5. **Customize:** Set up avatars, personalities, and preferences

Enjoy your privacy-focused AI assistant! 🤖✨
