# SwAIvyn Honey-Do Remediation Plan

This plan enumerates the twenty-five remediation items captured in the Honey-Do List and tracks their status, owners, and acceptance criteria. Tasks are grouped by priority to align engineering effort with the requested cadence.

## Legend
- **Status**: ⏳ Not started · 🚧 In progress · ✅ Complete · 🔁 Ongoing/Recurring
- **Owner**: Primary role responsible for execution (can be reassigned as needed)
- **Acceptance Criteria**: Verifiable outcome expected for closure

## Week 1 – Critical Issues
| # | Task | Status | Owner | Acceptance Criteria |
|---|------|--------|-------|----------------------|
| 1 | Remove SignalR dead code | ✅ | Frontend | `useChatHub.ts` removed, no SignalR references remain, frontend builds without unused dependency warnings. |
| 2 | Silence React Router v7 warnings | ✅ | Frontend | Browser console free of `v7_*` migration warnings when navigating the app. |
| 3 | Verify authentication hook usage | ✅ | Frontend | Audit log showing all routed pages rely on `useEffectiveUser()` pattern; no components bypass centralized auth state. |
| 4 | Add environment variable validation | ✅ | Platform | BFF and frontend block startup with actionable error when required env vars missing or malformed. |
| 5 | Align documentation with PostgreSQL schema | ✅ | Platform | Docs updated to reflect PostgreSQL usage; schema snapshot checked into repo matches migrator output. |
| 6 | Harden Temporal configuration | ✅ | Platform | Temporal services bind correctly on overlay networks; swarm/compose launches without ringpop bootstrap errors. |
| 7 | Introduce React error boundaries | ✅ | Frontend | ErrorBoundary component wraps routed content with tested fallback UI. |

## Week 2 – High Priority
| # | Task | Status | Owner | Acceptance Criteria |
|---|------|--------|-------|----------------------|
| 8 | Standardize API error responses | ✅ | Platform | All FastAPI endpoints return `{ "error": { code, message, details? } }`; shared helpers added with unit coverage. |
| 9 | Enforce TypeScript API contracts | ✅ | Frontend | HTTP services use typed request/response interfaces; no `any` in API surface; CI lint passes. |
| 10 | Externalize configuration values | ✅ | Platform | Replace hard-coded URLs/timeouts with config module keyed off env vars; documented defaults. |
| 11 | Health/readiness endpoints for all services | ✅ | Platform | `/healthz` and `/readyz` (or `/health`/`/ready`) implemented for orchestrator and TTS services with automated checks. |
| 12 | Unify logging strategy | ✅ | Platform | Frontend uses typed logger utility; backend adopts structured logging w/ consistent levels; docs updated. |

## Week 3 – Medium Priority
| # | Task | Status | Owner | Acceptance Criteria |
|---|------|--------|-------|----------------------|
| 13 | Prune unused dependencies | ⏳ | Platform | `depcheck`/`pip-audit` reports empty; package manifests updated; lockfiles regenerated. |
| 14 | Establish unit testing baseline | ⏳ | Platform | Frontend adopts Vitest; backend adds pytest suite; CI executes `npm test` and `pytest`. |
| 15 | Break up oversized React components | ⏳ | Frontend | Chat and Settings pages decomposed into focused subcomponents with clear boundaries. |
| 16 | Add documentation / JSDoc | ⏳ | Platform | Public APIs and complex flows carry inline documentation reviewed by peers. |
| 17 | Optimize React performance hot spots | ⏳ | Frontend | Identified components memoized; React Profiler shows reduced renders; metrics captured. |
| 18 | Provide loading states for async flows | ⏳ | Frontend | Chat send, settings save, and agent operations expose consistent skeleton/loading UI. |

## Week 4 – Security, Observability, and DX
| # | Task | Status | Owner | Acceptance Criteria |
|---|------|--------|-------|----------------------|
| 19 | Reinforce JWT handling | ⏳ | Platform | Tokens expire gracefully with refresh flow; no secrets logged; secure storage guidance documented. |
| 20 | Tighten CORS policies | ✅ | Platform | Allowed origins sourced from env/allowlist; security review sign-off. |
| 21 | Harden input validation | ✅ | Platform | FastAPI endpoints validate payloads via Pydantic models; rejects malformed data with standardized errors. |
| 22 | Add metrics collection | ⏳ | Platform | Prometheus-compatible `/metrics` exposed; dashboards documented. |
| 23 | Enable log aggregation | ⏳ | Platform | Central logging option (e.g., Loki/Fluent Bit) documented and wired into docker compose profile. |
| 24 | Configure pre-commit automation | ✅ | Platform | `.pre-commit-config.yaml` added with lint/format hooks; CONTRIBUTING explains setup. |
| 25 | Publish contributor onboarding docs | ✅ | Platform | `CONTRIBUTING.md` created with setup, workflow, and testing guidance. |

## Tracking & Reporting
- Weekly status review occurs each Friday with updates posted to the engineering Slack channel `#swai-maintenance`.
- Each task links to GitHub issues for traceability; closure requires code review + validation evidence.
- Remaining backlog after Week 4 will be reassessed and rolled into the quarterly roadmap.

