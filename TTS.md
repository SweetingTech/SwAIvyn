# SwAIvyn TTS (FishSpeech) – Status & Plan

This document summarizes what has been done for TTS and what remains to fully enable GPU-accelerated TTS within the SwAIvyn dev workflow.

## Summary
- Current end-to-end TTS path works through Traefik on CPU fallback (audio/wav 200 OK via `tts.localhost`).
- ~~GPU path not yet active/stable in the dev workflow; a helper script exists but needs finalization.~~ GPU compose service added and dev-run.ps1 now auto-ensures a FishSpeech GPU runtime container; activation depends on WSL/Docker GPU availability on the host.
- Weaviate stability has been improved by disabling heavy modules for now; service comes up and is reachable.
- Traefik is bound on port 8088 to avoid conflicts; routing for TTS works.
- Neo4j failed during the latest full start due to missing password injection.

---

## What’s Done

### TTS Proxy and Backend
- Removed external “dial-home” calls; proxy now operates offline.
- Added multi-upstream fallback for TTS:
  - Prefers GPU upstream first, falls back to CPU.
  - Controlled via `UPSTREAM_TTS_LIST` env var (e.g., `http://fishspeech-runtime-gpu:8000,http://fishspeech-runtime:8000`).
- Added `/health` in the TTS proxy to report upstream statuses (GPU/CPU).
- Backend BFF: added `/api/tts/health` passthrough for UI.

### Frontend
- VoiceSelector now fetches voices dynamically from backend (removed hard-coded lists).
- Dashboard defaults to FishSpeech provider.
- ChatPage: clarified TTS toggle with “Auto voice” label.
- SettingsPage: added TTS health panel showing GPU/CPU upstream status.

### Docker/Infra
- Traefik moved to port 8088 to avoid port 80 conflicts; routing to `tts.localhost` works.
- Weaviate:
  - Rebuilt with fresh volume and stabilized by disabling heavy AI modules for now.
  - Health endpoint is reachable on host port 8086 (`/v1/.well-known/ready`).
  - Telemetry disabled.
- TTS stack wiring:
  - TTS proxy exposed internally on 8081; Traefik routes `tts.localhost` -> TTS proxy.
  - Upstreams point to FishSpeech runtime(s).


#### GPU Compose wiring (new)
- Added `fishspeech-runtime-gpu` service to `docker-compose.yml` under the `tts` profile.
  - Runs `swai/fish-speech:cuda` with `--device cuda --half` and mounts the model from `./speech/TTS/openaudio-s1-mini/fish_speech_model`.
  - Declares `gpus: all` and NVIDIA envs for CUDA.
- Updated TTS proxy service env in compose to prefer GPU upstream by default:
  - `UPSTREAM_TTS` -> `http://fishspeech-runtime-gpu:8000`
  - `UPSTREAM_TTS_LIST` -> `http://fishspeech-runtime-gpu:8000`
- Result: `docker compose --profile tts up -d tts fishspeech-runtime-gpu` brings up the proxy and GPU runtime together for local testing.
- In Swarm (docker-stack.yml), TTS already prefers GPU via `UPSTREAM_TTS_LIST=http://fishspeech-runtime-gpu:8000,http://fishspeech-runtime:8000`.

### Scripts & Dev Flow
- dev-shutdown.ps1 updated to stop the standalone GPU TTS container if present.
- setup-gpu-tts.ps1 script added to bring up a standalone CUDA FishSpeech container on the Swarm overlay network (`swaivyn_default`) and point the TTS proxy to it.
- dev-run.ps1 improvements:
  - Longer converge timeout; Swarm overlay network recovery logic.
  - Neo4j password presence guard (intended; see “Open Issues”).

- setup.ps1 is the single source of truth for database and base infra setup; run it once initially (and when config changes).

### Verification Performed
- Weaviate: confirmed startup logs healthy; readiness returns 200.
- Traefik: `/ping` returns 200 on `http://127.0.0.1:8088/ping` once stable.
- TTS via Traefik: `curl -H "Host: tts.localhost" http://127.0.0.1:8088/tts -d 'text=Hello'` returns `audio/wav` 200 (CPU upstream).
- TTS proxy `/health` shows GPU upstream down and CPU upstream 200 OK (so fallback working as designed).

---

## What Still Needs To Be Done

### 1) Make GPU TTS path reliable
- Ensure the GPU FishSpeech container actually starts and is reachable on the overlay network `swaivyn_default` with model volume mounted.
- Validate CUDA availability inside the container; otherwise log a clear warning and continue with CPU.
- Confirm TTS proxy health shows GPU upstream UP and that a `/tts` request returns audio from the GPU upstream.
- Decide final orchestration:
  - Option A: Keep the GPU runtime as a standalone container (recommended for Docker Desktop) and ensure dev-run.ps1 manages it.
  - Option B: Move the GPU runtime into Swarm (less ideal on Docker Desktop due to GPU runtime limitations), only if necessary.

