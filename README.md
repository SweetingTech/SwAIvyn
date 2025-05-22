# SwAIvyn

<div align="center">
  
![SwAIvyn Logo](https://via.placeholder.com/200x200.png?text=SwAIvyn)

**A federated AI assistant with dual interfaces and Tamagotchi-like features**

[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET 7](https://img.shields.io/badge/.NET-7.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/7.0)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://reactjs.org/)

</div>

## 📋 Overview

SwAIvyn is a privacy-focused, self-contained AI assistant that runs entirely on your local network. It features both traditional text-based chat and an immersive voice-first interface where your AI lives in a customizable virtual space.

### Key Features

- **Dual Interfaces**
  - 💬 **Text Chat**: Traditional keyboard-first interface
  - 🎤 **AI Room**: Voice-first Tamagotchi-style interface with virtual living space
  
- **Complete Privacy**
  - 🔒 Runs entirely on your closed network
  - 🔐 No data sent to external servers
  - 📂 Local storage of all conversations and memories
  
- **Federated Experience**
  - 🔄 AI-to-AI communication across instances
  - 💌 Message passing between users
  - 🧠 Selective memory sharing between AIs
  
- **Personalization**
  - 🎭 Import character cards or create custom personalities
  - 🖼️ Customizable avatars (2D now, 3D planned)
  - 🏠 Decorable virtual living space

- **Advanced Features**
  - 📧 Email and calendar integration
  - 🔍 Web browsing capabilities via Browsh
  - 💾 Automated backup to NAS or cloud
  - 🧩 Modular plugin architecture

## 🖥️ Screenshots

<div align="center">
  <img src="https://via.placeholder.com/400x250.png?text=Text+Chat+UI" alt="Text Chat UI" width="45%">
  <img src="https://via.placeholder.com/400x250.png?text=AI+Room+UI" alt="AI Room UI" width="45%">
</div>

## 🚀 Getting Started

### Prerequisites

- **.NET 7 SDK** or later
- **Node.js** (v16+) and npm
- **Windows 10/11** or **Linux** with systemd support

### Installation

#### Single-Executable Release (Recommended)

1. Download the latest release from the [Releases page](https://github.com/SweetingTech/SwAIvyn/releases)
2. Run the executable
3. Follow the on-screen setup instructions

#### Manual Build

```bash
# Clone the repository
git clone https://github.com/SweetingTech/SwAIvyn.git
cd SwAIvyn

# Build the backend
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true

# The executable will be in bin/Release/net7.0/win-x64/publish/
```

### First-Time Setup

1. When first launched, SwAIvyn will create a default admin account
2. You'll need to set a password and generate recovery phrases
3. Configure your AI's personality and avatar
4. Connect with other SwAIvyn instances on your network (optional)

## 🧩 Features in Detail

### Text Chat Interface

Traditional chat interface for keyboard-based interaction:
- Full conversation history
- File uploads and webcam integration
- Rich markdown and code support
- TTS playback of AI responses

### AI Room Interface

Immersive, voice-focused interface:
- Visual representation of your AI in its living space
- Voice as primary interaction method
- Minimized text input for when needed
- Future: 3D environment with customizable items

### Memory Management

- Browse, search, and edit your AI's memories
- Toggle sharing of specific memories with other instances
- Categorize and organize important information
- Review what your AI has learned about you

### Character System

- Import JSON character cards to set personality
- Upload custom avatars for visual representation
- Configure voice and speech patterns
- Future: 3D avatar support and animations

### Module System

- Add plugins to extend functionality
- Configure TTS/STT engines
- Select LLM backends
- Install custom agents and workflows

### Federation

- AI-to-AI communication across instances
- Coordinate calendars, share information
- Pass messages between users
- Maintain appropriate privacy boundaries

## 🔧 Configuration

All settings can be configured through the Settings UI, including:

- Account management
- Network settings and federation
- Character personality and appearance
- Voice and speech preferences
- Backup locations and schedule

## 🛣️ Roadmap

- [x] Core chat functionality
- [x] Basic AI personality system
- [ ] Voice-first room interface
- [ ] Federation between instances
- [ ] Memory management UI
- [ ] Plugin system
- [ ] 3D avatar support
- [ ] Mobile companion app

See the [project board](https://github.com/SweetingTech/SwAIvyn/projects) for detailed progress.

## 💻 Technical Architecture

SwAIvyn is built as a single-executable application that includes:

- ASP.NET Core backend with SignalR for real-time communication
- React frontend served via embedded static files
- SQLite for persistent storage with VSS (Vector Similarity Search) extension
- Embedded vector and graph databases for AI memory
- Background services for email, backup, and federation

### SQLite VSS Integration

The application uses SQLite VSS for efficient vector similarity search:

- Pre-built DLL file located in the `assets` directory
- Test project in `TestSqliteVssProject` for verifying VSS functionality
- PowerShell build scripts for customized builds
- Ensure `VectorServerAvailable` is set to `true` in `appsettings.json`

When deploying to a new system, copy the `sqlite-vss.dll` file to `assets` directory before building.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- All the open-source AI and NLP communities
- Character card creators and standards
- Tamagotchi for the inspiration

---

<div align="center">
  <i>SwAIvyn: Your AI companion that lives in your home network</i>
</div>
