# SwAIvyn Data Flow Diagram

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                        SwAIvyn Application                          │
│                                                                     │
│  ┌───────────────┐          ┌───────────────┐          ┌─────────┐  │
│  │               │          │               │          │         │  │
│  │  React        │◄────────►│  .NET API     │◄────────►│ SQLite  │  │
│  │  Frontend     │  SignalR │  Backend      │          │ DB      │  │
│  │               │          │               │          │         │  │
│  └───────────────┘          └───────────────┘          └─────────┘  │
│                                     ▲                               │
│                                     │                               │
│                                     ▼                               │
│                             ┌───────────────┐                       │
│                             │               │                       │
│                             │  Log Files    │                       │
│                             │               │                       │
│                             └───────────────┘                       │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Detailed Data Flow

### 1. User Authentication Flow

```
┌──────────┐     ┌───────────────┐     ┌──────────────┐     ┌─────────┐
│          │     │               │     │              │     │         │
│  User    │────►│ AuthController│────►│ AuthService  │────►│ Database│
│          │     │               │     │              │     │         │
└──────────┘     └───────────────┘     └──────────────┘     └─────────┘
      ▲                                                          │
      │                                                          │
      │                                                          ▼
      │           ┌───────────────┐     ┌──────────────┐     ┌─────────┐
      │           │               │     │              │     │         │
      └───────────│ React Frontend│◄────│ JWT Token    │◄────│ Response│
                  │               │     │              │     │         │
                  └───────────────┘     └──────────────┘     └─────────┘
```

### 2. Chat Interaction Flow

```
┌──────────┐     ┌───────────────┐     ┌──────────────┐     ┌─────────────────┐
│          │     │               │     │              │     │                 │
│  User    │────►│ ChatController│────►│ ChatService  │────►│LlmConnectorSvc  │
│          │     │               │     │              │     │                 │
└──────────┘     └───────────────┘     └──────────────┘     └─────────────────┘
      ▲                                       │                      │
      │                                       │                      │
      │                                       ▼                      │
      │           ┌───────────────┐     ┌──────────────┐             │
      │           │               │     │              │             │
      └───────────│ React Frontend│◄────│ ChatHub      │◄────────────┘
                  │               │     │              │
                  └───────────────┘     └──────────────┘
                         │
                         │
                         ▼
                  ┌───────────────┐
                  │               │
                  │ Database      │
                  │ (Chat History)│
                  │               │
                  └───────────────┘
```

### 3. Memory Operations Flow

```
┌──────────┐     ┌───────────────┐     ┌──────────────┐     ┌─────────┐
│          │     │               │     │              │     │         │
│  User    │────►│MemoryController│───►│MemoryService │────►│ Database│
│          │     │               │     │              │     │         │
└──────────┘     └───────────────┘     └──────────────┘     └─────────┘
      ▲                  ▲                     │                 │
      │                  │                     │                 │
      │                  │                     ▼                 │
      │           ┌───────────────┐     ┌──────────────┐        │
      │           │               │     │              │        │
      └───────────│ React Frontend│◄────│ Response     │◄───────┘
                  │               │     │              │
                  └───────────────┘     └──────────────┘
```

### 4. Voice Processing Flow

```
┌──────────┐     ┌───────────────┐     ┌──────────────┐
│          │     │               │     │              │
│  User    │────►│VoiceController│────►│ VoiceService │
│          │     │               │     │              │
└──────────┘     └───────────────┘     └──────────────┘
      ▲                                       │
      │                                       │
      │                                       ▼
      │           ┌───────────────┐     ┌──────────────┐
      │           │               │     │              │
      └───────────│ React Frontend│◄────│ VoiceHub     │
                  │               │     │              │
                  └───────────────┘     └──────────────┘
```

### 5. Logging Flow

```
┌────────────────┐     ┌───────────────────┐     ┌───────────────┐
│                │     │                   │     │               │
│ Application    │────►│ SimpleLoggerSvc   │────►│ Log Files     │
│ Components     │     │                   │     │               │
└────────────────┘     └───────────────────┘     └───────────────┘
        │                        ▲
        │                        │
        ▼                        │
┌────────────────┐     ┌───────────────────┐     ┌───────────────┐
│                │     │                   │     │               │
│ Exception      │────►│ Global Exception  │────►│ Crash Logs    │
│ Occurs         │     │ Handler           │     │               │
└────────────────┘     └───────────────────┘     └───────────────┘
```

