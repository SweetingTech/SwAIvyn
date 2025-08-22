# Implementation Plan (Final)

[Overview]
This plan outlines the steps to create a unified, one-command startup process for the SwAIvyn application. A root-level Docker Compose file will be created to build and manage the backend, frontend, TTS, and Weaviate database services. This new application stack will be configured to connect to the pre-existing, external Whisper STT service, which is already running in Docker.

This approach will replace the current fragmented startup scripts with a single, reproducible command, simplifying the development environment.

[Types]
No changes to the application's data structures or types are required.

[Files]
This implementation will focus on creating Dockerfiles for the non-containerized services and a master Docker Compose file to orchestrate the application stack.

- **New Files to be Created:**
  - `d:/project/SwAIvyn/docker-compose.yml`: The primary Docker Compose file to define and orchestrate the SwAIvyn application services (Backend, Frontend, TTS, Weaviate).
  - `d:/project/SwAIvyn/backend/Dockerfile`: A multistage Dockerfile to build and run the .NET backend service.
  - `d:/project/SwAIvyn/frontend/Dockerfile`: A Dockerfile for the React frontend. For development, this will run the dev server; for production, it would build and serve static files.
  - `d:/project/SwAIvyn/run-docker-dev.ps1`: A new PowerShell script that simplifies launching the entire stack via `docker-compose up`.

- **Existing Files to be Integrated (Service definitions will be copied and adapted):**
  - `d:/project/SwAIvyn/speech/TTS/openaudio-s1-mini/docker-compose.yml`: The `swai-tts` service definition will be moved to the root `docker-compose.yml`.
  - `d:/project/SwAIvyn/vectorDB/docker-compose-djay.yml`: The Weaviate service definitions will be moved to the root `docker-compose.yml`.

- **Existing Files to be Modified:**
  - `d:/project/SwAIvyn/backend/appsettings.json`: Service URLs will be updated.
    - TTS URL will use Docker's internal DNS: `http://swai-tts:5002`.
    - STT URL will use Docker's special DNS name for the host to connect to the external Whisper container: `http://host.docker.internal:8002`.
  - `d:/project/SwAIvyn/.gitignore`: To be updated to ignore Docker-related artifacts.

- **Files to be Deprecated:**
  - `d:/project/SwAIvyn/run.cmd`
  - `d:/project/SwAIvyn/scripts/dev-run.ps1`
  - `d:/project/SwAIvyn/vectorDB/docker-compose-djay.yml`
  - `d:/project/SwAIvyn/speech/TTS/openaudio-s1-mini/docker-compose.yml`

[Functions & Classes]
No modifications to application logic are planned.

[Dependencies]
- **Docker Desktop:** A mandatory dependency for the development environment.

[Testing]
- **Service Health:** Use `docker-compose ps` to verify all managed containers (`backend`, `frontend`, `tts`, `weaviate`) are running.
- **External Connection:** From within the backend container, test the connection to `http://host.docker.internal:8002` to ensure it can reach the external Whisper STT service.
- **End-to-End Test:** Perform a full user flow to validate communication between all services.

[Implementation Order]
1.  **Create Backend Dockerfile:** Create the `backend/Dockerfile`.
2.  **Create Frontend Dockerfile:** Create the `frontend/Dockerfile`.
3.  **Create Root `docker-compose.yml`:** Create the main `docker-compose.yml` file.
4.  **Integrate Weaviate Service:** Copy and adapt the Weaviate service definitions from `vectorDB/docker-compose-djay.yml` into the root `docker-compose.yml`.
5.  **Integrate TTS Service:** Copy and adapt the `swai-tts` service definition from its local compose file into the root `docker-compose.yml`, adjusting the `build` context path.
6.  **Add Backend & Frontend Services:** Add the new service definitions for the backend and frontend to the root `docker-compose.yml`.
7.  **Configure Service Communication:** Update `backend/appsettings.json` with the new internal and external service URLs.
8.  **Create Startup Script:** Develop the `run-docker-dev.ps1` script.
9.  **Update Documentation:** Update `README.md` with the new setup instructions and remove deprecated scripts.
10. **Final System Test:** Run the full test plan.
