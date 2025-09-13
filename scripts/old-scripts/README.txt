This folder contains archived scripts that are no longer part of the primary
development flow. They are preserved for reference or fallback usage.

Active scripts to use instead:
- dev-run.ps1: Main entrypoint for dev (auto brings up Traefik + TTS via Swarm)
- docker-stack-up.ps1: Ensures the Swarm stack is up (builds images if missing)
- docker-build.ps1: Rebuild local images (tts-proxy, 11labs adapter, orchestrator)
- dev-bff.ps1 / dev-frontend.ps1 / dev-orchestrator.ps1: host-run helpers

Archived here:
- compose-build.ps1, start-stack.ps1, dev-start-infra.ps1, dev-up.ps1, dev-start.ps1
  (Compose-first orchestration replaced by Swarm + Traefik stack.)
- tts-docker-build.ps1 (superseded by docker-stack-up.ps1 + docker-build.ps1)
- dev-purge-conversations.ps1.bak (backup copy)

If you need to resurrect a script, move it back to the parent folder.
