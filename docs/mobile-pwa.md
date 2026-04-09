# Mobile Companion App – Technology Decision

## Status

**Decided** – Progressive Web App (PWA) using the existing React + Vite frontend.

---

## Options Evaluated

| Option | Pros | Cons |
|---|---|---|
| **React Native** | True native UI, full device API access | Separate codebase, requires Xcode/Android Studio, App Store review cycle |
| **Flutter** | High-performance native rendering, single codebase | Different language (Dart), separate codebase from existing React frontend |
| **Expo (React Native)** | Easier React Native setup, OTA updates | Still separate codebase, Expo limitations for native modules |
| **Progressive Web App (PWA)** ✅ | Reuses 100% of existing React codebase, no app store needed, instant deployment, offline support via Service Worker, Web Push API | No direct access to some native APIs (camera limitations on iOS), requires HTTPS in production |

---

## Decision

**Progressive Web App (PWA)** built on the existing React + Vite frontend.

### Rationale

1. **Zero duplication** – SwAIvyn already has a full-featured React frontend with text chat, voice interaction (VoiceRoomPage), conversation management, and settings sync. A PWA simply adds installability and offline support to that existing codebase.

2. **Instant distribution** – No App Store / Play Store submissions. The hosted PWA URL is available the moment the frontend build is deployed. CI uploads the built `dist/` folder as a release artifact.

3. **Offline graceful degradation** – Workbox (via `vite-plugin-pwa`) caches conversation lists, messages, settings, and characters using a `NetworkFirst` strategy so the last-known data is available when the SwAIvyn instance is unreachable.

4. **Push notifications** – The Web Push API + VAPID provides cross-platform push notifications on Android (Chrome/Firefox) and desktop without a proprietary push service. iOS 16.4+ supports Web Push for installed PWAs.

5. **All acceptance criteria met** within the existing stack:
   - Authentication: JWT stored in `localStorage` (existing, unchanged).
   - Text chat: Full conversation history, markdown, send/receive (existing).
   - Voice interaction: Microphone → Whisper STT → LLM → TTS (existing `VoiceRoomPage`).
   - Push notifications: New `pywebpush`-backed BFF endpoints + service worker.
   - Settings sync: Existing `/api/chat/settings/{user_id}` API (unchanged).
   - Offline: Workbox `NetworkFirst` + pre-cached static assets.
   - App distribution: CI releases the `frontend/dist/` folder as a zip artifact.

---

## Implementation Summary

### Frontend (`frontend/`)

- **`vite.config.ts`** – Added `vite-plugin-pwa` with:
  - Web App Manifest (name, icons, `display: standalone`, `theme_color`)
  - Workbox `runtimeCaching` for `/api/conversation*`, `/api/chat/settings*`, `/api/characters*`
- **`public/pwa-192.png`, `public/pwa-512.png`** – PWA icons
- **`src/services/pushService.ts`** – `subscribeToPush()`, `unsubscribeFromPush()`, `isPushEnabled()`
- **`src/hooks/usePushNotifications.ts`** – React hook that wraps push service state
- **`src/pages/SettingsPage.tsx`** – Added "Mobile & Notifications" tab with install instructions and push toggle

### Backend (`Services/bff/`)

- **`app/models.py`** – Added `push_subscriptions` table (id, user_id, endpoint, p256dh, auth, created_at)
- **`alembic/versions/006_push_subscriptions.py`** – Migration to create the table
- **`requirements.txt`** – Added `pywebpush==2.0.0`
- **`app/main.py`** – Added:
  - VAPID key management (`_get_vapid()`, `VAPID_PRIVATE_KEY` / `VAPID_PUBLIC_KEY` env vars)
  - `_send_push_notification()` async helper
  - `GET /api/push/vapid-public-key` – returns server's VAPID app server key
  - `POST /api/push/subscribe` – stores a subscription for the authenticated user
  - `POST /api/push/unsubscribe` – removes a subscription
  - Push trigger in `PATCH /api/agents/tasks/{task_id}` on status → completed/failed
  - Push trigger in `POST /api/agents/tasks/{task_id}/results`

### CI (`/.github/workflows/`)

- **`release.yml`** – Added `pwa-build` job that builds the frontend and uploads `frontend/dist/` as a release artifact (hosted PWA URL)

---

## Environment Variables (new)

| Variable | Required | Description |
|---|---|---|
| `VAPID_PRIVATE_KEY` | No (auto-generated) | PEM-encoded VAPID private key. Must be set in production to survive restarts. Generate with `python -c "from py_vapid import Vapid; v=Vapid(); v.generate_keys(); print(v.private_pem().decode())"` |
| `VAPID_PUBLIC_KEY` | No (auto-generated) | URL-safe base64 VAPID public key to share with the frontend |
| `VAPID_CLAIMS_EMAIL` | No | `mailto:` URI for VAPID claims (default: `mailto:admin@swai.local`) |

---

## iOS Notes

Push notifications for PWAs on iOS require:
- iOS 16.4 or later
- The PWA must be **installed** (added to Home Screen)
- The site must be served over **HTTPS**

When running in development over HTTP, push subscriptions will silently be unavailable – this is expected browser security behaviour.

---

## References

- [`vite-plugin-pwa`](https://vite-pwa-org.netlify.app/)
- [Web Push Protocol (RFC 8030)](https://datatracker.ietf.org/doc/html/rfc8030)
- [pywebpush](https://github.com/web-push-libs/pywebpush)
- `Services/bff/app/main.py` – BFF API consumed by the mobile app
- `docs/architecture-and-dataflow.md` – Runtime topology
