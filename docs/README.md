# SwAIvyn

## Overview

SwAIvyn is a privacy-focused, self-contained AI assistant that runs entirely on your local network. It features both traditional text-based chat and an immersive voice-first interface where your AI lives in a customizable virtual space.

## Getting Started

### Prerequisites

- .NET 7 SDK or later
- Node.js (v16+) and npm
- Windows 10/11 or Linux with systemd support

### Installation

1. Clone the repository
2. Build the backend with `dotnet publish`
3. Run the backend server
4. Run the frontend React app with `npm run dev`
5. Start Ollama and LM Studio local LLM servers

## Project Structure

- Backend: ASP.NET Core with EF Core and SignalR
- Frontend: React 18 with Vite and TailwindCSS
- Local LLM connectors for Ollama and LM Studio

## Build Instructions

- Backend: `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true`
- Frontend: `npm install` then `npm run dev`

## Usage

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