### 2) Fix Neo4j password injection in dev-run
- Latest `dev-run.ps1` failed with: `Invalid value for NEO4J_AUTH: 'neo4j/'` and port-in-use flaps.
- Ensure `NEO4J_PASSWORD` from `.env` is exported into the environment used by `docker stack deploy` (Swarm reads `${NEO4J_PASSWORD}` at deploy time).
- Options:
  - Export env in-session before `docker stack deploy`.
  - Or reference an `env_file`/secret in the stack to carry the password.

### 3) Traefik entrypoint/ports clean-up
- We forced Traefik to target/publish 8088 to bypass conflicts. It works, but standard is target 80 inside, publish 8088 outside.
- Consider reverting to target 80/publish 8088 and keep `--entrypoints.web.address=:8088` (current behavior OK). Document the decision and keep it consistent.

### 4) Weaviate modules (re-enable later)
- Currently running with `DEFAULT_VECTORIZER_MODULE=none` and `ENABLE_MODULES=backup-filesystem` only.
- After the rest is stable, consider re-enabling needed modules (e.g., multi2vec-clip, qna, sum, spellcheck, reranker) with adequate startup tolerances.

### 5) Temporal admin-tools image pre-pull
- Health check error previously reported: missing `temporalio/admin-tools:1.23` image.
- Add a pre-pull step in dev-run.ps1 (or setup script) to avoid transient failures.


## WSL GPU readiness checklist (host setup)
- Install the latest NVIDIA Windows driver with WSL support (RTX 3090).
- Docker Desktop: enable WSL 2 based engine, enable your WSL distro under Resources → WSL Integration, and enable GPU support.
- Verify GPU in WSL:
  - `wsl -e bash -lc "nvidia-smi"` should list the GPU
  - `wsl -e bash -lc "docker run --rm --gpus all nvidia/cuda:12.2.0-base-ubuntu22.04 nvidia-smi"` should also list the GPU
- If you see: `WSL environment detected but no adapters were found` from `nvidia-container-cli`, GPU is not exposed to containers yet; fix Docker Desktop GPU settings and WSL driver support, then retry.

### 6) Consolidate GPU setup into dev-run and setup
- Integrate the working steps from `scripts/setup-gpu-tts.ps1` into `dev-run.ps1` and `scripts/setup.ps1` so the standard dev workflow manages GPU TTS automatically at startup (and `.\dev-shutdown.ps1`), per project preference.

### 7) Tests and telemetry
- Add a small automated smoke test step in dev-run to hit:
  - Traefik `/ping` (200)
  - TTS proxy `/health` (expects at least CPU upstream 200; GPU preferred 200)
  - Optional: `/tts` with a short text and verify non-empty WAV
- Ensure no external API calls are made anywhere in the TTS codepaths.

---

## Quick-Start (current state)
0) First-time setup (one-time or when configs change)
```
./scripts/setup.ps1
```


1) Stop everything cleanly
```
./dev-shutdown.ps1
```

2) Start the stack (Traefik, DBs, services)
```
./dev-run.ps1
```

3) If GPU is desired and Docker Desktop supports GPU, in Windows PowerShell:
```
./scripts/setup-gpu-tts.ps1 -SkipGPUTest
```
- This will attempt to start `fishspeech-runtime-gpu` on the overlay network and point the TTS proxy to it.
- Temporary: this GPU setup step will be automated by dev-run once GPU consolidation is complete.


4) Verify
- Traefik ping:
```
curl -s http://127.0.0.1:8088/ping
```
- TTS proxy health (via Traefik):
```
curl -s -H "Host: tts.localhost" http://127.0.0.1:8088/health
```
- TTS synth (via Traefik):
```
curl -s -H "Host: tts.localhost" -X POST -d "text=Hello from SwAIvyn" --output out.wav http://127.0.0.1:8088/tts
```

---

## Known Issues / Notes
- On Docker Desktop, running GPU inside Swarm is unreliable; prefer a standalone GPU container joined to the Swarm overlay network.
- If using WSL or a different Docker context, volume paths and GPU runtime availability may differ (`D:/...` vs `/mnt/d/...`). Adjust the `ModelPath` and `-v` mount accordingly.
- The TTS proxy currently falls back to CPU automatically if GPU upstream is down, which is why `/tts` works even before GPU is active.
- Ensure `.env` is the single source for sensitive settings. `NEO4J_PASSWORD` must be injected properly at deploy time.

---

## Next Concrete Steps (proposed)
1) Fix Neo4j password injection (export env or use env_file/secrets) so full stack comes up reliably.
2) Run GPU container with the correct model mount and network; confirm `/health` shows GPU=UP.
3) Add a minimal, scripted smoke test at the end of dev-run (Traefik ping, TTS health, 1 synth) to catch regressions fast.
4) Decide and document final Traefik port/entrypoint convention (keep 8088 outside; 80 inside recommended).
5) Optionally re-enable Weaviate modules after stability and add startup tolerances.

