import { useState, useEffect, useCallback } from 'react';
import pushService from '../services/pushService';

interface UsePushNotificationsResult {
  isSupported: boolean;
  isEnabled: boolean;
  isLoading: boolean;
  enable: () => Promise<void>;
  disable: () => Promise<void>;
}

/**
 * Hook that manages Web Push notification subscription state.
 */
export function usePushNotifications(): UsePushNotificationsResult {
  const isSupported =
    typeof window !== 'undefined' &&
    'serviceWorker' in navigator &&
    'PushManager' in window;

  const [isEnabled, setIsEnabled] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  // Check current subscription state on mount
  useEffect(() => {
    if (!isSupported) return;
    pushService.isPushEnabled().then(setIsEnabled).catch(() => setIsEnabled(false));
  }, [isSupported]);

  const enable = useCallback(async () => {
    setIsLoading(true);
    try {
      const sub = await pushService.subscribeToPush();
      setIsEnabled(sub !== null);
    } catch (err) {
      console.error('[usePushNotifications] Failed to enable:', err);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const disable = useCallback(async () => {
    setIsLoading(true);
    try {
      await pushService.unsubscribeFromPush();
      setIsEnabled(false);
    } catch (err) {
      console.error('[usePushNotifications] Failed to disable:', err);
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { isSupported, isEnabled, isLoading, enable, disable };
}

export default usePushNotifications;
