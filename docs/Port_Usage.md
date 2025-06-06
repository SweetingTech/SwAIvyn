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
