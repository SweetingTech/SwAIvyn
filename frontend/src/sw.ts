/// <reference lib="webworker" />
import { cleanupOutdatedCaches, precacheAndRoute } from 'workbox-precaching';
import { registerRoute } from 'workbox-routing';
import { NetworkFirst } from 'workbox-strategies';
import { ExpirationPlugin } from 'workbox-expiration';

declare const self: ServiceWorkerGlobalScope;

// Clean up caches from previous versions
cleanupOutdatedCaches();

// Inject the precache manifest (replaced by vite-plugin-pwa at build time)
precacheAndRoute(self.__WB_MANIFEST);

// ---------------------------------------------------------------------------
// Runtime caching – API routes (mirrors the previous workbox config)
// ---------------------------------------------------------------------------

registerRoute(
  ({ url }: { url: URL }) => url.pathname.startsWith('/api/conversation'),
  new NetworkFirst({
    cacheName: 'api-conversations',
    networkTimeoutSeconds: 5,
    plugins: [new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 86400 })],
  })
);

registerRoute(
  ({ url }: { url: URL }) => /\/api\/chat\/settings/.test(url.pathname),
  new NetworkFirst({
    cacheName: 'api-settings',
    networkTimeoutSeconds: 5,
    plugins: [new ExpirationPlugin({ maxEntries: 20, maxAgeSeconds: 86400 })],
  })
);

registerRoute(
  ({ url }: { url: URL }) => url.pathname.startsWith('/api/characters'),
  new NetworkFirst({
    cacheName: 'api-characters',
    networkTimeoutSeconds: 5,
    plugins: [new ExpirationPlugin({ maxEntries: 50, maxAgeSeconds: 86400 })],
  })
);

// ---------------------------------------------------------------------------
// Web Push — display incoming push messages as notifications
// ---------------------------------------------------------------------------

self.addEventListener('push', (event: PushEvent) => {
  if (!event.data) return;

  let title = 'SwAIvyn';
  let body = '';
  let icon = '/pwa-192.png';

  try {
    const data = event.data.json() as { title?: string; body?: string; icon?: string };
    title = data.title ?? title;
    body = data.body ?? body;
    icon = data.icon ?? icon;
  } catch {
    body = event.data.text();
  }

  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      icon,
      badge: '/pwa-192.png',
    })
  );
});

// ---------------------------------------------------------------------------
// Notification click — focus an existing window or open a new one
// ---------------------------------------------------------------------------

self.addEventListener('notificationclick', (event: NotificationEvent) => {
  event.notification.close();

  event.waitUntil(
    self.clients
      .matchAll({ type: 'window', includeUncontrolled: true })
      .then((clientList) => {
        for (const client of clientList) {
          if ('focus' in client) return client.focus();
        }
        return self.clients.openWindow('/');
      })
  );
});
