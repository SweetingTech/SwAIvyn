# Project Documentation

This directory contains curated reference material for SwAIvyn. The documents are grouped by focus area so you can quickly find the guidance you need without wading through redundant or outdated files.

## Architecture & Data Flows
- **[Dataflow_and_Architecture.md](Dataflow_and_Architecture.md)** – Consolidated system diagrams, core data entities, and persistence strategy.
- **[Agent_Integration_Guide.md](Agent_Integration_Guide.md)** – End-to-end specifications for building and operating external agents.
- **[Agents_and_Workflows.md](Agents_and_Workflows.md)** – UI and backend integration overview for workflow management.

## Deployment & Operations
- **[Build_and_Deployment_Guide.md](Build_and_Deployment_Guide.md)** – Self-contained build and deployment process.
- **[Hybrid_Development_Guide.md](Hybrid_Development_Guide.md)** – Host/Docker development workflow with hot reload guidance.
- **[Bare_Metal_Deployment_Guide.md](Bare_Metal_Deployment_Guide.md)** – Running the full stack without Docker.
- **[Migration_Option_A.md](Migration_Option_A.md)** – Notes on the Python + Temporal backend migration.
- **[Project_Roadmap.md](Project_Roadmap.md)** – High-level delivery roadmap.

## Platform Services & Infrastructure
- **[Database_Implementation_Plan.md](Database_Implementation_Plan.md)** – SQLite/SQLite-VSS implementation plan and design goals.
- **[Neo4j_Configuration_Guide.md](Neo4j_Configuration_Guide.md)** – Graph database configuration details.
- **[Logging_System_Guide.md](Logging_System_Guide.md)** – Logging coverage, file formats, and troubleshooting tips.
- **[Dns_Like_Naming_System.md](Dns_Like_Naming_System.md)** – Service discovery approach and configuration.

## Feature-Specific Guides
- **[Fish_Speech_Integration_Guide.md](Fish_Speech_Integration_Guide.md)** – Steps to run and validate the Fish Speech TTS integration.
- **[Voice_Management_Implementation.md](Voice_Management_Implementation.md)** – Current status of custom voice features.
- **[Project_Structure.md](Project_Structure.md)** – Repository layout and key component locations.

The goal is to keep these documents accurate and actionable. When major architectural decisions change, update the relevant guide here so this directory remains trustworthy.
