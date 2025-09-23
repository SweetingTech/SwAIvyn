export interface RuntimeConfig {
  apiBaseUrl: string;
  stagewiseEnabled: boolean;
  isDevelopment: boolean;
}

const env = import.meta.env as Record<string, unknown>;

const rawApiBase = typeof env.VITE_API_BASE_URL === 'string' ? env.VITE_API_BASE_URL.trim() : '';

function normalizeApiBase(value: string): string {
  if (!value) return '';
  try {
    const url = new URL(value);
    return url.origin;
  } catch (error) {
    throw new Error(`VITE_API_BASE_URL is invalid: ${(error as Error).message}`);
  }
}

function parseBooleanFlag(value: unknown): boolean {
  if (typeof value === 'boolean') return value;
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    if (['true', '1', 'yes', 'on'].includes(normalized)) return true;
    if (['false', '0', 'no', 'off', ''].includes(normalized)) return false;
  }
  return false;
}

const rawDev = env.DEV;
const isDevelopment = typeof rawDev === 'boolean' ? rawDev : rawDev === 'true';

export const runtimeConfig: RuntimeConfig = {
  apiBaseUrl: normalizeApiBase(rawApiBase),
  stagewiseEnabled: parseBooleanFlag(env.VITE_STAGEWISE_ENABLED),
  isDevelopment,
};

export function withApiBase(path: string): string {
  if (!runtimeConfig.apiBaseUrl) {
    return path;
  }
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${runtimeConfig.apiBaseUrl}${normalizedPath}`;
}
