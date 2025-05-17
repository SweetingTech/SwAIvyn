# SwAIvyn

## Overview

SwAIvyn is a privacy-focused, self-contained AI assistant that runs entirely on your local network. It features both traditional text-based chat and an immersive voice-first interface where your AI lives in a customizable virtual space.

## Getting Started

### Quick Start

1. Double-click `SwAIvyn.cmd` in the project root
2. The application will build (first time only) and launch automatically
3. Interact with the assistant through your web browser

### System Requirements

- Windows 10/11
- .NET 7.0 Runtime (installed automatically if needed)
- 4GB RAM minimum (8GB recommended)
- Local LLM server (optional): Ollama or LM Studio

### Manual Installation

For developers who want to work with the source code:

1. Clone the repository
2. Run `.\scripts\build-app.ps1` to build the complete application
3. The executable will be created in the `dist` folder

## Project Structure

- **Backend**: ASP.NET Core with EF Core and SignalR for real-time communication
- **Frontend**: React 18 with TypeScript, Vite, and TailwindCSS
- **Single Executable**: Self-contained application with embedded frontend
- **Configuration Service**: Dynamic service discovery without hardcoded ports

## Architecture

SwAIvyn uses a service-based architecture with:

- **Configuration Service**: Provides DNS-like naming for services
- **Chat Service**: Real-time messaging with SignalR
- **LLM Connector**: Integration with local language models
- **Voice Processing**: Speech recognition and synthesis

## Build Instructions

- **Complete Application**: `.\scripts\build-app.ps1`
- **Backend Only**: `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true`
- **Frontend Only**: `npm install` then `npm run build`

## Usage

- Launch the application using `SwAIvyn.cmd` or the executable in the `dist` folder
- Use the Login page to authenticate
- Interact with AI via text chat or voice room interface
- Configure settings and AI personality via UI

## Dependencies

- .NET 7
- React 18
- TailwindCSS
- Ollama LLM server
- LM Studio LLM server

## License

MIT License

---

This README provides a brief overview and instructions for the SwAIvyn project.
