# Contributing to SwAIvyn

Thanks for helping improve SwAIvyn! This guide outlines the local setup, coding standards, and quality gates that keep the project healthy.

## Prerequisites

- Node.js 20+
- Python 3.11+
- Poetry (for managing Python packages)
- Docker Desktop (optional, required for the full stack)
- [pre-commit](https://pre-commit.com/) for git hook management

## Repository Setup

```bash
# Install frontend dependencies
npm install

# Install Python tooling for the BFF
cd Services/bff
poetry install
```

To work on Python services such as the orchestrator or TTS adapters, install their requirements as well:

```bash
cd Services/orchestrator
pip install -r requirements.txt
```

## Running the Stack

- **Frontend dev server**: `npm run dev --prefix frontend`
- **BFF**: `uvicorn Services.bff.app.main:app --reload`
- **Full docker environment**: `docker-compose up` (ensure `.env` contains required secrets)

## Code Quality

### Pre-commit Hooks

Install git hooks once after cloning:

```bash
pre-commit install
```

Hooks will run automatically on `git commit`. You can run the full suite manually with:

```bash
pre-commit run --all-files
```

The default configuration runs:
- YAML sanity checks
- End-of-file and whitespace cleanup
- `npm run lint --prefix frontend`

### TypeScript & React

- Prefer typed APIs—avoid `any` and favor shared interfaces in `frontend/src/types`.
- Use the shared logger (`frontend/src/utils/logger.ts`) instead of raw `console` calls.
- Loading and error states should be explicit for all async flows.

### Python Services

- Use the structured logger exposed from `Services/bff/app/main.py` (`logger = logging.getLogger("swai.bff")`).
- Raise HTTP errors via FastAPI (`HTTPException`) and rely on the global handlers for consistent responses.
- Validate required environment variables through `Services/bff/app/config.py` or service-specific config modules.

## Testing

- **Frontend**: `npm run lint --prefix frontend`
- **Backend (BFF)**: `poetry run pytest` (tests live under `Services/bff/tests`)
- **Orchestrator**: Unit tests can be added under `Services/orchestrator/tests`; run via `pytest` once created.

> ℹ️ The lint command currently surfaces legacy warnings. Please help reduce these over time; do not ignore new warnings.

## Submitting Changes

1. Create a descriptive branch name (e.g., `feature/add-memory-sync-health`).
2. Make your changes with clear, small commits.
3. Ensure all checks listed above pass locally.
4. Open a pull request that references the Honey-Do task or GitHub issue being addressed.
5. Include screenshots for UI-affecting changes.

Thanks for contributing! 🚀
