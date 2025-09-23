This folder contains archived scripts that are no longer part of the primary
development flow. They are preserved for reference or fallback usage.

Active scripts to use instead:
- dev-run.ps1: Main entrypoint for hybrid dev (Docker infrastructure + host apps)
- setup-bare-metal.ps1: Complete bare metal Windows deployment without Docker
- docker-stack-up.ps1: Ensures the Swarm stack is up (builds images if missing)
- docker-build.ps1: Rebuild local images (tts-proxy, 11labs adapter, orchestrator)
- dev-bff.ps1 / dev-frontend.ps1 / dev-orchestrator.ps1: individual host service runners

Archived here:
- compose-build.ps1, start-stack.ps1, dev-start-infra.ps1, dev-up.ps1, dev-start.ps1
  (Compose-first orchestration replaced by Swarm + Traefik stack.)
- tts-docker-build.ps1 (superseded by docker-stack-up.ps1 + docker-build.ps1)
- dev-run-simple.ps1 (superseded by dev-run.ps1 -DisableTraefik)
- run_dev.ps1 (redundant alias removed)
- full-setup.ps1 (legacy .NET-based setup, replaced by setup-bare-metal.ps1 for Python/FastAPI)
- dev-start-apps.ps1, start-apps.ps1 (redundant with dev-run.ps1)
- launch-app.ps1 (legacy .NET launcher)
- quick-setup.ps1 (legacy .NET setup)
- dev-stop.ps1, stop-all.ps1 (redundant stop scripts)

If you need to resurrect a script, move it back to the parent folder.
