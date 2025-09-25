# SwAIvyn Documentation

This directory contains comprehensive technical documentation for the SwAIvyn project. All documentation follows consistent kebab-case naming conventions.

##  Available Documentation

###  Agent Integration
- **[agent-stack-integration.md](agent-stack-integration.md)** - Comprehensive technical guide for building external agent systems with complete API specifications, data formats, and implementation examples
- **[external-agent-guide.md](external-agent-guide.md)** - Basic external agent connection guide with authentication and task management
- **[agents-and-workflows.md](agents-and-workflows.md)** - Internal workflow management and agent catalog integration

###  Architecture & Development
- **[architecture-and-dataflow.md](architecture-and-dataflow.md)** - System architecture diagrams and data flow patterns using Mermaid charts
- **[hybrid-development.md](hybrid-development.md)** - Development environment setup with Docker containers and host services
- **[bare-metal-deployment.md](bare-metal-deployment.md)** - Container-free deployment guide for production environments
- **[project-structure.md](project-structure.md)** - Detailed project organization and file structure guide

###  Data & Storage
- **[database-implementation.md](database-implementation.md)** - Database schema, tables, and implementation patterns
- **[neo4j-configuration.md](neo4j-configuration.md)** - Graph database setup and configuration for relationship storage
- **[logging-guide.md](logging-guide.md)** - Comprehensive logging implementation and monitoring setup

###  Audio & Voice Features
- **[fish-speech-integration.md](fish-speech-integration.md)** - Text-to-speech integration with Fish Speech service
- **[voice-management.md](voice-management.md)** - Voice feature implementation and management systems

###  Project Management
- **[roadmap.md](roadmap.md)** - Project development roadmap and future planning

##  Documentation Standards

All documentation in this directory follows these standards:
- **Naming Convention**: kebab-case (lowercase with hyphens)
- **Content Structure**: Clear headings, code examples, and practical implementation guidance
- **Technical Depth**: Comprehensive technical specifications suitable for developers and LLMs
- **Currency**: Regularly updated to reflect the current state of the SwAIvyn architecture

##  Quick Reference

For quick setup, start with:
1. **[hybrid-development.md](hybrid-development.md)** - Development environment setup
2. **[architecture-and-dataflow.md](architecture-and-dataflow.md)** - Understand system architecture
3. **[agent-stack-integration.md](agent-stack-integration.md)** - External agent development (if building agents)

For deployment guidance:
1. **[bare-metal-deployment.md](bare-metal-deployment.md)** - Production deployment without containers
2. **[database-implementation.md](database-implementation.md)** - Database setup and configuration
3. **[logging-guide.md](logging-guide.md)** - Monitoring and logging setup

---

*This documentation is maintained as part of the SwAIvyn project and reflects the current FastAPI + React + PostgreSQL architecture.*