/**
 * Push Notification Service
 *
 * Manages Web Push subscriptions for the mobile PWA companion.
 * Uses the VAPID-based Web Push API via the BFF backend.
 */

import apiService from './apiService';

export interface PushSubscriptionPayload {
  endpoint: string;
  keys: {
    p256dh: string;
    auth: string;
  };
}

/**
 * Fetch the server's VAPID public key.
 */
async function getVapidPublicKey(): Promise<string> {
  const res = await apiService.get<{ vapid_public_key: string }>('/api/push/vapid-public-key');
  return res.data.vapid_public_key;
}

/**
 * Convert a URL-safe base64 string to a Uint8Array (required by
 * PushManager.subscribe for applicationServerKey).
 */
function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = window.atob(base64);
  return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0)));
}

/**
 * Serialize a PushSubscription into the payload shape expected by the BFF.
 */
function serializeSubscription(sub: PushSubscription): PushSubscriptionPayload {
  const json = sub.toJSON();
  return {
    endpoint: json.endpoint!,
    keys: {
      p256dh: json.keys?.p256dh ?? '',
      auth: json.keys?.auth ?? '',
    },
  };
}

/**
 * Request permission + subscribe to push notifications.
 * Returns the serialized subscription, or null if the user denied permission
 * or the browser doesn't support push.
 */
export async function subscribeToPush(): Promise<PushSubscriptionPayload | null> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
    console.warn('[push] Push notifications are not supported in this browser.');
    return null;
  }

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    console.info('[push] Notification permission denied.');
    return null;
  }

  const registration = await navigator.serviceWorker.ready;
  const vapidKey = await getVapidPublicKey();

  let subscription = await registration.pushManager.getSubscription();
  if (!subscription) {
    subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidKey),
    });
  }

  const payload = serializeSubscription(subscription);

  // Register the subscription with the BFF
  await apiService.post('/api/push/subscribe', payload);

  return payload;
}

/**
 * Unsubscribe from push notifications and remove the subscription from the BFF.
 */
export async function unsubscribeFromPush(): Promise<void> {
  if (!('serviceWorker' in navigator)) return;

  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (!subscription) return;

  const payload = serializeSubscription(subscription);
  await subscription.unsubscribe();

  try {
    await apiService.post('/api/push/unsubscribe', { endpoint: payload.endpoint });
  } catch {
    // Best-effort cleanup
  }
}

/**
 * Check whether push notifications are currently enabled.
 */
export async function isPushEnabled(): Promise<boolean> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;
  if (Notification.permission !== 'granted') return false;

  const registration = await navigator.serviceWorker.ready;
  const sub = await registration.pushManager.getSubscription();
  return sub !== null;
}

const pushService = { subscribeToPush, unsubscribeFromPush, isPushEnabled };
export default pushService;