### 6. Application Monitoring Flow

```
┌────────────────┐     ┌───────────────────┐     ┌───────────────┐
│                │     │                   │     │               │
│ Application    │────►│ ApplicationMonitor│────►│ Log Files     │
│ Runtime        │     │ Service           │     │               │
└────────────────┘     └───────────────────┘     └───────────────┘
        │                        │
        │                        │
        ▼                        ▼
┌────────────────┐     ┌───────────────────┐
│                │     │                   │
│ System         │     │ Performance       │
│ Resources      │     │ Metrics           │
└────────────────┘     └───────────────────┘
```

## Error Handling Flow

```
┌────────────────┐
│                │
│ Error Occurs   │
│                │
└────────────────┘
        │
        ▼
┌────────────────────────────────────────────────────────────┐
│                                                            │
│                   Error Handling Layer                     │
│                                                            │
│  ┌───────────────┐     ┌───────────────┐     ┌─────────┐  │
│  │               │     │               │     │         │  │
│  │  Try-Catch    │     │  Middleware   │     │ Global  │  │
│  │  Blocks       │     │  Handlers     │     │ Handler │  │
│  │               │     │               │     │         │  │
│  └───────────────┘     └───────────────┘     └─────────┘  │
│           │                    │                  │       │
│           └────────────────────┼──────────────────┘       │
│                                │                          │
└────────────────────────────────┼──────────────────────────┘
                                 │
                                 ▼
                        ┌───────────────────┐
                        │                   │
                        │ SimpleLoggerSvc   │
                        │                   │
                        └───────────────────┘
                                 │
                                 │
          ┌────────────────────┬─┴─────────────────────┐
          │                    │                       │
          ▼                    ▼                       ▼
┌───────────────────┐ ┌───────────────────┐  ┌───────────────────┐
│                   │ │                   │  │                   │
│ Application Logs  │ │ Crash Logs        │  │ User Feedback     │
│                   │ │                   │  │                   │
└───────────────────┘ └───────────────────┘  └───────────────────┘
```

## Component Dependency Diagram

```
┌───────────────────────────────────────────────────────────────────────┐
│                                                                       │
│                         Application Components                        │
│                                                                       │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐       │
│  │               │     │               │     │               │       │
│  │ Controllers   │────►│ Services      │────►│ Data Access   │       │
│  │               │     │               │     │               │       │
│  └───────────────┘     └───────────────┘     └───────────────┘       │
│         ▲                     ▲                     ▲                │
│         │                     │                     │                │
│         │                     │                     │                │
│  ┌──────┴──────────┐  ┌──────┴──────────┐  ┌───────┴─────────┐      │
│  │                 │  │                 │  │                 │      │
│  │ Middleware      │  │ Background      │  │ Database        │      │
│  │                 │  │ Services        │  │                 │      │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘      │
│         │                     │                     │                │
│         │                     │                     │                │
│         ▼                     ▼                     ▼                │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                                                             │    │
│  │                     Logging System                          │    │
│  │                                                             │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

This document provides a visual representation of the data flow within the SwAIvyn application. Use it to understand how different components interact and to trace the path of data through the system when troubleshooting issues.

# Port Usage

This document outlines the network port usage for various components and services within the SwAIvyn application.

| Port  | Purpose / Service                     | Notes                                     |
| :---- | :------------------------------------ | :---------------------------------------- |
| 5000  | SwAIvyn backend API and SignalR hubs  | Default base URL (BaseUrl)                |
| 5001  | Optional HTTPS during development     | Allowed in CORS policy                    |
| 11434 | Ollama API                            | Configurable                              |
| 1234  | LM Studio API (default)               | Formerly used port 5000; customizable     |
| 8080  | Weaviate vector database              | Also shown as example LM Studio port; configurable |
| 7474  | Neo4j HTTP API                        | Configurable                              |
| 7687  | Neo4j Bolt protocol                   | Configurable                              |
| 3000, 5173, 5174 | Frontend dev servers (React/Vite) | Used only during development              |
